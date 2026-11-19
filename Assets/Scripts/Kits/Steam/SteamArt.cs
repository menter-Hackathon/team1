using System.Collections.Generic;
using UnityEngine;

// 【仮】Steam のキットの絵（2026-11-19。docs/app-kits-2-design.md 2-3）。コードの中のドット絵だけ。絵のファイルは足さない。
// ・色：紺 #171A21・青 #66C0F4・セールの緑 #4C6B22 に黄緑の字 #BEEE11・金のトロフィー・ボスの紫
// ・字は 3×5 ドットの小さい字（SteamPix.Text）。本物のデータは出さない（名前は PLAYER 2 だけ）

/// <summary>Steam のキットの絵</summary>
public static class SteamArt
{
    public static readonly Color32 Navy = RuntimeSprites.Hex("171A21");
    public static readonly Color32 Navy2 = RuntimeSprites.Hex("1B2838");
    public static readonly Color32 Edge = RuntimeSprites.Hex("2A475E");
    public static readonly Color32 Blue = RuntimeSprites.Hex("66C0F4");
    public static readonly Color32 SaleGreen = RuntimeSprites.Hex("4C6B22");
    public static readonly Color32 Lime = RuntimeSprites.Hex("BEEE11");
    public static readonly Color32 Gold = RuntimeSprites.Hex("FFD24A");
    public static readonly Color32 GoldDark = RuntimeSprites.Hex("C8901E");
    public static readonly Color32 GoldShine = RuntimeSprites.Hex("FFF3B0");
    public static readonly Color32 Purple = RuntimeSprites.Hex("A86CFF");
    public static readonly Color32 PurpleDark = RuntimeSprites.Hex("5B2C9E");
    public static readonly Color32 Online = RuntimeSprites.Hex("90BA3C");
    public static readonly Color32 Gray = RuntimeSprites.Hex("8F98A0");
    public static readonly Color32 White = new Color32(255, 255, 255, 255);
    public static readonly Color32 Red = RuntimeSprites.Hex("FF453A");

    // ───────── 値札 ─────────

    /// <summary>値札（-50% など）。big は大きい -95%</summary>
    public static Sprite PriceTag(string text, bool big = false)
    {
        int s = big ? 3 : 2;
        int tw = SteamPix.TextWidth(text, s);
        int pad = big ? 6 : 3;
        int notch = big ? 7 : 4;
        int w = notch + tw + pad * 2, h = 5 * s + pad * 2;
        var p = new SteamPix(w, h);
        p.Round(notch - 2, 0, w - notch + 2, h, 1, SaleGreen);
        // 左の三角（値札の形）と穴
        for (int y = 0; y < h; y++)
        {
            int d = Mathf.Abs(y - h / 2);
            for (int x = Mathf.Max(0, d * notch / Mathf.Max(1, h / 2)); x < notch; x++) p.Set(x, y, SaleGreen);
        }
        p.Rect(notch - 1, h / 2 - 1, 2, 2, Navy);
        p.Rect(notch, 0, w - notch, 1, RuntimeSprites.Hex("6C8F2E"));
        p.Text(notch + pad, pad, text, Lime, s);
        return p.ToSprite($"Steam_Tag_{text}_{(big ? "B" : "S")}");
    }

    /// <summary>大きい値札が落ちたときの低い衝撃（黄緑の波）</summary>
    public static Sprite Shock => new SteamPix(18, 8).With(p =>
    {
        for (int x = 0; x < 18; x++)
        {
            int top = 7 - (int)(Mathf.Abs(Mathf.Sin(x * 0.55f)) * 6f);
            for (int y = top; y < 8; y++) p.Set(x, y, y == top ? White : Lime);
        }
    }).ToSprite("Steam_Shock");

    // ───────── トロフィー ─────────

