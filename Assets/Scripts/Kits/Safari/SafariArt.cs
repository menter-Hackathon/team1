using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Safari のキットの色・絵・小さな道具（2026-11-19。docs/app-kits-2-design.md 4-4）。
/// 青 #006CFF、コンパスの針は赤と白、うすい灰色の UI。絵はコードの中のドット絵（1マス＝18ドット）。
/// </summary>
public static class SafariArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("006CFF");
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color Cyan = RuntimeSprites.Hex("64D2FF");
    public static readonly Color Paper = RuntimeSprites.Hex("F7F3E8");

    public const float Px = 1f / 18f;

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) },
        { 'B', (Color32)Blue },
        { 'b', RuntimeSprites.Hex("004FC4") },
        { 'C', (Color32)Cyan },
        { 'R', RuntimeSprites.Hex("FF3B30") },
        { 'r', RuntimeSprites.Hex("B3261E") },
        { 'G', RuntimeSprites.Hex("C7C7CC") },
        { 'g', RuntimeSprites.Hex("8E8E93") },
        { 'K', RuntimeSprites.Hex("101014") },
    };

    /// <summary>コンパスの文字盤（青い円・白いふち・目盛り。22ドット）</summary>
    public static Sprite Dial()
    {
        const int d = 22;
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dd = Mathf.Sqrt(dx * dx + dy * dy);
                char c = '.';
                if (dd <= r) c = 'W';
                if (dd <= r - 1.5f) c = dd < r * 0.55f ? 'C' : 'B';
                // 目盛り（上下左右）
                if (dd > r - 4f && dd <= r - 1.5f && (Mathf.Abs(dx) < 0.6f || Mathf.Abs(dy) < 0.6f)) c = 'W';
                line[x] = c;
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii("SF_Dial", rows, Pal);
    }

    /// <summary>コンパスの針（右が赤、左が白。24×6）</summary>
    public static Sprite Needle() => RuntimeSprites.FromAscii("SF_Needle", new[]
    {
        "..........GG............",
        "......GGGWWWRRRrr.......",
        "GGGWWWWWWWWWRRRRRRRRRRrr",
        "GGGWWWWWWWWWRRRRRRRRRRrr",
        "......GGGWWWRRRrr.......",
        "..........GG............",
    }, Pal);

    /// <summary>飛ぶ針（赤い側。弾の絵。12×6。右が先）</summary>
    public static Sprite RedNeedle() => RuntimeSprites.FromAscii("SF_RedNeedle", new[]
    {
        "rr..........",
        "rRRRRr......",
        "rRRRRRRRRRRr",
        "rRRRRRRRRRRr",
        "rRRRRr......",
        "rr..........",
    }, Pal);

    /// <summary>飛ぶ針（白い側。弾の絵。12×6。右が先）</summary>
    public static Sprite WhiteNeedle() => RuntimeSprites.FromAscii("SF_WhiteNeedle", new[]
    {
        "gg..........",
        "gWWWWg......",
        "gWWWWWWWWWWg",
        "gWWWWWWWWWWg",
        "gWWWWg......",
        "gg..........",
    }, Pal);

    /// <summary>盾（トラッカーのブロック。9×10）</summary>
    public static Sprite Shield() => RuntimeSprites.FromAscii("SF_Shield", new[]
    {
        "WWWWWWWWW",
        "WBBBBBBBW",
        "WBBBWBBBW",
        "WBBWWWBBW",
        "WBBBWBBBW",
        "WBBBBBBBW",
        ".WBBBBBW.",
        ".WBBBBBW.",
        "..WBBBW..",
        "...WWW...",
    }, Pal);

    /// <summary>暗い所で光る目（白い目に黒いひとみ。16×6）</summary>
    public static Sprite Eyes() => RuntimeSprites.FromAscii("SF_Eyes", new[]
    {
        ".WWWW....WWWW.",
        "WWWWWW..WWWWWW",
        "WWKKWW..WWKKWW",
        "WWKKWW..WWKKWW",
        "WWWWWW..WWWWWW",
        ".WWWW....WWWW.",
    }, Pal);

    /// <summary>明るい弾（白い丸に青いふち。10ドット）</summary>
    public static Sprite BrightShot()
    {
        const int d = 10;
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dd = dx * dx + dy * dy;
                line[x] = dd > r * r ? '.' : dd > (r - 1.6f) * (r - 1.6f) ? 'C' : 'W';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii("SF_BrightShot", rows, Pal);
    }

    // ───────── 道具 ─────────

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

    /// <summary>2点を結ぶ線（四角を回して伸ばす）。from・to はワールド座標</summary>
    public static void SetLine(SpriteRenderer sr, Vector2 from, Vector2 to, float widthPx)
    {
        if (sr == null) return;
        Vector2 d = to - from;
        float len = d.magnitude;
        sr.transform.position = (from + to) * 0.5f;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(len / Mathf.Max(0.001f, native.x), widthPx * Px / Mathf.Max(0.001f, native.y), 1f);
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
            return new Vector2(b.center.x, b.max.y + 0.8f);
        }
        return owner.OriginPoint + Vector2.up * 1.2f;
    }

    /// <summary>弾の絵を差し替える（左へ飛ぶ弾は 180°回るので、上下を戻す）</summary>
    public static void Dress(Projectile p, Sprite sprite, Vector2 dir)
    {
        if (p == null || sprite == null) return;
        if (!KitDress.Swap(p.gameObject, sprite)) return;
        SpriteRenderer sr = KitDress.FindVisual(p.transform);
        if (sr != null && dir.x < 0f) sr.flipY = true;
    }
}
