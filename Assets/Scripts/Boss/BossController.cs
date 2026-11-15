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
    /// <summary>正常化した（ゲージ100%）。もう攻撃しない</summary>
    Normalized = 2,
}

/// <summary>
/// アプリボスの簡単なAIと、ウイルス解除ゲージによる形態変化・正常化（docs 3章）。
/// ・技を順番に出す。同じ技は2回続けない（技が2つなら交互）。攻撃が終わったら待ち時間（第1形態1.2秒、第2形態0.8秒）
/// ・決まった回数攻撃するごとに移動する（飛んで移動／瞬間移動）
/// ・ゲージが50%になったら形態変化：1.5秒の演出（アップデート中…）、その間は無敵、画面上のボスの弾を消す
/// ・ゲージが100%になったら正常化：攻撃をやめ、出ている攻撃を消し、体当たりもなくなる。そのアプリの能力をプレイヤーに渡す
///   （空き枠があれば入る。満杯なら入れ替え待ち）
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
        StopAll();
        _runner.ClearSpawned();
    }

    void Start()
    {
        _started = true;
        ResetBoss();
    }

    void Update()
    {
        // 正面＝プレイヤーのいる方
        if (_runner.Target != null) _runner.Facing = _runner.Target.position.x >= transform.position.x ? 1 : -1;
    }

    void LateUpdate()
    {
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
        SetVisibility(1f);
        SetContactEnabled(true);
        if (_profile != null && isActiveAndEnabled) _brain = StartCoroutine(Brain(_profile.StartDelay));
    }

    void StopAll()
    {
        // 行動の中の段階（移動など）も入れ子のコルーチンなので、まとめて止める
        StopAllCoroutines();
        _brain = _transition = null;
        IsMoving = false;
        if (_runner != null) _runner.Cancel();
    }

    // ───────── 行動 ─────────

    IEnumerator Brain(float initialDelay)
    {
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            BossPhaseSettings phase = _profile.GetPhase(Phase);
            AttackData move = PickMove(phase.Moves);
            if (move != null)
            {
                _lastMove = move;
                MoveStarted?.Invoke(move);
                if (_runner.Run(move))
                {
                    while (_runner.IsRunning) yield return null;
                }
                _attackCount++;
            }

            if (phase.AttacksPerMove > 0 && _attackCount > 0 && _attackCount % phase.AttacksPerMove == 0)
            {
                yield return Reposition();
            }
            yield return new WaitForSeconds(phase.WaitAfterAttack);
        }
    }

    /// <summary>同じ技が2回続かないように選ぶ。最初は一覧の先頭</summary>
    AttackData PickMove(AttackData[] moves)
    {
        if (moves == null || moves.Length == 0) return null;
        if (_lastMove == null || Array.IndexOf(moves, _lastMove) < 0) return moves[0];
        var candidates = new List<AttackData>();
        foreach (var m in moves)
        {
            if (m != null && m != _lastMove) candidates.Add(m);
        }
        if (candidates.Count == 0) return _lastMove;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
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
                float targetX = BattleArena.GetBounds().center.x + points[next];
                IsMoving = true;
                MovementStarted?.Invoke();
                while (!Mathf.Approximately(transform.position.x, targetX))
                {
                    Vector3 p = transform.position;
                    p.x = Mathf.MoveTowards(p.x, targetX, _profile.HoverSpeed * Time.deltaTime);
                    transform.position = p;
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

    void OnGaugeGained(DamageInfo _) => _flashUntil = Time.time + 0.1f;

    void OnGaugeChanged(int current, int max)
    {
        if (State == BossState.Normalized || _profile == null) return;
        if (current >= max)
        {
            Normalize();
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
        SetVisibility(1f);          // 瞬間移動の途中で止まっても、元の姿に戻す
        SetContactEnabled(true);
        State = BossState.Transition;
        _gauge.Invincible = true;
        TransitionStarted?.Invoke();
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
        _brain = StartCoroutine(Brain(_profile.Phase2.WaitAfterAttack));
    }

    void Normalize()
    {
        StopAll();
        _runner.ClearSpawned();
        SetVisibility(1f);
        SetContactEnabled(false); // 体当たりも被弾もなし
        State = BossState.Normalized;
        TransitionProgress = 0f;

        SkillDefinition reward = _profile.Reward;
        if (reward != null)
        {
            PlayerSkills skills = _runner.Target != null ? _runner.Target.GetComponentInParent<PlayerSkills>() : null;
            if (skills == null) skills = FindObjectOfType<PlayerSkills>();
            if (skills != null) skills.TryAcquire(reward);
        }
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
