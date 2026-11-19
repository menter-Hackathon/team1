using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】Browser のスキン（Chrome と、ブラウザの代用ボス。docs/app-skill-visuals-design.md 2章）。担当2。2026-11-18。
// 何をまねるか：タブの列・読み込みの輪・広告のポップアップ・クラッシュしたタブ・404。
// ・タブ手裏剣＝タブ飛ばし（小弾。2-1）：予備動作でタブが横1列に並ぶ（撃つ数が見える）→ 水平のまま飛ぶタブ（尾は灰色の線）→ 壁で × が光って閉じる（2026-11-19：回すのをやめた）
// ・タブ増殖（上の列。2-2）：画面の上にタブの列の帯 → 読み込みの輪 → ふるえて赤いファビコン → 落ちて 404
// ・ポップアップ出現（窓。2-3）：点線の枠に読み込みの輪 → 6種類の広告（袋から引く）が「パッ」→ 足場の間は灰色のふち → 赤い丸がへこんでまん中へ縮む
// ・能力 新しいタブ（ワープ。2-4）：タブが閉じる → 読み込みの線 → 「＋」の新しいタブが開く
// ・当たった絵（2-5）：読み込みの輪 → 緑のチェック。エラーの絵は Aw, Snap!（15章の4：パロディは使ってよい）
// 当たり判定・秒数は変えない。絵は判定の中心に合わせる。
// 2026-11-19（docs/mac-look-design.md 13〜16・19）：OS の部品を Mac にした（ポップアップの枠・残像・閉じる動き、タブの列の信号機、新しいタブの窓の点 3つ）。
// タブ（× は右）・Aw, Snap!・404・読み込みの輪・広告の中身は Chrome の中身なので、そのまま。Mac の部品の色と数は BrowserDevMac（BrowserDevFx.cs）
public class BrowserSkin : AppSkin
{
    public override SkinId Id => SkinId.Browser;
    public override string NotificationTitle => "Page restored";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        try
        {
            switch (FrameFor(frame, info.Data))
            {
                case AttackFrame.SmallShot:
                    // 予備動作（攻撃する人の子に出す空の物）は横1列に並ぶタブ。弾はそれぞれ水平に飛ぶタブ
                    if (info.Target != null && !info.Target.TryGetComponent(out Projectile _))
                        return BrowserDevDriver.Claim<BrowserTabFan>(info, phase) != null;
                    return BrowserDevDriver.Claim<BrowserTabShot>(info, phase) != null;
                case AttackFrame.TopRow:
                    return BrowserDevDriver.Claim<BrowserTabRow>(info, phase) != null;
                case AttackFrame.Window:
                    return BrowserDevDriver.Claim<BrowserPopup>(info, phase) != null;
                case AttackFrame.Warp:
                    if (info.IsEnemy) return false; // ボスの瞬間移動は 2章に無い（Generic に任せる）
                    return BrowserNewTab.Dress(phase, info);
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BrowserSkin.Dress：{e}");
            return false;
        }
    }

    /// <summary>その技・能力だけの見た目を、攻撃データの名前で先に選ぶ（枠の表が間に合わないときも動くように）</summary>
    static AttackFrame FrameFor(AttackFrame frame, AttackData data)
    {
        string n = BrowserDevFx.NameOf(data);
        if (Has(n, "tabshuriken")) return AttackFrame.SmallShot;
        if (Has(n, "tabmultiply")) return AttackFrame.TopRow;
        if (Has(n, "popupappear")) return AttackFrame.Window;
        if (Has(n, "ability_newtab")) return AttackFrame.Warp;
        return frame;
    }

    static bool Has(string s, string part) => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // ───────── 当たった絵（2-5） ─────────

    public override bool Hit(HitInfo info)
    {
        try
        {
            Vector2 at = BrowserDevFx.HitAnchor(info);
            Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
            int shards;
            int size;
            float life;
            float scale = 1f;
            switch (info.Strength)
            {
                case HitStrength.Small:
                    BrowserArt.Fragments(info.Point, dir, 1, info.Theme);
                    return true;
                case HitStrength.Light: size = 12; life = 0.2f; shards = 3; break;
                case HitStrength.Finisher: size = 20; life = 0.3f; shards = 12; scale = 1.5f; break;
                default: size = 20; life = 0.3f; shards = 6; break;
            }
            BrowserArt.Fragments(info.Point, dir, shards, info.Theme);
            BrowserArt.SpinnerToCheck(at, size, life, scale);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BrowserSkin.Hit：{e}");
            return false;
        }
    }

    public override bool Error(HitInfo info)
    {
        try
        {
            bool down = info.Strength == HitStrength.Down;
            Vector2 head = BrowserDevFx.HeadOf(info.Target, info.Point);
            if (down)
            {
                // 画面のまん中寄りへ、2倍で（1-2 の表）
                Rect view = BrowserDevFx.View();
                head = Vector2.Lerp(head, view.center, 0.4f);
            }
            BrowserArt.AwSnap(head, down ? 2f : 1f, down ? 0.9f : 0.4f);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BrowserSkin.Error：{e}");
            return false;
        }
    }
}

/// <summary>【仮】Browser の色と絵（コードの中のドット絵）</summary>
public static class BrowserArt
{
    public static readonly Color TabGrey = RuntimeSprites.Hex("DEE1E6");
    public static readonly Color Dark = RuntimeSprites.Hex("202124");
    public static readonly Color XGrey = RuntimeSprites.Hex("5F6368");
    public static readonly Color LightGrey = RuntimeSprites.Hex("BDC1C6");
    public static readonly Color Blue = RuntimeSprites.Hex("4285F4");
    public static readonly Color Red = RuntimeSprites.Hex("EA4335");
    public static readonly Color Yellow = RuntimeSprites.Hex("FBBC05");
    public static readonly Color Green = RuntimeSprites.Hex("34A853");
    public static readonly Color PlatformEdge = BrowserDevMac.Light5; // 足場になった窓のふち（Mac の明るい窓のふち。2026-11-19）
    public static readonly Color Text404 = RuntimeSprites.Hex("9AA0A6");
    static readonly Color[] ChromeFour = { Blue, Red, Yellow, Green }; // 青 → 赤 → 黄 → 緑
    static readonly Color32 White32 = new Color32(255, 255, 255, 255);

    static string H(Color c) => ColorUtility.ToHtmlStringRGBA(c);

    /// <summary>4色の i 番目。Chrome（色の組が無い・名前が Chrome）は決まった4色、ほかはアプリの差し色</summary>
    public static Color Four(AppTheme theme, int i)
    {
        i = Mathf.Abs(i);
        if (theme == null || theme.name.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0) return ChromeFour[i % 4];
        Color[] acc = theme.Accents;
        return BrowserDevFx.Opaque(acc[i % acc.Length]);
    }

    // ───────── タブ（小弾。22×12） ─────────

    public const int TabW = 22, TabH = 12;

    /// <summary>
    /// Chrome のタブ（22×12）。2026-11-19（docs/app-feedback-v2-design.md 2-1）：14×9 の小さいタブをやめて大きくした。
    /// 上の角が丸く、下が少し広い台形。左に丸いファビコン（4ドット）、まん中に灰色の題名の棒（8×2）、右に ×（3ドット）
    /// </summary>
    public static Sprite Tab(Color edge, Color fav)
    {
        var g = new BrowserDevGrid(TabW, TabH);
        // 台形：上ほど内へ寄せる（行ごとの左の端）
        int[] inset = { 4, 3, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0 };
        for (int y = 0; y < TabH; y++)
        {
            int l = inset[y], r = TabW - 1 - inset[y];
            if (y == 0 || y == TabH - 1)
            {
                g.Rect(l, y, r - l + 1, 1, 'E');
                continue;
            }
            g.Set(l, y, 'E');
            g.Rect(l + 1, y, r - l - 1, 1, 'W');
            g.Set(r, y, 'E');
        }
        // 丸いファビコン（4ドット）・題名の棒（8×2）・×（3ドット）
        g.Rect(4, 4, 2, 1, 'F');
        g.Rect(3, 5, 4, 2, 'F');
        g.Rect(4, 7, 2, 1, 'F');
        g.Rect(8, 5, 8, 2, 'T');
        BrowserDevFx.StampX(g, 17, 4, 3, 'X');
        var pal = new Dictionary<char, Color32>
        {
            { 'E', edge }, { 'W', White32 }, { 'F', fav }, { 'T', XGrey }, { 'X', XGrey },
        };
        return BrowserDevFx.Art($"Tab22_{H(edge)}_{H(fav)}", g, pal);
    }

    /// <summary>タブの × のまん中（絵のまん中からの位置。マス）</summary>
    public static Vector2 TabCloseLocal => new Vector2(18f - TabW * 0.5f + 0.5f, TabH * 0.5f - 5.5f) * BrowserDevFx.Dot;

    // ───────── 大きいタブ（上の列。27×11） ─────────

