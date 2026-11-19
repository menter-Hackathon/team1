using UnityEngine;

/// <summary>既存の絵と同じ18px/マス。アンチエイリアスなしでパスと塗りを描く。</summary>
public sealed class IllustratorPixelCanvas
{
    public const float Ppu = GameArt.PixelsPerUnit;
    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    public int Width { get; }
    public int Height { get; }
    public Vector2 Origin { get; }
    public Color32[] Pixels { get; }
    readonly Texture2D _texture;
    readonly Sprite _sprite;

    public IllustratorPixelCanvas(Transform parent, Vector3[] points, float margin, int order)
    {
        Vector2 min = points[0], max = points[0];
        foreach (Vector3 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        Origin = new Vector2(Mathf.Floor((min.x - margin) * Ppu), Mathf.Floor((min.y - margin) * Ppu)) / Ppu;
        Width = Mathf.Max(1, Mathf.CeilToInt((max.x + margin - Origin.x) * Ppu) + 1);
        Height = Mathf.Max(1, Mathf.CeilToInt((max.y + margin - Origin.y) * Ppu) + 1);
        Pixels = new Color32[Width * Height];
        _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            { name = "Illustrator pixel ink", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _sprite = Sprite.Create(_texture, new Rect(0, 0, Width, Height), Vector2.zero, Ppu);
        var go = new GameObject("ドットのインク");
        go.transform.SetParent(parent, false);
        go.transform.position = Origin - Vector2.one / (Ppu * 2f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _sprite; renderer.sortingOrder = order;
    }

    public static Vector3 Snap(Vector3 point) => new Vector3(Mathf.Round(point.x * Ppu) / Ppu, Mathf.Round(point.y * Ppu) / Ppu, point.z);
    public Vector3 World(int x, int y) => new Vector3(Origin.x + x / Ppu, Origin.y + y / Ppu, 0f);
    public static bool Covered(int x, int y, int coverage) => Bayer[((y & 3) << 2) + (x & 3)] < coverage;
    public void Clear() => System.Array.Clear(Pixels, 0, Pixels.Length);

    public void Dot(int x, int y, Color32 color, int coverage = 16)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || !Covered(x, y, coverage)) return;
        color.a = 255;
        Pixels[y * Width + x] = color;
    }

    void Stamp(int x, int y, int width, Color32 color, int coverage)
    {
        int start = -width / 2, end = start + width;
        for (int dy = start; dy < end; dy++)
            for (int dx = start; dx < end; dx++) Dot(x + dx, y + dy, color, coverage);
    }

    public void Line(Vector3 a, Vector3 b, int width, Color32 color, int coverage = 16)
    {
        int x = Mathf.RoundToInt((a.x - Origin.x) * Ppu), y = Mathf.RoundToInt((a.y - Origin.y) * Ppu);
        int tx = Mathf.RoundToInt((b.x - Origin.x) * Ppu), ty = Mathf.RoundToInt((b.y - Origin.y) * Ppu);
        int dx = Mathf.Abs(tx - x), dy = -Mathf.Abs(ty - y);
        int sx = x < tx ? 1 : -1, sy = y < ty ? 1 : -1, error = dx + dy;
        while (true)
        {
            Stamp(x, y, width, color, coverage);
            if (x == tx && y == ty) break;
            int twice = error * 2;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
    }

    public void Path(Vector3[] points, int count, int width, Color32 color, int coverage = 16, bool closed = false)
    {
        for (int i = 1; i < count; i++) Line(points[i - 1], points[i], width, color, coverage);
        if (closed && count > 1) Line(points[count - 1], points[0], width, color, coverage);
    }

    public void Box(Vector3 center, int size, Color32 color)
    {
        int x = Mathf.RoundToInt((center.x - Origin.x) * Ppu), y = Mathf.RoundToInt((center.y - Origin.y) * Ppu);
        Stamp(x, y, size, color, 16);
    }

    public void Apply() { _texture.SetPixels32(Pixels); _texture.Apply(false, false); }
    public void Dispose()
    {
        Object.Destroy(_sprite);
        Object.Destroy(_texture);
    }
}
