using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ウィンドウのステージ（2026-11-20。docs/window-stage-and-pickup-design.md 1章・4章）。
/// ・いくつかのアプリの 1画面のステージで、Mac のデスクトップの壁紙の上に、そのアプリのウィンドウを 4枚 重ねて出す。ウィンドウの上の端が足場
/// ・ウィンドウ 1枚＝AppPlatform 1つ（AppStagePlatforms.SpawnWindow。子 "Art" の四角のまわりをワームが這い、下の面に巣が付く）
/// ・絵は実行時に描く（MacUi の部品）。1枚ごとに自分だけの Texture2D（蝕む係 WindowCorrosion が書きかえる。MacUi.WindowSprite は共有なので使わない）
/// ・AppStagePlatforms.Build（StageDirector.Awake の中）から Build を呼ぶ。失敗したら 0 を返し、今までのアプリの UI のステージに戻る
/// ・切るとき：Enabled = false。代用ボスを外す：AlsoUnlisted = false。壁紙を今のままに：UseMacWallpaper = false
/// 数値はすべて【仮】
/// </summary>
public static class WindowStage
{
    public static bool Enabled = true;
    /// <summary>ラベルが null のアプリ（代用ボス）もウィンドウにする</summary>
    public static bool AlsoUnlisted = true;
    public static bool UseMacWallpaper = true;
    /// <summary>【確認用】1画面のステージを全部ウィンドウに（ラスボスはのぞく）</summary>
    public static bool ForceAll;
    /// <summary>【確認用】型（0〜2）。-1 なら決まった選び方</summary>
    public static int ForceTemplate = -1;
    public static readonly string[] Labels = { "Slack", "Discord", "LINE", "Messages", "Mail", "Notes" };

    /// <summary>今のステージがウィンドウのステージか（ウィンドウがシーンといっしょに消えたら false）</summary>
    public static bool Active => _active && _root != null;
    /// <summary>ウィンドウ（奥から順）。無ければ空</summary>
    public static IReadOnlyList<StageWindow> Windows => Active ? (IReadOnlyList<StageWindow>)_windows : Empty;
    /// <summary>ウィンドウを作り終えた直後（StageDirector.Awake の中）。ステージごとに 1回</summary>
    public static event Action Built;

    const string DesktopScene = "Stage_Desktop";
    const string WallpaperPath = "AppBackgrounds/Bg_MacDesktop";
    const int Ppu = 18;              // 1マス 18ドット（MacUi.Px）
    const int BaseOrder = -8;        // ウィンドウ i（奥から 0〜3）：影 -8+3i・Art -7+3i・字 -6+3i
    const int FolderOrder = -9;      // 飾りのフォルダ（ウィンドウより後ろ）

