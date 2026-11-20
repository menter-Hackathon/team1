using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ウイルスの巣 1つ分（2026-11-20。docs/virus-types-design.md 4章・7章 B）。置くのは VirusNests（下）。
/// ・ワームの巣＝あいたポート（壁・動かない足場の下の面・床）／ルートキットの巣＝床下の根（床の線の下）。絵は VirusArt.NestFrames
/// ・Health（Team.Enemy・HP 3）＋Hurtbox。体当たりは無い（Hitbox を付けない）。雑魚の部品は付けない・EnemyWave に入れない
///   （倒す数に入らない。RewardKit.IsZako は false）
/// ・1.0秒で育つ → 2秒後に 1体、そのあと 5秒ごと（場面 ②③⑤ の間だけ）。出す前に 0.6秒の予告。3体出したら、しぼんで消える
/// ・出す雑魚は、かならず ZakoSpawner.SpawnNow を通す（null ＝ノルマが無い → しぼんで消える）。出した雑魚は消さない
/// ・こわすと煙とかけら、拾える物を 1つ（1ステージで 1個まで）。ボスの登場で 0.4秒でしぼんで消える（拾える物は出さない）
/// 数値はすべて【仮】
/// </summary>
public class VirusNest : MonoBehaviour
{
    public enum State { Resting, Growing, Idle, Warn, Leaving, Remains }

    // ───────── 数値【仮】 ─────────
    public const int Hp = 3;
    public const int MaxEmit = 3;            // 1つの巣が出す数
    public const int MaxOwnAlive = 2;        // 自分の雑魚がこれだけ生きている間は出さない
    public const float GrowSeconds = 1f;
    public const float FirstEmit = 2f;
    public const float EmitInterval = 5f;
    public const float WarnSeconds = 0.6f;
    public const float LeaveSeconds = 0.4f;
    public const float RemainsSeconds = 1.5f;
    public const float PlayerBlock = 3f;     // プレイヤーが口からこれ以内なら出さない
    public const float MouthOut = 0.3f;      // 口＝面からこれだけ外
    public const float RootkitSide = 1f;     // ルートキットは巣の左右この距離の床に出す
    const int Order = 3;                     // 足場の絵（2）より手前・キャラクター（11〜）より後ろ
    const float IdleFps = 2f;
    const float RestBrightness = 0.6f;
    const float GateForget = 6f;             // 横スクロール：画面の左へこれより離れた巣は消す
    const float Px = 1f / 18f;

    static readonly List<VirusNest> AllNests = new List<VirusNest>();
    static readonly Color Soot = new Color(0.11f, 0.11f, 0.12f, 1f);
    static readonly Color SootGrey = new Color(0.36f, 0.36f, 0.4f, 1f);

    /// <summary>今ある巣（確認用・置く場所のきょり用）</summary>
    public static IReadOnlyList<VirusNest> All => AllNests;

    public string Type { get; private set; }
    /// <summary>付いている面の法線（床＝上、天井・足場の下＝下、壁＝部屋の内向き）</summary>
    public Vector2 Normal { get; private set; } = Vector2.up;
    public State Phase => _state;
    public int Emitted => _emitted;
    /// <summary>雑魚が出てくる所</summary>
    public Vector2 Mouth => (Vector2)transform.position + Normal * MouthOut;
    /// <summary>まだ消えていないか（育つ・休む・出す間）</summary>
    public bool IsLive => _state != State.Leaving && _state != State.Remains;

    VirusNests _host;
    ZakoSpawner _spawner;
    StagePacer _pacer;
    AppPlatform _platform;
    bool _wall;
    Transform _visual;
    SpriteRenderer _art;
    Sprite[] _frames;
    Health _health;
    BoxCollider2D _hurt;
    State _state;
    float _stateAt;
    float _nextEmit;
    int _emitted;
    int _lastSide;
    float _shakeUntil;
    readonly List<Health> _mine = new List<Health>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AllNests.Clear();

