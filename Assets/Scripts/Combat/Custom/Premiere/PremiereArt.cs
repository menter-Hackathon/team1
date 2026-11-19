using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>エフェクトアイコンの種類（2026-11-18。docs/app-boss-skills-design.md 3-0）。値は変えない</summary>
public enum PremiereIcon
{
    /// <summary>炎：爆発（円）</summary>
    Fire = 0,
    /// <summary>雷：落雷（縦の柱）</summary>
    Thunder = 1,
    /// <summary>ディゾルブ：残るもや（能力だけ）</summary>
    Dissolve = 2,
}

/// <summary>
/// 【仮】Premiere Pro の技と能力で使い回す絵と色（2026-11-18。docs/app-boss-skills-design.md 3章）。
/// 絵はすべてコードの中のドット絵（RuntimeSprites.FromAscii）。1マス＝18ドット。色の16進は仕様書のとおり（どれも【仮】）
/// </summary>
public static class PremiereArt
{
    /// <summary>1ドット（マス）</summary>
    public const float Pixel = 1f / 18f;
    /// <summary>【仮】画面の上の表示（HUD）が隠す高さ（マス）。「画面の上の端」に出す物は、これより下に出す</summary>
    public const float HudInset = 1.9f;

    // ───────── 色【仮】 ─────────
    public static readonly Color IconBg = Hex("#232323");
    public static readonly Color FireColor = Hex("#FF9500");
    public static readonly Color FireCore = Hex("#FFD60A");
    public static readonly Color ThunderColor = Hex("#FFD60A");
    public static readonly Color ThunderEdge = Hex("#FFFFFF");
    public static readonly Color DissolveColor = Hex("#9999FF");
    public static readonly Color DissolveLight = Hex("#E6E6FF");
    public static readonly Color TimelineBg = Hex("#232323");
    public static readonly Color TimelineTick = Hex("#8A8A8A");
    public static readonly Color PlayheadBlue = Hex("#2D8CEB");
    public static readonly Color BladeSilver = Hex("#D0D0D8");
    public static readonly Color BladeShadow = Hex("#8A8A94");
    public static readonly Color WindowBg = Hex("#232323");
    public static readonly Color WindowTitle = Hex("#3A3A3A");
    public static readonly Color WindowBorder = Hex("#5A5A5E");
    public static readonly Color FastForward = Hex("#FFD60A");

    static Color Hex(string hex) => RuntimeSprites.Hex(hex);

    /// <summary>アイコンの色（線・範囲の色に使う）</summary>
    public static Color IconColor(PremiereIcon kind)
    {
        switch (kind)
        {
            case PremiereIcon.Fire: return FireColor;
            case PremiereIcon.Thunder: return ThunderColor;
            default: return DissolveColor;
        }
    }

    // ───────── エフェクトアイコン（18×18。角の丸い濃い灰色の四角の中に絵） ─────────

    static readonly Dictionary<char, Color32> IconPalette = new Dictionary<char, Color32>
    {
        { 'k', RuntimeSprites.Hex("#232323") },
        { 'o', RuntimeSprites.Hex("#FF9500") },
        { 'y', RuntimeSprites.Hex("#FFD60A") },
        { 'w', RuntimeSprites.Hex("#FFFFFF") },
        { 'p', RuntimeSprites.Hex("#9999FF") },
        { 'l', RuntimeSprites.Hex("#E6E6FF") },
    };

    static readonly string[] FireRows =
    {
        "..kkkkkkkkkkkkkk..",
        ".kkkkkkkkkkkkkkkk.",
        "kkkkkkkkokkkkkkkkk",
        "kkkkkkkkookkkkkkkk",
        "kkkkkkkoookkkkkkkk",
        "kkkkkkkooookkkkkkk",
        "kkkkkkoooookkokkkk",
        "kkkkkkooooooookkkk",
        "kkkkkoooooyooookkk",
        "kkkkkooooyyyoookkk",
        "kkkkoooooyyyyoookk",
        "kkkkooooyyyyyoookk",
        "kkkkooooyyyyyoookk",
        "kkkkkoooyyyyoookkk",
        "kkkkkkoooyyoookkkk",
        "kkkkkkkooooookkkkk",
        ".kkkkkkkkkkkkkkkk.",
        "..kkkkkkkkkkkkkk..",
    };

