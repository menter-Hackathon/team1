using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ボスの状態</summary>
public enum BossState
{
    /// <summary>戦闘中</summary>
    Fighting = 0,
    /// <summary>形態変化の演出中（アップデート中…）。無敵</summary>
    Transition = 1,
    /// <summary>正常化した（ウイルス本体にとどめを刺した。追い出しを使わないボスは、ゲージ100%ですぐこれになる）。もう攻撃しない</summary>
    Normalized = 2,
    /// <summary>ゲージ100%でウイルス本体が追い出され、逃げ回っている（とどめ待ち）。ボスは攻撃しない（docs 3章、11/17 決定）</summary>
    Ejected = 3,
}

/// <summary>
/// アプリボスの簡単なAIと、ウイルス解除ゲージによる形態変化・正常化（docs 3章）。
/// ・技を順番に出す。同じ技は2回続けない（技が2つなら交互）。攻撃が終わったら待ち時間（第1形態1.2秒、第2形態0.8秒）
/// ・決まった回数攻撃するごとに移動する（飛んで移動／瞬間移動）
/// ・ゲージが50%になったら形態変化：1.5秒の演出（アップデート中…）、その間は無敵、画面上のボスの弾を消す
/// ・ゲージが100%になったら：攻撃をやめ、出ている攻撃を消し、体当たりもなくなる。
///   ウイルス本体（子の「追い出されるウイルス」）がアプリから追い出されて逃げ回る（State＝Ejected）。
///   プレイヤーが1回当てるとはじけて消え、そこで正常化：そのアプリの能力をプレイヤーに渡す（空き枠があれば入る。満杯なら入れ替え待ち）
///   （docs 3章、2026-11-17 決定。追い出されるウイルスが設定されていなければ、今までどおりゲージ100%ですぐ正常化）
/// 必要なもの：AttackRunner（攻撃）、VirusGauge（ゲージ）、体当たりの Hitbox、被弾範囲の Hurtbox
/// </summary>
[RequireComponent(typeof(AttackRunner), typeof(VirusGauge))]
public class BossController : MonoBehaviour, IWarpable
{
    [SerializeField] BossProfile _profile;
    [Tooltip("体当たりの判定")]
    [SerializeField] Hitbox _contactHitbox;
    [Tooltip("被弾範囲")]
    [SerializeField] Hurtbox _hurtbox;
    [Tooltip("見た目。空なら子のスプライトすべて")]
    [SerializeField] SpriteRenderer[] _renderers;
    [Tooltip("【仮】正常化したときの色")]
    [SerializeField] Color _normalizedColor = new Color(0.55f, 0.95f, 0.6f);
    [Tooltip("オン：見た目の色（点滅・消える演出・当たったときの光）は別の部品（BossArmorVisual）が受け持つ。ここでは色を変えない")]
    [SerializeField] bool _externalVisuals;
    [Tooltip("ゲージ100%で追い出されて逃げ回るウイルス本体（子に非表示で置く。HP1、体当たりなし）。空なら、ゲージ100%ですぐ正常化")]
    [SerializeField] Health _ejectedVirus;

    AttackRunner _runner;
    VirusGauge _gauge;
    Coroutine _brain;
    Coroutine _transition;
    Vector3 _startPosition;
    AttackData _lastMove;
    int _attackCount;
    int _hoverIndex;
    Color[] _baseColors;
    float _visibility = 1f;
    float _flashUntil = float.NegativeInfinity;
    bool _started;
    bool _openingDone;    // 第2形態の大技を出したか（1回の戦いで1回だけ）
    bool _inSpecial;      // 大技を出している途中（エラーで止まらない）
    float _nextErrorAt;   // 次にエラーが起きうる時刻

    public BossProfile Profile => _profile;
    public AttackRunner Runner => _runner;
    public VirusGauge Gauge => _gauge;
    /// <summary>今の形態（1 か 2）</summary>
    public int Phase { get; private set; } = 1;
    public BossState State { get; private set; } = BossState.Fighting;
    /// <summary>形態変化の演出の進み具合（0〜1）。演出中でなければ 0</summary>
    public float TransitionProgress { get; private set; }
    /// <summary>移動中なら true</summary>
    public bool IsMoving { get; private set; }
    /// <summary>見え方（1＝見える、0＝消えている）。瞬間移動・登場の点滅で変わる。ウイルスの煙など、別に描く絵が合わせる</summary>
    public float Visibility => _visibility;