    /// <summary>
    /// 巣を作る。surface＝付く面の点（ワーム：下の辺のまん中、ルートキット：床の線）。normal＝面の法線。
    /// platform＝動かない足場の下の面に付くとき（出したワームをその足場レールに乗せる）。resting＝休んだまま出す（ルートキットの最初から見える巣）
    /// </summary>
    internal static VirusNest Create(VirusNests host, StagePacer pacer, string type, Vector2 surface, Vector2 normal, AppPlatform platform, bool wall, bool resting)
    {
        Sprite[] frames = VirusArt.NestFrames(type);
        if (frames == null || frames.Length < 6 || pacer == null || pacer.Spawner == null) return null;
        int enemy = LayerMask.NameToLayer("Enemy");

        // 体力の最大値を入れてから動かすので、いったん止めて部品を足す（TerminalGhost.Create と同じ）
        var go = new GameObject("VirusNest_" + type);
        go.SetActive(false);
        if (enemy >= 0) go.layer = enemy;
        if (host != null) go.transform.SetParent(host.transform, false); // ステージ（流れの係）といっしょに消える。EnemyWave の子にはしない
        go.transform.position = surface;

        var health = go.AddComponent<Health>();
        JsonUtility.FromJsonOverwrite("{\"_max\":" + Hp + ",\"_oneDamagePerHit\":false,\"_invincibleTimeAfterHit\":0}", health);
        health.ResetHealth();
        health.enabled = false; // 育つ・休む間は当たらない（TryTakeDamage は isActiveAndEnabled を見る）

        var visual = new GameObject("Visual").transform;
        visual.gameObject.layer = go.layer;
        visual.SetParent(go.transform, false);
        float angle = Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg; // 絵の下の辺を面に向ける
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);

        var artGo = new GameObject("Art");
        artGo.layer = go.layer;
        artGo.transform.SetParent(visual, false);
        var art = artGo.AddComponent<SpriteRenderer>();
        art.sprite = frames[0];
        art.sortingOrder = Order;

        var hurtGo = new GameObject("Hurt");
        hurtGo.layer = go.layer;
        hurtGo.transform.SetParent(visual, false);
        var col = hurtGo.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        bool rootkit = type == "Rootkit";
        col.size = rootkit ? new Vector2(1.4f, 0.6f) : new Vector2(1.4f, 1f);
        col.offset = rootkit ? new Vector2(0f, 0.3f) : new Vector2(0f, 0.5f);
        col.enabled = false;
        hurtGo.AddComponent<Hurtbox>();

