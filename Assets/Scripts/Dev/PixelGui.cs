using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】画面（OnGUI）を、デザイン担当の画面イメージ（2026-11-18）の色と部品で描くための道具。UI 担当の本番画面ができたら消す。
/// 画面を整数倍に拡大した「仮想の画面」（640×360 以上）で配置する。文字はドットのフォント（日本語 DotGothic16・英字 Press Start 2P）を
/// その大きさで描いてから点のまま拡大するので、ゲームの絵と同じドットの粗さになる。
/// 使い方：OnGUI の最初に Begin()、最後に End()。座標は Width・Height（仮想の画面の大きさ）を使う。
/// 色はイメージから取った色（イメージの PNG は Mac の画面の色空間なので、sRGB に直して取った）。
/// </summary>
public static class PixelGui
{
    // ───────── 色（画面イメージから） ─────────

    /// <summary>文字・暗い面（濃い紫）</summary>
    public static readonly Color Ink = new Color32(0x1e, 0x08, 0x30, 0xff);
    /// <summary>ウイルス（マゼンタ）。ロゴの影・ゲームオーバー・ボスが出るまでのゲージ</summary>
    public static readonly Color Magenta = new Color32(0xed, 0x38, 0xff, 0xff);
    /// <summary>小さな見出し（USAGE_SCAN.EXE・固有能力）</summary>
    public static readonly Color LabelPurple = new Color32(0x6a, 0x2a, 0x8c, 0xff);
    /// <summary>うまくいったときの小さな見出し（SCAN_REPORT.TXT）</summary>
    public static readonly Color LabelGreen = new Color32(0x1e, 0x6c, 0x13, 0xff);
    public static readonly Color Orange = new Color32(0xdf, 0x71, 0x26, 0xff);
    public static readonly Color OrangeHover = new Color32(0xef, 0x8a, 0x43, 0xff);
    public static readonly Color DarkRed = new Color32(0x97, 0x00, 0x00, 0xff);
    /// <summary>ウィンドウの中</summary>
    public static readonly Color Panel = new Color32(0xdc, 0xdc, 0xdc, 0xff);
    /// <summary>カード（選んでいない）</summary>
    public static readonly Color Card = new Color32(0xf4, 0xf4, 0xf4, 0xff);
    public static readonly Color White = Color.white;
    /// <summary>ウィンドウの枠・下のバー</summary>
    public static readonly Color Frame = new Color32(0x79, 0x79, 0x79, 0xff);
    public static readonly Color FrameDark = new Color32(0x51, 0x51, 0x51, 0xff);
    public static readonly Color BodyText = new Color32(0x33, 0x33, 0x33, 0xff);
    public static readonly Color SubText = new Color32(0x55, 0x55, 0x55, 0xff);
    /// <summary>空のメーター・点線</summary>
    public static readonly Color Muted = new Color32(0xc8, 0xc8, 0xc8, 0xff);
    /// <summary>暗い面の上の小さな文字（防衛ランク）</summary>
    public static readonly Color Peach = new Color32(0xff, 0xc8, 0x9b, 0xff);
    /// <summary>ゲームオーバーの文字</summary>
    public static readonly Color Lavender = new Color32(0xf0, 0xd8, 0xff, 0xff);
    /// <summary>ゲームオーバーの2つ目のボタンの縁</summary>
    public static readonly Color Lilac = new Color32(0xb3, 0x76, 0xe9, 0xff);
    public static readonly Color HpGreen = new Color32(0x42, 0xc1, 0x0f, 0xff);
    public static readonly Color HpOff = new Color32(0x2a, 0x2a, 0x2a, 0xff);
    /// <summary>能力の枠の中</summary>
    public static readonly Color SlotBg = new Color32(0x1e, 0x1e, 0x2a, 0xff);

    // ───────── 状態 ─────────

    static Matrix4x4 _saved;
    static Texture2D _white;
    static readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();
    static GUIStyle _panel, _button, _title, _label, _small;

    /// <summary>拡大率（整数）</summary>
    public static float Scale { get; private set; } = 1f;
    public static float Width => Screen.width / Scale;
    public static float Height => Screen.height / Scale;

