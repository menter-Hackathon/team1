using UnityEngine;

/// <summary>
/// 【仮】Notion のキットの絵（2026-11-19。docs/app-kits-1-design.md 8章）。コードの中のドット絵（KitArt）。
/// 色：白 #FFFFFF・黒 #191919・うすい灰色 #F1F1EF・ふちの灰色・差し色の赤 #EB5757・青 #2F80ED
/// </summary>
public static class NotionArt
{
    public static readonly Color32 White = KitArt.Hex("FFFFFF");
    public static readonly Color32 Black = KitArt.Hex("191919");
    public static readonly Color32 Light = KitArt.Hex("F1F1EF");
    public static readonly Color32 Edge = KitArt.Hex("C9C8C4");
    public static readonly Color32 Handle = KitArt.Hex("A5A4A0");
    public static readonly Color32 Red = KitArt.Hex("EB5757");
    public static readonly Color32 Blue = KitArt.Hex("2F80ED");

    // ブロックの左の「⋮⋮」（2列×3つの点）
    static void Grip(KitCanvas c, int x, int y)
    {
        for (int j = 0; j < 3; j++)
        {
            c.Fill(x, y + j * 3, 2, 2, Handle);
            c.Fill(x + 3, y + j * 3, 2, 2, Handle);
        }
    }

    /// <summary>ボスの技「ブロックを積む」の白いブロック（4×1 マス）。index で中の棒の長さを変える</summary>
    public static Sprite Block(int index)
    {
        int v = ((index % 3) + 3) % 3;
        return KitArt.Paint($"notion.block.{v}", 72, 18, c =>
        {
            c.RoundFill(0, 0, 72, 18, Edge);
            c.Fill(1, 1, 70, 16, White);
            Grip(c, 4, 5);
            int len = v == 0 ? 40 : v == 1 ? 30 : 46;
            c.Fill(14, 6, len, 3, Black);       // 見出しの太い棒
            c.Fill(14, 11, len - 12, 2, Edge);  // 本文の細い棒
        });
    }

    /// <summary>ボスの技「トグル」の中身の箱（2×2.8 マス）。左上に ▼</summary>
    public static Sprite ToggleBox(int index)
    {
        return KitArt.Paint("notion.togglebox", 36, 50, c =>
        {
            c.RoundFill(0, 0, 36, 50, Edge);
            c.Fill(1, 1, 34, 48, Light);
            DownTriangle(c, 4, 4, Black);
            c.Fill(13, 5, 18, 3, Black);
            for (int j = 0; j < 5; j++) c.Fill(6, 14 + j * 7, j % 2 == 0 ? 24 : 18, 2, Edge);
        });
    }

    /// <summary>能力「ブロック」の白いブロック（2×2 マス）</summary>
    public static Sprite SkillBlock()
    {
        return KitArt.Paint("notion.skillblock", 36, 36, c =>
        {
            c.RoundFill(0, 0, 36, 36, Edge);
            c.Fill(1, 1, 34, 34, White);
            Grip(c, 4, 4);
            c.Fill(12, 5, 19, 3, Black);
            c.Fill(6, 15, 24, 2, Edge);
            c.Fill(6, 21, 18, 2, Edge);
            c.Fill(6, 27, 22, 2, Edge);
            c.Frame(0, 0, 36, 36, Handle);
        });
    }

    /// <summary>ボスの技「ビューの切り替え」のカードの束（区画に伸ばす。白いカードに色の付いた小さい札）</summary>
    public static Sprite Cards()
    {
        return KitArt.Paint("notion.cards", 40, 48, c =>
        {
            for (int k = 0; k < 4; k++)
            {
                int y = 2 + k * 11;
                c.RoundFill(3, y, 34, 10, Edge);
                c.Fill(4, y + 1, 32, 8, White);
                c.Fill(7, y + 3, 14, 2, Black);
                c.Fill(24, y + 3, 9, 4, k % 2 == 0 ? Red : Blue); // 札
            }
        });
    }

    /// <summary>足場の左の端の印：開いている（▼）／閉じている（▶）。7×7 ドット</summary>
    public static Sprite ToggleMark(bool open)
    {
        return KitArt.Paint(open ? "notion.mark.open" : "notion.mark.closed", 9, 9, c =>
        {
            c.RoundFill(0, 0, 9, 9, White);
            if (open) DownTriangle(c, 1, 2, Black);
            else RightTriangle(c, 2, 1, Black);
        });
    }

    // ▼（幅 7・高さ 4）
    static void DownTriangle(KitCanvas c, int x, int y, Color32 col)
    {
        c.Fill(x, y, 7, 1, col);
        c.Fill(x + 1, y + 1, 5, 1, col);
        c.Fill(x + 2, y + 2, 3, 1, col);
        c.Fill(x + 3, y + 3, 1, 1, col);
    }

    // ▶（幅 4・高さ 7）
    static void RightTriangle(KitCanvas c, int x, int y, Color32 col)
    {
        c.Fill(x, y, 1, 7, col);
        c.Fill(x + 1, y + 1, 1, 5, col);
        c.Fill(x + 2, y + 2, 1, 3, col);
        c.Fill(x + 3, y + 3, 1, 1, col);
    }
}