        var nest = go.AddComponent<VirusNest>();
        nest.Type = type;
        nest.Normal = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector2.up;
        nest._host = host;
        nest._pacer = pacer;
        nest._spawner = pacer.Spawner;
        nest._platform = platform;
        nest._wall = wall;
        nest._visual = visual;
        nest._art = art;
        nest._frames = frames;
        nest._health = health;
        nest._hurt = col;
        nest._lastSide = UnityEngine.Random.value < 0.5f ? 1 : -1;
        nest.SetState(resting ? State.Resting : State.Growing);
        if (resting) art.color = new Color(RestBrightness, RestBrightness, RestBrightness, 1f);
        else visual.localScale = new Vector3(0.01f, 0.01f, 1f);
        go.SetActive(true);
        return nest;
    }

    void Awake() { if (!AllNests.Contains(this)) AllNests.Add(this); }

    void OnEnable()
    {
        if (_health == null) return;
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
    }

    void OnDisable()
    {
        if (_health == null) return;
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    void OnDestroy() => AllNests.Remove(this);

    /// <summary>休んでいる巣を起こす（場面 ② から出し始める）</summary>
    public void Wake()
    {
        if (_state != State.Resting) return;
        _art.color = Color.white;
        BecomeIdle();
    }

    /// <summary>しぼんで消える（拾える物は出さない。出した雑魚はそのまま）</summary>
    public void Leave()
    {
        if (_state == State.Leaving || _state == State.Remains) return;
        if (_health != null) _health.enabled = false;
        if (_hurt != null) _hurt.enabled = false;
        SetState(State.Leaving);
    }

    void BecomeIdle()
    {
        _health.enabled = true;
        _hurt.enabled = true;
        _visual.localScale = Vector3.one;
        _nextEmit = Time.time + FirstEmit;
        SetState(State.Idle);
    }

    void SetState(State s)
    {
        _state = s;
        _stateAt = Time.time;
    }

    void Update()
    {
        try { Tick(); }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusNest.Update", $"巣で例外（消す）：{e}");
            Destroy(gameObject); // 出した雑魚は消さない
        }
    }

    void Tick()
    {
        float now = Time.time;
        if (IsLive && BossStarted()) Leave();
        if (IsLive && _host != null && _host.Gate && transform.position.x < ScreenUi.ViewRect().xMin - GateForget)
        {
            Destroy(gameObject);
            return;
        }

        float t = now - _stateAt;
        Vector2 shake = Vector2.zero;
        switch (_state)
        {
            case State.Resting:
                _art.sprite = _frames[0]; // 目を閉じて休んでいる（コマ 0 のまま・暗く）
                break;
            case State.Growing:
                float k = Mathf.Clamp01(t / GrowSeconds);
                _visual.localScale = new Vector3(Mathf.Max(0.01f, k), Mathf.Max(0.01f, k), 1f);
                _art.sprite = _frames[0];
                if (k >= 1f) BecomeIdle();
                break;
            case State.Idle:
                _art.sprite = IdleFrame(now);
                TryStartEmit(now);
                break;
            case State.Warn:
                _art.sprite = _frames[2];
                shake.x = (Mathf.FloorToInt(now / 0.05f) % 2 == 0 ? 1f : -1f) * Px;
                if (t >= WarnSeconds) Emit(now);
                break;
            case State.Leaving:
                float s = 1f - Mathf.Clamp01(t / LeaveSeconds);
                _visual.localScale = new Vector3(Mathf.Max(0.01f, s), Mathf.Max(0.01f, s), 1f);
                if (t >= LeaveSeconds) Destroy(gameObject);
                break;
            case State.Remains:
                _art.sprite = _frames[5];
                Color c = _art.color;
                c.a = 1f - Mathf.Clamp01(t / RemainsSeconds);
                _art.color = c;
                if (t >= RemainsSeconds) Destroy(gameObject);
                break;
        }
        // 当たったとき：±2px ゆれる
        if (now < _shakeUntil) shake.x = (Mathf.FloorToInt(now / 0.03f) % 2 == 0 ? 2f : -2f) * Px;
        _art.transform.localPosition = shake;
    }

    Sprite IdleFrame(float now)
    {
        int f = Mathf.FloorToInt(now * IdleFps) % 2;
        bool broken = _health != null && _health.Current <= 1; // こわれかけ
        return _frames[(broken ? 3 : 0) + f];
    }

    bool BossStarted()
    {
        if (_pacer == null || _spawner == null) return true;
        return _pacer.Phase == PacePhase.Done || _spawner.IsStopped;
    }

    // ───────── 雑魚を出す ─────────

    void TryStartEmit(float now)
    {
        if (now < _nextEmit) return;
        if (_host == null || !_host.EmitAllowed)
        {
            _nextEmit = now + 0.5f; // ①④ は休む
            return;
        }
        if (_emitted >= MaxEmit || _spawner.QuotaUsedUp || VirusArt.IndexOf(_spawner, Type) < 0)
        {
            Leave(); // 出し切った・ノルマが無い・ひな形が無い
            return;
        }
        _mine.RemoveAll(h => h == null || h.IsDead || !h.gameObject.activeInHierarchy);
        Transform player = _pacer.Player;
        Health ph = player != null ? player.GetComponent<Health>() : null;
        bool blocked = _mine.Count >= MaxOwnAlive || player == null || (ph != null && ph.IsDead) || _spawner.IsStopped
                       || Vector2.Distance((Vector2)player.position + Vector2.up * 0.5f, Mouth) < PlayerBlock;
        if (blocked)
        {
            _nextEmit = now + 0.5f; // 目の前には出さない
            return;
        }
        SetState(State.Warn);
    }

    void Emit(float now)
    {
        int template = VirusArt.IndexOf(_spawner, Type);
        Health h = template >= 0 ? _spawner.SpawnNow(template, SpawnPoint()) : null;
        if (h == null)
        {
            Leave(); // ノルマが無くなった・ボスの登場が始まった
            return;
        }
        _emitted++;
        _mine.Add(h);
        if (Type == "Worm" && h.TryGetComponent(out ZakoWorm worm))
        {
            int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
            if (_platform != null) worm.AttachPlatform(_platform, transform.position.x, true, dir); // 巣の付いている面のレールに乗せる
            else if (_wall) worm.AttachRoom(Mouth, dir);
            // 床の巣は何もしない（ワームが足元を見て決める）
        }
        _nextEmit = now + EmitInterval;
        SetState(State.Idle);
    }

    /// <summary>ワーム＝口。ルートキット＝巣の左右 1マスの床（プレイヤーから遠い方を先に。交代で）</summary>
    Vector2 SpawnPoint()
    {
        if (Type != "Rootkit") return Mouth;
        Vector2 at = transform.position;
        Transform player = _pacer != null ? _pacer.Player : null;
        int side = -_lastSide;
        if (player != null && UnityEngine.Random.value < 0.6f) side = player.position.x >= at.x ? -1 : 1;
        _lastSide = side;
        int ground = LayerMask.GetMask("Ground");
        for (int i = 0; i < 2; i++)
        {
            int sd = i == 0 ? side : -side;
            var from = new Vector2(at.x + sd * RootkitSide, at.y + 1f);
            RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, 2f, ground);
            if (hit.collider != null && hit.distance > 0.05f) return hit.point;
        }
        return at;
    }

    // ───────── 当たった・こわれた ─────────

    Vector2 Center => (Vector2)transform.position + Normal * (Type == "Rootkit" ? 0.3f : 0.5f);

    void OnDamaged(DamageInfo info)
    {
        try
        {
            _shakeUntil = Time.time + 0.12f;
            if (_health != null && !_health.IsDead)
                FxKit.Fragments(Center, Normal, 3, new[] { FxArt.Square(2) }, new[] { Soot, SootGrey }); // 白く光るのは HitFx がやる
        }
        catch (Exception e) { SkinDresser.LogOnce("VirusNest.OnDamaged", $"巣で例外：{e}"); }
    }

    void OnDied()
    {
        try
        {
            if (_hurt != null) _hurt.enabled = false;
            Vector2 c = Center;
            Vfx.Smoke(c);
            FxKit.Fragments(c, Normal, 6, new[] { FxArt.Square(2), FxArt.Square(3) }, new[] { Soot, SootGrey });
            _visual.localScale = Vector3.one;
            _art.color = Color.white;
            SetState(State.Remains); // あと（コマ 5）が 1.5秒で消える
            if (_host != null) _host.NestDestroyed(c);
        }
        catch (Exception e) { SkinDresser.LogOnce("VirusNest.OnDied", $"巣で例外：{e}"); }
    }
}

