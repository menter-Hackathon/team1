using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アプリの色（AppTheme）で、絵の色だけを入れ替えた複製を作る（ドット絵のパレット替え。ドットは伸びない）。
/// 作った絵は覚えておき、同じ組み合わせなら使い回す。元の絵は「読み書きできる」設定で取り込んでおくこと（ArtAssets が入れる）。
/// </summary>
public static class ThemeRecolor
{
    // 元の絵の色（Tools/art/gen_sprites.py と同じ）
    static readonly Color32 Sky1 = new Color32(0x42, 0x9f, 0xec, 0xff);
    static readonly Color32 Sky2 = new Color32(0x40, 0x9b, 0xe5, 0xff);
    static readonly Color32 White = new Color32(0xff, 0xff, 0xff, 0xff);
    static readonly Color32 RoadTop = new Color32(0xa6, 0xa6, 0xa6, 0xff);
    static readonly Color32 Road = new Color32(0x79, 0x79, 0x79, 0xff);
    static readonly Color32 RoadDot = new Color32(0x76, 0x76, 0x76, 0xff);
    static readonly Color32 G8B = new Color32(0x8b, 0x8b, 0x8b, 0xff);
    static readonly Color32 G68 = new Color32(0x68, 0x68, 0x68, 0xff);
    static readonly Color32 G51 = new Color32(0x51, 0x51, 0x51, 0xff);
    static readonly Color32 G4A = new Color32(0x4a, 0x4a, 0x4a, 0xff);
    // フォルダの足場（Platform_Folder.png）の3色。2026-11-19：Mac の青いフォルダにした（docs/mac-look-design.md 28）
    static readonly Color32 FolderBody = new Color32(0x5a, 0xb0, 0xf0, 0xff);
    static readonly Color32 FolderShade = new Color32(0x2f, 0x7f, 0xc4, 0xff);
    static readonly Color32 FolderTabBase = new Color32(0x8f, 0xcb, 0xf7, 0xff);
    // 前のクリーム色のフォルダ（絵を取り込み直す前でも塗り替えられるように、残しておく）
    static readonly Color32 Cream = new Color32(0xf5, 0xf1, 0x98, 0xff);
    static readonly Color32 Olive = new Color32(0xb3, 0xb0, 0x6f, 0xff);
    static readonly Color32 CreamTab = new Color32(0xf6, 0xf2, 0x9a, 0xff);
    static readonly Color32 TabTitle = Sky1; // 新しいタブのウィンドウのタイトルバー
    static readonly Color32 TitleShade = new Color32(0x2a, 0x6f, 0xb0, 0xff); // 部屋のタイトルバーの影

    public enum Kind { Wallpaper, Floor, Wall, Folder, TitleBar }

    static readonly Dictionary<(int, int, int), Sprite> Cache = new Dictionary<(int, int, int), Sprite>();
    static readonly Dictionary<(int, bool), Sprite> SlotCache = new Dictionary<(int, bool), Sprite>();
    static readonly Dictionary<int, Sprite[]> TabOpenCache = new Dictionary<int, Sprite[]>();
    static readonly Dictionary<(int, int), Sprite> EffectCache = new Dictionary<(int, int), Sprite>();

