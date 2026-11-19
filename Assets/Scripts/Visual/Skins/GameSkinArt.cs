using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

// 【仮】Game のスキンの色と絵（2026-11-18。docs/app-skill-visuals-design.md 6章）。Steam・Minecraft Launcher・Epic Games Launcher。
// 照準・コントローラーのボタン（A B X Y）・実績の通知・ライブラリのタイルと進みのバー・コンボの数字・ドット絵の爆発。
// 型の名前は GameArt（今ある絵の入れ物）とぶつからないように GameSkinArt にする。
public static class GameSkinArt
{
    // ───────── 色（6章） ─────────
    public static readonly Color Navy = K.Hex("1B2838");
    public static readonly Color NavyLight = K.Hex("2A475E");
    public static readonly Color Sky = K.Hex("66C0F4");
    public static readonly Color BtnA = K.Hex("6DC849");
    public static readonly Color BtnB = K.Hex("E8452F");
    public static readonly Color BtnX = K.Hex("3D8BE8");
    public static readonly Color BtnY = K.Hex("F5C400");
    public static readonly Color Gold = K.Hex("FFC83D");
    public static readonly Color Yellow = K.Hex("FFD60A");
    public static readonly Color Orange = K.Hex("FF9500");
    public static readonly Color Smoke = K.Hex("8E8E93");
    public static readonly Color PlayGreen = K.Hex("5BA32B");

    public static readonly Color[] ButtonColors = { BtnA, BtnB, BtnX, BtnY };
    public static readonly string[] ButtonLetters = { "A", "B", "X", "Y" };

    // ───────── 手で描いた絵 ─────────

    /// <summary>爆弾（13×13。K＝黒、H＝光、D＝影、G＝口金、F＝導火線、S＝火花（コマで色が変わる）、Y＝火花のまん中）</summary>
    static readonly string[] BombArt =
    {
        "...........S.",
        "..........SYS",
        ".......GGFFS.",
        "...KKKKGG....",
        "..KKKKKKK....",
        ".KKHHKKKKK...",
        ".KKHKKKKKK...",
        ".KKKKKKKKK...",
        ".KKKKKKKKD...",
        ".KKKKKKKKD...",
        "..KKKKKKD....",
        "...KKKDD.....",
        ".............",
    };

    /// <summary>ボタンの印（8×8。丸に B）</summary>
    static readonly string[] BButtonArt =
    {
        "..RRRR..",
        ".RRWWRR.",
        "RRRWRWRR",
        "RRRWWRRR",
        "RRRWRWRR",
        "RRRWWRRR",
        ".RRRRRR.",
        "..RRRR..",
    };

    /// <summary>ドット絵のミサイル（22×7。右が前。2コマで炎がゆれる。ふちはコードで足す）</summary>
    static readonly string[] MissileArtA =
    {
        "......................",
        "...GG.................",
        "..OGGWWWRWWWWWWWWWRR..",
        ".OYYWWWWRWWWWWWWWWRRR.",
        "..OGGWWWRWWWWWWWWWRR..",
        "...GG.................",
        "......................",
    };

    static readonly string[] MissileArtB =
    {
        "......................",
        "...GG.................",
        ".OOGGWWWRWWWWWWWWWRR..",
        "OYYYWWWWRWWWWWWWWWRRR.",
        ".OOGGWWWRWWWWWWWWWRR..",
        "...GG.................",
        "......................",
    };

    /// <summary>金のトロフィー（16×16）</summary>
    static readonly string[] TrophyArt =
    {
        "................",
        "...GGGGGGGGGG...",
        "GGGGWGGGGGGGDGGG",
        "G..GWGGGGGGGD..G",
        "G..GWGGGGGGGD..G",
        ".G.GWGGGGGGGD.G.",
        "..GGGWGGGGGGDG..",
        "....GWGGGGGD....",
        ".....GGGGGD.....",
        "......GGGD......",
        ".......GD.......",
        ".......GD.......",
        "......GGGD......",
        ".....BBBBBB.....",
        "....BBBBBBBB....",
        "................",
    };

