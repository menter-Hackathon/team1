using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro のタイムラインの絵と色（2026-11-19。docs/app-feedback-v2-design.md 5章）。
/// ボスの技（再生ヘッド・レーザーツール・キーフレーム）と、ステージ（PremiereTimelineStage）と、能力（リップル削除）で使い回す。
/// 絵はコードの中のドット絵。1マス＝18ドット。色はどれも【仮】（5章の頭の色の表）
/// </summary>
public static class PremiereTimelineArt
{
    public const float Px = 1f / 18f;

    // ───────── 色【仮】 ─────────
    public static readonly Color Panel = RuntimeSprites.Hex("232323");
    public static readonly Color Dark = RuntimeSprites.Hex("1D1D1D");
    public static readonly Color Line = RuntimeSprites.Hex("2E2E2E");
    public static readonly Color Text = RuntimeSprites.Hex("A7A7A7");
    public static readonly Color Blue = RuntimeSprites.Hex("2D8CEB");
    public static readonly Color ClipPurple = RuntimeSprites.Hex("9A8CF0");
    public static readonly Color ClipBlue = RuntimeSprites.Hex("6E9CD8");
    public static readonly Color Audio = RuntimeSprites.Hex("3FB58B");
    public static readonly Color MarkerGreen = RuntimeSprites.Hex("5ECC4B");
    public static readonly Color RenderRed = RuntimeSprites.Hex("D93A2F");
    public static readonly Color RenderGreen = RuntimeSprites.Hex("3DBE4B");
    public static readonly Color EnemyRed = RuntimeSprites.Hex("FF453A");

    static readonly Color32 W = new Color32(255, 255, 255, 255);

    // ───────── 小さい絵 ─────────

    /// <summary>マーカー（下向きの五角形、横10ドット。赤）。原点は下の先</summary>
    public static Sprite Marker => RuntimeSprites.FromAscii("PrTl_Marker", new[]
    {
        "rrrrrrrrrr",
        "rrrrrrrrrr",
        "rrrrrrrrrr",
        "rrrrrrrrrr",
        ".rrrrrrrr.",
        "..rrrrrr..",
        "...rrrr...",
        "....rr....",
    }, new Dictionary<char, Color32> { { 'r', W } }, new Vector2(0.5f, 0f));

    /// <summary>キーフレームのひし形（9ドット。白に赤いふち 1ドット）</summary>
    public static Sprite Diamond => RuntimeSprites.FromAscii("PrTl_Diamond", new[]
    {
        "....r....",
        "...rwr...",
        "..rwwwr..",
        ".rwwwwwr.",
        "rwwwwwwwr",
        ".rwwwwwr.",
        "..rwwwr..",
        "...rwr...",
        "....r....",
    }, new Dictionary<char, Color32> { { 'w', W }, { 'r', EnemyRed } });

    /// <summary>動くクリップ（22×14ドット。紫の角の丸い四角・左にサムネイル・上に白い棒・赤いふち）</summary>
    public static Sprite EnemyClip => RuntimeSprites.FromAscii("PrTl_EnemyClip", new[]
    {
        ".rrrrrrrrrrrrrrrrrrrr.",
        "rppppppppppppppppppppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkpwwwwwwwwwwpppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkppppppppppppppr",
        "rpkkkkkpddddddddddddpr",
        "rpkkkkkppppppppppppppr",
        "rppppppppppppppppppppr",
        "rppppppppppppppppppppr",
        ".rrrrrrrrrrrrrrrrrrrr.",
    }, new Dictionary<char, Color32>
    {
        { 'r', EnemyRed }, { 'p', ClipPurple }, { 'k', Dark }, { 'w', W }, { 'd', RuntimeSprites.Hex("6B5FC4") },
    });

    /// <summary>白い四角（1ドット）。伸ばして使う</summary>
    public static Sprite Pixel => GenericFrameKit.Pixel;

    // ───────── 並べて描く絵（FullRect にする。Tiled で使う） ─────────

    static Sprite _checker, _dotH, _dotV, _hatch;

    /// <summary>空のタイムラインの暗い市松（4ドットのます。#1D1D1D と #232323）。Tiled で並べる</summary>
    public static Sprite Checker => _checker != null ? _checker : (_checker = Strip("PrTl_Checker", 8, 8, (x, y) => ((x / 4 + y / 4) & 1) == 0 ? Dark : Panel));

    /// <summary>横の点線（2ドット描いて2ドットあける。高さ 1ドット。白）。Tiled で並べる</summary>
    public static Sprite DotRow => _dotH != null ? _dotH : (_dotH = Strip("PrTl_DotRow", 4, 1, (x, y) => x < 2 ? (Color)W : Color.clear));

    /// <summary>縦の点線（4ドット描いて4ドットあける。幅 1ドット。白）。Tiled で並べる</summary>
    public static Sprite DotColumn => _dotV != null ? _dotV : (_dotV = Strip("PrTl_DotColumn", 1, 8, (x, y) => y >= 4 ? (Color)W : Color.clear));

    /// <summary>赤むらさきのしま（#FF453A と #9A8CF0。斜め。それぞれ3割）。Tiled で並べる</summary>
    public static Sprite RedPurpleHatch => _hatch != null ? _hatch : (_hatch = Strip("PrTl_Hatch", 8, 8, (x, y) =>
    {
        int k = (x + y) % 8;
        if (k < 2) return WithAlpha(EnemyRed, 0.3f);
        if (k >= 4 && k < 6) return WithAlpha(ClipPurple, 0.3f);
        return Color.clear;
    }));

    static Sprite _purpleHatch;

