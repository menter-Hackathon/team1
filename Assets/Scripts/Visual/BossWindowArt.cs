using UnityEngine;

/// <summary>
/// 代用ボスが着るウィンドウの絵を、ゲームの中で作る（2026-11-17：対応表に無いアプリのボス）。
/// Tools/art/gen_sprites.py の「着ているウィンドウ」（48×36、3段階）と同じ作り方：
/// 縁3重・タイトルバー（アイコンの色を左から）・閉じるボタン・中にアプリのアイコン（24 ドット）。
/// 0 きれい、1 小さなひび、2 大きなひびと欠けた角
/// </summary>
public static class BossWindowArt
{
    const int W = 48, H = 36;
    static readonly Color32 W51 = new Color32(0x51, 0x51, 0x51, 0xff);
    static readonly Color32 W79 = new Color32(0x79, 0x79, 0x79, 0xff);
    static readonly Color32 White = new Color32(0xff, 0xff, 0xff, 0xff);
    static readonly Color32 Orange = new Color32(0xdf, 0x71, 0x26, 0xff);
    static readonly Color32 Black = new Color32(0, 0, 0, 0xff);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    public static Sprite[] Create(Sprite icon24, AppTheme theme, string spriteName)
    {
        var stages = new Sprite[3];
        for (int stage = 0; stage < 3; stage++) stages[stage] = Make(icon24, theme, stage, $"{spriteName}_{stage}");
        return stages;
    }

    static Sprite Make(Sprite icon, AppTheme theme, int stage, string name)
    {
        var px = new Color32[W * H];
        Color main = theme != null ? theme.Main : Color.gray;
        Color32 back = Color.Lerp(main, Color.white, 0.86f);
        for (int i = 0; i < px.Length; i++) px[i] = back;
        Color32[] rings = { W51, W79, White };
        for (int i = 0; i < 3; i++)
        {
            for (int x = i; x < W - i; x++) { Set(px, x, i, rings[i]); Set(px, x, H - 1 - i, rings[i]); }
            for (int y = i; y < H - i; y++) { Set(px, i, y, rings[i]); Set(px, W - 1 - i, y, rings[i]); }
        }
        // タイトルバー（アイコンの色を左から並べる）と閉じるボタン
        Color[] acc = theme != null ? theme.Accents : new[] { main };
        for (int x = 3; x < W - 3; x++)
        {
            int seg = Mathf.Min(acc.Length - 1, (x - 3) * acc.Length / (W - 6));
            for (int y = 3; y < 7; y++) Set(px, x, y, acc[seg]);
        }
        for (int x = W - 9; x <= W - 4; x++) for (int y = 3; y <= 6; y++) Set(px, x, y, Orange);
        Set(px, W - 7, 4, White); Set(px, W - 6, 5, White); Set(px, W - 6, 4, White); Set(px, W - 7, 5, White);
        // 中にアプリのアイコン
        if (icon != null && icon.texture.isReadable)
        {
            Rect r = icon.textureRect;
            int iw = (int)r.width, ih = (int)r.height;
            Color32[] src = icon.texture.GetPixels32();
            int texW = icon.texture.width;
            int ox = (W - iw) / 2, oy = 8;
            for (int y = 0; y < ih; y++)
            {
                for (int x = 0; x < iw; x++)
                {
                    Color32 c = src[((int)r.y + (ih - 1 - y)) * texW + (int)r.x + x]; // y は上から
                    if (c.a > 0) Set(px, ox + x, oy + y, c);
                }
            }
        }
        if (stage >= 1) // ひび
        {
            Line(px, new[] { (31, 7), (28, 13), (32, 18), (27, 24), (30, 32) }, 10);
            foreach (var (x, y) in new[] { (9, 22), (11, 24), (13, 26), (10, 28) }) { Set(px, x, y, Black); Set(px, x + 1, y, Black); }
        }
        if (stage >= 2) // 大きなひびと、欠けた角
        {
            Line(px, new[] { (5, 9), (10, 14), (8, 19), (14, 23) }, 8);
            for (int y = H - 11; y < H; y++)
            {
                for (int x = W - 14 + (H - y); x < W; x++) Set(px, x, y, Clear);
                Set(px, W - 14 + (H - y) - 1, y, W51);
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), GameArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    static void Line(Color32[] px, (int x, int y)[] pts, int steps)
    {
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            for (int t = 0; t < steps; t++)
                Set(px, a.x + (b.x - a.x) * t / steps, a.y + (b.y - a.y) * t / steps, Black);
        }
    }

    // y は上から数える（gen_sprites.py と同じ）
    static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        px[(H - 1 - y) * W + x] = c;
    }
}