    /// <summary>fav が透明なら、ファビコンの所を空ける（読み込みの輪を重ねる）</summary>
    public static Sprite BigTab(Color edge, Color fav, Color xColor)
    {
        var g = new BrowserDevGrid(27, 11);
        g.Rect(3, 0, 21, 1, 'E');
        g.Set(2, 1, 'E');
        g.Rect(3, 1, 21, 1, 'W');
        g.Set(24, 1, 'E');
        g.Set(1, 2, 'E');
        g.Rect(2, 2, 23, 1, 'W');
        g.Set(25, 2, 'E');
        for (int y = 3; y <= 9; y++)
        {
            g.Set(0, y, 'E');
            g.Rect(1, y, 25, 1, 'W');
            g.Set(26, y, 'E');
        }
        g.Rect(0, 10, 27, 1, 'E');
        if (fav.a > 0f)
        {
            g.Rect(4, 3, 2, 1, 'F');
            g.Rect(3, 4, 4, 2, 'F');
            g.Rect(4, 6, 2, 1, 'F');
        }
        g.Rect(9, 4, 8, 1, 'T');
        g.Rect(9, 6, 5, 1, 'T');
        BrowserDevFx.StampX(g, 19, 3, 5, 'X');
        g.Rect(1, 9, 25, 1, 'G');
        var pal = new Dictionary<char, Color32>
        {
            { 'E', edge }, { 'W', White32 }, { 'F', fav }, { 'T', LightGrey }, { 'X', xColor }, { 'G', TabGrey },
        };
        return BrowserDevFx.Art($"BigTab_{H(edge)}_{H(fav)}_{H(xColor)}", g, pal);
    }

    // ───────── 広告のポップアップ（窓。54×36） ─────────

    static readonly string[] StarRows =
    {
        ".....#.....",
        ".....#.....",
        "....###....",
        "....###....",
        "###########",
        ".#########.",
        "..#######..",
        "..#######..",
        ".####.####.",
        ".###...###.",
        "##.......##",
    };

    static readonly string[] WarnRows =
    {
        "......W......",
        ".....WWW.....",
        ".....WRW.....",
        "....WWRWW....",
        "....WWRWW....",
        "...WWWRWWW...",
        "...WWWWWWW...",
        "..WWWWRWWWW..",
        "..WWWWWWWWW..",
        ".WWWWWWWWWWW.",
        "WWWWWWWWWWWWW",
    };

    public const int PopupW = 54, PopupH = 36;

    /// <summary>広告のデザインの数（2026-11-19：3 → 6。docs/app-feedback-v2-design.md 2-2）</summary>
    public const int PopupKinds = 6;

    /// <summary>
    /// 広告のポップアップ。kind：0＝WINNER!（黄）、1＝DOWNLOAD（白と緑）、2＝WARNING（赤）、
    /// 3＝動画広告（黒・▶・Skip Ad）、4＝セール（ピンク・-90%）、5＝クッキーの同意（白・クッキー・ACCEPT）。3〜5 は 2026-11-19 に足した【仮】。
    /// 2026-11-19（docs/mac-look-design.md 13）：枠は Mac の明るい窓（角の丸い 1ドットのふち・左上の信号機・まん中の題名の棒・右上は空）。
    /// 中身はタイトルバーが 1行高くなった分、1行下から（9行目〜）
    /// </summary>
    public static Sprite Popup(int kind, Color edge) => Popup(kind, edge, PopupW, PopupH);

    /// <summary>
    /// 広告のポップアップ（大きさを決めて描く。2026-11-19：広告の形4種類。docs 2-2 の B）。
    /// 中身は、54×36 で決めた置き方を、大きさに合わせて縮める・寄せる（小さい窓は中身を減らす）
    /// </summary>
    public static Sprite Popup(int kind, Color edge, int w, int h)
    {
        kind = Mathf.Abs(kind) % PopupKinds;
        w = Mathf.Max(24, w);
        h = Mathf.Max(18, h);
        var g = new BrowserDevGrid(w, h);
        char bg = kind switch { 0 => 'Y', 1 => 'W', 2 => 'R', 3 => 'D', 4 => 'P', _ => 'W' };
        BrowserDevMac.StampWindow(g, 0, 0, w, h, false, edge);
        BrowserDevMac.FillBody(g, 0, 0, w, h, bg); // 中身（広告の地）
        int top = BrowserDevMac.BodyTop(h);      // 中の面の最初の行
        int bottom = h - 2;                       // 中の面の最後の行
        int bodyH = bottom - top + 1;
        // 中身を描く箱（54×36 のときは 左 4・上 11 から、幅 46×高さ 22）
        int bx = 4, by = top + 2, bw = w - 8, bh = Mathf.Max(6, bodyH - 3);
        switch (kind)
        {
            case 0: PopupWinner(g, bx, by, bw, bh); break;
            case 1: PopupDownload(g, bx, by, bw, bh); break;
            case 2: PopupWarning(g, bx, by, bw, bh); break;
            case 3: PopupVideo(g, top, bottom, w); break;
            case 4: PopupSale(g, bx, by, bw, bh); break;
            default: PopupCookie(g, top, bx, by, bw, bh, w); break;
        }
        var pal = new Dictionary<char, Color32>
        {
            { 'C', Red }, { 'W', White32 }, { 'Y', Yellow }, { 'R', Red }, { 'N', Green }, { 'K', Dark }, { 'T', LightGrey },
            { 'S', White32 }, { 'O', RuntimeSprites.Hex("E37400") },
            { 'D', Dark }, { 'P', RuntimeSprites.Hex("FF4F8B") }, { 'B', Blue }, { 'G', XGrey }, { 'Q', RuntimeSprites.Hex("3C4043") },
            { 'M', RuntimeSprites.Hex("B5651D") }, { 'm', RuntimeSprites.Hex("6B3A12") }, { 'L', TabGrey },
        };
        return BrowserDevFx.Art($"PopupMac_{kind}_{w}x{h}_{H(edge)}", g, pal);
    }

    // ───────── 広告の中身（bx, by は中身の箱の左上） ─────────

    static void PopupWinner(BrowserDevGrid g, int bx, int by, int bw, int bh)
    {
        bool roomForStar = bw >= 30 && bh >= 13;
        int tx = bx;
        if (roomForStar)
        {
            var star = new BrowserDevGrid(13, 13);
            star.Stamp(StarRows, 1, 1, 'S');
            star.Outline('O');
            g.Stamp(star.Rows(), bx, by);
            tx = bx + 15;
        }
        string word = BrowserDevFont.Width("WINNER!") <= bx + bw - tx ? "WINNER!" : "WIN!"; // 細い窓は短く
        BrowserDevFont.Stamp(g, word, tx, by + 2, 'K');
        if (bh >= 14)
        {
            int btnW = Mathf.Min(25, bx + bw - tx - 1);
            g.Rect(tx + 1, by + 12, btnW, Mathf.Min(9, bh - 12), 'C'); // 赤いボタン（字は入れない：0-1 の決まった文字だけ）
            g.Rect(tx + 5, by + 15, Mathf.Max(2, btnW - 8), 3, 'W');
        }
    }

    static void PopupDownload(BrowserDevGrid g, int bx, int by, int bw, int bh)
    {
        g.Rect(bx + 2, by, bw - 4, 1, 'T');
        g.Rect(bx + 2, by + 2, Mathf.Max(4, bw / 2), 1, 'T');
        int bty = by + Mathf.Min(6, Mathf.Max(3, bh - 10));
        int btnH = Mathf.Clamp(bh - (bty - by) - 2, 7, 13);
        g.Rect(bx + 2, bty, bw - 4, btnH, 'N');
        string word = BrowserDevFont.Width("DOWNLOAD") <= bw - 6 ? "DOWNLOAD" : "GET"; // 細い窓は短く
        int tw = BrowserDevFont.Width(word);
        BrowserDevFont.Stamp(g, word, bx + 2 + Mathf.Max(1, (bw - 4 - tw) / 2), bty + (btnH - 5) / 2, 'W');
    }

    static void PopupWarning(BrowserDevGrid g, int bx, int by, int bw, int bh)
    {
        int tx = bx;
        if (bw < 30)
        {
            // 細い窓：三角をまん中に、下に短い字
            g.Stamp(WarnRows, bx + Mathf.Max(0, (bw - 13) / 2), by + 1);
            if (bh >= 19) BrowserDevFont.Stamp(g, "ERROR", bx + Mathf.Max(0, (bw - BrowserDevFont.Width("ERROR")) / 2), by + 14, 'W');
            return;
        }
        if (bh >= 11)
        {
            g.Stamp(WarnRows, bx, by + 1);
            tx = bx + 16;
        }
        BrowserDevFont.Stamp(g, "WARNING", tx, by + 2, 'W');
        if (bh >= 12) g.Rect(tx, by + 11, Mathf.Max(4, bx + bw - tx - 1), 1, 'W');
        if (bh >= 15) g.Rect(tx, by + 14, Mathf.Max(4, (bx + bw - tx) * 2 / 3), 1, 'W');
    }

