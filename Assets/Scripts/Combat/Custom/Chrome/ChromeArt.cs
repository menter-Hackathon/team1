using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Chrome のオフラインの恐竜ゲームの絵（2026-11-19。docs/app-feedback-v2-design.md 2-3・2-4）。コードの中のドット絵（1マス＝18ドット）。
/// ・恐竜（プレイヤーの能力）：灰色 #535353、白いふち 1ドット。走る2枚（足を入れ替え）と、ゲームオーバーの顔（目が ×）
/// ・サボテン（小・大）・翼竜（羽の上下2枚）：灰色に、赤 #EA4335 のふち 1ドット（危ない物の印）
/// ・オフラインのページ（ボスのウィンドウの中身）・HUD の能力のアイコン（恐竜の横顔）
/// 絵の名前は "BD_Chrome" で始める（BrowserDevFx.Art が "BD_" を足す）
/// </summary>
public static class ChromeArt
{
    public static readonly Color Grey = RuntimeSprites.Hex("535353");
    public static readonly Color Paper = RuntimeSprites.Hex("F7F7F7");
    public static readonly Color DangerRed = RuntimeSprites.Hex("EA4335");
    static readonly Color32 White32 = new Color32(255, 255, 255, 255);

    static string H(Color c) => ColorUtility.ToHtmlStringRGBA(c);

    // ───────── 恐竜（20×21 ＋ふち＝22×23） ─────────

    static readonly string[] DinoTop =
    {
        "...........########.",
        "..........##.#######",
        "..........##########",
        "..........##########",
        "..........##########",
        "..........#####.....",
        "..........########..",
        "#.........#####.....",
        "#........######.....",
        "##......#########...",
        "###....#######..#...",
        "####..########......",
        "##############......",
        ".#############......",
        "..###########.......",
        "...#########........",
        "....#######.........",
    };

    static readonly string[] LegsA =
    {
        ".....###.###........",
        ".....##....##.......",
        ".....#..............",
        ".....##.............",
    };

    static readonly string[] LegsB =
    {
        ".....###.###........",
        "......##...#........",
        "...........#........",
        "...........##.......",
    };

    static readonly string[] LegsStand =
    {
        ".....###.###........",
        ".....##...##........",
        ".....#.....#........",
        ".....##....##.......",
    };

    /// <summary>
    /// 恐竜（右向き。22×23）。frame：0・1＝走る（足を入れ替え）、2＝立つ、3＝ゲームオーバー（目が ×・立つ）。edge＝ふちの色
    /// </summary>
    public static Sprite Dino(int frame, Color edge)
    {
        frame = Mathf.Clamp(frame, 0, 3);
        var g = new BrowserDevGrid(22, 23);
        g.Stamp(DinoTop, 1, 1, 'G');
        string[] legs = frame == 0 ? LegsA : frame == 1 ? LegsB : LegsStand;
        g.Stamp(legs, 1, 1 + DinoTop.Length, 'G');
        if (frame == 3)
        {
            // 目を × にする（頭の中に透明な ×。ふちを付けると白い × になる）
            g.Set(13, 2, 'G');
            g.Set(14, 2, '.');
            g.Set(16, 2, '.');
            g.Set(15, 3, '.');
            g.Set(14, 4, '.');
            g.Set(16, 4, '.');
        }
        g.Outline('E');
        var pal = new Dictionary<char, Color32> { { 'G', Grey }, { 'E', edge } };
        return BrowserDevFx.Art($"ChromeDino_{frame}_{H(edge)}", g, pal);
    }

    // ───────── サボテン ─────────

    /// <summary>小さいサボテン（9×16 ＋ふち＝11×18。0.6×1.0マス）</summary>
    public static Sprite SmallCactus(Color edge)
    {
        var g = new BrowserDevGrid(11, 18);
        CactusInto(g, 1, 1, 16, true, true);
        g.Outline('E');
        return BrowserDevFx.Art($"ChromeCactusS_{H(edge)}", g, new Dictionary<char, Color32> { { 'G', Grey }, { 'E', edge } });
    }

    /// <summary>大きいサボテン（3本ならび。24×21 ＋ふち＝26×23。約1.4×1.3マス）</summary>
    public static Sprite BigCactus(Color edge)
    {
        var g = new BrowserDevGrid(26, 23);
        CactusInto(g, 1 + 7, 1, 21, true, true);    // まん中（いちばん高い）
        CactusInto(g, 1 + 0, 1 + 6, 15, true, false); // 左
        CactusInto(g, 1 + 15, 1 + 7, 14, false, true); // 右
        g.Outline('E');
        return BrowserDevFx.Art($"ChromeCactusB_{H(edge)}", g, new Dictionary<char, Color32> { { 'G', Grey }, { 'E', edge } });
    }

