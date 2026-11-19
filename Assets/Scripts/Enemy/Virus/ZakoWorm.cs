using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ワーム（2026-11-20。docs/virus-types-design.md 2章・7章 B）。歩く雑魚のひな形を複製して、絵をワームにした物。
/// ・当たり判定をたどらず、「四角のふち（レール）」の上を道のり s で這う（2-1）
///   部屋レール：床 → 壁 → 天井 → 壁（内側を回る。1画面のステージ）
///   足場レール：アプリの足場の絵（子 Art）のまわり（外側を回る）。毎 FixedUpdate 読み直すので、動く足場にそのまま付いていく
///   上だけレール：小さい足場の当たり判定の上の辺だけを往復（横スクロール・ウィンドウ移動）
/// ・ZakoWalker は止めて置く（外さない。RewardKit.IsZako が部品で雑魚と見分ける）。落ちるときだけ ZakoWalker を動かす（重力と歩きはまかせる）
/// ・手の届かない所に残らない（0章のかたい決まり）：床から 3.3 マスより上に つづけて 5秒いたら落ちる／天井でプレイヤーが真下に来たら落ちる／
///   残り 3体以下なら床だけを這う
/// ・ひな形は StagePacer.Started で足す（名前 Template_Worm。重み 0＝今の出し方では選ばれない。割り当て係・巣が名前で探す）
/// ・例外が出たら、歩く雑魚にもどして自分を止める（1画面のステージで、倒せない雑魚を残さない）
/// 数値はすべて【仮】
/// </summary>
[RequireComponent(typeof(ZakoWalker), typeof(KinematicBody2D), typeof(Health))]
public class ZakoWorm : MonoBehaviour
{
    /// <summary>今乗っているレール（None＝歩く雑魚にもどっている・落ちている）</summary>
    public enum Rail { None, Room, Platform, TopOnly }

    // ───────── 数値【仮】 ─────────
    public const float Speed = 2.5f;              // 這う速さ（マス/秒。GameBuffs.ZakoSpeed を掛ける）
    public const float HighLine = 3.3f;           // 床からこれより上は「手が届かない」（doc は 3.0。下の段の足場の上が 3.0〜3.05 なので少し上げた）
    public const float HighSeconds = 5f;          // つづけてこの秒数、上にいたら落ちる
    public const float WarnSeconds = 0.6f;        // 落ちる前の予告（止まってふるえる）
    public const float CeilingDropRange = 0.8f;   // 天井にいて、プレイヤーとの横の差がこれ以内なら落ちる
    const float LandDelay = 0.3f;                 // 落ち始めてから、着地を見るまで
    const float SideRelease = 0.5f;               // はなす前に面から離す距離（横の面）
    const float UnderRelease = 0.9f;              // 同じ（天井・足場の下の面。ルートの箱は足元から上へ約 0.8）
    const float CeilingInset = 1.6f;              // 部屋の天井＝画面の上の端からこれだけ下（上の HUD の帯の下）
    const float WallProbeUp = 1.5f;               // 壁を探す高さ（床から）
    const float WallProbeLength = 30f;
    const float TopOnlyMaxWidth = 10f;            // これより広い床は、レールにせず歩く雑魚にもどす
    const float JumpLimit = 1f;                   // 足場が 1コマでこれ以上とんだら落ちる
    const float FloorMargin = 0.5f;               // 床だけを這うとき、壁の手前で折り返す
    const float WallClear = 0.5f;                 // 壁からこれだけ内側（ルートの箱は左右 0.4）
    const float HoleCheckEvery = 0.25f;           // 部屋の床にいる間、足元の床があるかを見る間隔
    const float Px = 1f / 18f;

    static readonly List<ZakoWorm> AliveWorms = new List<ZakoWorm>();
    static readonly Color DustColor = new Color(0.62f, 0.62f, 0.66f, 1f);

    /// <summary>今いるワーム（確認用）</summary>
    public static IReadOnlyList<ZakoWorm> Alive => AliveWorms;

