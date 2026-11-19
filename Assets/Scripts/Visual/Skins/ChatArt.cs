using UnityEngine;
using K = OfficeGameChatKit;

// 【仮】Chat のスキンの色と絵（2026-11-18。docs/app-skill-visuals-design.md 7章）。Slack・Discord・LINE・メッセージ・Teams・Zoom（ボスのとき）。
// 吹き出し・赤い通知バッジ・「入力中…」の3つの点・メンション（@）・未読の赤い線・既読のチェック・リアクション。
// アプリの色（Slack の紫、Discord の青紫、LINE の緑）を吹き出しに使う。
public static class ChatArt
{
    // ───────── 色（7章） ─────────
    public static readonly Color Green = K.Hex("34C759");
    public static readonly Color Blue = K.Hex("0A84FF");
    public static readonly Color Grey = K.Hex("E5E5EA");
    public static readonly Color GreyDark = K.Hex("8E8E93");
    public static readonly Color BadgeRed = K.Hex("FF3B30");
    public static readonly Color MentionYellow = K.Hex("FFF3B8");
    public static readonly Color MentionOrange = K.Hex("F2A33A");
    public static readonly Color MentionInk = K.Hex("B25E00");
    public static readonly Color Ink = K.Hex("1C1C1E");
    public static readonly Color Yellow = K.Hex("FFCC00");

    /// <summary>アプリの色が無いとき（メッセージの青）</summary>
    public static Color App(AppTheme theme) => K.Main(theme, Blue);

    // ───────── 手で描いた絵 ─────────

    /// <summary>メッセージの吹き出し（22×9。A＝アプリの色、W＝白い棒。後ろ＝左下にしっぽ。ふちはコードで足す）。右が前</summary>
    static readonly string[] MessageArt =
    {
        "......................",
        "...AAAAAAAAAAAAAAAAA..",
        "..AAAAAAAAAAAAAAAAAAA.",
        "..AAWWWWWWWWWWWWAAAAA.",
        "..AAAAAAAAAAAAAAAAAAA.",
        "..AAWWWWWWWWAAAAAAAAA.",
        ".AAAAAAAAAAAAAAAAAAA..",
        ".A....................",
        "......................",
    };

    /// <summary>メンション（9×9。黄色い地の @、左にオレンジの縦の線）</summary>
    static readonly string[] MentionArt =
    {
        "OYYYYYYYY",
        "OYYKKKKYY",
        "OYKYYYYKY",
        "OKYYKKYKY",
        "OKYKYKYKY",
        "OKYYKKKKY",
        "OYKYYYYYY",
        "OYYKKKKYY",
        "OYYYYYYYY",
    };

    /// <summary>既読のチェック2つ（12×5）</summary>
    static readonly string[] ReadArt =
    {
        "......A....A",
        ".....AA...AA",
        "A...AA...AA.",
        "AA.AAAA.AA..",
        ".AAA..AAA...",
    };

    static readonly string[] HeartArt =
    {
        ".RR.RR.",
        "RRRRRRR",
        "RRRRRRR",
        ".RRRRR.",
        "..RRR..",
        "...R...",
    };

    static readonly string[] ThumbArt =
    {
        "...YY...",
        "...YY...",
        "..YYY...",
        "BBYYYYYY",
        "BBYYYYYD",
        "BBYYYYYY",
        "BBYYYYYD",
        "BBYYYYY.",
    };

    static readonly string[] StarArt =
    {
        "...Y...",
        "...Y...",
        "YYYYYYY",
        ".YYYYY.",
        "..YYY..",
        ".YY.YY.",
        ".Y...Y.",
    };

    /// <summary>紙飛行機（12×11。「送信」の印。W＝白、S＝アプリの色の影）。右が前</summary>
    static readonly string[] PlaneArt =
    {
        "W...........",
        "WWW.........",
        ".WWWW.......",
        ".WWWWWW.....",
        "..WWWWWWW...",
        "..WWWWWWWWWW",
        "..SSSSSSSS..",
        ".SSSSSS.....",
        ".SSSS.......",
        "SSS.........",
        "S...........",
    };

