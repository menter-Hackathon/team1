using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】Mac らしさの共通の部品（2026-11-19。docs/mac-look-design.md 1章）。
// ・ウィンドウ・ダイアログ・通知・信号機・待ちの印・キーは、ここで描く。色と数は、ここの定数だけに置く
// ・入口は3つ：(a) 絵を作る（Stamp*・*Sprite）、(b) ゲームの世界に置く（Place*・Label・CloseAnim）、(c) OnGUI（MacUi.Gui）
// ・大きさはドット（1マス＝18ドット）。OnGUI は仮想の画面 640×360 のドットで、同じ数を使う
// ・座標の y は上から（ドット絵の行）。絵の左上が (0, 0)

/// <summary>【仮】明るい見た目か、暗い見た目か（暗いのは VS Code・Terminal・Premiere・Steam・Spotify・ChatGPT）</summary>
public enum MacLook { Light, Dark }

/// <summary>【仮】Mac の部品（2026-11-19。docs/mac-look-design.md 1-1〜1-10）</summary>
public static partial class MacUi
{
    // ───────── 色（1-1。macOS のシステムの色） ─────────

    public static readonly Color Red = Hex("FF453A");
    public static readonly Color Orange = Hex("FF9F0A");
    public static readonly Color Yellow = Hex("FFD60A");
    public static readonly Color Green = Hex("30D158");
    public static readonly Color Blue = Hex("0A84FF");
    public static readonly Color Purple = Hex("BF5AF2");
    public static readonly Color Pink = Hex("FF375F");
    /// <summary>通知のバッジの赤（今と同じ）</summary>
    public static readonly Color BadgeRed = Hex("FF3B30");

    public static readonly Color Gray1 = Hex("8E8E93");
    public static readonly Color Gray2 = Hex("636366");
    public static readonly Color Gray3 = Hex("48484A");
    public static readonly Color Gray4 = Hex("3A3A3C");
    public static readonly Color Gray5 = Hex("2C2C2E");
    public static readonly Color Gray6 = Hex("1C1C1E");

    public static readonly Color Light1 = Hex("F2F2F7");
    public static readonly Color Light2 = Hex("E5E5EA");
    public static readonly Color Light3 = Hex("D1D1D6");
    public static readonly Color Light4 = Hex("C7C7CC");
    public static readonly Color Light5 = Hex("AEAEB2");

    /// <summary>信号機（赤・黄・緑）。使えないとき（後ろのウィンドウ）は 3つとも LightOff</summary>
    public static readonly Color LightR = Hex("FF5F57");
    public static readonly Color LightY = Hex("FEBC2E");
    public static readonly Color LightG = Hex("28C840");
    public static readonly Color LightOff = Hex("C7C7CC");
    /// <summary>押した赤い丸（1ドット暗く見せる色）</summary>
    public static readonly Color LightRPressed = Hex("C9413A");

    /// <summary>待ちカーソルの6色（FxArt.BeachBall の今の色）</summary>
    public static readonly Color[] BeachBallColors =
    {
        Hex("FF3B30"), Hex("FF9500"), Hex("FFCC00"), Hex("34C759"), Hex("0A84FF"), Hex("AF52DE"),
    };

    /// <summary>うすい字・字の代わりの棒（明るい・暗いどちらも）</summary>
    public static readonly Color SubText = Hex("8E8E93");
    /// <summary>既定のボタン（青）</summary>
    public static readonly Color ButtonDefault = Hex("0A84FF");
    /// <summary>選択（青い 1ドットの枠、中は 2割）</summary>
    public static readonly Color Selection = Hex("0A84FF");
    /// <summary>キーの地・影・字（DevArt.Key と同じ）</summary>
    public static readonly Color KeyFace = Hex("E5E5EA");
    public static readonly Color KeyShade = Hex("A0A0A5");
    public static readonly Color KeyInk = Hex("1C1C1E");
    /// <summary>フォルダの青（FinderArt.SmartFolder の色）</summary>
    public static readonly Color FolderBlue = Hex("5AB0F0");
    public static readonly Color FolderShade = Hex("2F7FC4");
    public static readonly Color FolderShine = Hex("8FCBF7");
    /// <summary>Dock の地（白 28%）とふち（白 60%）</summary>
    public static readonly Color DockFill = new Color(1f, 1f, 1f, 0.28f);
    public static readonly Color DockEdge = new Color(1f, 1f, 1f, 0.6f);

    /// <summary>【要相談 6】通知の地を暗くするなら true（今は Mac のライトモードに合わせて明るい）</summary>
    public static bool BannerDark = false;

    // ───────── 数（1-2〜1-7） ─────────

