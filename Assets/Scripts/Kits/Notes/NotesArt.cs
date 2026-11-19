using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メモ（Apple の Notes）のキットの絵（2026-11-19。docs/app-kits-1-design.md 14章）。コードの中のドット絵（KitArt）。
/// 色：黄 #FFD60A・紙の白 #FFFDF0・罫線の灰色 #D9D4C0・文字の濃い灰色
/// </summary>
public static class NotesArt
{
    public static readonly Color32 Yellow = KitArt.Hex("FFD60A");
    public static readonly Color32 YellowDark = KitArt.Hex("D9AE00");
    public static readonly Color32 Paper = KitArt.Hex("FFFDF0");
    public static readonly Color32 Rule = KitArt.Hex("D9D4C0");
    public static readonly Color32 Ink = KitArt.Hex("4A4A4A");
    public static readonly Color32 Edge = KitArt.Hex("BDB6A0");
    public static readonly Color32 LockGray = KitArt.Hex("8E8E93");
    public static readonly Color32 LockDark = KitArt.Hex("48484A");
    public static readonly Color32 PinRed = KitArt.Hex("FF453A");
    public static readonly Color32 Green = KitArt.Hex("30D158");

    /// <summary>ボスの技「メモの紙」（2×2 マス）：黄色い見出しの帯と、灰色の罫線 3本</summary>
    public static Sprite Paper2(int index)
    {
        int v = ((index % 2) + 2) % 2;
        return KitArt.Paint($"notes.paper.{v}", 36, 36, c =>
        {
            c.Fill(0, 0, 36, 36, Edge);
            c.Fill(1, 1, 34, 34, Paper);
            c.Fill(1, 1, 34, 7, Yellow);
            for (int j = 0; j < 3; j++) c.HLine(3, 15 + j * 7, 30, Rule);
            c.Fill(4, 11, v == 0 ? 18 : 24, 2, Ink); // 1行目の字
            c.Fill(4, 18, v == 0 ? 22 : 14, 1, Ink);
        });
    }

    /// <summary>ボスの技「クイックメモ」の窓（5×4 マス）：小さいウィンドウ（信号機）と、黄色い帯・罫線</summary>
    public static Sprite QuickMemo()
    {
        return KitArt.Paint("notes.quick", 90, 72, c =>
        {
            c.RoundFill(0, 0, 90, 72, Edge);
            c.RoundFill(1, 1, 88, 70, Paper);
            c.Fill(1, 1, 88, 10, KitArt.Hex("ECEAE0")); // タイトルの帯
            c.Disc(7, 6, 2, KitArt.Hex("FF5F57"));
            c.Disc(14, 6, 2, KitArt.Hex("FEBC2E"));
            c.Disc(21, 6, 2, KitArt.Hex("28C840"));
            c.Fill(1, 11, 88, 4, Yellow);
            for (int j = 0; j < 7; j++) c.HLine(5, 24 + j * 7, 80, Rule);
            c.Fill(6, 19, 34, 2, Ink);
            c.Fill(6, 27, 50, 1, Ink);
            c.Fill(6, 34, 42, 1, Ink);
        });
    }

    /// <summary>南京錠（12×14 ドット）。open：開いている</summary>
    public static Sprite Lock(bool open)
    {
        return KitArt.Paint(open ? "notes.lock.open" : "notes.lock.closed", 12, 14, c =>
        {
            // つる
            int lift = open ? 2 : 0;
            c.Fill(3, 0 + 0, 6, 2, LockDark);
            c.Fill(2, 1, 2, 6 - lift, LockDark);
            if (!open) c.Fill(8, 1, 2, 6, LockDark);
            else c.Fill(8, 1, 2, 2, LockDark);
            // 本体
            c.RoundFill(0, 6, 12, 8, LockDark);
            c.Fill(1, 7, 10, 6, LockGray);
            c.Fill(5, 8, 2, 4, LockDark); // 鍵穴
        });
    }