    /// <summary>紫 #9A8CF0 のしま（3割。斜め）。プレイヤーの物（赤は使わない）。Tiled で並べる</summary>
    public static Sprite PurpleHatch => _purpleHatch != null ? _purpleHatch : (_purpleHatch = Strip("PrTl_PurpleHatch", 8, 8, (x, y) =>
        (x + y) % 8 < 3 ? WithAlpha(ClipPurple, 0.3f) : Color.clear));

    static Sprite Strip(string name, int w, int h, System.Func<int, int, Color> f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) px[y * w + x] = f(x, h - 1 - y);
        }
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    /// <summary>並べて描く絵を置く（大きさはマス。ドットは伸びない）</summary>
    public static SpriteRenderer Tiled(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Tiled")
    {
        var go = new GameObject(name);
        go.layer = parent != null ? parent.gameObject.layer : 0;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>並べて描く絵の大きさを変える</summary>
    public static void SetTiledSize(SpriteRenderer sr, Vector2 size)
    {
        if (sr == null) return;
        sr.size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
    }

    /// <summary>a から b へ、点線（太さ thicknessPx ドット）を引く</summary>
    public static SpriteRenderer DottedLine(Transform parent, Vector2 a, Vector2 b, float thicknessPx, Color color, int order, string name = "Dotted")
    {
        Vector2 d = b - a;
        float len = Mathf.Max(0.01f, d.magnitude);
        SpriteRenderer sr = Tiled(parent, DotRow, Vector2.zero, new Vector2(len, thicknessPx * Px), color, order, name);
        sr.transform.position = (a + b) * 0.5f;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return sr;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    /// <summary>
    /// マーカーの予告（ルーラーの上の赤い五角形・真下へ床までの赤い点線・柱になる幅のうすい赤の帯）を置く。
    /// x は柱のまん中、rulerY は五角形の下の先の高さ、floorY は床の上面、topY は帯の上の端。返した物の子の "Marker" が五角形
    /// </summary>
    public static Transform BuildMarker(Transform parent, float x, float rulerY, float floorY, float topY, float width, int order)
    {
        Transform root = PremiereArt.Child(parent, "DipMarker", Vector2.zero);
        root.position = new Vector3(x, floorY, 0f);
        float h = Mathf.Max(0.1f, topY - floorY);
        // 帯（15%）
        PremiereArt.Box(root, new Vector2(0f, h * 0.5f), new Vector2(width, h), WithAlpha(EnemyRed, 0.15f), order - 2, "Band");
        // 赤い点線（ルーラーから床まで）。2026-11-20：topY がルーラーより上なら、topY まで伸ばす（編集画面全体：柱の判定の上下いっぱいに予告を見せる）
        float lineH = Mathf.Max(0.05f, Mathf.Max(rulerY, topY) - floorY);
        Tiled(root, DotColumn, new Vector2(0f, lineH * 0.5f), new Vector2(Px, lineH), EnemyRed, order - 1, "Line");
        SpriteRenderer mark = RuntimeSprites.Show(root, Marker, new Vector2(0f, rulerY - floorY), order, 1f, "Marker");
        mark.color = EnemyRed;
        return root;
    }
}

/// <summary>
/// 【仮】ディップ トゥ ホワイト（白く飛ぶトランジション）の柱の絵（2026-11-19。docs/app-feedback-v2-design.md 5-2）。
/// 判定（AreaStrike）の子に付ける。白い柱に赤いふち 2ドット。出た瞬間に画面全体も 0.05秒だけ白くうすく光る（1割）。
/// 判定と一緒に消える
/// </summary>
public class PremiereDip : MonoBehaviour
{
    SpriteRenderer _core, _left, _right;
    float _start, _duration;

    /// <summary>parent の真ん中に、横 width×縦 height（マス）の白い柱を duration 秒出す</summary>
    public static PremiereDip Attach(Transform parent, float width, float height, float duration, bool screenFlash = true)
    {
        Transform root = PremiereArt.Child(parent, "PremiereDip", Vector2.zero);
        var d = root.gameObject.AddComponent<PremiereDip>();
        d._start = Time.time;
        d._duration = Mathf.Max(0.05f, duration);
        float edge = 2f * PremiereTimelineArt.Px;
        d._core = PremiereArt.Box(root, Vector2.zero, new Vector2(Mathf.Max(0.05f, width - edge * 2f), height), Color.white, 22, "Core");
        d._left = PremiereArt.Box(root, new Vector2(-width * 0.5f + edge * 0.5f, 0f), new Vector2(edge, height), PremiereTimelineArt.EnemyRed, 23, "EdgeL");
        d._right = PremiereArt.Box(root, new Vector2(width * 0.5f - edge * 0.5f, 0f), new Vector2(edge, height), PremiereTimelineArt.EnemyRed, 23, "EdgeR");
        if (screenFlash) ScreenFx.WhiteFlash(0.1f, 0.05f);
        d.Apply(0f);
        return d;
    }

    void Update() => Apply(Mathf.Clamp01((Time.time - _start) / _duration));

    void Apply(float t)
    {
        // 出た瞬間は横に少し太く、すぐ元の幅。最後の3割でうすくなる
        float a = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
        float sx = t < 0.15f ? Mathf.Lerp(1.25f, 1f, t / 0.15f) : 1f;
        transform.localScale = new Vector3(sx, 1f, 1f);
        if (_core != null) _core.color = new Color(1f, 1f, 1f, a);
        if (_left != null) _left.color = PremiereTimelineArt.WithAlpha(PremiereTimelineArt.EnemyRed, a);
        if (_right != null) _right.color = PremiereTimelineArt.WithAlpha(PremiereTimelineArt.EnemyRed, a);
    }
}