    /// <summary>1ドットの大きさ（マス）</summary>
    public const float Px = 1f / 18f;
    /// <summary>タイトルバーの高さ（ふちと下の線は別）</summary>
    public const int TitleH = 7;
    /// <summary>ウィンドウの高さが SmallWindowH より小さいときのタイトルバー</summary>
    public const int TitleHSmall = 5;
    public const int SmallWindowH = 20;
    /// <summary>信号機の丸の直径・左のふちからの位置・丸と丸の間</summary>
    public const int LightD = 3;
    public const int LightX = 3;
    public const int LightGap = 1;
    /// <summary>幅がこれより小さいウィンドウは、信号機を 1ドットの点にする（x＝2・4・6）</summary>
    public const int TinyWindowW = 30;
    /// <summary>角を欠くドットの数（幅 BigWindowW 以上は CornerBig）</summary>
    public const int Corner = 2;
    public const int CornerBig = 3;
    public const int BigWindowW = 80;
    /// <summary>影を下へずらすドット</summary>
    public const int ShadowY = 2;
    /// <summary>ツールバーの高さ（あれば）</summary>
    public const int ToolbarH = 5;
    /// <summary>題名の棒（高さ 2ドット、幅はウィンドウの 3割）</summary>
    public const int TitleBarH = 2;
    public const float TitleBarRatio = 0.3f;
    /// <summary>通知の角（小さい通知は BannerCornerSmall）</summary>
    public const int BannerCorner = 3;
    public const int BannerCornerSmall = 2;
    /// <summary>ダイアログのボタン（高さ。字を入れるときは ButtonTextH）・角・字を入れる最小の幅</summary>
    public const int ButtonH = 7;
    public const int ButtonTextH = 9;
    public const int ButtonCorner = 1;
    public const int ButtonTextMinW = 20;
    /// <summary>アプリのアイコン（ダイアログ・通知）</summary>
    public const int IconPx = 16;
    /// <summary>ダイアログ・強制終了の幅の上限（9マス）と、題名の字数の上限</summary>
    public const int AlertMaxW = 162;
    public const int AlertTitleMaxChars = 18;
    /// <summary>キーの大きさ</summary>
    public const int KeyPx = 9;
    /// <summary>字の高さ（マス。Press Start 2P はこれより小さくしない）</summary>
    public const float TextH = 0.4f;
    /// <summary>Press Start 2P の1字の幅（ドット。字の高さ 0.4マスのとき）</summary>
    public const float CharPx = TextH * 18f;

    // ───────── 面の色（1-1 の下の表） ─────────

    public static Color WindowBg(MacLook look) => look == MacLook.Dark ? Hex("1E1E1E") : Light1;
    public static Color TitleBg(MacLook look) => look == MacLook.Dark ? Gray4 : Light2;
    public static Color TitleLine(MacLook look) => look == MacLook.Dark ? new Color(0f, 0f, 0f, 0.5f) : Light4;
    public static Color Edge(MacLook look) => look == MacLook.Dark ? Hex("5A5A5E") : Light5;
    public static Color Ink(MacLook look) => look == MacLook.Dark ? Color.white : Gray6;
    public static Color ButtonBg(MacLook look) => look == MacLook.Dark ? Gray2 : Color.white;
    public static Color ButtonEdge(MacLook look) => look == MacLook.Dark ? Gray2 : Light4;
    public static Color ShadowColor(MacLook look) => new Color(0f, 0f, 0f, look == MacLook.Dark ? 0.45f : 0.3f);
    public static Color BannerBg(MacLook look) => look == MacLook.Dark ? new Color(Gray6.r, Gray6.g, Gray6.b, 0.9f) : new Color(Light1.r, Light1.g, Light1.b, 0.92f);
    public static Color ProgressTrough(MacLook look) => look == MacLook.Dark ? Gray4 : Light3;
    /// <summary>通知の見た目（BannerDark で切り替え）</summary>
    public static MacLook BannerLook => BannerDark ? MacLook.Dark : MacLook.Light;

    /// <summary>タイトルバーの高さ（ウィンドウの高さで決まる）</summary>
    public static int TitleHeightFor(int h) => h < SmallWindowH ? TitleHSmall : TitleH;
    /// <summary>角を欠く数（ウィンドウの幅で決まる）</summary>
    public static int CornerFor(int w) => w >= BigWindowW ? CornerBig : Corner;
    /// <summary>ふち・タイトルバー・下の線を足した高さ（中の面はここから下）</summary>
    public static int HeaderH(int h, int toolbarH = 0) => 1 + TitleHeightFor(h) + Mathf.Max(0, toolbarH) + 1;

    // ───────── (a) 絵を作る ─────────

