using UnityEngine;

/// <summary>
/// 【仮】タイトル・リザルトの仮画面（OnGUI）を、ゲームの絵（壁紙・ウィンドウの枠・ボタン）で描くための道具。
/// 画面を整数倍に拡大した「仮想の画面」（高さ 360 前後）で配置する。UI 担当の本番画面ができたら消す。
/// 使い方：OnGUI の最初に Begin()、最後に End()。座標は Width・Height（仮想の画面の大きさ）を使う。
/// </summary>
public static class PixelGui
{
    static GUIStyle _panel, _button, _title, _label, _small;
    static Matrix4x4 _saved;

    /// <summary>拡大率（整数）</summary>
    public static float Scale { get; private set; } = 1f;
    public static float Width => Screen.width / Scale;
    public static float Height => Screen.height / Scale;

    public static GUIStyle Panel => _panel;
    public static GUIStyle Button => _button;
    public static GUIStyle Title => _title;
    public static GUIStyle Label => _label;
    public static GUIStyle Small => _small;

    /// <summary>壁紙を画面いっぱいに描いて、拡大を始める</summary>
    public static void Begin()
    {
        GameArt art = GameArt.Instance;
        if (art != null && art.Background != null) DrawCover(art.Background);
        Scale = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        _saved = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        EnsureStyles(art);
    }

    public static void End() => GUI.matrix = _saved;

    static void EnsureStyles(GameArt art)
    {
        if (_panel != null && _panel.normal.background != null) return;
        var dark = new Color32(0x22, 0x22, 0x22, 0xff);
        _panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(10, 10, 10, 10) };
        _button = new GUIStyle(GUI.skin.button) { border = new RectOffset(4, 4, 4, 4), fontSize = 14, fontStyle = FontStyle.Bold };
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
        _button.normal.textColor = dark;
        _button.hover.textColor = new Color32(0xdf, 0x71, 0x26, 0xff);
        _button.active.textColor = new Color32(0xdf, 0x71, 0x26, 0xff);
        _button.focused.textColor = dark;
        _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
        _title.normal.textColor = dark;
        _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        _label.normal.textColor = dark;
        _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        _small.normal.textColor = new Color32(0x97, 0x00, 0x00, 0xff);
    }

    /// <summary>スプライトを rect に描く（シートの中の1コマにも使える）</summary>
    public static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D tex = sprite.texture;
        Rect r = sprite.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);
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
}
