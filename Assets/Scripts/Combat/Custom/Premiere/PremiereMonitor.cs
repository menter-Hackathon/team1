using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro の編集画面の「プログラムモニター」（2026-11-20。docs/premiere-edit-screen-design.md 4〜6章）。
/// 背景の絵の映像の四角（px 120,24 80×45）に、作った動画（4つの場面のループ）を実行時に描く。PremiereTimelineStage が作る（割り付けが有効なときだけ）。
/// ・絵は再生位置 k（0〜1）だけで決まる。32×18 ます（1ます＝1/6 マス）のテクスチャに描く。動きは ます目 1つずつ（少ないコマ）
///   場面：0〜0.26 朝の丘（日がのぼる・題名）／〜0.61 歩く人／〜0.81 ウイルス（BOSS.MP4）／〜1 海と雲。8秒で1周
/// ・下に、スクラブバー（再生した所のうすい青）・青いつまみ・タイムコード（00:秒）・赤い印（Ticks）
/// ・合図（技・ステージが呼ぶ。どれも秒数で元に戻るので、技が途中で消されても映像が止まったままにならない）：
///   Follow（外の再生ヘッドに合わせる。0.2秒呼ばれなければ自分の時計）／Rewind（頭へ巻き戻す）／Hold（止める。つまみが点滅）／
///   Speed（速さ）／Skip（先へ飛ばす）／Flash（白く飛ぶ）／Blank（抜けた絵）／Scanline（白い横の線）／AddTick（スクラブの印）／ShowIcon（右上の小さい印）
/// ・ボスが出たら（EnterBoss）、どの k でも場面3（ウイルス）を映し、目がプレイヤーのいる方へ寄る。ボスが技を出すと目を見開く。
///   正常化でウイルスが消えた部屋になる（B1・B4）
/// ・重ねる順番：背景（-100）の上、-60〜-55（足場 2・予告 6〜8・レーザーツールの黒い列 9・キャラクター 11〜 より後ろ）
/// ・背景の絵（カメラの子）の子に置くので、画面のゆれにも付いてくる。判定は持たない（見た目だけ）
/// </summary>
public class PremiereMonitor : MonoBehaviour
{
    /// <summary>今のステージのモニター（無ければ null。技はこれが null なら何もしない）</summary>
    public static PremiereMonitor Current { get; private set; }

    // ───────── 数値【仮】 ─────────
    const int Cols = 32, Rows = 18;
    const float LoopSeconds = 8f;
    const float FollowGrace = 0.2f;
    const float VirusSceneSeconds = 1.6f; // 場面3 の長さ（ボス戦の間は、この長さでゆれ続ける）
    const float GlintTime = 0.35f;
    const float RewindOnBoss = 0.6f;     // ボスの登場で巻き戻す秒
    const float ApproachUntil = 1.0f;    // 2026-11-20：ボスの登場から、ボスが表示されるまで（BossEntrance の待ち 1秒）
    const int OrderVideo = -60, OrderVeil = -59, OrderScrub = -58, OrderTick = -57, OrderKnob = -56, OrderIcon = -55;

    // ───────── 色（4-3。黒・赤・プレイヤーの黄・真っ白は使わない） ─────────
    static readonly Color32 SkyTop = RuntimeSprites.Hex("7f95b5"), SkyLow = RuntimeSprites.Hex("a9b7c6");
    static readonly Color32 Hill = RuntimeSprites.Hex("7d9a8e"), Ground = RuntimeSprites.Hex("5f8274");
    static readonly Color32 Sun = RuntimeSprites.Hex("e6c9a8");
    static readonly Color32 TitleBar = Color32.Lerp(RuntimeSprites.Hex("a9b7c6"), RuntimeSprites.Hex("d9dde6"), 0.7f);
    static readonly Color32 Head = RuntimeSprites.Hex("d6c2b0"), Body = RuntimeSprites.Hex("6f8fa8"), Leg = RuntimeSprites.Hex("56667a");
    static readonly Color32 Wall = RuntimeSprites.Hex("5d6478"), RoomFloor = RuntimeSprites.Hex("525a6c"), Window = RuntimeSprites.Hex("7f95b5");
    static readonly Color32 Blob = RuntimeSprites.Hex("56506a"), Eye = RuntimeSprites.Hex("d8d4e8");
    static readonly Color32 Sea = RuntimeSprites.Hex("6f93b0"), Horizon = RuntimeSprites.Hex("86a6be"), Cloud = RuntimeSprites.Hex("c9d2dc");
    static readonly Color32 BlankA = RuntimeSprites.Hex("5a5966"), BlankB = RuntimeSprites.Hex("63626f");
    static readonly Color TimecodeBlue = RuntimeSprites.Hex("8fb4dc");

