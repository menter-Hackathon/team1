using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】蝕まれるウィンドウ（2026-11-20。docs/window-stage-and-pickup-design.md 1-8・7-3）。
/// ウィンドウのステージで、ウィンドウ 1枚ごとの Texture2D（StageWindow.Texture。そのウィンドウだけの絵）を実行時に書きかえる。
/// ・汚れは「ウイルスのいる所」から、ウィンドウの内側へだんだん広がる（7-3。まさぴ〜さんの決めたこと）：
///   ワーム（這っているふち）・ワームの巣（下の端）・ルートキット（出た所）・マイナー（立っている所。下へ細長く）・
///   ほかの雑魚（上に立っている所）・とぶ雑魚（ウィンドウの前）・ボス戦（ボスに近いふち）が「種」を置く
/// ・ドットごとに「汚れが届いた時刻」（Arrive）と「穴になる時刻」（HoleAt）を持つ。届いたばかりはマゼンタ → 紫 → 6秒から 3割が濃い紫。
///   強い種（巣・ルートキット）の近くは 8秒後に穴（透明。後ろが見える）。タイトルバーの行には穴をあけない
/// ・ボス戦はゲージで広がる（今ある種の半径を 最大 ×（1 ＋ 0.6 × ゲージ）まで）。上限：雑魚の時間 45%・ボス戦 70%（1枚の中の割合）
/// ・かじり跡（ワームが通ったふち）・横にずれる乱れ・題名の字がくずれる、は今までどおり（乱れ・題名は「強さ」level。場面とゲージ）
/// ・ボスの正常化で 1.2秒かけて元に戻る（新しく汚れた所から消え、種へちぢんで帰る。戻った境目が白く光る）
/// ・色は紫・濃い紫・マゼンタ（と戻るときの白）だけ。赤は使わない。字の説明は出さない
/// ・MacUi.WindowSprite の共有の絵にはさわらない。Art の絵のテクスチャと StageWindow.Texture が同じで、ほかと共有していない物だけを書く
/// ・書きこみ（SetPixels32 + Apply）は変わったウィンドウだけ、1枚 10回/秒まで（戻る 1.2秒の間だけ 20回/秒）
/// ・例外が出たら 1回だけログを出して、絵を元に戻して自分を止める（ステージは止めない）
/// 数値はすべて【仮】
/// </summary>
public class WindowCorrosion : MonoBehaviour
{
    /// <summary>false で止める（絵は元に戻す）</summary>
    public static bool Enabled = true;
    /// <summary>確認用：0〜1 を入れると、強さ（乱れ・題名）をその値に固定（-1＝切る）。紫の量は変わらない</summary>
    public static float ForceLevel = -1f;
    /// <summary>確認用：汚れの広がる速さを何倍にするか（1＝ふつう）</summary>
    public static float SpeedScale = 1f;
    /// <summary>今のステージの係（ウィンドウのステージでなければ null）</summary>
    public static WindowCorrosion Current { get; private set; }

    /// <summary>汚れの元の種類</summary>
    public enum Source { Worm, Nest, Rootkit, Miner, Standing, Flyer, Boss }