    enum Mode { Start, Rail, Warn, Falling, Walker, Off }

    Mode _mode = Mode.Start;
    Rail _rail;
    ZakoWalker _walker;
    KinematicBody2D _body;
    Rigidbody2D _rb;
    Health _health;
    Transform _visual;
    Vector3 _visualHome;
    StagePacer _pacer;
    ZakoSpawner _spawner;
    bool _oneScreen;

    Rect _room;
    bool _roomReady, _roomTried;

    AppPlatform _platform;
    SpriteRenderer _platformArt;
    Rigidbody2D _platformBody;
    Collider2D _topCol;
    Vector2 _lastCenter;
    bool _hasLastCenter;

    float _s;
    int _dir = 1;
    int _edge;
    Vector2 _normal = Vector2.up;
    Vector2 _tangent = Vector2.right;
    bool _floorOnly;

    float _highTime;
    float _warnUntil;
    float _fallAt;
    float _nextHoleCheck;

    // 巣が Start より前に入れたレール
    bool _pending;
    Rail _pendingRail;
    AppPlatform _pendingPlatform;
    Vector2 _pendingPoint;
    bool _pendingUnder;
    int _pendingDir;

    /// <summary>今乗っているレール（確認用。予告の間も入る）</summary>
    public Rail RailKind => _mode == Mode.Rail || _mode == Mode.Warn ? _rail : Rail.None;
    /// <summary>乗っている面の法線（レールに乗っていなければ上）</summary>
    public Vector2 SurfaceNormal => RailKind != Rail.None ? _normal : Vector2.up;
    /// <summary>落ちている途中か</summary>
    public bool IsFalling => _mode == Mode.Falling;
    /// <summary>落ちる前の予告の間か</summary>
    public bool IsWarning => _mode == Mode.Warn;

    // ───────── ひな形を足す（StagePacer.Started） ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        StagePacer.Started -= OnStarted;
        AliveWorms.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static void OnStarted(StagePacer pacer)
    {
        try { Register(pacer != null ? pacer.Spawner : null); }
        catch (Exception e) { SkinDresser.LogOnce("ZakoWorm.OnStarted", $"ワームのひな形を作れません：{e}"); }
    }

    /// <summary>
    /// ワームのひな形を spawner に足す（もうあれば、その番号）。作れなければ -1。
    /// 歩く雑魚（Template(0)）を複製 → 絵をワームに → ZakoWalker を止める → ZakoWorm を付ける → 重み 0 で AddTemplate
    /// </summary>
    public static int Register(ZakoSpawner spawner)
    {
        if (!VirusArt.Enabled || spawner == null) return -1;
        int have = VirusArt.IndexOf(spawner, "Worm");
        if (have >= 0) return have;
        GameObject walker = spawner.Template(0);
        if (walker == null || walker.GetComponent<ZakoWalker>() == null || walker.GetComponent<KinematicBody2D>() == null
            || walker.GetComponent<Health>() == null) return -1;
        if (!VirusArt.TryGetSpec("Worm", out VirusArt.Spec spec)) return -1;
        GameObject t = VirusArt.Clone(spawner, 0, "Worm");
        if (t == null) return -1;
        if (!VirusArt.Dress(t, "Worm", spec.Fps))
        {
            Destroy(t);
            return -1;
        }
        t.GetComponent<ZakoWalker>().enabled = false; // 止めるだけ（外さない）
        if (t.GetComponent<ZakoWorm>() == null) t.AddComponent<ZakoWorm>();
        int index = spawner.AddTemplate(t, 0f);
        if (index < 0) Destroy(t);
        return index;
    }

    // ───────── 巣から入れる口（7章 B） ─────────