    static readonly List<StageWindow> _windows = new List<StageWindow>();
    static readonly StageWindow[] Empty = new StageWindow[0];
    static bool _active;
    static GameObject _root;
    static StagePlan _failedPlan;    // 作るのに失敗したステージ（同じランのリトライでも、もう作らない）
    static Sprite _wallpaper;
    static bool _wallpaperTried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Built = null;
        _windows.Clear();
        _active = false;
        _root = null;
        _failedPlan = null;
        _wallpaper = null;
        _wallpaperTried = false;
        ForceAll = false;
        ForceTemplate = -1;
    }

    // ───────── どのステージか（1-1） ─────────

    /// <summary>そのステージをウィンドウのステージにするか。例外は外へ出さない（false）</summary>
    public static bool Wants(StagePlan plan)
    {
        try
        {
            if (!Enabled || plan == null || plan.App == null || plan.IsFinal) return false;
            if (!AppBackgrounds.StagePlatforms) return false; // 足場を作らない設定なら、ウィンドウも作らない（壁紙だけ変わらないように）
            if (ReferenceEquals(plan, _failedPlan)) return false;
            if (!string.Equals(plan.App.StageScene, DesktopScene, StringComparison.Ordinal)) return false;
            if (ForceAll) return true;
            string label = AppBackgrounds.LabelFor(plan);
            if (string.IsNullOrEmpty(label)) return AlsoUnlisted;
            return IsListed(label);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("WindowStage.Wants", $"[WindowStage] ウィンドウのステージにするか決められません（今までのステージにします）：{e}");
            return false;
        }
    }

    /// <summary>ウィンドウのステージの壁紙（Mac のデスクトップを明るくした物）。無い・使わないときは null</summary>
    public static Sprite Wallpaper()
    {
        if (!UseMacWallpaper) return null;
        try
        {
            if (_wallpaper == null && !_wallpaperTried)
            {
                _wallpaperTried = true;
                _wallpaper = Resources.Load<Sprite>(WallpaperPath);
                if (_wallpaper == null && Debug.isDebugBuild) Debug.Log($"[WindowStage] {WallpaperPath} が無いので、今までの壁紙");
            }
            return _wallpaper;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("WindowStage.Wallpaper", $"[WindowStage] 壁紙を読めません：{e}");
            return null;
        }
    }

    // ───────── 重なり（7-2。ワーム・巣・汚染が使う） ─────────

    /// <summary>その足場のウィンドウ（ウィンドウでなければ null）</summary>
    public static StageWindow Of(AppPlatform platform)
    {
        if (platform == null || !Active) return null;
        for (int i = 0; i < _windows.Count; i++)
            if (_windows[i] != null && _windows[i].Platform == platform) return _windows[i];
        return null;
    }

    /// <summary>その点（ワールド）が、window より手前のウィンドウの裏にかくれているか（手前のウィンドウの四角を inset マス内側にした中）</summary>
    public static bool Hidden(StageWindow window, Vector2 p, float inset = 0.1f)
    {
        if (window == null || !Active) return false;
        int index = _windows.IndexOf(window);
        if (index < 0) return false;
        for (int j = index + 1; j < _windows.Count; j++)
        {
            StageWindow w = _windows[j];
            if (w == null) continue;
            Rect r = w.World;
            if (p.x > r.xMin + inset && p.x < r.xMax - inset && p.y > r.yMin + inset && p.y < r.yMax - inset) return true;
        }
        return false;
    }

    /// <summary>
    /// window の下の端の、手前のウィンドウ（四角を grow マス広げた物）にかくれていない区間のうち、いちばん長い物（7-2 の巣）。
    /// 無ければ false
    /// </summary>
    public static bool LongestVisibleBottom(StageWindow window, float grow, out float x0, out float x1)
    {
        x0 = x1 = 0f;
        if (window == null || !Active) return false;
        int index = _windows.IndexOf(window);
        if (index < 0) return false;
        Rect me = window.World;
        float y = me.yMin;
        var spans = new List<Vector2> { new Vector2(me.xMin, me.xMax) };
        for (int j = index + 1; j < _windows.Count; j++)
        {
            StageWindow w = _windows[j];
            if (w == null) continue;
            Rect r = w.World;
            if (y < r.yMin - grow || y > r.yMax + grow) continue;
            float c0 = r.xMin - grow, c1 = r.xMax + grow;
            for (int k = spans.Count - 1; k >= 0; k--)
            {
                Vector2 s = spans[k];
                if (c1 <= s.x || c0 >= s.y) continue;
                spans.RemoveAt(k);
                if (c0 > s.x) spans.Add(new Vector2(s.x, c0));
                if (c1 < s.y) spans.Add(new Vector2(c1, s.y));
            }
        }
        float best = 0f;
        foreach (Vector2 s in spans)
            if (s.y - s.x > best)
            {
                best = s.y - s.x;
                x0 = s.x;
                x1 = s.y;
            }
        return best > 0f;
    }

    /// <summary>その点（ワールド）で、いちばん手前に見えているウィンドウ（四角を grow マス広げて見る。無ければ null）</summary>
    public static StageWindow FrontAt(Vector2 p, float grow = 0f)
    {
        if (!Active) return null;
        for (int j = _windows.Count - 1; j >= 0; j--)
        {
            StageWindow w = _windows[j];
            if (w == null) continue;
            Rect r = w.World;
            if (p.x >= r.xMin - grow && p.x <= r.xMax + grow && p.y >= r.yMin - grow && p.y <= r.yMax + grow) return w;
        }
        return null;
    }

    static bool IsListed(string label)
    {
        foreach (string l in Labels)
            if (string.Equals(l, label, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ───────── 作る ─────────

    /// <summary>AppStagePlatforms.Build から呼ぶ（フォルダの足場を見つけた後）。作った数を返す（0＝作らなかった。今までどおり）</summary>
    internal static int Build(StagePlan plan, Transform parent)
    {
        Clear();
        if (plan == null || parent == null) return 0;
        GameObject root = null;
        try
        {
            string label = AppBackgrounds.LabelFor(plan);
            string key = !string.IsNullOrEmpty(label) ? label : plan.DisplayName ?? "";
            int sum = 0;
            foreach (char ch in key) sum += ch;
            int index = 0;
            GameSession session = GameSession.Instance;
            if (session != null && session.Run != null) index = Mathf.Max(0, session.Run.CurrentIndex);
            int template = ForceTemplate >= 0 && ForceTemplate < Templates.Length ? ForceTemplate : (sum + index) % Templates.Length;
            bool mirror = sum % 2 == 1;
            TitleRow row = RowFor(label, plan.DisplayName);
            MacLook look = row.Dark ? MacLook.Dark : MacLook.Light;
            Color accent = AccentOf(plan);

            root = new GameObject($"WindowStage_{key}");
            root.transform.SetParent(parent, false); // フォルダと同じ入れ物（ステージのシーン）。シーンといっしょに消える
            Slot[] slots = Templates[template];
            string[] titles = AssignTitles(slots, row.Titles);
            var rng = new System.Random(sum * 31 + template * 7 + 1);
            var made = new List<StageWindow>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
                made.Add(MakeWindow(slots[i], mirror, i, i == slots.Length - 1, titles[i], row.Kind, look, accent, rng, root.transform));
            PlaceFolders(root.transform);

            _windows.AddRange(made);
            _root = root;
            _active = true;
            if (Debug.isDebugBuild) Debug.Log($"[WindowStage] {key}：型 {template}{(mirror ? "（左右反転）" : "")}、ウィンドウ {made.Count} 枚");
            RaiseBuilt();
            return made.Count;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("WindowStage.Build", $"[WindowStage] ウィンドウのステージを作れません（今までのステージにします）：{e}");
            Fail(plan, root);
            return 0;
        }
    }

    static void Clear()
    {
        _windows.Clear();
        _active = false;
        _root = null;
    }

    // 作りかけを消して、今までのステージに戻す。壁紙（AppBackgrounds.ForStage が先に Mac の壁紙にした）も、できればアプリの UI の絵に戻す
    static void Fail(StagePlan plan, GameObject root)
    {
        _failedPlan = plan;
        Clear();
        if (root != null)
        {
            root.SetActive(false); // Destroy はフレームの終わり。今の Awake の中の足場さがし（ReseatWave）に当たらないように、先に止める
            UnityEngine.Object.Destroy(root);
        }
        try
        {
            Sprite mac = _wallpaper;
            if (mac == null) return;
            Sprite app = AppBackgrounds.ForStage(plan); // もう Wants は false なので、アプリの UI の絵（無ければ null）
            if (app == null) return;                    // 色を変えた今までの壁紙には戻せないので、Mac の壁紙のまま
            foreach (SpriteRenderer sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true))
                if (sr != null && sr.sprite == mac) sr.sprite = app;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("WindowStage.Fail", $"[WindowStage] 壁紙を戻せません：{e}");
        }
    }

    static void RaiseBuilt()
    {
        Action handler = Built;
        if (handler == null) return;
        foreach (Delegate d in handler.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { SkinDresser.LogOnce("WindowStage.Built", $"[WindowStage] Built を聞いている所で例外（ステージは続けます）：{e}"); }
        }
    }

    static StageWindow MakeWindow(Slot s, bool mirror, int i, bool front, string title, Kind kind, MacLook look, Color accent, System.Random rng, Transform parent)
    {
        // 大きさはドットに合わせる（1-3）。上の端はそのまま
        int wPx = Mathf.Max(16, Mathf.RoundToInt(s.W * Ppu));
        int hPx = Mathf.Max(16, Mathf.RoundToInt(s.H * Ppu));
        float w = wPx / (float)Ppu, h = hPx / (float)Ppu;
        var center = new Vector2(mirror ? -s.Cx : s.Cx, s.Top - h * 0.5f);
        int shadowOrder = BaseOrder + 3 * i, artOrder = shadowOrder + 1, textOrder = shadowOrder + 2;

        // 題名：80ドット以上で、入るなら全部 → 入らなければ「—」の後ろだけ → それも入らなければ字なし（灰色の棒）
        string shown = null;
        if (wPx >= MacUi.BigWindowW && !string.IsNullOrEmpty(title))
        {
            if (MacUi.FitsChars(title, wPx - 30)) shown = title;
            else
            {
                string room = RoomOf(title);
                if (!string.IsNullOrEmpty(room) && MacUi.FitsChars(room, wPx - 30)) shown = room;
            }
        }

        var canvas = new MacCanvas(wPx, hPx);
        MacUi.StampWindow(canvas.Set, wPx, hPx, look, null, 0, shown == null, front);
        DrawContent(canvas, wPx, hPx, look, kind, s.Big, accent, rng);
        Texture2D tex = ToTexture(canvas, $"WindowStage_{s.Id}");
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, wPx, hPx), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
        sprite.name = tex.name;

        AppPlatform platform = AppStagePlatforms.SpawnWindow(s.Id, center, new Vector2(w, h), sprite, artOrder, parent);
        if (platform == null)
        {
            UnityEngine.Object.Destroy(sprite);
            UnityEngine.Object.Destroy(tex);
            throw new InvalidOperationException($"ウィンドウ {s.Id} の足場を作れません");
        }
        var owner = platform.gameObject.AddComponent<StageWindowArt>(); // ウィンドウが消えたら、自分の絵も消す
        owner.Texture = tex;
        owner.Sprite = sprite;
        Transform artT = platform.transform.Find("Art");
        SpriteRenderer art = artT != null ? artT.GetComponent<SpriteRenderer>() : null;
        if (art == null) throw new InvalidOperationException($"ウィンドウ {s.Id} に Art がありません");

        // 影（2ドット下へずらす。共有の形でよい：書きかえるのはウィンドウの絵だけ）
        SpriteRenderer shadow = RuntimeSprites.Show(platform.transform, MacUi.ShadowSprite(wPx, hPx), new Vector2(0f, -MacUi.ShadowY * MacUi.Px), shadowOrder, 1f, "Shadow");
        shadow.color = MacUi.ShadowColor(look);

        TextMesh tm = null;
        if (shown != null)
        {
            int th = MacUi.TitleHeightFor(hPx);
            tm = MacUi.Label(platform.transform, shown, MacUi.DotToLocal(wPx, hPx, wPx * 0.5f, 1f + th * 0.5f), MacUi.TextH, MacUi.Ink(look), textOrder, true);
        }

        Bounds b = art.bounds;
        Rect world = b.size.x > 0.01f ? new Rect(b.min.x, b.min.y, b.size.x, b.size.y) : new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        return new StageWindow
        {
            Id = s.Id,
            Platform = platform,
            Art = art,
            Texture = tex,
            Clean = tex.GetPixels32(),
            W = wPx,
            H = hPx,
            TitleH = MacUi.HeaderH(hPx),
            World = world,
            Title = tm,
            TitleText = shown,
            TitleColor = MacUi.Ink(look),
            Front = front,
        };
    }

    // ドットの板（y は上から）を、自分だけの Texture2D（下の行から・読み書きできる）にする
    static Texture2D ToTexture(MacCanvas c, string name)
    {
        var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[c.W * c.H];
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
                px[(c.H - 1 - y) * c.W + x] = c.Get(x, y);
        tex.SetPixels32(px);
        tex.Apply(false, false); // 読めるまま（蝕む係が書きかえる）
        return tex;
    }

    // 右上のあいた所に、デスクトップのフォルダを 2つ（字は無し。飾り）
    static void PlaceFolders(Transform root)
    {
        Sprite folder = MacUi.Folder(14);
        if (folder == null) return;
        RuntimeSprites.Show(root, folder, new Vector2(9.0f, 9.1f), FolderOrder, 1f, "DesktopFolder");
        RuntimeSprites.Show(root, folder, new Vector2(9.0f, 8.15f), FolderOrder, 1f, "DesktopFolder");
    }

    // ───────── 中身の絵（1-5 の 2） ─────────

    static void DrawContent(MacCanvas c, int w, int h, MacLook look, Kind kind, bool big, Color accent, System.Random rng)
    {
        int r = MacUi.CornerFor(w);
        int top = MacUi.HeaderH(h);  // タイトルバーの下の線の、次の行から
        int bottom = h - 2;          // 下のふちの 1つ上まで
        if (bottom - top < 3) return;
        bool dark = look == MacLook.Dark;
        bool step = h < 30;          // 段（通知の板に見える。中身 1行だけ）
        Color32 bar = dark ? MacUi.Gray2 : MacUi.Light4;
        Color32 faint = dark ? MacUi.Gray3 : MacUi.Light2;

        bool Inside(int x, int y) => y >= top && y <= bottom && MacUi.InRound(x - 1, y - 1, w - 2, h - 2, Mathf.Max(0, r - 1));
        void Box(int x, int y, int bw, int bh, Color32 col)
        {
            for (int j = 0; j < bh; j++)
                for (int i = 0; i < bw; i++)
                    if (Inside(x + i, y + j)) c.Set(x + i, y + j, col);
        }
        int Len(int max, double lo, double hi) => Mathf.Clamp((int)(max * (lo + rng.NextDouble() * (hi - lo))), Mathf.Min(3, max), Mathf.Max(1, max));

        // 暗い見た目（Discord）：#1E1E1E は敵の黒に近いので、中身の面を明るい灰色で塗り直す
        if (dark) Box(1, top, w - 2, bottom - top + 1, MacUi.Gray4);

        int left = 3;
        if (big)
        {
            // 左のアプリの色の帯（くすませる）。チャンネル・フォルダの一覧のふりの棒
            int sw = Mathf.RoundToInt(w * 0.22f);
            Color side = Color.Lerp(accent, Color.white, 0.5f);
            Box(1, top, sw, bottom - top + 1, side);
            Color32 sideBar = Color.Lerp(side, Color.white, 0.55f);
            for (int y = top + 3; y + 1 <= bottom - 2; y += 5) Box(3, y, Len(sw - 4, 0.4, 0.9), 1, sideBar);
            left = 1 + sw + 3;
        }
        int right = w - 4;           // 中身の右の端（含む）
        int avail = right - left + 1;
        if (avail < 6) return;

        switch (kind)
        {
            case Kind.Chat:
            {
                Color32[] faces = { accent, MacUi.Blue, MacUi.Green, MacUi.Orange };
                const int inputH = 5;
                int rowsEnd = step ? bottom - 1 : bottom - inputH - 3;
                int k = 0;
                for (int y = top + 3; y + 2 <= rowsEnd; y += 6)
                {
                    Box(left, y, 3, 3, faces[k % faces.Length]);
                    Box(left + 5, y + 1, Len(avail - 5, 0.35, 0.95), 2, bar);
                    k++;
                    if (step) break;
                }
                if (!step)
                {
                    // 入力の枠（白。ふちは灰色）
                    int y0 = bottom - inputH;
                    Box(left, y0, avail, inputH, bar);
                    Box(left + 1, y0 + 1, avail - 2, inputH - 2, dark ? MacUi.Gray3 : (Color)Color.white);
                }
                break;
            }
            case Kind.Note:
            {
                if (step)
                {
                    Box(left, top + 3, Len(avail, 0.5, 0.9), 2, bar);
                    break;
                }
                // 左に細い行の一覧（いちばん上はえらんでいる行）、右に長い棒 4〜6本
                int lw = Mathf.Max(6, Mathf.RoundToInt(avail * 0.32f));
                Box(left - 1, top + 1, lw + 1, 5, Color.Lerp(accent, Color.white, 0.65f));
                for (int y = top + 3; y + 1 <= bottom - 2; y += 6) Box(left, y, Len(lw - 1, 0.5, 0.95), 1, bar);
                for (int y = top + 1; y <= bottom; y++) Box(left + lw + 1, y, 1, 1, faint);
                int x0 = left + lw + 4, bodyW = right - x0 + 1;
                if (bodyW < 4) break;
                int n = 4 + rng.Next(3);
                for (int j = 0, y = top + 3; j < n && y + 1 <= bottom - 2; j++, y += 5)
                    Box(x0, y, j == n - 1 ? Len(bodyW, 0.3, 0.6) : Len(bodyW, 0.6, 1.0), 2, bar);
                break;
            }
            default: // List
            {
                // 横の細い線で行を分け、行ごとに灰色の棒 2本（長い・短い）
                for (int y = top + 3; y + 1 <= bottom - 1; y += 7)
                {
                    int longLen = Len(avail, 0.45, 0.7);
                    int shortLen = Len(Mathf.Max(3, avail / 4), 0.6, 1.0);
                    Box(left, y, longLen, 2, bar);
                    if (left + longLen + 3 < right - shortLen) Box(right - shortLen + 1, y, shortLen, 2, bar);
                    if (step) break;
                    if (y + 4 <= bottom - 1) Box(left - 1, y + 4, avail + 1, 1, faint);
                }
                break;
            }
        }
    }

    // ───────── 表 ─────────

    enum Kind { Chat, List, Note }

    struct Slot
    {
        public string Id;
        public float Cx, W, Top, H;
        public bool Big;
        public Slot(string id, float cx, float w, float top, float h, bool big)
        {
            Id = id;
            Cx = cx;
            W = w;
            Top = top;
            H = h;
            Big = big;
        }
    }

    // 型 3つ（7-1。まん中 cx・幅 w・上の端 top・高さ h。奥から順＝top の高い順）。
    // 重なる 2枚では手前の top が奥より 0.6 以上低い（奥のタイトルバーと上の端の当たり判定が全部見える）。幅は 9.0 まで。段は h 1.0〜1.1
    static readonly Slot[][] Templates =
    {
        new[] // 0：カスケード
        {
            new Slot("A", -3.4f, 9.0f, 7.6f, 5.0f, true),
            new Slot("B", 3.6f, 9.0f, 5.6f, 3.8f, true),
            new Slot("C", -5.6f, 5.4f, 3.0f, 1.1f, false),
            new Slot("D", 1.0f, 4.5f, 3.0f, 1.0f, false),
        },
        new[] // 1：山
        {
            new Slot("A", 0f, 9.0f, 7.4f, 4.8f, true),
            new Slot("B", -5.6f, 7.0f, 5.2f, 3.4f, true),
            new Slot("C", 5.8f, 6.6f, 5.6f, 3.6f, true),
            new Slot("D", 0.2f, 6.6f, 3.0f, 1.1f, false),
        },
        new[] // 2：ななめ
        {
            new Slot("A", -4.6f, 8.4f, 7.6f, 4.6f, true),
            new Slot("B", 0.6f, 9.0f, 5.4f, 3.4f, true),
            new Slot("C", 5.4f, 7.0f, 3.0f, 1.1f, false),
            new Slot("D", -5.4f, 5.0f, 2.9f, 1.0f, false),
        },
    };

    struct TitleRow
    {
        public string[] Titles;
        public Kind Kind;
        public bool Dark;
    }

    // 題名（<アプリ> — <部屋>。大きいウィンドウから順に使う。12字まで）と中身の形
    static readonly Dictionary<string, TitleRow> Rows = new Dictionary<string, TitleRow>(StringComparer.OrdinalIgnoreCase)
    {
        { "Slack", new TitleRow { Titles = new[] { "Slack — スレッド", "Slack — 通知", "Slack — DM", "Slack — 検索" }, Kind = Kind.Chat } },
        { "Discord", new TitleRow { Titles = new[] { "Discord — 通知", "Discord — DM", "Discord — 通話", "Discord — 検索" }, Kind = Kind.Chat, Dark = true } },
        { "LINE", new TitleRow { Titles = new[] { "LINE — トーク", "LINE — 通知", "LINE — 友だち", "LINE — ノート" }, Kind = Kind.Chat } },
        { "Messages", new TitleRow { Titles = new[] { "メッセージ — 新規", "メッセージ — 通知", "メッセージ — 写真", "メッセージ — 検索" }, Kind = Kind.Chat } },
        { "Mail", new TitleRow { Titles = new[] { "メール — 受信", "メール — 新規", "メール — 下書き", "メール — 迷惑" }, Kind = Kind.List } },
        { "Notes", new TitleRow { Titles = new[] { "メモ — すべて", "メモ — 新規", "メモ — フォルダ", "メモ — 最近" }, Kind = Kind.Note } },
    };

    // 表に無いラベル（代用ボス・ForceAll）は「<アプリの名前> — 1〜4」と list
    static TitleRow RowFor(string label, string displayName)
    {
        if (!string.IsNullOrEmpty(label) && Rows.TryGetValue(label, out TitleRow row) && row.Titles != null && row.Titles.Length > 0) return row;
        string name = string.IsNullOrEmpty(displayName) ? "App" : displayName;
        return new TitleRow { Titles = new[] { $"{name} — 1", $"{name} — 2", $"{name} — 3", $"{name} — 4" }, Kind = Kind.List };
    }

    // 題名を、幅の広いウィンドウから順に割り当てる（同じ幅なら奥から）
    static string[] AssignTitles(Slot[] slots, string[] titles)
    {
        var order = new List<int>();
        for (int i = 0; i < slots.Length; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int cmp = slots[b].W.CompareTo(slots[a].W);
            return cmp != 0 ? cmp : a.CompareTo(b);
        });
        var result = new string[slots.Length];
        for (int k = 0; k < order.Count; k++) result[order[k]] = titles != null && titles.Length > 0 ? titles[k % titles.Length] : null;
        return result;
    }

    static string RoomOf(string title)
    {
        int i = title.IndexOf('—');
        return i >= 0 && i + 1 < title.Length ? title.Substring(i + 1).Trim() : null;
    }

    static Color AccentOf(StagePlan plan)
    {
        AppTheme theme = plan != null ? plan.Theme : null;
        Color c = theme != null ? theme.Main : new Color(0.55f, 0.6f, 0.7f);
        c.a = 1f;
        return c;
    }
}