    /// <summary>技を出し始めたとき（技のデータ）</summary>
    public event Action<AttackData> MoveStarted;
    /// <summary>移動を始めたとき</summary>
    public event Action MovementStarted;
    /// <summary>形態変化の演出が始まったとき</summary>
    public event Action TransitionStarted;
    /// <summary>形態が変わったとき（新しい形態）</summary>
    public event Action<int> PhaseChanged;
    /// <summary>正常化したとき（手に入った能力。設定が無ければ null）</summary>
    public event Action<SkillDefinition> Normalized;
    /// <summary>ゲージ100%で、ウイルス本体が追い出されたとき（このあと、とどめを待つ）</summary>
    public event Action Ejected;
    /// <summary>追い出されたウイルス本体（逃げている間だけ表示）</summary>
    public Health EjectedVirus => _ejectedVirus;
    /// <summary>エラーで止まっている間 true（2026-11-19）</summary>
    bool _inError;
    /// <summary>第2形態の大技を出している途中なら true</summary>
    public bool IsUsingSpecial => _inSpecial;
    /// <summary>エラーで止まったとき（止まる秒数）。「error」の表示用</summary>
    public event Action<float> ErrorRaised;
    /// <summary>正常化したあとのきれいなアイコン（リザルトなどで使う）。設定が無ければ null</summary>
    public Sprite NormalizedIcon => _profile != null ? _profile.NormalIcon : null;
    /// <summary>正常化して手に入った能力（まだなら null）</summary>
    public SkillDefinition AcquiredReward { get; private set; }
    /// <summary>
    /// 正常化した瞬間に、報酬のスキルをプレイヤーに直接入れるか（既定 true＝今までどおり。テスト用のシーンはこのまま）。
    /// StageDirector だけが false にして、スキルを落とす（RewardDrop → 入れ替えの画面。2026-11-20）
    /// </summary>
    public bool AutoAcquireReward { get; set; } = true;

    IDisposable _passive; // ボスの常に効く効果（Steam ボスの実績解除）
    int _sequenceIndex;   // 技を順に出すときの、次の番号

    void Awake()
    {
        _runner = GetComponent<AttackRunner>();
        _gauge = GetComponent<VirusGauge>();
        if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
        _startPosition = transform.position;
        _hoverIndex = NearestHoverIndex();
        if (_profile != null) _gauge.SetMax(_profile.GaugeMax);
    }

    void OnEnable()
    {
        _gauge.Changed += OnGaugeChanged;
        _gauge.Gained += OnGaugeGained;
        if (_started) ResetBoss(); // 一度隠して出し直したときは最初から
    }

    void OnDisable()
    {
        _gauge.Changed -= OnGaugeChanged;
        _gauge.Gained -= OnGaugeGained;
        if (_ejectedVirus != null) _ejectedVirus.Died -= OnEjectedVirusDied;
        StopAll();
        _runner.ClearSpawned();
        DetachPassive();
    }