    // ───────── 絵を作る ─────────

    /// <summary>
    /// 通知バッジ（高さ 10。1字は丸、2字以上は横長）。fill＝地の色、edge＝ふち（敵＝赤、プレイヤー＝白）
    /// </summary>
    public static Sprite Badge(string text, Color fill, Color edge)
    {
        text = text ?? "";
        string key = $"Chat_Badge_{text}_{K.Key(fill)}_{K.Key(edge)}";
        return K.Cached(key, () =>
        {
            int tw = K.TextWidth(text);
            int h = 10, w = Mathf.Max(10, tw + 6);
            var g = K.Grid(w, h);
            Pill(g, 0, 0, w, h, 'E');
            Pill(g, 1, 1, w - 2, h - 2, 'F');
            K.DrawText(g, text, (w - tw) / 2, 2, 'W', 1);
            return K.Make(key, g, K.Pal(('E', edge), ('F', fill), ('W', Color.white)));
        });
    }

    /// <summary>角の丸い横長（両はしが半円）</summary>
    static void Pill(char[,] g, int x, int y, int w, int h, char c)
    {
        float r = h * 0.5f;
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float px = i + 0.5f, py = j + 0.5f;
                float cx = Mathf.Clamp(px, r, w - r);
                float dx = px - cx, dy = py - r;
                if (dx * dx + dy * dy <= r * r) K.Set(g, x + i, y + j, c);
            }
    }

    public static Sprite Message(Color app, bool enemy)
    {
        string key = $"Chat_Msg_{K.Key(app)}_{enemy}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(22, 9);
            K.Stamp(g, MessageArt, 0, 0);
            K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('A', app), ('W', Color.white), ('E', K.Edge(enemy))));
        });
    }

    /// <summary>メンションの印（@）。edge を入れると、まわりを1ドットふちどる（敵＝赤、プレイヤー＝白）</summary>
    public static Sprite Mention(bool enemy)
    {
        string key = $"Chat_Mention_{enemy}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(11, 11);
            K.Stamp(g, MentionArt, 1, 1);
            K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('O', MentionOrange), ('Y', MentionYellow), ('K', MentionInk), ('E', K.Edge(enemy))));
        });
    }

    /// <summary>既読のチェック2つ（白いふち）</summary>
    public static Sprite Read(Color c)
    {
        string key = $"Chat_Read_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(14, 7);
            K.Stamp(g, ReadArt, 1, 1);
            K.Outline(g, 'W', true);
            return K.Make(key, g, K.Pal(('A', c), ('W', Color.white)));
        });
    }

    static Sprite _heart, _thumb, _star;
    public static Sprite Heart => _heart != null ? _heart : (_heart = K.Make("Chat_Heart", HeartArt, K.Pal(('R', BadgeRed))));
    public static Sprite Thumb => _thumb != null ? _thumb : (_thumb = K.Make("Chat_Thumb", ThumbArt, K.Pal(('Y', Yellow), ('D', K.Hex("D9A400")), ('B', Blue))));
    public static Sprite Star => _star != null ? _star : (_star = K.Make("Chat_Star", StarArt, K.Pal(('Y', Yellow))));

    /// <summary>リアクション（ハート・親指・星）</summary>
    public static Sprite[] Reactions => new[] { Heart, Thumb, Star };

    public static Sprite Plane(Color app)
    {
        string key = $"Chat_Plane_{K.Key(app)}";
        return K.Cached(key, () => K.Make(key, PlaneArt, K.Pal(('W', Color.white), ('S', K.Darken(app, 0.15f)))));
    }

    /// <summary>
    /// 角の丸い吹き出し（w×h ドット。下の左か右にしっぽ）。中に @ と白い棒（mention）か、白い棒だけ。
    /// fill＝地の色、line＝ふち（暗い色にすると、積み重ねたときに区切りが見える）
    /// </summary>
    public static Sprite Bubble(int w, int h, Color fill, bool tailRight, bool mention, Color line, bool empty = false)
    {
        string key = $"Chat_Bubble_{w}_{h}_{K.Key(fill)}_{tailRight}_{mention}_{K.Key(line)}_{empty}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(w, h);
            int bh = h - 2;
            K.Fill(g, 1, 0, w - 2, bh, 'F');
            K.Set(g, 1, 0, '.'); K.Set(g, w - 2, 0, '.'); K.Set(g, 1, bh - 1, '.'); K.Set(g, w - 2, bh - 1, '.');
            // しっぽ
            int tx = tailRight ? w - 4 : 2;
            K.Fill(g, tx, bh, 2, 1, 'F');
            K.Set(g, tailRight ? w - 2 : 1, bh + 1, 'F');
            Color ink = K.Near(fill, Grey, 0.12f) ? GreyDark : Color.white;
            int cx = 4;
            if (mention)
            {
                K.DrawText(g, "@", cx, Mathf.Max(1, (bh - 5) / 2), 'I', 1);
                cx += 7;
            }
            int barW = Mathf.Max(2, w - cx - 4);
            if (!empty) K.Fill(g, cx, Mathf.Max(1, bh / 2 - 1), barW, 2, 'I');
            K.Outline(g, 'L');
            return K.Make(key, g, K.Pal(('F', fill), ('I', ink), ('L', line)));
        });
    }

    /// <summary>「入力中…」の吹き出し（16×9。灰色。点は子の絵で跳ねさせる）</summary>
    public static Sprite TypingBubble()
    {
        const string key = "Chat_Typing";
        return K.Cached(key, () =>
        {
            var g = K.Grid(16, 9);
            K.Fill(g, 1, 0, 14, 7, 'G');
            K.Fill(g, 0, 1, 16, 5, 'G');
            K.Fill(g, 2, 7, 2, 1, 'G');
            K.Set(g, 1, 8, 'G');
            return K.Make(key, g, K.Pal(('G', Grey)));
        });
    }

    /// <summary>NEW の札（未読の線の右の端）</summary>
    public static Sprite NewTag(Color fill, Color? edge = null)
    {
        string key = $"Chat_New_{K.Key(fill)}_{(edge.HasValue ? K.Key(edge.Value) : "-")}";
        return K.Cached(key, () =>
        {
            int tw = K.TextWidth("NEW");
            int m = edge.HasValue ? 1 : 0;
            int w = tw + 4 + m * 2, h = 7 + m * 2;
            var g = K.Grid(w, h);
            K.RoundRect(g, m, m, w - m * 2, h - m * 2, 'F');
            K.DrawText(g, "NEW", m + 2, m + 1, 'W', 1);
            if (edge.HasValue) K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('F', fill), ('W', Color.white), ('E', edge ?? Color.clear)));
        });
    }

    /// <summary>大きい吹き出し（窓。54×36。灰色、下にしっぽ、中に棒3本、右上に赤いバッジ）</summary>
    public static Sprite BigBubble(bool redEdge, bool platform)
    {
        string key = $"Chat_Big_{redEdge}_{platform}";
        return K.Cached(key, () =>
        {
            const int w = 54, h = 36;
            var g = K.Grid(w, h);
            int bh = 30;
            K.Fill(g, 2, 0, w - 4, bh, 'G');
            K.Fill(g, 0, 2, w, bh - 4, 'G');
            K.Fill(g, 1, 1, w - 2, bh - 2, 'G');
            // しっぽ（左下）
            for (int i = 0; i < 5; i++) K.Fill(g, 5, bh + i, 6 - i, 1, 'G');
            K.Fill(g, 6, 8, 30, 3, 'B');
            K.Fill(g, 6, 14, 38, 3, 'B');
            K.Fill(g, 6, 20, 22, 3, 'B');
            K.Outline(g, 'E');
            // 右上の赤いバッジ「1」
            K.Disc(g, 47f, 6f, 5f, 'R');
            K.DrawText(g, "1", 46, 4, 'W', 1);
            if (platform) K.Fill(g, 3, 0, w - 6, 1, 'H');
            return K.Make(key, g, K.Pal(('G', Grey), ('B', K.Hex("AEAEB2")), ('R', BadgeRed), ('W', Color.white),
                ('E', redEdge ? K.EnemyEdge : GreyDark), ('H', Color.white)));
        });
    }

    /// <summary>大きいスタンプ（54×36。白い地にアプリの色のふち。kind＝0：黄色い顔、1：親指）。外は赤いふち</summary>
    public static Sprite Stamp(int kind, Color app)
    {
        kind &= 1;
        string key = $"Chat_Stamp_{kind}_{K.Key(app)}";
        return K.Cached(key, () =>
        {
            const int w = 54, h = 36;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'E');
            K.Fill(g, 1, 1, w - 2, h - 2, 'A');
            K.Fill(g, 4, 4, w - 8, h - 8, 'W');
            if (kind == 0)
            {
                // 黄色い顔（直径 24）
                K.Disc(g, 27f, 18f, 12f, 'O');
                K.Disc(g, 27f, 18f, 11f, 'Y');
                K.Fill(g, 21, 12, 3, 5, 'K');
                K.Fill(g, 30, 12, 3, 5, 'K');
                for (int i = -6; i <= 6; i++)
                {
                    int my = 22 + Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 36f - i * i)) * 0.5f);
                    K.Set(g, 27 + i, my, 'K');
                    K.Set(g, 27 + i, my + 1, 'K');
                }
            }
            else
            {
                K.StampScaled(g, ThumbArt, 15, 6, 3);
            }
            return K.Make(key, g, K.Pal(('E', K.EnemyEdge), ('A', app), ('W', Color.white), ('O', K.Hex("E0A000")),
                ('Y', Yellow), ('K', Ink), ('D', K.Hex("D9A400")), ('B', Blue)));
        });
    }

    /// <summary>
    /// 上の列の通知の題名（2026-11-19。板は MacUi.PlaceBanner。小さい板には字が入らないので、濃い棒になる）【仮】
    /// </summary>
    public const string NotifyTitle = "New message";

    /// <summary>オンライン・オフラインの丸（8ドット。暗いふち）</summary>
    public static Sprite Status(bool online) => K.DiscSprite(8, online ? Green : GreyDark, K.Hex("2C2C2E"));

    /// <summary>丸い大きい吹き出し（円。直径 d。左下にしっぽ。中に「入力中…」の点）。プレイヤーの円（白いふち）</summary>
    public static Sprite RoundBubble(int d, Color app)
    {
        d = Mathf.Max(12, d);
        string key = $"Chat_Round_{d}_{K.Key(app)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            float r = d * 0.5f;
            K.Disc(g, r, r, r - 1.5f, 'A');
            // しっぽ（左下。円のふちから外へ三角）
            Vector2 a = new Vector2(r - r * 0.62f, r + r * 0.5f), b = new Vector2(r - r * 0.15f, r + r * 0.85f), tip = new Vector2(r * 0.12f, d - 1.5f);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                    if (InTri(new Vector2(x + 0.5f, y + 0.5f), a, b, tip)) g[y, x] = 'A';
            int dot = Mathf.Max(2, d / 12);
            for (int i = -1; i <= 1; i++) K.Fill(g, Mathf.RoundToInt(r + i * dot * 2.2f - dot * 0.5f), Mathf.RoundToInt(r - dot * 0.5f), dot, dot, 'W');
            K.Outline(g, 'W');
            return K.Make(key, g, K.Pal(('A', app), ('W', Color.white)));
        });
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    /// <summary>かけら：小さい吹き出しとハート</summary>
    public static Sprite[] Bits(Color app) => new[] { Bubble(7, 6, app, false, false, K.Darken(app, 0.3f)), Bubble(7, 6, Grey, true, false, GreyDark), Heart };
}