    /// <summary>金のトロフィー（16ドット）</summary>
    public static Sprite Trophy => SteamPix.Ascii("Steam_Trophy", new[]
    {
        "..oooooooooooo..",
        ".o#####W#####o..",
        "oo##########%oo.",
        "o.o#########%.o.",
        "o.o#########%.o.",
        ".oo#########%oo.",
        "...o#######%o...",
        "....o#####%o....",
        ".....o###%o.....",
        "......o#%o......",
        "......o#%o......",
        ".....o##%%o.....",
        "....oooooooo....",
        "....o######o....",
        "...o########o...",
        "...oooooooooo...",
    }, new Dictionary<char, Color32> { { '#', Gold }, { '%', GoldDark }, { 'o', RuntimeSprites.Hex("6B4A10") }, { 'W', GoldShine } });

    /// <summary>トロフィーの小さい印（8ドット。HUD の印）</summary>
    public static Sprite TrophyIcon => SteamPix.Ascii("Steam_TrophyIcon", new[]
    {
        "o######o",
        "o######o",
        ".o####o.",
        "..o##o..",
        "...##...",
        "...##...",
        "..####..",
        ".######.",
    }, new Dictionary<char, Color32> { { '#', Gold }, { 'o', GoldDark } });

    /// <summary>剣（与えるゲージ。8ドット）</summary>
    public static Sprite Sword => SteamPix.Ascii("Steam_Sword", new[]
    {
        "......WW",
        ".....WWW",
        "....WWW.",
        ".o.WWW..",
        "..oWW...",
        "..oo....",
        ".o..o...",
        "o.......",
    }, new Dictionary<char, Color32> { { 'W', RuntimeSprites.Hex("E8EEF2") }, { 'o', GoldDark } });

    /// <summary>靴（速さ。8ドット）</summary>
    public static Sprite Boot => SteamPix.Ascii("Steam_Boot", new[]
    {
        "..####..",
        "..####..",
        "..####..",
        "..####..",
        "..#####.",
        ".#######",
        "########",
        "oooooooo",
    }, new Dictionary<char, Color32> { { '#', Blue }, { 'o', RuntimeSprites.Hex("2F6E94") } });

    /// <summary>ボスが強くなっている印（紫の上向きの矢印。9ドット）</summary>
    public static Sprite BossUp => SteamPix.Ascii("Steam_BossUp", new[]
    {
        "....#....",
        "...###...",
        "..#####..",
        ".#######.",
        "###WWW###",
        "...WWW...",
        "...WWW...",
        "...WWW...",
    }, new Dictionary<char, Color32> { { '#', Purple }, { 'W', RuntimeSprites.Hex("D9C2FF") } });

    // ───────── フレンドの通知 ─────────

    /// <summary>フレンドの通知（3×1マス。紺の四角・緑のふちの顔・PLAYER 2 NOW ONLINE）</summary>
    public static Sprite FriendCard => new SteamPix(54, 18).With(p =>
    {
        p.Round(0, 0, 54, 18, 1, Navy);
        p.Frame(0, 0, 54, 18, Edge);
        // 顔のアイコン（緑のふち）
        p.Rect(2, 3, 12, 12, Online);
        p.Rect(3, 4, 10, 10, RuntimeSprites.Hex("3D4450"));
        p.Rect(6, 6, 4, 4, RuntimeSprites.Hex("C6D4DF"));
        p.Rect(5, 10, 6, 3, RuntimeSprites.Hex("C6D4DF"));
        p.Text(17, 3, "PLAYER 2", White);
        p.Text(17, 10, "ONLINE", Online);
    }).ToSprite("Steam_FriendCard");

    /// <summary>ダウンロードの下向き矢印（10ドット）</summary>
    public static Sprite DownArrow => SteamPix.Ascii("Steam_Down", new[]
    {
        "...####...",
        "...####...",
        "...####...",
        "...####...",
        "##########",
        ".########.",
        "..######..",
        "...####...",
        "....##....",
        "##########",
    }, new Dictionary<char, Color32> { { '#', Blue } });

    // ───────── 実績の通知 ─────────