    /// <summary>動画広告：黒い地に白い ▶、下に黄色の進みの線、右下に小さい箱 Skip Ad</summary>
    static void PopupVideo(BrowserDevGrid g, int top, int bottom, int w)
    {
        int bodyH = bottom - top + 1;
        // 進みの線（下から 2行目まで。黄色が 4割、残りは灰色）
        int ly = bottom - 1;
        g.Rect(2, ly, w - 4, 2, 'Q');
        g.Rect(2, ly, Mathf.Max(2, (w - 4) * 2 / 5), 2, 'Y');
        // ▶（高さ 11。窓が低いときは 7）
        int th = bodyH >= 20 ? 11 : 7;
        int half = th / 2;
        int cx = Mathf.Max(4, w / 2 - (w >= 40 ? 14 : 3));
        int cy = top + Mathf.Max(1, (bodyH - 3 - th) / 2);
        for (int r = 0; r < th; r++)
        {
            int len = r <= half ? r + 1 : th - r;
            g.Rect(cx, cy + r, Mathf.Max(1, len * 8 / (half + 1)), 1, 'W');
        }
        // Skip Ad の箱（右下。窓が狭いときは字だけ）
        int tw = BrowserDevFont.Width("Skip Ad");
        if (w >= tw + 10 && bodyH >= 14)
        {
            int boxW = tw + 4, boxH = 9;
            int x0 = w - 3 - boxW, y0 = ly - 2 - boxH;
            g.Rect(x0, y0, boxW, boxH, 'T');
            g.Rect(x0 + 1, y0 + 1, boxW - 2, boxH - 2, 'Q');
            BrowserDevFont.Stamp(g, "Skip Ad", x0 + 2, y0 + 2, 'W');
        }
    }

    /// <summary>セール：ピンクの地に大きい白い字 -90%、左にぎざぎざの丸（値札）</summary>
    static void PopupSale(BrowserDevGrid g, int bx, int by, int bw, int bh)
    {
        int tx = bx;
        if (bw >= 40 && bh >= 14)
        {
            // ぎざぎざの丸（14ドット。黄色の地に、まん中に白い丸）
            var burst = new BrowserDevGrid(14, 14);
            burst.Disc(2, 2, 10, 'Y');
            // とげ：上下左右は 2×2、ななめは 2ドット
            burst.Rect(12, 6, 2, 2, 'Y');
            burst.Rect(0, 6, 2, 2, 'Y');
            burst.Rect(6, 12, 2, 2, 'Y');
            burst.Rect(6, 0, 2, 2, 'Y');
            foreach (var d in new[] { new Vector2Int(11, 11), new Vector2Int(2, 2), new Vector2Int(11, 2), new Vector2Int(2, 11) })
            {
                burst.Set(d.x, d.y, 'Y');
                burst.Set(d.x + (d.x > 6 ? 1 : -1), d.y + (d.y > 6 ? 1 : -1), 'Y');
            }
            burst.Disc(5, 5, 4, 'W');
            g.Stamp(burst.Rows(), bx, by + Mathf.Max(0, (bh - 14) / 2));
            tx = bx + 15;
        }
        int scale = bh >= 10 && bx + bw - tx >= BrowserDevFont.Width("-90%") * 2 ? 2 : 1;
        StampBig(g, "-90%", tx, by + Mathf.Max(0, (bh - 5 * scale) / 2), 'W', scale);
    }

    /// <summary>クッキーの同意：白い地、上に灰色の線、茶色のクッキー（かけた丸）、青いボタン ACCEPT と灰色の小さいボタン</summary>
    static void PopupCookie(BrowserDevGrid g, int top, int bx, int by, int bw, int bh, int w)
    {
        g.Rect(1, top, w - 2, 2, 'L');
        int tx = bx;
        if (bw >= 36 && bh >= 12)
        {
            var cookie = new BrowserDevGrid(12, 12);
            cookie.Disc(0, 0, 12, 'M');
            // かじった跡（右上を丸く空ける）
            for (int y = 0; y < 5; y++)
                for (int x = 7; x < 12; x++)
                {
                    float dx = x + 0.5f - 11f, dy = y + 0.5f - 1f;
                    if (dx * dx + dy * dy <= 10f) cookie.Set(x, y, '.');
                }
            cookie.Rect(3, 3, 2, 2, 'm');
            cookie.Rect(6, 7, 2, 2, 'm');
            cookie.Rect(3, 8, 1, 1, 'm');
            g.Stamp(cookie.Rows(), bx, by + 1);
            tx = bx + 15;
        }
        int right = bx + bw;
        g.Rect(tx, by + 1, Mathf.Max(4, right - tx - 1), 1, 'T');
        g.Rect(tx, by + 4, Mathf.Max(4, (right - tx) * 2 / 3), 1, 'T');
        int tw = BrowserDevFont.Width("ACCEPT");
        int btnY = by + Mathf.Min(8, Mathf.Max(6, bh - 9));
        int btnH = Mathf.Min(9, Mathf.Max(7, bh - (btnY - by)));
        btnH = Mathf.Max(5, Mathf.Min(btnH, by + bh - btnY)); // 窓の下のふちに重ねない
        int btnW = Mathf.Min(tw + 6, Mathf.Max(8, right - tx - 6));
        g.Rect(tx, btnY, btnW, btnH, 'B');
        string word = btnW >= tw + 2 ? "ACCEPT" : "OK"; // 細い窓は短く
        int ww = BrowserDevFont.Width(word);
        BrowserDevFont.Stamp(g, word, tx + Mathf.Max(0, (btnW - ww) / 2), btnY + Mathf.Max(0, (btnH - 5) / 2), 'W');
        int gx = tx + btnW + 2;
        if (right - gx >= 3) g.Rect(gx, btnY, Mathf.Min(8, right - gx), btnH, 'T'); // 灰色の小さいボタン
    }

    /// <summary>字を scale 倍の大きさで貼る（大きい字。1ドットを scale×scale にする）</summary>
    static void StampBig(BrowserDevGrid g, string text, int x, int y, char c, int scale)
    {
        if (scale <= 1)
        {
            BrowserDevFont.Stamp(g, text, x, y, c);
            return;
        }
        var small = new BrowserDevGrid(BrowserDevFont.Width(text), BrowserDevFont.Height);
        BrowserDevFont.Stamp(small, text, 0, 0, '#');
        for (int j = 0; j < small.H; j++)
            for (int i = 0; i < small.W; i++)
                if (small.Get(i, j) == '#') g.Rect(x + i * scale, y + j * scale, scale, scale, c);
    }

    // ───────── 広告のデザインの袋（2-2 の A） ─────────

    class AdBag
    {
        public readonly List<int> Left = new List<int>();
        public readonly HashSet<int> Volley = new HashSet<int>();
    }

    static readonly Dictionary<int, AdBag> AdBags = new Dictionary<int, AdBag>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAdBags() => AdBags.Clear();

    /// <summary>
    /// 次の広告のデザインを袋から引く（2026-11-19。docs 2-2）。6種類を袋に入れて1枚ずつ引き、全部出るまで同じ物は出ない。
    /// 同じ技の中（newVolley＝true で始まる1回分）は、袋を足し直しても、その回に出たデザインは引かない
    /// </summary>
    public static int NextAdKind(int key, bool newVolley)
    {
        if (!AdBags.TryGetValue(key, out AdBag bag))
        {
            bag = new AdBag();
            AdBags[key] = bag;
        }
        if (newVolley) bag.Volley.Clear();
        if (bag.Volley.Count >= PopupKinds) bag.Volley.Clear(); // 1回に6枚より多いとき
        var choices = new List<int>();
        foreach (int k in bag.Left) if (!bag.Volley.Contains(k)) choices.Add(k);
        if (choices.Count == 0)
        {
            bag.Left.Clear();
            for (int k = 0; k < PopupKinds; k++) bag.Left.Add(k);
            foreach (int k in bag.Left) if (!bag.Volley.Contains(k)) choices.Add(k);
        }
        int pick = choices[UnityEngine.Random.Range(0, choices.Count)];
        bag.Left.Remove(pick);
        bag.Volley.Add(pick);
        return pick;
    }

    /// <summary>ポップアップの重なった残像（角の丸い枠とタイトルバーだけ。白。色は呼ぶ側が付ける）</summary>
    public static Sprite PopupGhost() => BrowserDevMac.ShapeSprite(PopupW, PopupH, true);

    // ───────── クラッシュしたタブ（エラーの絵。16×18） ─────────

    static readonly string[] CrashRows =
    {
        "KKKKKKKKKKK.....",
        "KGGGGGGGGGKK....",
        "KGGGGGGGGGKLK...",
        "KGGGGGGGGGKLLK..",
        "KGGGGGGGGGKLLLK.",
        "KGGGGGGGGGKKKKKK",
        "KGGGGGGGGGGGGGGK",
        "KGGKGKGGGGKGKGGK",
        "KGGGKGGGGGGKGGGK",
        "KGGKGKGGGGKGKGGK",
        "KGGGGGGGGGGGGGGK",
        "KGGGGGGGGGGGGGGK",
        "KGGGGGKKKKGGGGGK",
        "KGGGGKGGGGKGGGGK",
        "KGGGGGGGGGGGGGGK",
        "KGGGGGGGGGGGGGGK",
        "KGGGGGGGGGGGGGGK",
        "KKKKKKKKKKKKKKKK",
    };

    public static Sprite Crash() => BrowserDevFx.Art("CrashTab", CrashRows, new Dictionary<char, Color32>
    {
        { 'K', XGrey }, { 'G', RuntimeSprites.Hex("DADCE0") }, { 'L', RuntimeSprites.Hex("F1F3F4") },
    });

    // ───────── 新しいタブ（ワープの到着。26×20） ─────────