    /// <summary>
    /// 足場レールに乗せる（巣が出した直後に呼ぶ）。underside なら下の面の worldX の所、ちがえば上の面。
    /// dir＝ふちを回る向き（+1 反時計回り／-1 時計回り）
    /// </summary>
    public void AttachPlatform(AppPlatform platform, float worldX, bool underside, int dir)
    {
        if (platform == null) return;
        if (_mode == Mode.Start)
        {
            _pending = true;
            _pendingRail = Rail.Platform;
            _pendingPlatform = platform;
            _pendingPoint = new Vector2(worldX, 0f);
            _pendingUnder = underside;
            _pendingDir = dir;
            return;
        }
        Guard(() => { if (!AttachPlatformNow(platform, worldX, underside, dir)) ToWalker(false); });
    }

    /// <summary>部屋レールに乗せる（いちばん近い辺）。dir＝ふちを回る向き（+1 反時計回り／-1 時計回り）</summary>
    public void AttachRoom(Vector2 worldPoint, int dir)
    {
        if (_mode == Mode.Start)
        {
            _pending = true;
            _pendingRail = Rail.Room;
            _pendingPoint = worldPoint;
            _pendingDir = dir;
            return;
        }
        Guard(() => { if (!AttachRoomNow(worldPoint, dir, false)) ToWalker(false); });
    }

    // ───────── 動き ─────────

    void Awake()
    {
        _walker = GetComponent<ZakoWalker>();
        _body = GetComponent<KinematicBody2D>();
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _visual = transform.Find("Visual");
        if (_visual != null) _visualHome = _visual.localPosition;
    }

    void OnEnable() { if (!AliveWorms.Contains(this)) AliveWorms.Add(this); }

    void OnDisable() => AliveWorms.Remove(this);

    void OnDestroy() => AliveWorms.Remove(this);

    void Start()
    {
        Guard(() =>
        {
            _pacer = StagePacer.Current;
            _spawner = _pacer != null ? _pacer.Spawner : null;
            _oneScreen = _spawner != null && _spawner.Quota > 0;
            if (!VirusArt.Enabled || _visual == null)
            {
                ToWalker(false);
                return;
            }
            bool ok = false;
            if (_pending)
            {
                _pending = false;
                ok = _pendingRail == Rail.Platform
                    ? AttachPlatformNow(_pendingPlatform, _pendingPoint.x, _pendingUnder, _pendingDir)
                    : AttachRoomNow(_pendingPoint, _pendingDir, false);
            }
            if (!ok) ok = TryChooseRail();
            if (!ok) ToWalker(false);
        });
    }

    void FixedUpdate()
    {
        if (_mode == Mode.Off || _mode == Mode.Start || _health == null || _health.IsDead) return;
        try { Step(Time.fixedDeltaTime); }
        catch (Exception e) { FailSafe(e); }
    }

    void Update()
    {
        if (_mode == Mode.Off || _visual == null) return;
        try
        {
            // 予告：止まって ±1px ふるえる
            if (_mode == Mode.Warn)
            {
                float j = (Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0 ? 1f : -1f) * Px;
                _visual.localPosition = _visualHome + new Vector3(j, 0f, 0f);
            }
            else if (_visual.localPosition != _visualHome) _visual.localPosition = _visualHome;
        }
        catch (Exception e) { FailSafe(e); }
    }

    void Step(float dt)
    {
        switch (_mode)
        {
            case Mode.Rail:
                if (!StepRail(dt, true)) return;
                Rules(dt);
                if (_rail == Rail.TopOnly) GuardRoom();
                break;
            case Mode.Warn:
                if (!StepRail(dt, false)) return;
                if (Time.time >= _warnUntil) Drop();
                break;
            case Mode.Falling:
                if (Time.time - _fallAt >= LandDelay && _body.IsGrounded)
                {
                    _body.IgnoreOneWay = false;
                    if (!TryChooseRail()) _mode = Mode.Walker;
                }
                GuardRoom();
                break;
            case Mode.Walker:
                WalkerRules(dt);
                GuardRoom();
                break;
        }
    }