    void Start()
    {
        _started = true;
        // 狙う相手が空なら、シーンのプレイヤーを狙う（プレハブをそのままシーンに置いたとき用。2026-11-19）
        if (_runner.Target == null)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null) _runner.Target = player.transform;
        }
        ResetBoss();
    }

    void Update()
    {
        // 正面＝プレイヤーのいる方
        if (_runner.Target != null) _runner.Facing = _runner.Target.position.x >= transform.position.x ? 1 : -1;
    }

    void LateUpdate()
    {
        if (_externalVisuals) return;
        // 色はここでまとめて決める（正常化の色・被弾の点滅・形態変化中の点滅・消える途中の薄さ）
        float now = Time.time;
        bool flash = now < _flashUntil;
        float alpha = _visibility;
        if (State == BossState.Transition) alpha *= Mathf.FloorToInt(now / 0.12f) % 2 == 0 ? 1f : 0.45f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = State == BossState.Normalized && i == 0 ? _normalizedColor : _baseColors[i];
            if (flash) c = Color.Lerp(c, Color.white, 0.7f);
            c.a = _baseColors[i].a * alpha;
            _renderers[i].color = c;
        }
    }

    /// <summary>
    /// 設定を差し替える（M5：使用時間で強さを変えた複製を渡す）。
    /// 出てくる前（非表示のうち）に呼ぶのが基本。戦闘中に呼んだら最初からやり直す
    /// </summary>
    public void SetProfile(BossProfile profile)
    {
        _profile = profile;
        if (_gauge != null && profile != null) _gauge.SetMax(profile.GaugeMax);
        AttachExtensions();
        if (_started && isActiveAndEnabled) ResetBoss();
    }

    /// <summary>最初の状態に戻して、戦闘を始め直す（ゲージ0、第1形態、最初の位置）</summary>
    public void ResetBoss()
    {
        StopAll();
        _runner.ClearSpawned();
        if (_profile != null) _gauge.SetMax(_profile.GaugeMax);
        else _gauge.ResetGauge();
        Phase = 1;
        State = BossState.Fighting;
        TransitionProgress = 0f;
        transform.position = _startPosition;
        _hoverIndex = NearestHoverIndex();
        _lastMove = null;
        _attackCount = 0;
        _openingDone = false;
        _nextErrorAt = 0f;
        _sequenceIndex = 0;
        AcquiredReward = null;
        AttachPassive();
        SetVisibility(1f);
        SetContactEnabled(true);
        HideEjectedVirus();
        if (_profile != null && isActiveAndEnabled) _brain = StartCoroutine(Brain(_profile.StartDelay));
    }

    void AttachPassive()
    {
        DetachPassive();
        if (_profile != null && _profile.Passive != null && isActiveAndEnabled) _passive = _profile.Passive.Attach(gameObject);
    }

    void DetachPassive()
    {
        _passive?.Dispose();
        _passive = null;
    }

    void StopAll()
    {
        // 行動の中の段階（移動など）も入れ子のコルーチンなので、まとめて止める
        StopAllCoroutines();
        MoveSpeedMultiplier = 1f;
        WaitMultiplier = 1f;
        _stunUntil = 0f;
        _brain = _transition = null;
        IsMoving = false;
        _inError = false;
        _inSpecial = false;
        if (_runner != null) _runner.Cancel();
    }

    /// <summary>
    /// 【仮】ボスの速さの倍率（2026-11-18：Premiere Pro のスピードデュレーション）。飛んで移動する速さに掛ける。
    /// 予告・弾の速さ・判定は変えない（docs/app-boss-skills-design.md 3-4）。変えた技が、終わるときに 1 に戻す
    /// </summary>
    public float MoveSpeedMultiplier { get; set; } = 1f;
    /// <summary>【仮】攻撃のあとの待ち時間の倍率（2026-11-18：スピードデュレーション）。変えた技が、終わるときに 1 に戻す</summary>
    public float WaitMultiplier { get; set; } = 1f;

    // ───────── 2026-11-19：ひるむ（アプリのキット E5。KitBoss.Stun） ─────────
    float _stunUntil;

    /// <summary>
    /// 【仮】seconds 秒、次の技を始めない・移動しない（技の途中は止めない）。
    /// 続けてかけても伸びない（残りの時間が seconds に戻るだけ）。戦っている間だけ効く
    /// </summary>
    public void Stun(float seconds)
    {
        if (State != BossState.Fighting || seconds <= 0f) return;
        _stunUntil = Time.time + seconds;
    }

    /// <summary>ひるんでいるか（キットの Stun でひるんでいる間、またはエラーで止まっている間）</summary>
    public bool IsStunned => _inError || (State == BossState.Fighting && Time.time < _stunUntil);
    /// <summary>ひるみ（キットの Stun）の残り秒数</summary>
    public float StunRemaining => State == BossState.Fighting && Time.time < _stunUntil ? _stunUntil - Time.time : 0f;
    /// <summary>技の途中か（予告も含む）</summary>
    public bool InMove => _runner != null && _runner.IsRunning;

    readonly System.Collections.Generic.HashSet<BossExtension> _attached = new System.Collections.Generic.HashSet<BossExtension>();

    /// <summary>プロフィールの「ボスに足す仕組み」を、まだ足していなければ足す</summary>
    void AttachExtensions()
    {
        if (_profile == null || _profile.Extensions == null) return;
        foreach (var ext in _profile.Extensions)
        {
            if (ext == null || !_attached.Add(ext)) continue;
            ext.Attach(this);
        }
    }

    // ───────── 行動 ─────────

    IEnumerator Brain(float initialDelay)
    {
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            while (IsStunned) yield return null; // ひるんでいる間は、次の技を始めない（2026-11-19）
            BossPhaseSettings phase = _profile.GetPhase(Phase);
            // 第2形態になって最初の1回は、大技（設定されていれば。1回の戦いで1回だけ）
            bool special = Phase >= 2 && !_openingDone && _profile.Phase2OpeningMove != null;
            AttackData move = special ? _profile.Phase2OpeningMove
                : phase.Sequential ? PickSequential(phase.Moves) : PickMove(phase.Moves);
            if (special) _openingDone = true;
            if (move != null)
            {
                if (!special) _lastMove = move;
                _inSpecial = special;
                MoveStarted?.Invoke(move);
                if (_runner.Run(move))
                {
                    while (_runner.IsRunning) yield return null;
                }
                _inSpecial = false;
                _attackCount++;
                // 大技のあとは、いつもの場所へ戻る（大技で部屋の端へ寄ることがあるため）
                if (special) yield return Reposition();
            }

            if (move != null && phase.AttacksPerMove > 0 && _attackCount > 0 && _attackCount % phase.AttacksPerMove == 0)
            {
                while (IsStunned) yield return null; // ひるんでいる間は、移動しない（2026-11-19）
                yield return Reposition();
            }
            yield return new WaitForSeconds(phase.WaitAfterAttack * Mathf.Max(0.05f, WaitMultiplier * GameBuffs.BossWait));
        }
    }

    /// <summary>
    /// 同じ技が2回続かないように選ぶ。最初は一覧の先頭。
    /// 今は出せない技（場に残る技の効果が出ている間の、場に残る技。床が文字化けしている間の、床に逃げ場が要る技）は飛ばす。
    /// 出せる技が無ければ null（待ち時間をもう1回おく）（2026-11-18：docs/app-boss-skills-design.md 1-3）
    /// </summary>
    AttackData PickMove(AttackData[] moves)
    {
        if (moves == null || moves.Length == 0) return null;
        bool lingering = CustomEffect.AnyLingering;
        bool floorCorrupted = CustomEffect.AnyFloorCorrupted;
        bool Usable(AttackData m) => m != null && !(lingering && m.Lingering) && !(floorCorrupted && m.AvoidWhileFloorCorrupted);

        if (_lastMove == null || Array.IndexOf(moves, _lastMove) < 0)
        {
            foreach (var m in moves) if (Usable(m)) return m;
            return null;
        }
        var candidates = new List<AttackData>();
        foreach (var m in moves)
        {
            if (Usable(m) && m != _lastMove) candidates.Add(m);
        }
        if (candidates.Count == 0) return Usable(_lastMove) ? _lastMove : null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>技を一覧の順に出す（BossPhaseSettings.Sequential のとき）</summary>
    AttackData PickSequential(AttackData[] moves)
    {
        if (moves == null || moves.Length == 0) return null;
        AttackData move = moves[_sequenceIndex % moves.Length];
        _sequenceIndex = (_sequenceIndex + 1) % moves.Length;
        return move;
    }

    IEnumerator Reposition()
    {
        switch (_profile.Movement)
        {
            case BossMovement.Warp:
                if (_profile.WarpAttack == null) yield break;
                IsMoving = true;
                MovementStarted?.Invoke();
                if (_runner.Run(_profile.WarpAttack))
                {
                    while (_runner.IsRunning) yield return null;
                }
                IsMoving = false;
                break;

            case BossMovement.Hover:
                float[] points = _profile.HoverPointsX;
                if (points == null || points.Length < 2) yield break;
                // 【仮】今いる場所以外から、ランダムに選ぶ
                int next = UnityEngine.Random.Range(0, points.Length - 1);
                if (next >= _hoverIndex) next++;
                _hoverIndex = next;
                // 高さ：設定があればその高さ（部屋の床から）、無ければ最初の高さへ戻す
                // （大技で部屋の端へ寄ったあとも、元の高さに戻る。2026-11-19。高さの設定が無いボスは今までどおり）
                Rect arena = BattleArena.GetBounds();
                float[] ys = _profile.HoverPointsY;
                float targetY = ys != null && next < ys.Length ? arena.yMin + ys[next] : _startPosition.y;
                var target = new Vector2(arena.center.x + points[next], targetY);
                IsMoving = true;
                MovementStarted?.Invoke();
                while (((Vector2)transform.position - target).sqrMagnitude > 0.0001f)
                {
                    if (IsStunned) { yield return null; continue; } // 移動の途中でひるんだら、その場で止まる（2026-11-19）
                    float speed = _profile.HoverSpeed * Mathf.Max(0.05f, MoveSpeedMultiplier * GameBuffs.BossSpeed);
                    Vector2 p = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
                    transform.position = new Vector3(p.x, p.y, transform.position.z);
                    yield return null;
                }
                IsMoving = false;
                break;
        }
    }

    int NearestHoverIndex()
    {
        if (_profile == null || _profile.HoverPointsX == null || _profile.HoverPointsX.Length == 0) return 0;
        float cx = BattleArena.GetBounds().center.x;
        int best = 0;
        for (int i = 1; i < _profile.HoverPointsX.Length; i++)
        {
            if (Mathf.Abs(cx + _profile.HoverPointsX[i] - transform.position.x) < Mathf.Abs(cx + _profile.HoverPointsX[best] - transform.position.x)) best = i;
        }
        return best;
    }

    // ───────── ゲージ・形態変化・正常化 ─────────

    void OnGaugeGained(DamageInfo _)
    {
        _flashUntil = Time.time + 0.1f;
        TryRaiseError();
    }

    /// <summary>
    /// ダメージ（ゲージが増える攻撃）を受けたとき、設定の確率でエラーになって少し止まる（2026-11-19：AI チャットのボス）。
    /// 戦闘中だけ。大技の途中・エラーの直後（次に起きうるまでの秒数）は起きない。出している途中の技は止まり、出した弾・予告は残る
    /// </summary>
    void TryRaiseError()
    {
        if (_profile == null || _profile.ErrorChance <= 0f || State != BossState.Fighting) return;
        if (IsStunned || _inSpecial || Time.time < _nextErrorAt) return;
        if (UnityEngine.Random.value >= _profile.ErrorChance) return;

        StopAll();
        _runner.ClearSpawnedWith<WarpTelegraph>(); // 止めた瞬間移動の予告も消す（現れないので）
        SetVisibility(1f);                         // 瞬間移動の途中で止まっても、元の姿に戻す
        SetContactEnabled(true);
        _brain = StartCoroutine(ErrorRoutine(_profile.ErrorStunTime));
    }

    IEnumerator ErrorRoutine(float duration)
    {
        _inError = true;
        ErrorRaised?.Invoke(duration);
        if (duration > 0f) yield return new WaitForSeconds(duration);
        _inError = false;
        _nextErrorAt = Time.time + _profile.ErrorCooldown;
        yield return Brain(_profile.GetPhase(Phase).WaitAfterAttack * 0.5f);
    }

    void OnGaugeChanged(int current, int max)
    {
        if (State == BossState.Normalized || State == BossState.Ejected || _profile == null) return;
        if (current >= max)
        {
            if (_ejectedVirus != null) Eject();
            else Normalize();
            return;
        }
        if (Phase == 1 && State == BossState.Fighting && current >= max * _profile.Phase2Threshold)
        {
            BeginTransition();
        }
    }

    void BeginTransition()
    {
        StopAll();
        _runner.ClearProjectiles(); // 画面上のボスの弾だけを消す（落下物・ダメージ床は残る）
        _runner.ClearSpawnedWith<WarpTelegraph>(); // 止めた瞬間移動の予告も消す（現れないので）
        _runner.ClearSpawnedWith<CustomEffect>(); // 専用の技が場に残した物も消し、地形・プレイヤーの状態を戻す（2026-11-18）
        SetVisibility(1f);          // 瞬間移動の途中で止まっても、元の姿に戻す
        SetContactEnabled(true);
        State = BossState.Transition;
        _gauge.Invincible = true;
        TransitionStarted?.Invoke();
        GameAudio.Event("boss_phase2");
        _transition = StartCoroutine(TransitionRoutine());
    }

    IEnumerator TransitionRoutine()
    {
        float duration = _profile.TransitionTime;
        float start = Time.time;
        while (Time.time - start < duration)
        {
            TransitionProgress = Mathf.Clamp01((Time.time - start) / duration);
            yield return null;
        }
        TransitionProgress = 0f;
        Phase = 2;
        State = BossState.Fighting;
        _gauge.Invincible = false;
        _attackCount = 0;
        _transition = null;
        PhaseChanged?.Invoke(Phase);
        GameAudio.BossPhase(Phase);
        _brain = StartCoroutine(Brain(_profile.Phase2.WaitAfterAttack));
    }

    /// <summary>ゲージ100%：攻撃をやめ、ウイルス本体を追い出す。本体にとどめを刺したら正常化</summary>
    void Eject()
    {
        StopAll();
        _runner.ClearSpawned();
        SetVisibility(1f);
        SetContactEnabled(false); // 体当たりも被弾もなし（State を変える前に呼ぶと、体当たりが切れる）
        State = BossState.Ejected;
        TransitionProgress = 0f;
        DetachPassive(); // 追い出したら、ボスの実績も止める

        // ボスの真ん中から、プレイヤーと反対の向きへ走り出す
        Transform v = _ejectedVirus.transform;
        v.SetParent(null, true);
        v.position = BodyBounds.center;
        _ejectedVirus.ResetHealth();
        _ejectedVirus.gameObject.SetActive(true);
        if (v.TryGetComponent(out ZakoWalker walker))
        {
            float px = _runner.Target != null ? _runner.Target.position.x : transform.position.x - 1f;
            walker.SetDirection(v.position.x >= px ? 1 : -1);
        }
        _ejectedVirus.Died -= OnEjectedVirusDied;
        _ejectedVirus.Died += OnEjectedVirusDied;
        Ejected?.Invoke();
    }

    void OnEjectedVirusDied()
    {
        _ejectedVirus.Died -= OnEjectedVirusDied;
        if (State == BossState.Ejected) Normalize();
    }

    /// <summary>
    /// 追い出されるウイルスを隠す（やり直し用）。親には戻さない（ボスを表示する途中で親を変えるとエラーになるため）。
    /// 次に追い出すときは、Eject がボスの真ん中へ動かす
    /// </summary>
    void HideEjectedVirus()
    {
        if (_ejectedVirus == null) return;
        _ejectedVirus.Died -= OnEjectedVirusDied;
        if (_ejectedVirus.gameObject.activeSelf) _ejectedVirus.gameObject.SetActive(false);
    }

    void Normalize()
    {
        StopAll();
        _runner.ClearSpawned();
        SetVisibility(1f);
        SetContactEnabled(false); // 体当たりも被弾もなし
        State = BossState.Normalized;
        TransitionProgress = 0f;
        DetachPassive();

        SkillDefinition reward = _profile.Reward;
        if (reward != null && AutoAcquireReward)
        {
            PlayerSkills skills = _runner.Target != null ? _runner.Target.GetComponentInParent<PlayerSkills>() : null;
            if (skills == null) skills = FindObjectOfType<PlayerSkills>();
            if (skills != null) skills.TryAcquire(reward);
        }
        GameAudio.BossNormalized();
        Normalized?.Invoke(reward);
    }

    // ───────── IWarpable ─────────

    public Bounds BodyBounds => _hurtbox != null ? _hurtbox.GetComponent<Collider2D>().bounds : new Bounds(transform.position, Vector3.one);

    public void SetVisibility(float alpha) => _visibility = Mathf.Clamp01(alpha);

    public void SetContactEnabled(bool enabled)
    {
        bool on = enabled && State != BossState.Normalized;
        if (_contactHitbox != null)
        {
            if (on) _contactHitbox.Activate();
            else _contactHitbox.Deactivate();
        }
        if (_hurtbox != null) _hurtbox.GetComponent<Collider2D>().enabled = on;
    }

    public void Teleport(Vector2 position) => transform.position = position;

    /// <summary>体当たりの判定が出ているか（テスト用）</summary>
    public bool IsContactActive => _contactHitbox != null && _contactHitbox.IsActive;
}