    /// <summary>
    /// 新しいタブ（26×20）。2026-11-19（docs/mac-look-design.md 19）：窓の上をタブの列（灰色の帯）にして、
    /// 左の端に信号機（小さい窓なので 1ドットの点 3つ。窓の左から 2・4・6）。新しいタブはその右から、「＋」はタブの右
    /// </summary>
    public static Sprite NewTab(Color accent)
    {
        var g = new BrowserDevGrid(26, 20);
        // 窓の体：上はタブの列、その下にアドレスバーとページ
        g.Rect(2, 2, 22, 4, 'S');
        g.Rect(2, 6, 22, 12, 'W');
        g.Rect(3, 7, 20, 2, 'G');       // アドレスバー
        g.Rect(5, 7, 14, 1, 'W');
        g.Rect(5, 11, 12, 1, 'G');       // ページの中身
        g.Rect(5, 13, 16, 1, 'G');
        g.Rect(5, 15, 9, 1, 'G');
        // 新しいタブ（上の端がアプリの差し色。下の体とつながる）
        g.Rect(11, 2, 4, 1, 'A');
        g.Rect(10, 3, 6, 3, 'W');
        g.Outline('D');
        g.Outline('E');
        // 信号機（1ドットの点 3つ）
        BrowserDevMac.StampDots(g, 4, 3);
        // 「＋」（新しいタブのボタン）
        var plus = new BrowserDevGrid(7, 7);
        plus.Rect(3, 1, 1, 5, 'D');
        plus.Rect(1, 3, 5, 1, 'D');
        plus.Outline('E');
        g.Stamp(plus.Rows(), 16, 0);
        var pal = new Dictionary<char, Color32>
        {
            { 'A', accent }, { 'W', White32 }, { 'G', TabGrey }, { 'S', TabGrey }, { 'D', XGrey }, { 'E', White32 },
        };
        return BrowserDevFx.Art($"NewTabMac_{H(accent)}", g, pal);
    }

    /// <summary>新しいタブの絵の、タブの左（読み込みの輪を重ねる所）。絵のまん中からの位置（マス）</summary>
    public static Vector2 NewTabFavicon => new Vector2(-1.5f, 5.5f) * BrowserDevFx.Dot;

    /// <summary>タブの列の右の端の「＋」（7×7）</summary>
    public static Sprite Plus()
    {
        var g = new BrowserDevGrid(7, 7);
        g.Rect(3, 1, 1, 5, 'D');
        g.Rect(1, 3, 5, 1, 'D');
        return BrowserDevFx.Art("StripPlus", g, new Dictionary<char, Color32> { { 'D', XGrey } });
    }

    /// <summary>緑のチェック（白いふち。size ドット角）</summary>
    public static Sprite Check(int size, Color color)
    {
        var g = new BrowserDevGrid(size, size);
        BrowserDevFx.StampCheck(g, 1, 1, size - 2, 'C');
        g.Outline('O');
        return BrowserDevFx.Art($"Check_{size}_{H(color)}", g, new Dictionary<char, Color32> { { 'C', color }, { 'O', White32 } });
    }

    // ───────── 出す物 ─────────

    /// <summary>4色の小さい四角（3ドット）のかけら</summary>
    public static void Fragments(Vector2 at, Vector2 dir, int count, AppTheme theme)
    {
        Sprite sq = BrowserDevFx.Square(3);
        int start = UnityEngine.Random.Range(0, 4);
        BrowserDevFx.Burst(at, dir, count, i => sq, i => Four(theme, start + i), unscaled: true);
    }

    /// <summary>読み込みの輪が1回転して、緑のチェックになる（当たった絵。上へ 0.4マス浮きながら消える）</summary>
    public static void SpinnerToCheck(Vector2 at, int size, float life, float scale)
    {
        GameObject go = BrowserDevFx.Loose("HitCheck", at);
        int t = size <= 12 ? 2 : 3;
        SpriteRenderer spin = BrowserDevFx.Show(go.transform, BrowserDevFx.Spinner(size, t), Vector2.zero, BrowserDevFx.OrderHit, scale, "Spinner");
        spin.color = Blue;
        SpriteRenderer check = BrowserDevFx.Show(go.transform, Check(size + 2, Green), Vector2.zero, BrowserDevFx.OrderHit + 1, scale, "Check");
        check.enabled = false;
        float spinTime = life * 0.4f;
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            go.transform.position = at + Vector2.up * (0.4f * k);
            if (e < spinTime)
            {
                spin.transform.localRotation = Quaternion.Euler(0f, 0f, -360f * e / spinTime);
                return;
            }
            spin.enabled = false;
            check.enabled = true;
            float p = BrowserDevFx.Pop(e - spinTime, 0.08f) * scale;
            check.transform.localScale = new Vector3(p, p, 1f);
            float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            check.color = new Color(1f, 1f, 1f, a);
        }, true);
    }

    /// <summary>クラッシュしたタブと Aw, Snap!（エラーの絵）。head は出す位置（頭の上）</summary>
    public static void AwSnap(Vector2 head, float scale, float life)
    {
        GameObject go = BrowserDevFx.Loose("AwSnap", head);
        Transform body = BrowserDevFx.Child(go.transform, "Body", Vector2.zero);
        BrowserDevFx.Show(body, Crash(), new Vector2(0f, 0.25f), BrowserDevFx.OrderHit, 1f, "Tab");
        BrowserDevFx.Show(body, BrowserDevFx.Text("Aw, Snap!", White32, true), new Vector2(0f, -0.38f), BrowserDevFx.OrderHit + 1, 1f, "Text");
        var renderers = go.GetComponentsInChildren<SpriteRenderer>();
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.1f) * scale;
            body.localScale = new Vector3(s, s, 1f);
            // 「ガタッ」と小さくゆれる
            float shake = e < 0.12f ? Mathf.Sin(e * 120f) * 1.5f * BrowserDevFx.Dot : 0f;
            body.localPosition = new Vector3(shake, 0f, 0f);
            float a = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
            foreach (var r in renderers) if (r != null) r.color = new Color(1f, 1f, 1f, a);
        }, true);
    }

    /// <summary>灰色の字 404（高さ 0.45マス）が上へ浮いて消える</summary>
    public static GameObject Float404(Vector2 at, float rise, float life)
    {
        GameObject go = BrowserDevFx.Loose("404", at);
        TextMesh tm = WorldText.Create(go.transform, "404", 0.45f, Text404, BrowserDevFx.OrderHit);
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            go.transform.position = at + Vector2.up * (rise * k);
            WorldText.SetColor(tm, BrowserDevFx.WithAlpha(Text404, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f));
        });
        return go;
    }
}

// ───────── タブ手裏剣：予備動作（2-1） ─────────

/// <summary>
/// 【仮】予備動作の間、ボスのウィンドウの上に、撃つ数だけタブが横1列に並ぶ（0.15秒ごとに1枚ずつ「パッ」。タブの列に見せる）。最後の 0.3秒は赤いふちが点滅。
/// 第2形態（5発×2回）は、2回目の分の列が「もう1段」上に重なって出て、1回目を撃ったあとも残る。
/// 2026-11-19（docs/app-feedback-v2-design.md 2-1）：扇に並べるのをやめて、横1列にした（タブも 22×12 に）
/// </summary>
public class BrowserTabFan : BrowserDevDriver
{
    const float Step = 0.15f;
    /// <summary>となりのタブとの間（タブの列のように、2ドット重ねる）</summary>
    const float Pitch = (BrowserArt.TabW - 2) * BrowserDevFx.Dot;
    /// <summary>2列目の高さのずれ</summary>
    const float RowRise = (BrowserArt.TabH + 1) * BrowserDevFx.Dot;

    struct Card
    {
        public SpriteRenderer R;
        public float At;
        public int Row;
        public Vector2 Pos;
        public Sprite Red, Blink;
    }

    readonly List<Card> _cards = new List<Card>();
    Transform _root;
    int _rows, _perRow;

    protected override void Build()
    {
        AttackData d = Info.Data;
        _rows = d != null ? Mathf.Max(1, d.RepeatCount) : 1;
        _perRow = d != null ? Mathf.Max(1, d.ProjectileCount) : Mathf.Max(1, Info.Count);
        _rows = Mathf.Min(_rows, 3);
        float windup = Info.Duration > 0f ? Info.Duration : (d != null ? d.Windup : 0.8f);
        float step = Mathf.Min(Step, Mathf.Max(0.03f, (windup - 0.3f) / Mathf.Max(1, _perRow)));
        Color blinkEdge = new Color(0.85f, 0.87f, 0.9f);

        _root = BrowserDevFx.Child(transform, "TabRow", Vector2.zero);
        for (int r = 0; r < _rows; r++)
        {
            for (int i = 0; i < _perRow; i++)
            {
                Color fav = BrowserArt.Four(Info.Theme, r * _perRow + i);
                Vector2 pos = new Vector2((i - (_perRow - 1) * 0.5f) * Pitch, RowRise * r);
                Sprite red = BrowserArt.Tab(EdgeColor, fav);
                // 左のタブほど手前（タブの列の重なり方）
                SpriteRenderer sr = BrowserDevFx.Show(_root, red, pos, BrowserDevFx.OrderDeco - r * 2 + (i == 0 ? 1 : 0), 1f, "Tab");
                sr.transform.localScale = Vector3.zero;
                if (r > 0) sr.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                _cards.Add(new Card { R = sr, At = i * step + r * 0.07f, Row = r, Pos = pos, Red = red, Blink = BrowserArt.Tab(blinkEdge, fav) });
            }
        }
        Place();
    }

