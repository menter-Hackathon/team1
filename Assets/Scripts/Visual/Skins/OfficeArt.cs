using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

// 【仮】Office のスキンの色と絵（2026-11-18。docs/app-skill-visuals-design.md 4章）。
// グラフ（棒・円・折れ線）・セルの選択の緑の枠・シートのタブ・スライド・「保存しますか？」のダイアログ・スペルチェックの赤い波線。
// 絵はコードの中のドット絵（1マス＝18ドット）。色が変わる絵は、key に色を入れる（同じ key は1回だけ作る）。
// 2026-11-19：ダイアログ・読み込みの輪・強制終了は Mac の部品（MacUi）で描く（docs/mac-look-design.md 21〜24）。
public static class OfficeArt
{
    // ───────── 色（4章） ─────────
    public static readonly Color Blue = K.Hex("4472C4");
    public static readonly Color Orange = K.Hex("ED7D31");
    public static readonly Color Yellow = K.Hex("FFC000");
    public static readonly Color Green = K.Hex("70AD47");
    public static readonly Color CellWhite = K.Hex("FFFFFF");
    public static readonly Color CellLine = K.Hex("D4D4D4");
    public static readonly Color Select = K.Hex("217346");     // 選択の緑（Excel）
    public static readonly Color Magenta = K.Hex("E040FB");    // 折れ線
    public static readonly Color SlideOrange = K.Hex("D24726"); // スライドの帯（PowerPoint）
    public static readonly Color DarkRed = K.Hex("B3261E");
    public static readonly Color Grey = K.Hex("8E8E93");
    public static readonly Color LightGrey = K.Hex("E5E5EA");
    public static readonly Color Ink = K.Hex("1C1C1E");

    /// <summary>アプリの色が無いとき（Excel の緑）</summary>
    public static readonly Color Fallback = Select;

    /// <summary>
    /// グラフの系列の色。1本目はアプリの色、残りはオレンジ・黄・緑・青（アプリの色に近い物は飛ばす）。4章のはじめ
    /// </summary>
    public static Color Series(int i, AppTheme theme)
    {
        Color main = K.Main(theme, Fallback);
        var list = new List<Color> { main };
        foreach (Color c in new[] { Orange, Yellow, Green, Blue })
            if (!K.Near(c, main)) list.Add(c);
        if (list.Count < 2) list.Add(Orange);
        i = ((i % list.Count) + list.Count) % list.Count;
        return list[i];
    }

    // ───────── 手で描いた絵 ─────────

    /// <summary>折れ線グラフの弾（22×9。M＝線、W＝節の点。ふちはコードで足す）。右が前</summary>
    static readonly string[] LineGraphArt =
    {
        "......................",
        ".....WW.....WW....M...",
        ".....WW....MWWM...MM..",
        "....MMMM..MM..MMMMMMM.",
        "...MM..MMMM....MMMMMM.",
        "..MM....WW........MM..",
        ".WW.....WW........M...",
        ".WW...................",
        "......................",
    };

    /// <summary>赤い「！」の三角（9×8。落下物の予告）</summary>
    static readonly string[] RedTriangleArt =
    {
        "....R....",
        "...RRR...",
        "...RWR...",
        "..RRWRR..",
        "..RRWRR..",
        ".RRRRRRR.",
        ".RRRWRRR.",
        "RRRRRRRRR",
    };

    /// <summary>チェック（9×7）</summary>
    static readonly string[] CheckArt =
    {
        "........G",
        ".......GG",
        "......GG.",
        "G....GG..",
        "GG..GG...",
        ".GGGG....",
        "..GG.....",
    };

    /// <summary>白いセルのかけら（4×3）</summary>
    static readonly string[] CellBitArt =
    {
        "GGGG",
        "GWWG",
        "GGGG",
    };

    // ───────── 絵を作る ─────────

