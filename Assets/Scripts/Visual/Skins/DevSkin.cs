using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】Dev のスキン（VS Code と、開発の代用ボス。docs/app-skill-visuals-design.md 3章）。担当2。2026-11-18。
// 何をまねるか：エディタの行番号・エラーの赤丸・赤い波線・ブレークポイント・デバッグの操作バー・文字のカーソル・切り取りと貼り付け。
// コードは読めなくてよい：色つきの短い棒（高さ 2ドット）を並べて「コードの行」に見せる。
// ・エラー弾（長弾。3-1）：予備動作でエラーの数のバッジ（撃つたびに減る）→ 赤丸 × とコードの行と波線 → 床で赤丸が砕ける
// ・赤い波線（床。3-2）：コードの行の下の波線が流れる。判定の瞬間に白く光る。消えるときは緑に直ってチェック
// ・ブレークポイント（柱。3-3・3-6）：赤丸と行番号の帯 → 止まった行の黄色の柱・矢印・操作バー → 左右から閉じる（能力は 2026-11-19 に見せ方を変えた：赤丸 → 黄色い行 → ⏸）
// ・エラーウィンドウ落下（落下物。3-4）：画面の上に OUTPUT の見出し → エラーログの行の束が落ちる → 行がばらけて左右へ流れる（2026-11-19：ダイアログ → ログ）
// ・瞬間移動（ワープ。3-5）：選択の青 → ⌘X → 文字のカーソル → 上の行から貼り付け → ⌘V。切り取った跡の点線
// ・当たった絵（3-7）：緑のチェックと { }。エラーの絵は赤丸 × と Error、足元に赤い波線
// 当たり判定・秒数は変えない。
// 2026-11-19（docs/mac-look-design.md 17・18・20）：エラーのダイアログの枠を Mac の暗い窓に、ダウンを Mac の「予期しない理由で終了」のダイアログにした。
public class DevSkin : AppSkin
{
    public override SkinId Id => SkinId.Dev;
    public override string NotificationTitle => "Build succeeded";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        try
        {
            switch (FrameFor(frame, info.Data))
            {
                case AttackFrame.LongShot:
                    // 予備動作（攻撃する人の子に出す空の物）はエラーの数のバッジ。弾はエラーの印とコードの行
                    if (info.Target != null && !info.Target.TryGetComponent(out Projectile _))
                        return BrowserDevDriver.Claim<DevErrorBadge>(info, phase) != null;
                    return BrowserDevDriver.Claim<DevErrorShot>(info, phase) != null;
                case AttackFrame.Floor:
                    return BrowserDevDriver.Claim<DevRedWave>(info, phase) != null;
                case AttackFrame.Pillar:
                    return BrowserDevDriver.Claim<DevBreakpoint>(info, phase) != null;
                case AttackFrame.Falling:
                    return BrowserDevDriver.Claim<DevErrorWindow>(info, phase) != null;
                case AttackFrame.Warp:
                    if (!info.IsEnemy) return false; // プレイヤーのワープ（新しいタブ）は Browser
                    return BrowserDevDriver.Claim<DevWarp>(info, phase) != null;
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DevSkin.Dress：{e}");
            return false;
        }
    }

    static AttackFrame FrameFor(AttackFrame frame, AttackData data)
    {
        string n = BrowserDevFx.NameOf(data);
        if (Has(n, "errorbullet")) return AttackFrame.LongShot;
        if (Has(n, "redwave")) return AttackFrame.Floor;
        if (Has(n, "errorwindowfall")) return AttackFrame.Falling;
        if (Has(n, "vscode_warp")) return AttackFrame.Warp;
        if (Has(n, "breakpoint")) return AttackFrame.Pillar;
        return frame;
    }

    static bool Has(string s, string part) => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // ───────── 当たった絵（3-7） ─────────