    /// <summary>
    /// 能力の効果の絵（柱・爆発・弾など）を、アプリの色に塗り替えた複製（2026-11-17：その場で作る能力）。
    /// 元の絵の明るさを4段に分けて、暗い色・代表の色・明るい色・ほぼ白に置き換える（ドット絵の陰影は残る）。
    /// 塗り替えられなければ元の絵
    /// </summary>
    public static Sprite Effect(Sprite src, AppTheme theme)
    {
        if (src == null || theme == null) return src;
        var key = (src.GetInstanceID(), theme.GetInstanceID());
        if (EffectCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Color32[] px = Read(src, out int w, out int h);
        if (px == null) return src;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var c in px)
        {
            if (c.a == 0) continue;
            float l = Lum(c);
            lo = Mathf.Min(lo, l);
            hi = Mathf.Max(hi, l);
        }
        Color main = theme.Main;
        Color32 dark = Mix(main, Color.black, 0.5f);
        Color32 mid = Mix(main, Color.black, 0f);
        Color32 light = Mix(main, Color.white, 0.45f);
        Color32 top = Mix(main, Color.white, 0.8f);
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (c.a == 0) continue;
            float t = hi > lo ? (Lum(c) - lo) / (hi - lo) : 0.5f;
            Color32 to = t < 0.3f ? dark : t < 0.65f ? mid : t < 0.9f ? light : top;
            to.a = c.a;
            px[i] = to;
        }
        Sprite made = Make(px, w, h, src, $"{src.name}_{theme.name}");
        EffectCache[key] = made;
        return made;
    }

    static readonly Dictionary<(int, int), Sprite> SmokeCache = new Dictionary<(int, int), Sprite>();
    static readonly Dictionary<(int, uint), Sprite> ColorizeCache = new Dictionary<(int, uint), Sprite>();

    /// <summary>
    /// 絵を1つの色の濃淡にした複製（2026-11-19：通知の大技の赤いもや）。明るさを3段に分けて、暗い色・その色・明るい色に置き換える。
    /// 塗り替えられなければ元の絵
    /// </summary>
    public static Sprite Colorize(Sprite src, Color color)
    {
        if (src == null) return src;
        Color32 c32 = color;
        var key = (src.GetInstanceID(), ((uint)c32.r << 16) | ((uint)c32.g << 8) | c32.b);
        if (ColorizeCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Color32[] px = Read(src, out int w, out int h);
        if (px == null) return src;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var c in px)
        {
            if (c.a == 0) continue;
            float l = Lum(c);
            lo = Mathf.Min(lo, l);
            hi = Mathf.Max(hi, l);
        }
        Color32 dark = Mix(color, Color.black, 0.45f);
        Color32 mid = Mix(color, Color.black, 0f);
        Color32 light = Mix(color, Color.white, 0.45f);
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (c.a == 0) continue;
            float t = hi > lo ? (Lum(c) - lo) / (hi - lo) : 0.5f;
            Color32 to = t < 0.33f ? dark : t < 0.7f ? mid : light;
            to.a = c.a;
            px[i] = to;
        }
        Sprite made = Make(px, w, h, src, $"{src.name}_tint");
        ColorizeCache[key] = made;
        return made;
    }

    /// <summary>
    /// ウイルスの黒い煙を、アプリの色味にした複製（2026-11-19：AI チャットのボスの後ろに漂う煙）。
    /// 暗さは残し（黒っぽいまま）、色味だけをアプリの代表の色に寄せる。明るさを3段に分けて置き換える。塗り替えられなければ元の絵
    /// </summary>
    public static Sprite Smoke(Sprite src, AppTheme theme)
    {
        if (src == null || theme == null) return src;
        var key = (src.GetInstanceID(), theme.GetInstanceID());
        if (SmokeCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Color32[] px = Read(src, out int w, out int h);
        if (px == null) return src;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var c in px)
        {
            if (c.a == 0) continue;
            float l = Lum(c);
            lo = Mathf.Min(lo, l);
            hi = Mathf.Max(hi, l);
        }
        Color main = theme.Main;
        Color32 deep = Mix(main, Color.black, 0.82f);
        Color32 mid = Mix(main, Color.black, 0.58f);
        Color32 light = Mix(main, Color.black, 0.3f);
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (c.a == 0) continue;
            float t = hi > lo ? (Lum(c) - lo) / (hi - lo) : 0f;
            Color32 to = t < 0.33f ? deep : t < 0.7f ? mid : light;
            to.a = c.a;
            px[i] = to;
        }
        Sprite made = Make(px, w, h, src, $"{src.name}_smoke_{theme.name}");
        SmokeCache[key] = made;
        return made;
    }

    /// <summary>ステージの絵をアプリの色に塗り替えた複製。塗り替えられなければ元の絵</summary>
    public static Sprite Stage(Sprite src, AppTheme theme, Kind kind)
    {
        if (src == null || theme == null) return src;
        var key = (src.GetInstanceID(), theme.GetInstanceID(), (int)kind);
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Color32[] px = Read(src, out int w, out int h);
        if (px == null) return src;
        switch (kind)
        {
            case Kind.Wallpaper: RecolorWallpaper(px, theme); break;
            case Kind.Floor:
                Map(px, new Dictionary<uint, Color32>
                {
                    { Key(RoadTop), theme.FloorTop },
                    { Key(G68), Mix(theme.Floor, Color.black, 0.35f) },
                    { Key(Road), theme.Floor },
                    { Key(RoadDot), Mix(theme.Floor, Color.black, 0.08f) },
                    { Key(G8B), Mix(theme.Floor, Color.white, 0.12f) },
                });
                break;
            case Kind.Wall:
                Map(px, new Dictionary<uint, Color32>
                {
                    { Key(G51), theme.Wall },
                    { Key(G68), Mix(theme.Wall, theme.WallHighlight, 0.5f) },
                    { Key(Road), theme.WallHighlight },
                    { Key(G4A), Mix(theme.Wall, Color.black, 0.12f) },
                });
                break;
            case Kind.TitleBar: // ウィンドウの足場（タイトルバーの下の 2ドットの帯。2026-11-19）・部屋のタイトルバー：空色をアプリの代表の色に
                Map(px, new Dictionary<uint, Color32>
                {
                    { Key(Sky1), theme.Main },
                    { Key(TitleShade), Mix(theme.Main, Color.black, 0.35f) },
                });
                break;
            case Kind.Folder:
                Map(px, new Dictionary<uint, Color32>
                {
                    { Key(FolderBody), theme.Folder },
                    { Key(FolderShade), theme.FolderShade },
                    { Key(FolderTabBase), theme.FolderTab },
                    { Key(Cream), theme.Folder },
                    { Key(Olive), theme.FolderShade },
                    { Key(CreamTab), theme.FolderTab },
                });
                break;
        }
        Sprite made = Make(px, w, h, src, $"{src.name}_{theme.name}");
        Cache[key] = made;
        return made;
    }

    /// <summary>能力の枠（20×20 ドット）。縁をアプリの色で塗る（4色なら上・右・下・左）。選択中は縁が2重</summary>
    public static Sprite Slot(AppTheme theme, bool selected)
    {
        if (theme == null) return null;
        var key = (theme.GetInstanceID(), selected);
        if (SlotCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        const int n = 20;
        var px = new Color32[n * n];
        var face = new Color32(0xdc, 0xdc, 0xdc, 0xff);
        for (int i = 0; i < px.Length; i++) px[i] = face;
        Color[] acc = theme.Accents;
        bool four = acc.Length >= 4;
        int rings = selected ? 2 : 1;
        for (int k = 1; k <= rings; k++)
        {
            for (int i = k; i < n - k; i++)
            {
                Set(px, n, i, k, four ? acc[0] : theme.Main);          // 上
                Set(px, n, n - 1 - k, i, four ? acc[1] : theme.Main);  // 右
                Set(px, n, i, n - 1 - k, four ? acc[2] : theme.Main);  // 下
                Set(px, n, k, i, four ? acc[3] : theme.Main);          // 左
            }
        }
        for (int i = 0; i < n; i++)
        {
            Set(px, n, i, 0, Color.black); Set(px, n, i, n - 1, Color.black);
            Set(px, n, 0, i, Color.black); Set(px, n, n - 1, i, Color.black);
        }
        var tex = NewTexture(n, n, $"Slot_{theme.name}_{selected}");
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), GameArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        s.name = tex.name;
        SlotCache[key] = s;
        return s;
    }

    /// <summary>新しいタブ（瞬間移動）のウィンドウのコマを、タイトルバーだけアプリの色にした複製</summary>
    public static Sprite[] TabOpen(Sprite[] frames, AppTheme theme)
    {
        if (frames == null || frames.Length == 0 || theme == null) return frames;
        int key = theme.GetInstanceID();
        if (TabOpenCache.TryGetValue(key, out Sprite[] cached) && cached != null && cached.Length == frames.Length && cached[0] != null) return cached;
        Color[] acc = theme.Accents;
        var result = new Sprite[frames.Length];
        for (int f = 0; f < frames.Length; f++)
        {
            Color32[] px = Read(frames[f], out int w, out int h);
            if (px == null) return frames;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!Same(px[i], TabTitle)) continue;
                    int seg = Mathf.Clamp(x * acc.Length / w, 0, acc.Length - 1); // 左から色を順に
                    px[i] = acc[seg];
                }
            }
            result[f] = Make(px, w, h, frames[f], $"{frames[f].name}_{theme.name}");
        }
        TabOpenCache[key] = result;
        return result;
    }

    // ───────── 中身 ─────────

    static void RecolorWallpaper(Color32[] px, AppTheme theme)
    {
        Color[] hills = theme.Hills;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var c in px)
        {
            if (IsSky(c) || Same(c, White) || c.a == 0) continue;
            float l = Lum(c);
            lo = Mathf.Min(lo, l);
            hi = Mathf.Max(hi, l);
        }
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (c.a == 0) continue;
            if (IsSky(c)) px[i] = theme.Sky;
            else if (Same(c, White)) px[i] = theme.Cloud;
            else if (hills != null && hills.Length >= 2 && hi > lo) px[i] = Ramp(hills, (Lum(c) - lo) / (hi - lo));
        }
    }

    static Color Ramp(Color[] ramp, float t)
    {
        t = Mathf.Clamp01(t) * (ramp.Length - 1);
        int i = Mathf.Min((int)t, ramp.Length - 2);
        Color c = Color.Lerp(ramp[i], ramp[i + 1], t - i);
        c.a = 1f;
        return c;
    }

    static void Map(Color32[] px, Dictionary<uint, Color32> map)
    {
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            byte a = px[i].a;
            if (map.TryGetValue(Key(new Color32(px[i].r, px[i].g, px[i].b, 255)), out Color32 to))
            {
                to.a = a;
                px[i] = to;
            }
        }
    }

    static Color32[] Read(Sprite s, out int w, out int h)
    {
        Rect r = s.textureRect;
        w = Mathf.RoundToInt(r.width);
        h = Mathf.RoundToInt(r.height);
        if (!s.texture.isReadable)
        {
            Debug.LogWarning($"{s.texture.name} は読み書きできない設定なので、色を塗り替えられません（Tools/Team1/グラフィックを適用）");
            return null;
        }
        Color[] c = s.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
        var px = new Color32[c.Length];
        for (int i = 0; i < c.Length; i++) px[i] = c[i];
        return px;
    }

    static Sprite Make(Color32[] px, int w, int h, Sprite src, string name)
    {
        var tex = NewTexture(w, h, name);
        tex.SetPixels32(px);
        tex.Apply();
        Vector2 pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
        s.name = name;
        return s;
    }

    static Texture2D NewTexture(int w, int h, string name) => new Texture2D(w, h, TextureFormat.RGBA32, false)
    {
        name = name,
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp,
    };

    static void Set(Color32[] px, int n, int x, int y, Color c) => px[(n - 1 - y) * n + x] = c; // y は上から
    static uint Key(Color32 c) => ((uint)c.r << 16) | ((uint)c.g << 8) | c.b;
    static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
    static bool IsSky(Color32 c) => Same(c, Sky1) || Same(c, Sky2);
    static float Lum(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    static Color32 Mix(Color a, Color b, float t) { Color c = Color.Lerp(a, b, t); c.a = 1f; return c; }
}
