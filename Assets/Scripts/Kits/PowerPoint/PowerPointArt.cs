using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】PowerPoint のキットの絵（2026-11-19。docs/app-kits-1-design.md 11章）。コードの中のドット絵。1マス＝18ドット。
/// 色：オレンジ赤 #D24726・白・濃い灰色 #3B3B3B・ポインターの赤 #FF2D2D
/// </summary>
public static class PowerPointArt
{
    public static readonly Color Orange = RuntimeSprites.Hex("D24726");
    public static readonly Color OrangeLight = RuntimeSprites.Hex("F07A5A");
    public static readonly Color Dark = RuntimeSprites.Hex("3B3B3B");
    public static readonly Color Pointer = RuntimeSprites.Hex("FF2D2D");

    static Dictionary<char, Color32> Pal(params (char c, Color col)[] items)
    {
        var d = new Dictionary<char, Color32>();
        foreach (var (c, col) in items) d[c] = col;
        return d;
    }

    /// <summary>箇条書きの文の行（45×11＝2.5×0.6マス）。白い板に灰色の棒、先頭にオレンジの「•」。赤いふち（危ない物）</summary>
    public static Sprite Line => RuntimeSprites.FromAscii("PPT_Line", new[]
    {
        "RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR",
        "RWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWR",
        "RWWOOOWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWR",
        "RWOOOOOWWGGGGGGGGGGWGGGGGGGGGGGGWGGGGGGGGWWWR",
        "RWOOOOOWWGGGGGGGGGGWGGGGGGGGGGGGWGGGGGGGGWWWR",
        "RWOOOOOWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWR",
        "RWWOOOWWWGGGGGGGGGWGGGGGGGGGGWGGGGGGWWWWWWWWR",
        "RWWWWWWWWGGGGGGGGGWGGGGGGGGGGWGGGGGGWWWWWWWWR",
        "RWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWR",
        "RWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWR",
        "RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR",
    }, Pal(('R', KitDress.Red), ('W', Color.white), ('O', Orange), ('G', RuntimeSprites.Hex("B8B8BD"))));

    /// <summary>箇条書きの「•」（9×9）</summary>
    public static Sprite Dot => RuntimeSprites.FromAscii("PPT_Dot", new[]
    {
        "...OOO...",
        "..OOOOO..",
        ".OOLLOOO.",
        "OOOLOOOOO",
        "OOOOOOOOO",
        "OOOOOOOOO",
        ".OOOOOOO.",
        "..OOOOO..",
        "...OOO...",
    }, Pal(('O', Orange), ('L', OrangeLight)));

    /// <summary>ボスのブロック矢印（54×18＝3×1マス。右向き）。オレンジ赤に赤いふち</summary>
    public static Sprite Arrow => RuntimeSprites.FromAscii("PPT_Arrow", ArrowRows(), Pal(('E', KitDress.Red), ('O', Orange), ('L', OrangeLight)));

    /// <summary>能力のブロック矢印（54×18。右向き）。オレンジに白いふち（当たらない色）</summary>
    public static Sprite SkillArrow => RuntimeSprites.FromAscii("PPT_SkillArrow", ArrowRows(), Pal(('E', Color.white), ('O', RuntimeSprites.Hex("ED7D31")), ('L', RuntimeSprites.Hex("FFC000"))));

    /// <summary>壁の予告の大きい矢印（ふちだけ。赤）</summary>
    public static Sprite ArrowOutline => RuntimeSprites.FromAscii("PPT_ArrowOutline", ArrowRows(true), Pal(('E', KitDress.Red), ('O', new Color(1f, 0.27f, 0.23f, 0.18f)), ('L', new Color(1f, 0.27f, 0.23f, 0.18f))));

    static string[] ArrowRows(bool outlineOnly = false)
    {
        const int w = 54, h = 18;
        var rows = new string[h];
        int headStart = 36; // 矢じりの始まり
        for (int y = 0; y < h; y++)
        {
            var sb = new System.Text.StringBuilder(w);
            for (int x = 0; x < w; x++)
            {
                char c = '.';
                bool shaft = x < headStart && y >= 5 && y <= 12;
                // 矢じり：x が進むほど細くなる三角
                int half = (w - 1 - x) * 9 / (w - headStart);
                bool head = x >= headStart && Mathf.Abs(y * 2 + 1 - h) <= half * 2;
                if (shaft || head)
                {
                    bool edge = (shaft && (y == 5 || y == 12 || x == 0)) || (head && (Mathf.Abs(y * 2 + 1 - h) >= half * 2 - 2 || x == headStart));
                    if (shaft && head) edge = false;
                    if (x == headStart && (y < 5 || y > 12)) edge = true;
                    c = edge ? 'E' : (y < 8 ? 'L' : 'O');
                    if (outlineOnly && !edge) c = 'O';
                }
                sb.Append(c);
            }
            rows[y] = sb.ToString();
        }
        return rows;
    }

    /// <summary>レーザーポインターの輪（d ドット、太さ 2）と、まん中の点</summary>
    public static Sprite PointerRing(int d)
    {
        d = Mathf.Max(6, d);
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var sb = new System.Text.StringBuilder(d);
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                char c = '.';
                if (dist <= r && dist > r - 2f) c = 'R';
                else if (dist <= 2.2f) c = 'C';
                else if (dist <= r - 2f) c = 'G';
                sb.Append(c);
            }
            rows[y] = sb.ToString();
        }
        return RuntimeSprites.FromAscii($"PPT_Pointer_{d}", rows, Pal(('R', Pointer), ('C', Pointer), ('G', new Color(1f, 0.18f, 0.18f, 0.18f))));
    }
}