    public static Sprite LineGraph(bool enemy)
    {
        string key = $"Office_Line_{(enemy ? "e" : "p")}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(22, 9);
            K.Stamp(g, LineGraphArt, 0, 0);
            K.Outline(g, 'R');
            return K.Make(key, g, K.Pal(('M', Magenta), ('W', Color.white), ('R', K.Edge(enemy))));
        });
    }

    static Sprite _redTri, _cellBit;

    public static Sprite RedTriangle => _redTri != null ? _redTri : (_redTri = K.Make("Office_RedTri", RedTriangleArt, K.Pal(('R', K.EnemyEdge), ('W', Color.white))));
    public static Sprite CellBit => _cellBit != null ? _cellBit : (_cellBit = K.Make("Office_CellBit", CellBitArt, K.Pal(('G', CellLine), ('W', CellWhite))));

    public static Sprite Check(Color c) => K.Cached($"Office_Check_{K.Key(c)}", () => K.Make($"Office_Check_{K.Key(c)}", CheckArt, K.Pal(('G', c))));

    /// <summary>チェック（白いふちつき。当たった絵の大）</summary>
    public static Sprite CheckOutlined(Color c)
    {
        string key = $"Office_CheckO_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(11, 9);
            K.Stamp(g, CheckArt, 1, 1);
            K.Outline(g, 'W', true);
            return K.Make(key, g, K.Pal(('G', c), ('W', Color.white)));
        });
    }

    /// <summary>棒グラフの小さい棒（かけら。2×4）</summary>
    public static Sprite BarBit(Color c)
    {
        string key = $"Office_BarBit_{K.Key(c)}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(2, 4);
            K.Fill(g, 0, 0, 2, 4, 'B');
            return K.Make(key, g, K.Pal(('B', c)));
        });
    }

    /// <summary>
    /// 円グラフ（直径 d、n 切れ。先の k 切れに色を入れ、残りは灰色）。edge＝ふちの色。
    /// 切れの境目は白い線
    /// </summary>
    public static Sprite Pie(int d, int n, int k, Color edge, AppTheme theme)
    {
        n = Mathf.Clamp(n, 1, 8);
        k = Mathf.Clamp(k, 0, n);
        string key = $"Office_Pie_{d}_{n}_{k}_{K.Key(edge)}_{K.Key(Series(0, theme))}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(d, d);
            float r = d * 0.5f;
            var pal = new Dictionary<char, Color32> { { 'E', edge }, { 'L', LightGrey }, { 'W', Color.white } };
            for (int i = 0; i < n; i++) pal[(char)('0' + i)] = Series(i, theme);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - r, dy = r - (y + 0.5f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    if (dist > r - 1.2f) { g[y, x] = 'E'; continue; }
                    // 12時から時計回りに切る
                    float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    float per = 360f / n;
                    int s = Mathf.Min(n - 1, Mathf.FloorToInt(ang / per));
                    float edgeAng = Mathf.Min(ang - s * per, (s + 1) * per - ang);
                    bool seam = n > 1 && edgeAng * Mathf.Deg2Rad * dist < 0.6f && dist > 1f;
                    g[y, x] = seam ? 'W' : (s < k ? (char)('0' + s) : 'L');
                }
            return K.Make(key, g, pal);
        });
    }

    /// <summary>円グラフの1切れ（扇形。半径 r ドット、開き 70°。右が弧。ふちつき）。重心が絵のまん中に来るように置く</summary>
    public static Sprite Wedge(Color fill, bool enemy, int r = 9)
    {
        string key = $"Office_Wedge_{r}_{K.Key(fill)}_{(enemy ? "e" : "p")}";
        return K.Cached(key, () =>
        {
            int size = r + 4;
            var g = K.Grid(size, size);
            float half = 35f * Mathf.Deg2Rad;
            float centroid = 2f * r * Mathf.Sin(half) / (3f * half);
            float ax = size * 0.5f - centroid, ay = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - ax, dy = y + 0.5f - ay;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r || dx <= 0f) continue;
                    if (Mathf.Abs(Mathf.Atan2(dy, dx)) > half) continue;
                    g[y, x] = dist > r - 1.3f ? 'L' : 'F';
                }
            K.Outline(g, 'E');
            return K.Make(key, g, K.Pal(('F', fill), ('L', K.Lighten(fill, 0.35f)), ('E', K.Edge(enemy))));
        });
    }

    /// <summary>
    /// セルの選択の枠（w×h ドット、太さ2、右下に小さい四角）。当たった絵（4-3）と柱の予告（4-2）
    /// </summary>
    public static Sprite CellFrame(int w, int h, Color c, bool fillWhite = false)
    {
        string key = $"Office_CellFrame_{w}_{h}_{K.Key(c)}_{fillWhite}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(w + 2, h + 2);
            if (fillWhite) K.Fill(g, 2, 2, w - 4, h - 4, 'F');
            K.Border(g, 0, 0, w, h, 'C', 2);
            // 右下の小さい四角（フィルハンドル）。白いすき間で囲む
            K.Fill(g, w - 3, h - 3, 5, 5, 'W');
            K.Fill(g, w - 2, h - 2, 4, 4, 'C');
            return K.Make(key, g, K.Pal(('C', c), ('W', Color.white), ('F', K.Alpha(Color.white, 0.18f))));
        });
    }

    /// <summary>白いセルに赤い字（#REF! など）。左上に赤い三角（セルのエラーの印）</summary>
    public static Sprite ErrorCell(string text, int scale = 1)
    {
        string key = $"Office_ErrCell_{text}_{scale}";
        return K.Cached(key, () =>
        {
            int tw = K.TextWidth(text) * scale;
            int w = tw + 8, h = 5 * scale + 6;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'G');
            K.Fill(g, 1, 1, w - 2, h - 2, 'W');
            K.DrawText(g, text, 5, 3, 'R', scale);
            // 左上の三角
            K.Fill(g, 1, 1, 3, 1, 'T');
            K.Fill(g, 1, 2, 2, 1, 'T');
            K.Set(g, 1, 3, 'T');
            return K.Make(key, g, K.Pal(('G', Grey), ('W', CellWhite), ('R', K.Hex("E02020")), ('T', K.Hex("E02020"))));
        });
    }

    // ───────── 「保存しますか？」のダイアログ（Mac の注意のダイアログ。2026-11-19 docs/mac-look-design.md 21・22） ─────────

    /// <summary>題名（字が入らなければ、灰色の棒の上に濃い棒。1-8）【仮】</summary>
    public const string SaveTitle = "Save?";

    /// <summary>ダイアログの大きさ（ドット）。判定のマス×18 で作る（伸ばすと信号機がつぶれる。1-2）</summary>
    public static Vector2Int SavePx(Vector2 size) =>
        new Vector2Int(Mathf.Max(24, Mathf.RoundToInt(size.x * 18f)), Mathf.Max(20, Mathf.RoundToInt(size.y * 18f)));

    /// <summary>
    /// ダイアログの地（MacUi.AlertSprite。ボタン 2つ＝Cancel・青い Save。字は入らないので棒）。
    /// enemy＝赤いふち。でなければ足場（ふつうのふちの色）
    /// </summary>
    public static Sprite SaveDialog(Vector2Int px, bool enemy) =>
        MacUi.AlertSprite(px.x, px.y, MacLook.Light, enemy ? (Color32?)(Color32)MacUi.Red : null, false, 2);

    /// <summary>
    /// ダイアログの上に重ねる物（pop の子）：影（判定の外・下に 2ドット）・アプリのアイコン・注意の黄色い三角・題名。
    /// order はダイアログの地の順番
    /// </summary>
    public static void DressSaveDialog(Transform pop, Vector2Int px, Sprite icon, int order)
    {
        int w = px.x, h = px.y;
        var sh = K.Show(pop, MacUi.ShadowSprite(w, h), new Vector2(0f, -MacUi.ShadowY * MacUi.Px), order - 1, "Shadow");
        sh.color = MacUi.ShadowColor(MacLook.Light);
        MacUi.AlertLayout l = MacUi.LayoutAlert(w, h, 2);
        // アプリのアイコン（無ければ灰色の角の丸い四角）
        Vector2 iconAt = MacUi.DotToLocal(w, h, l.Icon.x + l.Icon.width * 0.5f, l.Icon.y + l.Icon.height * 0.5f);
        if (icon != null)
        {
            float native = Mathf.Max(0.0001f, Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));
            var ic = K.Show(pop, icon, iconAt, order + 1, "Icon");
            float s = l.Icon.width * MacUi.Px / native;
            ic.transform.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            var ic = K.Show(pop, MacUi.RoundRectSprite(l.Icon.width, l.Icon.height, 3), iconAt, order + 1, "Icon");
            ic.color = MacUi.Gray1;
        }
        // 注意の三角（アイコンの右下。1-3）
        K.Show(pop, MacUi.CautionTriangle(), MacUi.DotToLocal(w, h, l.Icon.xMax - 6 + 4f, l.Icon.yMax - 6 + 4f), order + 2, "Caution");
        // 題名（太字。字が入らなければ濃い棒）
        float room = w - l.Title.x - 4;
        if (MacUi.FitsChars(SaveTitle, room))
        {
            MacUi.Label(pop, SaveTitle, MacUi.DotToLocal(w, h, l.Title.x, l.Title.y), MacUi.TextH, MacUi.Ink(MacLook.Light), order + 1, true, TextAnchor.MiddleLeft);
        }
        else
        {
            float bw = Mathf.Clamp(room - 2f, 4f, 24f);
            K.Box(pop, MacUi.DotToLocal(w, h, l.Title.x + bw * 0.5f, l.Title.y), new Vector2(bw, MacUi.TitleBarH) * MacUi.Px, MacUi.Ink(MacLook.Light), order + 1, "Title");
        }
    }

    /// <summary>左上の赤い丸のまん中（pop からの位置。閉じる動き MacUi.CloseAnim に渡す）</summary>
    public static Vector2 SaveRedLight(Vector2Int px)
    {
        int th = MacUi.TitleHeightFor(px.y);
        return MacUi.DotToLocal(px.x, px.y, MacUi.LightX + 1.5f, 1f + (th - MacUi.LightD) / 2 + 1.5f);
    }

    /// <summary>スライド（54×36。上にオレンジの帯、箇条書き、右に小さい棒グラフ）。赤いふち</summary>
    public static Sprite Slide(AppTheme theme)
    {
        string key = $"Office_Slide_{K.Key(Series(0, theme))}";
        return K.Cached(key, () =>
        {
            const int w = 54, h = 36;
            var g = K.Grid(w, h);
            K.Fill(g, 0, 0, w, h, 'W');
            K.Fill(g, 1, 1, w - 2, 8, 'O');            // 帯
            K.Fill(g, 4, 4, 22, 2, 'W');               // 題名（白い棒）
            int[] lens = { 18, 14, 20 };
            for (int i = 0; i < 3; i++)                // 箇条書き
            {
                int y = 13 + i * 6;
                K.Fill(g, 4, y, 2, 2, 'K');
                K.Fill(g, 8, y, lens[i], 2, 'L');
            }
            // 小さい棒グラフ（右）
            K.Fill(g, 34, 31, 16, 1, 'K');
            int[] hs = { 6, 10, 14 };
            for (int i = 0; i < 3; i++) K.Fill(g, 36 + i * 5, 31 - hs[i], 3, hs[i], (char)('0' + i));
            K.Border(g, 0, 0, w, h, 'E');
            return K.Make(key, g, K.Pal(('W', CellWhite), ('O', SlideOrange), ('K', K.Hex("3A3A3C")), ('L', K.Hex("B8B8BD")),
                ('0', Series(0, theme)), ('1', Series(1, theme)), ('2', Series(2, theme)), ('E', K.EnemyEdge)));
        });
    }

    /// <summary>箇条書きの点・棒（スライドが砕けたかけら）</summary>
    public static Sprite[] SlideBits(AppTheme theme)
    {
        return new[]
        {
            K.DiscSprite(2, K.Hex("3A3A3C")),
            BarBit(Series(0, theme)),
            BarBit(Series(1, theme)),
            BarBit(Series(2, theme)),
            K.Text("-", K.Hex("B8B8BD")),
        };
    }

    /// <summary>シートのタブ（27×11）。白い地、下に緑の線、字 SHEET1〜。敵は赤いふち。shaky なら線が赤（落ちる直前）</summary>
    public static Sprite SheetTab(int number, bool enemy, bool alarm)
    {
        number = Mathf.Clamp(number, 1, 9);
        string key = $"Office_Tab_{number}_{enemy}_{alarm}";
        return K.Cached(key, () =>
        {
            const int w = 27, h = 11;
            var g = K.Grid(w, h);
            K.Fill(g, 1, 1, w - 2, h - 2, 'W');
            K.Fill(g, 1, 8, w - 2, 2, alarm ? 'A' : 'S');
            K.DrawText(g, "SHEET" + number, 2, 2, 'K', 1);
            K.Border(g, 0, 0, w, h, 'E');
            K.Set(g, 0, 0, '.'); K.Set(g, w - 1, 0, '.');
            return K.Make(key, g, K.Pal(('W', CellWhite), ('S', Select), ('A', K.EnemyEdge), ('K', Ink), ('E', K.Edge(enemy))));
        });
    }

    /// <summary>
    /// スペルチェックの赤い波線（w×h ドット。下に波線、上にうすい灰色の字の行）。phase で波が 1ドットずれる。
    /// white なら波線を白に（判定の瞬間に光る）。green なら緑の下線（直った）
    /// </summary>
    public static Sprite SpellWave(int w, int h, int phase, bool white, bool green = false)
    {
        w = Mathf.Max(4, w); h = Mathf.Max(4, h);
        phase = ((phase % 4) + 4) % 4;
        string key = $"Office_Wave_{w}_{h}_{phase}_{white}_{green}";
        return K.Cached(key, () =>
        {
            var g = K.Grid(w, h);
            int baseY = h - 3;
            // 字の行（棒をすき間つきで並べる）
            int x = 1;
            int[] lens = { 5, 3, 6, 4, 2, 5, 3 };
            for (int i = 0; x < w - 1; i++)
            {
                int len = Mathf.Min(lens[i % lens.Length], w - 1 - x);
                K.Fill(g, x, Mathf.Max(0, baseY - 3), len, 2, 'T');
                x += len + 2;
            }
            if (green)
            {
                K.Fill(g, 0, h - 2, w, 1, 'G');
            }
            else
            {
                // 波線（高さ3、周期4）
                for (int i = 0; i < w; i++)
                {
                    int p = (i + phase) % 4;
                    int dy = p == 0 ? 0 : p == 2 ? 2 : 1;
                    K.Set(g, i, baseY + dy, 'R');
                }
            }
            return K.Make(key, g, K.Pal(('T', K.Alpha(K.Hex("AEAEB2"), 0.5f)), ('R', white ? Color.white : K.EnemyEdge), ('G', Green)));
        });
    }
}