    /// <summary>
    /// 【注意】put には半とうめいの色も来る（信号機の角・暗いタイトルバーの下の線）。重ねる（透明さで混ぜる）こと。MacCanvas.Put はそうなっている。
    /// 信号機（3×3 の丸 3つ。4つの角は 5割すかす）。x は左の丸の左の端、yTop は丸の上の端。
    /// active＝false なら 3つとも灰色。redOnly なら赤だけ色つき（ダイアログ）
    /// </summary>
    public static void StampLights(Action<int, int, Color32> put, int x, int yTop, bool active = true, bool redOnly = false)
    {
        if (put == null) return;
        Color[] cols =
        {
            active ? LightR : LightOff,
            active && !redOnly ? LightY : LightOff,
            active && !redOnly ? LightG : LightOff,
        };
        for (int i = 0; i < 3; i++) StampDot3(put, x + i * (LightD + LightGap), yTop, cols[i]);
    }

    /// <summary>大きさを変えた信号機（d は丸の直径。間は gap、無ければ d/3）。d＝3 なら StampLights と同じ</summary>
    public static void StampLights(Action<int, int, Color32> put, int x, int yTop, int d, bool active = true, bool redOnly = false, int gap = -1)
    {
        if (put == null) return;
        if (d == LightD && gap < 0)
        {
            StampLights(put, x, yTop, active, redOnly);
            return;
        }
        int g = gap >= 0 ? gap : Mathf.Max(1, d / 3);
        Color[] cols =
        {
            active ? LightR : LightOff,
            active && !redOnly ? LightY : LightOff,
            active && !redOnly ? LightG : LightOff,
        };
        for (int i = 0; i < 3; i++) StampDisc(put, x + i * (d + g), yTop, d, cols[i]);
    }