    public override bool Hit(HitInfo info)
    {
        try
        {
            Vector2 at = BrowserDevFx.HitAnchor(info);
            Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
            int shards, size;
            float life, scale = 1f;
            switch (info.Strength)
            {
                case HitStrength.Small:
                    DevArt.Fragments(info.Point, dir, 1);
                    return true;
                case HitStrength.Light: size = 12; life = 0.2f; shards = 3; break;
                case HitStrength.Finisher: size = 22; life = 0.3f; shards = 12; scale = 1.5f; break;
                default: size = 22; life = 0.3f; shards = 6; break;
            }
            DevArt.Fragments(info.Point, dir, shards);
            DevArt.CheckBraces(at, size, life, scale);
            // 能力ブレークポイント（2026-11-19：docs/app-feedback-v2-design.md 3-3）：
            // 相手の高さに黄色い行の帯（横 3マス）が 0.4秒残り、頭の上に ⏸。相手は少しおくれて 0.15秒だけ黄色く光る（白く光るのが先に来るので）
            if (Has(BrowserDevFx.NameOf(info.Data), "breakpoint") && info.Target != null)
            {
                DevArt.StoppedRow(info.Target, 0.4f);
                DevArt.PauseMark(BrowserDevFx.HeadOf(info.Target, info.Point, 0.35f), 0.4f, info.Target.transform);
                DevArt.YellowFlashLater(info.Target, 0.06f, 0.15f);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DevSkin.Hit：{e}");
            return false;
        }
    }

    public override bool Error(HitInfo info)
    {
        try
        {
            if (info.Strength == HitStrength.Down)
            {
                DevArt.CrashedWindow(BrowserDevFx.HeadOf(info.Target, info.Point, 0.3f), 1.0f, info.AppIcon, info.Theme);
                return true;
            }
            Vector2 head = BrowserDevFx.HeadOf(info.Target, info.Point);
            DevArt.ErrorPop(head, 0.4f);
            DevArt.FeetWave(BrowserDevFx.FeetOf(info.Target, info.Point), 0.4f, info.Target != null ? info.Target.transform : null);
            // ボスのブレークポイントに当たった：のけぞりの間、頭の上に黄色の ⏸
            if (Has(BrowserDevFx.NameOf(info.Data), "breakpoint"))
            {
                float stun = info.Data != null ? Mathf.Max(0.25f, info.Data.HitStun) : 0.25f;
                DevArt.PauseMark(BrowserDevFx.HeadOf(info.Target, info.Point, 0.15f), stun, info.Target != null ? info.Target.transform : null);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DevSkin.Error：{e}");
            return false;
        }
    }
}

/// <summary>【仮】Dev の色と絵（コードの中のドット絵）</summary>
public static class DevArt
{
    public static readonly Color Bg = RuntimeSprites.Hex("1E1E1E");
    public static readonly Color Panel = RuntimeSprites.Hex("252526");
    public static readonly Color LineNo = RuntimeSprites.Hex("858585");
    public static readonly Color Blue = RuntimeSprites.Hex("007ACC");
    public static readonly Color ErrorRed = RuntimeSprites.Hex("F14C4C");
    public static readonly Color Stopped = RuntimeSprites.Hex("FFCC00");
    public static readonly Color Select = RuntimeSprites.Hex("264F78");
    public static readonly Color CodeBlue = RuntimeSprites.Hex("569CD6");
    public static readonly Color CodeOrange = RuntimeSprites.Hex("CE9178");
    public static readonly Color CodeGreen = RuntimeSprites.Hex("4EC9B0");
    public static readonly Color CodeYellow = RuntimeSprites.Hex("DCDCAA");
    public static readonly Color CodePurple = RuntimeSprites.Hex("C586C0");
    public static readonly Color BreakRed = RuntimeSprites.Hex("E51400");
    static readonly Color[] CodeColors = { CodeBlue, CodeOrange, CodeGreen, CodeYellow, CodePurple };
    static readonly Color32 White32 = new Color32(255, 255, 255, 255);

    static string H(Color c) => ColorUtility.ToHtmlStringRGBA(c);

    public static Color Code(int i) => CodeColors[Mathf.Abs(i) % CodeColors.Length];

    /// <summary>アプリの色（VS Code は青。色の組があればその代表の色）</summary>
    public static Color AppColor(AppTheme theme)
    {
        if (theme == null || theme.name.IndexOf("VSCode", StringComparison.OrdinalIgnoreCase) >= 0) return Blue;
        return BrowserDevFx.Opaque(theme.Main);
    }

    // ───────── 赤丸 ×（エラーの印） ─────────

    static void StampErrorDot(BrowserDevGrid g, int x, int y, int d)
    {
        g.Disc(x, y, d, 'R');
        int m = Mathf.Max(2, Mathf.RoundToInt(d * 0.28f));
        int s = d - m * 2;
        BrowserDevFx.StampX(g, x + m, y + m, s, 'W');
        if (d >= 12) BrowserDevFx.StampX(g, x + m + 1, y + m, s - 1, 'W'); // 太くする
    }

    public static Sprite ErrorDot(int d)
    {
        var g = new BrowserDevGrid(d, d);
        StampErrorDot(g, 0, 0, d);
        return BrowserDevFx.Art($"ErrorDot_{d}", g, new Dictionary<char, Color32> { { 'R', ErrorRed }, { 'W', White32 } });
    }

    // ───────── 波線（周期 4ドット・高さ 3ドット） ─────────

    static int WaveRow(int x, int phase)
    {
        int v = ((x + phase) % 4 + 4) % 4;
        return v == 0 ? 2 : v == 2 ? 0 : 1;
    }

    static void StampWave(BrowserDevGrid g, int x0, int y0, int w, int phase, char c)
    {
        for (int x = 0; x < w; x++) g.Set(x0 + x, y0 + WaveRow(x, phase), c);
    }

    // ───────── エラー弾（長弾。22×9） ─────────

    /// <summary>エラー弾。phase は波線の位置（0〜3）、colorShift はコードの棒の色の並び</summary>
    public static Sprite ErrorBullet(int phase, int colorShift, Color edge)
    {
        var g = new BrowserDevGrid(22, 9);
        // エラーのある行（暗い地に、色つきの棒と赤い波線）
        g.Rect(0, 1, 14, 7, 'E');
        g.Rect(1, 2, 12, 5, 'K');
        g.Rect(2, 2, 3, 2, 'A');
        g.Rect(6, 2, 3, 2, 'B');
        g.Rect(10, 2, 2, 2, 'C');
        StampWave(g, 1, 4, 12, phase, 'S');
        // 先頭の赤丸 ×（9ドット）
        StampErrorDot(g, 13, 0, 9);
        var pal = new Dictionary<char, Color32>
        {
            { 'E', edge }, { 'K', Bg }, { 'A', Code(colorShift) }, { 'B', Code(colorShift + 1) }, { 'C', Code(colorShift + 2) },
            { 'S', ErrorRed }, { 'R', ErrorRed }, { 'W', White32 },
        };
        return BrowserDevFx.Art($"ErrBullet_{phase}_{colorShift % 5}_{H(edge)}", g, pal);
    }

    // ───────── エラーの数のバッジ（17×11） ─────────

    public static Sprite Badge(int n)
    {
        n = Mathf.Clamp(n, 0, 9);
        var g = new BrowserDevGrid(17, 11);
        g.Rect(1, 0, 15, 11, 'E');
        g.Rect(0, 1, 17, 9, 'E');
        g.Rect(1, 1, 15, 9, 'K');
        g.Disc(2, 2, 7, 'R');
        BrowserDevFx.StampX(g, 4, 4, 3, 'W');
        BrowserDevFont.Stamp(g, n.ToString(), 11, 3, 'W');
        var pal = new Dictionary<char, Color32> { { 'E', BrowserDevFx.EnemyEdge }, { 'K', Panel }, { 'R', ErrorRed }, { 'W', White32 } };
        return BrowserDevFx.Art($"ErrBadge_{n}", g, pal);
    }

    // ───────── 赤い波線（床） ─────────

    static readonly int[] BarLens = { 5, 8, 3, 6, 9, 4, 7 };

    /// <summary>コードの行（うすい灰色の棒、上 2ドット）と、その下の波線（下 3ドット）。w×h ドット</summary>
    public static Sprite Wave(int w, int h, int phase, Color wave, bool withCode = true)
    {
        w = Mathf.Clamp(w, 4, 200);
        h = Mathf.Clamp(h, 3, 40);
        var g = new BrowserDevGrid(w, h);
        if (withCode && h >= 6)
        {
            int x = 1, i = 0;
            while (x < w - 1)
            {
                int len = Mathf.Min(BarLens[i % BarLens.Length], w - 1 - x);
                g.Rect(x, h - 6, len, 2, 'G');
                x += len + 2;
                i++;
            }
        }
        StampWave(g, 0, h - 3, w, phase, 'S');
        var pal = new Dictionary<char, Color32> { { 'G', new Color(0.52f, 0.52f, 0.52f, 0.5f) }, { 'S', wave } };
        return BrowserDevFx.Art($"Wave_{w}_{h}_{phase}_{H(wave)}_{(withCode ? 1 : 0)}", g, pal);
    }

    // ───────── ブレークポイント ─────────

    /// <summary>ブレークポイントの赤丸（9ドット。ふち 1ドット）</summary>
    public static Sprite BreakpointDot(Color edge)
    {
        var g = new BrowserDevGrid(9, 9);
        g.Disc(1, 1, 7, 'R');
        g.Set(3, 2, 'L');
        g.Set(2, 3, 'L');
        g.Outline('E');
        var pal = new Dictionary<char, Color32> { { 'R', BreakRed }, { 'L', RuntimeSprites.Hex("FF6B5E") }, { 'E', edge } };
        return BrowserDevFx.Art($"BpDot_{H(edge)}", g, pal);
    }

    /// <summary>下向きの黄色の矢印（▶ を下向きに。幅 10ドット＋ふち）</summary>
    public static Sprite DownArrow()
    {
        var g = new BrowserDevGrid(12, 7);
        for (int r = 0; r < 5; r++) g.Rect(1 + r, 1 + r, 10 - r * 2, 1, 'Y');
        g.Outline('O');
        return BrowserDevFx.Art("BpArrow", g, new Dictionary<char, Color32> { { 'Y', Stopped }, { 'O', RuntimeSprites.Hex("5C4A00") } });
    }

    /// <summary>デバッグの小さい操作バー（28×8。⏸ ▶ ⤵ ■）</summary>
    public static Sprite DebugBar()
    {
        var g = new BrowserDevGrid(28, 8);
        g.Rect(0, 0, 28, 8, 'L');
        g.Rect(1, 1, 26, 6, 'K');
        // つまみ（点 2列）
        for (int y = 2; y <= 5; y += 1) if (y % 2 == 0) { g.Set(2, y, 'D'); g.Set(3, y + 1, 'D'); }
        // ⏸
        g.Rect(6, 1, 2, 6, 'U');
        g.Rect(9, 1, 2, 6, 'U');
        // ▶
        g.Set(13, 1, 'G');
        g.Rect(13, 2, 2, 1, 'G');
        g.Rect(13, 3, 3, 2, 'G');
        g.Rect(13, 5, 2, 1, 'G');
        g.Set(13, 6, 'G');
        // ⤵（またぐ矢印と点）
        g.Rect(18, 1, 3, 1, 'U');
        g.Set(17, 2, 'U');
        g.Set(21, 2, 'U');
        g.Rect(20, 3, 3, 1, 'U');
        g.Set(21, 4, 'U');
        g.Set(19, 6, 'U');
        // ■
        g.Rect(23, 2, 4, 4, 'Q');
        var pal = new Dictionary<char, Color32>
        {
            { 'L', RuntimeSprites.Hex("454545") }, { 'K', Panel }, { 'D', LineNo }, { 'U', RuntimeSprites.Hex("75BEFF") },
            { 'G', RuntimeSprites.Hex("89D185") }, { 'Q', RuntimeSprites.Hex("F48771") },
        };
        return BrowserDevFx.Art("DebugBar", g, pal);
    }

    /// <summary>⏸（8ドット＋暗いふち）</summary>
    public static Sprite Pause(Color color)
    {
        var g = new BrowserDevGrid(10, 10);
        g.Rect(1, 1, 3, 8, 'Y');
        g.Rect(6, 1, 3, 8, 'Y');
        g.Outline('O');
        return BrowserDevFx.Art($"Pause_{H(color)}", g, new Dictionary<char, Color32> { { 'Y', color }, { 'O', new Color32(30, 30, 30, 230) } });
    }

    /// <summary>赤い「！」の三角（8ドット＋白いふち）</summary>
    public static Sprite Alert()
    {
        var g = new BrowserDevGrid(11, 10);
        string[] rows =
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
        g.Stamp(rows, 1, 1);
        g.Outline('O');
        return BrowserDevFx.Art("Alert", g, new Dictionary<char, Color32> { { 'R', BrowserDevFx.EnemyEdge }, { 'W', White32 }, { 'O', White32 } });
    }

    // ───────── エラーのダイアログ（落下物。54×36） ─────────

    public const int ErrorW = 54, ErrorH = 36;

    /// <summary>
    /// VS Code のエラーのダイアログ。2026-11-19（docs/mac-look-design.md 17）：枠は Mac の暗い窓（角の丸いふち・丸い信号機・まん中の題名の棒）。
    /// 中身（赤丸 ×・字・灰色の行・青いボタン）は今のまま。タイトルバーが 1行高くなった分、中身は 1行下へ
    /// </summary>
    public static Sprite ErrorWindow(string text, Color edge)
    {
        var g = new BrowserDevGrid(ErrorW, ErrorH);
        BrowserDevMac.StampWindow(g, 0, 0, ErrorW, ErrorH, true, edge);
        StampErrorDot(g, 4, 11, 12);
        BrowserDevFont.Stamp(g, text, 19, 12, 'W');
        g.Rect(19, 21, 28, 1, 'G');
        g.Rect(19, 24, 19, 1, 'G');
        // 青いボタン（角を 1ドット欠く）
        g.Rect(38, 28, 12, 6, 'B');
        g.Rect(41, 30, 6, 2, 'W');
        Color32 bg = BrowserDevMac.WindowBg(true);
        g.Put(38, 28, bg);
        g.Put(49, 28, bg);
        g.Put(38, 33, bg);
        g.Put(49, 33, bg);
        var pal = new Dictionary<char, Color32> { { 'R', ErrorRed }, { 'W', White32 }, { 'G', LineNo }, { 'B', Blue } };
        return BrowserDevFx.Art($"ErrWindowMac_{text}_{H(edge)}", g, pal);
    }

    // ───────── エラーログの束（落下物。54×36。2026-11-19：docs/app-feedback-v2-design.md 3-2） ─────────

    public static readonly Color LogBlue = RuntimeSprites.Hex("3794FF");
    public const int LogW = 54, LogH = 36;

    /// <summary>
    /// 下のパネルに流れる赤いログの「行の束」（暗いパネルの切れはし。左のふちに赤い線 2ドット）。
    /// 1行目：赤丸 ×（5ドット）と赤い長い棒（Error:）。2・3行目：右へ 4ドット下げた灰色の棒と青い短い棒（at main.ts:12。ファイル名だけ青）
    /// </summary>
    public static Sprite ErrorLog(Color edge)
    {
        var g = new BrowserDevGrid(LogW, LogH);
        g.Rect(0, 0, LogW, LogH, 'K');
        g.Frame(0, 0, LogW, LogH, 'E');
        g.Rect(1, 1, 2, LogH - 2, 'R');            // 左のふちの赤い線
        StampLogRows(g, 0, true);
        var pal = new Dictionary<char, Color32>
        {
            { 'K', Bg }, { 'E', edge }, { 'R', ErrorRed }, { 'W', White32 }, { 'G', LineNo }, { 'B', LogBlue },
        };
        return BrowserDevFx.Art($"ErrLog_{H(edge)}", g, pal);
    }

    /// <summary>ログの行を書く（y0 から。full なら3行、でなければ1行目だけ）</summary>
    static void StampLogRows(BrowserDevGrid g, int y0, bool full)
    {
        // 1行目
        StampErrorDot(g, 5, y0 + 6, 5);
        g.Rect(12, y0 + 7, 34, 3, 'R');
        if (!full) return;
        // 2・3行目（右へ 4ドット下げる）
        g.Rect(9, y0 + 18, 18, 2, 'G');
        g.Rect(29, y0 + 18, 11, 2, 'B');
        g.Rect(9, y0 + 26, 14, 2, 'G');
        g.Rect(25, y0 + 26, 13, 2, 'B');
    }

    /// <summary>ログの1行（束の上の、流れてきた跡・着地でばらける行）。row：0＝1行目（赤）、1・2＝灰色と青</summary>
    public static Sprite LogRow(int row)
    {
        row = Mathf.Clamp(row, 0, 2);
        var g = new BrowserDevGrid(LogW, 8);
        g.Rect(0, 0, LogW, 8, 'K');
        g.Rect(1, 0, 2, 8, 'R');
        if (row == 0)
        {
            StampErrorDot(g, 5, 1, 5);
            g.Rect(12, 2, 34, 3, 'R');
        }
        else if (row == 1)
        {
            g.Rect(9, 3, 18, 2, 'G');
            g.Rect(29, 3, 11, 2, 'B');
        }
        else
        {
            g.Rect(9, 3, 14, 2, 'G');
            g.Rect(25, 3, 13, 2, 'B');
        }
        var pal = new Dictionary<char, Color32> { { 'K', Bg }, { 'R', ErrorRed }, { 'W', White32 }, { 'G', LineNo }, { 'B', LogBlue } };
        return BrowserDevFx.Art($"ErrLogRow_{row}", g, pal);
    }

    /// <summary>パネルの見出しの帯（54×9。暗い地に小さい字 OUTPUT、右に赤い × と数字）。withX＝false で × を消す（点滅用）</summary>
    public static Sprite OutputHeader(bool withX, int count)
    {
        count = Mathf.Clamp(count, 1, 9);
        var g = new BrowserDevGrid(LogW, 9);
        g.Rect(0, 0, LogW, 9, 'P');
        g.Rect(0, 8, LogW, 1, 'L');
        BrowserDevFont.Stamp(g, "OUTPUT", 3, 2, 'W');
        g.Rect(3, 7, BrowserDevFont.Width("OUTPUT"), 1, 'U');  // 選ばれているタブの下線
        if (withX) StampErrorDot(g, 40, 2, 5);
        BrowserDevFont.Stamp(g, count.ToString(), 47, 2, 'W');
        var pal = new Dictionary<char, Color32>
        {
            { 'P', Panel }, { 'L', RuntimeSprites.Hex("454545") }, { 'W', White32 }, { 'U', Blue }, { 'R', ErrorRed },
        };
        return BrowserDevFx.Art($"OutputHeader_{(withX ? 1 : 0)}_{count}", g, pal);
    }

    // ───────── 能力のブレークポイント（2026-11-19：docs 3-3） ─────────

    /// <summary>大きい赤丸（14ドット。白いふち 1ドット）。行の左の赤丸</summary>
    public static Sprite BreakpointDotBig()
    {
        var g = new BrowserDevGrid(14, 14);
        g.Disc(1, 1, 12, 'R');
        g.Rect(4, 3, 2, 1, 'L');
        g.Rect(3, 4, 1, 2, 'L');
        g.Outline('E');
        var pal = new Dictionary<char, Color32> { { 'R', ErrorRed }, { 'L', RuntimeSprites.Hex("FF8A80") }, { 'E', White32 } };
        return BrowserDevFx.Art("BpDotBig", g, pal);
    }

    /// <summary>右向きの黄色の矢印 ▶（止まった行の印。高さ size ドット＋暗いふち）</summary>
    public static Sprite RightArrow(int size = 7)
    {
        size = Mathf.Max(3, size | 1);
        int half = size / 2;
        var g = new BrowserDevGrid(half + 3, size + 2);
        for (int r = 0; r < size; r++)
        {
            int len = r <= half ? r + 1 : size - r;
            g.Rect(1, 1 + r, len, 1, 'Y');
        }
        g.Outline('O');
        return BrowserDevFx.Art($"BpArrowR_{size}", g, new Dictionary<char, Color32> { { 'Y', Stopped }, { 'O', RuntimeSprites.Hex("5C4A00") } });
    }

    /// <summary>アプリのアイコンが無いときの代わり（16ドットの角の丸い四角にアプリの色、白い { }）</summary>
    public static Sprite PlaceholderIcon(Color color)
    {
        var g = new BrowserDevGrid(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (BrowserDevMac.Inside(16, 16, 2, x, y)) g.Set(x, y, 'A');
        BrowserDevFont.Stamp(g, "{}", 5, 5, 'W');
        return BrowserDevFx.Art($"DevIcon_{H(color)}", g, new Dictionary<char, Color32> { { 'A', color }, { 'W', White32 } });
    }

    // ───────── キー（⌘ X・⌘ V。9ドット） ─────────
    // 2026-11-19（docs/mac-look-design.md 20）：キーの絵は MacUi.KeySprite に移した（見た目は同じ。'*'＝⌘）。
    // 下の CmdRows と自分の絵は、MacUi が絵を作れなかったときの控え【仮】

    static readonly string[] CmdRows =
    {
        "##.##",
        "#.#.#",
        ".###.",
        "#.#.#",
        "##.##",
    };

    public static Sprite Key(char key)
    {
        try
        {
            Sprite mac = MacUi.KeySprite(key);
            if (mac != null) return mac;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DevArt.Key：MacUi.KeySprite が使えないので、今までの絵にします（{e.Message}）");
        }
        var g = new BrowserDevGrid(9, 9);
        g.Rect(1, 0, 7, 8, 'K');
        g.Rect(0, 1, 9, 6, 'K');
        g.Rect(1, 8, 7, 1, 'S');
        g.Set(0, 7, 'S');
        g.Set(8, 7, 'S');
        if (key == '*') g.Stamp(CmdRows, 2, 1, 'D');
        else BrowserDevFont.Stamp(g, key.ToString(), 3, 1, 'D');
        var pal = new Dictionary<char, Color32> { { 'K', RuntimeSprites.Hex("E5E5EA") }, { 'S', RuntimeSprites.Hex("A0A0A5") }, { 'D', Bg } };
        return BrowserDevFx.Art($"Key_{(key == '*' ? "Cmd" : key.ToString())}", g, pal);
    }

    /// <summary>コードの記号（{ } ; &lt; &gt; /）の小さい絵</summary>
    public static Sprite Symbol(int i)
    {
        string syms = "{};<>/";
        char c = syms[Mathf.Abs(i) % syms.Length];
        return BrowserDevFx.Text(c.ToString(), Code(i), true);
    }

    /// <summary>色つきの棒（4×2ドット）</summary>
    public static Sprite Bar()
    {
        var g = new BrowserDevGrid(4, 2);
        g.Rect(0, 0, 4, 2, 'X');
        return BrowserDevFx.Art("CodeBar", g, new Dictionary<char, Color32> { { 'X', White32 } });
    }

    // ───────── 出す物 ─────────

    /// <summary>コードの記号と色つきの棒のかけら</summary>
    public static void Fragments(Vector2 at, Vector2 dir, int count)
    {
        int start = UnityEngine.Random.Range(0, 6);
        Sprite bar = Bar();
        BrowserDevFx.Burst(at, dir, count, i => i % 3 == 2 ? bar : Symbol(start + i), i => i % 3 == 2 ? Code(start + i) : Color.white, unscaled: true);
    }

    /// <summary>緑のチェックが「パッ」と出て、まわりの { } が左右へ開く（当たった絵。上へ 0.4マス浮きながら消える）</summary>
    public static void CheckBraces(Vector2 at, int size, float life, float scale)
    {
        GameObject go = BrowserDevFx.Loose("HitCheck", at);
        SpriteRenderer check = BrowserDevFx.Show(go.transform, BrowserArt.Check(size, CodeGreen), Vector2.zero, BrowserDevFx.OrderHit + 1, scale, "Check");
        float braceScale = scale * (size >= 20 ? 2f : 1f);
        SpriteRenderer l = BrowserDevFx.Show(go.transform, BrowserDevFx.Text("{", CodeGreen, true), Vector2.zero, BrowserDevFx.OrderHit, braceScale, "L");
        SpriteRenderer r = BrowserDevFx.Show(go.transform, BrowserDevFx.Text("}", CodeGreen, true), Vector2.zero, BrowserDevFx.OrderHit, braceScale, "R");
        float open = (size * 0.5f + 3f) * BrowserDevFx.Dot * scale;
        var all = new[] { check, l, r };
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            go.transform.position = at + Vector2.up * (0.4f * k);
            float p = BrowserDevFx.Pop(e, 0.08f) * scale;
            check.transform.localScale = new Vector3(p, p, 1f);
            float o = open * Mathf.Clamp01(e / 0.12f);
            l.transform.localPosition = new Vector3(-o, 0f, 0f);
            r.transform.localPosition = new Vector3(o, 0f, 0f);
            float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            foreach (var s in all) s.color = new Color(1f, 1f, 1f, a);
        }, true);
    }

    /// <summary>赤丸 ×（14ドット）と、その右に Error（エラーの絵）</summary>
    public static void ErrorPop(Vector2 head, float life)
    {
        GameObject go = BrowserDevFx.Loose("DevError", head);
        Transform body = BrowserDevFx.Child(go.transform, "Body", Vector2.zero);
        Sprite text = BrowserDevFx.Text("Error", White32, true);
        float tw = text.bounds.size.x;
        float dw = 14f * BrowserDevFx.Dot;
        float total = dw + 2f * BrowserDevFx.Dot + tw;
        BrowserDevFx.Show(body, ErrorDot(14), new Vector2(-total * 0.5f + dw * 0.5f, 0f), BrowserDevFx.OrderHit, 1f, "Dot");
        BrowserDevFx.Show(body, text, new Vector2(total * 0.5f - tw * 0.5f, 0f), BrowserDevFx.OrderHit + 1, 1f, "Text");
        var rs = go.GetComponentsInChildren<SpriteRenderer>();
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.1f);
            body.localScale = new Vector3(s, s, 1f);
            float a = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
            foreach (var sr in rs) if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
        }, true);
    }

    /// <summary>プレイヤーの足元に赤い波線（幅 1マス）。follow があれば付いていく</summary>
    public static void FeetWave(Vector2 feet, float life, Transform follow)
    {
        GameObject go = BrowserDevFx.Loose("FeetWave", feet);
        var frames = new Sprite[4];
        for (int i = 0; i < 4; i++) frames[i] = Wave(18, 3, i, ErrorRed, false);
        SpriteRenderer sr = BrowserDevFx.Show(go.transform, frames[0], new Vector2(0f, 1.5f * BrowserDevFx.Dot), BrowserDevFx.OrderHit, 1f, "Wave");
        Vector2 offset = follow != null ? feet - (Vector2)follow.position : Vector2.zero;
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            if (follow != null) go.transform.position = (Vector2)follow.position + offset;
            sr.sprite = frames[Mathf.FloorToInt(e / 0.06f) % 4];
            sr.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        }, true);
    }

    /// <summary>頭の上の ⏸（黄色）。follow があれば付いていく</summary>
    public static void PauseMark(Vector2 at, float life, Transform follow)
    {
        GameObject go = BrowserDevFx.Loose("Pause", at);
        SpriteRenderer sr = BrowserDevFx.Show(go.transform, Pause(Stopped), Vector2.zero, BrowserDevFx.OrderMark, 1f, "Pause");
        Vector2 offset = follow != null ? at - (Vector2)follow.position : Vector2.zero;
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            if (follow != null) go.transform.position = (Vector2)follow.position + offset;
            float s = BrowserDevFx.Pop(e, 0.08f);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.enabled = k < 0.8f || Mathf.FloorToInt(e / 0.04f) % 2 == 0;
        }, true);
    }

    /// <summary>当たった相手の高さに、止まった行の黄色い帯（横 3マス×高さ 1マス）。左の端に ▶。life 秒で消える（相手に付いていく）</summary>
    public static void StoppedRow(GameObject target, float life)
    {
        if (target == null) return;
        Bounds b = BrowserDevFx.BoundsOf(target);
        Vector2 at = b.center;
        GameObject go = BrowserDevFx.Loose("StoppedRow", at);
        float d = BrowserDevFx.Dot;
        SpriteRenderer band = BrowserDevFx.Rect(go.transform, Vector2.zero, new Vector2(3f, 1f), BrowserDevFx.WithAlpha(Stopped, 0.45f), BrowserDevFx.OrderPillarDeco, "Band");
        SpriteRenderer top = BrowserDevFx.Rect(go.transform, new Vector2(0f, 0.5f - d * 0.5f), new Vector2(3f, d), BrowserDevFx.WithAlpha(Stopped, 0.9f), BrowserDevFx.OrderPillarDeco, "Top");
        SpriteRenderer bottom = BrowserDevFx.Rect(go.transform, new Vector2(0f, -0.5f + d * 0.5f), new Vector2(3f, d), BrowserDevFx.WithAlpha(Stopped, 0.9f), BrowserDevFx.OrderPillarDeco, "Bottom");
        SpriteRenderer arrow = BrowserDevFx.Show(go.transform, RightArrow(7), new Vector2(-1.5f + 4f * d, 0f), BrowserDevFx.OrderPillarDeco + 1, 1f, "Arrow");
        var all = new[] { band, top, bottom, arrow };
        var baseA = new float[all.Length];
        for (int i = 0; i < all.Length; i++) baseA[i] = all[i].color.a;
        Transform follow = target.transform;
        Vector2 offset = at - (Vector2)follow.position;
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            if (follow != null) go.transform.position = (Vector2)follow.position + offset;
            float open = Mathf.Clamp01(e / 0.06f);
            go.transform.localScale = new Vector3(open, 1f, 1f);
            float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].color = BrowserDevFx.WithAlpha(all[i].color, baseA[i] * a);
        }, true);
    }

    /// <summary>delay 秒あとに、相手を seconds 秒だけ黄色く光らせる（実時間で待つ。ヒットストップの間も数える）</summary>
    public static void YellowFlashLater(GameObject target, float delay, float seconds)
    {
        if (target == null) return;
        GameObject go = BrowserDevFx.Loose("YellowFlash", target.transform.position);
        bool done = false;
        BrowserDevFx.Animate(go, delay + 0.02f, (e, k) =>
        {
            if (done || e < delay) return;
            done = true;
            if (target != null) FlashOverlay.Play(target, Stopped, Stopped, seconds);
        }, true);
    }

    const float HudInset = 1.9f; // 画面の上の HUD の高さ（PremiereArt.HudInset と同じ）。ダイアログをこの下に収める

    /// <summary>
    /// ダウン：Mac の「予期しない理由で終了」のダイアログ（MacUi.PlaceAlert、暗い）が、プレイヤーの頭の上に出る（docs/mac-look-design.md 18・1-9）。
    /// 左にアプリのアイコンと黄色い三角、題名 quit unexpectedly.（アプリ名は字で書かない。アイコンで分かる）、右下に Ignore と青い Reopen。
    /// 幅は 9マスまで。画面の外に出ないように、横と上下を画面に収める。head＝頭の上（ダイアログの下の端）
    /// </summary>
    public static void CrashedWindow(Vector2 head, float life, Sprite icon, AppTheme theme)
    {
        Transform root = MacUi.PlaceAlert(null, head, icon != null ? icon : PlaceholderIcon(AppColor(theme)), "quit unexpectedly.",
            new[] { "Ignore", "Reopen" }, BrowserDevFx.OrderHit, MacLook.Dark, true);
        GameObject go = root.gameObject;
        // 大きさは板の絵から（字の長さで決まる）。下の端を頭の上に合わせて、画面に収める
        Transform bodyT = root.Find("Body");
        SpriteRenderer body = bodyT != null ? bodyT.GetComponent<SpriteRenderer>() : null;
        Vector2 half = body != null && body.sprite != null ? (Vector2)body.sprite.bounds.extents : new Vector2(4.5f, 1.2f);
        Rect view = BrowserDevFx.View();
        Vector2 at = head + new Vector2(0f, half.y);
        at.x = Mathf.Clamp(at.x, view.xMin + half.x + 0.2f, Mathf.Max(view.xMin + half.x + 0.2f, view.xMax - half.x - 0.2f));
        at.y = Mathf.Max(Mathf.Min(at.y, view.yMax - HudInset - half.y), view.yMin + half.y + 0.2f);
        root.position = at;
        // 「パッ」と出て、最後の 2割でうすくなって消える（実時間。ゲームオーバーで時間が止まっても動く）
        var rs = go.GetComponentsInChildren<SpriteRenderer>(true);
        var baseColors = new Color[rs.Length];
        for (int i = 0; i < rs.Length; i++) baseColors[i] = rs[i].color;
        var tms = go.GetComponentsInChildren<TextMesh>(true);
        var tmColors = new Color[tms.Length];
        for (int i = 0; i < tms.Length; i++) tmColors[i] = tms[i].color; // 色の直しは済んでいるので、透明さだけ変える
        BrowserDevFx.Animate(go, life, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.12f);
            root.localScale = new Vector3(s, s, 1f);
            float a = k < 0.8f ? 1f : 1f - (k - 0.8f) / 0.2f;
            for (int i = 0; i < rs.Length; i++)
                if (rs[i] != null) rs[i].color = BrowserDevFx.WithAlpha(baseColors[i], baseColors[i].a * a);
            for (int i = 0; i < tms.Length; i++)
                if (tms[i] != null) tms[i].color = BrowserDevFx.WithAlpha(tmColors[i], tmColors[i].a * a);
        }, true);
    }
}