    static readonly string[] ThunderRows =
    {
        "..kkkkkkkkkkkkkk..",
        ".kkkkkkkkkkkkkkkk.",
        "kkkkkkkkkkwwwwwkkk",
        "kkkkkkkkkwyyyywkkk",
        "kkkkkkkkwyyyywkkkk",
        "kkkkkkkwyyyywkkkkk",
        "kkkkkkwyyyywkkkkkk",
        "kkkkkwyyyyywwwwkkk",
        "kkkkwyyyyyyyyywkkk",
        "kkkkwwwwwyyyywkkkk",
        "kkkkkkkkwyyywkkkkk",
        "kkkkkkkwyyywkkkkkk",
        "kkkkkkkwyywkkkkkkk",
        "kkkkkkwyywkkkkkkkk",
        "kkkkkkwywkkkkkkkkk",
        "kkkkkwwkkkkkkkkkkk",
        ".kkkkkkkkkkkkkkkk.",
        "..kkkkkkkkkkkkkk..",
    };

    static readonly string[] DissolveRows =
    {
        "..kkkkkkkkkkkkkk..",
        ".kkkkkkkkkkkkkkkk.",
        "kkkkkkkkkkkkkkkkkk",
        "kkklllllllllllpkkk",
        "kkkllllllllllppkkk",
        "kkklllllllllpppkkk",
        "kkkllllllllppppkkk",
        "kkklllllllpppppkkk",
        "kkkllllllppppppkkk",
        "kkklllllpppppppkkk",
        "kkkllllppppppppkkk",
        "kkklllpppppppppkkk",
        "kkkllppppppppppkkk",
        "kkklpppppppppppkkk",
        "kkkppppppppppppkkk",
        "kkkkkkkkkkkkkkkkkk",
        ".kkkkkkkkkkkkkkkk.",
        "..kkkkkkkkkkkkkk..",
    };

    /// <summary>エフェクトアイコンの絵（1×1マス）</summary>
    public static Sprite Icon(PremiereIcon kind)
    {
        switch (kind)
        {
            case PremiereIcon.Fire: return RuntimeSprites.FromAscii("Premiere_Icon_Fire", FireRows, IconPalette);
            case PremiereIcon.Thunder: return RuntimeSprites.FromAscii("Premiere_Icon_Thunder", ThunderRows, IconPalette);
            default: return RuntimeSprites.FromAscii("Premiere_Icon_Dissolve", DissolveRows, IconPalette);
        }
    }

    // ───────── ほかの小さい絵 ─────────

    /// <summary>再生ヘッドのつまみ（五角形、横10ドット）。下の先が線につながる</summary>
    public static Sprite PlayheadKnob => RuntimeSprites.FromAscii("Premiere_PlayheadKnob", new[]
    {
        "bbbbbbbbbb",
        "bbbbbbbbbb",
        "bbbbbbbbbb",
        "bbbbbbbbbb",
        "bbbbbbbbbb",
        ".bbbbbbbb.",
        "..bbbbbb..",
        "...bbbb...",
        "....bb....",
    }, new Dictionary<char, Color32> { { 'b', RuntimeSprites.Hex("#2D8CEB") } }, new Vector2(0.5f, 0f));

    /// <summary>カミソリの刃（横1.2×縦0.8マス）。真ん中に穴</summary>
    public static Sprite RazorBlade => RuntimeSprites.FromAscii("Premiere_RazorBlade", new[]
    {
        "..ssssssssssssssssss..",
        ".ssssssssssssssssssss.",
        "ssssssssssssssssssssss",
        "ssssssssssssssssssssss",
        "sssss............sssss",
        ".ssss............ssss.",
        "..sss............sss..",
        "..sss............sss..",
        ".ssss............ssss.",
        "sssss............sssss",
        "ssssssssssssssssssssss",
        "ssssssssssssssssssssss",
        ".dddddddddddddddddddd.",
        "..dddddddddddddddddd..",
    }, new Dictionary<char, Color32> { { 's', RuntimeSprites.Hex("#D0D0D8") }, { 'd', RuntimeSprites.Hex("#8A8A94") } });