    /// <summary>丸（左上 x, y、直径 d）。ふちのドットは 5割すかす（3 なら StampLights の丸と同じ形）</summary>
    public static void StampDisc(Action<int, int, Color32> put, int x, int y, int d, Color c)
    {
        if (put == null || d <= 0) return;
        if (d == 3)
        {
            StampDot3(put, x, y, c);
            return;
        }
        Color half = new Color(c.r, c.g, c.b, c.a * 0.5f);
        float r = d * 0.5f;
        for (int j = 0; j < d; j++)
            for (int i = 0; i < d; i++)
            {
                float dx = i + 0.5f - r, dy = j + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r - 0.5f) put(x + i, y + j, c);
                else if (dist <= r + 0.15f) put(x + i, y + j, half);
            }
    }

    /// <summary>小さいウィンドウの信号機（1ドットの点 3つ。x, x+2, x+4）</summary>
    public static void StampLightsTiny(Action<int, int, Color32> put, int x, int y, bool active = true)
    {
        if (put == null) return;
        put(x, y, active ? LightR : LightOff);
        put(x + 2, y, active ? LightY : LightOff);
        put(x + 4, y, active ? LightG : LightOff);
    }

    static void StampDot3(Action<int, int, Color32> put, int x, int y, Color c)
    {
        Color half = new Color(c.r, c.g, c.b, c.a * 0.5f);
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                put(x + i, y + j, (i == 1 || j == 1) ? c : half);
    }

    /// <summary>
    /// タイトルバー（ウィンドウの上の 1＋titleH＋1 行。上のふち・角・タイトルバー・信号機・題名の棒・下の線）。
    /// 左右のふちは描く。titleBar＝false なら題名の棒を描かない（字を重ねるとき）。h はウィンドウの高さ（タイトルバーの高さを決める）
    /// </summary>
    public static void StampTitleBar(Action<int, int, Color32> put, int w, MacLook look, bool titleBar, int h = 36, Color? edge = null, bool active = true, bool redOnly = false)
    {
        if (put == null) return;
        int th = TitleHeightFor(h);
        int r = CornerFor(w);
        Color e = edge ?? Edge(look);
        Color t = TitleBg(look);
        Color line = TitleLine(look);
        for (int y = 0; y <= th + 1; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!InRound(x, y, w, Mathf.Max(h, th + 2), r)) continue;
                bool isEdge = y == 0 || x == 0 || x == w - 1 || !InRound(x - 1, y, w, h, r) || !InRound(x + 1, y, w, h, r) || !InRound(x, y - 1, w, h, r);
                if (isEdge) put(x, y, e);
                else if (y == th + 1)
                {
                    put(x, y, t);
                    put(x, y, line);
                }
                else put(x, y, t);
            }
        }
        if (w < TinyWindowW) StampLightsTiny(put, 2, 1 + (th - 1) / 2, active);
        else StampLights(put, LightX, 1 + (th - LightD) / 2, active, redOnly);
        if (titleBar) StampTitlePlaceholder(put, w, th);
    }

    static void StampTitlePlaceholder(Action<int, int, Color32> put, int w, int th)
    {
        int bw = Mathf.Max(3, Mathf.RoundToInt(w * TitleBarRatio));
        int bx = (w - bw) / 2;
        int lightsRight = w < TinyWindowW ? 7 : LightX + LightD * 3 + LightGap * 2 + 1;
        if (bx < lightsRight + 1) return; // 信号機と重なるなら描かない
        int by = 1 + (th - TitleBarH) / 2;
        for (int j = 0; j < TitleBarH; j++)
            for (int i = 0; i < bw; i++)
                put(bx + i, by + j, SubText);
    }

    /// <summary>
    /// ウィンドウの絵（w×h ドット。影は入れない＝PlaceWindow が別の絵で下へずらして置く）。
    /// edge を入れると、ふちだけその色（敵の攻撃は MacUi.Red）。toolbarH はタイトルバーの下の帯
    /// </summary>
    public static Sprite WindowSprite(int w, int h, MacLook look, Color32? edge = null, int toolbarH = 0, bool titleBar = true, bool active = true)
    {
        w = Mathf.Clamp(w, 8, 512);
        h = Mathf.Clamp(h, 8, 512);
        string key = $"MacWin_{w}x{h}_{look}_{Key(edge)}_{toolbarH}_{titleBar}_{active}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        DrawWindow(c, w, h, look, edge.HasValue ? (Color)edge.Value : Edge(look), toolbarH, titleBar, active, false);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>
    /// ウィンドウ全体を格子に描く（担当2 のお願い。WindowSprite と同じ絵を、自分の格子へ写す）。左上が (x0, y0)。
    /// 透明な所（丸い角）は put を呼ばない。put には不透明な色が来る
    /// </summary>
    public static void StampWindow(Action<int, int, Color32> put, int w, int h, MacLook look, Color32? edge = null, int toolbarH = 0, bool titleBar = true, bool active = true, int x0 = 0, int y0 = 0)
    {
        if (put == null) return;
        w = Mathf.Clamp(w, 8, 512);
        h = Mathf.Clamp(h, 8, 512);
        var c = new MacCanvas(w, h);
        DrawWindow(c, w, h, look, edge.HasValue ? (Color)edge.Value : Edge(look), toolbarH, titleBar, active, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 p = c.Get(x, y);
                if (p.a > 0) put(x0 + x, y0 + y, p);
            }
    }

    static void DrawWindow(MacCanvas c, int w, int h, MacLook look, Color edge, int toolbarH, bool titleBar, bool active, bool redOnly)
    {
        int r = CornerFor(w);
        c.RoundRect(0, 0, w, h, r, edge);
        c.RoundRect(1, 1, w - 2, h - 2, Mathf.Max(0, r - 1), WindowBg(look));
        int th = TitleHeightFor(h);
        StampTitleBar(c.Put, w, look, titleBar, h, edge, active, redOnly);
        if (toolbarH > 0)
        {
            // ツールバー：タイトルバーと同じ色の帯。下の線を、帯の下へ移す
            Color t = TitleBg(look);
            for (int y = th + 1; y <= th + toolbarH; y++)
                for (int x = 1; x < w - 1; x++) c.Set(x, y, t);
            for (int x = 1; x < w - 1; x++)
            {
                c.Set(x, th + toolbarH + 1, t);
                c.Put(x, th + toolbarH + 1, TitleLine(look));
            }
        }
    }

    /// <summary>影の形（白。色は ShadowColor で塗る）。ウィンドウと同じ角</summary>
    public static Sprite ShadowSprite(int w, int h, int corner = -1)
    {
        int r = corner >= 0 ? corner : CornerFor(w);
        string key = $"MacShadow_{w}x{h}_{r}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        c.RoundRect(0, 0, w, h, r, Color.white);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>角の丸い四角（白。色は sr.color で塗る）</summary>
    public static Sprite RoundRectSprite(int w, int h, int corner)
    {
        string key = $"MacRR_{w}x{h}_{corner}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        c.RoundRect(0, 0, w, h, corner, Color.white);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>ダイアログの中の置き場所（ドット。y は上から）</summary>
    public struct AlertLayout
    {
        public RectInt Icon;
        /// <summary>題名の字の左の端と、行のまん中</summary>
        public Vector2Int Title;
        public RectInt[] BodyBars;
        /// <summary>ボタン（左から。いちばん右が既定のボタン）</summary>
        public RectInt[] Buttons;
    }

    /// <summary>ダイアログの置き場所を決める（AlertSprite と PlaceAlert が使う。ほかの担当が字やアイコンを重ねるのにも使える）</summary>
    public static AlertLayout LayoutAlert(int w, int h, int buttons, int[] buttonWidths = null)
    {
        var l = new AlertLayout();
        int top = HeaderH(h) + 3;
        int icon = Mathf.Clamp(h - top - 4 - ButtonH, 6, IconPx);
        l.Icon = new RectInt(5, top, icon, icon);
        int tx = l.Icon.xMax + 5;
        l.Title = new Vector2Int(tx, top + 4);
        int textW = Mathf.Max(4, w - tx - 5);
        l.BodyBars = new[]
        {
            new RectInt(tx, top + 10, Mathf.RoundToInt(textW * 0.75f), 1),
            new RectInt(tx, top + 13, Mathf.RoundToInt(textW * 0.5f), 1),
        };
        buttons = Mathf.Clamp(buttons, 0, 3);
        l.Buttons = new RectInt[buttons];
        bool text = buttonWidths != null;
        int bh = text ? ButtonTextH : ButtonH;
        int by = h - 3 - bh;
        int x = w - 5;
        int avail = Mathf.Max(6, w - tx - 5);
        int each = Mathf.Clamp((avail - (buttons - 1) * 3) / Mathf.Max(1, buttons), 6, 30);
        for (int i = buttons - 1; i >= 0; i--)
        {
            int bw = text && i < buttonWidths.Length ? buttonWidths[i] : each;
            l.Buttons[i] = new RectInt(x - bw, by, bw, bh);
            x -= bw + 3;
        }
        return l;
    }

    /// <summary>
    /// ダイアログの絵（アイコンと字は後から重ねる）。タイトルバーは信号機だけ（赤だけ色つき）。
    /// buttons はボタンの数（いちばん右が青）。caution なら、アイコンの右下に黄色い三角（アイコンを重ねるなら false にして、CautionTriangle を上に重ねる）
    /// </summary>
    public static Sprite AlertSprite(int w, int h, MacLook look, Color32? edge, bool caution, int buttons)
        => AlertSpriteCore(w, h, look, edge, caution, buttons, null, true);

    static Sprite AlertSpriteCore(int w, int h, MacLook look, Color32? edge, bool caution, int buttons, int[] buttonWidths, bool bodyBars)
    {
        w = Mathf.Clamp(w, 24, 512);
        h = Mathf.Clamp(h, 20, 512);
        string bws = buttonWidths != null ? string.Join("-", buttonWidths) : "n";
        string key = $"MacAlert_{w}x{h}_{look}_{Key(edge)}_{caution}_{buttons}_{bws}_{bodyBars}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        DrawWindow(c, w, h, look, edge.HasValue ? (Color)edge.Value : Edge(look), 0, false, true, true);
        AlertLayout l = LayoutAlert(w, h, buttons, buttonWidths);
        if (bodyBars)
        {
            foreach (var b in l.BodyBars) c.Fill(b.x, b.y, b.width, b.height, SubText);
        }
        for (int i = 0; i < l.Buttons.Length; i++)
        {
            RectInt b = l.Buttons[i];
            bool def = i == l.Buttons.Length - 1;
            DrawButton(c, b, look, def, buttonWidths == null);
        }
        if (caution)
        {
            RectInt ic = l.Icon;
            StampCaution(c.Put, ic.xMax - 6, ic.yMax - 6);
        }
        return Cache(key, c.ToSprite(key));
    }

    static void DrawButton(MacCanvas c, RectInt b, MacLook look, bool def, bool placeholder)
    {
        if (def) c.RoundRect(b.x, b.y, b.width, b.height, ButtonCorner, ButtonDefault);
        else
        {
            c.RoundRect(b.x, b.y, b.width, b.height, ButtonCorner, ButtonEdge(look));
            c.RoundRect(b.x + 1, b.y + 1, b.width - 2, b.height - 2, 0, ButtonBg(look));
        }
        if (!placeholder) return;
        // 字の代わりの棒（白いボタンは灰、青いボタンは白）
        int bw = Mathf.Max(2, b.width / 2);
        c.Fill(b.x + (b.width - bw) / 2, b.y + b.height / 2, bw, 1, def ? Color.white : (look == MacLook.Dark ? Light4 : SubText));
    }

    /// <summary>通知の絵（角の丸い板。ふちは edge があるときだけ。右上に時刻の代わりの棒）。アイコンと字は後から重ねる</summary>
    public static Sprite BannerSprite(int w, int h, MacLook look, Color32? edge = null)
    {
        w = Mathf.Clamp(w, 6, 512);
        h = Mathf.Clamp(h, 4, 256);
        string key = $"MacBanner_{w}x{h}_{look}_{Key(edge)}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        int r = h <= 12 ? BannerCornerSmall : BannerCorner;
        if (edge.HasValue)
        {
            c.RoundRect(0, 0, w, h, r, edge.Value);
            c.RoundRect(1, 1, w - 2, h - 2, Mathf.Max(0, r - 1), BannerBg(look));
        }
        else c.RoundRect(0, 0, w, h, r, BannerBg(look));
        // 時刻の代わりの棒（7×1。小さい通知は 3×1）
        int tw = h <= 12 ? 3 : 7;
        int pad = h <= 12 ? 2 : 4;
        if (w > tw + pad * 2 + 8) c.Fill(w - pad - tw, pad, tw, 1, look == MacLook.Dark ? Gray2 : Light5);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>
    /// キー（9×9。DevArt.Key と同じ見た目）。記号は '⌘'（'*' でも可）'⌥'（'~'）'⇧'（'+'）'⌃'（'^'）'⏎'（'\r'）'⎋'（'\x1b'）'⌫'（'\b'）。
    /// 英数字は 3×5 の字
    /// </summary>
    public static Sprite KeySprite(char glyph)
    {
        string key = $"MacKey_{(int)glyph}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(KeyPx, KeyPx);
        c.Fill(1, 0, 7, 8, KeyFace);
        c.Fill(0, 1, 9, 6, KeyFace);
        c.Fill(1, 8, 7, 1, KeyShade);
        c.Set(0, 7, KeyShade);
        c.Set(8, 7, KeyShade);
        string[] g = KeyGlyph(glyph);
        if (g != null) c.Stamp(g, 2, 1, KeyInk);
        else
        {
            var grid = new BrowserDevGrid(3, 5);
            BrowserDevFont.Stamp(grid, char.ToUpperInvariant(glyph).ToString(), 0, 0, 'D');
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 3; x++)
                    if (grid.Get(x, y) != '.') c.Set(3 + x, 1 + y, KeyInk);
        }
        return Cache(key, c.ToSprite(key));
    }

    static string[] KeyGlyph(char g)
    {
        switch (g)
        {
            case '*': case '⌘': return new[] { "##.##", "#.#.#", ".###.", "#.#.#", "##.##" };   // ⌘
            case '~': case '⌥': return new[] { "##.##", ".#...", "..#..", "...#.", "...##" };   // ⌥
            case '+': case '⇧': return new[] { "..#..", ".#.#.", "##.##", ".#.#.", ".###." };   // ⇧
            case '^': case '⌃': return new[] { ".....", "..#..", ".#.#.", "#...#", "....." };   // ⌃
            case '\r': case '⏎': return new[] { "....#", "....#", ".#..#", "#####", ".#..." }; // ⏎
            case '\x1b': case '⎋': return new[] { "###..", "##...", "#.#..", "...#.", "....#" }; // ⎋
            case '\b': case '⌫': return new[] { ".####", "#.#.#", "##.##", "#.#.#", ".####" };  // ⌫
            default: return null;
        }
    }

    /// <summary>虹色の待ちカーソル（16ドット。8コマで1回転。0.4秒で1回転＝20コマ/秒）</summary>
    public static Sprite[] BeachBall()
    {
        if (_beachBall != null && _beachBall[0] != null) return _beachBall;
        var palette = new Dictionary<char, Color32>();
        for (int i = 0; i < 6; i++) palette[(char)('0' + i)] = BeachBallColors[i];
        palette['H'] = Hex("FFFFFFCC"); // 光
        palette['E'] = Hex("2C2C2E");   // ふち
        const int n = 16;
        _beachBall = new Sprite[8];
        for (int f = 0; f < 8; f++)
        {
            float rot = f * Mathf.PI * 2f / 8f;
            var rows = new string[n];
            for (int y = 0; y < n; y++)
            {
                var line = new char[n];
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = n * 0.5f - (y + 0.5f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > 7.6f) { line[x] = '.'; continue; }
                    if (dist > 6.8f) { line[x] = 'E'; continue; }
                    if (dx < -1.5f && dy > 2.5f && dist < 5.2f && dist > 3.6f) { line[x] = 'H'; continue; }
                    float a = Mathf.Atan2(dy, dx) - rot;
                    a = Mathf.Repeat(a, Mathf.PI * 2f);
                    line[x] = (char)('0' + Mathf.Clamp(Mathf.FloorToInt(a / (Mathf.PI * 2f) * 6f), 0, 5));
                }
                rows[y] = new string(line);
            }
            _beachBall[f] = RuntimeSprites.FromAscii($"Fx_BeachBall_{f}", rows, palette);
        }
        return _beachBall;
    }
    static Sprite[] _beachBall;
    /// <summary>待ちカーソルのコマの速さ（0.4秒で1回転）</summary>
    public const float BeachBallFps = 20f;

    /// <summary>
    /// 進行の輪（棒が 8本、輪に並ぶ。1本が明るく、後ろへだんだん暗くなる。8コマで1周）。d は直径（8・16）。
    /// 色は tint（OS の待ちは SubText、敵の予告は Red）
    /// </summary>
    public static Sprite[] Spinner(int d, Color tint)
    {
        d = Mathf.Clamp(d, 6, 48);
        string baseKey = $"MacSpin_{d}_{Key((Color32?)(Color32)tint)}";
        if (Spinners.TryGetValue(baseKey, out Sprite[] cached) && cached[0] != null) return cached;
        var frames = new Sprite[8];
        float c = d * 0.5f;
        float inner = d <= 10 ? c * 0.35f : c * 0.45f;
        float[] fade = { 1f, 0.8f, 0.62f, 0.48f, 0.36f, 0.28f, 0.22f, 0.18f };
        for (int f = 0; f < 8; f++)
        {
            var cv = new MacCanvas(d, d);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > c + 0.1f || dist < inner) continue;
                    float ang = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg; // 上が 0、右回り
                    if (ang < 0f) ang += 360f;
                    float within = Mathf.Repeat(ang + 22.5f, 45f);
                    if (d >= 12 && (within < 8f || within > 37f)) continue; // 棒と棒のすき間
                    int seg = Mathf.FloorToInt(Mathf.Repeat(ang + 22.5f, 360f) / 45f) % 8;
                    int back = (f - seg + 8) % 8;
                    cv.Set(x, y, new Color(tint.r, tint.g, tint.b, tint.a * fade[back]));
                }
            frames[f] = cv.ToSprite($"{baseKey}_{f}");
        }
        Spinners[baseKey] = frames;
        return frames;
    }
    static readonly Dictionary<string, Sprite[]> Spinners = new Dictionary<string, Sprite[]>();
    /// <summary>進行の輪のコマの速さ（8コマで 0.8秒）</summary>
    public const float SpinnerFps = 10f;

    static readonly string[] CautionRows =
    {
        "...YY...",
        "...YY...",
        "..YKKY..",
        "..YKKY..",
        ".YYKKYY.",
        ".YYYYYY.",
        "YYYKKYYY",
        "YYYYYYYY",
    };

    static void StampCaution(Action<int, int, Color32> put, int x, int y)
    {
        Color k = Gray6;
        for (int j = 0; j < CautionRows.Length; j++)
            for (int i = 0; i < CautionRows[j].Length; i++)
            {
                char ch = CautionRows[j][i];
                if (ch == 'Y') put(x + i, y + j, Yellow);
                else if (ch == 'K') put(x + i, y + j, k);
            }
    }

    /// <summary>注意の三角（黄色に黒い「！」。8ドット）。ダイアログのアイコンの右下に重ねる</summary>
    public static Sprite CautionTriangle()
    {
        const string key = "MacCaution";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(8, 8);
        StampCaution(c.Put, 0, 0);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>青いフォルダ（幅 w ドット。高さは 8割）</summary>
    public static Sprite Folder(int w)
    {
        w = Mathf.Clamp(w, 8, 64);
        int h = Mathf.Max(6, Mathf.RoundToInt(w * 0.8f));
        string key = $"MacFolder_{w}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        int tabW = Mathf.Max(3, w * 2 / 5);
        int tabH = Mathf.Max(1, h / 8);
        c.RoundRect(0, 0, tabW, tabH + 2, 1, FolderShade);         // つまみ
        c.RoundRect(0, tabH, w, h - tabH, 1, FolderShade);          // 後ろの面
        int front = tabH + Mathf.Max(2, h / 6);
        c.RoundRect(0, front, w, h - front, 1, FolderBlue);         // 前の面
        c.Fill(1, front, w - 2, 1, FolderShine);                    // 光
        return Cache(key, c.ToSprite(key));
    }

    static readonly string[] ArrowRows =
    {
        "W.........",
        "WW........",
        "WBW.......",
        "WBBW......",
        "WBBBW.....",
        "WBBBBW....",
        "WBBBBBW...",
        "WBBBBBBW..",
        "WBBBBBBBW.",
        "WBBBBBBBBW",
        "WBBBBBWWWW",
        "WBBWBBW...",
        "WBW.WBBW..",
        "WW..WBBW..",
        ".....WW...",
    };

    /// <summary>矢印のカーソル（黒い矢印に白いふち。10×15。軸は左上の先）</summary>
    public static Sprite Arrow()
    {
        const string key = "MacArrow";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(10, 15);
        c.Stamp(ArrowRows, 0, 0, Color.white, 'W');
        c.Stamp(ArrowRows, 0, 0, Gray6, 'B');
        return Cache(key, c.ToSprite(key, new Vector2(0.05f, 0.97f)));
    }

    /// <summary>リンゴの代わりの印（プレイヤーの顔の小さい絵。白。size は 7 か 24）【仮】</summary>
    public static Sprite Mark(int size = 7)
    {
        size = size >= 16 ? 24 : 7;
        string key = $"MacMark_{size}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(size, size);
        if (size == 7)
        {
            c.Stamp(new[]
            {
                ".#####.",
                "#######",
                "##.#.##",
                "#######",
                "#.###.#",
                "##...##",
                ".#####.",
            }, 0, 0, Color.white, '#');
        }
        else
        {
            // 24ドット：角の丸い顔に、目 2つと口
            c.RoundRect(0, 0, 24, 24, 5, Color.white);
            Color hole = new Color(0f, 0f, 0f, 0f);
            c.Fill(7, 7, 3, 5, hole);
            c.Fill(14, 7, 3, 5, hole);
            c.Fill(6, 16, 2, 2, hole);
            c.Fill(16, 16, 2, 2, hole);
            c.Fill(8, 17, 8, 2, hole);
        }
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>メニューバーの右の印（Wi-Fi の扇・電池・スイッチ。7ドット。白。色は塗る）。i＝0 Wi-Fi、1 電池、2 スイッチ</summary>
    public static Sprite StatusIcon(int i)
    {
        i = Mathf.Clamp(i, 0, 2);
        string key = $"MacStatus_{i}";
        if (TryCached(key, out Sprite s)) return s;
        string[][] rows =
        {
            new[] { ".#####.", "#.....#", "..###..", ".#...#.", "...#...", "..###..", "...#..." },
            new[] { ".......", "######.", "#....#.", "#.##.##", "#....#.", "######.", "......." },
            new[] { "..##...", "####...", "..##...", ".......", "...##..", "...####", "...##.." },
        };
        var c = new MacCanvas(7, 7);
        c.Stamp(rows[i], 0, 0, Color.white, '#');
        return Cache(key, c.ToSprite(key));
    }

    // ───────── 色の道具 ─────────

    static Color Hex(string hex) => RuntimeSprites.Hex(hex);

    /// <summary>a の上に b を重ねた色（b の 透明さで混ぜる）</summary>
    public static Color32 Over(Color32 a, Color32 b)
    {
        if (b.a >= 255 || a.a == 0) return b;
        if (b.a == 0) return a;
        float ba = b.a / 255f, aa = a.a / 255f;
        float oa = ba + aa * (1f - ba);
        return new Color32(
            (byte)Mathf.RoundToInt((b.r * ba + a.r * aa * (1f - ba)) / oa),
            (byte)Mathf.RoundToInt((b.g * ba + a.g * aa * (1f - ba)) / oa),
            (byte)Mathf.RoundToInt((b.b * ba + a.b * aa * (1f - ba)) / oa),
            (byte)Mathf.RoundToInt(oa * 255f));
    }

    /// <summary>角を r 段けずった四角の中か（GenericFrameKit.InRound と同じ形）</summary>
    public static bool InRound(int x, int y, int w, int h, int r)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
        return dx + dy >= r;
    }

    static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    static bool TryCached(string key, out Sprite s) => Sprites.TryGetValue(key, out s) && s != null;
    static Sprite Cache(string key, Sprite s)
    {
        Sprites[key] = s;
        return s;
    }
    static string Key(Color32? c) => c.HasValue ? $"{c.Value.r:x2}{c.Value.g:x2}{c.Value.b:x2}{c.Value.a:x2}" : "def";
}