    // ───────── 小さい絵（4-4） ─────────
    static readonly string[] SunRows = { "..oo..", ".oooo.", "oooooo", "oooooo", ".oooo.", "..oo.." };
    static readonly string[] WalkA = { ".hhh.", ".hhh.", "bbbbb", ".bbb.", ".bbb.", ".l.l.", "l...l" };
    static readonly string[] WalkB = { ".hhh.", ".hhh.", "bbbbb", ".bbb.", ".bbb.", "..l..", "..l.." };
    static readonly string[] BlobRows = { "..mmmmmm..", ".mmmmmmmm.", "mmmmmmmmmm", "mmmmmmmmmm", "mmmmmmmmmm", "mmmmmmmmmm", ".mmmmmmmm.", "m.mm..mm.m" };
    static readonly string[] CloudRows = { "..cccc..", ".cccccc.", "cccccccc" };

    class TickMark
    {
        public float K;
        public float Until;
        public Color Color;
        public SpriteRenderer Sr;
    }

    Rect _arena;
    int _layer, _sortingLayer;
    Texture2D _tex;
    readonly Color32[] _px = new Color32[Cols * Rows];
    readonly Color32[] _shown = new Color32[Cols * Rows];
    bool _dirty = true;
    SpriteRenderer _video, _veil, _scan, _played, _knob, _icon;
    readonly SpriteRenderer[] _digits = new SpriteRenderer[4];
    readonly List<TickMark> _ticks = new List<TickMark>();

    float _k;
    float _followUntil = float.NegativeInfinity;
    bool _rewinding;
    float _rewindFrom, _rewindStart, _rewindEnd;
    float _holdUntil = float.NegativeInfinity;
    float _speedMul = 1f, _speedUntil = float.NegativeInfinity;
    float _flashStart, _flashEnd = float.NegativeInfinity;
    float _blankUntil = float.NegativeInfinity;
    float _scanStart, _scanEnd = float.NegativeInfinity;
    float _iconUntil = float.NegativeInfinity;

    bool _bossMode, _clean;
    BossController _boss;
    float _nextBossSearch;
    float _glintUntil = float.NegativeInfinity;
    // 2026-11-20（10章）：ボスがモニターから這い出す
    float _approachStart = float.PositiveInfinity, _approachEnd = float.PositiveInfinity; // 表示される前：ウイルスが画面へ近づく
    float _tugUntil = float.NegativeInfinity;    // 這い出す 1回ごと：映像が下へ 1ドット引っぱられる（10-A4）
    bool _virusOut;                              // 這い出したあと：映像の中にウイルスはいない（技のときだけ目が光る）
    Vector3 _videoHome;

    // ───────── 作る ─────────