// ───────── エラー弾：予備動作（3-1） ─────────

/// <summary>【仮】予備動作で、ボスのウィンドウの右下にエラーの数のバッジ（赤丸 × と数字）が「パッ」と出る。1発撃つごとに数字が1つ減る</summary>
public class DevErrorBadge : BrowserDevDriver
{
    protected override void Build()
    {
        if (Info.Owner == null) return;
        int n = Info.Count > 0 ? Info.Count : (Info.Data != null ? Info.Data.ProjectileCount * Info.Data.RepeatCount : 1);
        GameObject go = BrowserDevFx.Loose("ErrorBadge", transform.position);
        go.AddComponent<DevErrorBadgeLive>().Setup(Info.Owner, Info.Data, n);
        Adopt(go);
    }
}

/// <summary>【仮】エラーの数のバッジの本体（予備動作が終わっても、撃ち終わるまで残る）</summary>
public class DevErrorBadgeLive : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    SpriteRenderer _sr;
    int _left;
    float _born, _bump, _endAt = -1f;

    public void Setup(AttackRunner owner, AttackData data, int count)
    {
        _owner = owner;
        _data = data;
        _left = Mathf.Max(1, count);
        _born = _bump = Time.time;
        _sr = BrowserDevFx.Show(transform, DevArt.Badge(_left), Vector2.zero, BrowserDevFx.OrderMark, 1f, "Badge");
        if (_owner != null) _owner.Released += OnReleased;
        Place();
    }

    void OnDestroy()
    {
        if (_owner != null) _owner.Released -= OnReleased;
    }

    void OnReleased(AttackData d)
    {
        if (d != _data || _endAt >= 0f) return;
        _left = Mathf.Max(0, _left - 1);
        _sr.sprite = DevArt.Badge(_left);
        _bump = Time.time;
        if (_left == 0) _endAt = Time.time + 0.15f;
    }

    void Place()
    {
        if (_owner == null) return;
        Bounds b = BrowserDevFx.OwnerBounds(_owner);
        transform.position = new Vector2(b.max.x - 0.2f, b.min.y + 0.25f);
    }

    void LateUpdate()
    {
        float now = Time.time;
        bool gone = _owner == null || (_owner.Current != _data && _endAt < 0f) || now - _born > 5f;
        if (gone || (_endAt >= 0f && now >= _endAt))
        {
            Destroy(gameObject);
            return;
        }
        Place();
        float s = BrowserDevFx.Pop(now - _bump, 0.1f);
        if (_endAt >= 0f) s *= Mathf.Clamp01((_endAt - now) / 0.15f);
        _sr.transform.localScale = new Vector3(s, s, 1f);
    }
}