    /// <summary>サボテン1本（左上 x, y。幅 9、高さ h）。幹は 3ドット、腕は左右に</summary>
    static void CactusInto(BrowserDevGrid g, int x, int y, int h, bool leftArm, bool rightArm)
    {
        g.Set(x + 4, y, 'G');
        g.Rect(x + 3, y + 1, 3, h - 1, 'G');
        int a = Mathf.Max(2, h / 4);
        if (leftArm)
        {
            g.Rect(x, y + a + 1, 2, a + 1, 'G');
            g.Rect(x + 1, y + a * 2 + 1, 2, 2, 'G');
        }
        if (rightArm)
        {
            g.Rect(x + 7, y + a, 2, a, 'G');
            g.Rect(x + 6, y + a * 2 - 1, 2, 2, 'G');
        }
    }

    // ───────── 翼竜（20×12 ＋ふち＝22×14。1.2×0.8マス。左向き） ─────────

    /// <summary>翼竜（左向き）。up＝羽が上</summary>
    public static Sprite Bird(bool up, Color edge)
    {
        var g = new BrowserDevGrid(22, 14);
        int ox = 1, oy = 1;
        g.Rect(ox + 5, oy + 6, 12, 3, 'G');   // 体
        g.Rect(ox + 17, oy + 7, 3, 1, 'G');   // しっぽ
        g.Rect(ox + 2, oy + 4, 4, 3, 'G');    // 頭
        g.Rect(ox + 0, oy + 5, 2, 1, 'G');    // くちばし
        g.Set(ox + 4, oy + 3, 'G');           // とさか
        if (up)
        {
            for (int r = 0; r < 6; r++) g.Rect(ox + 8, oy + 5 - r, 6 - r, 1, 'G');
        }
        else
        {
            for (int r = 0; r < 3; r++) g.Rect(ox + 8, oy + 9 + r, 6 - r * 2, 1, 'G');
        }
        g.Outline('E');
        return BrowserDevFx.Art($"ChromeBird_{(up ? 1 : 0)}_{H(edge)}", g, new Dictionary<char, Color32> { { 'G', Grey }, { 'E', edge } });
    }

    // ───────── オフラインのページ（ボスのウィンドウの中身。36×26） ─────────

    /// <summary>白っぽい地に、灰色の恐竜の顔と、灰色の短い行（No internet のページのつもり）</summary>
    public static Sprite OfflinePage()
    {
        var g = new BrowserDevGrid(36, 26);
        g.Rect(0, 0, 36, 26, 'P');
        g.Frame(0, 0, 36, 26, 'L');
        // 恐竜の頭（DinoTop の上の 9行）を左上に
        for (int j = 0; j < 9; j++)
        {
            string row = DinoTop[j];
            for (int i = 10; i < row.Length; i++)
                if (row[i] == '#') g.Set(4 + i - 10, 4 + j, 'G');
        }
        g.Rect(4, 17, 22, 2, 'G'); // 題名（No internet のつもり）
        g.Rect(4, 21, 14, 1, 'L');
        g.Rect(20, 21, 9, 1, 'L');
        var pal = new Dictionary<char, Color32> { { 'P', Paper }, { 'L', RuntimeSprites.Hex("BDBDBD") }, { 'G', Grey } };
        return BrowserDevFx.Art("ChromeOfflinePage", g, pal);
    }

    // ───────── HUD の能力のアイコン（16×16） ─────────

    /// <summary>恐竜の横顔（灰色。地は白い角の丸い四角）</summary>
    public static Sprite DinoIcon()
    {
        var g = new BrowserDevGrid(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (BrowserDevMac.Inside(16, 16, 2, x, y)) g.Set(x, y, 'W');
        // 恐竜の頭と首（DinoTop の 0〜12行・6〜19列を、ひとまわり小さく写す）
        for (int j = 0; j < 13; j++)
        {
            string row = DinoTop[j];
            for (int i = 6; i < row.Length; i++)
            {
                if (row[i] != '#') continue;
                int x = 1 + (i - 6), y = 2 + j;
                if (x < 15 && y < 15) g.Set(x, y, 'G');
            }
        }
        var pal = new Dictionary<char, Color32> { { 'W', White32 }, { 'G', Grey } };
        return BrowserDevFx.Art("ChromeDinoIcon", g, pal);
    }

    // ───────── 小物 ─────────

    /// <summary>恐竜ゲームの地面の線を出す（灰色 1ドット。所どころに小石の点）。長さ length マス、左の端が localLeft</summary>
    public static Transform GroundLine(Transform parent, Vector2 localLeft, float length, int order, string name = "GroundLine")
    {
        Transform root = BrowserDevFx.Child(parent, name, localLeft);
        float d = BrowserDevFx.Dot;
        BrowserDevFx.Rect(root, new Vector2(length * 0.5f, d * 0.5f), new Vector2(length, d), Grey, order, "Line");
        // 小石（線の下に 1ドット。だいたい 0.6マスおき）
        int stones = Mathf.Max(0, Mathf.FloorToInt(length / 0.6f));
        for (int i = 0; i < stones; i++)
        {
            float x = (i + 0.5f) * 0.6f + Random.Range(-0.2f, 0.2f);
            if (x <= 0f || x >= length) continue;
            float w = Random.value < 0.3f ? 2f * d : d;
            BrowserDevFx.Rect(root, new Vector2(x, -d * (Random.value < 0.5f ? 1.5f : 2.5f)), new Vector2(w, d), Grey, order, "Stone");
        }
        return root;
    }
}