    public static Font FontJa => GameArt.Instance != null ? GameArt.Instance.FontJa : null;
    public static Font FontEn => GameArt.Instance != null ? GameArt.Instance.FontEn : null;

    // 前からある部品（図鑑など）
    public static GUIStyle Panel9 => _panel;
    public static GUIStyle Button => _button;
    public static GUIStyle Title => _title;
    public static GUIStyle Label => _label;
    public static GUIStyle Small => _small;

    /// <summary>壁紙を画面いっぱいに描いて（drawBackground＝false なら描かずに）、拡大を始める</summary>
    public static void Begin(bool drawBackground = true)
    {
        GameArt art = GameArt.Instance;
        if (drawBackground && art != null && art.Background != null) DrawCover(art.Background);
        // 仮想の画面が 640×360 より小さくならない、いちばん大きな整数倍（画面がそれより小さいときは等倍）
        Scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(Screen.width / 640f, Screen.height / 360f)));
        _saved = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        KeepFontsCrisp();
        EnsureStyles(art);
    }

    public static void End() => GUI.matrix = _saved;

    // フォントの文字の絵は、点のまま拡大する（ぼかすとドットに見えない）。文字が増えると絵が作り直されるので毎回確かめる
    static void KeepFontsCrisp()
    {
        Point(FontJa);
        Point(FontEn);
    }

    static void Point(Font font)
    {
        if (font == null || font.material == null) return;
        Texture tex = font.material.mainTexture;
        if (tex != null && tex.filterMode != FilterMode.Point) tex.filterMode = FilterMode.Point;
    }

    static void EnsureStyles(GameArt art)
    {
        if (_panel != null && _panel.normal.background != null) return;
        _panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(10, 10, 10, 10) };
        _button = new GUIStyle(GUI.skin.button) { border = new RectOffset(4, 4, 4, 4), fontSize = 12, font = FontJa };
        if (art != null)
        {
            if (art.WindowFrame != null) _panel.normal.background = art.WindowFrame.texture;
            if (art.Button != null) _button.normal.background = art.Button.texture;
            if (art.ButtonHover != null)
            {
                _button.hover.background = art.ButtonHover.texture;
                _button.active.background = art.ButtonHover.texture;
                _button.focused.background = art.Button != null ? art.Button.texture : null;
            }
        }
        _button.normal.textColor = Ink;
        _button.hover.textColor = Orange;
        _button.active.textColor = Orange;
        _button.focused.textColor = Ink;
        _title = Style(true, 16, TextAnchor.MiddleCenter, false);
        _title.normal.textColor = Ink;
        _label = Style(false, 12, TextAnchor.MiddleLeft, false);
        _label.normal.textColor = Ink;
        _small = Style(false, 12, TextAnchor.UpperLeft, true);
        _small.normal.textColor = SubText;
    }

    // ───────── 文字 ─────────

    /// <summary>文字の型（en＝英字のフォント）。色は描くときに入れる</summary>
    public static GUIStyle Style(bool en, int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
    {
        int key = (en ? 1 : 0) | (size << 1) | ((int)anchor << 8) | ((wrap ? 1 : 0) << 12);
        if (_styles.TryGetValue(key, out GUIStyle s) && s.font != null) return s;
        s = new GUIStyle
        {
            font = en ? FontEn : FontJa,
            fontSize = size,
            alignment = anchor,
            wordWrap = wrap,
            clipping = TextClipping.Overflow,
            richText = false,
            padding = new RectOffset(0, 0, 0, 0),
        };
        _styles[key] = s;
        return s;
    }

    /// <summary>日本語の文字（16 か 12 で使う。ほかの大きさはドットが崩れる）</summary>
    public static void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        => Draw(r, text, Style(false, size, anchor, wrap), color);

    /// <summary>英字の見出し（8 の倍数で使う）</summary>
    public static void TextEn(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        => Draw(r, text, Style(true, size, anchor, false), color);

    /// <summary>影つきの文字（ロゴ・ランク）</summary>
    public static void ShadowText(Rect r, string text, GUIStyle style, Color color, Color shadow, float offset)
    {
        Draw(new Rect(r.x + offset, r.y + offset, r.width, r.height), text, style, shadow);
        Draw(r, text, style, color);
    }

    public static void Draw(Rect r, string text, GUIStyle style, Color color)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
        style.normal.textColor = ToGui(color);
        style.Draw(Snap(r), new GUIContent(text), false, false, false, false);
    }

    /// <summary>文字の幅（仮想の画面のドット）</summary>
    public static float Measure(string text, GUIStyle style) => style.CalcSize(new GUIContent(text)).x;

    /// <summary>幅に収まるように、後ろを「…」で切る</summary>
    public static string Fit(string text, GUIStyle style, float width)
    {
        if (string.IsNullOrEmpty(text) || Measure(text, style) <= width) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string t = text.Substring(0, n) + "…";
            if (Measure(t, style) <= width) return t;
        }
        return "…";
    }

    /// <summary>日本語の文字を、幅に収まるように切って描く</summary>
    public static void TextFit(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        GUIStyle style = Style(false, size, anchor, false);
        Draw(r, Fit(text, style, r.width), style, color);
    }

    // 文字の位置を、仮想の画面のドットにそろえる（ずれると、拡大したときにドットの太さがまちまちになる）
    static Rect Snap(Rect r) => new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));

    // ───────── 塗り ─────────

    public static void Fill(Rect r, Color color)
    {
        if (Event.current.type != EventType.Repaint || r.width <= 0f || r.height <= 0f) return;
        if (_white == null)
        {
            _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
        Color saved = GUI.color;
        GUI.color = ToGui(color);
        GUI.DrawTexture(r, _white);
        GUI.color = saved;
    }

    /// <summary>
    /// 画面の色（sRGB）を、OnGUI に渡す色にする。このプロジェクトは Linear の色空間なので、そのまま渡すと明るく白っぽくなる
    /// </summary>
    public static Color ToGui(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

    /// <summary>四角の縁（内側に t ドット）</summary>
    public static void Border(Rect r, Color color, float t)
    {
        Fill(new Rect(r.x, r.y, r.width, t), color);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
        Fill(new Rect(r.x, r.y + t, t, r.height - t * 2f), color);
        Fill(new Rect(r.xMax - t, r.y + t, t, r.height - t * 2f), color);
    }

    /// <summary>点線（横）</summary>
    public static void DottedLine(float x, float y, float width, Color color)
    {
        for (float d = 0f; d < width; d += 3f) Fill(new Rect(x + d, y, 1f, 1f), color);
    }

    /// <summary>点線の四角（空の能力の枠）</summary>
    public static void DashedBorder(Rect r, Color color)
    {
        for (float d = 0f; d < r.width; d += 4f)
        {
            Fill(new Rect(r.x + d, r.y, 2f, 1f), color);
            Fill(new Rect(r.x + d, r.yMax - 1f, 2f, 1f), color);
        }
        for (float d = 0f; d < r.height; d += 4f)
        {
            Fill(new Rect(r.x, r.y + d, 1f, 2f), color);
            Fill(new Rect(r.xMax - 1f, r.y + d, 1f, 2f), color);
        }
    }

    // ───────── 部品（画面イメージの形） ─────────

    /// <summary>ウィンドウ：外から濃い灰・灰・白の縁（3ドットずつ）、中は明るい灰</summary>
    public static Rect Window(Rect r)
    {
        Fill(r, FrameDark);
        Fill(Inset(r, 3f), Frame);
        Fill(Inset(r, 6f), White);
        Fill(Inset(r, 9f), Panel);
        return Inset(r, 9f);
    }

    /// <summary>カード：選んでいなければ灰の縁と薄い灰、選んでいれば濃い赤とオレンジの縁と白</summary>
    public static void CardBox(Rect r, bool selected)
    {
        if (selected)
        {
            Fill(r, DarkRed);
            Fill(Inset(r, 1f), Orange);
            Fill(Inset(r, 3f), White);
        }
        else
        {
            Fill(r, Frame);
            Fill(Inset(r, 2f), Card);
        }
    }

    /// <summary>白い箱（リザルトの表）：灰の縁</summary>
    public static void WhiteBox(Rect r)
    {
        Fill(r, Frame);
        Fill(Inset(r, 2f), White);
    }

    /// <summary>オレンジのボタン（決定）。focused＝キーで選んでいる</summary>
    public static bool PrimaryButton(Rect r, string text, bool focused = false, int size = 12)
    {
        bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
        Fill(r, DarkRed);
        Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 3f, r.height - 3f), hover || focused ? OrangeHover : Orange);
        Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, size, GUI.enabled ? White : Muted, TextAnchor.MiddleCenter);
        return Click(r);
    }

    /// <summary>白いボタン（もう一度あそぶ）：右下に濃い灰の影</summary>
    public static bool SecondaryButton(Rect r, string text, bool focused = false, int size = 12)
    {
        bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
        Fill(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), FrameDark);
        Fill(r, hover || focused ? Card : White);
        Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, size, hover || focused ? Orange : Ink, TextAnchor.MiddleCenter);
        return Click(r);
    }

    /// <summary>浮き出たボタン（下のバーの「◀ もどる」）：左上が白、右下が濃い灰</summary>
    public static bool BevelButton(Rect r, string text, int size = 12)
    {
        bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
        Fill(r, FrameDark);
        Fill(new Rect(r.x, r.y, r.width - 2f, r.height - 2f), White);
        Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Panel);
        Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, size, hover ? Orange : Ink, TextAnchor.MiddleCenter);
        return Click(r);
    }

    /// <summary>浮き出た箱（下のバーの「相棒」の枠）</summary>
    public static Rect BevelBox(Rect r)
    {
        Fill(r, FrameDark);
        Fill(new Rect(r.x, r.y, r.width - 2f, r.height - 2f), White);
        Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Panel);
        return Inset(r, 2f);
    }

    /// <summary>暗い面のボタン（ゲームオーバー）。primary＝マゼンタ、そうでなければ紫の縁</summary>
    public static bool DarkButton(Rect r, string text, bool primary, bool focused = false)
    {
        bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
        if (primary)
        {
            Fill(r, White);
            Fill(Inset(r, 2f), hover || focused ? new Color32(0xf3, 0x6e, 0xff, 0xff) : Magenta);
            Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, 12, Ink, TextAnchor.MiddleCenter);
        }
        else
        {
            Fill(r, hover || focused ? Lavender : Lilac);
            Fill(Inset(r, 2f), Ink);
            Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, 12, Lavender, TextAnchor.MiddleCenter);
        }
        return Click(r);
    }

    /// <summary>大きな × ボタン（ウィンドウの右上）：オレンジに白い ×、右下に濃い赤の影</summary>
    public static bool CloseButton(Rect r)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        float sh = Mathf.Max(2f, Mathf.Round(r.width * 0.1f));
        Fill(new Rect(r.x + sh, r.y + sh, r.width - sh, r.height - sh), DarkRed);
        var face = new Rect(r.x, r.y, r.width - sh, r.height - sh);
        Fill(face, hover ? OrangeHover : Orange);
        // 白い ×（太さ face の 1/6 の四角を斜めに並べる）
        float t = Mathf.Max(2f, Mathf.Round(face.width / 6f));
        float pad = Mathf.Round(face.width * 0.2f);
        float span = face.width - pad * 2f - t;
        int steps = Mathf.Max(1, Mathf.RoundToInt(span / (t * 0.5f)));
        for (int i = 0; i <= steps; i++)
        {
            float d = Mathf.Round(span * i / steps);
            Fill(new Rect(face.x + pad + d, face.y + pad + d, t, t), White);
            Fill(new Rect(face.xMax - pad - t - d, face.y + pad + d, t, t), White);
        }
        return Click(r);
    }

    /// <summary>オレンジの小さな札（「▶ 選択中」）</summary>
    public static void Chip(Rect r, string text)
    {
        Fill(r, Orange);
        Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, 12, White, TextAnchor.MiddleCenter);
    }

    /// <summary>縁だけの小さな札（能力のタグ「遠距離」など）</summary>
    public static float OutlineTag(float x, float y, string text)
    {
        GUIStyle s = Style(false, 12);
        float w = Mathf.Ceil(Measure(text, s)) + 8f;
        var r = new Rect(x, y, w, 16f);
        Fill(r, FrameDark);
        Fill(Inset(r, 1f), White);
        Text(new Rect(r.x, r.y - 1f, r.width, r.height), text, 12, BodyText, TextAnchor.MiddleCenter);
        return w;
    }

    /// <summary>濃い紫の名札（ウィンドウの左上「■ YouTube」）</summary>
    public static void NameTag(Rect r, string text)
    {
        Fill(r, Ink);
        Fill(new Rect(r.x + 4f, r.y + Mathf.Round((r.height - 5f) * 0.5f), 5f, 5f), Magenta);
        Text(new Rect(r.x + 12f, r.y - 1f, r.width - 14f, r.height), text, 12, White);
    }

    /// <summary>区切りのメーター（使用時間）</summary>
    public static void Meter(Rect r, int segments, float ratio, Color on, Color off)
    {
        float gap = 2f;
        float w = Mathf.Floor((r.width - gap * (segments - 1)) / segments);
        int lit = Mathf.Clamp(Mathf.RoundToInt(ratio * segments), 0, segments);
        for (int i = 0; i < segments; i++) Fill(new Rect(r.x + i * (w + gap), r.y, w, r.height), i < lit ? on : off);
    }

    /// <summary>下のバー（灰に白い線）。バーの上端を返す</summary>
    public static float BottomBar(float height)
    {
        float y = Height - height;
        Fill(new Rect(0f, y, Width, height), Frame);
        Fill(new Rect(0f, y, Width, 2f), White);
        return y;
    }

    public static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - d * 2f, r.height - d * 2f);

    static bool Click(Rect r) => GUI.enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);

    // ───────── 絵 ─────────

    /// <summary>スプライトを rect に描く（シートの中の1コマにも使える）</summary>
    public static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D tex = sprite.texture;
        Rect r = sprite.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);
    }

    /// <summary>スプライトを色を掛けて描く（暗い影など）</summary>
    public static void DrawSprite(Rect rect, Sprite sprite, Color tint)
    {
        Color saved = GUI.color;
        GUI.color = ToGui(tint);
        DrawSprite(rect, sprite);
        GUI.color = saved;
    }

    /// <summary>スプライトを、ドットの大きさ×scale（仮想の画面のドット）で描く。左上をそろえる</summary>
    public static void DrawSpriteAt(float x, float y, Sprite sprite, float scale = 1f)
    {
        if (sprite == null) return;
        Rect r = sprite.textureRect;
        DrawSprite(new Rect(x, y, r.width * scale, r.height * scale), sprite);
    }

    // 縦横の比を保って、画面いっぱいに描く（はみ出した所は切る）
    static void DrawCover(Sprite sprite)
    {
        Rect r = sprite.textureRect;
        float s = Mathf.Max(Screen.width / r.width, Screen.height / r.height);
        float w = r.width * s, h = r.height * s;
        DrawSprite(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), sprite);
    }

    /// <summary>プレイヤーの絵（1コマ目）。デザイン担当の絵は1ドットが3ピクセルなので、2/3 倍で描くと1ドット＝仮想の画面の2ドット（雑魚の2倍と同じ）</summary>
    public static Vector2 DrawPlayer(float x, float bottom, int frame = 0)
    {
        Sprite[] walk = GameArt.Instance != null ? GameArt.Instance.PlayerWalk : null;
        if (walk == null || walk.Length == 0) return Vector2.zero;
        Sprite f = walk[Mathf.Abs(frame) % walk.Length];
        Rect r = f.textureRect;
        var size = new Vector2(Mathf.Round(r.width * 2f / 3f), Mathf.Round(r.height * 2f / 3f));
        DrawSprite(new Rect(x, bottom - size.y, size.x, size.y), f);
        return size;
    }

    /// <summary>秒を「12:48」に</summary>
    public static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