// ───────── エラー弾：弾（3-1） ─────────

/// <summary>【仮】先頭に赤丸 ×、後ろにコードの行と赤い波線（流れる）。床で赤丸が砕ける。時間切れは後ろから消える</summary>
public class DevErrorShot : BrowserDevDriver
{
    Projectile _proj;
    Transform _holder;
    SpriteRenderer _sr;
    readonly Sprite[] _frames = new Sprite[4];

    protected override void Build()
    {
        _proj = GetComponent<Projectile>();
        for (int i = 0; i < 4; i++) _frames[i] = DevArt.ErrorBullet(i, Index, EdgeColor);
        _holder = BrowserDevFx.Child(transform, "ErrorShot", Vector2.zero);
        _sr = BrowserDevFx.Show(_holder, _frames[0], Vector2.zero, BrowserDevFx.OrderCore, 1f, "Bullet");
        LateUpdate();
    }

    void LateUpdate()
    {
        if (_sr == null) return;
        Vector2 v = _proj != null ? _proj.Velocity : Info.Direction;
        // 左へ飛ぶと Target が 180° 回って逆さになるので、絵を上下に返す
        _holder.localScale = new Vector3(1f, v.x < 0f ? -1f : 1f, 1f);
        _sr.sprite = _frames[Mathf.FloorToInt((Time.time - Born) / 0.05f) % 4];
        _sr.enabled = SourceVisible;
    }