    /// <summary>レールの上を進めて、体を置く。レールが無くなったら落として false</summary>
    bool StepRail(float dt, bool moving)
    {
        if (!TryRect(out Rect r))
        {
            Drop();
            return false;
        }
        if (_rail == Rail.Platform || _rail == Rail.TopOnly)
        {
            bool solid = _rail == Rail.Platform
                ? _platform != null && _platform.IsSolidNow
                : _topCol != null && _topCol.enabled && _topCol.gameObject.activeInHierarchy;
            Vector2 c = r.center;
            bool jumped = _hasLastCenter && (c - _lastCenter).sqrMagnitude >= JumpLimit * JumpLimit;
            _lastCenter = c;
            _hasLastCenter = true;
            if (!solid || jumped)
            {
                Drop(); // 足場が乗れなくなった・とんだ：予告なしで、すぐ落ちる
                return false;
            }
        }
        if (moving)
        {
            float before = _s;
            _s += _dir * Speed * Mathf.Max(0f, GameBuffs.ZakoSpeed) * dt;
            Constrain(r);
            // 壁ぎわの足場：まわりこむと壁にめりこむので、その手前で折り返す（1画面）
            if (_rail == Rail.Platform && _oneScreen && EnsureRoom())
            {
                float over = WallOvershoot(PointAt(r, _s, true, out _, out _, out _).x);
                if (over > 0f && over > WallOvershoot(PointAt(r, before, true, out _, out _, out _).x))
                {
                    _s = before;
                    _dir = -_dir;
                }
            }
        }
        Vector2 p = PointAt(r, _s, _rail != Rail.Room, out _edge, out _tangent, out _normal);
        _body.Warp(p); // 足場に付いている間は位置を直接書く（補間なし。動く足場とずれない）
        Pose();
        return true;
    }

    /// <summary>落ちる決まり（2-4 の表）。1画面のステージだけ</summary>
    void Rules(float dt)
    {
        if (!_oneScreen) return;
        float height = transform.position.y - FloorY();
        _highTime = height > HighLine ? _highTime + dt : 0f;
        bool last = LastThree();
        bool onFloor = _rail == Rail.Room && _edge == 0;
        if (last && onFloor) _floorOnly = true;

        if (_highTime >= HighSeconds) StartWarn();
        else if (_rail == Rail.Room && _edge == 2 && PlayerBelow()) StartWarn();
        else if (last && !onFloor) StartWarn();
        else if (onFloor && Time.time >= _nextHoleCheck)
        {
            // 床が消えている所（穴）に来たら落ちる
            _nextHoleCheck = Time.time + HoleCheckEvery;
            Vector2 feet = transform.position;
            if (Physics2D.Raycast(feet + Vector2.up * 0.3f, Vector2.down, 0.7f, GroundOnly).collider == null) Drop();
        }
    }

    /// <summary>歩く雑魚にもどっている間も、5秒の決まりを守る（1画面だけ）。真下の床へ移して煙を出す</summary>
    void WalkerRules(float dt)
    {
        if (!_oneScreen) return;
        float floor = FloorY();
        float height = transform.position.y - floor;
        _highTime = height > HighLine && _body.IsGrounded ? _highTime + dt : 0f;
        if (_highTime < HighSeconds) return;
        _highTime = 0f;
        Vector2 feet = transform.position;
        if (EnsureRoom()) WarpToFloor(new Vector2(Mathf.Clamp(feet.x, _room.xMin + 1f, _room.xMax - 1f), _room.yMin)); // 部屋の床へ
        else
        {
            RaycastHit2D hit = AppStagePlatforms.RaycastSteady(feet + Vector2.up * 0.3f, Vector2.down, 30f, GroundOnly);
            WarpToFloor(hit.collider != null && hit.distance > 0.01f ? hit.point : new Vector2(feet.x, floor));
        }
    }