    /// <summary>
    /// 実績の通知（右下・左下）の絵。左にトロフィー、上に ACHIEVEMENT UNLOCKED、下に名前、右に効果の印。
    /// boss なら紫のトロフィーと紫のふち。badges は「アイコン・字」の組
    /// </summary>
    public static Sprite Toast(string name, bool boss, (Sprite icon, string text)[] badges)
    {
        const int pad = 4, iconW = 18, h = 28;
        string header = "ACHIEVEMENT UNLOCKED";
        int textW = Mathf.Max(SteamPix.TextWidth(header), SteamPix.TextWidth(name.ToUpperInvariant()));
        int badgeW = 0;
        if (badges != null) foreach (var b in badges) badgeW = Mathf.Max(badgeW, 10 + SteamPix.TextWidth(b.text));
        int w = pad + iconW + 5 + textW + (badgeW > 0 ? 6 + badgeW : 0) + pad;
        var p = new SteamPix(w, h);
        Color32 accent = boss ? Purple : Blue;
        // 地（上が少し明るい紺）とふち
        p.Round(0, 0, w, h, 2, Navy);
        p.Rect(1, 1, w - 2, h / 2, Navy2);
        p.Frame(0, 0, w, h, boss ? PurpleDark : Edge);
        p.Rect(0, h - 2, w, 2, accent);
        // トロフィー（ボスは紫に塗る）
        p.Rect(pad, (h - iconW) / 2 - 1, iconW, iconW, boss ? RuntimeSprites.Hex("2A1A44") : RuntimeSprites.Hex("2A2F38"));
        p.Blit(Trophy, pad + 1, (h - 16) / 2 - 1, boss ? (Color32?)Purple : null);
        int tx = pad + iconW + 5;
        p.Text(tx, 6, header, Gray);
        p.Text(tx, 15, name.ToUpperInvariant(), White);
        if (badges != null)
        {
            int bx = tx + textW + 6;
            for (int i = 0; i < badges.Length && i < 2; i++)
            {
                int by = 4 + i * 10;
                if (badges[i].icon != null) p.Blit(badges[i].icon, bx, by, null);
                p.Text(bx + 10, by + 2, badges[i].text, boss ? Purple : Lime);
            }
        }
        return p.ToSprite($"Steam_Toast_{name}_{(boss ? "B" : "P")}");
    }
}