    Vector2 Head => (Vector2)transform.position + (Vector2)(transform.right * (6f * BrowserDevFx.Dot));

    protected override void OnLanded(DressInfo info)
    {
        // 赤丸が砕ける（赤い波線は、追撃の床が書く）
        Vector2 v = _proj != null ? _proj.Velocity : Info.Direction;
        Vector2 back = v.sqrMagnitude > 0.0001f ? -v.normalized : Vector2.up;
        Sprite sq = BrowserDevFx.Square(2);
        BrowserDevFx.Burst(Head, back + Vector2.up * 0.5f, 7, i => sq, i => i % 3 == 0 ? Color.white : DevArt.ErrorRed, BrowserDevFx.OrderShard, 70f, 3f, 7f, 0.2f, 0.4f);
    }

    protected override void OnVanish(DressInfo info)
    {
        if (Landed || _sr == null) return;
        float age = Time.time - Born;
        bool timeout = Info.Data != null && age >= Info.Data.Lifetime - 0.06f;
        GameObject go = BrowserDevFx.Loose("ErrShotEnd", transform.position, transform.eulerAngles.z);
        Transform pivot = BrowserDevFx.Child(go.transform, "Pivot", new Vector2(11f * BrowserDevFx.Dot, 0f)); // 先頭（右の端）
        SpriteRenderer sr = BrowserDevFx.Show(pivot, _sr.sprite, new Vector2(-11f * BrowserDevFx.Dot, 0f), BrowserDevFx.OrderCore, 1f, "Bullet");
        sr.transform.localScale = _holder.localScale;
        if (timeout)
        {
            // 後ろから消える
            BrowserDevFx.Animate(go, 0.15f, (e, k) => pivot.localScale = new Vector3(1f - k, 1f, 1f));
        }
        else
        {
            BrowserDevFx.Animate(go, 0.08f, (e, k) =>
            {
                float s = 1f + k * 0.3f;
                pivot.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(1f, 1f, 1f, 1f - k);
            });
        }
        Adopt(go);
    }
}

// ───────── 赤い波線（3-2） ─────────

/// <summary>【仮】コードの行の下の赤い波線（0.1秒ごとに 1ドット流れる）。判定の瞬間に白く光る。出るときはまん中から左右へ書かれ、消えるときは緑に直る</summary>
public class DevRedWave : BrowserDevDriver
{
    AreaStrike _area;
    Transform _holder;
    SpriteRenderer _sr;
    int _w, _h;
    Color _color;
    readonly Sprite[] _normal = new Sprite[4];
    readonly Sprite[] _flash = new Sprite[4];

    protected override void Build()
    {
        _area = GetComponent<AreaStrike>();
        Vector2 size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(2f, 0.4f);
        _w = Mathf.Max(4, Mathf.RoundToInt(size.x / BrowserDevFx.Dot));
        _h = Mathf.Max(5, Mathf.RoundToInt(size.y / BrowserDevFx.Dot));
        _color = Info.IsEnemy ? DevArt.ErrorRed : DevArt.AppColor(Info.Theme);
        for (int i = 0; i < 4; i++)
        {
            _normal[i] = DevArt.Wave(_w, _h, i, _color);
            _flash[i] = DevArt.Wave(_w, _h, i, Color.white);
        }
        _holder = BrowserDevFx.Child(transform, "RedWave", Vector2.zero);
        _sr = BrowserDevFx.Show(_holder, _normal[0], Vector2.zero, BrowserDevFx.OrderFloor, 1f, "Wave");
        LateUpdate();
    }

    void LateUpdate()
    {
        if (_sr == null) return;
        float now = Time.time;
        bool active = _area == null || _area.IsHitActive || Activated;
        _sr.enabled = active && SourceVisible;
        if (!active) return;
        float since = ActiveAt >= 0f ? now - ActiveAt : now - Born;
        // まん中から左右へ書かれる（0.1秒）
        _holder.localScale = new Vector3(Mathf.Clamp01(since / 0.1f), 1f, 1f);
        // 判定の瞬間に白く光る
        float tick = Info.Data != null ? Mathf.Max(0.05f, Info.Data.TickInterval) : 0.5f;
        bool flash = since % tick < 0.06f;
        int phase = Mathf.FloorToInt((now - Born) / 0.1f) % 4;
        _sr.sprite = flash ? _flash[phase] : _normal[phase];
    }

    protected override void OnVanish(DressInfo info)
    {
        if (_sr == null) return;
        // 左から緑に変わって、小さいチェックが出て消える（0.2秒。「直った」）
        float w = _w * BrowserDevFx.Dot;
        GameObject go = BrowserDevFx.Loose("WaveFixed", transform.position, transform.eulerAngles.z);
        SpriteRenderer red = BrowserDevFx.Show(go.transform, _sr.sprite, Vector2.zero, BrowserDevFx.OrderFloor, 1f, "Red");
        Transform pivot = BrowserDevFx.Child(go.transform, "Pivot", new Vector2(-w * 0.5f, 0f));
        int phase = Mathf.FloorToInt((Time.time - Born) / 0.1f) % 4;
        SpriteRenderer green = BrowserDevFx.Show(pivot, DevArt.Wave(_w, _h, phase, DevArt.CodeGreen), new Vector2(w * 0.5f, 0f), BrowserDevFx.OrderFloor + 1, 1f, "Green");
        SpriteRenderer check = BrowserDevFx.Show(go.transform, BrowserArt.Check(8, DevArt.CodeGreen), new Vector2(w * 0.5f + 0.15f, 0.1f), BrowserDevFx.OrderFloor + 2, 1f, "Check");
        check.enabled = false;
        BrowserDevFx.Animate(go, 0.26f, (e, k) =>
        {
            float p = Mathf.Clamp01(e / 0.15f);
            pivot.localScale = new Vector3(p, 1f, 1f);
            red.enabled = p < 1f;
            check.enabled = e >= 0.12f;
            float s = BrowserDevFx.Pop(e - 0.12f, 0.08f);
            check.transform.localScale = new Vector3(s, s, 1f);
            float a = e < 0.18f ? 1f : 1f - (e - 0.18f) / 0.08f;
            green.color = check.color = new Color(1f, 1f, 1f, a);
        });
        Adopt(go);
    }
}

// ───────── ブレークポイント（3-3・3-6） ─────────

/// <summary>
/// 【仮】予告：床に赤丸（ふち 1ドット。敵は濃い赤、プレイヤーは白）と、床から上へ伸びる行番号の帯（左の端に灰色の数字）。
/// 本体：止まった行の黄色の柱（左右の端は敵＝赤、プレイヤー＝アプリの色）。黄色の矢印が上から下へ走り、上の端にデバッグの操作バー。
/// 消える：左右から閉じる。第2形態（3か所）は、行番号を 1〜・10〜・20〜 と続けて「順に実行される」感じにする。
/// 2026-11-19（docs/app-feedback-v2-design.md 3-3）：プレイヤーの能力だけ、見せ方を変えた（敵の柱は今のまま。代用ボスが使う）。
/// 「赤丸を置く → そこの行が止まる → 相手が止まる」：押した瞬間にクリックの輪と大きい赤丸（左に行番号）が「カチッ」→ 判定で黄色い行の帯が
/// 下から上へ6段、0.03秒ずつ積み上がる（左の端はアプリの色の線、各段の左に ▶）→ 当たった相手に黄色い行と ⏸（DevSkin.Hit）→ 赤丸が「カチッ」と外れて消える
/// </summary>
public class DevBreakpoint : BrowserDevDriver
{
    // プレイヤーの能力の見せ方（2026-11-19）
    SpriteRenderer _bigDot, _lineNo;
    readonly List<SpriteRenderer> _rows = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _rowArrows = new List<SpriteRenderer>();
    const int PlayerRows = 6;
    const float RowStep = 0.03f;

