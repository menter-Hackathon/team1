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
    /// 分身の絵（ボスのウィンドウ 48×36 ドットの6割、29×22 ドット）。緑1色のウィンドウ（ふち・タイトルバーの点 3つと下の線・中のプロンプト）。
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
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                bool titleLine = y == 4; // タイトルバーの下の線
                line[x] = corner ? '.' : edge || titleLine ? 'G' : 'g';
            }
            rows[y] = new string(line);
        }
        // 2026-11-19【仮】Mac の形（docs/mac-look-design.md 2章 10）：角を 1ドット欠き、左上に点 3つ（1ドット、x＝2・4・6）。右上は空
        char[] r2 = rows[2].ToCharArray(); r2[2] = 'G'; r2[4] = 'G'; r2[6] = 'G'; rows[2] = new string(r2);
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

    // ───────── 権限のカード（2026-11-19：chmod 000 player。docs/app-feedback-v2-design.md 6-2） ─────────

    /// <summary>権限のカードの印の種類（並びの順：移動・ジャンプ・近接・能力）</summary>
    public enum Perm { Move, Jump, Melee, Skill }

    /// <summary>使える印の緑【仮】</summary>
    public static readonly Color PermGreen = RuntimeSprites.Hex("33FF66");
    /// <summary>使えない印の灰色【仮】</summary>
    public static readonly Color PermGray = RuntimeSprites.Hex("6E6E6E");

    /// <summary>カードの板の大きさ（ドット）</summary>
    public const int CardW = 44, CardH = 12;
    /// <summary>印の大きさ（ドット）</summary>
    public const int PermSize = 9;

    /// <summary>印の形（9×9。X が絵）</summary>
    static string[] PermShape(Perm p)
    {
        switch (p)
        {
            case Perm.Move: return new[]
            {
                ".........",
                ".........",
                "..X...X..",
                ".XX...XX.",
                "XXXXXXXXX",
                ".XX...XX.",
                "..X...X..",
                ".........",
                ".........",
            };
            case Perm.Jump: return new[]
            {
                "....X....",
                "...XXX...",
                "..XXXXX..",
                ".XXXXXXX.",
                "...XXX...",
                "...XXX...",
                "...XXX...",
                "...XXX...",
                ".........",
            };
            case Perm.Melee: return new[]
            {
                "........X",
                ".......XX",
                "......XX.",
                ".....XX..",
                ".X..XX...",
                "..XXX....",
                "..XX.....",
                ".X..X....",
                "X........",
            };
            default: return new[]
            {
                "....X....",
                "....X....",
                "...XXX...",
                "XXXXXXXXX",
                ".XXXXXXX.",
                "..XXXXX..",
                "..XX.XX..",
                ".XX...XX.",
                ".X.....X.",
            };
        }
    }

    /// <summary>使える印（緑）</summary>
    public static Sprite PermOn(Perm p) => RuntimeSprites.FromAscii($"Terminal_PermOn_{p}", PermShape(p),
        new Dictionary<char, Color32> { { 'X', RuntimeSprites.Hex("33FF66") } });

    /// <summary>
    /// 使えない印：灰色の印に、赤い斜めの線（左上から右下）。右下に小さい赤い鍵（4ドット）。印の後ろは暗い赤。
    /// flash＝ボタンを押したときの白い光（印が白くなる）
    /// </summary>
    public static Sprite PermOff(Perm p, bool flash = false)
    {
        string[] shape = PermShape(p);
        var rows = new string[PermSize];
        for (int y = 0; y < PermSize; y++)
        {
            var line = new char[PermSize];
            for (int x = 0; x < PermSize; x++)
            {
                bool inKey = x >= 5 && y >= 5;
                bool slash = (x == y || x == y + 1) && !inKey;           // 鍵の所では線を止める
                bool key = inKey && KeyPixel(x - 5, y - 5);
                bool hole = inKey && y == 6 && (x == 6 || x == 7);        // 鍵のつるの内側（黒）
                if (key) line[x] = 'R';
                else if (hole) line[x] = 'K';
                else if (slash) line[x] = 'R';
                else if (shape[y][x] == 'X') line[x] = 'g';
                else line[x] = 'b';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(flash ? $"Terminal_PermOffFlash_{p}" : $"Terminal_PermOff_{p}", rows, new Dictionary<char, Color32>
        {
            { 'g', flash ? RuntimeSprites.Hex("FFFFFF") : RuntimeSprites.Hex("6E6E6E") },
            { 'R', RuntimeSprites.Hex("FF453A") },
            { 'b', flash ? RuntimeSprites.Hex("8A2A22") : RuntimeSprites.Hex("4A0F0C") },
            { 'K', RuntimeSprites.Hex("0C0C0C") },
        });
    }

    /// <summary>右下の小さい鍵（4×4。南京錠の形）</summary>
    static bool KeyPixel(int x, int y)
    {
        string[] k = { ".RR.", "R..R", "RRRR", "RRRR" };
        return y >= 0 && y < 4 && x >= 0 && x < 4 && k[y][x] == 'R';
    }

    /// <summary>権限のカードの板（44×12。暗い角の丸い板、ふち 1ドット）</summary>
    public static Sprite PermCard
    {
        get
        {
            var rows = new string[CardH];
            for (int y = 0; y < CardH; y++)
            {
                var line = new char[CardW];
                for (int x = 0; x < CardW; x++)
                {
                    bool cornerOut = (x == 0 || x == CardW - 1) && (y == 0 || y == CardH - 1);
                    bool edge = x == 0 || y == 0 || x == CardW - 1 || y == CardH - 1;
                    line[x] = cornerOut ? '.' : edge ? 'E' : 'B';
                }
                rows[y] = new string(line);
            }
            return RuntimeSprites.FromAscii("Terminal_PermCard", rows, new Dictionary<char, Color32>
            {
                { 'E', RuntimeSprites.Hex("5A5A5E") },
                { 'B', RuntimeSprites.Hex("0C0C0C") },
            });
        }
    }

    /// <summary>カードの中の印の位置（カードの真ん中から。マス）。印は左から 2・12・22・32 ドット目、上から 2 ドット目</summary>
    public static Vector2 PermSlot(int i) => new Vector2((2 + 10 * i + PermSize * 0.5f - CardW * 0.5f) * Pixel, -0.5f * Pixel);

    /// <summary>武器を持つ手の小さい赤い ×（6ドット。近接が使えない印）</summary>
    public static Sprite RedCross => RuntimeSprites.FromAscii("Terminal_RedCross", new[]
    {
        "KK..KK",
        "KRKKRK",
        ".KRRK.",
        ".KRRK.",
        "KRKKRK",
        "KK..KK",
    }, new Dictionary<char, Color32>
    {
        { 'R', RuntimeSprites.Hex("FF453A") },
        { 'K', RuntimeSprites.Hex("0C0C0C") },
    });

    // ───────── 能力 rm -rf（2026-11-19。docs/app-feedback-v2-design.md 6-3） ─────────

    static Sprite _rmIcon;

    /// <summary>能力のアイコン（16ドット。黒い地に緑の rm）。HUD の枠に出す（毎フレーム聞かれるので、作った絵を覚えておく）</summary>
    public static Sprite RmIcon => _rmIcon != null ? _rmIcon : (_rmIcon = MakeRmIcon());

    static Sprite MakeRmIcon() => RuntimeSprites.FromAscii("Terminal_RmIcon", new[]
    {
        "EEEEEEEEEEEEEEEE",
        "EBBBBBBBBBBBBBBE",
        "EBBBBBBBBBBBBBBE",
        "EBBBBBBBBBBBBBBE",
        "EBBBBBBBBBBBBBBE",
        "EBBGBGGBGGBGBBBE",
        "EBBGGBBBGBGBGBBE",
        "EBBGBBBBGBGBGBBE",
        "EBBGBBBBGBGBGBBE",
        "EBBGBBBBGBGBGBBE",
        "EBBBBBBBBBBBBBBE",
        "EBBBBBBBBBBWWBBE",
        "EBBBBBBBBBBWWBBE",
        "EBBBBBBBBBBBBBBE",
        "EBBBBBBBBBBBBBBE",
        "EEEEEEEEEEEEEEEE",
    }, new Dictionary<char, Color32>
    {
        { 'E', RuntimeSprites.Hex("3A3A3C") },
        { 'B', RuntimeSprites.Hex("0C0C0C") },
        { 'G', RuntimeSprites.Hex("33FF66") },
        { 'W', RuntimeSprites.Hex("F2F2F7") },
    });

    /// <summary>能力の波の字（緑と白）</summary>
    public static readonly string[] WaveGlyphs = { "#", "/", "*", "rm", "#", "/", "*", "0", "1" };

    /// <summary>白い四角の絵を、大きさ size で子に出す（線・帯・カーソル）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localPos, Vector2 size, Color color, int sortingOrder, string objectName = "Box")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, PlaceholderSprites.Square, color, size, sortingOrder, objectName);
        sr.transform.localPosition = localPos;
        return sr;
    }
}