    /// <summary>1画面のステージで、部屋の外へ出ていたら、床の上（x は部屋の中）へもどす</summary>
    void GuardRoom()
    {
        if (!_oneScreen || !EnsureRoom()) return;
        Vector2 p = transform.position;
        bool outside = p.x < _room.xMin - 0.05f || p.x > _room.xMax + 0.05f || p.y > _room.yMax + 1f || p.y < _room.yMin - 2f;
        if (!outside) return;
        float x = Mathf.Clamp(p.x, _room.xMin + 1f, _room.xMax - 1f);
        WarpToFloor(new Vector2(x, _room.yMin));
    }

    void WarpToFloor(Vector2 to)
    {
        _body.IgnoreOneWay = false;
        _body.Warp(to);
        Vfx.Smoke(to + Vector2.up * 0.4f);
        if (!TryChooseRail()) ToWalker(false);
    }

    void StartWarn()
    {
        _mode = Mode.Warn;
        _warnUntil = Time.time + WarnSeconds;
        // ほこりが 2つ落ちる
        Vector2 at = transform.position;
        for (int i = 0; i < 2; i++)
        {
            Vector2 from = at + _tangent * UnityEngine.Random.Range(-0.4f, 0.4f) + _normal * 0.05f;
            FxAnim.Sprite(FxArt.Square(1), from, FxKit.OrderFragment, 0.6f, DustColor, 1f, "WormDust")
                .Velocity(new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), -0.6f)).Gravity(6f).FadeFrom(0.5f);
        }
    }

    /// <summary>落ちる：面から体を離して（2-4。重なったまま始めると宙で着地したことになる）、歩く雑魚にまかせる</summary>
    void Drop()
    {
        Vector2 n = _normal;
        float d = n.y > 0.5f ? 0f : n.y < -0.5f ? UnderRelease : SideRelease;
        Vector2 at = ClampToRoom((Vector2)transform.position + n * d);
        ToWalker(true, at);
    }

    /// <summary>歩く雑魚にもどす。falling なら、すり抜け床を無視して床（Ground）まで落とす</summary>
    void ToWalker(bool falling, Vector2? at = null)
    {
        int worldDir = WorldDir();
        if (_visual != null)
        {
            _visual.localRotation = Quaternion.identity;
            _visual.localPosition = _visualHome;
        }
        if (at.HasValue) _body.Warp(at.Value);
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _body.IgnoreOneWay = falling;
        _walker.SetDirection(worldDir);
        _walker.enabled = true;
        ClearRail();
        _highTime = 0f;
        _mode = falling ? Mode.Falling : Mode.Walker;
        _fallAt = Time.time;
    }

    // ───────── レールに乗る（2-3） ─────────

    /// <summary>足元を見て、乗るレールを決める。見つからなければ false（呼んだ側が歩く雑魚にする）</summary>
    bool TryChooseRail()
    {
        Vector2 feet = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(feet + Vector2.up * 0.3f, Vector2.down, 0.8f, GroundAndOneWay);
        if (hit.collider == null) return false;
        bool last = LastThree();
        int worldDir = _walker != null ? _walker.Direction : 1;

        AppPlatform platform = hit.collider.GetComponentInParent<AppPlatform>();
        if (platform != null)
        {
            if (last || !platform.IsSolidNow) return false; // 残り 3体以下：足場ではレールに乗らない
            return AttachPlatformNow(platform, feet.x, false, worldDir > 0 ? -1 : 1); // 上の辺は s が増えると左へ
        }

        bool ground = hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground");
        if (_oneScreen && ground && EnsureRoom() && Mathf.Abs(hit.point.y - _room.yMin) < 0.35f)
        {
            bool ok = AttachRoomNow(new Vector2(feet.x, _room.yMin), worldDir, last); // 下の辺は s が増えると右へ
            return ok;
        }
        if (last) return false;
        if (hit.distance <= 0.01f) return false;  // 壁などの中から調べた（上に乗っていない）
        if (_oneScreen && ground) return false;   // 1画面の床・壁の上は這わない（上だけレールはすり抜け床だけ）

        Bounds b = hit.collider.bounds;
        if (b.size.x > TopOnlyMaxWidth || b.size.x < 0.6f) return false; // 広い床は歩く雑魚のまま
        _topCol = hit.collider;
        _platform = null;
        _platformArt = null;
        _rail = Rail.TopOnly;
        _floorOnly = false;
        if (!TryRect(out Rect r))
        {
            ClearRail();
            return false;
        }
        _s = r.width + r.height + Mathf.Clamp(r.xMax - feet.x, 0f, r.width);
        _dir = worldDir > 0 ? -1 : 1;
        Constrain(r);
        EnterRail();
        return true;
    }

    bool AttachPlatformNow(AppPlatform platform, float worldX, bool underside, int dir)
    {
        if (platform == null || !platform.IsSolidNow) return false;
        Transform art = platform.transform.Find("Art");
        SpriteRenderer sr = art != null ? art.GetComponent<SpriteRenderer>() : null;
        if (sr == null) return false;
        _platform = platform;
        _platformArt = sr;
        _platformBody = platform.GetComponent<Rigidbody2D>();
        _topCol = null;
        _rail = Rail.Platform;
        _floorOnly = false;
        if (!TryRect(out Rect r))
        {
            ClearRail();
            return false;
        }
        float w = r.width, h = r.height;
        _s = underside ? Mathf.Clamp(worldX - r.xMin, 0f, w) : w + h + Mathf.Clamp(r.xMax - worldX, 0f, w);
        _dir = dir >= 0 ? 1 : -1;
        EnterRail();
        return true;
    }

    bool AttachRoomNow(Vector2 point, int dir, bool floorOnly)
    {
        if (!EnsureRoom()) return false;
        _platform = null;
        _platformArt = null;
        _topCol = null;
        _rail = Rail.Room;
        int edge = NearestEdge(_room, point);
        _s = SOf(_room, edge, point);
        _dir = dir >= 0 ? 1 : -1;
        _floorOnly = floorOnly && edge == 0;
        if (_floorOnly) Constrain(_room);
        EnterRail();
        return true;
    }

    void EnterRail()
    {
        _walker.enabled = false;
        _body.IgnoreOneWay = false;
        _rb.interpolation = RigidbodyInterpolation2D.None;
        _hasLastCenter = false;
        _highTime = 0f;
        _mode = Mode.Rail;
        StepRail(0f, false); // その場で体を置いて、絵を面に合わせる
    }

    void ClearRail()
    {
        _rail = Rail.None;
        _platform = null;
        _platformArt = null;
        _platformBody = null;
        _topCol = null;
        _floorOnly = false;
        _hasLastCenter = false;
    }

    /// <summary>床だけ・上だけのレールは、端で折り返す</summary>
    void Constrain(Rect r)
    {
        float w = r.width, h = r.height;
        float lo, hi;
        if (_rail == Rail.TopOnly)
        {
            float m = Mathf.Min(0.3f, w * 0.25f);
            lo = w + h + m;
            hi = 2f * w + h - m;
        }
        else if (_rail == Rail.Room && _floorOnly)
        {
            float m = Mathf.Min(FloorMargin, w * 0.25f);
            lo = m;
            hi = w - m;
        }
        else
        {
            _s = Mathf.Repeat(_s, 2f * (w + h));
            return;
        }
        if (_s < lo) { _s = lo; _dir = 1; }
        else if (_s > hi) { _s = hi; _dir = -1; }
    }

    /// <summary>今のレールの四角（足場は今の場所。動く足場は体の位置で補う）</summary>
    bool TryRect(out Rect r)
    {
        r = default;
        switch (_rail)
        {
            case Rail.Room:
                if (!EnsureRoom()) return false;
                r = _room;
                return true;
            case Rail.Platform:
                if (_platform == null || _platformArt == null) return false;
                return BoundsRect(_platformArt.bounds, _platform.transform, _platformBody, out r);
            case Rail.TopOnly:
                if (_topCol == null) return false;
                Rigidbody2D arb = _topCol.attachedRigidbody;
                return BoundsRect(_topCol.bounds, arb != null ? arb.transform : null, arb, out r);
        }
        return false;
    }

    // 動く足場は Body.position で動くので、FixedUpdate の中では絵（transform）が1コマ遅れている。体の位置との差を足す
    static bool BoundsRect(Bounds b, Transform owner, Rigidbody2D body, out Rect r)
    {
        Vector2 c = b.center;
        if (owner != null && body != null) c += body.position - (Vector2)owner.position;
        Vector2 size = b.size;
        r = new Rect(c - size * 0.5f, size);
        return size.x > 0.2f && size.y > 0.05f;
    }

    /// <summary>部屋の四角（床・左右の壁・天井の内側。2-1）。最初に1回だけ作る</summary>
    bool EnsureRoom()
    {
        if (_roomReady) return true;
        if (_roomTried) return false;
        _roomTried = true;
        Rect area = _pacer != null && _pacer.PlayArea.width > 1f ? _pacer.PlayArea : KitMotion.Area;
        float floor = KitMotion.FloorY();
        float y = floor + WallProbeUp;
        float cx = area.center.x;
        float left = area.xMin, right = area.xMax;
        RaycastHit2D hl = Physics2D.Raycast(new Vector2(cx, y), Vector2.left, WallProbeLength, GroundOnly);
        if (hl.collider != null && hl.distance > 3f) left = hl.point.x;
        RaycastHit2D hr = Physics2D.Raycast(new Vector2(cx, y), Vector2.right, WallProbeLength, GroundOnly);
        if (hr.collider != null && hr.distance > 3f) right = hr.point.x;
        float top = Mathf.Min(area.yMax, ScreenUi.ViewRect().yMax) - CeilingInset;
        if (top - floor < 2f || right - left < 4f) return false;
        _room = Rect.MinMaxRect(left, floor, right, top);
        _roomReady = true;
        return true;
    }

    float FloorY() => EnsureRoom() ? _room.yMin : KitMotion.FloorY();

    /// <summary>部屋の壁からどれだけはみ出しているか（体の半分ぶん内側を線にする）</summary>
    float WallOvershoot(float x) => Mathf.Max(0f, Mathf.Max(_room.xMin + WallClear - x, x - (_room.xMax - WallClear)));

    /// <summary>1画面のステージでは、はなす点を壁の内側に入れる（壁に重なったまま落とすと、壁の上に乗ってしまう）</summary>
    Vector2 ClampToRoom(Vector2 p)
    {
        if (!_oneScreen || !EnsureRoom()) return p;
        p.x = Mathf.Clamp(p.x, _room.xMin + WallClear, _room.xMax - WallClear);
        return p;
    }

    bool LastThree() => _oneScreen && _pacer != null && _pacer.Remaining >= 1 && _pacer.Remaining <= StagePaceTable.MarkFrom;

    bool PlayerBelow()
    {
        Transform p = _pacer != null ? _pacer.Player : null;
        return p != null && Mathf.Abs(p.position.x - transform.position.x) <= CeilingDropRange && p.position.y < transform.position.y;
    }

    /// <summary>今進んでいる横の向き（右 1／左 -1）。縦の面なら、今の歩く向き</summary>
    int WorldDir()
    {
        float x = (_tangent * _dir).x;
        if (_rail != Rail.None && Mathf.Abs(x) > 0.5f) return x > 0f ? 1 : -1;
        Transform p = _pacer != null ? _pacer.Player : null;
        if (_rail != Rail.None && p != null) return p.position.x >= transform.position.x ? 1 : -1;
        return _walker != null ? _walker.Direction : 1;
    }

    /// <summary>絵を面に合わせる（Visual を回す。0/90/180/270 度でパッと切り替え）。左右は進む向き</summary>
    void Pose()
    {
        if (_visual == null) return;
        float angle = Mathf.Atan2(-_normal.x, _normal.y) * Mathf.Rad2Deg; // FromToRotation は 180 度のとき回る軸が決まらないので使わない
        _visual.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 right = new Vector2(_normal.y, -_normal.x);
        float sx = Vector2.Dot(_tangent * _dir, right) >= 0f ? 1f : -1f;
        Vector3 s = _visual.localScale;
        s.x = Mathf.Abs(s.x) * sx;
        _visual.localScale = s;
    }

    // ───────── 四角のふち（左下から反時計回り。下 +x → 右 +y → 上 −x → 左 −y） ─────────

    /// <summary>道のり s の点。edge：0 下・1 右・2 上・3 左。outward なら法線は外向き（足場）、ちがえば内向き（部屋）</summary>
    static Vector2 PointAt(Rect r, float s, bool outward, out int edge, out Vector2 tangent, out Vector2 normal)
    {
        float w = r.width, h = r.height;
        s = Mathf.Repeat(s, 2f * (w + h));
        Vector2 p;
        if (s < w) { edge = 0; p = new Vector2(r.xMin + s, r.yMin); tangent = Vector2.right; normal = Vector2.up; }
        else if (s < w + h) { edge = 1; p = new Vector2(r.xMax, r.yMin + (s - w)); tangent = Vector2.up; normal = Vector2.left; }
        else if (s < 2f * w + h) { edge = 2; p = new Vector2(r.xMax - (s - w - h), r.yMax); tangent = Vector2.left; normal = Vector2.down; }
        else { edge = 3; p = new Vector2(r.xMin, r.yMax - (s - 2f * w - h)); tangent = Vector2.down; normal = Vector2.right; }
        if (outward) normal = -normal;
        return p;
    }

    static int NearestEdge(Rect r, Vector2 p)
    {
        float b = Mathf.Abs(p.y - r.yMin), ri = Mathf.Abs(p.x - r.xMax), t = Mathf.Abs(p.y - r.yMax), l = Mathf.Abs(p.x - r.xMin);
        float m = Mathf.Min(Mathf.Min(b, ri), Mathf.Min(t, l));
        if (m == b) return 0;
        if (m == ri) return 1;
        if (m == t) return 2;
        return 3;
    }

    static float SOf(Rect r, int edge, Vector2 p)
    {
        float w = r.width, h = r.height;
        switch (edge)
        {
            case 0: return Mathf.Clamp(p.x - r.xMin, 0f, w);
            case 1: return w + Mathf.Clamp(p.y - r.yMin, 0f, h);
            case 2: return w + h + Mathf.Clamp(r.xMax - p.x, 0f, w);
            default: return 2f * w + h + Mathf.Clamp(r.yMax - p.y, 0f, h);
        }
    }

    static int GroundOnly => LayerMask.GetMask("Ground");
    static int GroundAndOneWay => LayerMask.GetMask("Ground", "OneWayPlatform");

    // ───────── 例外 ─────────

    void Guard(Action a)
    {
        try { a(); }
        catch (Exception e) { FailSafe(e); }
    }

    /// <summary>例外が出たら：絵をもどし、面から離して、歩く雑魚にもどす。自分は止める（倒せない雑魚を残さない）</summary>
    void FailSafe(Exception e)
    {
        SkinDresser.LogOnce("ZakoWorm", $"ワームで例外（歩く雑魚にもどす）：{e}");
        try
        {
            if (_visual != null)
            {
                _visual.localRotation = Quaternion.identity;
                _visual.localPosition = _visualHome;
            }
            if (_body != null && _rb != null)
            {
                Vector2 n = _normal;
                float d = n.y > 0.5f ? 0f : n.y < -0.5f ? UnderRelease : SideRelease;
                if (_mode == Mode.Rail || _mode == Mode.Warn) _body.Warp(ClampToRoom((Vector2)transform.position + n * d));
                _body.IgnoreOneWay = false;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            if (_walker != null) _walker.enabled = true;
        }
        catch (Exception) { /* これ以上は何もしない */ }
        _mode = Mode.Off;
        enabled = false;
    }
}