/// <summary>コードで描くドット絵の板（Steam のキットの中だけで使う）。y は上から</summary>
public sealed class SteamPix
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    readonly int _w, _h;
    readonly Color32[] _px;

    public SteamPix(int w, int h)
    {
        _w = Mathf.Max(1, w);
        _h = Mathf.Max(1, h);
        _px = new Color32[_w * _h];
    }

    public int Width => _w;
    public int Height => _h;

    public SteamPix With(System.Action<SteamPix> draw)
    {
        draw?.Invoke(this);
        return this;
    }

    public void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= _w || y >= _h) return;
        _px[(_h - 1 - y) * _w + x] = c;
    }

    public void Rect(int x, int y, int w, int h, Color32 c)
    {
        for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
    }

    public void Frame(int x, int y, int w, int h, Color32 c)
    {
        Rect(x, y, w, 1, c);
        Rect(x, y + h - 1, w, 1, c);
        Rect(x, y, 1, h, c);
        Rect(x + w - 1, y, 1, h, c);
    }

    /// <summary>角を r ドット欠いた四角</summary>
    public void Round(int x, int y, int w, int h, int r, Color32 c)
    {
        for (int j = 0; j < h; j++)
        {
            int cut = 0;
            if (j < r) cut = r - j;
            else if (j >= h - r) cut = j - (h - r) + 1;
            Rect(x + cut, y + j, w - cut * 2, 1, c);
        }
    }

    /// <summary>スプライトを写す（色を変えるなら tint で塗りつぶす。透明は写さない）</summary>
    public void Blit(Sprite s, int x, int y, Color32? tint)
    {
        if (s == null || s.texture == null) return;
        Rect r = s.textureRect;
        Color32[] src = s.texture.GetPixels32();
        int tw = s.texture.width;
        for (int j = 0; j < (int)r.height; j++)
        {
            for (int i = 0; i < (int)r.width; i++)
            {
                Color32 c = src[((int)r.y + j) * tw + (int)r.x + i];
                if (c.a == 0) continue;
                if (tint.HasValue)
                {
                    float k = (c.r + c.g + c.b) / (3f * 255f);
                    Color32 t = tint.Value;
                    c = new Color32((byte)(t.r * (0.45f + k * 0.55f)), (byte)(t.g * (0.45f + k * 0.55f)), (byte)(t.b * (0.45f + k * 0.55f)), c.a);
                }
                Set(x + i, y + ((int)r.height - 1 - j), c);
            }
        }
    }

    /// <summary>小さい字（3×5）を書く。返り値は書いた幅</summary>
    public int Text(int x, int y, string s, Color32 c, int scale = 1)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int cx = x;
        foreach (char ch in s.ToUpperInvariant())
        {
            string g = Glyph(ch);
            for (int j = 0; j < 5; j++)
                for (int i = 0; i < 3; i++)
                    if (g[j * 3 + i] == '#') Rect(cx + i * scale, y + j * scale, scale, scale, c);
            cx += 4 * scale;
        }
        return cx - x - scale;
    }

    /// <summary>小さい字の幅</summary>
    public static int TextWidth(string s, int scale = 1) => string.IsNullOrEmpty(s) ? 0 : s.Length * 4 * scale - scale;

    public Sprite ToSprite(string key, Vector2? pivot = null)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
        {
            name = $"Kit_{key}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(_px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, _w, _h), pivot ?? new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        sprite.name = key;
        sprite.hideFlags = HideFlags.DontSave;
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>文字の並びの絵（RuntimeSprites.FromAscii と同じ。読み戻せるように Texture を読める形で作る）</summary>
    public static Sprite Ascii(string key, string[] rows, Dictionary<char, Color32> palette)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var p = new SteamPix(w, rows.Length);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (palette.TryGetValue(rows[y][x], out Color32 c)) p.Set(x, y, c);
        return p.ToSprite(key);
    }

    static string Glyph(char c)
    {
        switch (c)
        {
            case 'A': return ".#.#.#####.##.#";
            case 'B': return "##.#.###.#.###.";
            case 'C': return ".###..#..#...##";
            case 'D': return "##.#.##.##.###.";
            case 'E': return "####..##.#..###";
            case 'F': return "####..##.#..#..";
            case 'G': return ".###..#.##.#.##";
            case 'H': return "#.##.#####.##.#";
            case 'I': return "###.#..#..#.###";
            case 'J': return "..#..#..##.#.#.";
            case 'K': return "#.##.###.#.##.#";
            case 'L': return "#..#..#..#..###";
            case 'M': return "#.########.##.#";
            case 'N': return "##.#.##.##.##.#";
            case 'O': return ".#.#.##.##.#.#.";
            case 'P': return "##.#.###.#..#..";
            case 'Q': return ".#.#.##.###..##";
            case 'R': return "##.#.###.#.##.#";
            case 'S': return ".###...#...###.";
            case 'T': return "###.#..#..#..#.";
            case 'U': return "#.##.##.##.####";
            case 'V': return "#.##.##.##.#.#.";
            case 'W': return "#.##.########.#";
            case 'X': return "#.##.#.#.#.##.#";
            case 'Y': return "#.##.#.#..#..#.";
            case 'Z': return "###..#.#.#..###";
            case '0': return "####.##.##.####";
            case '1': return ".#.##..#..#.###";
            case '2': return "##...#.#.#..###";
            case '3': return "##...#.#...###.";
            case '4': return "#.##.####..#..#";
            case '5': return "####..##...###.";
            case '6': return ".###..####.####";
            case '7': return "###..#.#..#..#.";
            case '8': return "####.#####.####";
            case '9': return "####.####..###.";
            case '+': return "....#.###.#....";
            case '-': return "......###......";
            case '%': return "#.#..#.#.#..#.#";
            case '!': return ".#..#..#.....#.";
            case '.': return ".............#.";
            case ':': return "....#.....#....";
            case '/': return "..#..#.#.#..#..";
            case '#': return "#.#####.#####.#";
            default: return "...............";
        }
    }
}
