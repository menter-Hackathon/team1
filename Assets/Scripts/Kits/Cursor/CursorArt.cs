using UnityEngine;

/// <summary>
/// 【仮】Cursor のキットの色・絵・小さな道具（2026-11-19。docs/app-kits-2-design.md 3-3）。
/// 決まり：灰色のゴースト（#8A8A8A の半透明）＝これから起きること（安全）。赤に変わってから 0.8秒で本物になる。
/// </summary>
public static class CursorArt
{
    public static readonly Color Black = RuntimeSprites.Hex("141414");
    public static readonly Color Ghost = RuntimeSprites.Hex("8A8A8A");
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color Green = RuntimeSprites.Hex("3FB950");
    public static readonly Color Purple = RuntimeSprites.Hex("A371F7");

    public const float Px = 1f / 18f;

    /// <summary>意味のないコードの行（等幅の記号）</summary>
    public static readonly string[] CodeLines =
    {
        "fn fix() { return ok; }",
        "if (virus) { patch(); }",
        "let x = await gen(42);",
        "for (i = 0; i < n; i++)",
        "const v = run(main);",
    };

    public static SpriteRenderer Box(Transform parent, Vector2 localCenter, Vector2 size, Color color, int order, string name = "Box")
        => FxKit.Box(parent, localCenter, size, color, order, name);

    public static void SetBox(SpriteRenderer sr, Vector2 localCenter, Vector2 size)
    {
        if (sr == null) return;
        sr.transform.localPosition = localCenter;
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(Mathf.Max(0f, size.x) / Mathf.Max(0.001f, native.x), Mathf.Max(0f, size.y) / Mathf.Max(0.001f, native.y), 1f);
    }

    public static TextMesh Text(Transform parent, string text, Vector2 localPos, float height, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        TextMesh tm = WorldText.Create(parent, text, Mathf.Max(0.4f, height), color, order, true, anchor);
        tm.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
        return tm;
    }

    public static SpriteRenderer Dotted(Transform parent, Vector2 localCenter, float length, int thick, float angle, Color color, int order)
        => BrowserDevFx.DottedLine(parent, localCenter, length, thick, angle, color, order);

    /// <summary>点線の四角い枠（4本の点線を返す）</summary>
    public static SpriteRenderer[] DottedBox(Transform parent, Vector2 center, Vector2 size, Color color, int order)
    {
        float hx = size.x * 0.5f, hy = size.y * 0.5f, t = Px;
        return new[]
        {
            Dotted(parent, center + new Vector2(0f, hy - t), size.x, 2, 0f, color, order),
            Dotted(parent, center + new Vector2(0f, -hy + t), size.x, 2, 0f, color, order),
            Dotted(parent, center + new Vector2(-hx + t, 0f), size.y, 2, 90f, color, order),
            Dotted(parent, center + new Vector2(hx - t, 0f), size.y, 2, 90f, color, order),
        };
    }

    /// <summary>Tab のキーの印（灰色の角の丸いキーに Tab）。中心 localPos</summary>
    public static Transform TabKey(Transform parent, Vector2 localPos, int order, Color? ink = null)
    {
        var root = new GameObject("TabKey").transform;
        root.gameObject.layer = parent.gameObject.layer;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        Box(root, new Vector2(0f, -Px), new Vector2(1.6f, 0.7f), MacUi.KeyShade, order, "Shade");
        Box(root, new Vector2(0f, Px * 0.5f), new Vector2(1.6f - 2f * Px, 0.7f - Px), MacUi.KeyFace, order + 1, "Face");
        Text(root, "Tab", new Vector2(0f, 0.02f), 0.4f, ink ?? MacUi.KeyInk, order + 2);
        return root;
    }

    public static Hitbox AddHit(AttackRunner owner, AttackData data, Transform parent, Vector2 localCenter, Vector2 size, string name = "Hit")
    {
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        return owner.AddHitbox(go, data, col);
    }

    public static bool Blink(float left, float slow = 0.12f, float fast = 0.05f)
    {
        float step = left < 0.3f ? fast : slow;
        return left > 0.6f || Mathf.FloorToInt(Time.time / step) % 2 == 0;
    }

    public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    public static Vector2 HeadOf(AttackRunner owner)
    {
        if (owner == null) return Vector2.zero;
        var boss = owner.GetComponent<BossController>();
        if (boss != null)
        {
            Bounds b = boss.BodyBounds;
            return new Vector2(b.center.x, b.max.y + 0.5f);
        }
        return owner.OriginPoint + Vector2.up * 1.2f;
    }

    /// <summary>プレイヤーの体の絵（子の Visual/Art。無ければ null）</summary>
    public static SpriteRenderer PlayerArt(Transform player)
    {
        if (player == null) return null;
        Transform art = player.Find("Visual/Art");
        return art != null ? art.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// プレイヤーの形のゴースト（絵を写した物。色だけ変えられる）。center は体の真ん中。
    /// 絵が無ければ 0.7×1.0 の四角
    /// </summary>
    public static SpriteRenderer PlayerGhost(Transform parent, Transform player, Vector2 localCenter, Color color, int order)
    {
        SpriteRenderer src = PlayerArt(player);
        if (src == null || src.sprite == null)
        {
            SpriteRenderer box = Box(parent, localCenter, new Vector2(0.7f, 1f), color, order, "Ghost");
            return box;
        }
        var go = new GameObject("Ghost");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.flipX = src.flipX;
        sr.sortingOrder = order;
        sr.color = color;
        Vector3 s = src.transform.lossyScale;
        go.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);
        // 絵の真ん中を体の真ん中に合わせる
        Vector2 offset = (Vector2)src.bounds.center - (Vector2)src.transform.position;
        go.transform.localPosition = localCenter - offset;
        return sr;
    }
}
