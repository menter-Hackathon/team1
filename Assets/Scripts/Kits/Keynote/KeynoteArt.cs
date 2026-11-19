using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 【仮】Keynote のキットの絵（2026-11-19。docs/app-kits-1-design.md 13章）。コードの中のドット絵。1マス＝18ドット。
/// 色：青 #0A84FF・黒・白・スポットライトのうすい黄 #FFF4B0。丸は青から紫へ
/// </summary>
public static class KeynoteArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("0A84FF");
    public static readonly Color Purple = RuntimeSprites.Hex("BF5AF2");
    public static readonly Color Spot = RuntimeSprites.Hex("FFF4B0");
    public static readonly Color Ink = RuntimeSprites.Hex("101014");

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    static Sprite Make(string key, char[,] g, Dictionary<char, Color32> pal)
    {
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        int h = g.GetLength(0), w = g.GetLength(1);
        var rows = new string[h];
        var sb = new StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Clear();
            for (int x = 0; x < w; x++) sb.Append(g[y, x]);
            rows[y] = sb.ToString();
        }
        s = RuntimeSprites.FromAscii(key, rows, pal);
        Cache[key] = s;
        return s;
    }

    static char[,] Grid(int w, int h)
    {
        var g = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[y, x] = '.';
        return g;
    }

    static Dictionary<char, Color32> Pal(params (char c, Color col)[] items)
    {
        var d = new Dictionary<char, Color32>();
        foreach (var (c, col) in items) d[c] = col;
        return d;
    }

    /// <summary>マジックムーブの丸（d ドット。白で作る。色は SpriteRenderer で付ける。左上に光、ふちは少し暗い）</summary>
    public static Sprite Ball(int d)
    {
        string key = $"Keynote_Ball_{d}";
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        var g = Grid(d, d);
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;
                char c = dist > r - 2f ? 'E' : 'W';
                float hx = dx + r * 0.35f, hy = dy + r * 0.35f;
                if (hx * hx + hy * hy < r * r * 0.06f) c = 'H';
                g[y, x] = c;
            }
        return Make(key, g, Pal(('E', new Color(0.72f, 0.72f, 0.8f)), ('W', Color.white), ('H', Color.white)));
    }

    /// <summary>赤い点線の輪（d ドット、太さ 2）。行き先の予告</summary>
    public static Sprite DottedRing(int d)
    {
        string key = $"Keynote_DotRing_{d}";
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        var g = Grid(d, d);
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r || dist < r - 2f) continue;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                if (Mathf.FloorToInt(ang / 15f) % 2 == 0) g[y, x] = 'R';
            }
        return Make(key, g, Pal(('R', KitDress.Red)));
    }

    // 5×7 の数字
    static readonly string[][] Digits =
    {
        new[] { ".XXX.", "XX.XX", "XX.XX", "XX.XX", "XX.XX", "XX.XX", ".XXX." },
        new[] { "..XX.", ".XXX.", "XXXX.", "..XX.", "..XX.", "..XX.", "XXXXX" },
        new[] { ".XXX.", "XX.XX", "...XX", "..XX.", ".XX..", "XX...", "XXXXX" },
        new[] { "XXXX.", "...XX", "...XX", ".XXX.", "...XX", "...XX", "XXXX." },
    };

    /// <summary>大きい数字（45×54＝2.5×3マス）。白い字に赤いふち（危ない物）と黒い影。n は 0〜3</summary>
    public static Sprite BigNumber(int n)
    {
        n = Mathf.Clamp(n, 0, Digits.Length - 1);
        string key = $"Keynote_Num_{n}";
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        const int w = 45, h = 54, sc = 7;
        var g = Grid(w, h);
        string[] dg = Digits[n];
        int ox = (w - 5 * sc) / 2, oy = (h - 7 * sc) / 2;
        // 影（右下に 2ドット）
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 5; x++)
                if (dg[y][x] == 'X')
                    for (int yy = 0; yy < sc; yy++)
                        for (int xx = 0; xx < sc; xx++)
                        {
                            int px = ox + x * sc + xx + 2, py = oy + y * sc + yy + 2;
                            if (px < w && py < h) g[py, px] = 'S';
                        }
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 5; x++)
                if (dg[y][x] == 'X')
                    for (int yy = 0; yy < sc; yy++)
                        for (int xx = 0; xx < sc; xx++) g[oy + y * sc + yy, ox + x * sc + xx] = 'W';
        // ふち
        var edge = new List<(int, int)>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (g[y, x] == 'W') continue;
                bool near = (x > 0 && g[y, x - 1] == 'W') || (x < w - 1 && g[y, x + 1] == 'W') || (y > 0 && g[y - 1, x] == 'W') || (y < h - 1 && g[y + 1, x] == 'W');
                if (near) edge.Add((x, y));
            }
        foreach (var (x, y) in edge) g[y, x] = 'R';
        return Make(key, g, Pal(('W', Color.white), ('R', KitDress.Red), ('S', new Color(0f, 0f, 0f, 0.55f))));
    }

    /// <summary>白い星の弾（11×11。うすい赤のふち）</summary>
    public static Sprite Star => RuntimeSprites.FromAscii("Keynote_Star", new[]
    {
        ".....R.....",
        "....RWR....",
        "....RWR....",
        "RRRRWWWRRRR",
        ".RWWWWWWWR.",
        "..RWWWWWR..",
        "..RWWWWWR..",
        ".RWWWRWWWR.",
        ".RWWR.RWWR.",
        "RWRR...RRWR",
        "RR.......RR",
    }, Pal(('R', RuntimeSprites.Hex("FF8A80")), ('W', Color.white)));

    /// <summary>観客の頭の影（12×8。1人分）。raised＝手が上がっている（16×14）</summary>
    public static Sprite Audience(bool raised) => raised
        ? RuntimeSprites.FromAscii("Keynote_AudienceUp", new[]
        {
            ".K..........K.",
            ".KK........KK.",
            "..KK......KK..",
            "...KK....KK...",
            "....K.KK.K....",
            ".....KKKK.....",
            ".....KKKK.....",
            "......KK......",
            "...KKKKKKKK...",
            "..KKKKKKKKKK..",
            "..KKKKKKKKKK..",
            "..KKKKKKKKKK..",
        }, Pal(('K', Color.white)))
        : RuntimeSprites.FromAscii("Keynote_Audience", new[]
        {
            "..............",
            "..............",
            "..............",
            "..............",
            "......KK......",
            ".....KKKK.....",
            ".....KKKK.....",
            "......KK......",
            "...KKKKKKKK...",
            "..KKKKKKKKKK..",
            "..KKKKKKKKKK..",
            "..KKKKKKKKKK..",
        }, Pal(('K', Color.white)));

    /// <summary>紙吹雪の色</summary>
    public static readonly Color[] Confetti =
    {
        RuntimeSprites.Hex("0A84FF"), RuntimeSprites.Hex("FF9F0A"), RuntimeSprites.Hex("30D158"),
        RuntimeSprites.Hex("FFD60A"), RuntimeSprites.Hex("BF5AF2"), RuntimeSprites.Hex("FF375F"),
    };
}