    /// <summary>作る（割り付けが有効なときだけ。1ステージに1つ）。owner はステージの物（シーンを合わせる）</summary>
    public static PremiereMonitor Create(Transform owner)
    {
        if (!PremiereEditScreen.Active) return null;
        if (Current != null) Destroy(Current.gameObject);
        var go = new GameObject("PremiereMonitor");
        SpriteRenderer bg = FindBackground(owner);
        Rect arena = BattleArena.GetBounds();
        if (bg != null)
        {
            go.layer = bg.gameObject.layer;
            go.transform.SetParent(bg.transform, false);
            go.transform.localPosition = Vector3.zero;
        }
        else
        {
            if (owner != null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            go.transform.position = PremiereEditScreen.WorldFromPx(arena, 160f, 90f);
        }
        var m = go.AddComponent<PremiereMonitor>();
        m._arena = arena;
        m._layer = go.layer;
        m._sortingLayer = bg != null ? bg.sortingLayerID : 0;
        Current = m;
        m.Build();
        return m;
    }

    /// <summary>
    /// 背景の絵（AppBackgrounds.Current を出している SpriteRenderer。カメラの子）。同じ絵がほかにもあれば、
    /// ステージのシーンの物を先に、その中でいちばん奥（重ねる順番が小さい）の物を選ぶ
    /// </summary>
    static SpriteRenderer FindBackground(Transform owner)
    {
        Sprite s = AppBackgrounds.Current;
        if (s == null) return null;
        SpriteRenderer best = null;
        foreach (var sr in FindObjectsOfType<SpriteRenderer>())
        {
            if (sr == null || sr.sprite != s) continue;
            if (best == null) { best = sr; continue; }
            bool inScene = owner != null && sr.gameObject.scene == owner.gameObject.scene;
            bool bestInScene = owner != null && best.gameObject.scene == owner.gameObject.scene;
            if (inScene != bestInScene) { if (inScene) best = sr; continue; }
            if (sr.sortingOrder < best.sortingOrder) best = sr;
        }
        return best;
    }

    static Vector2 L(float px, float py) => PremiereEditScreen.LocalFromPx(px, py);
    static float U(float px) => PremiereEditScreen.Units(px);

    void Build()
    {
        const float vx = PremiereEditScreen.VideoX, vy = PremiereEditScreen.VideoY, vw = PremiereEditScreen.VideoW, vh = PremiereEditScreen.VideoH;
        _tex = new Texture2D(Cols, Rows, TextureFormat.RGBA32, false)
        {
            name = "Runtime_PremiereMonitor",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        Sprite sprite = Sprite.Create(_tex, new Rect(0, 0, Cols, Rows), new Vector2(0.5f, 0.5f), Cols / U(vw));
        sprite.name = "PremiereMonitor";
        sprite.hideFlags = HideFlags.DontSave;
        Vector2 center = L(vx + vw * 0.5f, vy + vh * 0.5f);
        _video = RuntimeSprites.Show(transform, sprite, center, OrderVideo, 1f, "Video");
        _videoHome = _video.transform.localPosition;

        _veil = PremiereArt.Box(transform, center, new Vector2(U(vw), U(vh)), new Color(1f, 1f, 1f, 0f), OrderVeil, "Flash");
        _veil.enabled = false;
        _scan = PremiereArt.Box(transform, center, new Vector2(U(vw), U(1f)), new Color(0.95f, 0.95f, 1f, 0.85f), OrderVeil, "Scan");
        _scan.enabled = false;

        // スクラブバー：再生した所（うすい青）とつまみ（青）。地は背景
        float sy = L(0f, PremiereEditScreen.ScrubY + PremiereEditScreen.ScrubH * 0.5f).y;
        _played = PremiereArt.Box(transform, new Vector2(center.x, sy), new Vector2(U(1f), U(PremiereEditScreen.ScrubH)), PremiereArt.WithAlpha(PremiereArt.PlayheadBlue, 0.5f), OrderScrub, "Played");
        _knob = PremiereArt.Box(transform, new Vector2(center.x, L(0f, 72.5f).y), new Vector2(U(3f), U(5f)), PremiereArt.PlayheadBlue, OrderKnob, "Knob");

        // タイムコード「00:SS」（今ある数字の絵。ゲームの 1ドット＝1/18 マス）
        Transform tc = PremiereArt.Child(transform, "Timecode", L(PremiereEditScreen.TimecodeX + 1f, 72.5f));
        float g = 1f / 18f, x = 1.5f * g;
        for (int i = 0; i < 4; i++)
        {
            _digits[i] = RuntimeSprites.Show(tc, GenericFrameBits.DigitSprite("0"), new Vector2(x, 0f), OrderKnob, 1f, "Digit");
            _digits[i].color = TimecodeBlue;
            x += 4f * g;
            if (i == 1)
            {
                PremiereArt.Box(tc, new Vector2(x - 0.5f * g, 1f * g), Vector2.one * g, TimecodeBlue, OrderKnob, "Colon");
                PremiereArt.Box(tc, new Vector2(x - 0.5f * g, -1f * g), Vector2.one * g, TimecodeBlue, OrderKnob, "Colon");
                x += 2f * g;
            }
        }

        _icon = RuntimeSprites.Show(transform, PremiereArt.ClockFace, L(vx + vw - 8f, vy + 7f), OrderIcon, 1f, "Icon");
        _icon.enabled = false;
        FixLayers(transform);
        Render(Time.time);
    }

    void FixLayers(Transform root)
    {
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerID = _sortingLayer;
            sr.gameObject.layer = _layer;
        }
    }

    void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.Normalized -= OnNormalized;
            _boss.MoveStarted -= OnMoveStarted;
            _boss.TransitionStarted -= OnTransition;
        }
        if (_tex != null) Destroy(_tex);
        if (Current == this) Current = null;
    }

    // ───────── 合図（5-1） ─────────

    /// <summary>今の再生位置（0〜1）</summary>
    public float K => _k;

    /// <summary>外の再生ヘッドに合わせる（毎フレーム呼ぶ。0.2秒呼ばれなければ、今の所から自分の時計で進む）</summary>
    public void Follow(float k)
    {
        _k = Mathf.Clamp(k, 0f, 0.9999f);
        _followUntil = Time.time + FollowGrace;
    }

    /// <summary>今の所から頭（0）へ、この秒数で巻き戻す。着いたらそこから進む</summary>
    public void Rewind(float seconds)
    {
        float now = Time.time;
        _rewinding = true;
        _rewindFrom = _k;
        _rewindStart = now;
        _rewindEnd = now + Mathf.Max(0.05f, seconds);
    }

    /// <summary>映像を止める（つまみが点滅）。秒数で戻る</summary>
    public void Hold(float seconds) => _holdUntil = Mathf.Max(_holdUntil, Time.time + Mathf.Max(0f, seconds));

    /// <summary>自分の時計の速さを変える。秒数で 1倍に戻る</summary>
    public void Speed(float multiplier, float seconds)
    {
        _speedMul = Mathf.Max(0f, multiplier);
        _speedUntil = Time.time + Mathf.Max(0f, seconds);
    }

    /// <summary>今の所から dk だけ先へ飛ばす（1 を越えたら頭から。ジャンプカット）</summary>
    public void Skip(float dk) => _k = Mathf.Repeat(_k + dk, 1f);

    /// <summary>映像の上に白い幕（8割 → 0）。ディップ トゥ ホワイト</summary>
    public void Flash(float seconds)
    {
        _flashStart = Time.time;
        _flashEnd = _flashStart + Mathf.Max(0.05f, seconds);
    }

    /// <summary>映像を「抜けた絵」（灰色の市松）にする。秒数で戻る</summary>
    public void Blank(float seconds) => _blankUntil = Mathf.Max(_blankUntil, Time.time + Mathf.Max(0f, seconds));

    /// <summary>白い横の線が、映像の上から下へ 1本走る</summary>
    public void Scanline(float seconds)
    {
        _scanStart = Time.time;
        _scanEnd = _scanStart + Mathf.Max(0.05f, seconds);
    }

    /// <summary>スクラブバーの上に小さい印（k の所。2×3 ドット）を、秒数だけ出す</summary>
    public void AddTick(float k, float seconds, Color color)
    {
        var t = new TickMark { K = Mathf.Clamp01(k), Until = Time.time + Mathf.Max(0.05f, seconds), Color = color };
        float px = PremiereEditScreen.VideoX + PremiereEditScreen.VideoW * t.K;
        t.Sr = PremiereArt.Box(transform, L(px, 69.5f), new Vector2(U(2f), U(3f)), color, OrderTick, "Tick");
        t.Sr.sortingLayerID = _sortingLayer;
        _ticks.Add(t);
    }

    /// <summary>映像の右上に小さい印（時計・▶▶ など）を、秒数だけ出す</summary>
    public void ShowIcon(Sprite sprite, float seconds)
    {
        if (_icon == null || sprite == null) return;
        _icon.sprite = sprite;
        _iconUntil = Time.time + Mathf.Max(0.05f, seconds);
    }

    /// <summary>
    /// ボスが出た：映像を頭へ巻き戻し、ここからはウイルスを映す。2026-11-20（10章）：巻き戻したあと、ボスが表示されるまで、
    /// ウイルスが画面へ近づいて大きくなる（白い横の線が走る）
    /// </summary>
    public void EnterBoss()
    {
        if (_bossMode || _clean) return;
        _bossMode = true;
        Rewind(RewindOnBoss);
        _nextBossSearch = 0f;
        if (!PremiereBossCrawl.Enabled) return; // 這い出す演出を切っているときは、予告も出さない（今のまま）
        float now = Time.time;
        _approachStart = now + RewindOnBoss;
        _approachEnd = now + ApproachUntil;
    }

    /// <summary>
    /// 2026-11-20（10章）：ボスの本物の体が映像から這い出し始めた。映像の中のかたまり（2倍）は本物の体と入れかわって消え、白い横の線が 1本走る。
    /// このあと、映像の中は空の部屋（技を出すときだけ目が光る。10-3 案1）
    /// </summary>
    public void OnBossCrawl(float seconds)
    {
        _approachEnd = Mathf.Min(_approachEnd, Time.time);
        _virusOut = true;
        Scanline(0.25f);
    }

    /// <summary>2026-11-20（10-A4）：這い出す 1回ごとに、映像が下へ 1ドット引っぱられて戻る</summary>
    public void Tug() => _tugUntil = Time.time + 0.08f;

    /// <summary>2026-11-20（10-5）：這い出す演出の片づけ（ゆれを止め、映像の四角を元の位置へ。かたまり 2倍を終わらせる）</summary>
    public void EndBossCrawl()
    {
        _approachEnd = Mathf.Min(_approachEnd, Time.time);
        _tugUntil = float.NegativeInfinity;
        if (_video != null) _video.transform.localPosition = _videoHome;
    }

    /// <summary>
    /// モニターの下のまん中から to へ、細い白い光の線（ゲームの 1ドット、5割。当たらない）を seconds 秒（B3）。
    /// parent（技の部品）の子に作るので、技が消されたら一緒に消える
    /// </summary>
    public void Beam(Transform parent, Vector2 to, float seconds, int order)
    {
        if (parent == null) return;
        Vector2 from = BottomCenter;
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.1f) return;
        SpriteRenderer sr = PremiereArt.Box(parent, Vector2.zero, new Vector2(1f / 18f, len), new Color(1f, 1f, 1f, 0.5f), order, "MonitorBeam");
        sr.transform.position = (Vector3)(from + d * 0.5f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        Destroy(sr.gameObject, Mathf.Max(0.05f, seconds));
    }

    /// <summary>部屋の x（ワールド）を、スクラブバーの k に直す（部屋の左の壁＝0、右の壁＝1）</summary>
    public float KAt(float worldX) => Mathf.InverseLerp(_arena.xMin, _arena.xMax, worldX);

    /// <summary>映像の四角のまん中（ワールド。画面のゆれも入る）</summary>
    public Vector2 VideoCenter => _video != null ? (Vector2)_video.transform.position : Vector2.zero;
    /// <summary>映像の四角の大きさ（マス）</summary>
    public Vector2 VideoSize => new Vector2(U(PremiereEditScreen.VideoW), U(PremiereEditScreen.VideoH)) * Mathf.Abs(transform.lossyScale.x);
    /// <summary>映像の四角の下のまん中（技の出どころ）</summary>
    public Vector2 BottomCenter => VideoCenter - new Vector2(0f, VideoSize.y * 0.5f);

    // ───────── 進める ─────────

    void Update()
    {
        float now = Time.time;
        StepClock(now, Time.deltaTime);
        WatchBoss(now);
        Render(now);
        UpdateOverlays(now);
    }

    void StepClock(float now, float dt)
    {
        if (now < _followUntil) return; // 外の再生ヘッドが決める
        if (_rewinding)
        {
            float t = Mathf.InverseLerp(_rewindStart, _rewindEnd, now);
            _k = Mathf.Lerp(_rewindFrom, 0f, t);
            if (t >= 1f) _rewinding = false;
            return;
        }
        if (now < _holdUntil) return;
        float mul = now < _speedUntil ? _speedMul : 1f;
        _k = Mathf.Repeat(_k + dt * mul / LoopSeconds, 1f);
    }

    void WatchBoss(float now)
    {
        if (!_bossMode || _boss != null || now < _nextBossSearch) return;
        _nextBossSearch = now + 0.5f;
        _boss = FindObjectOfType<BossController>();
        if (_boss == null) return;
        _boss.Normalized += OnNormalized;
        _boss.MoveStarted += OnMoveStarted;
        _boss.TransitionStarted += OnTransition;
    }

    /// <summary>第2形態に入る：白く飛んで、横の線が走る（B5。飾り）</summary>
    void OnTransition()
    {
        Flash(0.3f);
        Scanline(0.3f);
    }

    void OnNormalized(SkillDefinition _)
    {
        _bossMode = false;
        _clean = true;
        Flash(0.3f);
    }

    void OnMoveStarted(AttackData _) => _glintUntil = Time.time + GlintTime;

    void UpdateOverlays(float now)
    {
        float vx = PremiereEditScreen.VideoX, vw = PremiereEditScreen.VideoW;
        // スクラブ：つまみと再生した所
        float knobPx = vx + vw * _k;
        Vector3 kp = _knob.transform.localPosition;
        _knob.transform.localPosition = new Vector3(L(knobPx, 0f).x, kp.y, 0f);
        bool holding = now < _holdUntil && !_rewinding && now >= _followUntil;
        _knob.enabled = !holding || Mathf.FloorToInt(now / 0.12f) % 2 == 0;
        float playedW = Mathf.Max(0.001f, U(vw * _k));
        _played.enabled = _k > 0.005f;
        GenericFrameKit.SetSize(_played, new Vector2(playedW, U(PremiereEditScreen.ScrubH)));
        Vector3 pp = _played.transform.localPosition;
        _played.transform.localPosition = new Vector3(L(vx, 0f).x + playedW * 0.5f, pp.y, 0f);

        // タイムコード（00:SS。SS＝k×8秒）
        int ss = Mathf.FloorToInt(_k * LoopSeconds) % 60;
        int[] v = { 0, 0, ss / 10, ss % 10 };
        for (int i = 0; i < 4; i++) if (_digits[i] != null) _digits[i].sprite = GenericFrameBits.DigitSprite(v[i].ToString());

        // 2026-11-20（10章）：表示の前はウイルスが近づいて映像が 1ドットずつゆれる。這い出す 1回ごとに下へ 1ドット引っぱられる
        if (_video != null)
        {
            float j = U(1f);
            Vector3 off = Vector3.zero;
            if (now >= _approachStart && now < _approachEnd) off = new Vector3(Random.Range(-1, 2) * j, Random.Range(-1, 2) * j, 0f);
            else if (now < _tugUntil) off = new Vector3(0f, -j, 0f);
            _video.transform.localPosition = _videoHome + off;
        }
        // 白い幕
        bool flashing = now < _flashEnd;
        _veil.enabled = flashing;
        if (flashing)
        {
            float t = Mathf.InverseLerp(_flashStart, _flashEnd, now);
            _veil.color = new Color(1f, 1f, 1f, 0.8f * (1f - t));
        }
        // 白い横の線
        bool scanning = now < _scanEnd;
        _scan.enabled = scanning;
        if (scanning)
        {
            float t = Mathf.InverseLerp(_scanStart, _scanEnd, now);
            float py = PremiereEditScreen.VideoY + 0.5f + (PremiereEditScreen.VideoH - 1f) * t;
            Vector3 sp = _scan.transform.localPosition;
            _scan.transform.localPosition = new Vector3(sp.x, L(0f, py).y, 0f);
        }
        // 右上の印
        _icon.enabled = now < _iconUntil && Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        // スクラブの印（白く飛んでいる間は白）
        for (int i = _ticks.Count - 1; i >= 0; i--)
        {
            TickMark t = _ticks[i];
            if (now >= t.Until || t.Sr == null)
            {
                if (t.Sr != null) Destroy(t.Sr.gameObject);
                _ticks.RemoveAt(i);
                continue;
            }
            t.Sr.color = flashing ? Color.white : t.Color;
        }
    }

    // ───────── 映像を描く（4-4） ─────────

    void Render(float now)
    {
        if (now < _blankUntil) DrawBlank();
        else if (_bossMode) DrawVirus(Mathf.Repeat(_k * LoopSeconds / VirusSceneSeconds, 1f), true, now);
        else if (_k < 0.26f) DrawHills(_k / 0.26f);
        else if (_k < 0.61f) DrawWalk((_k - 0.26f) / 0.35f);
        else if (_k < 0.81f) DrawVirus((_k - 0.61f) / 0.2f, false, now);
        else DrawSea((_k - 0.81f) / 0.19f);
        Upload();
    }

    void Upload()
    {
        bool changed = _dirty;
        for (int i = 0; i < _px.Length && !changed; i++)
        {
            if (_px[i].r != _shown[i].r || _px[i].g != _shown[i].g || _px[i].b != _shown[i].b || _px[i].a != _shown[i].a) changed = true;
        }
        if (!changed) return;
        System.Array.Copy(_px, _shown, _px.Length);
        _tex.SetPixels32(_px);
        _tex.Apply(false);
        _dirty = false;
    }

    void Fill(int x0, int y0, int w, int h, Color32 c)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(Rows, y0 + h); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(Cols, x0 + w); x++)
                _px[(Rows - 1 - y) * Cols + x] = c; // テクスチャは下の行から
    }

    void Stamp(string[] rows, int x0, int y0, System.Func<char, Color32?> pal)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                Color32? col = pal(row[c]);
                if (col.HasValue) Fill(x0 + c, y0 + r, 1, 1, col.Value);
            }
        }
    }

    void Sky()
    {
        Fill(0, 0, Cols, 6, SkyTop);
        Fill(0, 6, Cols, 5, SkyLow);
    }

    void Land()
    {
        Fill(0, 11, Cols, 3, Hill);
        Fill(3, 10, 7, 1, Hill);   // 丘のふくらみ（小さく）
        Fill(19, 10, 8, 1, Hill);
        Fill(0, 14, Cols, 4, Ground);
    }

    static Color32? SunPal(char c) => c == 'o' ? Sun : (Color32?)null;

    /// <summary>場面1：朝の丘。日がのぼる（7段）。後半に題名の棒</summary>
    void DrawHills(float u)
    {
        Sky();
        int top = 9 - Mathf.Min(6, Mathf.FloorToInt(u * 7f));
        Stamp(SunRows, 22, top, SunPal);
        Land();
        if (u >= 0.5f)
        {
            Fill(10, 7, 12, 2, TitleBar);
            Fill(12, 10, 8, 1, TitleBar);
        }
    }

    /// <summary>場面2：同じ丘を、小さい人が左から右へ歩く（1ますずつ。足は 2コマ）</summary>
    void DrawWalk(float u)
    {
        Sky();
        Stamp(SunRows, 22, 3, SunPal);
        Land();
        int col = 3 + Mathf.Min(21, Mathf.FloorToInt(u * 22f));
        Stamp(col % 2 == 0 ? WalkA : WalkB, col, 7, c => c == 'h' ? Head : c == 'b' ? Body : c == 'l' ? Leg : (Color32?)null);
    }

    /// <summary>場面3：うす暗い部屋にウイルスのかたまり（上下にゆれる。目は白っぽい）。boss：目がプレイヤーの方へ寄る・技で見開く。正常化のあとは空の部屋</summary>
    void DrawVirus(float u, bool boss, float now)
    {
        Fill(0, 0, Cols, 13, Wall);
        Fill(0, 13, Cols, 5, RoomFloor);
        Fill(3, 3, 6, 5, Window);
        if (_clean) return;
        if (boss && now >= _approachStart && now < _approachEnd)
        {
            // 2026-11-20（10章）：表示される前、ウイルスが画面へ近づいて大きくなる（2倍）。横のノイズの線
            DrawBlobBig(now);
            return;
        }
        if (boss && _virusOut)
        {
            // 這い出したあと：映像の中は空の部屋。技を出すときだけ、暗がりに目が光る
            if (now < _glintUntil)
            {
                int lk = LookDirection();
                Fill(13 + lk, 9, 2, 1, Eye);
                Fill(17 + lk, 9, 2, 1, Eye);
            }
            return;
        }
        int top = 6 + Mathf.FloorToInt(u * 6f) % 2;
        Stamp(BlobRows, 11, top, c => c == 'm' ? Blob : (Color32?)null);
        bool closed = u >= 0.45f && u <= 0.55f;
        bool glint = boss && now < _glintUntil;
        if (closed && !glint) return;
        int look = boss ? LookDirection() : 0;
        int eyeRow = top + 3;
        Fill(11 + 2 + look, eyeRow, 2, glint ? 2 : 1, Eye);
        Fill(11 + 6 + look, eyeRow, 2, glint ? 2 : 1, Eye);
        if (glint) { Fill(11 + 2 + look, eyeRow - 1, 2, 1, Eye); Fill(11 + 6 + look, eyeRow - 1, 2, 1, Eye); }
    }

    /// <summary>2026-11-20（10章）：ウイルスのかたまりを 2倍で、映像のまん中に（画面のすぐ前まで来た）。ノイズの線が 2本走る</summary>
    void DrawBlobBig(float now)
    {
        const int scale = 2;
        int w = BlobRows[0].Length * scale, h = BlobRows.Length * scale;
        int x0 = (Cols - w) / 2, y0 = Rows - h; // 下の端に合わせる（下から出てくる）
        for (int r = 0; r < BlobRows.Length; r++)
            for (int c = 0; c < BlobRows[r].Length; c++)
                if (BlobRows[r][c] == 'm') Fill(x0 + c * scale, y0 + r * scale, scale, scale, Blob);
        int eyeRow = y0 + 3 * scale;
        Fill(x0 + 2 * scale, eyeRow, 2 * scale, scale, Eye);
        Fill(x0 + 6 * scale, eyeRow, 2 * scale, scale, Eye);
        int band = Mathf.FloorToInt(now * 24f) % Rows;
        Fill(0, band, Cols, 1, BlankB);
        Fill(0, (band + 7) % Rows, Cols, 1, BlankA);
    }

    /// <summary>プレイヤーのいる方（左 -1・まん中 0・右 1）</summary>
    int LookDirection()
    {
        StagePacer pacer = StagePacer.Current;
        Transform p = pacer != null ? pacer.Player : null;
        if (p == null) return 0;
        float third = _arena.width / 6f;
        float dx = p.position.x - _arena.center.x;
        return dx < -third ? -1 : dx > third ? 1 : 0;
    }

    /// <summary>場面4：海と雲。雲がゆっくり流れる</summary>
    void DrawSea(float u)
    {
        Sky();
        Fill(0, 11, Cols, 1, Horizon);
        Fill(0, 12, Cols, 6, Sea);
        System.Func<char, Color32?> pal = c => c == 'c' ? Cloud : (Color32?)null;
        Stamp(CloudRows, 4 + Mathf.Min(6, Mathf.FloorToInt(u * 7f)), 2, pal);
        Stamp(CloudRows, 18 + Mathf.Min(4, Mathf.FloorToInt(u * 5f)), 5, pal);
    }

    /// <summary>抜けた絵：灰色の市松（4ますごと）</summary>
    void DrawBlank()
    {
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
                _px[(Rows - 1 - y) * Cols + x] = ((x / 4) + (y / 4)) % 2 == 0 ? BlankA : BlankB;
    }
}

