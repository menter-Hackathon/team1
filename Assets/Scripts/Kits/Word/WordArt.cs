using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 【仮】Word のキットの絵（2026-11-19。docs/app-kits-1-design.md 12章）。コードの中のドット絵。1マス＝18ドット。
/// 色：青 #2B579A・白・赤 #E81123・コメントの黄 #FFF4CE
/// </summary>
public static class WordArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("2B579A");
    public static readonly Color BlueLight = RuntimeSprites.Hex("5B8BD9");
    public static readonly Color Red = RuntimeSprites.Hex("E81123");
    public static readonly Color CommentYellow = RuntimeSprites.Hex("FFF4CE");
    public static readonly Color CommentEdge = RuntimeSprites.Hex("E6C65C");
    public static readonly Color TextGrey = RuntimeSprites.Hex("AEAEB2");
    public static readonly Color Ink = RuntimeSprites.Hex("1C1C1E");

    static Dictionary<char, Color32> Pal(params (char c, Color col)[] items)
    {
        var d = new Dictionary<char, Color32>();
        foreach (var (c, col) in items) d[c] = col;
        return d;
    }

    static Sprite Make(string key, char[,] g, Dictionary<char, Color32> pal)
    {
        int h = g.GetLength(0), w = g.GetLength(1);
        var rows = new string[h];
        var sb = new StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Clear();
            for (int x = 0; x < w; x++) sb.Append(g[y, x]);
            rows[y] = sb.ToString();
        }
        return RuntimeSprites.FromAscii(key, rows, pal);
    }

    static char[,] Grid(int w, int h)
    {
        var g = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[y, x] = '.';
        return g;
    }

    static void Fill(char[,] g, int x0, int y0, int w, int h, char c)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (y >= 0 && x >= 0 && y < g.GetLength(0) && x < g.GetLength(1)) g[y, x] = c;
    }

    /// <summary>うすい灰色の文の行（w×h ドット。棒をすき間つきで並べる）。白で作るので、色は SpriteRenderer で付ける</summary>
    public static Sprite TextRow(int w, int h)
    {
        w = Mathf.Max(8, w); h = Mathf.Max(4, h);
        var g = Grid(w, h);
        int[] lens = { 9, 5, 12, 7, 4, 10, 6, 8 };
        int x = 1, i = 0, bar = Mathf.Max(2, h / 3), y = (h - bar) / 2;
        while (x < w - 1)
        {
            int len = Mathf.Min(lens[i % lens.Length], w - 1 - x);
            Fill(g, x, y, len, bar, 'T');
            x += len + 3;
            i++;
        }
        return Make($"Word_TextRow_{w}_{h}", g, Pal(('T', Color.white)));
    }

    /// <summary>コメントの吹き出し（54×36＝3×2マス）。黄色い地、赤いふち（危ない物）、左下にしっぽ、灰色の字の行と丸いアイコン</summary>
    public static Sprite Comment => _comment != null ? _comment : (_comment = BuildComment());
    static Sprite _comment;

    static Sprite BuildComment()
    {
        {
            const int w = 54, h = 36;
            var g = Grid(w, h);
            Fill(g, 0, 0, w, h - 6, 'E');
            Fill(g, 1, 1, w - 2, h - 8, 'Y');
            // しっぽ（左下）
            for (int i = 0; i < 6; i++) Fill(g, 6, h - 6 + i, 6 - i, 1, 'E');
            for (int i = 0; i < 5; i++) Fill(g, 7, h - 7 + i, 4 - i, 1, 'Y');
            // アイコン（丸）と字の行
            Fill(g, 4, 4, 6, 6, 'B'); g[4, 4] = 'Y'; g[4, 9] = 'Y'; g[9, 4] = 'Y'; g[9, 9] = 'Y';
            Fill(g, 13, 5, 16, 2, 'K');
            Fill(g, 4, 14, 40, 2, 'T');
            Fill(g, 4, 19, 30, 2, 'T');
            Fill(g, 4, 24, 36, 2, 'T');
            return Make("Word_Comment", g, Pal(('E', Red), ('Y', CommentYellow), ('B', BlueLight), ('K', Ink), ('T', TextGrey)));
        }
    }

    /// <summary>大きい画像（72×54＝4×3マス）。白い板に、山と太陽の絵（画像の仮の絵）。赤いふち</summary>
    public static Sprite Image => _image != null ? _image : (_image = BuildImage());
    static Sprite _image;

    static Sprite BuildImage()
    {
        {
            const int w = 72, h = 54;
            var g = Grid(w, h);
            Fill(g, 0, 0, w, h, 'E');
            Fill(g, 2, 2, w - 4, h - 4, 'W');
            Fill(g, 5, 5, w - 10, h - 10, 'S');
            // 太陽
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                {
                    float dx = x - 4.5f, dy = y - 4.5f;
                    if (dx * dx + dy * dy <= 20f) g[9 + y, 50 + x] = 'U';
                }
            // 山（2つ）
            for (int x = 5; x < w - 5; x++)
            {
                int top1 = 24 + Mathf.Abs(x - 24) * 2 / 3;
                int top2 = 30 + Mathf.Abs(x - 50) / 2;
                int top = Mathf.Min(top1, top2);
                for (int y = Mathf.Max(5, top); y < h - 5; y++) g[y, x] = top == top1 ? 'G' : 'D';
            }
            return Make("Word_Image", g, Pal(('E', Red), ('W', Color.white), ('S', RuntimeSprites.Hex("BFE3FF")),
                ('U', RuntimeSprites.Hex("FFD60A")), ('G', RuntimeSprites.Hex("34C759")), ('D', RuntimeSprites.Hex("248A3D"))));
        }
    }

    /// <summary>押し出された文字の波（12×14。黒い字 A と赤い下のふち）</summary>
    public static Sprite TextWave => RuntimeSprites.FromAscii("Word_TextWave", new[]
    {
        "............",
        ".....KK.....",
        "....KKKK....",
        "....KK.KK...",
        "...KK..KK...",
        "...KK...KK..",
        "..KKKKKKKK..",
        "..KK....KK..",
        ".KK......KK.",
        ".KK......KK.",
        "RRRRRRRRRRRR",
        "RWWWWWWWWWWR",
        "RRRRRRRRRRRR",
        "............",
    }, Pal(('K', Ink), ('R', Red), ('W', Color.white)));

    /// <summary>太字の B（16×18。青い字に白いふち）</summary>
    public static Sprite BoldB => RuntimeSprites.FromAscii("Word_BoldB", new[]
    {
        "WWWWWWWWWWWW....",
        "WBBBBBBBBBBWW...",
        "WBBBBBBBBBBBWW..",
        "WBBBWWWWWBBBBW..",
        "WBBBW...WWBBBW..",
        "WBBBW....WBBBW..",
        "WBBBWWWWWBBBWW..",
        "WBBBBBBBBBBWW...",
        "WBBBBBBBBBBBWW..",
        "WBBBWWWWWWBBBWW.",
        "WBBBW....WWBBBW.",
        "WBBBW.....WBBBW.",
        "WBBBW.....WBBBW.",
        "WBBBWWWWWWBBBBW.",
        "WBBBBBBBBBBBBWW.",
        "WBBBBBBBBBBBWW..",
        "WWWWWWWWWWWWW...",
        "................",
    }, Pal(('W', Color.white), ('B', Blue)));
}
