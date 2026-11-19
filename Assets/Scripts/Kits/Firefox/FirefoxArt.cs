using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】Firefox のキットの絵（2026-11-19。docs/app-kits-2-design.md 4-6）。コードの中のドット絵だけ。
// オレンジ #FF7139 〜 黄 #FFBD4F、むらさき #7542E5。コンテナタブの色（青・オレンジ・緑・ピンク）。Chrome の絵は使わない

/// <summary>Firefox のキットの絵</summary>
public static class FirefoxArt
{
    public static readonly Color32 Orange = RuntimeSprites.Hex("FF7139");
    public static readonly Color32 Yellow = RuntimeSprites.Hex("FFBD4F");
    public static readonly Color32 Purple = RuntimeSprites.Hex("7542E5");
    public static readonly Color32 Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color32 Deep = RuntimeSprites.Hex("E31587");
    public static readonly Color32 Dark = RuntimeSprites.Hex("20123A");

    /// <summary>コンテナタブの色（青・オレンジ・緑・ピンク）</summary>
    public static readonly Color32[] Containers =
    {
        RuntimeSprites.Hex("37ADFF"), RuntimeSprites.Hex("FF9F00"), RuntimeSprites.Hex("51CD00"), RuntimeSprites.Hex("FF4BDA"),
    };

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'o', Orange }, { 'y', Yellow }, { 'p', Purple }, { 'd', Deep }, { 'w', new Color32(255, 255, 255, 255) },
        { 'k', Dark }, { 'r', Red }, { 'W', new Color32(255, 248, 220, 255) }, { 'b', RuntimeSprites.Hex("37ADFF") },
        { 'g', RuntimeSprites.Hex("51CD00") }, { 's', RuntimeSprites.Hex("C7C7CC") },
    };

    /// <summary>火の玉（しっぽの炎。12ドット）</summary>
    public static Sprite Fireball => RuntimeSprites.FromAscii("Firefox_Fireball", new[]
    {
        "....dd......",
        "..ddood.....",
        ".doooyood...",
        "dooyyyyoodd.",
        "doyyWWyyood.",
        "doyWWWWyyodd",
        "doyWWWWyyod.",
        "doyyWWyyood.",
        ".dooyyyyod..",
        "..dooooodd..",
        "...ddddd....",
        "............",
    }, Pal);

    /// <summary>床に残る炎（幅 1.5×高さ 1 マス＝27×18 ドット）。i で 2つの形を交互に</summary>
    public static Sprite Flame(int i) => Make("Firefox_Flame" + (i & 1), 27, 18, (x, y) =>
    {
        // y は下から。炎の先がゆれる形
        float phase = (i & 1) == 0 ? 0f : 1.7f;
        float top = 10f + 6f * Mathf.Abs(Mathf.Sin(x * 0.45f + phase)) + 2f * Mathf.Sin(x * 1.3f + phase);
        if (y > top) return new Color32(0, 0, 0, 0);
        float k = y / Mathf.Max(1f, top);
        if (k < 0.35f) return Yellow;
        if (k < 0.7f) return Orange;
        return Deep;
    });

    /// <summary>コンテナの区画の炎の柱（幅 1 ドットの帯を伸ばして使う。下が黄、上がむらさき）</summary>
    public static Sprite PillarStrip => Make("Firefox_PillarStrip", 1, 32, (x, y) =>
    {
        float k = y / 31f;
        return k < 0.4f ? Yellow : k < 0.75f ? Orange : Deep;
    });

    /// <summary>パズルのピース（14ドット。白。色を掛けて使う）</summary>
    public static Sprite Piece => RuntimeSprites.FromAscii("Firefox_Piece", new[]
    {
        ".....ww.......",
        "....wwww......",
        "....wwww......",
        "wwwwwwwwwwww..",
        "wwwwwwwwwwww..",
        "wwwwwwwwwwwwww",
        "wwwwwwwwwwwwww",
        "wwwwwwwwwwwww.",
        "wwwwwwwwwwwwww",
        "wwwwwwwwwwwwww",
        "wwwwwwwwwwww..",
        "wwwww..wwwww..",
        "wwww....wwww..",
        "..............",
    }, Pal);

    /// <summary>ピースの中の印（0＝靴・1＝剣・2＝時計。7ドット。黒）</summary>
    public static Sprite PieceMark(int kind)
    {
        switch (kind)
        {
            case 0:
                return RuntimeSprites.FromAscii("Firefox_MarkSpeed", new[] { ".kk....", ".kk....", ".kk....", ".kkkk..", "kkkkkk.", "kkkkkkk", "......." }, Pal);
            case 1:
                return RuntimeSprites.FromAscii("Firefox_MarkPower", new[] { ".....kk", "....kk.", "...kk..", "k.kk...", ".kk....", "k.k....", "......." }, Pal);
            default:
                return RuntimeSprites.FromAscii("Firefox_MarkCool", new[] { "..kkk..", ".k.k.k.", "k..k..k", "k..kk.k", "k.....k", ".k...k.", "..kkk.." }, Pal);
        }
    }

    /// <summary>アドオンの色（0＝速さ 青・1＝力 オレンジ・2＝クールタイム 緑）</summary>
    public static Color AddonColor(int kind) => kind == 0 ? (Color)Containers[0] : kind == 1 ? (Color)Orange : (Color)Containers[2];

    /// <summary>HUD の印（8ドット）：ピースの形にアドオンの色</summary>
    public static Sprite AddonIcon(int kind) => RuntimeSprites.FromAscii("Firefox_AddonIcon" + kind, new[]
    {
        "...cc...",
        "..cccc..",
        "cccccccc",
        "ccccccc.",
        "cccccccc",
        "cccccccc",
        "ccc..ccc",
        "........",
    }, new Dictionary<char, Color32> { { 'c', AddonColor(kind) } });

    /// <summary>ボスのアドオンの印（8ドット。紫のピース。ボスのゲージの下）</summary>
    public static Sprite BossAddonIcon => RuntimeSprites.FromAscii("Firefox_BossAddonIcon", new[]
    {
        "...cc...",
        "..cccc..",
        "cccccccc",
        "ccccccc.",
        "cccccccc",
        "cccccccc",
        "ccc..ccc",
        "........",
    }, new Dictionary<char, Color32> { { 'c', Purple } });

    /// <summary>ピクチャー・イン・ピクチャーの小さい窓（2×1.5マス＝36×27 ドット。赤いふち・再生の三角・進みの線）</summary>
    public static Sprite PipWindow => Make("Firefox_Pip", 36, 27, (x, y) =>
    {
        // y は下から
        bool edge = x < 2 || y < 2 || x >= 34 || y >= 25;
        if (edge) return Red;
        if (y >= 21) return RuntimeSprites.Hex("2B2A33"); // 上の帯
        if (y >= 21 - 1) return RuntimeSprites.Hex("42414D");
        if (y >= 4 && y <= 5 && x >= 5 && x <= 30) return x <= 14 ? (Color32)Orange : RuntimeSprites.Hex("5B5B66");
        // 再生の三角
        int cx = 16, cy = 13;
        int dx = x - cx, dy = Mathf.Abs(y - cy);
        if (dx >= 0 && dx <= 6 && dy <= 6 - dx) return new Color32(255, 255, 255, 255);
        return RuntimeSprites.Hex("15141A");
    });

    /// <summary>広告ブロックの盾（12ドット）</summary>
    public static Sprite Shield => RuntimeSprites.FromAscii("Firefox_Shield", new[]
    {
        ".pppppppppp.",
        "pppppwwppppp",
        "ppppwwwwpppp",
        "pppwwwwwwppp",
        "pppwwrrwwppp",
        "pppwrrrrwppp",
        "ppppwrrwpppp",
        ".pppwwwwppp.",
        ".ppppwwpppp.",
        "..pppppppp..",
        "...pppppp...",
        ".....pp.....",
    }, Pal);

    /// <summary>白い円（ふちだけ。直径 d ドット）</summary>
    public static Sprite Ring(int d) => Make("Firefox_Ring" + d, d, d, (x, y) =>
    {
        float r = d * 0.5f - 0.5f;
        float dist = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
        return dist <= r + 0.3f && dist >= r - 1.2f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
    });

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>関数で 1 ドットずつ色を決めて絵を作る（x は左から、y は下から）。同じ名前は1回だけ作る</summary>
    public static Sprite Make(string key, int w, int h, Func<int, int, Color32> color)
    {
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Kit_" + key,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = color(x, y);
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        s.name = key;
        s.hideFlags = HideFlags.DontSave;
        Cache[key] = s;
        return s;
    }
}
