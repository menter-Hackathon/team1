using UnityEngine;

/// <summary>
/// 【仮】攻撃の絵がまだ無いときに使う、実行時に作る白い図形（1×1マス）。
/// 色は SpriteRenderer.color で付ける。デザインが来たら AttackData の絵を差し替える。
/// </summary>
public static class PlaceholderSprites
{
    const int Size = 32;
    static Sprite _square;
    static Sprite _circle;
    static Sprite _frame;

    /// <summary>白い四角</summary>
    public static Sprite Square => _square != null ? _square : (_square = Create("Square", (x, y) => true));

    /// <summary>白い円（赤丸の予告・円の判定）</summary>
    public static Sprite Circle => _circle != null ? _circle : (_circle = Create("Circle", (x, y) =>
    {
        float dx = x + 0.5f - Size * 0.5f, dy = y + 0.5f - Size * 0.5f;
        return dx * dx + dy * dy <= Size * Size * 0.25f;
    }));

    /// <summary>白い枠だけの四角（出現の予告）</summary>
    public static Sprite Frame => _frame != null ? _frame : (_frame = Create("Frame", (x, y) =>
        x < 2 || y < 2 || x >= Size - 2 || y >= Size - 2));

    static Sprite Create(string name, System.Func<int, int, bool> inside)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = $"Placeholder_{name}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                pixels[y * Size + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = $"Placeholder_{name}";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
