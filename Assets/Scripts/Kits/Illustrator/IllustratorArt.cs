using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Illustrator のキットの絵（2026-11-19。docs/app-kits-2-design.md 3-4）。コードの中のドット絵。
/// こげ茶 #330000・オレンジ #FF9A00・パスは細いオレンジの線、点は白い四角・バウンディングボックス（8つの白い四角）
/// </summary>
public static class IllustratorArt
{
    public static readonly Color Brown = RuntimeSprites.Hex("330000");
    public static readonly Color Orange = RuntimeSprites.Hex("FF9A00");
    public static readonly Color OrangeLight = RuntimeSprites.Hex("FFC266");
    public static readonly Color Gray = RuntimeSprites.Hex("8A8A8A");
    public static readonly Color BoxBlue = RuntimeSprites.Hex("4F9CFF");

    /// <summary>アンカーポイント（白い四角に、オレンジのふち。7×7）</summary>
    public static Sprite Anchor => RuntimeSprites.FromAscii("AiAnchor", new[]
    {
        "ooooooo",
        "owwwwwo",
        "owwwwwo",
        "owwwwwo",
        "owwwwwo",
        "owwwwwo",
        "ooooooo",
    }, new Dictionary<char, Color32> { { 'o', Orange }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>ハンドルの先の丸（5×5）</summary>
    public static Sprite HandleDot => RuntimeSprites.FromAscii("AiHandleDot", new[]
    {
        ".ooo.",
        "owwwo",
        "owwwo",
        "owwwo",
        ".ooo.",
    }, new Dictionary<char, Color32> { { 'o', Orange }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>バウンディングボックスの四角（5×5。白に青いふち）</summary>
    public static Sprite BoxHandle => RuntimeSprites.FromAscii("AiBoxHandle", new[]
    {
        "bbbbb",
        "bwwwb",
        "bwwwb",
        "bwwwb",
        "bbbbb",
    }, new Dictionary<char, Color32> { { 'b', BoxBlue }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>星の先の小さい三角の弾（7×7。右向き）</summary>
    public static Sprite Triangle => RuntimeSprites.FromAscii("AiTriangle", new[]
    {
        "o......",
        "ooo....",
        "ooooo..",
        "ooooooo",
        "ooooo..",
        "ooo....",
        "o......",
    }, new Dictionary<char, Color32> { { 'o', Orange } });

    /// <summary>アンカーポイント1つ（点と、両側のハンドル）を parent の子に作る。tangent はハンドルの向き</summary>
    public static void AnchorWithHandles(Transform parent, Vector2 p, Vector2 tangent, float handle, int order)
    {
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector2.right;
        tangent.Normalize();
        Vector2 h0 = p - tangent * handle, h1 = p + tangent * handle;
        PathStroke.Bar(parent, h0, h1, KitDress.Px, Orange, order, "HandleLine");
        RuntimeSprites.Show(parent, HandleDot, h0 - (Vector2)parent.position, order + 1, 1f, "HandleDot");
        RuntimeSprites.Show(parent, HandleDot, h1 - (Vector2)parent.position, order + 1, 1f, "HandleDot");
        RuntimeSprites.Show(parent, Anchor, p - (Vector2)parent.position, order + 2, 1f, "Anchor");
    }

    /// <summary>バウンディングボックス（細い線と 8つの四角）を root の子に作る。root の位置がまん中、size はマス</summary>
    public static List<SpriteRenderer> BoundingBox(Transform root, Vector2 size, int order)
    {
        var list = new List<SpriteRenderer>();
        Vector2 h = size * 0.5f, o = root.position;
        float w = KitDress.Px;
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, h.y), o + new Vector2(h.x, h.y), w, BoxBlue, order, "Top"));
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, -h.y), o + new Vector2(h.x, -h.y), w, BoxBlue, order, "Bottom"));
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, -h.y), o + new Vector2(-h.x, h.y), w, BoxBlue, order, "Left"));
        list.Add(PathStroke.Bar(root, o + new Vector2(h.x, -h.y), o + new Vector2(h.x, h.y), w, BoxBlue, order, "Right"));
        for (int ix = -1; ix <= 1; ix++)
            for (int iy = -1; iy <= 1; iy++)
            {
                if (ix == 0 && iy == 0) continue;
                list.Add(RuntimeSprites.Show(root, BoxHandle, new Vector2(ix * h.x, iy * h.y), order + 1, 1f, "Handle"));
            }
        return list;
    }
}