/// <summary>
/// 【仮】巣を置く係（2026-11-20。docs/virus-types-design.md 4-2・7章 B）。ステージごとに、流れの係（StagePacer）の GameObject に付く。
/// ・StagePacer.Started で「数を決める」と AddQuota だけ（1画面だけ。巣の数×3）。ひな形は出すときに名前で探す（順番にたよらない）
/// ・場面（pacer.Phase）を毎フレーム見て、決めた場面になったら巣を作る。ボスの登場（Done・spawner.IsStopped）で全部しぼませる
/// ・ルートキットの巣は、1画面のステージなら最初から見えている（休んでいる。Started の次の Update で作る）
/// ・VirusArt.Enabled = false なら何もしない
/// 数値はすべて【仮】
/// </summary>
public class VirusNests : MonoBehaviour
{
    // ───────── 数値【仮】 ─────────
    public const int ZakoPerNest = 3;
    public const float MinPlayerDistance = 5f;
    public const float MinNestDistance = 2.5f;
    public const float WallHeight = 1.8f;       // 壁の巣の高さ（床から）
    public const float UnderMinWidth = 2.5f;    // 下の面に付ける足場の幅
    public const float UnderMinHeight = 1.8f;   // 下の面の高さ（床から。ジャンプで届く）
    public const float UnderMaxHeight = 3.6f;
    public const float WindowCover = 0.3f;      // 重なったウィンドウ：手前のウィンドウの四角をこれだけふくらませて、かくれる所を切る（docs/window-stage-and-pickup-design.md 7-2）
    public const float WindowSpanMin = 2.0f;    // 同じ：見えている区間がこれより短ければ付けない
    const float RetrySeconds = 0.5f;
    const int FloorTries = 12;