    /// <summary>早送りの印 ▶▶（8ドット、黄色）</summary>
    public static Sprite FastForwardMark => RuntimeSprites.FromAscii("Premiere_FastForward", new[]
    {
        "y...y...",
        "yy..yy..",
        "yyy.yyy.",
        "yyyyyyyy",
        "yyyyyyyy",
        "yyy.yyy.",
        "yy..yy..",
        "y...y...",
    }, new Dictionary<char, Color32> { { 'y', RuntimeSprites.Hex("#FFD60A") } });

    /// <summary>時計の文字盤（12ドット、うすい紫）。針は別に回す</summary>
    public static Sprite ClockFace => RuntimeSprites.FromAscii("Premiere_ClockFace", new[]
    {
        "...llllll...",
        "..l......l..",
        ".l........l.",
        "l..........l",
        "l..........l",
        "l..........l",
        "l..........l",
        "l..........l",
        "l..........l",
        ".l........l.",
        "..l......l..",
        "...llllll...",
    }, new Dictionary<char, Color32> { { 'l', RuntimeSprites.Hex("#E6E6FF") } });

    /// <summary>小さい炎（7×10ドット）。着地した所の火に並べる</summary>
    public static Sprite SmallFlame => RuntimeSprites.FromAscii("Premiere_SmallFlame", new[]
    {
        "...o...",
        "...o...",
        "..ooo..",
        "..ooo.o",
        ".ooooo.",
        ".ooyoo.",
        "ooyyyoo",
        "ooyyyoo",
        ".oyyyo.",
        "..ooo..",
    }, new Dictionary<char, Color32> { { 'o', RuntimeSprites.Hex("#FF9500") }, { 'y', RuntimeSprites.Hex("#FFD60A") } }, new Vector2(0.5f, 0f));

    /// <summary>
    /// 落雷の柱の1段（横1.5×縦1マス＝27×18ドット）。ぎざぎざの黄色に白い芯。上下につなげて並べる。variant で左右が逆になる
    /// </summary>
    public static Sprite BoltTile(int variant)
    {
        int v = variant & 1;
        return Pattern($"Premiere_BoltTile_{v}", 27, 18, (x, y) =>
        {
            // 三角の波（18ドットで1往復）。上下の端で同じ位置になるので、つなげても切れない
            int t = y % 18;
            float zig = t < 9 ? -4f + t : 4f - (t - 9);
            if (v == 1) zig = -zig;
            float d = Mathf.Abs(x - (13f + zig));
            if (d <= 1f) return 'w';
            if (d <= 3.5f) return 'y';
            return '.';
        }, new Dictionary<char, Color32> { { 'w', RuntimeSprites.Hex("#FFFFFF") }, { 'y', RuntimeSprites.Hex("#FFD60A") } });
    }

    /// <summary>もや（3×3マス＝54×54ドット）。真ん中ほど濃い、紫のつぶつぶ</summary>
    public static Sprite FogBlob => Pattern("Premiere_FogBlob", 54, 54, (x, y) =>
    {
        float dx = (x + 0.5f) / 27f - 1f, dy = (y + 0.5f) / 27f - 1f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float density = Mathf.Clamp01(1.05f - r);           // 真ん中 1 → ふち 0
        int bayer = Bayer4[(y & 3) * 4 + (x & 3)];          // 4×4 のディザ
        float threshold = (bayer + 0.5f) / 16f;
        if (density > threshold + 0.35f) return 'l';
        if (density > threshold) return 'p';
        return '.';
    }, new Dictionary<char, Color32> { { 'p', RuntimeSprites.Hex("#9999FFC0") }, { 'l', RuntimeSprites.Hex("#E6E6FFC0") } });

