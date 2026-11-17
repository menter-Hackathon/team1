using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アプリのアイコンをドット絵にする（2026-11-17：アイコンからドット絵を作る）。
/// ・まわりの透明な余白を切り、指定の大きさ（16・24 ドットなど）へ平均して縮め、8色に減らす。透明は半分を境に、ある・なしの2つにする
/// ・アイコンの色から、代表の色（多くて鮮やかな色）と差し色（最大4色）を取り出す
/// ・アイコンが無いアプリには、名前から決まる色の仮のアイコンを作る（同じ名前なら、いつも同じ色）
/// </summary>
public static class PixelIcon
{
    public const int Colors = 8;

    /// <summary>ドット絵のアイコン（Point・1マス＝18ドット・中心が原点）</summary>
    public static Sprite Pixelize(Texture2D source, int size, string spriteName)
    {
        Color32[] src = ReadPixels(source);
        if (src == null) return null;
        int sw = source.width, sh = source.height;

        // 透明でない部分だけを切り出す（正方形にそろえる）
        int x0 = sw, y0 = sh, x1 = -1, y1 = -1;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (src[y * sw + x].a > 40) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        if (x1 < 0) return null;
        int side = Mathf.Max(x1 - x0 + 1, y1 - y0 + 1);
        float cx = (x0 + x1 + 1) * 0.5f, cy = (y0 + y1 + 1) * 0.5f;
        float left = cx - side * 0.5f, bottom = cy - side * 0.5f;

        // 平均して縮める（透明の重みを付ける）
        var px = new Color32[size * size];
        float cell = side / (float)size;
        for (int ty = 0; ty < size; ty++)
        {
            for (int tx = 0; tx < size; tx++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                int n = 0;
                int sx0 = Mathf.FloorToInt(left + tx * cell), sx1 = Mathf.CeilToInt(left + (tx + 1) * cell);
                int sy0 = Mathf.FloorToInt(bottom + ty * cell), sy1 = Mathf.CeilToInt(bottom + (ty + 1) * cell);
                for (int y = sy0; y < sy1; y++)
                {
                    for (int x = sx0; x < sx1; x++)
                    {
                        n++;
                        if (x < 0 || y < 0 || x >= sw || y >= sh) continue;
                        Color32 c = src[y * sw + x];
                        float w = c.a / 255f;
                        r += c.r * w; g += c.g * w; b += c.b * w; a += w;
                    }
                }
                if (n == 0 || a / n < 0.5f) { px[ty * size + tx] = new Color32(0, 0, 0, 0); continue; }
                px[ty * size + tx] = new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), 255);
            }
        }
        Quantize(px, Colors);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), GameArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    /// <summary>
    /// 代表の色と差し色を取り出す。代表の色は「数 × 鮮やかさ」が一番大きい色。
    /// 白・黒・灰色だけのアイコン（Notion など）は、少し青みのある灰色にする
    /// </summary>
    public static void ExtractColors(Sprite icon, out Color main, out Color[] accents)
    {
        main = new Color32(0x70, 0x77, 0x84, 0xff);
        accents = new[] { main };
        Color32[] px = icon != null ? ReadPixels(icon.texture) : null;
        if (px == null) return;
        var counts = new Dictionary<uint, int>();
        foreach (var c in px)
        {
            if (c.a == 0 || Saturation(c) < 40) continue;
            uint k = Key(c);
            counts.TryGetValue(k, out int n);
            counts[k] = n + 1;
        }
        if (counts.Count == 0) return;
        var list = new List<(Color32 c, int n)>();
        foreach (var kv in counts) list.Add((FromKey(kv.Key), kv.Value));
        list.Sort((p, q) => (q.n * Saturation(q.c)).CompareTo(p.n * Saturation(p.c)));
        main = list[0].c;
        // 差し色：色合いが離れた色を、多い順に最大4色
        var picked = new List<Color>();
        list.Sort((p, q) => q.n.CompareTo(p.n));
        foreach (var (c, n) in list)
        {
            Color.RGBToHSV(c, out float h, out _, out _);
            bool near = false;
            foreach (var p in picked)
            {
                Color.RGBToHSV(p, out float ph, out _, out _);
                float d = Mathf.Abs(h - ph);
                if (Mathf.Min(d, 1f - d) < 0.07f) { near = true; break; }
            }
            if (!near) picked.Add(c);
            if (picked.Count >= 4) break;
        }
        accents = picked.Count >= 4 ? picked.ToArray() : new[] { main }; // 4色そろわなければ、枠は代表の色1つで塗る
    }

    static readonly Dictionary<int, Sprite> GrayCache = new Dictionary<int, Sprite>();

    /// <summary>グレーにした複製（図鑑の、まだ正常化していないアプリ。2026-11-17）。少し明るめの灰色にする</summary>
    public static Sprite Grayscale(Sprite icon)
    {
        if (icon == null || !icon.texture.isReadable) return icon;
        if (GrayCache.TryGetValue(icon.GetInstanceID(), out Sprite cached) && cached != null) return cached;
        Rect r = icon.textureRect;
        int w = (int)r.width, h = (int)r.height;
        Color32[] src = icon.texture.GetPixels32();
        int texW = icon.texture.width;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 c = src[((int)r.y + y) * texW + (int)r.x + x];
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt((0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * 0.55f + 40f), 0, 255);
                px[y * w + x] = new Color32(v, v, (byte)Mathf.Min(255, v + 6), c.a);
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = icon.name + "_Gray", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), icon.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = tex.name;
        GrayCache[icon.GetInstanceID()] = sprite;
        return sprite;
    }

    /// <summary>名前から決まる色（同じ名前なら、いつも同じ色）</summary>
    public static Color ColorFromName(string name)
    {
        uint h = Hash(name);
        return Color.HSVToRGB((h % 360) / 360f, 0.62f, 0.88f);
    }

    /// <summary>アイコンが無いアプリの仮のアイコン（角の丸い四角に、ウィンドウの形）</summary>
    public static Sprite Placeholder(Color color, int size, string spriteName)
    {
        var px = new Color32[size * size];
        Color32 body = color;
        Color32 shade = Color.Lerp(color, Color.black, 0.35f);
        Color32 white = Color.white;
        int r = Mathf.Max(2, size / 6);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Max(0, Mathf.Max(r - x, x - (size - 1 - r)));
                int dy = Mathf.Max(0, Mathf.Max(r - y, y - (size - 1 - r)));
                if (dx * dx + dy * dy > r * r) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                px[y * size + x] = y < size / 8 + 1 ? shade : body;
            }
        }
        // 中に白いウィンドウ（上にタイトルバー）
        int m = size / 4;
        for (int y = m; y < size - m; y++)
        {
            for (int x = m; x < size - m; x++)
            {
                bool title = y >= size - m - Mathf.Max(2, size / 8);
                bool edge = x == m || x == size - m - 1 || y == m || y == size - m - 1;
                px[y * size + x] = title || edge ? shade : white;
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = spriteName, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), GameArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    /// <summary>名前のハッシュ（FNV-1a。実行のたびに変わらない）</summary>
    public static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char c in (s ?? "").Trim().ToLowerInvariant())
        {
            h ^= c;
            h *= 16777619;
        }
        return h;
    }

    // ───────── 中身 ─────────

    static Color32[] ReadPixels(Texture2D tex)
    {
        if (tex == null) return null;
        if (!tex.isReadable) return null;
        return tex.GetPixels32();
    }

    /// <summary>色を k 色に減らす（k 平均法。最初の色は、多い色から選ぶので、毎回同じ結果になる）</summary>
    static void Quantize(Color32[] px, int k)
    {
        var colors = new List<Vector3>();
        var counts = new Dictionary<uint, int>();
        foreach (var c in px)
        {
            if (c.a == 0) continue;
            uint key = (uint)((c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3));
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }
        if (counts.Count <= k) return;
        var keys = new List<uint>(counts.Keys);
        keys.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
        var centers = new List<Vector3>();
        foreach (uint key in keys)
        {
            var v = new Vector3(((key >> 10) & 31) * 8 + 4, ((key >> 5) & 31) * 8 + 4, (key & 31) * 8 + 4);
            bool near = false;
            foreach (var c in centers) if ((c - v).sqrMagnitude < 40f * 40f) { near = true; break; }
            if (!near) centers.Add(v);
            if (centers.Count >= k) break;
        }
        for (int iter = 0; iter < 8; iter++)
        {
            var sum = new Vector3[centers.Count];
            var num = new int[centers.Count];
            foreach (var c in px)
            {
                if (c.a == 0) continue;
                int best = Nearest(centers, c);
                sum[best] += new Vector3(c.r, c.g, c.b);
                num[best]++;
            }
            for (int i = 0; i < centers.Count; i++) if (num[i] > 0) centers[i] = sum[i] / num[i];
        }
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            Vector3 v = centers[Nearest(centers, px[i])];
            px[i] = new Color32((byte)Mathf.RoundToInt(v.x), (byte)Mathf.RoundToInt(v.y), (byte)Mathf.RoundToInt(v.z), 255);
        }
    }

    static int Nearest(List<Vector3> centers, Color32 c)
    {
        var v = new Vector3(c.r, c.g, c.b);
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < centers.Count; i++)
        {
            float d = (centers[i] - v).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    static int Saturation(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
    static uint Key(Color32 c) => ((uint)c.r << 16) | ((uint)c.g << 8) | c.b;
    static Color32 FromKey(uint k) => new Color32((byte)(k >> 16), (byte)(k >> 8), (byte)k, 255);
}