    void Place()
    {
        if (Info.Owner == null || _root == null) return;
        Bounds b = BrowserDevFx.OwnerBounds(Info.Owner);
        _root.position = new Vector2(b.center.x, b.max.y + 0.5f);
        _root.rotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        Place();
        float e = Time.time - Born;
        float windup = Info.Duration > 0f ? Info.Duration : 0.8f;
        bool blink = windup - e <= 0.3f && Mathf.FloorToInt(e / 0.06f) % 2 == 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            Card c = _cards[i];
            if (c.R == null) continue;
            float s = BrowserDevFx.Pop(e - c.At, 0.1f);
            c.R.transform.localScale = new Vector3(s, s, 1f);
            // 最後の 0.3秒：赤いふちの点滅（ふちを白っぽい灰色と入れかえる）
            if (windup - e <= 0.3f && c.Row == 0) c.R.sprite = blink ? c.Blink : c.Red;
        }
    }

    protected override void OnVanish(DressInfo info)
    {
        // 1列目は撃たれる（パッと消える）。2列目からは、次の連射まで残す
        if (_rows <= 1 || Info.Owner == null) return;
        GameObject rest = BrowserDevFx.Loose("TabFanRest", _root.position);
        var keep = new List<SpriteRenderer>();
        foreach (Card c in _cards)
        {
            if (c.Row == 0 || c.R == null) continue;
            SpriteRenderer sr = BrowserDevFx.Show(rest.transform, c.Red, c.Pos - new Vector2(0f, RowRise), c.R.sortingOrder + 2, 1f, "Tab");
            sr.color = Color.white;
            keep.Add(sr);
        }
        var r = rest.AddComponent<BrowserTabFanRest>();
        r.Setup(Info.Owner, Info.Data, keep, _perRow);
        Adopt(rest);
    }
}

/// <summary>【仮】2回目からの連射の分のタブ。撃つたびに1列ずつ消える</summary>
public class BrowserTabFanRest : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    readonly List<SpriteRenderer> _tabs = new List<SpriteRenderer>();
    int _perRow;
    int _bornFrame;
    float _born;

    public void Setup(AttackRunner owner, AttackData data, List<SpriteRenderer> tabs, int perRow)
    {
        _owner = owner;
        _data = data;
        _tabs.AddRange(tabs);
        _perRow = Mathf.Max(1, perRow);
        _bornFrame = Time.frameCount;
        _born = Time.time;
        if (_owner != null) _owner.Released += OnReleased;
    }

    void OnDestroy()
    {
        if (_owner != null) _owner.Released -= OnReleased;
    }

    void OnReleased(AttackData d)
    {
        if (d != _data || Time.frameCount == _bornFrame) return; // 同じフレームは1回目の連射
        int n = 0;
        for (int i = 0; i < _tabs.Count && n < _perRow; i++)
        {
            if (_tabs[i] == null) continue;
            Destroy(_tabs[i].gameObject);
            _tabs[i] = null;
            n++;
        }
    }

    void LateUpdate()
    {
        bool any = _tabs.Exists(t => t != null);
        if (!any || _owner == null || _owner.Current != _data || Time.time - _born > 3f)
        {
            Destroy(gameObject);
            return;
        }
        Bounds b = BrowserDevFx.OwnerBounds(_owner);
        transform.position = new Vector2(b.center.x, b.max.y + 0.5f);
    }
}

// ───────── タブ手裏剣：弾（2-1） ─────────

/// <summary>
/// 【仮】「タブ飛ばし」の弾。2026-11-19（docs/app-feedback-v2-design.md 2-1）：回すのをやめた。タブは水平のまま飛び、上下に 1ドットだけゆれる。
/// タブの下のふちから、タブの列の灰色の細い線（高さ 2ドット）が後ろへ 1マス（タブの列から引きちぎられて飛んできた絵）。
/// 壁・床で × が赤く光って横に閉じる。時間切れはうすくなって消える。判定・速さ・数は変えない
/// </summary>
public class BrowserTabShot : BrowserDevDriver
{
    const float TailLength = 1f;

    Projectile _proj;
    SpriteRenderer _main;
    readonly SpriteRenderer[] _tail = new SpriteRenderer[2];

    protected override void Build()
    {
        _proj = GetComponent<Projectile>();
        Sprite s = BrowserArt.Tab(EdgeColor, BrowserArt.Four(Info.Theme, Index));
        for (int i = 0; i < 2; i++)
        {
            // 尾（近い半分は濃く、遠い半分はうすく）。タブより後ろ
            _tail[i] = BrowserDevFx.Rect(transform, Vector2.zero, Vector2.zero, BrowserDevFx.WithAlpha(BrowserArt.TabGrey, i == 0 ? 0.95f : 0.45f), BrowserDevFx.OrderCore - 1, "Tail");
        }
        _main = BrowserDevFx.Show(transform, s, Vector2.zero, BrowserDevFx.OrderCore, 1f, "BrowserTab");
        LateUpdate();
    }

    void LateUpdate()
    {
        if (_main == null) return;
        Vector2 v = _proj != null ? _proj.Velocity : Info.Direction;
        Vector2 back = v.sqrMagnitude > 0.0001f ? -v.normalized : Vector2.left;
        Vector2 pos = transform.position;
        // 上下に 1ドットだけゆれる（0.12秒ごと）
        float bob = Mathf.FloorToInt((Time.time - Born) / 0.12f) % 2 == 0 ? 0f : BrowserDevFx.Dot;
        _main.transform.SetPositionAndRotation(pos + new Vector2(0f, bob), Quaternion.identity);
        // 尾：タブの下のふちの所から、進む向きと反対へ
        Vector2 root = pos + new Vector2(0f, bob - (BrowserArt.TabH * 0.5f - 1.5f) * BrowserDevFx.Dot) + back * (BrowserArt.TabW * 0.35f * BrowserDevFx.Dot);
        float ang = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
        for (int i = 0; i < 2; i++)
        {
            float half = TailLength * 0.5f;
            Vector2 c = root + back * (half * (i + 0.5f));
            _tail[i].transform.SetPositionAndRotation(c, Quaternion.Euler(0f, 0f, ang));
            Vector3 ls = new Vector3(half / BrowserDevFx.Dot, 2f, 1f);
            Vector3 parent = transform.lossyScale;
            _tail[i].transform.localScale = new Vector3(ls.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), ls.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        }
        bool vis = SourceVisible;
        _main.enabled = vis;
        _tail[0].enabled = _tail[1].enabled = vis;
    }

    protected override void OnVanish(DressInfo info)
    {
        if (_main == null) return;
        float age = Time.time - Born;
        bool timeout = Info.Data != null && age >= Info.Data.Lifetime - 0.06f;
        GameObject go = BrowserDevFx.Loose("TabClose", _main.transform.position, 0f);
        SpriteRenderer tab = BrowserDevFx.Show(go.transform, _main.sprite, Vector2.zero, BrowserDevFx.OrderCore, 1f, "Tab");
        if (timeout)
        {
            BrowserDevFx.Animate(go, 0.15f, (e, k) => tab.color = new Color(1f, 1f, 1f, 1f - k));
        }
        else
        {
            // × が赤く光って、横にしぼんで閉じる（0.1秒）
            SpriteRenderer x = BrowserDevFx.Show(go.transform, BrowserDevFx.Disc(5), BrowserArt.TabCloseLocal, BrowserDevFx.OrderCore + 1, 1f, "XFlash");
            x.color = BrowserDevFx.EnemyEdge;
            BrowserDevFx.Animate(go, 0.1f, (e, k) =>
            {
                tab.transform.localScale = new Vector3(1f - k, 1f, 1f);
                x.transform.localScale = new Vector3(1f - k * 0.5f, 1f - k * 0.5f, 1f);
                x.color = BrowserDevFx.WithAlpha(BrowserDevFx.EnemyEdge, 1f - k);
            });
        }
        Adopt(go);
    }
}

// ───────── タブ増殖（2-2） ─────────

/// <summary>
/// 【仮】上の列のタブ1枚。出た直後は読み込みの輪 → ファビコン。並び終わったらふるえて、ファビコンが赤になる。
/// 落ちる間は上に白いページの帯を引く。着地でつぶれて 404。クリックで消したら × がへこんで横に閉じる
/// </summary>
public class BrowserTabRow : BrowserDevDriver
{
    const float LoadTime = 0.35f;
    static readonly Color HoverTint = new Color(1f, 0.78f, 0.62f, 1f);

    FallingObject _fall;
    DismissableTab _dismiss;
    Transform _holder;
    SpriteRenderer _tab, _spinner, _page;
    Sprite _blue, _red, _loading;
    float _shownAt = -1f;
    float _fallStartY;
    BrowserTabStrip _strip;

    public bool Shown => _shownAt >= 0f;
    public bool Falling => _fall != null && (_fall.IsFalling || _fall.HasLanded);