/// <summary>
/// 【仮】モニターから飛び出す、当たらない絵（2026-11-20。docs/premiere-edit-screen-design.md 6章）。
/// startAt まではかくれていて、endAt までに from から to（毎フレーム読む）へ動き、大きさを scaleFrom → scaleTo に変え、endAt で消える。
/// 技の部品（Effect）の子に作るので、技が消されたら一緒に消える。赤いふちは付けない（当たる物と見分ける）
/// </summary>
public class PremiereMonitorFlyer : MonoBehaviour
{
    Vector2 _from;
    System.Func<Vector2> _to;
    Vector3 _scaleFrom, _scaleTo;
    float _startAt, _endAt;
    SpriteRenderer[] _renderers;

    /// <summary>映像のコマ（白いふち・中は紫の 6割）。sizeFrom の四角を作り、sizeTo の大きさへちぢめる</summary>
    public static PremiereMonitorFlyer Frame(Transform parent, Vector2 from, Vector2 sizeFrom, System.Func<Vector2> to, Vector2 sizeTo, float startAt, float endAt, int order)
    {
        Transform root = PremiereArt.Child(parent, "MonitorFrame", Vector2.zero);
        float edge = 2f / 18f;
        PremiereArt.Box(root, Vector2.zero, sizeFrom, new Color(1f, 1f, 1f, 0.7f), order, "Edge");
        PremiereArt.Box(root, Vector2.zero, sizeFrom - Vector2.one * edge * 2f, PremiereArt.WithAlpha(PremiereTimelineArt.ClipPurple, 0.6f), order + 1, "Fill");
        Vector3 to3 = new Vector3(sizeTo.x / Mathf.Max(0.01f, sizeFrom.x), sizeTo.y / Mathf.Max(0.01f, sizeFrom.y), 1f);
        return Setup(root, from, to, Vector3.one, to3, startAt, endAt);
    }

