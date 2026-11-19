using UnityEngine;

/// <summary>
/// 代用ボスが着るウィンドウの絵を、ゲームの中で作る（2026-11-17：対応表に無いアプリのボス）。
/// 0 きれい、1 小さなひび、2 大きなひびと欠けた角。
///
/// 2026-11-19【仮】Mac の形にした（docs/mac-look-design.md 2章 2）。大きさ 48×36・中心の軸・FullRect は今のまま（TerminalReboot が 48×36 で探す）。
/// ドットの並び（y は上から。gen_sprites.py の armor() も、この並びで描く＝担当3 の 4）：
/// ・角：丸い。2ドット欠く（dx＋dy が 2 より小さい所は透明）。四隅の 3ドットずつが透明
/// ・ふち：1ドット #AEAEB2（黒い煙の上なので明るいまま）。立体の3重のふちは無し
/// ・タイトルバー：1〜7行目 #E5E5EA（x＝1〜46）
/// ・信号機：3×3 の丸（角の 4ドットは 5割すかす）。x＝3・7・11（各 3ドット）、3〜5行目。赤 #FF5F57・黄 #FEBC2E・緑 #28C840
/// ・題名の棒：#8E8E93、x＝17〜30（14ドット）、3〜4行目
/// ・右上：何も置かない（× は無し）
/// ・アプリの色の帯：8〜9行目（x＝1〜46）。アイコンの色を左から等分に並べる
/// ・中の面：10〜34行目 #F2F2F7。アプリのアイコン（24ドット）を x＝12、10行目から
/// ・ひび：今と同じ点（(31,7)→(28,13)→(32,18)→(27,24)→(30,32) と、小さい点 4つ。2段目は (5,9)→(10,14)→(8,19)→(14,23)）
/// ・欠けた角（2段目）：右下の三角を透明にし、欠けたふちを #AEAEB2 で描く
/// </summary>
public static class BossWindowArt
{
    public const int W = 48, H = 36;
    /// <summary>タイトルバーの行（上のふちの下から）</summary>
    public const int TitleTop = 1, TitleBottom = 7;
    /// <summary>アプリの色の帯の行</summary>
    public const int BandTop = 8, BandBottom = 9;
    /// <summary>信号機の左の端と右の端（x）、上の行</summary>
    public const int LightsLeft = 3, LightsRight = 13, LightsTop = 3;
    /// <summary>題名の棒の左の端・幅・上の行</summary>
    public const int TitleBarX = 17, TitleBarW = 14, TitleBarY = 3;
    const int IconTop = 10;
    const int CornerCut = 2;

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
        Color32 edge = MacUi.Edge(MacLook.Light);
        Color32 back = MacUi.WindowBg(MacLook.Light);
        Color32 title = MacUi.TitleBg(MacLook.Light);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (!MacUi.InRound(x, y, W, H, CornerCut)) { Set(px, x, y, Clear); continue; }
                bool isEdge = !MacUi.InRound(x - 1, y, W, H, CornerCut) || !MacUi.InRound(x + 1, y, W, H, CornerCut)
                    || !MacUi.InRound(x, y - 1, W, H, CornerCut) || !MacUi.InRound(x, y + 1, W, H, CornerCut);
                Color32 c = isEdge ? edge : y >= TitleTop && y <= TitleBottom ? title : back;
                Set(px, x, y, c);
            }
        }
        // 信号機（左上）と題名の棒（まん中）。右上は空
        System.Action<int, int, Color32> put = (x, y, c) => Blend(px, x, y, c);
        MacUi.StampLights(put, LightsLeft, LightsTop);
        for (int y = TitleBarY; y < TitleBarY + MacUi.TitleBarH; y++)
            for (int x = TitleBarX; x < TitleBarX + TitleBarW; x++) Set(px, x, y, MacUi.SubText);

        // アプリの色の帯（タイトルバーの下の 2ドット。アイコンの色を左から）
        Color main = theme != null ? theme.Main : Color.gray;
        Color[] acc = theme != null && theme.Accents != null && theme.Accents.Length > 0 ? theme.Accents : new[] { main };
        for (int x = 1; x < W - 1; x++)
        {
            int seg = Mathf.Min(acc.Length - 1, (x - 1) * acc.Length / (W - 2));
            Color c = acc[seg];
            c.a = 1f;
            for (int y = BandTop; y <= BandBottom; y++) Set(px, x, y, c);
        }

        // 中にアプリのアイコン（24ドット）
        if (icon != null && icon.texture.isReadable)
        {
            Rect r = icon.textureRect;
            int iw = (int)r.width, ih = (int)r.height;
            Color32[] src = icon.texture.GetPixels32();
            int texW = icon.texture.width;
            int ox = (W - iw) / 2, oy = IconTop + Mathf.Max(0, (H - 1 - IconTop - ih) / 2);
            for (int y = 0; y < ih; y++)
            {
                for (int x = 0; x < iw; x++)
                {
                    Color32 c = src[((int)r.y + (ih - 1 - y)) * texW + (int)r.x + x]; // y は上から
                    if (c.a > 0 && oy + y < H - 1) Set(px, ox + x, oy + y, c);
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
                Set(px, W - 14 + (H - y) - 1, y, edge);
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

    // 半とうめいの色は重ねる（信号機の角）
    static void Blend(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = (H - 1 - y) * W + x;
        px[i] = MacUi.Over(px[i], c);
    }

    /// <summary>信号機の丸の所か（TerminalReboot が赤く塗るときに、よける）</summary>
    public static bool IsLight(int x, int y)
    {
        if (y < LightsTop || y > LightsTop + MacUi.LightD - 1 || x < LightsLeft || x > LightsRight) return false;
        return (x - LightsLeft) % (MacUi.LightD + MacUi.LightGap) < MacUi.LightD;
    }
}