/// <summary>ウィンドウ 1枚（4章の約束。名前・型を変えない）</summary>
public sealed class StageWindow
{
    public string Id;                 // "A"〜"E"
    public AppPlatform Platform;
    public SpriteRenderer Art;        // 子 "Art"
    public Texture2D Texture;         // このウィンドウだけの絵（RGBA32・Point・読み書きできる）。B が書きかえる
    public Color32[] Clean;           // 元の絵（Texture.GetPixels32 と同じ並び＝下の行から。長さ W×H）。書きかえない
    public int W, H;                  // ドット（18ドット＝1マス）
    public int TitleH;                // 上から何ドットがタイトルバーか（ふち・下の線こみ＝MacUi.HeaderH(H)）。行 y >= H − TitleH がタイトルバー
    public Rect World;                // Art の四角（ワールド座標。動かない）
    public TextMesh Title;            // 題名の字（無ければ null）
    public string TitleText;          // 元の題名
    public Color TitleColor;          // 元の字の色
    public bool Front;                // いちばん手前か
}

/// <summary>ウィンドウの自分だけの絵を、ウィンドウといっしょに消す（WindowStage が付ける）</summary>
public class StageWindowArt : MonoBehaviour
{
    internal Texture2D Texture;
    internal Sprite Sprite;

    void OnDestroy()
    {
        if (Sprite != null) Destroy(Sprite);
        if (Texture != null) Destroy(Texture);
    }
}