    protected override void Build()
    {
        _fall = GetComponent<FallingObject>();
        _dismiss = GetComponent<DismissableTab>();
        Color fav = BrowserArt.Four(Info.Theme, 0);
        _blue = BrowserArt.BigTab(EdgeColor, fav, BrowserArt.XGrey);
        _red = BrowserArt.BigTab(EdgeColor, BrowserArt.Red, BrowserArt.XGrey);
        _loading = BrowserArt.BigTab(EdgeColor, Color.clear, BrowserArt.XGrey);
        _holder = BrowserDevFx.Child(transform, "BigTab", Vector2.zero);
        _page = BrowserDevFx.Rect(transform, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.3f), BrowserDevFx.OrderWindow - 2, "Page");
        _page.enabled = false;
        _tab = BrowserDevFx.Show(_holder, _loading, Vector2.zero, BrowserDevFx.OrderWindow - 1, 1f, "Tab");
        _spinner = BrowserDevFx.Show(_holder, BrowserDevFx.Spinner(5, 1), new Vector2(-8.5f, 0f) * BrowserDevFx.Dot, BrowserDevFx.OrderWindow, 1f, "Spinner");
        _spinner.color = BrowserArt.Blue;
        _strip = BrowserTabStrip.Join(this);
        Apply();
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (_tab == null) return;
        float now = Time.time;
        bool vis = SourceVisible;
        if (vis && _shownAt < 0f) _shownAt = now;
        _tab.enabled = vis;

        float since = _shownAt >= 0f ? now - _shownAt : 0f;
        float pop = _shownAt >= 0f ? BrowserDevFx.Pop(since, 0.1f) : 0f;
        bool falling = Falling;

        // 読み込みの輪 → ファビコン（並び終わったら赤）
        bool loading = vis && since < LoadTime && !falling;
        _spinner.enabled = loading;
        if (loading) _spinner.transform.localRotation = Quaternion.Euler(0f, 0f, -900f * since);
        bool danger = _strip != null && _strip.AllShown || (_fall != null && _fall.FallStartTime - now < 0.5f);
        _tab.sprite = loading ? _loading : (danger || falling ? _red : _blue);

        // 並び終わってから落ちるまで：小さくふるえる
        float shake = !falling && _strip != null && _strip.AllShown && Mathf.FloorToInt(now / 0.05f) % 2 == 0 ? BrowserDevFx.Dot : 0f;
        _holder.localPosition = new Vector3(shake, 0f, 0f);
        _holder.localScale = new Vector3(pop, pop, 1f);

        // クリックで消せる間、マウスの下にあれば明るくする（今の絵の色は見えないので、こちらで出す）
        bool hover = _dismiss != null && _dismiss.CanDismiss && MousePointer.TryGetWorldPosition(out Vector2 m) && _dismiss.ContainsPoint(m);
        _tab.color = hover ? HoverTint : Color.white;

        // 落ちる間：上に白いページの帯（長さ 2マスまで）
        if (_fall != null && _fall.IsFalling && !_fall.HasLanded)
        {
            float len = Mathf.Clamp(_fallStartY - transform.position.y, 0f, 2f);
            Vector2 size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(1.5f, 0.6f);
            _page.enabled = len > 0.02f;
            BrowserDevFx.SetRect(_page, new Vector2(0f, size.y * 0.5f + len * 0.5f), new Vector2(size.x - 2f * BrowserDevFx.Dot, len));
        }
        else _page.enabled = false;
    }

    protected override void OnActive(DressInfo info) => _fallStartY = transform.position.y;

    protected override void OnLanded(DressInfo info)
    {
        Vector2 size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(1.5f, 0.6f);
        Vector2 at = transform.position;
        // つぶれる
        GameObject go = BrowserDevFx.Loose("TabSquash", at);
        SpriteRenderer tab = BrowserDevFx.Show(go.transform, _red, Vector2.zero, BrowserDevFx.OrderWindow - 1, 1f, "Tab");
        float bottom = -size.y * 0.5f;
        BrowserDevFx.Animate(go, 0.18f, (e, k) =>
        {
            float sy = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(k * 2f));
            float sx = Mathf.Lerp(1f, 1.2f, Mathf.Clamp01(k * 2f));
            tab.transform.localScale = new Vector3(sx, sy, 1f);
            tab.transform.localPosition = new Vector3(0f, bottom * (1f - sy), 0f);
            tab.color = new Color(1f, 1f, 1f, 1f - k);
        });
        Adopt(go);
        Adopt(BrowserArt.Float404(at + new Vector2(0f, 0.2f), 0.5f, 0.3f));
        // ちり 3個
        Sprite dust = BrowserDevFx.Square(2);
        BrowserDevFx.Burst(at + new Vector2(0f, bottom), Vector2.up, 3, i => dust, i => BrowserArt.LightGrey, BrowserDevFx.OrderShard, 70f, 2f, 4f, 0.25f, 0.35f, 8f, false);
    }

    protected override void OnVanish(DressInfo info)
    {
        if (Landed || _tab == null) return;
        // クリックで消した（または途中で消された）：× がへこんで、タブが横に閉じる
        GameObject go = BrowserDevFx.Loose("TabDismiss", transform.position);
        SpriteRenderer tab = BrowserDevFx.Show(go.transform, BrowserArt.BigTab(EdgeColor, BrowserArt.Red, BrowserArt.Red), Vector2.zero, BrowserDevFx.OrderWindow - 1, 1f, "Tab");
        BrowserDevFx.Animate(go, 0.12f, (e, k) =>
        {
            float p = Mathf.Clamp01((e - 0.03f) / 0.09f);
            tab.transform.localScale = new Vector3(1f - p, e < 0.03f ? 0.92f : 1f, 1f);
        });
        Adopt(go);
    }
}

/// <summary>
/// 【仮】タブ増殖の1回分に1つ：画面の上の端のタブの列の帯（左から伸びる）と、列の右の端の「＋」。
/// 6枚目が出たら帯が上下に 2ドットゆれる（「重くなった」）。全部落ち始めたら消える。
/// 2026-11-19（docs/mac-look-design.md 16）：帯の左の端に信号機（Mac の Chrome はタブの列の左に信号機がある）。
/// 1枚目のタブより左に 14ドット空いているときだけ。タブの位置は動かさない
/// </summary>
public class BrowserTabStrip : MonoBehaviour
{
    static readonly Dictionary<string, BrowserTabStrip> Live = new Dictionary<string, BrowserTabStrip>();
    const float TabHalfW = 13.5f * BrowserDevFx.Dot;   // 大きいタブ（27ドット）の半分
    const float LightsRoom = 14f * BrowserDevFx.Dot;   // 信号機に要る幅（MacUi.LightX 3ドット＋丸 3つ 11ドット＝14ドット）

    readonly List<BrowserTabRow> _tabs = new List<BrowserTabRow>();
    string _key;
    int _count;
    int _joined;
    float _born;
    float _lastJoin;
    float _closeAt = -1f;
    float _y;
    Rect _arena;
    SpriteRenderer _band, _plus, _lights;
    bool _lightsOn;   // 一度出したら、帯が消えるまで出しておく（タブが先に消えても、帯といっしょにうすくなる）
    float _plusX = float.NaN;
    float _plusMovedAt;

    /// <summary>全部のタブが並んだか</summary>
    public bool AllShown
    {
        get
        {
            if (_joined < _count) return false;
            foreach (var t in _tabs) if (t != null && !t.Shown) return false;
            return true;
        }
    }

    public static BrowserTabStrip Join(BrowserTabRow tab)
    {
        DressInfo info = tab.Info;
        string key = (info.Owner != null ? info.Owner.GetInstanceID() : 0) + "/" + BrowserDevFx.NameOf(info.Data);
        if (!Live.TryGetValue(key, out BrowserTabStrip s) || s == null || s._closeAt >= 0f || Time.time - s._lastJoin > 2f || s._joined >= s._count)
        {
            GameObject go = BrowserDevFx.Loose("TabStrip", tab.transform.position);
            s = go.AddComponent<BrowserTabStrip>();
            s.Init(key, info, tab.transform.position.y);
            SkinDresser.Adopt(info, go);
            Live[key] = s;
        }
        s._tabs.Add(tab);
        s._joined++;
        s._lastJoin = Time.time;
        return s;
    }

    void Init(string key, DressInfo info, float y)
    {
        _key = key;
        _count = Mathf.Max(1, info.Count > 0 ? info.Count : (info.Data != null ? info.Data.SpawnCount : 6));
        _born = Time.time;
        _lastJoin = _born;
        _y = y;
        _arena = BattleArena.GetBounds();
        transform.position = new Vector2(_arena.xMin, y);
        float h = info.HitSize.y > 0f ? info.HitSize.y : 0.6f;
        _band = BrowserDevFx.Rect(transform, Vector2.zero, new Vector2(0f, h), BrowserDevFx.WithAlpha(BrowserArt.TabGrey, 0.8f), BrowserDevFx.OrderWindow - 3, "Band");
        _plus = BrowserDevFx.Show(transform, BrowserArt.Plus(), Vector2.zero, BrowserDevFx.OrderWindow, 1f, "Plus");
        _plus.enabled = false;
        // 信号機（帯の子。帯といっしょにゆれて、いっしょに消える）。帯とタブの間の順番
        float lx = (MacUi.LightX + BrowserDevMac.LightsW * 0.5f) * BrowserDevFx.Dot;
        _lights = BrowserDevFx.Show(transform, BrowserDevMac.LightsSprite(), new Vector2(lx, 0f), BrowserDevFx.OrderWindow - 2, 1f, "Lights");
        _lights.enabled = false;
    }

    void OnDestroy()
    {
        if (_key != null && Live.TryGetValue(_key, out BrowserTabStrip s) && s == this) Live.Remove(_key);
    }

