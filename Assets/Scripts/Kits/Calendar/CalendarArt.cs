using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのキットの絵（2026-11-19。docs/app-kits-1-design.md 9章）。コードの中のドット絵。1マス＝18ドット。
/// 色：赤 #FF3B30（今日・今の線）・白・予定の色（青 #0A84FF・緑 #34C759・オレンジ #FF9F0A・紫 #BF5AF2）。
/// 本物の予定の名前は出さない（予定の板は、色と白い棒だけ）
/// </summary>
public static class CalendarArt
{
    public static readonly Color Red = RuntimeSprites.Hex("FF3B30");
    public static readonly Color Blue = RuntimeSprites.Hex("0A84FF");
    public static readonly Color Green = RuntimeSprites.Hex("34C759");
    public static readonly Color Orange = RuntimeSprites.Hex("FF9F0A");
    public static readonly Color Purple = RuntimeSprites.Hex("BF5AF2");
    public static readonly Color Grid = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color Ink = RuntimeSprites.Hex("1C1C1E");

    /// <summary>予定の色（順に）</summary>
    public static readonly Color[] EventColors = { Blue, Green, Orange, Purple };

    static readonly Color32 W = new Color32(255, 255, 255, 255);
    static readonly Color32 R = new Color32(0xFF, 0x3B, 0x30, 255);

    /// <summary>予定の色（番号はくり返す）</summary>
    public static Color EventColor(int i) => EventColors[((i % EventColors.Length) + EventColors.Length) % EventColors.Length];

    /// <summary>
    /// 予定のブロック（角の丸い色の板に、左に濃い帯・白い棒 2本。45×27＝2.5×1.5マス）。i は色の番号
    /// </summary>
    public static Sprite Block(int i)
    {
        int n = ((i % EventColors.Length) + EventColors.Length) % EventColors.Length;
        const int w = 45, h = 27, corner = 4;
        string key = $"Calendar_Block{n}";
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, corner, w - corner), cy = Mathf.Clamp(y + 0.5f, corner, h - corner);
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                bool inside = dx * dx + dy * dy <= corner * corner;
                char c = '.';
                if (inside)
                {
                    c = 'C';
                    if (x < 4) c = 'D';                                           // 左の濃い帯
                    else if (y >= 6 && y < 9 && x >= 8 && x < 34) c = 'W';        // 白い棒（題名）
                    else if (y >= 13 && y < 15 && x >= 8 && x < 24) c = 'L';      // うすい棒（時刻）
                }
                line[x] = c;
            }
            rows[y] = new string(line);
        }
        Color main = EventColors[n];
        var pal = new Dictionary<char, Color32>
        {
            { 'C', (Color32)main },
            { 'D', (Color32)Color.Lerp(main, Color.black, 0.35f) },
            { 'W', W },
            { 'L', (Color32)Color.Lerp(main, Color.white, 0.55f) },
        };
        return RuntimeSprites.FromAscii(key, rows, pal);
    }

    /// <summary>くり返す予定の印（回る矢印。白。12×12）</summary>
    public static Sprite Repeat => RuntimeSprites.FromAscii("Calendar_Repeat", new[]
    {
        "....WWWW.W..",
        "..WW....WW..",
        ".W.....WWW..",
        ".W..........",
        "W...........",
        "W..........W",
        "W..........W",
        "...........W",
        "..........W.",
        "..WWW.....W.",
        "..WW....WW..",
        "..W.WWWW....",
    }, new Dictionary<char, Color32> { { 'W', W } });

    /// <summary>今の線の左の端の赤い丸（7×7）</summary>
    public static Sprite NowDot => FxArt.Disc(7);

    /// <summary>白いチェック（出席）</summary>
    public static Sprite Check => FxArt.Check;

    /// <summary>能力のアイコン（カレンダーのアイコン風：赤い上の帯と白い地に日付の形。16×16）</summary>
    public static Sprite SkillIcon => RuntimeSprites.FromAscii("Calendar_SkillIcon", new[]
    {
        "..RRRRRRRRRRRR..",
        ".RRRRRRRRRRRRRR.",
        "RRRRRRRRRRRRRRRR",
        "RRRRRRRRRRRRRRRR",
        "WWWWWWWWWWWWWWWW",
        "WWWWWWWWWWWWWWWW",
        "WWWKKKWWWWKKKWWW",
        "WWKWWWKWWKWWWKWW",
        "WWWWWWKWWWWWKWWW",
        "WWWWWKWWWWWKWWWW",
        "WWWWKWWWWWKWWWWW",
        "WWWKWWWWWKWWWWWW",
        "WWKKKKKWWKKKKKWW",
        "WWWWWWWWWWWWWWWW",
        ".WWWWWWWWWWWWWW.",
        "..WWWWWWWWWWWW..",
    }, new Dictionary<char, Color32> { { 'R', R }, { 'W', W }, { 'K', new Color32(0x1C, 0x1C, 0x1E, 255) } });

    /// <summary>曜日の頭文字（英語。日曜から）</summary>
    public static readonly string[] Weekdays = { "S", "M", "T", "W", "T", "F", "S" };

    /// <summary>子の絵を1枚出す（大きさはマス）</summary>
    public static SpriteRenderer Put(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Art")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, sprite, color, size, order, name);
        sr.transform.localPosition = localPos;
        return sr;
    }
}
