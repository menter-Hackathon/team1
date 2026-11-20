using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 【仮】ルートキット（2026-11-20。docs/virus-types-design.md 3章・7章 C）。床の下にかくれて出てくる「もぐらたたき」の雑魚。
/// ・もぐる（コマ 0：割れ目と赤い目だけ）→ 予告（ふるえる＋灰色のかけら 3つ）→ 出る（コマ 1）→ 出ている（コマ 2。動かない）→ しずむ（コマ 1 → 0）をくり返す
/// ・もぐっている間は、割れ目ごとプレイヤーの x の方へすべる。もぐるのは 2.0秒まで（手の届かない敵を作らない）
/// ・当たりの入り切りは Visual/Body（Hurtbox と体当たりの Hitbox）の SetActive。予告の間もまだ当たらない
/// ・立つ面は、動かない床・足場の上だけ（AppStagePlatforms.RaycastSteady）。その左右の端から 0.5マス内側まで
/// ・面が見つからないときは、歩く雑魚にもどる（ZakoWalker を入れる。見た目はコマ 2 のまま）
/// ひな形は StagePacer.Started で作る（名前 "Template_Rootkit"。ZakoWalker は止めるが外さない）。
/// 何か起きたら、かならず「当たる」方へ倒す（かくれたまま残ると、1画面のステージがクリアできない）。数値はすべて【仮】
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class ZakoRootkit : MonoBehaviour
{
    public enum Phase { Hidden, Warn, Rising, Up, Sinking, Walking }

    public const string TypeName = "Rootkit";

    [Tooltip("もぐっている秒数（この間でランダム。上限 2.0）")]
    [SerializeField] Vector2 _hideTime = new Vector2(1.2f, 2.0f);
    [SerializeField] float _warnTime = 0.7f;
    [SerializeField] float _riseTime = 0.15f;
    [Tooltip("出ている秒数（残り 3体以下なら 2倍）")]
    [SerializeField] float _upTime = 2.2f;
    [SerializeField] float _sinkTime = 0.25f;
    [Tooltip("もぐっている間、すべる速さ（マス／秒）")]
    [SerializeField] float _slideSpeed = 3.5f;

    const float MaxHide = 2.0f;       // もぐるのは、つづけてここまで（かたい決まり）
    const float StopDistance = 0.3f;  // プレイヤーとの横の差がこれ以下なら、すべるのを止める
    const float EdgeMargin = 0.5f;    // 面の端・壁から、これだけ内側まで
    const float Px = 1f / 18f;        // 1ドット
    const int ChipCount = 3;
    const float ChipInterval = 0.2f;

    static readonly Color ChipGrey = RuntimeSprites.Hex("8E8E93");
    static readonly float[] Sway = { 0f, 1f, 0f, -1f }; // 出ている間、茎の上でゆれる（ドット）

    /// <summary>今の状態（確認用。撮るときのログに出す）</summary>
    public Phase State { get; private set; } = Phase.Hidden;

    Health _health;
    Rigidbody2D _rb;
    KinematicBody2D _kb;
    ZakoWalker _walker;
    Transform _visual;
    GameObject _body;
    SpriteRenderer _art;
    SpriteFrameAnimation _anim;
    Sprite[] _frames;

    Collider2D _surface;
    AppPlatform _surfacePlatform;
    Vector2 _surfaceCenter;
    float _surfaceY;
    float _minX;
    float _maxX;

    float _stateAt;
    float _stateLen;
    float _hiddenSince;  // 当たらなくなった時刻（見張り用）
    bool _hittable = true;
    int _chips;
    int _face = 1;
    float _phase;
    float _nextTry;
    float _nextFind;
    bool _ready;
    bool _failed;        // 例外が出た・部品が足りない：ずっと歩く雑魚のまま
    int _mask;
    int _wallMask;
    Transform _player;

    // ───────── ひな形（StagePacer.Started で作る） ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        StagePacer.Started -= OnStarted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static void OnStarted(StagePacer pacer)
    {
        GameObject t = null;
        try
        {
            if (!VirusArt.Enabled || pacer == null) return;
            ZakoSpawner spawner = pacer.Spawner;
            if (spawner == null || VirusArt.IndexOf(spawner, TypeName) >= 0) return; // もうある
            Sprite[] frames = VirusArt.Frames(TypeName);
            if (frames == null || frames.Length < 3) return; // 絵が無ければ足さない（今のスス玉で出る）

            t = VirusArt.Clone(spawner, 0, TypeName);
            if (t == null) return;
            ZakoWalker walker = t.GetComponent<ZakoWalker>();
            bool ok = walker != null && t.GetComponent<Health>() != null && t.GetComponent<Rigidbody2D>() != null
                      && t.GetComponent<KinematicBody2D>() != null && VirusArt.BodyOf(t) != null
                      && VirusArt.Dress(t, TypeName, 0f);
            if (!ok)
            {
                Destroy(t);
                return;
            }
            walker.enabled = false; // 歩かない（部品は外さない。RewardKit.IsZako などが部品で雑魚と見分ける）
            t.AddComponent<ZakoRootkit>();
            if (spawner.AddTemplate(t, 0f) < 0) Destroy(t);
            t = null;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.Template", $"ルートキットのひな形を作れません：{e}");
            if (t != null) Destroy(t);
        }
    }

    // ───────── 1体分 ─────────

    void Awake()
    {
        try
        {
            _health = GetComponent<Health>();
            _rb = GetComponent<Rigidbody2D>();
            _kb = GetComponent<KinematicBody2D>();
            _walker = GetComponent<ZakoWalker>();
            _visual = transform.Find("Visual");
            Transform body = VirusArt.BodyOf(gameObject);
            _body = body != null ? body.gameObject : null;
            _art = VirusArt.ArtOf(gameObject);
            _anim = _art != null ? _art.GetComponent<SpriteFrameAnimation>() : null;
            _frames = VirusArt.Frames(TypeName);
            _phase = Random.value * 1f;
            if (_health == null || _rb == null || _kb == null || _body == null || _art == null || _frames == null || _frames.Length < 3)
            {
                _failed = true; // Start で歩く雑魚にもどす
                return;
            }
            if (_walker != null) _walker.enabled = false;
            // 出たときは、もぐった状態から（出た瞬間に体当たりが当たらないように、Start より前に切る）
            State = Phase.Hidden;
            SetHittable(false);
            _art.sprite = _frames[0];
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.Awake", $"ルートキットの準備で例外：{e}");
            _failed = true;
        }
    }

    void Start()
    {
        try
        {
            _mask = LayerMask.GetMask("Ground", "OneWayPlatform");
            ZakoSpawner spawner = StagePacer.Current != null ? StagePacer.Current.Spawner : null;
            if (spawner != null && spawner.GroundMask.value != 0) _mask = spawner.GroundMask.value;
            _wallMask = LayerMask.GetMask("Ground");
            FindPlayer();
            if (_player != null) _face = _player.position.x >= transform.position.x ? 1 : -1;

            if (_failed || !AcquireSurface(_rb.position))
            {
                BecomeWalker();
                return;
            }
            // 出た場所の下の床（面）に合わせる（SpawnNow は床に合わせない。歩く部品を止めているので、重力も無い）
            _kb.Warp(new Vector2(Mathf.Clamp(_rb.position.x, _minX, _maxX), _surfaceY));
            Enter(Phase.Hidden);
            _ready = true;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.Start", $"ルートキットの始めで例外：{e}");
            FailOpen();
        }
    }

    void OnDisable()
    {
        // 部品だけ止められたとき（体は出ている）は、当たる姿で止める（かくれたまま残さない）。
        // 消える・隠れるとき（Destroy・SetActive(false)）は何もしない（その最中に子を入り切りしない）
        try
        {
            if (enabled || !gameObject.activeInHierarchy) return;
            if (_health == null || _health.IsDead || State == Phase.Walking) return;
            SetHittable(true);
            ResetOffset();
            if (_art != null && _frames != null && _frames.Length > 2) _art.sprite = _frames[2];
            State = Phase.Up;
            _stateAt = Time.time;
            _stateLen = _upTime;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.OnDisable", $"ルートキットを止めるときに例外：{e}");
        }
    }

    void FixedUpdate()
    {
        if (!_ready || _health == null || _health.IsDead) return;
        try
        {
            if (State == Phase.Walking)
            {
                TickWalking();
                return;
            }
            if (_walker != null && _walker.enabled) _walker.enabled = false;

            if (SurfaceLost())
            {
                Reanchor();
                if (State == Phase.Walking) return;
            }

            // 見張り：当たらない時間が長すぎたら、すぐ出す（2.0 + 0.7 秒が決まり。少しだけ余裕を見る）
            if (!_hittable && Time.time - _hiddenSince > MaxHide + _warnTime + 0.5f)
            {
                Enter(Phase.Rising);
                return;
            }

            float t = Time.time - _stateAt;
            switch (State)
            {
                case Phase.Hidden:
                    Slide();
                    if (t >= _stateLen) Enter(Phase.Warn);
                    break;
                case Phase.Warn:
                    while (_chips < ChipCount && t >= _chips * ChipInterval)
                    {
                        _chips++;
                        SpawnChip();
                    }
                    if (t >= _warnTime) Enter(Phase.Rising);
                    break;
                case Phase.Rising:
                    if (t >= _riseTime) Enter(Phase.Up);
                    break;
                case Phase.Up:
                    if (t >= _stateLen) Enter(Phase.Sinking);
                    break;
                case Phase.Sinking:
                    if (t >= _sinkTime) Resink();
                    break;
            }
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.FixedUpdate", $"ルートキットの動きで例外：{e}");
            FailOpen();
        }
    }

    void Update()
    {
        if (!_ready || _health == null || _health.IsDead || State == Phase.Walking) return;
        try
        {
            float t = Time.time - _stateAt;
            int frame;
            float dx = 0f;
            switch (State)
            {
                case Phase.Warn:
                    frame = 0;
                    dx = (Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0 ? 1f : -1f) * Px; // ±1ドットでふるえる
                    break;
                case Phase.Rising:
                    frame = 1;
                    break;
                case Phase.Up:
                    frame = 2;
                    dx = Sway[Mathf.FloorToInt((Time.time + _phase) / 0.18f) & 3] * Px;
                    break;
                case Phase.Sinking:
                    frame = t < _sinkTime * 0.5f ? 1 : 0;
                    break;
                default:
                    frame = 0;
                    break;
            }
            if (_anim != null && _anim.enabled) _anim.enabled = false; // コマは自分で入れる
            _art.sprite = _frames[frame];
            _art.transform.localPosition = new Vector3(dx, 0f, 0f);

            // プレイヤーの方を向く（すぐ近くでは変えない）
            Transform pl = FindPlayer();
            if (pl != null)
            {
                float d = pl.position.x - transform.position.x;
                if (Mathf.Abs(d) > StopDistance) _face = d > 0f ? 1 : -1;
            }
            if (_visual != null)
            {
                Vector3 s = _visual.localScale;
                s.x = Mathf.Abs(s.x) * _face;
                _visual.localScale = s;
            }
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.Update", $"ルートキットの見た目で例外：{e}");
            FailOpen();
        }
    }

    // ───────── 状態 ─────────

    void Enter(Phase phase)
    {
        State = phase;
        _stateAt = Time.time;
        switch (phase)
        {
            case Phase.Hidden:
                _stateLen = Mathf.Min(MaxHide, Random.Range(_hideTime.x, _hideTime.y)); // 時計だけで決める（プレイヤーに着いたか、には関係しない）
                SetHittable(false);
                break;
            case Phase.Warn:
                _chips = 0;
                SetHittable(false);
                break;
            case Phase.Rising:
                SetHittable(true); // ここから体当たり・被弾あり
                break;
            case Phase.Up:
                _stateLen = _upTime * (FewLeft() ? 2f : 1f);
                SetHittable(true);
                break;
            case Phase.Sinking:
                SetHittable(true); // しずみ切るまでは当たる
                break;
        }
        ResetOffset();
    }

    /// <summary>しずみ切った：面を確かめ直してから、またもぐる</summary>
    void Resink()
    {
        if (!AcquireSurface(_rb.position))
        {
            BecomeWalker();
            return;
        }
        Vector2 p = _rb.position;
        float x = Mathf.Clamp(p.x, _minX, _maxX);
        if (Mathf.Abs(x - p.x) > 0.001f || Mathf.Abs(_surfaceY - p.y) > 0.001f) _kb.Warp(new Vector2(x, _surfaceY));
        Enter(Phase.Hidden);
    }

    /// <summary>体当たり・被弾の箱（Visual/Body）の入り切り</summary>
    void SetHittable(bool on)
    {
        if (_hittable && !on) _hiddenSince = Time.time;
        _hittable = on;
        if (_body != null && _body.activeSelf != on) _body.SetActive(on);
    }

    void ResetOffset()
    {
        if (_art != null) _art.transform.localPosition = Vector3.zero;
    }

    /// <summary>残りが 3体以下か（StagePacer の白い印と同じ決め方。ノルマの無いステージは -1）</summary>
    static bool FewLeft()
    {
        StagePacer p = StagePacer.Current;
        return p != null && p.Remaining > 0 && p.Remaining <= StagePaceTable.MarkFrom;
    }

    // ───────── もぐっている間の動き ─────────

    /// <summary>割れ目ごと、プレイヤーの x の方へすべる（面の中だけ）</summary>
    void Slide()
    {
        Transform pl = FindPlayer();
        if (pl == null) return;
        Vector2 p = _rb.position;
        if (Mathf.Abs(pl.position.x - p.x) <= StopDistance) return;
        float target = Mathf.Clamp(pl.position.x, _minX, _maxX);
        float dx = target - p.x;
        if (Mathf.Abs(dx) < 0.001f) return;
        float step = _slideSpeed * Mathf.Max(0f, GameBuffs.ZakoSpeed) * Time.fixedDeltaTime;
        _rb.MovePosition(new Vector2(p.x + Mathf.Clamp(dx, -step, step), _surfaceY));
    }

    void SpawnChip()
    {
        // 見た目だけ。失敗しても動きは止めない
        try
        {
            var at = new Vector2(_rb.position.x + Random.Range(-0.25f, 0.25f), _surfaceY + 0.05f);
            var v = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(3f, 4.5f));
            FxAnim.Sprite(FxArt.Square(2), at, FxKit.OrderFragment, 0.45f, ChipGrey, 1f, "RootkitChip")
                .Velocity(v).Gravity(26f).FadeFrom(0.6f);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.Chip", $"ルートキットのかけらで例外：{e}");
        }
    }

    // ───────── 立つ面 ─────────

    /// <summary>足元の下の、動かない面をさがす（動く・消える足場は飛ばす）。左右に動ける範囲も決める</summary>
    bool AcquireSurface(Vector2 feet)
    {
        RaycastHit2D hit = AppStagePlatforms.RaycastSteady(feet + Vector2.up * 0.3f, Vector2.down, 20f, _mask);
        Collider2D col = hit.collider;
        if (col == null || col.isTrigger || hit.distance <= 0.001f) return false; // 無い・壁の中から始まった
        AppPlatform platform = col.GetComponentInParent<AppPlatform>();
        if (platform != null && (!platform.IsSteady || !platform.IsSolidNow)) return false;

        Bounds b = col.bounds;
        float y = hit.point.y;
        float min = b.min.x + EdgeMargin;
        float max = b.max.x - EdgeMargin;
        // 壁（Ground）の手前まで。床の当たり判定が壁の下までのびていることがあるので
        var origin = new Vector2(Mathf.Clamp(feet.x, b.min.x, b.max.x), y + 0.5f);
        RaycastHit2D left = Physics2D.Raycast(origin, Vector2.left, 30f, _wallMask);
        if (left.collider != null && !left.collider.isTrigger && left.distance > 0.01f) min = Mathf.Max(min, left.point.x + EdgeMargin);
        RaycastHit2D right = Physics2D.Raycast(origin, Vector2.right, 30f, _wallMask);
        if (right.collider != null && !right.collider.isTrigger && right.distance > 0.01f) max = Mathf.Min(max, right.point.x - EdgeMargin);
        // ボス部屋（BattleArena）の中へは入らない（横スクロール・ウィンドウ移動で、床がボス部屋まで続いていても、ゲートの下をくぐって追わないように）
        BattleArena arena = BattleArena.Current;
        if (arena != null && !arena.Bounds.Contains(new Vector2(feet.x, y + 0.5f)))
        {
            Rect a = arena.Bounds;
            if (feet.x < a.xMin) max = Mathf.Min(max, a.xMin - EdgeMargin);
            else if (feet.x > a.xMax) min = Mathf.Max(min, a.xMax + EdgeMargin);
        }
        if (min > max) min = max = (min + max) * 0.5f; // せまい面：まん中だけ

        _surface = col;
        _surfacePlatform = platform;
        _surfaceCenter = b.center;
        _surfaceY = y;
        _minX = min;
        _maxX = max;
        return true;
    }

    /// <summary>乗っている面が消えた・動いた（ほかの決まりが足場を閉じた・動かしたなど）</summary>
    bool SurfaceLost()
    {
        if (_surface == null || !_surface.enabled || !_surface.gameObject.activeInHierarchy) return true;
        if (_surfacePlatform != null && !_surfacePlatform.IsSolidNow) return true;
        return ((Vector2)_surface.bounds.center - _surfaceCenter).sqrMagnitude > 0.05f * 0.05f;
    }

    /// <summary>面をさがし直す。かくれている間はその面へ移る。出ている間・面が無いときは、歩く雑魚にして落とす</summary>
    void Reanchor()
    {
        bool hidden = State == Phase.Hidden || State == Phase.Warn;
        if (!hidden || !AcquireSurface(_rb.position))
        {
            BecomeWalker();
            return;
        }
        // 状態と時計はそのまま（ずっとかくれたままにならないように）
        _kb.Warp(new Vector2(Mathf.Clamp(_rb.position.x, _minX, _maxX), _surfaceY));
    }

    // ───────── 歩く雑魚にもどる ─────────

    /// <summary>歩く雑魚にもどす（当たる・重力あり・見た目はコマ 2）。面が見つかれば、あとでまたもぐる</summary>
    void BecomeWalker()
    {
        State = Phase.Walking;
        _stateAt = Time.time;
        _nextTry = Time.time + 0.5f;
        SetHittable(true);
        ResetOffset();
        if (_art != null && _frames != null && _frames.Length > 2) _art.sprite = _frames[2];
        if (_anim != null && _anim.enabled) _anim.enabled = false;
        if (_walker != null)
        {
            _walker.SetDirection(_face);
            _walker.enabled = true;
        }
        _ready = true;
    }

    /// <summary>何か起きたとき：当たる姿にして、ずっと歩く雑魚のまま（かくれたまま残さない）</summary>
    void FailOpen()
    {
        _failed = true;
        try
        {
            BecomeWalker();
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoRootkit.FailOpen", $"ルートキットを歩く雑魚にもどせません：{e}");
            if (_body != null && !_body.activeSelf) _body.SetActive(true);
            _hittable = true;
        }
    }

    /// <summary>歩いている間：着地して、足元が動かない面なら、しずんでもぐる</summary>
    void TickWalking()
    {
        if (_failed || Time.time < _nextTry) return;
        _nextTry = Time.time + 0.3f;
        if (_kb == null || !_kb.IsGrounded || _walker == null) return;
        Vector2 p = _rb.position;
        if (!AcquireSurface(p) || Mathf.Abs(p.y - _surfaceY) > 0.1f) return; // 足元が、その面でない（動く足場の上など）
        _walker.enabled = false;
        float x = Mathf.Clamp(p.x, _minX, _maxX);
        _kb.Warp(new Vector2(x, _surfaceY));
        Enter(Phase.Sinking);
    }

    Transform FindPlayer()
    {
        if (_player != null && _player.gameObject.activeInHierarchy) return _player;
        if (Time.time < _nextFind) return null;
        _nextFind = Time.time + 1f;
        StagePacer pacer = StagePacer.Current;
        _player = pacer != null ? pacer.Player : null;
        if (_player == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            _player = pc != null ? pc.transform : null;
        }
        return _player;
    }
}