    /// <summary>ルートキットの巣を先にするアプリ（開発の道具・ゲーム）。ほかはワームの巣が先</summary>
    static readonly string[] RootkitFirst = { "Terminal", "VSCode", "Cursor", "Xcode", "Unity", "Steam" };

    class Plan
    {
        public string Type;
        public PacePhase At;
        public bool Made;
        public bool Failed;
        public VirusNest Nest;
        public float NextTry;
    }

    StagePacer _pacer;
    ZakoSpawner _spawner;
    bool _oneScreen, _gate;
    readonly List<Plan> _plans = new List<Plan>();
    bool _restPlaced;
    bool _pickupGiven;
    bool _debug;
    bool _closed;

    /// <summary>1画面のステージ（ノルマあり）か</summary>
    public bool OneScreen => _oneScreen;
    /// <summary>横スクロール・ウィンドウ移動か</summary>
    public bool Gate => _gate;
    /// <summary>巣が雑魚を出してよいか（場面 ②③⑤。確認用の DebugPlaceNow のあとは、いつでも）</summary>
    public bool EmitAllowed
    {
        get
        {
            if (_closed || _pacer == null) return false;
            PacePhase p = _pacer.Phase;
            if (p == PacePhase.Done) return false;
            return _debug || p == PacePhase.Build || p == PacePhase.Event || p == PacePhase.Climax;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => StagePacer.Started -= OnStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static void OnStarted(StagePacer pacer)
    {
        try
        {
            if (!VirusArt.Enabled || pacer == null || pacer.Spawner == null) return;
            if (pacer.GetComponent<VirusNests>() != null) return;
            pacer.gameObject.AddComponent<VirusNests>().Setup(pacer, true);
        }
        catch (Exception e) { SkinDresser.LogOnce("VirusNests.OnStarted", $"巣の係を作れません：{e}"); }
    }

    /// <summary>
    /// 【確認用】今すぐ巣を置く（場面を待たない。休んでいる巣も起こす。このあとは、どの場面でも出す）。置いた数を返す。
    /// 表が空のステージ（ノルマも入口も無い）では、ワームとルートキットの巣を 1つずつ試す
    /// </summary>
    public static int DebugPlaceNow(StagePacer pacer)
    {
        try
        {
            if (pacer == null) pacer = StagePacer.Current;
            if (pacer == null || pacer.Spawner == null || !VirusArt.Enabled) return 0;
            VirusNests host = pacer.GetComponent<VirusNests>();
            if (host == null)
            {
                host = pacer.gameObject.AddComponent<VirusNests>();
                host.Setup(pacer, false);
            }
            ZakoWorm.Register(pacer.Spawner);
            host._debug = true;
            host._restPlaced = true;
            if (host._plans.Count == 0)
            {
                host.AddPlan("Worm", PacePhase.Intro);
                host.AddPlan("Rootkit", PacePhase.Intro);
            }
            int n = 0;
            foreach (Plan p in host._plans)
            {
                if (p.Made)
                {
                    if (p.Nest != null) p.Nest.Wake();
                    continue;
                }
                p.Failed = false;
                if (host.TryMake(p, false)) n++;
            }
            return n;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusNests.DebugPlaceNow", $"巣を置けません：{e}");
            return 0;
        }
    }

    void Setup(StagePacer pacer, bool addQuota)
    {
        _pacer = pacer;
        _spawner = pacer.Spawner;
        _oneScreen = _spawner.Quota > 0 && pacer.Remaining >= 0; // Remaining は 1画面のときだけ 0 以上（StagePacer.Start）
        BossEntrance entrance = FindObjectOfType<BossEntrance>();
        _gate = !_oneScreen && _spawner.Quota == 0 && entrance != null && entrance.WaitsForGate;
        if (!_oneScreen && !_gate) return; // ノルマも入口も無いシーンは、巣を作らない

        // どの巣にするか（4-2 の小さい表）。2つ目はもう片方
        string label = AppBackgrounds.CurrentLabel;
        if (string.IsNullOrEmpty(label)) label = AppBackgrounds.LabelFor(pacer.AppName, pacer.BundleId);
        bool rootkitFirst = false;
        if (!string.IsNullOrEmpty(label))
            foreach (string l in RootkitFirst)
                if (string.Equals(l, label, StringComparison.OrdinalIgnoreCase)) rootkitFirst = true;
        string first = rootkitFirst ? "Rootkit" : "Worm";
        string second = rootkitFirst ? "Worm" : "Rootkit";

        if (_oneScreen)
        {
            if (pacer.IsFinal) AddPlan("Rootkit", PacePhase.Build); // Terminal（最後）はルートキットの巣 1つ
            else
            {
                AddPlan(first, PacePhase.Build);
                if (pacer.StageNumber >= 3) AddPlan(second, PacePhase.Event);
            }
            // ノルマは最初に足す（右上のゲージが途中で伸びない）。余ったら ⑤ で流れの係が出し切る
            if (addQuota && _plans.Count > 0) _spawner.AddQuota(_plans.Count * ZakoPerNest);
        }
        else
        {
            AddPlan(first, PacePhase.Build); // 横スクロール・ウィンドウ移動はノルマが無いので、足さない
            AddPlan(second, PacePhase.Climax);
        }
    }

    void AddPlan(string type, PacePhase at)
    {
        Sprite[] nest = VirusArt.NestFrames(type);
        Sprite[] body = VirusArt.Frames(type);
        if (nest == null || nest.Length < 6 || body == null || body.Length == 0) return; // 絵が無い種類の巣は作らない
        _plans.Add(new Plan { Type = type, At = at });
    }

    void Update()
    {
        try { Tick(); }
        catch (Exception e) { SkinDresser.LogOnce("VirusNests.Update", $"巣の係で例外：{e}"); }
    }

    void Tick()
    {
        if (_closed || _pacer == null || _spawner == null || _plans.Count == 0) return;
        PacePhase phase = _pacer.Phase;
        if (phase == PacePhase.Done || _spawner.IsStopped)
        {
            Close();
            return;
        }
        // ルートキットの巣は、1画面のステージなら最初から見えている（Started の次の Update。ひな形がそろってから）
        if (!_restPlaced)
        {
            _restPlaced = true;
            if (_oneScreen)
                foreach (Plan p in _plans)
                    if (p.Type == "Rootkit" && !p.Made && phase < p.At) TryMake(p, true);
        }
        foreach (Plan p in _plans)
        {
            if (p.Failed) continue;
            bool due = _debug || phase >= p.At;
            if (!due) continue;
            if (p.Made)
            {
                if (p.Nest != null && p.Nest.Phase == VirusNest.State.Resting) p.Nest.Wake(); // ② から起きる
                continue;
            }
            if (Time.time < p.NextTry) continue;
            if (!TryMake(p, false)) p.NextTry = Time.time + RetrySeconds; // 置ける所が無ければ、少しあとでもう一度
        }
    }

    void Close()
    {
        _closed = true;
        foreach (Plan p in _plans)
            if (p.Nest != null) p.Nest.Leave();
    }

    bool TryMake(Plan p, bool resting)
    {
        if (VirusArt.IndexOf(_spawner, p.Type) < 0)
        {
            p.Failed = true; // ひな形が無い種類の巣は作らない（あまったノルマは ⑤ で流れの係が出し切る）
            return false;
        }
        if (!FindSpot(p.Type, out Vector2 at, out Vector2 normal, out AppPlatform platform, out bool wall)) return false;
        VirusNest nest = VirusNest.Create(this, _pacer, p.Type, at, normal, platform, wall, resting);
        if (nest == null)
        {
            p.Failed = true;
            return false;
        }
        p.Made = true;
        p.Nest = nest;
        return true;
    }

    /// <summary>こわされた巣から呼ぶ。拾える物を 1つ（1ステージで 1個まで。ZakoElite.OnDied と同じ決め方）</summary>
    internal void NestDestroyed(Vector2 center)
    {
        if (_pickupGiven || !KillChain.RewardsOpen) return;
        _pickupGiven = true;
        Transform player = RewardKit.PlayerOf(_pacer);
        Health ph = player != null ? player.GetComponent<Health>() : null;
        PickupKind kind = ph != null && ph.Current >= ph.Max ? PickupKind.Shield : PickupKind.Heal;
        Pickup.Drop(center, kind, player != null ? player.position.x : center.x);
    }

    // ───────── 置く場所（4-2） ─────────

    bool FindSpot(string type, out Vector2 at, out Vector2 normal, out AppPlatform platform, out bool wall)
    {
        normal = Vector2.up;
        platform = null;
        wall = false;
        Rect view = _gate ? ScreenUi.ViewRect() : (_pacer.PlayArea.width > 1f ? _pacer.PlayArea : KitMotion.Area);
        // 横スクロール・ウィンドウ移動：プレイヤーの進む先（画面の右半分）の床
        float xMin = _gate ? view.center.x : view.xMin + 1.5f; // 壁から離す（ルートキットは巣の左右 1マスに出る）
        float xMax = view.xMax - 1.5f;
        if (type == "Worm" && _oneScreen)
        {
            if (FindUnderside(out at, out platform))
            {
                normal = Vector2.down;
                return true;
            }
            if (FindWall(view, out at, out normal))
            {
                wall = true;
                return true;
            }
        }
        return FindFloor(view, xMin, xMax, out at);
    }

    /// <summary>動かない足場の下の面のまん中（幅 2.5 以上・床から 1.8〜3.6）</summary>
    bool FindUnderside(out Vector2 at, out AppPlatform platform)
    {
        at = default;
        platform = null;
        float floor = KitMotion.FloorY();
        var found = new List<(AppPlatform p, Vector2 at)>();
        foreach (AppPlatform p in FindObjectsOfType<AppPlatform>())
        {
            if (p == null || !p.IsSteady || !p.IsSolidNow) continue;
            Transform t = p.transform.Find("Art");
            SpriteRenderer sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
            if (sr == null) continue;
            Bounds b = sr.bounds;
            float under = b.min.y - floor;
            if (b.size.x < UnderMinWidth || under < UnderMinHeight || under > UnderMaxHeight) continue;
            var spot = new Vector2(b.center.x, b.min.y);
            StageWindow win = WindowStage.Of(p);
            if (win != null)
            {
                // 重なったウィンドウ（docs/window-stage-and-pickup-design.md 7-2）：下の端の、手前のウィンドウ（0.3 ふくらませる）にかくれていない
                // いちばん長い区間（2.0マス以上）のまん中
                if (!WindowStage.LongestVisibleBottom(win, WindowCover, out float x0, out float x1) || x1 - x0 < WindowSpanMin) continue;
                spot = new Vector2((x0 + x1) * 0.5f, b.min.y);
                if (!SpotOk(spot)) continue;
                found.Add((p, spot));
                continue;
            }
            if (!SpotOk(spot)) continue;
            found.Add((p, spot));
        }
        if (found.Count == 0) return false;
        var pick = found[UnityEngine.Random.Range(0, found.Count)];
        at = pick.at;
        platform = pick.p;
        return true;
    }

    /// <summary>左右の壁（プレイヤーから遠い方を先に）。床から 1.8 の高さ。巣は部屋の内向き</summary>
    bool FindWall(Rect view, out Vector2 at, out Vector2 normal)
    {
        at = default;
        normal = Vector2.up;
        float y = KitMotion.FloorY() + WallHeight;
        float cx = view.center.x;
        int ground = LayerMask.GetMask("Ground");
        Transform player = _pacer.Player;
        int far = player == null || player.position.x <= cx ? 1 : -1;
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? far : -far;
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(cx, y), new Vector2(side, 0f), 30f, ground);
            if (hit.collider == null || hit.distance < 3f || Mathf.Abs(hit.normal.x) < 0.7f) continue;
            if (!SpotOk(hit.point)) continue;
            at = hit.point;
            normal = new Vector2(Mathf.Sign(hit.normal.x), 0f);
            return true;
        }
        return false;
    }