    void LateUpdate()
    {
        float now = Time.time;
        float e = now - _born;
        _tabs.RemoveAll(t => t == null);

        // 帯：左から伸びる（0.25秒）。タブの高さ 0.6マス
        float h = _band.transform.localScale.y * BrowserDevFx.Dot;
        float w = _arena.width * Mathf.Clamp01(e / 0.25f);
        int shown = 0;
        float rightmost = float.NegativeInfinity;
        bool anyWaiting = false;
        foreach (var t in _tabs)
        {
            if (t.Shown && !t.Falling)
            {
                shown++;
                rightmost = Mathf.Max(rightmost, t.transform.position.x);
            }
            if (!t.Falling) anyWaiting = true;
        }
        float jitter = shown >= 6 && Mathf.FloorToInt(now / 0.06f) % 2 == 0 ? 2f * BrowserDevFx.Dot : 0f;
        transform.position = new Vector2(_arena.xMin, _y + jitter);

        // 全部落ち始めたら（またはタブが無くなったら）消えていく
        if (_closeAt < 0f && e > 0.5f && (!anyWaiting || _tabs.Count == 0)) _closeAt = now;
        float alpha = 0.8f;
        if (_closeAt >= 0f)
        {
            float k = (now - _closeAt) / 0.25f;
            alpha *= 1f - Mathf.Clamp01(k);
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
        }
        if (e > 15f)
        {
            Destroy(gameObject);
            return;
        }
        _band.color = BrowserDevFx.WithAlpha(BrowserArt.TabGrey, alpha);
        BrowserDevFx.SetRect(_band, new Vector2(w * 0.5f, 0f), new Vector2(w, h));

        // 信号機：帯が伸びて届いたら。いちばん左のタブの左の端まで 14ドット空いていなければ出さない
        float firstLeft = float.PositiveInfinity;
        foreach (var t in _tabs) firstLeft = Mathf.Min(firstLeft, t.transform.position.x - TabHalfW);
        bool room = _tabs.Count > 0 && firstLeft - _arena.xMin >= LightsRoom;
        if (room && w >= LightsRoom) _lightsOn = true;
        _lights.enabled = _lightsOn;
        _lights.color = new Color(1f, 1f, 1f, alpha / 0.8f);

        // 列の右の端の「＋」（いちばん右のタブのとなり）
        if (shown > 0 && _closeAt < 0f)
        {
            float x = rightmost + 0.75f + 0.3f;
            if (!Mathf.Approximately(x, _plusX))
            {
                _plusX = x;
                _plusMovedAt = now;
            }
            _plus.enabled = true;
            float p = BrowserDevFx.Pop(now - _plusMovedAt, 0.1f);
            _plus.transform.position = new Vector2(x, _y + jitter);
            _plus.transform.localScale = new Vector3(p, p, 1f);
        }
        else _plus.enabled = false;
    }
}

// ───────── ポップアップ出現（2-3） ─────────

/// <summary>
/// 【仮】広告のポップアップ。予告は今の点線の枠＋まん中に赤い読み込みの輪。出るときは 0 → 115% → 100% と、うしろに重なった残像 2枚。
/// 判定の 0.1秒は全体が赤く光る。足場の間は灰色のふちと、上の端の白い線。
/// 第2形態（4枚）は、絵だけ 0.05秒ずつずらして「パパパパッ」と出る（15章の3：案どおり。判定は今と同じ瞬間）
/// 2026-11-19（docs/mac-look-design.md 13〜15）：枠は Mac の窓（左上の信号機・右上は空）と、下へ 2ドットの影。
/// 消えるときは、左上の赤い丸がへこんで、まん中へ縮む（右上の × へ縮む動きは、やめた）
/// </summary>
public class BrowserPopup : BrowserDevDriver
{
    const float Stagger = 0.05f;

    FallingObject _fall;
    SpriteRenderer _frameCopy, _spinner;
    Transform _holder;
    SpriteRenderer _window, _flash, _topLine, _shadow;
    readonly SpriteRenderer[] _ghosts = new SpriteRenderer[2];
    Sprite _redSprite, _greySprite;
    Vector2 _size;
    int _kind;
    int _pw = BrowserArt.PopupW, _ph = BrowserArt.PopupH; // 窓の絵の大きさ（ドット）。広告の形（B）で変わる

    protected override void Build()
    {
        _fall = GetComponent<FallingObject>();
        _size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(3f, 2f);
        // 2026-11-19（docs/app-feedback-v2-design.md 2-2）：デザイン6種類を袋から引く（同じ技の中では同じデザインを出さない）
        int key = Info.Owner != null ? Info.Owner.GetInstanceID() : 0;
        _kind = BrowserArt.NextAdKind(key, Index == 0);
        // 広告の形（B）：判定の大きさに合わせて窓を描く（3×2 は今までの 54×36）
        _pw = Mathf.Max(24, Mathf.RoundToInt(_size.x / BrowserDevFx.Dot));
        _ph = Mathf.Max(18, Mathf.RoundToInt(_size.y / BrowserDevFx.Dot));
        _redSprite = BrowserArt.Popup(_kind, EdgeColor, _pw, _ph);
        _greySprite = BrowserArt.Popup(_kind, BrowserArt.PlatformEdge, _pw, _ph);

        _holder = BrowserDevFx.Child(transform, "Popup", Vector2.zero);
        for (int i = 0; i < 2; i++)
        {
            _ghosts[i] = BrowserDevFx.Show(transform, BrowserDevMac.ShapeSprite(_pw, _ph, true), new Vector2(3f * (i + 1), -3f * (i + 1)) * BrowserDevFx.Dot, BrowserDevFx.OrderWindow - 3 - i, 1f, "Ghost");
            _ghosts[i].enabled = false;
        }
        _window = BrowserDevFx.Show(_holder, _redSprite, Vector2.zero, BrowserDevFx.OrderWindow - 1, 1f, "Window");
        // 影（同じ形を下へ 2ドット。判定の外・下にだけ見える）
        _shadow = BrowserDevMac.Shadow(_holder, Vector2.zero, _pw, _ph, false, BrowserDevFx.OrderWindow - 2);
        // 判定の 0.1秒の赤い光：窓と同じ角の丸い形で
        _flash = BrowserDevFx.Show(_holder, BrowserDevMac.ShapeSprite(_pw, _ph, false), Vector2.zero, BrowserDevFx.OrderWindow, 1f, "Flash");
        _flash.color = BrowserDevFx.WithAlpha(BrowserDevFx.EnemyEdge, 0.45f);
        // 足場の上の端の白い線：角が丸いので、両はしを角の欠けの分だけ短くする
        float lineW = _size.x - 2f * BrowserDevMac.Cut(_pw) * BrowserDevFx.Dot;
        _topLine = BrowserDevFx.Rect(_holder, new Vector2(0f, _size.y * 0.5f - BrowserDevFx.Dot * 0.5f), new Vector2(lineW, BrowserDevFx.Dot), Color.white, BrowserDevFx.OrderWindow, "TopLine");
        _window.enabled = _flash.enabled = _topLine.enabled = _shadow.enabled = false;
        _spinner = BrowserDevFx.Show(transform, BrowserDevFx.Spinner(8, 2), Vector2.zero, BrowserDevFx.OrderWindow, 1f, "Spinner");
        _spinner.color = BrowserArt.Red;
        _spinner.enabled = false;
    }

    protected override void OnTelegraph(DressInfo info)
    {
        // 今の点線の枠はそのまま出す（呼んだ側がかくすので、写しを作る）。ずらして出す間も残すため、この物の子にする
        _frameCopy = Replicate(info.CurrentVisual, "FrameCopy", transform);
    }

    void LateUpdate()
    {
        if (_window == null) return;
        float now = Time.time;
        bool appeared = _fall != null ? _fall.AppearedTime >= 0f : Activated;

        if (!appeared)
        {
            if (_frameCopy != null) _frameCopy.enabled = TelegraphSource == null || TelegraphSource.enabled;
            _spinner.enabled = true;
            _spinner.transform.localRotation = Quaternion.Euler(0f, 0f, -720f * (now - Born));
            return;
        }
        _spinner.enabled = false;

        // 絵だけをずらすのは第2形態（3枚以上）だけ（2-3 のハッタリ・15章の3）
        float stagger = Info.Count > 2 ? Index * Stagger : 0f;
        float appearAt = (_fall != null ? _fall.AppearedTime : ActiveAt) + stagger;
        float e = now - appearAt;
        // 絵をずらして出す間（判定はもう出ている）：点線の枠を赤くして残す（見えない物に当たらないように）
        if (_frameCopy != null)
        {
            _frameCopy.enabled = e < 0f;
            if (e < 0f) _frameCopy.color = BrowserDevFx.WithAlpha(BrowserDevFx.EnemyEdge, 0.9f);
        }
        bool vis = e >= 0f && SourceVisible;
        float s = BrowserDevFx.Pop(e, 0.1f);
        _holder.localScale = new Vector3(s, s, 1f);
        _window.enabled = vis;
        _shadow.enabled = vis;
        bool platform = _fall != null ? _fall.IsPlatform && !_fall.IsHitActive : Landed;
        _window.sprite = platform ? _greySprite : _redSprite;
        _topLine.enabled = vis && platform;
        bool hit = _fall != null && _fall.IsHitActive;
        _flash.enabled = vis && hit;
        for (int i = 0; i < 2; i++)
        {
            float a = e >= 0f && e < 0.2f ? (1f - e / 0.2f) * (i == 0 ? 0.55f : 0.3f) : 0f;
            _ghosts[i].enabled = a > 0f;
            _ghosts[i].color = new Color(0.95f, 0.95f, 0.97f, a);
        }
    }