/// <summary>【仮】Mac の部品を描くドットの板（2026-11-19）。y は上から。Put は重ねる（透明さで混ぜる）、Set は置き換える</summary>
public sealed class MacCanvas
{
    public readonly int W, H;
    readonly Color32[] _px;

    public MacCanvas(int w, int h)
    {
        W = Mathf.Max(1, w);
        H = Mathf.Max(1, h);
        _px = new Color32[W * H];
    }

    public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

    public void Put(int x, int y, Color32 c)
    {
        if (!In(x, y)) return;
        int i = y * W + x;
        _px[i] = MacUi.Over(_px[i], c);
    }

    public void Set(int x, int y, Color32 c)
    {
        if (In(x, y)) _px[y * W + x] = c;
    }

    public Color32 Get(int x, int y) => In(x, y) ? _px[y * W + x] : new Color32(0, 0, 0, 0);

    public void Fill(int x, int y, int w, int h, Color32 c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++) Set(x + i, y + j, c);
    }

    /// <summary>角の丸い四角を置き換えて塗る（角を r 段けずる）</summary>
    public void RoundRect(int x, int y, int w, int h, int r, Color32 c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                if (MacUi.InRound(i, j, w, h, r)) Set(x + i, y + j, c);
    }

    /// <summary>文字の並びを貼る（only の字だけ。'\0' なら '.' と ' ' 以外の全部）</summary>
    public void Stamp(string[] rows, int x, int y, Color32 c, char only = '\0')
    {
        for (int j = 0; j < rows.Length; j++)
            for (int i = 0; i < rows[j].Length; i++)
            {
                char ch = rows[j][i];
                if (ch == '.' || ch == ' ') continue;
                if (only != '\0' && ch != only) continue;
                Set(x + i, y + j, c);
            }
    }

    /// <summary>スプライトにする（1マス＝18ドット。点のまま拡大）</summary>
    public Sprite ToSprite(string name, Vector2? pivot = null)
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = $"Runtime_{name}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var flipped = new Color32[_px.Length];
        for (int y = 0; y < H; y++)
            Array.Copy(_px, y * W, flipped, (H - 1 - y) * W, W); // テクスチャは下の行から
        tex.SetPixels32(flipped);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, W, H), pivot ?? new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