    /// <summary>今の強さ（0〜1。乱れ・題名）</summary>
    public float Level => _level;
    /// <summary>汚れたドットの割合（全部のウィンドウ。0〜1）</summary>
    public float Coverage => _coverage;
    /// <summary>i 枚目（奥から）のウィンドウの汚れたドットの割合（無ければ 0）</summary>
    public float CoverageOf(int i) => i >= 0 && i < _wins.Count ? _wins[i].Coverage : 0f;
    /// <summary>ウィンドウの枚数（書いてよい物だけ）</summary>
    public int WindowCount => _wins.Count;
    /// <summary>今ある種の数（広がっている物＋広がり終わった物）</summary>
    public int SeedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _wins.Count; i++) n += _wins[i].Seeds.Count;
            return n;
        }
    }
    /// <summary>これまでに置いた種の数（種類ごと。確認用）</summary>
    public int SeedsPlaced(Source kind) => _placed[(int)kind];
    /// <summary>かじり跡の数（全部のウィンドウの合計）</summary>
    public int BiteCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _wins.Count; i++) n += _wins[i].Bites.Count;
            return n;
        }
    }

    /// <summary>確認用：i 枚目のウィンドウの、ワールドの点 world に種を置く（上限は守る）。置けたら true</summary>
    public bool DebugSeed(int i, Vector2 world, Source kind)
    {
        if (i < 0 || i >= _wins.Count || _restoring || _restored) return false;
        Win w = _wins[i];
        return Place(w, kind, ToPx(w, world), -1000 - i, false, Time.time, true) != null;
    }

    // ───────── 数値【仮】（7-3 の表） ─────────
    struct SourceSpec
    {
        public float Speed;    // 広がる速さ（ドット/秒）
        public float Reach;    // 最大の半径（ドット）
        public int Cap;        // 1枚の上限
        public bool Holes;     // 穴をあけるか
        public float YScale;   // たての距離に掛ける（0.5＝下へ細長く）
        public float Interval; // 時間で置く物の間隔（秒）
        public float Spacing;  // 同じ元・同じ種類の種から、これだけ離す（ドット）
    }

    static readonly SourceSpec[] Specs =
    {
        new SourceSpec { Speed = 3f,   Reach = 9f,  Cap = 48, Holes = false, YScale = 1f,   Interval = 0f,   Spacing = 10f }, // Worm：這っているふち
        new SourceSpec { Speed = 1.2f, Reach = 46f, Cap = 2,  Holes = true,  YScale = 1f,   Interval = 0f,   Spacing = 0f },  // Nest：下の端から上へ（巣がある間だけ）
        new SourceSpec { Speed = 5f,   Reach = 12f, Cap = 12, Holes = true,  YScale = 1f,   Interval = 0f,   Spacing = 0f },  // Rootkit：出た所（Rising）
        new SourceSpec { Speed = 4f,   Reach = 10f, Cap = 12, Holes = false, YScale = 0.5f, Interval = 1.0f, Spacing = 0f },  // Miner：足元。下へ細長く（ほった穴）
        new SourceSpec { Speed = 2.5f, Reach = 6f,  Cap = 16, Holes = false, YScale = 1f,   Interval = 1.5f, Spacing = 12f }, // Standing：上に立つ雑魚
        new SourceSpec { Speed = 2f,   Reach = 6f,  Cap = 8,  Holes = false, YScale = 1f,   Interval = 1.0f, Spacing = 0f },  // Flyer：ウィンドウの前に 1秒
        new SourceSpec { Speed = 2f,   Reach = 30f, Cap = 1,  Holes = false, YScale = 1f,   Interval = 0f,   Spacing = 0f },  // Boss：種の無いウィンドウの、ボスに近いふち
    };

    const int SeedsPerWindow = 96;          // 1枚の種の合計
    const float ScanEvery = 0.15f;          // 元を探す・かじり跡を見る間隔（秒）
    const float GrowEvery = 0.2f;           // 種を広げる間隔（秒。届く時刻は先回りして書くので、見た目はなめらか）
    const float SourceGrace = 0.5f;         // 元をこの秒数見なかったら、その元の巣の種を止める
    const float StandTolerance = 0.22f;     // 足元がウィンドウの上の端からこれ以内なら「上に立っている」（マス）
    const float RootkitReachUp = 2.5f;      // 床から出たルートキット：真上この高さまでのウィンドウの下の端を汚す（マス）
    const float CoverGrow = 0.3f;           // 見えているか：手前のウィンドウの四角をこれだけふくらませる（マス）
    const float WobbleMin = 0.65f, WobbleMax = 1.5f; // にじみ（距離に掛ける）
    const float FrontAge = 0.4f;            // 届いてからこの秒数はマゼンタ（広がっている先）
    const float DarkAge = 6f;               // この秒数から、3割が濃い紫（まだら）
    const int DarkShare = 77;               // 3割（0〜255）
    const int RootkitDarkShare = 153;       // ルートキットの汚れは 6割（ひび）
    const float StainPurple = 0.82f, StainDark = 0.92f; // 元の絵の上に重ねる濃さ
    const float HoleDelay = 8f;             // 届いてから 8秒で穴
    const float HoleShare = 0.35f;          // 種の半径の 35% まで
    const float SettleAge = HoleDelay + 1f; // 最後に届いてから、これだけたてば絵は変わらない
    const int MinProtectRows = 9;           // 上から 9ドット（タイトルバー）は透明にしない
    const float CapZako = 0.45f, CapBoss = 0.70f; // 1枚の中の割合の上限（こえたら新しいドットを足さない）
    const float BossGrow = 0.6f;            // ボス戦：最大の半径 ×（1 ＋ 0.6 × ゲージ）
    const float GaugeStep = 0.03f;          // ゲージがこれだけ変わったら広げ直す
    const float RestoreSeconds = 1.2f;      // 正常化で元に戻る時間
    const float FlashShare = 0.08f;         // 戻った境目（届いた時刻の幅の 8%）は白
    const float Rise = 0.05f, Fall = 0.2f;  // 強さが目標へ近づく速さ（毎秒）
    const float BiteReach = 0.3f;           // ワームがふちからこれ以内なら、かじる（マス）
    const int BiteMax = 60;                 // 1枚の上限
    const int BiteSpacing = 4;              // これより近い所には、もうかじらない（ドット）
    const float GlitchFrom = 0.50f;
    const float GlitchMinGap = 0.8f, GlitchMaxGap = 2.0f;
    const float GlitchSeconds = 0.08f;
    const float TitleFrom = 0.60f;
    const float TitleEvery = 0.5f;
    const float UploadInterval = 0.1f;      // 1枚 10回/秒まで
    const float RestoreUploadInterval = 0.05f;
    const float RefRetry = 0.5f;            // 監督・ボスが見つからないとき、探し直す間隔
    const string Glyphs = "#%&?";

    static readonly Color32 Purple = new Color32(0x6A, 0x2A, 0x8C, 0xFF);
    static readonly Color32 DarkPurple = new Color32(0x1E, 0x08, 0x30, 0xFF);
    static readonly Color32 Magenta = new Color32(0xED, 0x38, 0xFF, 0xFF);
    static readonly Color32 Flash = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color TitlePurple = new Color32(0x6A, 0x2A, 0x8C, 0xFF);
    static readonly Color TitleMagenta = new Color32(0xED, 0x38, 0xFF, 0xFF);

    struct Bite
    {
        public int X, Y, R;
        public bool Top;     // 上の辺（乗る面）：透明にしないで紫にするだけ
        public float Time;   // かじった時刻（戻るとき、この時刻より新しい物から消える）
    }

    sealed class Seed
    {
        public int SourceId;
        public Source Kind;
        public bool Bound;       // 元がいる間だけ広がる（巣）
        public float X, Y;       // ドット（左下から）
        public float Speed, Reach, ReachNow, YScale;
        public bool Holes;
        public float GrowStart, GrowFrom; // 半径 r(t) ＝ GrowFrom ＋ 速さ ×（t − GrowStart）
        public float Done;       // 書き終えた半径
        public bool Growing = true;
    }

    sealed class Win
    {
        public StageWindow Src;
        public Texture2D Tex;
        public int W, H, Protect;
        public float[] Arrive;       // 汚れが届いた時刻（float.MaxValue＝まだ）
        public float[] HoleAt;       // 穴になる時刻（float.MaxValue＝ならない）
        public byte[] KindAt;        // いちばん先に届いた種の種類
        public float[] Wobble;       // にじみ（0.65〜1.5）
        public byte[] Rand;          // まだらの乱数（0〜255）
        public bool[] HoleMask;      // 絵を作るときの作業用
        public Color32[] Out;
        public readonly List<Seed> Seeds = new List<Seed>();
        public readonly int[] KindCount = new int[7];
        public readonly List<Bite> Bites = new List<Bite>();
        public System.Random Rng;
        public float LastArrive = float.MinValue; // いちばん新しく届いた時刻（ここから SettleAge の間は書き直す）
        public float Coverage;
        public int Opaque;           // 透明でないドットの数
        public float NextUpload;
        public bool Dirty, Force, Clean = true;
        public bool Glitching;
        public int GlitchY, GlitchH, GlitchShift;
        public TextMesh[] Texts;
        public Color[] TextColors;   // 元の TextMesh.color（そのまま書き戻す）
        public string[] TextOriginal;
        public string TitleBase;
        public Color TitleBroken;
        public bool Broken;
    }

    sealed class SourceState
    {
        public float SeenAt;
        public Win Win;              // いちばん新しく種を置いた（立っている・前にいる）ウィンドウ
        public Seed Last;            // いちばん新しく置いた種
        public float NextAt;         // 時間で置く物の次の時刻
        public float InsideSince;    // とぶ雑魚：今のウィンドウの前に来た時刻
        public bool WasRising;       // ルートキット：前に見たとき Rising だったか
    }

    readonly List<Win> _wins = new List<Win>();
    readonly Dictionary<int, SourceState> _sources = new Dictionary<int, SourceState>();
    readonly int[] _placed = new int[7];
    readonly List<Health> _scanBuf = new List<Health>();
    readonly List<Health> _waveBuf = new List<Health>();
    readonly List<int> _dropBuf = new List<int>();
    float _level;
    float _coverage;
    bool _started;
    bool _failed;
    bool _idleClean = true;

    StageDirector _director;
    BossController _boss;
    BossController _hooked;
    Collider2D _bossCol;
    EnemyWave _wave;
    BossEntrance _entrance;
    float _nextRefTry;
    bool _normalizedSeen;
    bool _bossSeeded;
    float _gaugeDone = -1f;  // 最後に広げ直したときのゲージ（-1＝まだ）

    bool _restoring, _restored;
    float _restoreNow;       // 戻し始めた時刻（色はこの時刻で決める）
    float _cut, _cutRate, _cutEnd, _band;
    float _levelAtRestore;

    float _nextScan, _nextGrow, _nextTitle;
    float _nextGlitch = -1f;
    float _glitchEnd;
    Win _glitchWin;
    System.Random _rng;

    // ───────── 付ける（WindowStage.Built） ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        try { WindowStage.Built -= OnBuilt; } catch (Exception) { }
        Current = null;
        Enabled = true;
        ForceLevel = -1f;
        SpeedScale = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        try
        {
            WindowStage.Built -= OnBuilt;
            WindowStage.Built += OnBuilt;
        }
        catch (Exception e) { SkinDresser.LogOnce("WindowCorrosion.Hook", $"蝕む係を付けられません：{e}"); }
    }

    static void OnBuilt()
    {
        try
        {
            if (Current != null) Destroy(Current.gameObject); // 同じシーンで 2回作られたとき（ふつうは無い）
            Current = null;
            IReadOnlyList<StageWindow> list = WindowStage.Windows;
            if (list == null || list.Count == 0) return;
            var go = new GameObject("WindowCorrosion");
            // ウィンドウと同じシーンに置く（シーンといっしょに消える）：ウィンドウの親の下に入れる。親が無ければシーンへ移す
            for (int i = 0; i < list.Count; i++)
            {
                StageWindow w = list[i];
                if (w == null || w.Platform == null) continue;
                Transform parent = w.Platform.transform.parent;
                if (parent != null) go.transform.SetParent(parent, false);
                else
                {
                    Scene s = w.Platform.gameObject.scene;
                    try { if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s); }
                    catch (Exception) { }
                }
                break;
            }
            go.AddComponent<WindowCorrosion>();
        }
        catch (Exception e) { SkinDresser.LogOnce("WindowCorrosion.OnBuilt", $"蝕む係を作れません：{e}"); }
    }

    void Awake()
    {
        Current = this;
        try
        {
            _rng = new System.Random(Environment.TickCount);
            var seen = new HashSet<Texture2D>();
            IReadOnlyList<StageWindow> list = WindowStage.Windows;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    Win w = Prepare(list[i], seen);
                    if (w != null) _wins.Add(w);
                }
            _level = 0.10f;
        }
        catch (Exception e) { Fail(e); }
    }

    void Start()
    {
        if (_failed) return;
        try
        {
            FindRefs();
            if (ForceLevel >= 0f) _level = Mathf.Clamp01(ForceLevel);
            _started = true;
        }
        catch (Exception e) { Fail(e); }
    }

    void OnDestroy()
    {
        Unhook();
        if (Current == this) Current = null;
        if (!_idleClean)
        {
            try { RestoreAll(); } catch (Exception) { }
        }
    }

    // ───────── ウィンドウ 1枚の下ごしらえ ─────────

    /// <summary>書いてよいウィンドウなら、にじみ・まだらを作る。だめなら null</summary>
    static Win Prepare(StageWindow src, HashSet<Texture2D> seen)
    {
        if (src == null || src.Texture == null || src.Clean == null) return null;
        Texture2D tex = src.Texture;
        int W = src.W, H = src.H;
        if (W <= 0 || H <= 0 || tex.width != W || tex.height != H || src.Clean.Length != W * H) return null;
        if (!tex.isReadable) return null;
        // ほかの絵と共有していないこと：Art に出ている絵のテクスチャが、このウィンドウだけの物
        if (src.Art == null || src.Art.sprite == null || src.Art.sprite.texture != tex) return null;
        if (!seen.Add(tex)) return null;

        int n = W * H;
        var w = new Win
        {
            Src = src,
            Tex = tex,
            W = W,
            H = H,
            Arrive = new float[n],
            HoleAt = new float[n],
            KindAt = new byte[n],
            Wobble = new float[n],
            Rand = new byte[n],
            HoleMask = new bool[n],
            Out = new Color32[n],
        };
        int titleRows = src.TitleH > 0 ? src.TitleH : MacUi.HeaderH(H);
        w.Protect = Mathf.Clamp(Mathf.Max(titleRows, MinProtectRows), 0, H);
        int seed = Seed0(src.Id, W, H);
        var rng = new System.Random(seed);
        w.Rng = new System.Random(seed ^ 0x5bd1e995);

        // にじみ：6ドットの大きさのなめらかな乱数 8割 ＋ ドットごとの乱数 2割 → 0.65〜1.5
        float[] smooth = ValueNoise(W, H, 6, rng);
        for (int i = 0; i < n; i++)
        {
            w.Arrive[i] = float.MaxValue;
            w.HoleAt[i] = float.MaxValue;
            float a = Mathf.Clamp01((smooth[i] - 0.5f) * 1.8f + 0.5f);
            float m = Mathf.Clamp01(0.8f * a + 0.2f * (float)rng.NextDouble());
            w.Wobble[i] = Mathf.Lerp(WobbleMin, WobbleMax, m);
            w.Rand[i] = (byte)rng.Next(256);
            if (src.Clean[i].a != 0) w.Opaque++;
        }

        // 題名（太字の子 Bold も）
        if (src.Title != null)
        {
            w.Texts = src.Title.GetComponentsInChildren<TextMesh>(true);
            w.TextColors = new Color[w.Texts.Length];
            w.TextOriginal = new string[w.Texts.Length];
            for (int k = 0; k < w.Texts.Length; k++)
            {
                w.TextColors[k] = w.Texts[k].color;
                w.TextOriginal[k] = w.Texts[k].text;
            }
            w.TitleBase = !string.IsNullOrEmpty(src.TitleText) ? src.TitleText : src.Title.text;
            Color ink = src.TitleColor.a > 0f ? src.TitleColor : w.TextColors.Length > 0 ? w.TextColors[0] : Color.black;
            bool lightInk = ink.grayscale > 0.5f; // 白い字＝暗い見た目（Discord）はマゼンタで
            w.TitleBroken = lightInk ? TitleMagenta : TitlePurple;
        }
        return w;
    }

    // ゆるい形の乱数（cell ドットごとの格子の値を、なめらかにつなぐ。0〜1）
    static float[] ValueNoise(int W, int H, int cell, System.Random rng)
    {
        int gw = W / cell + 2, gh = H / cell + 2;
        var grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)rng.NextDouble();
        var o = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float gx = x / (float)cell, gy = y / (float)cell;
                int ix = (int)gx, iy = (int)gy;
                float fx = gx - ix, fy = gy - iy;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                float a = Mathf.Lerp(grid[iy * gw + ix], grid[iy * gw + ix + 1], fx);
                float b = Mathf.Lerp(grid[(iy + 1) * gw + ix], grid[(iy + 1) * gw + ix + 1], fx);
                o[y * W + x] = Mathf.Lerp(a, b, fy);
            }
        return o;
    }

    static int Seed0(string id, int w, int h)
    {
        unchecked
        {
            int s = 17;
            if (id != null)
                for (int i = 0; i < id.Length; i++) s = s * 31 + id[i];
            return s * 486187739 + w * 7919 + h * 104729;
        }
    }

    // ───────── 毎フレーム ─────────

    void Update()
    {
        if (_failed || !_started) return;
        try { Tick(); }
        catch (Exception e) { Fail(e); }
    }

    void Tick()
    {
        if (!Enabled)
        {
            if (!_idleClean) RestoreAll();
            return;
        }
        float now = Time.unscaledTime;
        float t = Time.time;

        if (now >= _nextRefTry && (_director == null || _boss == null || _wave == null || _entrance == null))
            FindRefs();

        bool normalized = IsNormalized();
        if (normalized && !_restoring && !_restored) BeginRestore();

        if (_restoring)
        {
            _cut -= _cutRate * Time.unscaledDeltaTime; // 重いコマ・止まった画面でも 1.2秒をのばさない
            float k = Mathf.Clamp01((_cut - _cutEnd) / Mathf.Max(0.0001f, _cutRate * RestoreSeconds));
            _level = _levelAtRestore * k;
            MarkAll(false);
            if (_cut <= _cutEnd) FinishRestore();
        }
        else if (!_restored)
        {
            bool boss = InBossFight();
            if (t >= _nextScan)
            {
                _nextScan = t + ScanEvery;
                ScanSources(t, boss);
            }
            if (boss) BossGrowth(t);
            if (t >= _nextGrow)
            {
                _nextGrow = t + GrowEvery;
                GrowAll(t + GrowEvery, boss ? CapBoss : CapZako);
            }
            // 強さ（乱れ・題名。紫の量には使わない）
            if (ForceLevel >= 0f) _level = Mathf.Clamp01(ForceLevel);
            else
            {
                float target = Mathf.Clamp01(Target(boss));
                _level = _level < target ? Mathf.Min(target, _level + Rise * Time.deltaTime) : Mathf.Max(target, _level - Fall * Time.deltaTime);
            }
        }

        UpdateGlitch(now, normalized);
        UpdateTitles(normalized);

        // ── 書きこみ ──
        float interval = _restoring ? RestoreUploadInterval : UploadInterval;
        float cover = 0f;
        int opaque = 0;
        for (int i = 0; i < _wins.Count; i++)
        {
            Win w = _wins[i];
            if (w.Tex == null) continue;
            bool settling = t - w.LastArrive <= SettleAge;
            if ((w.Dirty || w.Force || settling) && (w.Force || now >= w.NextUpload))
            {
                Compose(w, _restoring ? _restoreNow : t);
                w.Tex.SetPixels32(w.Out);
                w.Tex.Apply(false);
                w.Dirty = false;
                w.Force = false;
                w.NextUpload = now + interval;
            }
            cover += w.Coverage * w.Opaque;
            opaque += w.Opaque;
        }
        _coverage = opaque > 0 ? cover / opaque : 0f;
        _idleClean = AllClean() && !AnyGlitch() && !AnyBroken();
    }

    // ───────── 強さの目標（乱れ・題名。1-8 の表のまま） ─────────

    float Target(bool boss)
    {
        if (boss) return 0.50f + 0.45f * GaugeRatio();
        float p = _wave != null ? Mathf.Clamp01(_wave.Progress) : 0f;
        float t = 0.10f + 0.40f * p;
        StagePacer pacer = StagePacer.Current;
        if (pacer != null) t = Mathf.Max(t, PhaseFloor(pacer.Phase));
        return t;
    }

    static float PhaseFloor(PacePhase phase)
    {
        switch (phase)
        {
            case PacePhase.Intro: return 0.10f;
            case PacePhase.Build: return 0.22f;
            case PacePhase.Event: return 0.34f;
            case PacePhase.Rest: return 0.40f;
            default: return 0.50f; // Climax・Done
        }
    }

    float GaugeRatio()
    {
        if (_boss == null) return 0f;
        if (_boss.State == BossState.Ejected) return 1f;
        return _boss.Gauge != null && _boss.Gauge.Max > 0 ? Mathf.Clamp01(_boss.Gauge.Ratio) : 0f;
    }

    bool InBossFight()
    {
        if (_entrance != null) return _entrance.IsTriggered;
        // 入口が無いシーン：ボスのゲージが動き始めたらボス戦
        return _boss != null && _boss.Gauge != null && _boss.Gauge.Current > 0;
    }

    bool IsNormalized() => _normalizedSeen || (_boss != null && _boss.State == BossState.Normalized);

    void FindRefs()
    {
        _nextRefTry = Time.unscaledTime + RefRetry;
        if (_director == null) _director = FindObjectOfType<StageDirector>();
        if (_director != null)
        {
            if (_boss == null) _boss = _director.Boss;
            if (_wave == null) _wave = _director.Wave;
            if (_entrance == null) _entrance = _director.Entrance;
        }
        if (_wave == null) _wave = FindObjectOfType<EnemyWave>();
        if (_entrance == null) _entrance = FindObjectOfType<BossEntrance>();
        if (_boss == null && _entrance != null) _boss = _entrance.Boss;
        if (_boss != null && _hooked != _boss)
        {
            Unhook();
            _boss.Normalized += OnBossNormalized;
            _hooked = _boss;
            _bossCol = null;
            foreach (Collider2D c in _boss.GetComponentsInChildren<Collider2D>())
                if (c != null && !c.isTrigger) { _bossCol = c; break; }
            if (_bossCol == null) _bossCol = _boss.GetComponentInChildren<Collider2D>();
        }
    }

    void Unhook()
    {
        if (_hooked != null) _hooked.Normalized -= OnBossNormalized;
        _hooked = null;
    }

    void OnBossNormalized(SkillDefinition reward) => _normalizedSeen = true;

    // ───────── 汚れの元（7-3 の表） ─────────

    void ScanSources(float t, bool boss)
    {
        // 雑魚（出た物＋シーンに置いてある最初の雑魚）
        _scanBuf.Clear();
        StagePacer pacer = StagePacer.Current;
        ZakoSpawner spawner = pacer != null ? pacer.Spawner : null;
        if (spawner != null) _scanBuf.AddRange(spawner.AliveList);
        if (_wave != null && !_wave.IsCleared)
        {
            _wave.GetComponentsInChildren(false, _waveBuf);
            for (int i = 0; i < _waveBuf.Count; i++)
                if (!_scanBuf.Contains(_waveBuf[i])) _scanBuf.Add(_waveBuf[i]);
            _waveBuf.Clear();
        }
        for (int i = 0; i < _scanBuf.Count; i++)
        {
            Health h = _scanBuf[i];
            if (h == null || h.IsDead || !h.isActiveAndEnabled) continue;
            if (h.GetComponentInParent<BossController>() != null) continue;
            ScanZako(h.gameObject, t);
        }
        _scanBuf.Clear();

        // ワームの巣（下の端の点。1つの巣に 1つ。巣がある間だけ広がる）
        IReadOnlyList<VirusNest> nests = VirusNest.All;
        if (nests != null)
            for (int i = 0; i < nests.Count; i++)
            {
                VirusNest nest = nests[i];
                if (nest == null || !nest.IsLive) continue;
                SourceState st = StateOf(nest.GetInstanceID(), t);
                if (st.Last != null) continue;
                Vector2 at = nest.transform.position;
                Win w = BottomAt(at, 0.25f);
                if (w == null) continue;
                Seed s = Place(w, Source.Nest, new Vector2(ToPx(w, at).x, 0f), nest.GetInstanceID(), true, t, false);
                if (s != null) { st.Last = s; st.Win = w; }
            }

        // ボス戦の始まり：種が 0 のウィンドウに、ボスにいちばん近いふちの点
        if (boss && !_bossSeeded && _boss != null)
        {
            _bossSeeded = true;
            Vector2 c = _bossCol != null ? (Vector2)_bossCol.bounds.center : (Vector2)_boss.transform.position;
            for (int i = 0; i < _wins.Count; i++)
            {
                Win w = _wins[i];
                if (w.Seeds.Count > 0) continue;
                Place(w, Source.Boss, ToPx(w, NearestEdgePoint(w.Src.World, c)), _boss.GetInstanceID(), false, t, false);
            }
        }

        // 見なくなった元：巣の種を止める（汚れは残る）
        _dropBuf.Clear();
        foreach (KeyValuePair<int, SourceState> kv in _sources)
            if (t - kv.Value.SeenAt > SourceGrace) _dropBuf.Add(kv.Key);
        for (int i = 0; i < _dropBuf.Count; i++)
        {
            int id = _dropBuf[i];
            for (int k = 0; k < _wins.Count; k++)
            {
                List<Seed> seeds = _wins[k].Seeds;
                for (int s = 0; s < seeds.Count; s++)
                    if (seeds[s].Bound && seeds[s].SourceId == id) seeds[s].Growing = false;
            }
            _sources.Remove(id);
        }
        _dropBuf.Clear();
    }

    void ScanZako(GameObject go, float t)
    {
        int id = go.GetInstanceID();
        SourceState st = StateOf(id, t);
        Vector2 feet = go.transform.position;
        string type = VirusArt.TypeOf(go);

        // ワーム：這っているふち（その足場のウィンドウ）。かじり跡も
        ZakoWorm worm = type == "Worm" ? go.GetComponent<ZakoWorm>() : null;
        if (worm != null && worm.RailPlatform != null)
        {
            Win w = WinOf(WindowStage.Of(worm.RailPlatform));
            if (w == null) return;
            AddBite(w, feet, worm.SurfaceNormal, t);
            Vector2 px = ToPx(w, feet);
            if (st.Last != null && st.Win == w && st.Last.Kind == Source.Worm && Dist2(px, st.Last) < Sq(Specs[(int)Source.Worm].Spacing)) return;
            Seed s = Place(w, Source.Worm, px, id, false, t, false);
            if (s != null) { st.Last = s; st.Win = w; }
            return;
        }

        // ルートキット：出たとき（Rising になった瞬間）。上の端から出たらその点、床から出たら真上のウィンドウの見えている下の端
        ZakoRootkit rk = type == "Rootkit" ? go.GetComponent<ZakoRootkit>() : null;
        if (rk != null && rk.State != ZakoRootkit.Phase.Walking)
        {
            bool rising = rk.State == ZakoRootkit.Phase.Rising;
            if (rising && !st.WasRising)
            {
                Win w = TopAt(feet);
                Vector2 px = default;
                if (w != null) px = new Vector2(ToPx(w, feet).x, w.H - 1);
                else
                {
                    w = BottomAbove(feet);
                    if (w != null) px = new Vector2(ToPx(w, feet).x, 0f);
                }
                if (w != null) Place(w, Source.Rootkit, px, id, false, t, false);
            }
            st.WasRising = rising;
            return;
        }

        // とぶ雑魚：ウィンドウの前に 1秒いたら、その点（いちばん手前のウィンドウ）
        if (go.GetComponent<ZakoFlyer>() != null)
        {
            Transform body = VirusArt.BodyOf(go);
            Vector2 c = body != null ? (Vector2)body.position : feet;
            Win w = WinOf(WindowStage.FrontAt(c, 0f));
            if (w != st.Win || w == null)
            {
                st.Win = w;
                st.InsideSince = t;
                return;
            }
            if (t - st.InsideSince < Specs[(int)Source.Flyer].Interval) return;
            st.InsideSince = t;
            Place(w, Source.Flyer, ToPx(w, c), id, false, t, false);
            return;
        }

        // 上に立っている雑魚（マイナーは下へ細長く）：立っている間、決まった秒ごとに足元
        KinematicBody2D kb = go.GetComponent<KinematicBody2D>();
        Win on = kb != null && !kb.IsGrounded ? null : TopAt(feet);
        if (on == null)
        {
            st.Win = null;
            return;
        }
        Source kind = type == "Miner" ? Source.Miner : Source.Standing;
        SourceSpec spec = Specs[(int)kind];
        if (on != st.Win)
        {
            st.Win = on;
            st.NextAt = t + spec.Interval; // 立って 1.0〜1.5秒たってから
            return;
        }
        if (t < st.NextAt) return;
        st.NextAt = t + spec.Interval;
        Vector2 at = new Vector2(ToPx(on, feet).x, on.H - 1);
        if (spec.Spacing > 0f)
            for (int k = 0; k < on.Seeds.Count; k++)
            {
                Seed o = on.Seeds[k];
                if (o.Kind == kind && Dist2(at, o) < Sq(spec.Spacing)) return; // ほかの弱い種から 12ドット以上あける
            }
        Place(on, kind, at, id, false, t, false);
    }

    SourceState StateOf(int id, float t)
    {
        if (!_sources.TryGetValue(id, out SourceState st))
        {
            st = new SourceState();
            _sources[id] = st;
        }
        st.SeenAt = t;
        return st;
    }

    /// <summary>種を置く（上限を守る）。置けなければ null</summary>
    Seed Place(Win w, Source kind, Vector2 px, int sourceId, bool bound, float t, bool ignoreCover)
    {
        if (w == null || _restoring || _restored) return null;
        SourceSpec spec = Specs[(int)kind];
        if (w.Seeds.Count >= SeedsPerWindow || w.KindCount[(int)kind] >= spec.Cap) return null;
        if (!ignoreCover && w.Coverage >= (InBossFight() ? CapBoss : CapZako)) return null;
        float factor = InBossFight() && _gaugeDone >= 0f ? 1f + BossGrow * _gaugeDone : 1f;
        var s = new Seed
        {
            SourceId = sourceId,
            Kind = kind,
            Bound = bound,
            X = Mathf.Clamp(px.x, 0f, w.W - 1),
            Y = Mathf.Clamp(px.y, 0f, w.H - 1),
            Speed = spec.Speed,
            Reach = spec.Reach,
            ReachNow = spec.Reach * factor,
            YScale = spec.YScale,
            Holes = spec.Holes,
            GrowStart = t,
            GrowFrom = 0f,
        };
        w.Seeds.Add(s);
        w.KindCount[(int)kind]++;
        _placed[(int)kind]++;
        return s;
    }

    // ボス戦：ゲージが 0.03 変わるごとに、全部の種の半径を 最大 ×（1 ＋ 0.6 × ゲージ）まで広げ直す（届く時刻は「今」から）
    void BossGrowth(float t)
    {
        float g = GaugeRatio();
        if (_gaugeDone >= 0f && Mathf.Abs(g - _gaugeDone) < GaugeStep) return;
        _gaugeDone = g;
        float factor = 1f + BossGrow * g;
        for (int i = 0; i < _wins.Count; i++)
        {
            List<Seed> seeds = _wins[i].Seeds;
            for (int k = 0; k < seeds.Count; k++)
            {
                Seed s = seeds[k];
                float r = s.Reach * factor;
                if (r <= s.ReachNow + 0.1f) continue;
                s.ReachNow = r;
                if (!s.Growing)
                {
                    s.Growing = true;
                    s.GrowFrom = s.Done;
                    s.GrowStart = t;
                }
            }
        }
    }

    // 種を広げる：horizon（少し先の時刻）までに届くドットに、届く時刻を書く。上限の割合をこえたウィンドウは止めて待つ
    void GrowAll(float horizon, float cap)
    {
        float scale = Mathf.Max(0.01f, SpeedScale);
        for (int k = 0; k < _wins.Count; k++)
        {
            Win w = _wins[k];
            bool paused = w.Coverage >= cap;
            List<Seed> seeds = w.Seeds;
            for (int i = 0; i < seeds.Count; i++)
            {
                Seed s = seeds[i];
                if (!s.Growing) continue;
                if (paused)
                {
                    s.GrowFrom = s.Done; // 止めている間の分を、後でいっぺんに広げない
                    s.GrowStart = horizon;
                    continue;
                }
                Grow(w, s, horizon, scale);
            }
        }
    }

    static void Grow(Win w, Seed s, float horizon, float scale)
    {
        float speed = s.Speed * scale;
        float r = Mathf.Min(s.ReachNow, s.GrowFrom + speed * Mathf.Max(0f, horizon - s.GrowStart));
        if (r < s.ReachNow && r <= s.Done + 0.3f) return;
        float reach = r / WobbleMin;
        float reachY = reach / Mathf.Max(0.1f, s.YScale);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(s.X - reach)), x1 = Mathf.Min(w.W - 1, Mathf.CeilToInt(s.X + reach));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(s.Y - reachY)), y1 = Mathf.Min(w.H - 1, Mathf.CeilToInt(s.Y + reachY));
        float reach2 = reach * reach;
        float holeR = s.Holes ? s.ReachNow * HoleShare : -1f;
        Color32[] clean = w.Src.Clean;
        float latest = w.LastArrive;
        bool any = false;
        byte kind = (byte)s.Kind;
        for (int y = y0; y <= y1; y++)
        {
            float dy = (y - s.Y) * s.YScale;
            int row = y * w.W;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - s.X;
                float d2 = dx * dx + dy * dy;
                if (d2 > reach2) continue;
                int i = row + x;
                if (clean[i].a == 0) continue;
                float d = Mathf.Sqrt(d2) * w.Wobble[i]; // にじみ：n が小さい筋にそって、根のように伸びる
                if (d > r) continue;
                float at = s.GrowStart + Mathf.Max(0f, d - s.GrowFrom) / speed;
                if (at < w.Arrive[i])
                {
                    w.Arrive[i] = at;
                    w.KindAt[i] = kind;
                    if (at > latest) latest = at;
                    any = true;
                }
                if (d <= holeR)
                {
                    float h = at + HoleDelay;
                    if (h < w.HoleAt[i]) w.HoleAt[i] = h;
                }
            }
        }
        s.Done = r;
        if (r >= s.ReachNow) s.Growing = false;
        if (any)
        {
            w.LastArrive = latest;
            w.Clean = false;
        }
    }

    // ───────── どのウィンドウか ─────────

    Win WinOf(StageWindow sw)
    {
        if (sw == null) return null;
        for (int i = 0; i < _wins.Count; i++) if (_wins[i].Src == sw) return _wins[i];
        return null;
    }

    /// <summary>足元が上の端にのっているウィンドウ（手前から。上の端は重なりの決まりでかくれない）</summary>
    Win TopAt(Vector2 feet)
    {
        for (int i = _wins.Count - 1; i >= 0; i--)
        {
            Rect r = _wins[i].Src.World;
            if (Mathf.Abs(feet.y - r.yMax) <= StandTolerance && feet.x >= r.xMin && feet.x <= r.xMax) return _wins[i];
        }
        return null;
    }

    /// <summary>その点が下の端にあるウィンドウ（手前から）</summary>
    Win BottomAt(Vector2 p, float tol)
    {
        for (int i = _wins.Count - 1; i >= 0; i--)
        {
            Rect r = _wins[i].Src.World;
            if (Mathf.Abs(p.y - r.yMin) <= tol && p.x >= r.xMin && p.x <= r.xMax) return _wins[i];
        }
        return null;
    }

    /// <summary>床から出たルートキット：真上 2.5マス以内で、下の端が見えている いちばん低いウィンドウ</summary>
    Win BottomAbove(Vector2 feet)
    {
        Win best = null;
        float bestY = float.MaxValue;
        for (int i = 0; i < _wins.Count; i++)
        {
            Rect r = _wins[i].Src.World;
            float up = r.yMin - feet.y;
            if (up <= 0f || up > RootkitReachUp || feet.x < r.xMin || feet.x > r.xMax) continue;
            if (WindowStage.Hidden(_wins[i].Src, new Vector2(feet.x, r.yMin), -CoverGrow)) continue;
            if (r.yMin < bestY)
            {
                bestY = r.yMin;
                best = _wins[i];
            }
        }
        return best;
    }

    static Vector2 ToPx(Win w, Vector2 p)
    {
        Rect r = w.Src.World;
        if (r.width <= 0f || r.height <= 0f) return Vector2.zero;
        return new Vector2(Mathf.Clamp((p.x - r.xMin) / r.width * w.W, 0f, w.W - 1),
                           Mathf.Clamp((p.y - r.yMin) / r.height * w.H, 0f, w.H - 1));
    }

    static Vector2 NearestEdgePoint(Rect r, Vector2 p)
    {
        Vector2 q = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
        float l = q.x - r.xMin, ri = r.xMax - q.x, b = q.y - r.yMin, tp = r.yMax - q.y;
        float m = Mathf.Min(Mathf.Min(l, ri), Mathf.Min(b, tp));
        if (m == l) q.x = r.xMin;
        else if (m == ri) q.x = r.xMax;
        else if (m == b) q.y = r.yMin;
        else q.y = r.yMax;
        return q;
    }

    static float Dist2(Vector2 px, Seed s)
    {
        float dx = px.x - s.X, dy = px.y - s.Y;
        return dx * dx + dy * dy;
    }

    static float Sq(float v) => v * v;

    // ───────── かじり跡（ワームが通ったふち） ─────────

    void AddBite(Win w, Vector2 p, Vector2 n, float t)
    {
        int edge; // 0 下・1 右・2 上・3 左（足場のレールでは、乗っている辺の外向き）
        if (n.y > 0.5f) edge = 2;
        else if (n.y < -0.5f) edge = 0;
        else if (n.x > 0.5f) edge = 1;
        else if (n.x < -0.5f) edge = 3;
        else return;
        Rect r = w.Src.World;
        float dist;
        switch (edge)
        {
            case 2: dist = Mathf.Abs(p.y - r.yMax); break;
            case 0: dist = Mathf.Abs(p.y - r.yMin); break;
            case 1: dist = Mathf.Abs(p.x - r.xMax); break;
            default: dist = Mathf.Abs(p.x - r.xMin); break;
        }
        if (dist > BiteReach || w.Bites.Count >= BiteMax) return;
        float sx = w.W / r.width, sy = w.H / r.height;
        int px = Mathf.Clamp(Mathf.RoundToInt((p.x - r.xMin) * sx), 0, w.W - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt((p.y - r.yMin) * sy), 0, w.H - 1);
        switch (edge)
        {
            case 2: py = w.H - 1; break;
            case 0: py = 0; break;
            case 1: px = w.W - 1; break;
            default: px = 0; break;
        }
        for (int k = 0; k < w.Bites.Count; k++)
        {
            int dx = w.Bites[k].X - px, dy = w.Bites[k].Y - py;
            if (dx * dx + dy * dy < BiteSpacing * BiteSpacing) return; // 止まっているワームで上限を使い切らない
        }
        w.Bites.Add(new Bite { X = px, Y = py, R = 2 + w.Rng.Next(2), Top = edge == 2, Time = t });
        w.Dirty = true;
        w.Clean = false;
    }

    // ───────── 元に戻る（正常化） ─────────

    void BeginRestore()
    {
        _restoring = true;
        _restoreNow = Time.time;
        _levelAtRestore = _level;
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < _wins.Count; i++)
        {
            Win w = _wins[i];
            w.Seeds.Clear();
            Array.Clear(w.KindCount, 0, w.KindCount.Length);
            float[] a = w.Arrive;
            for (int k = 0; k < a.Length; k++)
            {
                float v = a[k];
                if (v == float.MaxValue) continue;
                if (v > _restoreNow) { a[k] = float.MaxValue; continue; } // まだ届いていない先回りの分は消す
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            for (int k = 0; k < w.Bites.Count; k++)
            {
                float v = w.Bites[k].Time;
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            if (w.Glitching) EndGlitch(w);
        }
        _sources.Clear();
        if (lo > hi) { lo = hi = _restoreNow; }
        float span = Mathf.Max(0.05f, hi - lo);
        _band = span * FlashShare;
        _cut = hi + 0.0001f;
        _cutEnd = lo - _band - 0.0001f;
        _cutRate = (_cut - _cutEnd) / RestoreSeconds;
        MarkAll(true);
    }

    void FinishRestore()
    {
        _restoring = false;
        _restored = true;
        _level = 0f;
        for (int i = 0; i < _wins.Count; i++)
        {
            Win w = _wins[i];
            ClearField(w);
            w.Force = true;
        }
    }

    static void ClearField(Win w)
    {
        for (int k = 0; k < w.Arrive.Length; k++)
        {
            w.Arrive[k] = float.MaxValue;
            w.HoleAt[k] = float.MaxValue;
        }
        w.Bites.Clear();
        w.Seeds.Clear();
        Array.Clear(w.KindCount, 0, w.KindCount.Length);
        w.LastArrive = float.MinValue;
        w.Clean = true;
        w.Coverage = 0f;
    }

    // ───────── 乱れ ─────────

    void UpdateGlitch(float now, bool normalized)
    {
        if (_glitchWin != null && now >= _glitchEnd) EndGlitch(_glitchWin);
        bool allowed = !normalized && _level >= GlitchFrom && _wins.Count > 0;
        if (!allowed)
        {
            _nextGlitch = -1f;
            return;
        }
        if (_nextGlitch < 0f) _nextGlitch = Time.time + Gap();
        if (_glitchWin != null || Time.time < _nextGlitch) return;
        _nextGlitch = Time.time + Gap();
        Win w = _wins[_rng.Next(_wins.Count)];
        if (w.Tex == null || w.H < 2) return;
        w.GlitchH = Mathf.Min(w.H, 2 + _rng.Next(3));
        w.GlitchY = _rng.Next(w.H - w.GlitchH + 1);
        w.GlitchShift = (3 + _rng.Next(4)) * (_rng.Next(2) == 0 ? -1 : 1);
        w.Glitching = true;
        w.Force = true;
        _glitchWin = w;
        _glitchEnd = now + GlitchSeconds;
    }

    float Gap() => GlitchMinGap + (float)_rng.NextDouble() * (GlitchMaxGap - GlitchMinGap);

    void EndGlitch(Win w)
    {
        if (w != null && w.Glitching)
        {
            w.Glitching = false;
            w.Force = true;
        }
        if (_glitchWin == w) _glitchWin = null;
    }

    bool AnyGlitch()
    {
        for (int i = 0; i < _wins.Count; i++) if (_wins[i].Glitching) return true;
        return false;
    }

    bool AllClean()
    {
        for (int i = 0; i < _wins.Count; i++) if (!_wins[i].Clean || _wins[i].Bites.Count > 0) return false;
        return true;
    }

    // ───────── 題名の字がくずれる ─────────

    void UpdateTitles(bool normalized)
    {
        bool want = _level >= TitleFrom;
        if (!want)
        {
            for (int i = 0; i < _wins.Count; i++) if (_wins[i].Broken) RestoreTitle(_wins[i]);
            return;
        }
        if (normalized) return; // 戻る途中は増やさない（0.60 を切ったら元の字に）
        if (Time.time < _nextTitle) return;
        _nextTitle = Time.time + TitleEvery;
        for (int i = 0; i < _wins.Count; i++) BreakTitle(_wins[i]);
    }

    void BreakTitle(Win w)
    {
        if (w.Texts == null || w.Texts.Length == 0 || string.IsNullOrEmpty(w.TitleBase)) return;
        char[] cs = w.TitleBase.ToCharArray();
        int slots = 0;
        for (int i = 0; i < cs.Length; i++) if (Breakable(cs[i])) slots++;
        if (slots == 0) return;
        int count = Mathf.Min(slots, 1 + _rng.Next(3));
        for (int c = 0; c < count; c++)
        {
            int pick = _rng.Next(slots), seen = 0;
            for (int i = 0; i < cs.Length; i++)
            {
                if (!Breakable(cs[i])) continue;
                if (seen++ != pick) continue;
                cs[i] = Glyphs[_rng.Next(Glyphs.Length)];
                slots--;
                break;
            }
            if (slots <= 0) break;
        }
        string s = new string(cs);
        for (int k = 0; k < w.Texts.Length; k++)
            if (w.Texts[k] != null) w.Texts[k].text = s;
        if (!w.Broken) MacUi.SetLabelColor(w.Src.Title, w.TitleBroken);
        w.Broken = true;
    }

    static bool Breakable(char c) => !char.IsWhiteSpace(c) && c != '—' && c != '-' && Glyphs.IndexOf(c) < 0;

    static void RestoreTitle(Win w)
    {
        w.Broken = false;
        if (w.Texts == null) return;
        for (int k = 0; k < w.Texts.Length; k++)
        {
            TextMesh t = w.Texts[k];
            if (t == null) continue;
            t.text = w.TextOriginal[k];
            t.color = w.TextColors[k]; // 元の値をそのまま（SetColor の色の直しを 2回かけない）
        }
    }

    bool AnyBroken()
    {
        for (int i = 0; i < _wins.Count; i++) if (_wins[i].Broken) return true;
        return false;
    }

    // ───────── 絵を作る（いつも Clean から） ─────────

    void Compose(Win w, float now)
    {
        Color32[] clean = w.Src.Clean;
        Color32[] o = w.Out;
        int W = w.W, H = w.H, n = W * H;
        bool restoring = _restoring;
        if (w.Clean && w.Bites.Count == 0 && !w.Glitching)
        {
            Array.Copy(clean, o, n);
            w.Coverage = 0f;
            return;
        }

        int protectFrom = H - w.Protect; // この行から上はタイトルバー（透明にしない）
        float[] arrive = w.Arrive;
        bool[] holes = w.HoleMask;
        bool anyHole = false;
        int infected = 0;
        for (int y = 0; y < H; y++)
        {
            int row = y * W;
            bool canHole = y < protectFrom;
            for (int x = 0; x < W; x++)
            {
                int i = row + x;
                holes[i] = false;
                Color32 c = clean[i];
                if (c.a == 0) { o[i] = c; continue; } // 角の外は塗らない
                float a = arrive[i];
                if (a > now) { o[i] = c; continue; }  // まだ届いていない
                if (restoring)
                {
                    if (a > _cut + _band) { o[i] = c; continue; }        // 戻った
                    if (a > _cut) { o[i] = Paint(Flash, c.a); continue; } // 戻った境目
                }
                infected++;
                if (canHole && w.HoleAt[i] <= now)
                {
                    o[i] = Clear;
                    holes[i] = true;
                    anyHole = true;
                    continue;
                }
                float age = now - a;
                if (age < FrontAge) { o[i] = Paint(Magenta, c.a); continue; } // 広がっている先
                int share = w.KindAt[i] == (byte)Source.Rootkit ? RootkitDarkShare : DarkShare;
                bool dark = age >= DarkAge && w.Rand[i] < share;
                o[i] = Blend(c, dark ? DarkPurple : Purple, dark ? StainDark : StainPurple);
            }
        }
        w.Coverage = w.Opaque > 0 ? infected / (float)w.Opaque : 0f;

        // 穴のふち 1ドットは濃い紫
        if (anyHole)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (holes[i] || clean[i].a == 0 || o[i].a == 0) continue;
                    bool rim = (x > 0 && holes[i - 1]) || (x < W - 1 && holes[i + 1]) || (y > 0 && holes[i - W]) || (y < H - 1 && holes[i + W]);
                    if (rim) o[i] = Paint(DarkPurple, clean[i].a);
                }

        // かじり跡（ふちを先に、中を後に）
        if (w.Bites.Count > 0)
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < w.Bites.Count; k++)
                {
                    Bite b = w.Bites[k];
                    if (restoring && b.Time > _cut) continue;
                    int r = b.R, ext = r + 1;
                    int r2 = r * r, rim2 = (r + 1) * (r + 1);
                    int y0 = Mathf.Max(0, b.Y - ext), y1 = Mathf.Min(H - 1, b.Y + ext);
                    int x0 = Mathf.Max(0, b.X - ext), x1 = Mathf.Min(W - 1, b.X + ext);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int i = y * W + x;
                            if (clean[i].a == 0) continue;
                            int dx = x - b.X, dy = y - b.Y;
                            int d2 = dx * dx + dy * dy;
                            bool solid = b.Top || y >= protectFrom; // 乗る面は透明にしない
                            if (pass == 0)
                            {
                                if (d2 > r2 && d2 <= rim2) o[i] = Paint(solid ? DarkPurple : Purple, clean[i].a);
                            }
                            else if (d2 <= r2) o[i] = solid ? Paint(Purple, clean[i].a) : Clear;
                        }
                }

        // 乱れ：帯を横へずらす。あいた所・透明になった所はマゼンタ（穴にはしない）
        if (w.Glitching)
        {
            int s = w.GlitchShift;
            int yEnd = Mathf.Min(H, w.GlitchY + w.GlitchH);
            Color32[] row = RowBuffer(W);
            for (int y = Mathf.Max(0, w.GlitchY); y < yEnd; y++)
            {
                int start = y * W;
                Array.Copy(o, start, row, 0, W);
                for (int x = 0; x < W; x++)
                {
                    int i = start + x;
                    if (clean[i].a == 0) { o[i] = clean[i]; continue; }
                    int from = x - s;
                    Color32 c = from >= 0 && from < W ? row[from] : Clear;
                    o[i] = c.a == 0 ? Paint(Magenta, clean[i].a) : c;
                }
            }
        }
    }

    static Color32[] _row;
    static Color32[] RowBuffer(int w)
    {
        if (_row == null || _row.Length < w) _row = new Color32[Mathf.Max(w, 128)];
        return _row;
    }

    static Color32 Paint(Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);

    static Color32 Blend(Color32 c, Color32 ink, float k)
    {
        return new Color32(
            (byte)(c.r + (ink.r - c.r) * k),
            (byte)(c.g + (ink.g - c.g) * k),
            (byte)(c.b + (ink.b - c.b) * k),
            c.a);
    }

    // ───────── 元に戻す・止める ─────────

    void MarkAll(bool force)
    {
        for (int i = 0; i < _wins.Count; i++)
        {
            _wins[i].Dirty = true;
            if (force) _wins[i].Force = true;
        }
    }

    /// <summary>全部のウィンドウを元の絵・元の題名に（止めたとき・例外のとき）</summary>
    void RestoreAll()
    {
        for (int i = 0; i < _wins.Count; i++)
        {
            Win w = _wins[i];
            w.Glitching = false;
            ClearField(w);
            if (w.Broken) RestoreTitle(w);
            if (w.Tex != null && w.Src.Clean != null && w.Src.Clean.Length == w.W * w.H)
            {
                w.Tex.SetPixels32(w.Src.Clean);
                w.Tex.Apply(false);
            }
        }
        _sources.Clear();
        _glitchWin = null;
        _idleClean = true;
    }

    void Fail(Exception e)
    {
        if (_failed) return;
        _failed = true;
        SkinDresser.LogOnce("WindowCorrosion", $"蝕まれるウィンドウで例外（絵を元に戻して止める）：{e}");
        try { RestoreAll(); } catch (Exception) { }
        enabled = false;
    }
}
