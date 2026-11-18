using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ラスボス Terminal の技と演出で使う、色・ドット絵・小さな道具（2026-11-18。docs/app-boss-skills-design.md 4章）。
/// 絵はコードの中のドット絵（RuntimeSprites.FromAscii）で作る。絵のファイルは足さない。
/// 色の16進は、どれも仕様書の【仮】の値
/// </summary>
public static class TerminalArt
{
    /// <summary>1ドット（1マス＝18ドット）</summary>
    public const float Pixel = 1f / 18f;

    // ───────── 色【仮】 ─────────

    /// <summary>ターミナルの緑（コマンドの行の文字・分身）</summary>
    public static readonly Color Green = RuntimeSprites.Hex("33FF66");
    /// <summary>カーネル・パニックの飾りの文字（暗い緑）</summary>
    public static readonly Color DarkGreen = RuntimeSprites.Hex("0F3D1E");
    /// <summary>危ない物の赤（予告・文字化けの床・柱・鍵の光）</summary>
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color White = RuntimeSprites.Hex("FFFFFF");
    /// <summary>カーネル・パニックのときの床の線・足場のふち</summary>
    public static readonly Color Gray = RuntimeSprites.Hex("C8C8C8");
    /// <summary>コマンドの行の地</summary>
    public static readonly Color LineBg = RuntimeSprites.Hex("0C0C0C");
    public static readonly Color Black = RuntimeSprites.Hex("000000");

    // ───────── 文字 ─────────

    /// <summary>文字化けの床の文字（▒ はフォントにあれば足す）</summary>
    static string[] _corruptGlyphs;
    public static string[] CorruptGlyphs
    {
        get
        {
            if (_corruptGlyphs != null) return _corruptGlyphs;
            var list = new List<string> { "#", "?", "%", "!", "&", "@", "*", "$" };
            Font font = PixelGui.FontEn;
            if (font != null && font.HasCharacter('▒')) list.Add("▒");
            _corruptGlyphs = list.ToArray();
            return _corruptGlyphs;
        }
    }

    /// <summary>床が文字にくずれるときの字（緑の英数字）</summary>
    public static readonly string[] CrumbleGlyphs = { "0", "1", "/", "#", "$", "rm", "-rf", "0", "1" };
    /// <summary>分身が消えるときの字</summary>
    public static readonly string[] BinaryGlyphs = { "0", "1" };
    /// <summary>降ってくる文字列の字</summary>
    public const string RainChars = "01#$%&@!?*/\\[]{}=+";

    // ───────── 画面の上の表示（OnGUI） ─────────

    /// <summary>画面の切り替え中・ゲームオーバー中は、画面の上の表示を出さない（PixelHud と同じ）</summary>
    public static bool GuiHidden()
    {
        GameSession s = GameSession.Instance;
        return s != null && (s.IsTransitioning || s.IsGameOver);
    }

    /// <summary>1マスが PixelGui の座標で何ドットか（PixelGui.Begin のあとに呼ぶ）。カメラが無ければ 30</summary>
    public static float GuiPerUnit()
    {
        Vector2? a = ScreenUi.WorldToGui(Vector3.zero);
        Vector2? b = ScreenUi.WorldToGui(Vector3.up);
        if (a is Vector2 pa && b is Vector2 pb)
        {
            float d = Mathf.Abs(pa.y - pb.y);
            if (d > 0.01f) return d;
        }
        return 30f;
    }

    /// <summary>ゲームの世界の高さ y を、PixelGui の座標の y に直す（カメラが無ければ fallback）</summary>
    public static float GuiY(float worldY, float fallback)
    {
        Vector2? p = ScreenUi.WorldToGui(new Vector3(0f, worldY, 0f));
        return p is Vector2 v ? v.y : fallback;
    }