    AreaStrike _area;
    Vector2 _size;
    float _baseY;          // 帯の下の端（ローカル）
    Transform _tele, _body;
    SpriteRenderer _band, _dot;
    readonly List<SpriteRenderer> _numbers = new List<SpriteRenderer>();
    readonly List<float> _numberY = new List<float>();
    SpriteRenderer _arrow, _bar;
    float _teleAt = -1f;
    Color _side;

    protected override void Build()
    {
        _area = GetComponent<AreaStrike>();
        _size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(1.5f, 6f);
        _baseY = -_size.y * 0.5f;
        _side = Info.IsEnemy ? BrowserDevFx.EnemyEdge : DevArt.AppColor(Info.Theme);
        _tele = BrowserDevFx.Child(transform, "BpTelegraph", Vector2.zero);
        _body = BrowserDevFx.Child(transform, "BpBody", Vector2.zero);
        _body.gameObject.SetActive(false);
        _tele.gameObject.SetActive(false);
    }

    protected override void OnTelegraph(DressInfo info)
    {
        _teleAt = Time.time;
        // 帯の下の端は、予告の赤丸（床の上）の所。画面の高さの柱でも、プレイヤーの立つ足場から伸ばす
        if (TelegraphSource != null) _baseY = transform.InverseTransformPoint(TelegraphSource.transform.position).y - 0.15f;
        _baseY = Mathf.Clamp(_baseY, -_size.y * 0.5f, _size.y * 0.5f - 0.5f);
        if (!Info.IsEnemy)
        {
            BuildPlayerDot();
            return;
        }
        BuildTelegraph();
    }

    /// <summary>プレイヤー：床に大きい赤丸（左に行番号）と、マウスのクリックの輪（白、0.1秒）</summary>
    void BuildPlayerDot()
    {
        if (_bigDot != null) return;
        _tele.gameObject.SetActive(true);
        float d = BrowserDevFx.Dot;
        Vector2 dotPos = new Vector2(0f, _baseY + 7.5f * d);
        _bigDot = BrowserDevFx.Show(_tele, DevArt.BreakpointDotBig(), dotPos, BrowserDevFx.OrderPillarDeco + 2, 1f, "BpDot");
        _bigDot.transform.localScale = Vector3.zero;
        int line = 12 + Mathf.Abs(Index) * 10; // 行番号（飾り）
        _lineNo = BrowserDevFx.Show(_tele, BrowserDevFx.Text(line.ToString(), DevArt.LineNo, true), dotPos + new Vector2(-14f * d, 0f), BrowserDevFx.OrderPillarDeco + 2, 1f, "LineNo");
        // クリックの輪（白。6 → 20ドットに広がって消える）
        GameObject ring = BrowserDevFx.Loose("BpClick", transform.TransformPoint(dotPos));
        SpriteRenderer rr = BrowserDevFx.Show(ring.transform, BrowserDevFx.RingSprite(20, 1), Vector2.zero, BrowserDevFx.OrderPillarDeco + 3, 1f, "Ring");
        BrowserDevFx.Animate(ring, 0.12f, (e, k) =>
        {
            float sc = Mathf.Lerp(0.3f, 1f, k);
            rr.transform.localScale = new Vector3(sc, sc, 1f);
            rr.color = new Color(1f, 1f, 1f, 1f - k);
        });
        Adopt(ring);
    }

    void BuildTelegraph()
    {
        if (_band != null) return;
        float x0 = -_size.x * 0.5f;
        Color bandColor = Info.IsEnemy ? BrowserDevFx.WithAlpha(BrowserDevFx.EnemyEdge, 0.15f) : BrowserDevFx.WithAlpha(DevArt.AppColor(Info.Theme), 0.22f);
        _band = BrowserDevFx.Rect(_tele, new Vector2(0f, _baseY), new Vector2(_size.x, 0f), bandColor, BrowserDevFx.OrderTelegraph, "Band");
        _dot = BrowserDevFx.Show(_tele, DevArt.BreakpointDot(Info.IsEnemy ? RuntimeSprites.Hex("8B0F0F") : Color.white),
            new Vector2(0f, _baseY + 5f * BrowserDevFx.Dot), BrowserDevFx.OrderTelegraph + 1, 1f, "Dot");
        // 行番号（1マスおき）。第2形態の2つ目・3つ目は 10〜・20〜
        int start = Index <= 0 ? 1 : Index * 10;
        float top = _size.y * 0.5f;
        int n = 0;
        for (float y = _baseY + 0.75f; y < top - 0.2f && n < 14; y += 1f, n++)
        {
            SpriteRenderer num = BrowserDevFx.Show(_tele, BrowserDevFx.Text((start + n).ToString(), DevArt.LineNo), new Vector2(x0 + 4f * BrowserDevFx.Dot, y), BrowserDevFx.OrderPillarDeco, 1f, "LineNo");
            num.enabled = false;
            _numbers.Add(num);
            _numberY.Add(y);
        }
    }

    void LateUpdate()
    {
        if (_tele == null) return;
        float now = Time.time;
        bool hit = _area != null ? _area.IsHitActive : Activated;
        if (!Info.IsEnemy)
        {
            PlayerUpdate(now);
            return;
        }
        if (!hit && Told)
        {
            // 予告：今の赤丸の点滅（最後の 0.4秒）とそろえる
            bool on = TelegraphSource == null || TelegraphSource.enabled;
            _tele.gameObject.SetActive(on);
            float rise = Mathf.Clamp01((now - _teleAt) / 0.3f);
            float h = (_size.y * 0.5f - _baseY) * rise;
            BrowserDevFx.SetRect(_band, new Vector2(0f, _baseY + h * 0.5f), new Vector2(_size.x, h));
            for (int i = 0; i < _numbers.Count; i++) _numbers[i].enabled = _numberY[i] <= _baseY + h;
        }
        if (hit && _arrow != null)
        {
            // 黄色の矢印が上から下へ一瞬で走る（0.1秒）
            float t = Mathf.Clamp01((now - ActiveAt) / 0.1f);
            float y = Mathf.Lerp(_size.y * 0.5f - 0.3f, -_size.y * 0.5f + 0.3f, t);
            _arrow.transform.localPosition = new Vector3(0f, y, 0f);
            _arrow.color = new Color(1f, 1f, 1f, t < 1f ? 1f : Mathf.Clamp01(1f - (now - ActiveAt - 0.1f) / 0.1f));
        }
    }