    /// <summary>小さい印（時計・▶▶ など）。半透明 6割で飛ぶ</summary>
    public static PremiereMonitorFlyer Icon(Transform parent, Sprite sprite, Vector2 from, System.Func<Vector2> to, float startAt, float endAt, int order)
    {
        Transform root = PremiereArt.Child(parent, "MonitorIcon", Vector2.zero);
        SpriteRenderer sr = RuntimeSprites.Show(root, sprite, Vector2.zero, order, 1f, "Art");
        sr.color = new Color(1f, 1f, 1f, 0.6f);
        return Setup(root, from, to, Vector3.one * 0.6f, Vector3.one * 1.2f, startAt, endAt);
    }

    static PremiereMonitorFlyer Setup(Transform root, Vector2 from, System.Func<Vector2> to, Vector3 scaleFrom, Vector3 scaleTo, float startAt, float endAt)
    {
        var f = root.gameObject.AddComponent<PremiereMonitorFlyer>();
        f._from = from;
        f._to = to;
        f._scaleFrom = scaleFrom;
        f._scaleTo = scaleTo;
        f._startAt = startAt;
        f._endAt = Mathf.Max(startAt + 0.05f, endAt);
        f._renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        f.Apply(Time.time);
        return f;
    }

    void Update() => Apply(Time.time);

    void Apply(float now)
    {
        if (now >= _endAt)
        {
            Destroy(gameObject);
            return;
        }
        bool shown = now >= _startAt;
        foreach (var sr in _renderers) if (sr != null && sr.enabled != shown) sr.enabled = shown;
        if (!shown) return;
        float t = Mathf.InverseLerp(_startAt, _endAt, now);
        float e = 1f - (1f - t) * (1f - t); // 出だしが速く、着く所でゆっくり
        Vector2 to = _to != null ? _to() : _from;
        transform.position = new Vector3(Mathf.Lerp(_from.x, to.x, e), Mathf.Lerp(_from.y, to.y, e), 0f);
        transform.localScale = Vector3.Lerp(_scaleFrom, _scaleTo, e);
    }
}