    /// <summary>黒いふち（1ドット）つきの英字</summary>
    public static void OutlinedTextEn(Rect r, string text, int size, Color color, Color outline, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GUIStyle style = PixelGui.Style(true, size, anchor);
        PixelGui.Draw(new Rect(r.x - 1f, r.y, r.width, r.height), text, style, outline);
        PixelGui.Draw(new Rect(r.x + 1f, r.y, r.width, r.height), text, style, outline);
        PixelGui.Draw(new Rect(r.x, r.y - 1f, r.width, r.height), text, style, outline);
        PixelGui.Draw(new Rect(r.x, r.y + 1f, r.width, r.height), text, style, outline);
        PixelGui.Draw(r, text, style, color);
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // ───────── ドット絵【仮】 ─────────

    static Dictionary<char, Color32> _lockPalette;
    static Dictionary<char, Color32> LockPalette => _lockPalette ?? (_lockPalette = new Dictionary<char, Color32>
    {
        { 'W', RuntimeSprites.Hex("F2F2F7") }, // つる
        { 'D', RuntimeSprites.Hex("5A5A5E") }, // 本体のふち
        { 'L', RuntimeSprites.Hex("8E8E93") }, // 本体
        { 'K', RuntimeSprites.Hex("1C1C1E") }, // かぎ穴
    });

    /// <summary>南京錠の弾（14ドット）</summary>
    public static Sprite LockBullet => RuntimeSprites.FromAscii("Terminal_LockBullet", new[]
    {
        "....WWWWWW....",
        "...WW....WW...",
        "..WW......WW..",
        "..W........W..",
        "..W........W..",
        "..W........W..",
        "DDDDDDDDDDDDDD",
        "DLLLLLLLLLLLLD",
        "DLLLLLKKLLLLLD",
        "DLLLLLKKLLLLLD",
        "DLLLLLLKLLLLLD",
        "DLLLLLLKLLLLLD",
        "DLLLLLLLLLLLLD",
        "DDDDDDDDDDDDDD",
    }, LockPalette);

    /// <summary>頭の上の南京錠（10ドット、閉じている）</summary>
    public static Sprite LockClosed => RuntimeSprites.FromAscii("Terminal_LockClosed", new[]
    {
        "..........",
        "..WWWWWW..",
        ".WW....WW.",
        ".W......W.",
        ".W......W.",
        "DDDDDDDDDD",
        "DLLLLLLLLD",
        "DLLLKKLLLD",
        "DLLLLKLLLD",
        "DLLLLLLLLD",
        "DDDDDDDDDD",
    }, LockPalette);

    /// <summary>頭の上の南京錠（開いた）</summary>
    public static Sprite LockOpen => RuntimeSprites.FromAscii("Terminal_LockOpen", new[]
    {
        "..WWWWWW..",
        ".WW....WW.",
        ".W......W.",
        ".W........",
        ".W........",
        "DDDDDDDDDD",
        "DLLLLLLLLD",
        "DLLLKKLLLD",
        "DLLLLKLLLD",
        "DLLLLLLLLD",
        "DDDDDDDDDD",
    }, LockPalette);

    /// <summary>封印の間、足元に出す赤い鎖の輪</summary>
    public static Sprite ChainRing => RuntimeSprites.FromAscii("Terminal_ChainRing", new[]
    {
        "....RrRrRrRrRrRr....",
        "..Rr............rR..",
        "Rr................rR",
        "..rR............Rr..",
        "....rRrRrRrRrRrR....",
    }, new Dictionary<char, Color32>
    {
        { 'R', RuntimeSprites.Hex("FF453A") },
        { 'r', RuntimeSprites.Hex("8A1F18") },
    });

    /// <summary>分身の弾（緑の &amp; に白い芯。9ドット）</summary>
    public static Sprite AmpBullet => RuntimeSprites.FromAscii("Terminal_AmpBullet", new[]
    {
        "..GGG....",
        ".GWWWG...",
        ".GWGWG...",
        "..GWWG...",
        ".GWWWG.GG",
        "GWG.GWGWG",
        "GWG..GWG.",
        "GWWGGWWG.",
        ".GGG.GGWG",
    }, new Dictionary<char, Color32>
    {
        { 'G', RuntimeSprites.Hex("33FF66") },
        { 'W', RuntimeSprites.Hex("FFFFFF") },
    });

    /// <summary>
    /// 分身の絵（ボスのウィンドウ 48×36 ドットの6割、29×22 ドット）。緑1色のウィンドウ（ふち・タイトルバー・中のプロンプト）。
    /// 半分すかすのは、表示する側（SpriteRenderer の色）
    /// </summary>
    public static Sprite GhostWindow => MakeGhostWindow("Terminal_GhostWindow", "33FF66", "0F3D1EC0");

    /// <summary>分身が白く光るときの絵（形は GhostWindow と同じ）</summary>
    public static Sprite GhostWindowFlash => MakeGhostWindow("Terminal_GhostWindowFlash", "FFFFFF", "FFFFFFC0");

    static Sprite MakeGhostWindow(string key, string edgeHex, string fillHex)
    {
        const int w = 29, h = 22;
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                bool title = y >= 1 && y <= 3;
                line[x] = edge || title ? 'G' : 'g';
            }
            rows[y] = new string(line);
        }
        // 中に「>_」（ターミナルのプロンプト）
        char[] r9 = rows[9].ToCharArray(); r9[4] = 'G'; rows[9] = new string(r9);
        char[] r10 = rows[10].ToCharArray(); r10[5] = 'G'; rows[10] = new string(r10);
        char[] r11 = rows[11].ToCharArray(); r11[4] = 'G'; rows[11] = new string(r11);
        char[] r12 = rows[12].ToCharArray(); r12[7] = 'G'; r12[8] = 'G'; r12[9] = 'G'; rows[12] = new string(r12);
        return RuntimeSprites.FromAscii(key, rows, new Dictionary<char, Color32>
        {
            { 'G', RuntimeSprites.Hex(edgeHex) },
            { 'g', RuntimeSprites.Hex(fillHex) },
        });
    }

    /// <summary>白い四角の絵を、大きさ size で子に出す（線・帯・カーソル）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localPos, Vector2 size, Color color, int sortingOrder, string objectName = "Box")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, PlaceholderSprites.Square, color, size, sortingOrder, objectName);
        sr.transform.localPosition = localPos;
        return sr;
    }
}