    static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    /// <summary>
    /// 斜線の模様（うすい紫 #9999FF の3割）。大きさは widthUnits×heightUnits マス。レーザーツールの切られる区間に使う
    /// </summary>
    public static Sprite Hatch(float widthUnits, float heightUnits)
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(widthUnits * 18f));
        int h = Mathf.Max(1, Mathf.RoundToInt(heightUnits * 18f));
        return Pattern($"Premiere_Hatch_{w}x{h}", w, h, (x, y) => (x + y) % 8 < 2 ? 'p' : '.',
            new Dictionary<char, Color32> { { 'p', RuntimeSprites.Hex("#9999FF4D") } });
    }

    /// <summary>縦の点線（太さ2ドット、4ドット描いて4ドットあける）。長さ heightUnits マス</summary>
    public static Sprite DottedColumn(float heightUnits)
    {
        int h = Mathf.Max(1, Mathf.RoundToInt(heightUnits * 18f));
        return Pattern($"Premiere_DottedColumn_{h}", 2, h, (x, y) => y % 8 < 4 ? 'w' : '.',
            new Dictionary<char, Color32> { { 'w', RuntimeSprites.Hex("#FFFFFF") } });
    }

    /// <summary>点線の四角の枠（太さ1ドット）。大きさ widthUnits×heightUnits マス。消えた足場の跡</summary>
    public static Sprite DashedFrame(float widthUnits, float heightUnits)
    {
        int w = Mathf.Max(2, Mathf.RoundToInt(widthUnits * 18f));
        int h = Mathf.Max(2, Mathf.RoundToInt(heightUnits * 18f));
        return Pattern($"Premiere_DashedFrame_{w}x{h}", w, h, (x, y) =>
        {
            bool edgeX = x == 0 || x == w - 1, edgeY = y == 0 || y == h - 1;
            if (edgeY && x % 4 < 2) return 'w';
            if (edgeX && y % 4 < 2) return 'w';
            return '.';
        }, new Dictionary<char, Color32> { { 'w', RuntimeSprites.Hex("#FFFFFF") } });
    }

    static readonly Dictionary<string, Sprite> PatternCache = new Dictionary<string, Sprite>();

    /// <summary>文字の並びを関数で作ってからスプライトにする（大きい模様用）。f(x, y) の y は上から。同じ key は1回だけ作る</summary>
    static Sprite Pattern(string key, int w, int h, System.Func<int, int, char> f, IReadOnlyDictionary<char, Color32> palette)
    {
        if (PatternCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        var rows = new string[h];
        var sb = new StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Length = 0;
            for (int x = 0; x < w; x++) sb.Append(f(x, y));
            rows[y] = sb.ToString();
        }
        Sprite sprite = RuntimeSprites.FromAscii(key, rows, palette);
        PatternCache[key] = sprite;
        return sprite;
    }

    // ───────── 道具 ─────────

    /// <summary>
    /// 絵（SpriteRenderer）の絵を差し替え、指定した大きさ（マス）に合わせる。落下物などの絵を実行時のアイコンにするとき用
    /// </summary>
    public static void SetSprite(SpriteRenderer sr, Sprite sprite, Vector2 size)
    {
        if (sr == null || sprite == null) return;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sprite = sprite;
        Vector2 native = sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
    }

    /// <summary>四角（白い絵を伸ばした物）を1つ置く。位置は parent からの位置（マス）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localPos, Vector2 size, Color color, int sortingOrder, string objectName = "Box")
    {
        var sr = AttackRunner.AddVisual(parent, PlaceholderSprites.Square, color, size, sortingOrder, objectName);
        sr.transform.localPosition = localPos;
        return sr;
    }

    /// <summary>空の子の GameObject を作る（持ち主と同じレイヤー）</summary>
    public static Transform Child(Transform parent, string objectName, Vector2 localPos)
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    /// <summary>色の濃さだけを変える</summary>
    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