    /// <summary>ハート（14×12）</summary>
    static readonly string[] HeartArt =
    {
        "..RRR....RRR..",
        ".RRRRR..RRRRR.",
        "RRWWRRRRRRRRRR",
        "RRWRRRRRRRRRRR",
        "RRRRRRRRRRRRRR",
        "RRRRRRRRRRRRRD",
        ".RRRRRRRRRRRD.",
        "..RRRRRRRRRD..",
        "...RRRRRRRD...",
        "....RRRRRD....",
        ".....RRRD.....",
        "......RD......",
    };

    // ───────── 絵を作る ─────────

    public static Sprite Bomb(int frame)
    {
        frame &= 1;
        string key = $"Game_Bomb_{frame}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(15, 15);
            K.Stamp(g, BombArt, 1, 1);
            K.Outline(g, 'E'); // 白いふち（プレイヤーの攻撃）
            return K.Make(key, g, K.Pal(('K', K.Hex("1A1A1C")), ('H', K.Hex("9A9AA0")), ('D', K.Hex("3A3A3C")),
                ('G', K.Hex("8E8E93")), ('F', K.Hex("C8A060")), ('Y', frame == 0 ? Yellow : Orange),
                ('S', frame == 0 ? Color.white : Yellow), ('E', Color.white)));
        });
    }

    static Sprite _bButton, _trophy, _heart;

    public static Sprite BButton => _bButton != null ? _bButton : (_bButton = K.Make("Game_BButton", BButtonArt, K.Pal(('R', BtnB), ('W', Color.white))));

    public static Sprite Trophy => _trophy != null ? _trophy : (_trophy = K.Make("Game_Trophy", TrophyArt,
        K.Pal(('G', Gold), ('W', K.Hex("FFF1B0")), ('D', K.Hex("C8902A")), ('B', K.Hex("6B4F1D")))));

    public static Sprite Heart => _heart != null ? _heart : (_heart = K.Make("Game_Heart", HeartArt,
        K.Pal(('R', K.Hex("FF3B30")), ('W', K.Hex("FFB3AE")), ('D', K.Hex("B3261E")))));

    /// <summary>割れたハートの片方（left＝左。ぎざぎざに割る）</summary>
    public static Sprite HeartHalf(bool left)
    {
        string key = $"Game_HeartHalf_{left}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(14, 12);
            K.Stamp(g, HeartArt, 0, 0);
            for (int y = 0; y < 12; y++)
            {
                int split = 7 + (y % 3 == 1 ? -1 : y % 3 == 2 ? 1 : 0);
                for (int x = 0; x < 14; x++)
                    if (left ? x >= split : x < split) g[y, x] = '.';
            }
            return K.Make(key, g, K.Pal(('R', K.Hex("FF3B30")), ('W', K.Hex("FFB3AE")), ('D', K.Hex("B3261E"))));
        });
    }

    public static Sprite Missile(int frame)
    {
        frame &= 1;
        string key = $"Game_Missile_{frame}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(22, 7);
            K.Stamp(g, frame == 0 ? MissileArtA : MissileArtB, 0, 0);
            K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('W', K.Hex("F2F2F7")), ('R', K.Hex("E8452F")), ('G', K.Hex("8E8E93")),
                ('O', Orange), ('Y', Yellow), ('E', K.EnemyEdge)));
        });
    }

    /// <summary>コントローラーのボタン（直径 11、色と白い字。i＝0:A 1:B 2:X 3:Y）。ふちは敵＝赤、プレイヤー＝白</summary>
    public static Sprite Button(int i, bool enemy, Color? fillOverride = null, int d = 11)
    {
        i = ((i % 4) + 4) % 4;
        Color fill = fillOverride ?? ButtonColors[i];
        string key = $"Game_Btn_{i}_{enemy}_{K.Key(fill)}_{d}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            K.Disc(g, d * 0.5f, d * 0.5f, d * 0.5f, 'E');
            K.Disc(g, d * 0.5f, d * 0.5f, d * 0.5f - 1f, 'F');
            K.Fill(g, 3, 2, d - 7, 1, 'L'); // 上の光
            K.DrawText(g, ButtonLetters[i], (d - 3) / 2, (d - 5) / 2 + (d % 2 == 0 ? 0 : 0), 'W', 1);
            return K.Make(key, g, K.Pal(('E', K.Edge(enemy)), ('F', fill), ('L', K.Lighten(fill, 0.35f)), ('W', Color.white)));
        });
    }

    /// <summary>暗い（光っていない）ボタン（予告の十字に並べる）</summary>
    public static Sprite DimButton(int i, int d = 9)
    {
        i = ((i % 4) + 4) % 4;
        string key = $"Game_DimBtn_{i}_{d}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            K.Disc(g, d * 0.5f, d * 0.5f, d * 0.5f, 'E');
            K.Disc(g, d * 0.5f, d * 0.5f, d * 0.5f - 1f, 'F');
            K.DrawText(g, ButtonLetters[i], (d - 3) / 2, (d - 5) / 2, 'W', 1);
            return K.Make(key, g, K.Pal(('E', K.Hex("3A3A3C")), ('F', K.Darken(ButtonColors[i], 0.55f)), ('W', K.Hex("8E8E93"))));
        });
    }

    /// <summary>照準（直径 d、太さ1の輪と、十字の目盛り、まん中の点）</summary>
    public static Sprite Crosshair(int d, Color c)
    {
        d = Mathf.Max(9, d);
        string key = $"Game_Cross_{d}_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d + 6, d + 6);
            float cx = (d + 6) * 0.5f, r = d * 0.5f;
            for (int y = 0; y < d + 6; y++)
                for (int x = 0; x < d + 6; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r && dist > r - 1.2f) g[y, x] = 'C';
                }
            int c0 = (d + 6) / 2;
            bool even = (d + 6) % 2 == 0;
            int len = 4;
            for (int k = 0; k < len; k++)
            {
                // 上下左右の目盛り（輪をまたぐ）
                int o = Mathf.RoundToInt(r) - 1 + k;
                K.Set(g, c0, c0 - 1 - o + (even ? 0 : 1), 'C'); K.Set(g, c0, c0 + o, 'C');
                K.Set(g, c0 - 1 - o + (even ? 0 : 1), c0, 'C'); K.Set(g, c0 + o, c0, 'C');
            }
            K.Fill(g, c0 - 1, c0 - 1, 2, 2, 'C');
            return K.Make(key, g, K.Pal(('C', c)));
        });
    }

    /// <summary>ぎざぎざの星（直径 d。8つのとげ。黄色にオレンジのふち）</summary>
    public static Sprite Star(int d, Color fill, Color edge)
    {
        string key = $"Game_Star_{d}_{K.Key(fill)}_{K.Key(edge)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            float c = d * 0.5f, R = d * 0.5f - 1f, rIn = R * 0.52f;
            var poly = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                float a = (i * 22.5f + 11.25f) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? R : rIn;
                poly[i] = new Vector2(c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r);
            }
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                    if (Inside(poly, new Vector2(x + 0.5f, y + 0.5f))) g[y, x] = 'F';
            K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('F', fill), ('E', edge)));
        });
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>ぎざぎざの輪（直径 d。爆発の3段目）</summary>
    public static Sprite JagRing(int d, Color c)
    {
        d = Mathf.Max(8, d);
        string key = $"Game_Jag_{d}_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            float cx = d * 0.5f, R = d * 0.5f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 12f; // 12のとげ
                    float tri = Mathf.Abs(a - Mathf.Floor(a) - 0.5f) * 2f;   // 0〜1
                    float outer = R * (0.8f + 0.2f * tri);
                    if (dist <= outer && dist > outer - 3f) g[y, x] = 'C';
                }
            return K.Make(key, g, K.Pal(('C', c)));
        });
    }

    /// <summary>実績の通知（54×36。暗い紺、金のトロフィー、ACHIEVEMENT）。redEdge＝敵の赤いふち、platform＝足場（上の端を白く）</summary>
    public static Sprite Achievement(bool redEdge, bool platform)
    {
        string key = $"Game_Ach_{redEdge}_{platform}";
        return K.Cached(key, () =>
        {
            const int w = 54, h = 36;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'N');
            K.Fill(g, 1, 1, w - 2, 2, 'L');                 // 上の明るい帯
            K.Stamp(g, TrophyArt, 4, 5);                    // トロフィー
            K.Fill(g, 23, 8, 26, 2, 'w');                   // 題名（白い棒）
            K.Fill(g, 23, 13, 18, 2, 'g');                  // 説明（灰色の棒）
            K.Fill(g, 23, 18, 22, 2, 'g');
            K.Fill(g, 3, 24, w - 6, 1, 'L');                // 区切り
            K.DrawText(g, "ACHIEVEMENT", (w - K.TextWidth("ACHIEVEMENT")) / 2, 27, 'S', 1);
            K.Border(g, 0, 0, w, h, 'E');
            if (platform) K.Fill(g, 1, 0, w - 2, 1, 'H');
            return K.Make(key, g, K.Pal(('N', Navy), ('L', NavyLight), ('w', Color.white), ('g', K.Hex("8F98A0")), ('S', Sky),
                ('E', redEdge ? K.EnemyEdge : K.Hex("4C6B82")), ('H', Color.white),
                ('G', Gold), ('W', K.Hex("FFF1B0")), ('D', K.Hex("C8902A")), ('B', K.Hex("6B4F1D"))));
        });
    }

    /// <summary>ライブラリのタイル（54×36。絵の代わりの山と太陽、下に進みのバーと緑の再生ボタン）。full＝バーが満タン</summary>
    public static Sprite LibraryTile(bool full)
    {
        string key = $"Game_Tile_{full}";
        return K.Cached(key, () =>
        {
            const int w = 54, h = 36;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'N');
            K.Fill(g, 3, 3, w - 6, 20, 'A');               // 絵の地（空）
            K.Disc(g, 40.5f, 9.5f, 3.6f, 'Y');            // 太陽
            for (int y = 0; y < 12; y++)                   // 山（2つ。絵の地の中だけ）
            {
                int half1 = y + 1, half2 = (int)(y * 0.8f) + 1;
                if (11 + y <= 22) K.Fill(g, 17 - half1, 11 + y, half1 * 2, 1, 'M');
                if (13 + y <= 22) K.Fill(g, 33 - half2, 13 + y, half2 * 2, 1, 'P');
            }
            K.Fill(g, 3, 23, w - 6, 1, 'N');
            K.Fill(g, 3, 28, 26, 3, 'T');                  // 進みのバー
            K.Fill(g, 3, 28, full ? 26 : 16, 3, 'S');
            K.RoundRect(g, 32, 26, 19, 7, 'B');            // 緑の再生ボタン（▶ はドット絵。字は出さない）
            for (int i = 0; i < 3; i++) K.Fill(g, 39 + i, 27 + i, 1, 5 - i * 2, 'W');
            K.Border(g, 0, 0, w, h, 'E');
            return K.Make(key, g, K.Pal(('N', Navy), ('A', NavyLight), ('Y', Yellow), ('M', K.Hex("3D6E8C")), ('P', K.Hex("5A93B8")),
                ('T', K.Hex("0E1621")), ('S', Sky), ('B', PlayGreen), ('W', Color.white), ('E', K.EnemyEdge)));
        });
    }

    /// <summary>ダウンロードの列の板（27×11。左に小さい絵、右に進みのバー。p＝0〜8 の満ち具合）</summary>
    public static Sprite DownloadTile(int p, int art)
    {
        p = Mathf.Clamp(p, 0, 8);
        art = ((art % 4) + 4) % 4;
        string key = $"Game_DL_{p}_{art}";
        return K.Cached(key, () =>
        {
            const int w = 27, h = 11;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'N');
            K.Fill(g, 2, 2, 7, 7, 'A');                   // 小さい絵（ゲームのアイコン）
            K.Fill(g, 3, 6, 5, 2, 'M');
            K.Fill(g, 6, 3, 2, 2, 'Y');
            K.Fill(g, 11, 3, 10, 1, 'W');                 // 題名
            K.Fill(g, 11, 6, 13, 2, 'T');                 // 進みのバー
            K.Fill(g, 11, 6, Mathf.RoundToInt(13f * p / 8f), 2, 'S');
            K.Border(g, 0, 0, w, h, 'E');
            return K.Make(key, g, K.Pal(('N', Navy), ('A', ButtonColors[art]), ('M', K.Darken(ButtonColors[art], 0.4f)), ('Y', Color.white),
                ('W', Color.white), ('T', K.Hex("0E1621")), ('S', Sky), ('E', K.EnemyEdge)));
        });
    }

    /// <summary>溶岩のブロック（w×h ドット。6ドットのます目。frame で明るい所が入れ替わる）。dark＝黒く固まった。white＝判定の瞬間に光る</summary>
    public static Sprite Lava(int w, int h, int frame, bool dark, bool white = false)
    {
        w = Mathf.Max(6, w); h = Mathf.Max(4, h);
        frame &= 1;
        string key = $"Game_Lava_{w}_{h}_{frame}_{dark}_{white}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(w, h);
            for (int x = 0; x < w; x++)
            {
                int bx = x / 6;
                bool a = (bx + frame) % 2 == 0;
                for (int y = 1; y < h; y++) g[y, x] = a ? 'A' : 'B';
                if (x % 6 == 0) for (int y = 1; y < h; y++) g[y, x] = 'L';          // ます目の線
                if (x % 6 == 1 || x % 6 == 2) g[Mathf.Min(h - 1, 2), x] = 'H';      // 光
                g[0, x] = (x + frame) % 3 == 0 ? 'T' : '.';                          // 上のあわ
            }
            Color A = dark ? K.Hex("3A3A3C") : K.Hex("E8452F"), B = dark ? K.Hex("2C2C2E") : Orange;
            if (white) { A = K.Lighten(A, 0.6f); B = K.Lighten(B, 0.6f); }
            return K.Make(key, g, K.Pal(('A', A), ('B', B), ('L', dark ? K.Hex("1C1C1E") : K.Hex("B3261E")),
                ('H', dark ? K.Hex("6B2A20") : Yellow), ('T', K.Alpha(dark ? K.Hex("48484A") : K.Hex("FFB340"), 0.85f))));
        });
    }

    /// <summary>ロード中の輪（直径 16、8つに分けて1つだけ明るい。phase で回る）</summary>
    public static Sprite LoadingRing(int phase, bool red = false)
    {
        phase = ((phase % 8) + 8) % 8;
        string key = $"Game_Load_{phase}_{red}";
        return K.Cached(key, () =>
        {
            const int d = 16;
            var g = K.Grid(d, d);
            float c = d * 0.5f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > c || dist < c - 3f) continue;
                    float ang = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    int seg = Mathf.FloorToInt(ang / 45f);
                    if ((ang % 45f) < 6f) continue; // すき間
                    int back = (phase - seg + 8) % 8;
                    g[y, x] = back == 0 ? 'A' : back == 1 ? 'B' : 'C';
                }
            Color tint = red ? K.EnemyEdge : Sky;
            return K.Make(key, g, K.Pal(('A', Color.white), ('B', K.Alpha(tint, 0.8f)), ('C', K.Alpha(tint, 0.3f))));
        });
    }

    /// <summary>かけら：ボタンの丸（4色）と金の四角</summary>
    public static Sprite[] Bits()
    {
        return new[]
        {
            K.DiscSprite(4, BtnA), K.DiscSprite(4, BtnB), K.DiscSprite(4, BtnX), K.DiscSprite(4, BtnY),
            Square(3, Gold), Square(2, Gold),
        };
    }

    public static Sprite Square(int s, Color c)
    {
        string key = $"Game_Sq_{s}_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(s, s);
            K.Fill(g, 0, 0, s, s, 'S');
            return K.Make(key, g, K.Pal(('S', c)));
        });
    }
}