    protected override void OnVanish(DressInfo info)
    {
        if (_window == null || _fall != null && _fall.AppearedTime < 0f) return;
        // 左上の赤い丸が 0.05秒へこむ → ウィンドウがまん中へ縮みながら消える（0.1秒）。1-2 の閉じる動き
        Adopt(BrowserDevMac.CloseAnim(_holder.position, 0f, _window.sprite, _pw, _ph, BrowserDevFx.OrderWindow - 1, true, false));
    }
}

// ───────── 能力：新しいタブ（2-4） ─────────

/// <summary>
/// 【仮】能力「新しいタブ」（ワープ）の絵：出発の所でタブが横に閉じる → 床から 0.5マスの高さに読み込みの線（アプリの差し色）が伸びて、
/// 出発の側から消える → 到着の所で「＋」の新しいタブが開く。壁の手前で止まったら、線の先に 404。線の上にプレイヤーの残像 3枚。
/// 呼ばれ方（担当1 への約束）：Warp 枠で、予告＝消える直前（Owner がまだ出発の所）、本体＝着いた直後（Owner が到着の所）。
/// 本体だけでも出る（Direction に移動量 to − from を入れてもらえれば、出発の所を逆算する）。直接呼ぶなら Play
/// </summary>
public static class BrowserNewTab
{
    class Memo : MonoBehaviour
    {
        public Vector2 From;
        public float At = -10f;
        public int PlayedFrame = -1;
    }

    public static bool Dress(DressPhase phase, DressInfo info)
    {
        GameObject who = info.Owner != null ? info.Owner.gameObject : info.Target;
        if (who == null) return false;
        Memo memo = who.GetComponent<Memo>();
        if (memo == null) memo = who.AddComponent<Memo>();
        Vector2 here = who.transform.position;
        switch (phase)
        {
            case DressPhase.Telegraph:
                memo.From = here;
                memo.At = Time.time;
                TabClose(info, here);
                return true;
            case DressPhase.Active:
            case DressPhase.Landed:
                if (memo.PlayedFrame == Time.frameCount) return true;
                memo.PlayedFrame = Time.frameCount;
                bool told = Time.time - memo.At < 1f;
                Vector2 from = told ? memo.From : (info.Direction.sqrMagnitude > 0.0001f ? here - info.Direction : here);
                memo.At = -10f;
                if (!told) TabClose(info, from);
                PlayTrip(info, from, here);
                return true;
            default:
                return true;
        }
    }

    /// <summary>出発から到着まで、全部の絵を出す（呼ぶ側が場面を分けないとき用）</summary>
    public static void Play(AttackRunner owner, AttackData data, Vector2 from, Vector2 to, AppTheme theme)
    {
        var info = new DressInfo { Owner = owner, Data = data, Theme = theme, Target = owner != null ? owner.gameObject : null, IsEnemy = false };
        TabClose(info, from);
        PlayTrip(info, from, to);
    }

    static void TabClose(DressInfo info, Vector2 feet)
    {
        GameObject go = BrowserDevFx.Loose("NewTabClose", feet + new Vector2(0f, 0.55f));
        SpriteRenderer tab = BrowserDevFx.Show(go.transform, BrowserArt.Tab(Color.white, BrowserArt.Four(info.Theme, 0)), Vector2.zero, BrowserDevFx.OrderHit, 1f, "Tab");
        BrowserDevFx.Animate(go, 0.1f, (e, k) =>
        {
            float p = Mathf.Clamp01(e / 0.08f);
            tab.transform.localScale = new Vector3(1f - p, 1f + p * 0.1f, 1f);
        });
        Adopt(info, go);
    }

    static void PlayTrip(DressInfo info, Vector2 from, Vector2 to)
    {
        float y = from.y + 0.5f;
        Vector2 a = new Vector2(from.x, y), b = new Vector2(to.x, y);
        float dist = Mathf.Abs(b.x - a.x);

        // 読み込みの線（太さ 2ドット）：色はアプリの差し色を左から順に。0.08秒で伸びて、0.2秒で出発の側から消える
        if (dist > 0.05f)
        {
            GameObject line = BrowserDevFx.Loose("NewTabLine", a);
            const int segs = 4;
            var parts = new SpriteRenderer[segs];
            float left = Mathf.Min(a.x, b.x);
            for (int i = 0; i < segs; i++)
            {
                parts[i] = BrowserDevFx.Rect(line.transform, Vector2.zero, Vector2.zero, BrowserArt.Four(info.Theme, i), BrowserDevFx.OrderHit - 1, "Seg");
            }
            float dir = Mathf.Sign(b.x - a.x);
            BrowserDevFx.Animate(line, 0.3f, (e, k) =>
            {
                float grow = Mathf.Clamp01(e / 0.08f);               // 出発 → 到着へ伸びる
                float clip = Mathf.Clamp01((e - 0.1f) / 0.2f);        // 出発の側から消える
                for (int i = 0; i < segs; i++)
                {
                    // 区切り（左から順の色）。線の上の位置は 0（左）〜1（右）
                    float s0 = (float)i / segs, s1 = (float)(i + 1) / segs;
                    // 出発からの割合に直して、見える範囲を求める
                    float lo = dir > 0f ? clip : 1f - grow;
                    float hi = dir > 0f ? grow : 1f - clip;
                    float v0 = Mathf.Max(s0, lo), v1 = Mathf.Min(s1, hi);
                    if (v1 <= v0)
                    {
                        parts[i].enabled = false;
                        continue;
                    }
                    parts[i].enabled = true;
                    float x0 = left + dist * v0, x1 = left + dist * v1;
                    BrowserDevFx.SetRect(parts[i], new Vector2((x0 + x1) * 0.5f - a.x, 0f), new Vector2(x1 - x0, 2f * BrowserDevFx.Dot));
                }
            });
            Adopt(info, line);

            // プレイヤーの残像（アプリの色、うすい）を線の上に 3枚
            SpriteRenderer art = PlayerArt(info.Owner);
            if (art != null && art.sprite != null)
            {
                GameObject ghosts = BrowserDevFx.Loose("NewTabGhosts", from);
                Color tint = BrowserDevFx.WithAlpha(BrowserArt.Four(info.Theme, 0), 0.45f);
                var list = new List<SpriteRenderer>();
                for (int i = 1; i <= 3; i++)
                {
                    float t = i / 4f;
                    Vector2 p = Vector2.Lerp(from, to, t) - from;
                    var go = new GameObject("Ghost");
                    go.transform.SetParent(ghosts.transform, false);
                    go.transform.localPosition = p + ((Vector2)art.transform.position - (Vector2)(info.Owner != null ? info.Owner.transform.position : (Vector3)to));
                    go.transform.localScale = art.transform.lossyScale;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art.sprite;
                    sr.flipX = art.flipX;
                    sr.sortingOrder = art.sortingOrder - 1;
                    sr.color = tint;
                    list.Add(sr);
                }
                BrowserDevFx.Animate(ghosts, 0.25f, (e, k) =>
                {
                    foreach (var sr in list) if (sr != null) sr.color = BrowserDevFx.WithAlpha(tint, tint.a * (1f - k));
                });
                Adopt(info, ghosts);
            }

            // 壁の手前で止まった：線の先に 404
            AttackData d = info.Data;
            if (d != null && d.WarpDistance > 0.1f && dist < d.WarpDistance - 0.1f)
            {
                Adopt(info, BrowserArt.Float404(b + new Vector2(dir * 0.5f, 0.15f), 0.15f, 0.3f));
            }
        }

        // 到着：「＋」の付いた新しいタブが開く（0 → 115% → 100%）。読み込みの輪が半回転して消える
        GameObject open = BrowserDevFx.Loose("NewTabOpen", to + new Vector2(0f, 0.6f));
        SpriteRenderer win = BrowserDevFx.Show(open.transform, BrowserArt.NewTab(BrowserArt.Four(info.Theme, 0)), Vector2.zero, BrowserDevFx.OrderHit, 1f, "Tab");
        SpriteRenderer spin = BrowserDevFx.Show(open.transform, BrowserDevFx.Spinner(5, 1), BrowserArt.NewTabFavicon, BrowserDevFx.OrderHit + 1, 1f, "Spinner");
        spin.color = BrowserArt.Four(info.Theme, 0);
        BrowserDevFx.Animate(open, 0.4f, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.1f);
            open.transform.localScale = new Vector3(s, s, 1f);
            spin.enabled = e < 0.2f;
            spin.transform.localRotation = Quaternion.Euler(0f, 0f, -180f * Mathf.Clamp01(e / 0.2f));
            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            win.color = new Color(1f, 1f, 1f, alpha);
        });
        Adopt(info, open);
    }

    static void Adopt(DressInfo info, GameObject go)
    {
        if (go != null && info.Owner != null) SkinDresser.Adopt(info, go);
    }

    /// <summary>プレイヤーの絵（"Art" という名前の絵。無ければ最初に見つかった絵）</summary>
    static SpriteRenderer PlayerArt(AttackRunner owner)
    {
        if (owner == null) return null;
        SpriteRenderer first = null;
        foreach (var sr in owner.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.enabled || sr.sprite == null) continue;
            if (sr.name == "Art") return sr;
            if (first == null) first = sr;
        }
        return first;
    }
}
