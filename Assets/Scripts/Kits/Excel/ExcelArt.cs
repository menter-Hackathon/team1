using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 【仮】Excel のキットの絵（2026-11-19。docs/app-kits-1-design.md 10章）。コードの中のドット絵。1マス＝18ドット。
/// 色：緑 #217346・白・線 #D4D4D4・グラフ（青 #4472C4・オレンジ #ED7D31・黄 #FFC000）
/// </summary>
public static class ExcelArt
{
    public static readonly Color Green = RuntimeSprites.Hex("217346");
    public static readonly Color GreenLight = RuntimeSprites.Hex("33C481");
    public static readonly Color White = Color.white;
    public static readonly Color Line = RuntimeSprites.Hex("D4D4D4");
    public static readonly Color Ink = RuntimeSprites.Hex("1C1C1E");
    public static readonly Color Blue = RuntimeSprites.Hex("4472C4");
    public static readonly Color Orange = RuntimeSprites.Hex("ED7D31");
    public static readonly Color Yellow = RuntimeSprites.Hex("FFC000");
    public static readonly Color ErrorRed = RuntimeSprites.Hex("E02020");

    /// <summary>セル（w×h ドット。白い地、灰色の線）</summary>
    public static Sprite Cell(int w, int h)
    {
        w = Mathf.Max(4, w); h = Mathf.Max(4, h);
        var rows = new string[h];
        var sb = new StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Clear();
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                sb.Append(edge ? 'L' : 'W');
            }
            rows[y] = sb.ToString();
        }
        return RuntimeSprites.FromAscii($"Excel_Cell_{w}_{h}", rows, new Dictionary<char, Color32>
        {
            { 'L', (Color32)Line },
            { 'W', (Color32)White },
        });
    }

    /// <summary>右肩上がりの小さい棒グラフ（n 本。0〜5。20×16。右上に緑の矢印）</summary>
    public static Sprite MiniChart(int n)
    {
        n = Mathf.Clamp(n, 0, 5);
        const int w = 22, h = 16;
        var g = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[y, x] = '.';
        // 軸
        for (int x = 1; x < w - 1; x++) g[h - 2, x] = 'K';
        for (int y = 2; y < h - 1; y++) g[y, 1] = 'K';
        // 棒（右へ行くほど高い）
        for (int i = 0; i < n; i++)
        {
            int bx = 3 + i * 4, bh = 2 + i * 2;
            char c = (char)('0' + (i % 3));
            for (int y = h - 2 - bh; y < h - 2; y++)
                for (int x = bx; x < bx + 3; x++) g[y, x] = c;
        }
        // 右上の矢印
        if (n > 0)
        {
            for (int i = 0; i < 5; i++) g[6 - i, 14 + i] = 'G';
            g[1, 17] = 'G'; g[1, 18] = 'G'; g[2, 18] = 'G'; g[1, 19] = 'G'; g[3, 19] = 'G'; g[2, 19] = 'G';
        }
        return Make($"Excel_MiniChart_{n}", g, new Dictionary<char, Color32>
        {
            { 'K', (Color32)Ink }, { 'G', (Color32)GreenLight },
            { '0', (Color32)Blue }, { '1', (Color32)Orange }, { '2', (Color32)Yellow },
        });
    }

    static Sprite Make(string key, char[,] g, Dictionary<char, Color32> pal)
    {
        int h = g.GetLength(0), w = g.GetLength(1);
        var rows = new string[h];
        var sb = new StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Clear();
            for (int x = 0; x < w; x++) sb.Append(g[y, x]);
            rows[y] = sb.ToString();
        }
        return RuntimeSprites.FromAscii(key, rows, pal);
    }

    /// <summary>四角い枠（太さ thick マス）を線 4本で作る。返すのは [上・下・左・右]</summary>
    public static SpriteRenderer[] Frame(Transform parent, Color color, float thick, int order)
    {
        var lines = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++) lines[i] = FxKit.Box(parent, Vector2.zero, new Vector2(1f, thick), color, order, "FrameLine");
        return lines;
    }

    /// <summary>枠の線を rect（ゲームの世界）に合わせる</summary>
    public static void PlaceFrame(SpriteRenderer[] lines, Rect r, float thick)
    {
        if (lines == null || lines.Length < 4) return;
        SetBox(lines[0], new Vector2(r.center.x, r.yMax - thick * 0.5f), new Vector2(r.width, thick));
        SetBox(lines[1], new Vector2(r.center.x, r.yMin + thick * 0.5f), new Vector2(r.width, thick));
        SetBox(lines[2], new Vector2(r.xMin + thick * 0.5f, r.center.y), new Vector2(thick, r.height));
        SetBox(lines[3], new Vector2(r.xMax - thick * 0.5f, r.center.y), new Vector2(thick, r.height));
    }

    /// <summary>四角（PlaceholderSprites.Square を伸ばした物）の位置と大きさを変える</summary>
    public static void SetBox(SpriteRenderer sr, Vector2 worldCenter, Vector2 size)
    {
        if (sr == null) return;
        sr.transform.position = new Vector3(worldCenter.x, worldCenter.y, sr.transform.position.z);
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
    }

    /// <summary>枠の色をまとめて変える</summary>
    public static void TintFrame(SpriteRenderer[] lines, Color c)
    {
        if (lines == null) return;
        foreach (var l in lines) if (l != null) l.color = c;
    }
}