    /// <summary>プレイヤー：赤丸が「カチッ」と出る。判定のあと、黄色い行が下から 0.03秒ずつ積み上がる（出た瞬間は白く光る）</summary>
    void PlayerUpdate(float now)
    {
        if (_bigDot != null)
        {
            float s = BrowserDevFx.Pop(now - _teleAt, 0.08f);
            _bigDot.transform.localScale = new Vector3(s, s, 1f);
        }
        if (!Activated) return;
        float e = now - ActiveAt;
        for (int i = 0; i < _rows.Count; i++)
        {
            float t = e - i * RowStep;
            bool on = t >= 0f;
            _rows[i].enabled = on;
            if (i < _rowArrows.Count) _rowArrows[i].enabled = on;
            if (on) _rows[i].color = t < 0.04f ? new Color(1f, 1f, 1f, 0.85f) : BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f);
        }
    }

    /// <summary>プレイヤー：柱の中に、黄色い行の帯（高さ 1マス）を下から6段。左の端はアプリの色の線、白いふち</summary>
    void BuildPlayerBody()
    {
        if (_bigDot == null) BuildPlayerDot();
        _body.gameObject.SetActive(true);
        float w = _size.x, h = _size.y, d = BrowserDevFx.Dot;
        float bottom = -h * 0.5f;
        float rowH = h / PlayerRows;
        for (int i = 0; i < PlayerRows; i++)
        {
            float cy = bottom + rowH * (i + 0.5f);
            SpriteRenderer row = BrowserDevFx.Rect(_body, new Vector2(d, cy), new Vector2(w - 4f * d, rowH - d), BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f), BrowserDevFx.OrderPillar, "Row");
            row.enabled = false;
            _rows.Add(row);
            SpriteRenderer arrow = BrowserDevFx.Show(_body, DevArt.RightArrow(7), new Vector2(-w * 0.5f + 6f * d, cy), BrowserDevFx.OrderPillarDeco + 1, 1f, "Arrow");
            arrow.enabled = false;
            _rowArrows.Add(arrow);
        }
        // 左の端はアプリの色の線（2ドット）。プレイヤーの攻撃は白いふち（1ドット）
        BrowserDevFx.Rect(_body, new Vector2(-w * 0.5f + d, 0f), new Vector2(2f * d, h), _side, BrowserDevFx.OrderPillar + 1, "Left");
        BrowserDevFx.Rect(_body, new Vector2(-w * 0.5f - d * 0.5f, 0f), new Vector2(d, h), Color.white, BrowserDevFx.OrderPillar, "EdgeL");
        BrowserDevFx.Rect(_body, new Vector2(w * 0.5f + d * 0.5f, 0f), new Vector2(d, h), Color.white, BrowserDevFx.OrderPillar, "EdgeR");
        BrowserDevFx.Rect(_body, new Vector2(0f, h * 0.5f + d * 0.5f), new Vector2(w + 2f * d, d), Color.white, BrowserDevFx.OrderPillar, "EdgeT");
    }

    protected override void OnActive(DressInfo info)
    {
        if (!Info.IsEnemy)
        {
            BuildPlayerBody();
            return;
        }
        BuildTelegraph();
        // 予告の帯と赤丸は消し、行番号は柱の上に残す
        _band.enabled = false;
        _dot.enabled = false;
        _tele.gameObject.SetActive(true);
        for (int i = 0; i < _numbers.Count; i++) _numbers[i].enabled = true;

        _body.gameObject.SetActive(true);
        float w = _size.x, h = _size.y, d = BrowserDevFx.Dot;
        // 止まった行の黄色（6割）と、左右の端（2ドット）
        BrowserDevFx.Rect(_body, Vector2.zero, new Vector2(w - 4f * d, h), BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f), BrowserDevFx.OrderPillar, "Fill");
        BrowserDevFx.Rect(_body, new Vector2(-w * 0.5f + d, 0f), new Vector2(2f * d, h), _side, BrowserDevFx.OrderPillar, "Left");
        BrowserDevFx.Rect(_body, new Vector2(w * 0.5f - d, 0f), new Vector2(2f * d, h), _side, BrowserDevFx.OrderPillar, "Right");
        if (!Info.IsEnemy)
        {
            // プレイヤーの攻撃は白いふち（1ドット）
            BrowserDevFx.Rect(_body, new Vector2(-w * 0.5f - d * 0.5f, 0f), new Vector2(d, h), Color.white, BrowserDevFx.OrderPillar, "EdgeL");
            BrowserDevFx.Rect(_body, new Vector2(w * 0.5f + d * 0.5f, 0f), new Vector2(d, h), Color.white, BrowserDevFx.OrderPillar, "EdgeR");
            BrowserDevFx.Rect(_body, new Vector2(0f, h * 0.5f + d * 0.5f), new Vector2(w + 2f * d, d), Color.white, BrowserDevFx.OrderPillar, "EdgeT");
        }
        // 止まった行（濃い黄色の横の帯）
        BrowserDevFx.Rect(_body, new Vector2(0f, _baseY + 0.45f), new Vector2(w - 4f * d, 4f * d), BrowserDevFx.WithAlpha(DevArt.Stopped, 0.9f), BrowserDevFx.OrderPillar + 1, "Line");
        _arrow = BrowserDevFx.Show(_body, DevArt.DownArrow(), new Vector2(0f, h * 0.5f - 0.3f), BrowserDevFx.OrderPillarDeco + 1, 1f, "Arrow");
        // 柱の上の端（画面の中）に、デバッグの操作バー
        float topWorld = transform.TransformPoint(new Vector3(0f, h * 0.5f, 0f)).y;
        Rect view = BrowserDevFx.View();
        float barY = Mathf.Min(topWorld, view.yMax) - 0.35f;
        Vector3 barLocal = transform.InverseTransformPoint(new Vector3(transform.position.x, barY, 0f));
        _bar = BrowserDevFx.Show(_body, DevArt.DebugBar(), new Vector2(0f, barLocal.y), BrowserDevFx.OrderPillarDeco + 2, 1f, "DebugBar");
    }

    /// <summary>プレイヤー：黄色い行が左右から閉じ、赤丸が「カチッ」と外れて消える</summary>
    void PlayerVanish()
    {
        float d = BrowserDevFx.Dot;
        GameObject go = BrowserDevFx.Loose("BpOff", transform.position, transform.eulerAngles.z);
        Transform col = BrowserDevFx.Child(go.transform, "Col", Vector2.zero);
        if (Activated)
        {
            foreach (var r in _rows)
            {
                if (r == null) continue;
                SpriteRenderer c = BrowserDevFx.Rect(col, r.transform.localPosition, new Vector2(r.transform.localScale.x * d, r.transform.localScale.y * d), BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f), BrowserDevFx.OrderPillar, "Row");
                c.enabled = r.enabled;
            }
        }
        SpriteRenderer dot = _bigDot != null ? BrowserDevFx.Show(go.transform, _bigDot.sprite, _bigDot.transform.localPosition, BrowserDevFx.OrderPillarDeco + 2, 1f, "BpDot") : null;
        SpriteRenderer num = _lineNo != null ? BrowserDevFx.Show(go.transform, _lineNo.sprite, _lineNo.transform.localPosition, BrowserDevFx.OrderPillarDeco + 2, 1f, "LineNo") : null;
        BrowserDevFx.Animate(go, 0.16f, (e, k) =>
        {
            float p = Mathf.Clamp01(e / 0.08f);
            col.localScale = new Vector3(1f - p, 1f, 1f);
            if (dot != null)
            {
                // 「カチッ」：一瞬 85% にへこんでから、縮んで消える
                float s = e < 0.04f ? 0.85f : Mathf.Lerp(0.85f, 0f, Mathf.Clamp01((e - 0.04f) / 0.1f));
                dot.transform.localScale = new Vector3(s, s, 1f);
            }
            if (num != null) num.color = new Color(1f, 1f, 1f, 1f - k);
        });
        Adopt(go);
    }

    protected override void OnVanish(DressInfo info)
    {
        if (!Info.IsEnemy)
        {
            if (Told || Activated) PlayerVanish();
            return;
        }
        if (!Activated || _body == null) return;
        // 柱が左右から閉じる（0.08秒）。行番号は下から消える
        GameObject go = BrowserDevFx.Loose("BpClose", transform.position, transform.eulerAngles.z);
        float w = _size.x, h = _size.y, d = BrowserDevFx.Dot;
        Transform col = BrowserDevFx.Child(go.transform, "Col", Vector2.zero);
        var parts = new List<SpriteRenderer>
        {
            BrowserDevFx.Rect(col, Vector2.zero, new Vector2(w - 4f * d, h), BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f), BrowserDevFx.OrderPillar, "Fill"),
            BrowserDevFx.Rect(col, new Vector2(-w * 0.5f + d, 0f), new Vector2(2f * d, h), _side, BrowserDevFx.OrderPillar, "Left"),
            BrowserDevFx.Rect(col, new Vector2(w * 0.5f - d, 0f), new Vector2(2f * d, h), _side, BrowserDevFx.OrderPillar, "Right"),
        };
        var nums = new List<KeyValuePair<SpriteRenderer, float>>();
        for (int i = 0; i < _numbers.Count; i++)
        {
            if (_numbers[i] == null) continue;
            SpriteRenderer n = BrowserDevFx.Show(go.transform, _numbers[i].sprite, _numbers[i].transform.localPosition, BrowserDevFx.OrderPillarDeco, 1f, "LineNo");
            nums.Add(new KeyValuePair<SpriteRenderer, float>(n, (float)i / Mathf.Max(1, _numbers.Count)));
        }
        BrowserDevFx.Animate(go, 0.2f, (e, k) =>
        {
            float p = Mathf.Clamp01(e / 0.08f);
            col.localScale = new Vector3(1f - p, 1f, 1f);
            foreach (var kv in nums) kv.Key.enabled = kv.Value >= k;
        });
        Adopt(go);
    }
}

// ───────── エラーウィンドウ落下 → エラーログ（3-4） ─────────

/// <summary>
/// 【仮】2026-11-19（docs/app-feedback-v2-design.md 3-2）：ダイアログをやめて「エラーログの行の束」にした（VS Code の下のパネルに流れる赤いログ）。
/// 予告：今の影のまま＋影の真上の画面の上の端に、パネルの見出しの帯（OUTPUT・赤い ×・数字）。赤い × が点滅。
/// 本体：ログの束がまっすぐ落ちる。上に同じ形のうすい行（3割・1割）を2段（ログが上から流れてきた跡）。
/// 着地：行がばらけて、左右へ1行ずつ流れて消える（0.2秒）＋コードの記号のかけら。影・判定・秒数は変えない。
/// ステージの出来事「Build failed」の雨（同じ枠）も、この絵になる
/// </summary>
public class DevErrorWindow : BrowserDevDriver
{
    FallingObject _fall;
    SpriteRenderer _shadowCopy, _window, _drop;
    SpriteRenderer _header;
    Sprite _headerOn, _headerOff;
    Transform _holder, _trail;
    Vector2 _size;

    protected override void Build()
    {
        _fall = GetComponent<FallingObject>();
        _size = Info.HitSize.sqrMagnitude > 0f ? Info.HitSize : new Vector2(3f, 2f);
        _holder = BrowserDevFx.Child(transform, "ErrLog", Vector2.zero);
        _window = BrowserDevFx.Show(_holder, DevArt.ErrorLog(EdgeColor), Vector2.zero, BrowserDevFx.OrderWindow - 1, 1f, "Log");
        // 影（下へ 2ドット。四角いパネルなので四角い影）
        _drop = BrowserDevFx.Rect(_holder, new Vector2(0f, -2f * BrowserDevFx.Dot), new Vector2(DevArt.LogW, DevArt.LogH) * BrowserDevFx.Dot,
            new Color(0f, 0f, 0f, 0.35f), BrowserDevFx.OrderWindow - 2, "Shadow");
        _window.enabled = _drop.enabled = false;
        // 流れてきた跡（同じ形のうすい行を2段。3割・1割）
        _trail = BrowserDevFx.Child(transform, "Trace", Vector2.zero);
        float d = BrowserDevFx.Dot;
        for (int i = 0; i < 2; i++)
        {
            float y = DevArt.LogH * 0.5f * d + (4f + i * 8f) * d + 0.5f * d;
            SpriteRenderer row = BrowserDevFx.Show(_trail, DevArt.LogRow(i == 0 ? 2 : 0), new Vector2(0f, y), BrowserDevFx.OrderWindow - 2, 1f, "Row");
            row.color = new Color(1f, 1f, 1f, i == 0 ? 0.3f : 0.1f);
        }
        _trail.gameObject.SetActive(false);
        // パネルの見出しの帯（予告。画面の上の端）
        int n = Info.Count > 0 ? Info.Count : 3;
        _headerOn = DevArt.OutputHeader(true, n);
        _headerOff = DevArt.OutputHeader(false, n);
        _header = BrowserDevFx.Show(transform, _headerOn, Vector2.zero, BrowserDevFx.OrderWindow, 1f, "Header");
        _header.enabled = false;
    }