    /// <summary>画びょう（能力「ピン留め」。10×12 ドット。頭が左、針が右）</summary>
    public static Sprite Pin()
    {
        return KitArt.Paint("notes.pin", 14, 10, c =>
        {
            c.Disc(4, 4, 3, PinRed);
            c.Fill(2, 2, 2, 2, KitArt.Hex("FFB0AA")); // 光
            c.Fill(7, 3, 2, 3, KitArt.Hex("C7302A"));
            c.Fill(9, 4, 5, 1, LockGray);              // 針
        });
    }

    /// <summary>ボスに刺さった画びょう（上から刺さった向き。10×14 ドット）</summary>
    public static Sprite PinDown()
    {
        return KitArt.Paint("notes.pin.down", 10, 14, c =>
        {
            c.Disc(4, 4, 3, PinRed);
            c.Fill(2, 2, 2, 2, KitArt.Hex("FFB0AA"));
            c.Fill(3, 7, 3, 2, KitArt.Hex("C7302A"));
            c.Fill(4, 9, 1, 5, LockGray);
        });
    }

    // ───────── チェックリスト（HUD の左上のメモ。8×8 の絵） ─────────

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'k', KitArt.Hex("1C1C1E") }, { 'w', KitArt.Hex("FFFFFF") }, { 'y', KitArt.Hex("FFD60A") },
        { 'r', KitArt.Hex("FF453A") }, { 'g', KitArt.Hex("30D158") }, { 'b', KitArt.Hex("0A84FF") }, { 's', KitArt.Hex("AEAEB2") },
    };

    /// <summary>近接の絵（剣）</summary>
    public static Sprite IconMelee() => KitArt.Paint("notes.icon.melee", 8, 8, c => c.Ascii(0, 0, new[]
    {
        "......ss",
        ".....sws",
        "....sws.",
        "...sws..",
        ".ksws...",
        "..kk....",
        ".k.k....",
        "k.......",
    }, Pal));

    /// <summary>能力の絵（きらめき）</summary>
    public static Sprite IconSkill() => KitArt.Paint("notes.icon.skill", 8, 8, c => c.Ascii(0, 0, new[]
    {
        "...b....",
        "...b....",
        "..bwb...",
        "bbwwwbb.",
        "..bwb...",
        "...b..b.",
        "...b.bwb",
        "......b.",
    }, Pal));

    /// <summary>ハートの絵（ダメージを受けない）</summary>
    public static Sprite IconHeart() => KitArt.Paint("notes.icon.heart", 8, 8, c => c.Ascii(0, 0, new[]
    {
        ".rr.rr..",
        "rwrrrrr.",
        "rrrrrrr.",
        "rrrrrrr.",
        ".rrrrr..",
        "..rrr...",
        "...r....",
        "........",
    }, Pal));

    /// <summary>できた行の ✓（7×7）</summary>
    public static Sprite Check() => KitArt.Paint("notes.check", 7, 7, c => c.Ascii(0, 0, new[]
    {
        "......g",
        ".....gg",
        "g...gg.",
        "gg.gg..",
        ".ggg...",
        "..g....",
        ".......",
    }, Pal));

    /// <summary>まだの行の ○（7×7）</summary>
    public static Sprite Circle() => KitArt.Paint("notes.circle", 7, 7, c => c.Ascii(0, 0, new[]
    {
        "..sss..",
        ".s...s.",
        "s.....s",
        "s.....s",
        "s.....s",
        ".s...s.",
        "..sss..",
    }, Pal));

    /// <summary>倍率の印のアイコン（8×8 の黄色いメモ）</summary>
    public static Sprite IconMemo() => KitArt.Paint("notes.icon.memo", 8, 8, c =>
    {
        c.Fill(0, 0, 8, 8, Edge);
        c.Fill(1, 1, 6, 6, Paper);
        c.Fill(1, 1, 6, 2, Yellow);
        c.HLine(2, 4, 4, Rule);
        c.HLine(2, 6, 3, Rule);
    });
}