    /// <summary>床（Ground。すり抜け床・動く足場には置かない）。巣の左右にも床がある所</summary>
    bool FindFloor(Rect view, float xMin, float xMax, out Vector2 at)
    {
        at = default;
        if (xMax - xMin < 0.5f) return false;
        int ground = LayerMask.GetMask("Ground");
        for (int i = 0; i < FloorTries; i++)
        {
            float x = UnityEngine.Random.Range(xMin, xMax);
            var from = new Vector2(x, view.yMax - 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, view.height + 2f, ground);
            if (hit.collider == null || hit.distance < 0.2f || hit.normal.y < 0.7f) continue;
            Vector2 p = hit.point;
            if (!HasFloor(p + Vector2.left * 0.8f, ground) || !HasFloor(p + Vector2.right * 0.8f, ground)) continue;
            if (!SpotOk(p)) continue;
            at = p;
            return true;
        }
        return false;
    }

    static bool HasFloor(Vector2 p, int ground)
    {
        RaycastHit2D hit = Physics2D.Raycast(p + Vector2.up * 0.3f, Vector2.down, 0.6f, ground);
        return hit.collider != null && hit.distance > 0.01f;
    }

    /// <summary>プレイヤーから 5 以上・ほかの巣から 2.5 以上・（道中なら）ボス部屋の外</summary>
    bool SpotOk(Vector2 p)
    {
        Transform player = _pacer.Player;
        if (player != null && Vector2.Distance(p, player.position) < MinPlayerDistance) return false;
        foreach (VirusNest n in VirusNest.All)
            if (n != null && n.IsLive && Vector2.Distance(p, n.transform.position) < MinNestDistance) return false;
        if (_spawner.AvoidBossRoom && BattleArena.Current != null && BattleArena.Current.Bounds.Contains(p + Vector2.up * 0.5f)) return false;
        return true;
    }
}
