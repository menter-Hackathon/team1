using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Zoom のキット（ボス・ギミック）の絵（2026-11-19。docs/app-kits-1-design.md 18章）。コードの中のドット絵。1マス＝18ドット。
/// 能力のギャラリービューの絵（ZoomArt：名札・マイク・退出のボタン。LockOnStrike.cs）は読むだけで使い、ここには足りない物だけをかく。
/// 色：青 #2D8CFF・暗い地 #1A1A1A・緑の枠 #23D959・退出の赤 #E02828
/// </summary>
public static class ZoomKitArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("2D8CFF");
    public static readonly Color Dark = RuntimeSprites.Hex("1A1A1A");
    public static readonly Color Green = RuntimeSprites.Hex("23D959");
    public static readonly Color LeaveRed = RuntimeSprites.Hex("E02828");

    static readonly Color32 Y = new Color32(0xFF, 0xCC, 0x4D, 0xFF);
    static readonly Color32 O = new Color32(0xE0, 0x9A, 0x2B, 0xFF);
    static readonly Color32 K = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    static readonly Color32 W = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    /// <summary>拍手の手（黄色。16×16。本物の絵文字の絵は使わない）</summary>
    public static Sprite Clap => RuntimeSprites.FromAscii("ZoomKit_Clap", new[]
    {
        "..W...W.........",
        "...W.W....YY....",
        "W.......YYYYO...",
        ".W....YYYYYYO...",
        "......YYYYYYO...",
        ".....YYYYYYO....",
        "....YYOYYYYO....",
        "...YYOYYYYYOO...",
        "..YYOYYYYYYYO...",
        "..YOYYYYYYYYO...",
        "..YOYYYYYYYO....",
        "..YYYYYYYYYO....",
        "...YYYYYYYO.....",
        "....YYYYYO......",
        ".....OOOO.......",
        "................",
    }, new Dictionary<char, Color32> { { 'Y', Y }, { 'O', O }, { 'W', W } });

    /// <summary>
    /// 参加者のタイル（カメラがオフの暗い四角に、灰色の人の形・左下の名札・緑の話している枠。36×27＝2×1.5マス）。i で人の色を変える
    /// </summary>
    public static Sprite ParticipantTile(int i)
    {
        int n = ((i % 4) + 4) % 4;
        const int w = 36, h = 27;
        string key = $"ZoomKit_Tile{n}";
        Color32[] people = { new Color32(0x8E, 0x8E, 0x93, 255), new Color32(0x63, 0x66, 0xF1, 255), new Color32(0x2D, 0x8C, 0xFF, 255), new Color32(0xAF, 0x52, 0xDE, 255) };
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++)
            {
                char c = 'K';
                if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) c = n == 0 ? 'G' : 'E'; // ふち（1枚目は話している緑）
                else
                {
                    // 頭（丸）と肩（半円）
                    float hx = x + 0.5f - w * 0.5f, hy = y + 0.5f - 10f;
                    float sx = x + 0.5f - w * 0.5f, sy = y + 0.5f - 25f;
                    if (hx * hx + hy * hy <= 20f) c = 'P';
                    else if (sx * sx * 0.45f + sy * sy <= 70f && y < h - 2) c = 'P';
                    if (y >= h - 7 && y < h - 4 && x >= 4 && x < 15) c = 'W'; // 名札
                }
                line[x] = c;
            }
            rows[y] = new string(line);
        }
        var pal = new Dictionary<char, Color32>
        {
            { 'K', K }, { 'G', (Color32)Green }, { 'E', new Color32(0x3A, 0x3A, 0x3C, 255) }, { 'P', people[n] }, { 'W', new Color32(0xE5, 0xE5, 0xEA, 255) },
        };
        return RuntimeSprites.FromAscii(key, rows, pal);
    }

    /// <summary>子の絵を1枚出す（大きさはマス）</summary>
    public static SpriteRenderer Put(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Art")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, sprite, color, size, order, name);
        sr.transform.localPosition = localPos;
        return sr;
    }
}
