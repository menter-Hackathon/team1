using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】キットの絵をコードでかく道具（2026-11-19）。担当0 の物。
// ・RuntimeSprites.FromAscii では大きい絵（ブロック・窓）が書きにくいので、四角・ふち・線・点で描けるようにした。
// ・座標は左上が (0, 0)、右・下へ増える（ドット）。1マス＝18ドット。同じ鍵の絵は1回だけ作る（使い回す）。
// ・例：KitArt.Paint("notion.block", 72, 18, c => { c.Fill(0, 0, 72, 18, White); c.Frame(0, 0, 72, 18, Gray); });

/// <summary>絵をかく板（KitArt.Paint の中で使う）</summary>
public sealed class KitCanvas
{
    public readonly int W, H;
    internal readonly Color32[] Pixels;

    internal KitCanvas(int w, int h)
    {
        W = w;
        H = h;
        Pixels = new Color32[w * h];
    }

    /// <summary>1ドット（左上が原点。外は無視）</summary>
    public void Px(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        Pixels[(H - 1 - y) * W + x] = c;
    }

    /// <summary>四角を塗る</summary>
    public void Fill(int x, int y, int w, int h, Color32 c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++) Px(x + i, y + j, c);
    }

    /// <summary>四角のふち（太さ t）</summary>
    public void Frame(int x, int y, int w, int h, Color32 c, int t = 1)
    {
        Fill(x, y, w, t, c);
        Fill(x, y + h - t, w, t, c);
        Fill(x, y, t, h, c);
        Fill(x + w - t, y, t, h, c);
    }

    /// <summary>角を1ドット落とした四角（丸い角の代わり）</summary>
    public void RoundFill(int x, int y, int w, int h, Color32 c)
    {
        Fill(x + 1, y, w - 2, h, c);
        Fill(x, y + 1, 1, h - 2, c);
        Fill(x + w - 1, y + 1, 1, h - 2, c);
    }

    public void HLine(int x, int y, int w, Color32 c) => Fill(x, y, w, 1, c);
    public void VLine(int x, int y, int h, Color32 c) => Fill(x, y, 1, h, c);

    /// <summary>塗った円（真ん中と半径）</summary>
    public void Disc(int cx, int cy, int r, Color32 c)
    {
        for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
                if (i * i + j * j <= r * r + r) Px(cx + i, cy + j, c);
    }

    /// <summary>文字の並びで描く（'.' と ' ' は描かない）</summary>
    public void Ascii(int x, int y, string[] rows, IReadOnlyDictionary<char, Color32> palette)
    {
        for (int j = 0; j < rows.Length; j++)
        {
            string row = rows[j];
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '.' || ch == ' ' || !palette.TryGetValue(ch, out Color32 c)) continue;
                Px(x + i, y + j, c);
            }
        }
    }
}

/// <summary>キットの絵をコードでかく</summary>
public static class KitArt
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>w×h ドットの絵を描いて、スプライトにする（同じ鍵は使い回す）。pivot は (0.5, 0.5) が真ん中</summary>
    public static Sprite Paint(string key, int w, int h, Action<KitCanvas> draw, Vector2? pivot = null)
    {
        if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        var canvas = new KitCanvas(Mathf.Max(1, w), Mathf.Max(1, h));
        try { draw?.Invoke(canvas); }
        catch (Exception e) { Debug.LogException(e); }
        var tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false)
        {
            name = $"Kit_{key}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(canvas.Pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, canvas.W, canvas.H), pivot ?? new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        sprite.name = key ?? "Kit";
        sprite.hideFlags = HideFlags.DontSave;
        if (!string.IsNullOrEmpty(key)) Cache[key] = sprite;
        return sprite;
    }

    /// <summary>16進の色</summary>
    public static Color32 Hex(string hex) => RuntimeSprites.Hex(hex);
}
