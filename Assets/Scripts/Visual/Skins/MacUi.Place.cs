using System;
using UnityEngine;

// 【仮】Mac の部品を、ゲームの世界に置く（2026-11-19。docs/mac-look-design.md 1-10 の (b)）。
// ・置いた物はどれも、親の子。位置は親からの位置（マス）。大きさはドット
// ・影は、別の絵で下へ 2ドットずらして、order - 1 に置く（判定の外・下にだけ見える）

public static partial class MacUi
{
    /// <summary>
    /// ウィンドウを置く（sizePx はドット。判定のマス×18 にすると、伸ばさずに済む）。
    /// edge を入れると、ふちだけその色。title を入れると（幅 80ドット以上）、題名を字で出す。
    /// 子に "MacRedLight"（押した赤い丸。ふだんは見えない）を置く。CloseAnim が使う
    /// </summary>
    public static Transform PlaceWindow(Transform parent, Vector2 pos, Vector2 sizePx, MacLook look, Color? edge, int order, string title = null, int toolbarH = 0)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(sizePx.x)), h = Mathf.Max(8, Mathf.RoundToInt(sizePx.y));
        Transform root = Node(parent, "MacWindow", pos);
        bool text = !string.IsNullOrEmpty(title) && w >= BigWindowW && FitsChars(title, w - 30);
        SpriteRenderer body = RuntimeSprites.Show(root, WindowSprite(w, h, look, ToC32(edge), toolbarH, !text), Vector2.zero, order, 1f, "Body");
        body.color = Color.white;
        PlaceShadow(root, w, h, CornerFor(w), look, order - 1);
        if (text)
        {
            int th = TitleHeightFor(h);
            Vector2 c = DotToLocal(w, h, w * 0.5f, 1f + th * 0.5f);
            Label(root, title, c, TextH, Ink(look), order + 1, true);
        }
        PlaceRedLight(root, w, h, order + 1);
        return root;
    }

    /// <summary>
    /// 通知を置く（角の丸い板・左にアイコン・右に題名 1行・右上に時刻の代わりの棒）。
    /// edge は敵の攻撃のときだけ（MacUi.Red）。無ければ影だけ。icon が無ければ、アプリの色の代わりに灰色の角の丸い四角
    /// </summary>
    public static Transform PlaceBanner(Transform parent, Vector2 pos, Vector2 sizePx, Sprite icon, string title, Color? edge, int order)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(sizePx.x)), h = Mathf.Max(6, Mathf.RoundToInt(sizePx.y));
        MacLook look = BannerLook;
        Transform root = Node(parent, "MacBanner", pos);
        RuntimeSprites.Show(root, BannerSprite(w, h, look, ToC32(edge)), Vector2.zero, order, 1f, "Body");
        bool small = h <= 12;
        PlaceShadow(root, w, h, small ? BannerCornerSmall : BannerCorner, look, order - 1, small ? 1 : ShadowY);
        int iconPx = small ? Mathf.Clamp(h - 4, 5, 7) : Mathf.Min(IconPx, h - 6);
        int pad = small ? 2 : 5;
        Vector2 iconPos = DotToLocal(w, h, pad + iconPx * 0.5f, h * 0.5f);
        PlaceIcon(root, icon, iconPos, iconPx, order + 1, small);
        if (!string.IsNullOrEmpty(title))
        {
            float x0 = pad + iconPx + (small ? 2 : 5);
            float maxW = w - x0 - (small ? 2 : 4);
            if (FitsChars(title, maxW))
                Label(root, title, DotToLocal(w, h, x0, h * 0.5f + 0.5f), TextH, Ink(look), order + 1, true, TextAnchor.MiddleLeft);
            else
                Bar(root, DotToLocal(w, h, x0 + Mathf.Min(maxW, 24f) * 0.5f, h * 0.5f), Mathf.Max(2f, Mathf.Min(maxW, 24f)), 2f, look == MacLook.Dark ? Color.white : Gray6, order + 1);
        }
        return root;
    }

    /// <summary>
    /// ダイアログを置く（幅は字に合わせて 9マスまで）。左にアプリのアイコン（caution なら右下に黄色い三角）、右に太字の題名、
    /// その下に灰色の棒、右下にボタン（いちばん右が青）。buttons の字は、幅が足りるときだけ出す（足りなければ棒）
    /// </summary>
    public static Transform PlaceAlert(Transform parent, Vector2 pos, Sprite icon, string title, string[] buttons, int order, MacLook look = MacLook.Light, bool caution = true, Color? edge = null)
    {
        title = title ?? "";
        if (title.Length > AlertTitleMaxChars) title = title.Substring(0, AlertTitleMaxChars);
        int n = buttons != null ? Mathf.Clamp(buttons.Length, 0, 3) : 0;
        string[] names = new string[n];
        for (int i = 0; i < n; i++) names[i] = buttons[buttons.Length - n + i] ?? ""; // 3つより多ければ右の3つ

        // 幅：アイコンの列 26 ＋ 題名 ＋ 5。ボタンの列が広ければ、それに合わせる
        int titleW = Mathf.CeilToInt(title.Length * CharPx);
        int[] bws = new int[n];
        int buttonsW = 0;
        for (int i = 0; i < n; i++)
        {
            bws[i] = Mathf.Max(ButtonTextMinW, Mathf.CeilToInt(names[i].Length * CharPx) + 6);
            buttonsW += bws[i] + (i > 0 ? 3 : 0);
        }
        int w = Mathf.Clamp(26 + Mathf.Max(titleW, buttonsW) + 5, 72, AlertMaxW);
        bool buttonText = 26 + buttonsW + 5 <= AlertMaxW;
        if (!buttonText)
        {
            // 入らなければ、右の 2つだけにして、もう一度
            if (n == 3) return PlaceAlert(parent, pos, icon, title, new[] { names[1], names[2] }, order, look, caution, edge);
            bws = null;
        }
        int h = HeaderH(40) + 3 + IconPx + 3 + ButtonTextH + 3;

        Transform root = Node(parent, "MacAlert", pos);
        Sprite body = AlertSpriteCore(w, h, look, ToC32(edge), false, n, bws, true);
        RuntimeSprites.Show(root, body, Vector2.zero, order, 1f, "Body");
        PlaceShadow(root, w, h, CornerFor(w), look, order - 1);
        AlertLayout l = LayoutAlert(w, h, n, bws);

        Vector2 iconPos = DotToLocal(w, h, l.Icon.x + l.Icon.width * 0.5f, l.Icon.y + l.Icon.height * 0.5f);
        PlaceIcon(root, icon, iconPos, l.Icon.width, order + 1, false);
        if (caution)
        {
            Vector2 tri = DotToLocal(w, h, l.Icon.xMax - 6 + 4f, l.Icon.yMax - 6 + 4f);
            RuntimeSprites.Show(root, CautionTriangle(), tri, order + 2, 1f, "Caution");
        }
        if (title.Length > 0)
        {
            bool fits = FitsChars(title, w - l.Title.x - 4);
            if (fits) Label(root, title, DotToLocal(w, h, l.Title.x, l.Title.y), TextH, Ink(look), order + 1, true, TextAnchor.MiddleLeft);
            else Bar(root, DotToLocal(w, h, l.Title.x + 20f, l.Title.y), 40f, 2f, Ink(look), order + 1);
        }
        if (bws != null)
        {
            for (int i = 0; i < n; i++)
            {
                RectInt b = l.Buttons[i];
                bool def = i == n - 1;
                Color ink = def ? Color.white : Ink(look);
                Label(root, names[i], DotToLocal(w, h, b.x + b.width * 0.5f, b.y + b.height * 0.5f + 0.5f), TextH, ink, order + 1, false);
            }
        }
        PlaceRedLight(root, w, h, order + 1);
        return root;
    }

    /// <summary>
    /// 強制終了のウィンドウを置く（題名 Force Quit・行に「アプリのアイコン＋赤い字 Not Responding」（うすい青の選択）・右下に青いボタン（棒）。
    /// 上に虹色の待ちカーソル（回る））。幅は 9マスまで。shortcutKeys なら、下にキー ⌥ ⌘ ⎋ を並べる（優先度 C の飾り）
    /// </summary>
    public static Transform PlaceForceQuit(Transform parent, Vector2 pos, Sprite icon, int order, MacLook look = MacLook.Light, bool shortcutKeys = false)
    {
        const string rowText = "Not Responding";
        int rowIcon = 12;
        int w = Mathf.Min(AlertMaxW, 6 + rowIcon + 4 + Mathf.CeilToInt(rowText.Length * CharPx) + 6);
        int h = HeaderH(40) + 3 + 16 + 4 + ButtonH + 3;
        Transform root = Node(parent, "MacForceQuit", pos);
        RuntimeSprites.Show(root, ForceQuitSprite(w, h, look), Vector2.zero, order, 1f, "Body");
        PlaceShadow(root, w, h, CornerFor(w), look, order - 1);
        int th = TitleHeightFor(h);
        Label(root, "Force Quit", DotToLocal(w, h, w * 0.5f, 1f + th * 0.5f + 0.5f), TextH, Ink(look), order + 1, true);
        int rowY = HeaderH(h) + 3;
        PlaceIcon(root, icon, DotToLocal(w, h, 6 + rowIcon * 0.5f, rowY + 8f), rowIcon, order + 1, false);
        Label(root, rowText, DotToLocal(w, h, 6 + rowIcon + 4, rowY + 8.5f), TextH, Red, order + 1, false, TextAnchor.MiddleLeft);
        // 上に待ちカーソル
        Vector2 ball = DotToLocal(w, h, w - 14f, -9f);
        SpriteRenderer bb = RuntimeSprites.Show(root, BeachBall()[0], ball, order + 2, 1f, "BeachBall");
        Animate(bb, BeachBall(), BeachBallFps);
        if (shortcutKeys)
        {
            char[] keys = { '\u2325', '\u2318', '\u238B' }; // ⌥ ⌘ ⎋
            for (int i = 0; i < keys.Length; i++)
                RuntimeSprites.Show(root, KeySprite(keys[i]), DotToLocal(w, h, w * 0.5f + (i - 1) * (KeyPx + 2), h + 3f + KeyPx * 0.5f), order, 1f, "Key");
        }
        PlaceRedLight(root, w, h, order + 1);
        return root;
    }

    static Sprite ForceQuitSprite(int w, int h, MacLook look)
    {
        string key = $"MacForceQuit_{w}x{h}_{look}";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(w, h);
        DrawWindow(c, w, h, look, Edge(look), 0, false, true, false);
        int rowY = HeaderH(h) + 3;
        // 行（うすい青の選択。1ドットの枠、中は 2割）
        Color sel = Selection;
        Color selFill = new Color(sel.r, sel.g, sel.b, 0.2f);
        for (int y = rowY; y < rowY + 16; y++)
            for (int x = 3; x < w - 3; x++)
            {
                bool frame = y == rowY || y == rowY + 15 || x == 3 || x == w - 4;
                c.Put(x, y, frame ? sel : selFill);
            }
        // 右下に青いボタン（棒）
        var b = new RectInt(w - 5 - 26, h - 3 - ButtonH, 26, ButtonH);
        DrawButton(c, b, look, true, true);
        return Cache(key, c.ToSprite(key));
    }

    /// <summary>
    /// Mac の字（Press Start 2P。英字だけでなければ DotGothic16）。height は 0.4マス以上。bold なら 1ドット右へずらした同じ字を重ねる（子 "Bold"）。
    /// 色を後から変えるときは SetLabelColor
    /// </summary>
    public static TextMesh Label(Transform parent, string text, Vector2 pos, float height, Color color, int order, bool bold, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        bool en = IsAscii(text);
        TextMesh tm = WorldText.Create(parent, text, Mathf.Max(TextH, height), color, order, en, anchor, "MacLabel");
        tm.transform.localPosition = new Vector3(pos.x, pos.y, -0.001f);
        if (bold)
        {
            TextMesh b = WorldText.Create(tm.transform, text, Mathf.Max(TextH, height), color, order, en, anchor, "Bold");
            b.transform.localPosition = new Vector3(Px, 0f, 0f);
        }
        return tm;
    }

    /// <summary>Label の色を変える（太字の重ねも）</summary>
    public static void SetLabelColor(TextMesh tm, Color color)
    {
        if (tm == null) return;
        foreach (var t in tm.GetComponentsInChildren<TextMesh>(true)) WorldText.SetColor(t, color);
    }

    /// <summary>
    /// 閉じる動き：左上の赤い丸が 0.05秒へこむ → ウィンドウがまん中へ縮みながら消える（0.1秒）→ done。
    /// done が無ければ、ウィンドウを消す。MacUi で置いたウィンドウでなければ、redLightLocal（赤い丸のまん中。親からの位置）を渡す
    /// </summary>
    public static void CloseAnim(Transform window, Action done, Vector2? redLightLocal = null)
    {
        if (window == null)
        {
            done?.Invoke();
            return;
        }
        var a = window.gameObject.GetComponent<MacCloseAnim>();
        if (a == null) a = window.gameObject.AddComponent<MacCloseAnim>();
        a.Begin(done, redLightLocal);
    }

    /// <summary>
    /// 進みのバーを置く（両端を 1ドット丸める。地は ProgressTrough、中は青）。返した物の Value（0〜1）で進める
    /// </summary>
    public static MacProgress PlaceProgress(Transform parent, Vector2 pos, int wPx, int hPx, MacLook look, int order, Color? fill = null)
    {
        Transform root = Node(parent, "MacProgress", pos);
        var p = root.gameObject.AddComponent<MacProgress>();
        p.Build(Mathf.Max(4, wPx), Mathf.Clamp(hPx, 2, 8), look, order, fill ?? Blue);
        return p;
    }

    /// <summary>コマ送りの絵を回し続ける（止まっている間も回る）</summary>
    public static void Animate(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        if (sr == null || frames == null || frames.Length == 0) return;
        var a = sr.gameObject.GetComponent<MacFrames>();
        if (a == null) a = sr.gameObject.AddComponent<MacFrames>();
        a.Set(sr, frames, fps);
    }

    // ───────── 置く道具 ─────────

    /// <summary>ドットの位置（左上が 0、y は下へ）を、絵のまん中からの位置（マス）にする</summary>
    public static Vector2 DotToLocal(int w, int h, float x, float y) => new Vector2((x - w * 0.5f) * Px, (h * 0.5f - y) * Px);

    /// <summary>字が maxPx ドットに入るか（Press Start 2P の字の高さ 0.4マス）</summary>
    public static bool FitsChars(string text, float maxPx) => text != null && text.Length * CharPx <= maxPx;

    static Transform Node(Transform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.localPosition = pos;
        return go.transform;
    }

    static void PlaceShadow(Transform root, int w, int h, int corner, MacLook look, int order, int dy = ShadowY)
    {
        SpriteRenderer sh = RuntimeSprites.Show(root, ShadowSprite(w, h, corner), new Vector2(0f, -dy * Px), order, 1f, "Shadow");
        sh.color = ShadowColor(look);
    }

    static void PlaceRedLight(Transform root, int w, int h, int order)
    {
        int th = TitleHeightFor(h);
        Vector2 p = w < TinyWindowW ? DotToLocal(w, h, 2.5f, 1f + (th - 1) / 2 + 0.5f) : DotToLocal(w, h, LightX + 1.5f, 1f + (th - LightD) / 2 + 1.5f);
        PlaceRedLightAt(root, p, w < TinyWindowW ? 1 : LightD, order);
    }

    internal static SpriteRenderer PlaceRedLightAt(Transform root, Vector2 local, int d, int order)
    {
        Sprite s = d <= 1 ? FxArt.Square(1) : PressedLight();
        SpriteRenderer sr = RuntimeSprites.Show(root, s, local, order, 1f, "MacRedLight");
        sr.color = LightRPressed;
        sr.enabled = false;
        return sr;
    }

    static Sprite PressedLight()
    {
        const string key = "MacLightPressed";
        if (TryCached(key, out Sprite s)) return s;
        var c = new MacCanvas(3, 3);
        StampDot3(c.Put, 0, 0, Color.white);
        return Cache(key, c.ToSprite(key));
    }

    static void PlaceIcon(Transform root, Sprite icon, Vector2 local, int px, int order, bool small)
    {
        if (icon != null)
        {
            float native = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
            float s = native > 0.0001f ? px * Px / native : 1f;
            RuntimeSprites.Show(root, icon, local, order, s, "Icon");
            return;
        }
        // アイコンが無ければ、灰色の角の丸い四角
        SpriteRenderer sr = RuntimeSprites.Show(root, RoundRectSprite(px, px, small ? 1 : 3), local, order, 1f, "Icon");
        sr.color = Gray1;
    }

    static void Bar(Transform root, Vector2 local, float wPx, float hPx, Color c, int order)
    {
        SpriteRenderer sr = RuntimeSprites.Show(root, FxArt.Square(1), local, order, 1f, "Bar");
        sr.transform.localScale = new Vector3(wPx, hPx, 1f);
        sr.color = c;
    }

    static Color32? ToC32(Color? c) => c.HasValue ? (Color32?)(Color32)c.Value : null;

    static bool IsAscii(string s)
    {
        if (s == null) return true;
        foreach (char ch in s) if (ch > 126) return false;
        return true;
    }
}

