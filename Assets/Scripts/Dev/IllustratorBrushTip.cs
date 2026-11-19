using UnityEngine;

/// <summary>既存の絵と同じ18px/マス。筆の向きも画像内で8方向に描き、スプライトを斜め拡縮しない。</summary>
public sealed class IllustratorBrushTip : MonoBehaviour
{
    readonly Sprite[] _sprites = new Sprite[8];
    readonly Texture2D[] _textures = new Texture2D[8];
    public SpriteRenderer Renderer { get; private set; }

    public static IllustratorBrushTip Create(Transform parent, Color ink, int order)
    {
        var go = new GameObject("ドット絵の筆");
        go.transform.SetParent(parent, false);
        var brush = go.AddComponent<IllustratorBrushTip>();
        const int width = 24, height = 12, canvas = 49, center = 24;
        var paint = new Color32[width * height];
        for (int x = 2; x <= 14; x++)
            for (int y = 5; y <= 7; y++)
                paint[y * width + x] = y == 7 ? new Color32(187, 128, 65, 255) : new Color32(112, 66, 38, 255);
        for (int x = 14; x <= 17; x++)
            for (int y = 3; y <= 9; y++)
                paint[y * width + x] = x == 15 ? new Color32(222, 218, 202, 255) : new Color32(129, 137, 151, 255);
        for (int x = 18; x <= 22; x++)
        {
            int half = (22 - x) / 2;
            for (int y = 6 - half; y <= 6 + half; y++) paint[y * width + x] = ink;
        }
        var source = (Color32[])paint.Clone();
        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (paint[index].a == 0 && (paint[index - 1].a > 0 || paint[index + 1].a > 0 || paint[index - width].a > 0 || paint[index + width].a > 0))
                    source[index] = new Color32(31, 22, 40, 255);
            }
        for (int direction = 0; direction < 8; direction++)
        {
            float angle = direction * Mathf.PI / 4f;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            var pixels = new Color32[canvas * canvas];
            for (int y = 0; y < canvas; y++)
                for (int x = 0; x < canvas; x++)
                {
                    float dx = x - center, dy = y - center;
                    int sx = Mathf.RoundToInt(22 + cosine * dx + sine * dy);
                    int sy = Mathf.RoundToInt(6 - sine * dx + cosine * dy);
                    if (sx >= 0 && sx < width && sy >= 0 && sy < height) pixels[y * canvas + x] = source[sy * width + sx];
                }
            var texture = new Texture2D(canvas, canvas, TextureFormat.RGBA32, false)
                { name = "Pixel brush " + direction, filterMode = FilterMode.Point };
            texture.SetPixels32(pixels); texture.Apply();
            brush._textures[direction] = texture;
            brush._sprites[direction] = Sprite.Create(texture, new Rect(0, 0, canvas, canvas), Vector2.one * 0.5f, GameArt.PixelsPerUnit);
        }
        brush.Renderer = go.AddComponent<SpriteRenderer>();
        brush.Renderer.sprite = brush._sprites[0]; brush.Renderer.sortingOrder = order;
        return brush;
    }

    public void SetHeading(Vector2 tangent)
    {
        if (tangent.sqrMagnitude < 0.000001f) return;
        int frame = (Mathf.RoundToInt(Mathf.Atan2(tangent.y, tangent.x) / (Mathf.PI / 4f)) + 8) % 8;
        Renderer.sprite = _sprites[frame];
    }

    void OnDestroy()
    {
        foreach (Sprite sprite in _sprites) if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in _textures) if (texture != null) Destroy(texture);
    }
}