    protected override void OnTelegraph(DressInfo info)
    {
        // 今の影はそのまま出す（呼んだ側がかくすので、写しを作る）
        _shadowCopy = Replicate(info.CurrentVisual, "ShadowCopy");
    }

    void LateUpdate()
    {
        if (_window == null) return;
        float now = Time.time;
        bool falling = _fall != null ? _fall.IsFalling : Activated;
        bool landed = _fall != null && _fall.HasLanded;
        if (_shadowCopy != null && TelegraphSource != null) _shadowCopy.enabled = TelegraphSource.enabled;

        // 予告：影の真上の画面の上の端に、見出しの帯。赤い × が点滅
        bool waiting = Told && !falling && !landed;
        _header.enabled = waiting;
        if (waiting)
        {
            Rect view = BrowserDevFx.View();
            float top = Mathf.Min(view.yMax, BattleArena.GetBounds().yMax);
            _header.transform.position = new Vector2(transform.position.x, top - 0.55f);
            _header.sprite = Mathf.FloorToInt(now / 0.1f) % 2 == 0 ? _headerOn : _headerOff;
        }

        bool vis = falling && SourceVisible;
        _window.enabled = vis;
        _drop.enabled = vis;
        _trail.gameObject.SetActive(vis && !landed);
        _holder.localRotation = Quaternion.identity;
    }

    protected override void OnLanded(DressInfo info)
    {
        Vector2 at = transform.position;
        // 行がばらけて、左右へ1行ずつ流れて消える（0.2秒）
        GameObject go = BrowserDevFx.Loose("LogScatter", at);
        float d = BrowserDevFx.Dot;
        var rows = new SpriteRenderer[3];
        float[] ys = { (DevArt.LogH * 0.5f - 8.5f) * d, (DevArt.LogH * 0.5f - 19f) * d, (DevArt.LogH * 0.5f - 27f) * d };
        for (int i = 0; i < 3; i++) rows[i] = BrowserDevFx.Show(go.transform, DevArt.LogRow(i), new Vector2(0f, ys[i]), BrowserDevFx.OrderWindow - 1, 1f, "Row");
        BrowserDevFx.Animate(go, 0.2f, (e, k) =>
        {
            for (int i = 0; i < 3; i++)
            {
                if (rows[i] == null) continue;
                float dir = i % 2 == 0 ? -1f : 1f;
                rows[i].transform.localPosition = new Vector3(dir * 2.2f * k, ys[i], 0f);
                rows[i].color = new Color(1f, 1f, 1f, 1f - k);
            }
        });
        Adopt(go);
        // コードの記号のかけら（今のまま）。当たらなくても、少しゆらす
        int start = UnityEngine.Random.Range(0, 6);
        BrowserDevFx.Burst(at + Vector2.up * 0.2f, Vector2.up, 8, i => DevArt.Symbol(start + i), i => Color.white, BrowserDevFx.OrderShard, 60f, 5f, 9f, 0.35f, 0.55f);
        StageCamera.Shake(0.06f, 0.12f);
        if (_window != null) _window.enabled = false;
        if (_drop != null) _drop.enabled = false;
    }
}

// ───────── 瞬間移動（3-5） ─────────

/// <summary>
/// 【仮】ボスの瞬間移動：切り取り（カット）と貼り付け（ペースト）。WarpTelegraph（現れる所の予告）から呼ばれる。Target＝予告の物（現れる所）。
/// 予告：ボスの上から下へ選択の青の行が走り、頭の上に ⌘ X。現れる所に白い文字のカーソル（太さ 2ドット、0.25秒ごとに点滅）。
/// 本体（現れる瞬間）：上の行から 4段で貼り付けられ、頭の上に ⌘ V。消えた所には点線の枠（切り取った跡）が 0.3秒
/// </summary>
public class DevWarp : BrowserDevDriver
{
    Bounds _from;
    Vector2 _size;
    SpriteRenderer _cursor;
    Transform _select;
    readonly List<SpriteRenderer> _rows = new List<SpriteRenderer>();
    float _teleAt = -1f;

    static readonly float[] RowWidths = { 1f, 0.82f, 0.95f, 0.68f };

    protected override void Build()
    {
        _size = Info.HitSize.x > 0.05f && Info.HitSize.y > 0.05f ? Info.HitSize : new Vector2(4f, 3f);
        // 元の所＝現れる所 − 移動量（WarpTelegraph は Direction に移動量を入れる）。ボスの被弾範囲は切れていることがあるので見ない
        _from = new Bounds((Vector2)transform.position - Info.Direction, _size);
        _cursor = BrowserDevFx.Rect(transform, Vector2.zero, new Vector2(2f * BrowserDevFx.Dot, _size.y * 0.6f), Color.white, BrowserDevFx.OrderMark, "Cursor");
        _cursor.enabled = false;
    }

    protected override void OnTelegraph(DressInfo info)
    {
        _teleAt = Time.time;
        // 選択の青（4行。上から下へ 0.3秒で走る）
        _select = BrowserDevFx.Child(transform, "Select", Vector2.zero);
        _select.position = _from.center;
        float h = _from.size.y / 4f;
        for (int i = 0; i < 4; i++)
        {
            float w = _from.size.x * RowWidths[i];
            Vector2 c = new Vector2(-_from.size.x * 0.5f + w * 0.5f, _from.size.y * 0.5f - h * (i + 0.5f));
            SpriteRenderer r = BrowserDevFx.Rect(_select, c, new Vector2(w, h - BrowserDevFx.Dot), BrowserDevFx.WithAlpha(DevArt.Select, 0.75f), BrowserDevFx.OrderMark - 2, "Row");
            r.enabled = false;
            _rows.Add(r);
        }
        Keys('X', new Vector2(_from.center.x, _from.max.y + 0.45f));
    }

    /// <summary>頭の上に ⌘ と key のキー（0.3秒）</summary>
    void Keys(char key, Vector2 at)
    {
        GameObject go = BrowserDevFx.Loose("Keys", at);
        float d = BrowserDevFx.Dot;
        SpriteRenderer a = BrowserDevFx.Show(go.transform, DevArt.Key('*'), new Vector2(-5.5f * d, 0f), BrowserDevFx.OrderMark, 1f, "Cmd");
        SpriteRenderer b = BrowserDevFx.Show(go.transform, DevArt.Key(key), new Vector2(5.5f * d, 0f), BrowserDevFx.OrderMark, 1f, "Key");
        BrowserDevFx.Animate(go, 0.3f, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.08f);
            go.transform.localScale = new Vector3(s, s, 1f);
            // 2つ目のキーは少しおくれて押される
            b.transform.localPosition = new Vector3(5.5f * d, e > 0.06f && e < 0.12f ? -d : 0f, 0f);
            float al = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
            a.color = b.color = new Color(1f, 1f, 1f, al);
        });
        Adopt(go);
    }

    void LateUpdate()
    {
        if (_cursor == null) return;
        float now = Time.time;
        // 文字のカーソル：0.25秒ごとに点滅（現れるまで）
        _cursor.enabled = Told && !Activated && Mathf.FloorToInt((now - _teleAt) / 0.25f) % 2 == 0;
        if (_select != null)
        {
            float e = now - _teleAt;
            for (int i = 0; i < _rows.Count; i++) _rows[i].enabled = !Activated && e >= i * 0.075f;
        }
    }

    protected override void OnActive(DressInfo info)
    {
        if (_select != null) _select.gameObject.SetActive(false);
        _cursor.enabled = false;
        // 現れる所＝この物（予告）の所。ボスが移る瞬間と同じフレームとは限らないので、ボスの位置は見ない
        var to = new Bounds(transform.position, _size);

        // 切り取った跡（点線の枠、灰色、0.3秒）
        GameObject ghost = BrowserDevFx.Loose("CutTrace", _from.center);
        var lines = new List<SpriteRenderer>();
        BrowserDevFx.DottedBox(ghost.transform, Vector2.zero, _from.size, DevArt.LineNo, BrowserDevFx.OrderMark - 2, lines);
        BrowserDevFx.Animate(ghost, 0.3f, (e, k) =>
        {
            foreach (var l in lines) l.color = BrowserDevFx.WithAlpha(DevArt.LineNo, 1f - k);
        });
        Adopt(ghost);

        // 上の行から 4段で貼り付けられる（0.05秒ずつ）
        GameObject paste = BrowserDevFx.Loose("Paste", to.center);
        float h = to.size.y / 4f;
        var bands = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            bands[i] = BrowserDevFx.Rect(paste.transform, new Vector2(0f, to.size.y * 0.5f - h * (i + 0.5f)), new Vector2(to.size.x, h - BrowserDevFx.Dot),
                BrowserDevFx.WithAlpha(DevArt.Select, 0.8f), BrowserDevFx.OrderMark - 2, "Band");
            bands[i].enabled = false;
        }
        BrowserDevFx.Animate(paste, 0.32f, (e, k) =>
        {
            for (int i = 0; i < 4; i++)
            {
                float born = i * 0.05f;
                bands[i].enabled = e >= born;
                // 出た瞬間は白く光って、選択の青になってから消える
                float t = e - born;
                Color c = t < 0.04f ? new Color(1f, 1f, 1f, 0.8f) : BrowserDevFx.WithAlpha(DevArt.Select, 0.8f * Mathf.Clamp01(1f - (e - 0.2f) / 0.12f));
                bands[i].color = c;
            }
        });
        Adopt(paste);
        Keys('V', new Vector2(to.center.x, to.max.y + 0.45f));
    }
}