/// <summary>【仮】閉じる動き（MacUi.CloseAnim。2026-11-19）。止まっている間（ヒットストップ）は止まる</summary>
public class MacCloseAnim : MonoBehaviour
{
    const float PressTime = 0.05f, ShrinkTime = 0.1f;
    Action _done;
    float _age;
    bool _running;
    Vector3 _scale;
    SpriteRenderer _red;
    SpriteRenderer[] _srs;
    float[] _alpha;
    TextMesh[] _tms;
    Color[] _tmBase;

    public void Begin(Action done, Vector2? redLightLocal)
    {
        if (_running) return;
        _done = done;
        _running = true;
        _age = 0f;
        _scale = transform.localScale;
        Transform red = FindDeep(transform, "MacRedLight");
        if (red != null) _red = red.GetComponent<SpriteRenderer>();
        if (_red == null && redLightLocal.HasValue)
        {
            int order = 0;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) order = Mathf.Max(order, sr.sortingOrder);
            _red = MacUi.PlaceRedLightAt(transform, redLightLocal.Value, MacUi.LightD, order + 1);
            // 親の大きさで割って、3ドットに見せる
            Vector3 ls = transform.lossyScale;
            _red.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
        }
        if (_red != null) _red.enabled = true;
        _srs = GetComponentsInChildren<SpriteRenderer>(true);
        _alpha = new float[_srs.Length];
        for (int i = 0; i < _srs.Length; i++) _alpha[i] = _srs[i].color.a;
        _tms = GetComponentsInChildren<TextMesh>(true);
        _tmBase = new Color[_tms.Length];
        for (int i = 0; i < _tms.Length; i++) _tmBase[i] = FxKit.TextColor(_tms[i]);
    }

    void Update()
    {
        if (!_running) return;
        _age += Time.deltaTime;
        if (_age < PressTime) return;
        float k = Mathf.Clamp01((_age - PressTime) / ShrinkTime);
        float s = Mathf.Lerp(1f, 0.1f, k * k);
        transform.localScale = new Vector3(_scale.x * s, _scale.y * s, _scale.z);
        float a = 1f - k;
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srs[i].color;
            c.a = _alpha[i] * a;
            _srs[i].color = c;
        }
        for (int i = 0; i < _tms.Length; i++)
        {
            if (_tms[i] == null) continue;
            Color c = _tmBase[i];
            c.a *= a;
            WorldText.SetColor(_tms[i], c);
        }
        if (k >= 1f)
        {
            _running = false;
            Action d = _done;
            _done = null;
            if (d != null) d();
            else Destroy(gameObject);
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform f = FindDeep(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}

/// <summary>【仮】進みのバー（MacUi.PlaceProgress。2026-11-19）。Value を 0〜1 で入れる</summary>
public class MacProgress : MonoBehaviour
{
    SpriteRenderer _fill;
    int _w, _h;
    float _value;

    internal void Build(int w, int h, MacLook look, int order, Color fill)
    {
        _w = w;
        _h = h;
        SpriteRenderer trough = RuntimeSprites.Show(transform, MacUi.RoundRectSprite(w, h, 1), Vector2.zero, order, 1f, "Trough");
        trough.color = MacUi.ProgressTrough(look);
        _fill = RuntimeSprites.Show(transform, MacUi.RoundRectSprite(w, h, 1), Vector2.zero, order + 1, 1f, "Fill");
        _fill.color = fill;
        Value = 0f;
    }

    /// <summary>進み（0〜1）。左から伸びる（ドットにそろえる）</summary>
    public float Value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp01(value);
            if (_fill == null) return;
            int px = Mathf.RoundToInt(_w * _value);
            _fill.enabled = px > 0;
            if (px <= 0) return;
            _fill.sprite = MacUi.RoundRectSprite(Mathf.Max(2, px), _h, px >= 3 ? 1 : 0);
            _fill.transform.localPosition = new Vector2((px - _w) * 0.5f * MacUi.Px, 0f);
        }
    }
}

/// <summary>【仮】コマ送りを回し続ける（MacUi.Animate。止まっている間も回る）</summary>
public class MacFrames : MonoBehaviour
{
    SpriteRenderer _sr;
    Sprite[] _frames;
    float _fps;
    float _t;

    public void Set(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        _sr = sr;
        _frames = frames;
        _fps = fps;
    }

    void Update()
    {
        if (_sr == null || _frames == null || _frames.Length == 0) return;
        _t += Time.unscaledDeltaTime;
        _sr.sprite = _frames[Mathf.FloorToInt(_t * _fps) % _frames.Length];
    }
}
