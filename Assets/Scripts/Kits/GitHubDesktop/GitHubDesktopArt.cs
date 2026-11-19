using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】GitHub Desktop のキットの色・絵・小さな道具（2026-11-19。docs/app-kits-2-design.md 2-2）。
/// 絵はコードの中のドット絵（1マス＝18ドット）。色は仕様書のとおり（どれも【仮】）。
/// </summary>
public static class GitHubDesktopArt
{
    public static readonly Color Navy = RuntimeSprites.Hex("24292F");
    public static readonly Color Green = RuntimeSprites.Hex("2DA44E");
    public static readonly Color DiffRed = RuntimeSprites.Hex("CF222E");
    public static readonly Color Purple = RuntimeSprites.Hex("8250DF");
    public static readonly Color Blue = RuntimeSprites.Hex("0969DA");
    /// <summary>危ない物の予告の赤</summary>
    public static readonly Color Danger = RuntimeSprites.Hex("FF453A");

    /// <summary>1ドット（マス）</summary>
    public const float Px = 1f / 18f;

    static Dictionary<char, Color32> Pal => new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) },
        { 'B', (Color32)Blue },
        { 'P', (Color32)Purple },
        { 'N', (Color32)Navy },
        { 'G', (Color32)Green },
        { 'R', (Color32)DiffRed },
    };

    /// <summary>コミットの丸（白いふちに青。11ドット）</summary>
    public static Sprite Commit() => Disc("GH_Commit", 11, 'B');

    /// <summary>Revert のコミットの丸（白いふちに紫。13ドット）</summary>
    public static Sprite RevertCommit() => Disc("GH_RevertCommit", 13, 'P');

    /// <summary>HUD の枠の絵（Revert の丸を置いている間）</summary>
    public static Sprite RevertIcon() => Disc("GH_RevertIcon", 16, 'P');

    /// <summary>背景のブランチの線の丸（5ドット）</summary>
    public static Sprite SmallCommit(bool virus) => Disc(virus ? "GH_DotV" : "GH_DotM", 5, virus ? 'P' : 'B');

    static Sprite Disc(string key, int d, char fill)
    {
        var rows = new string[d];
        float r = d * 0.5f;
        float inner = r - 1.6f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dd = dx * dx + dy * dy;
                line[x] = dd > r * r ? '.' : dd > inner * inner ? 'W' : fill;
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, Pal);
    }

    /// <summary>上向きの矢印（Push の ↑。白。5×7）</summary>
    public static Sprite UpArrow() => RuntimeSprites.FromAscii("GH_Up", new[]
    {
        "..W..",
        ".WWW.",
        "WWWWW",
        "..W..",
        "..W..",
        "..W..",
        "..W..",
    }, Pal);

    /// <summary>床の矢印（閉じる向き。白で描いて色を掛ける。7×5。右向き）</summary>
    public static Sprite FloorArrow() => RuntimeSprites.FromAscii("GH_FloorArrow", new[]
    {
        "...W...",
        "....W..",
        "WWWWWW.",
        "....W..",
        "...W...",
    }, Pal);

    // ───────── 道具 ─────────

    /// <summary>四角（中心・大きさはマス）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localCenter, Vector2 size, Color color, int order, string name = "Box")
        => FxKit.Box(parent, localCenter, size, color, order, name);

    /// <summary>四角の位置と大きさを変える（Box で作った物）</summary>
    public static void SetBox(SpriteRenderer sr, Vector2 localCenter, Vector2 size)
    {
        if (sr == null) return;
        sr.transform.localPosition = localCenter;
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(Mathf.Max(0f, size.x) / Mathf.Max(0.001f, native.x), Mathf.Max(0f, size.y) / Mathf.Max(0.001f, native.y), 1f);
    }

    /// <summary>英字（Press Start 2P）。height は 0.4 マス以上</summary>
    public static TextMesh Text(Transform parent, string text, Vector2 localPos, float height, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        TextMesh tm = WorldText.Create(parent, text, Mathf.Max(0.4f, height), color, order, true, anchor);
        tm.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
        return tm;
    }

    /// <summary>点線（横向き。長さ length マス、太さ thick ドット）</summary>
    public static SpriteRenderer Dotted(Transform parent, Vector2 localCenter, float length, int thick, float angle, Color color, int order)
        => BrowserDevFx.DottedLine(parent, localCenter, length, thick, angle, color, order);

    /// <summary>当たり判定の子（箱）を作る。最初は切ってある（Activate で出す）</summary>
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

    /// <summary>判定の箱の位置と大きさを変える（AddHit で作った物）</summary>
    public static void SetHit(Hitbox hit, Vector2 localCenter, Vector2 size)
    {
        if (hit == null) return;
        hit.transform.localPosition = localCenter;
        var col = hit.GetComponent<BoxCollider2D>();
        if (col != null) col.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
    }

    /// <summary>予告の点滅（最後の 0.3秒は速く）。true＝見せる</summary>
    public static bool Blink(float left, float slow = 0.12f, float fast = 0.05f)
    {
        float step = left < 0.3f ? fast : slow;
        return left > 0.6f || Mathf.FloorToInt(Time.time / step) % 2 == 0;
    }

    /// <summary>色の濃さを変えた色</summary>
    public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>ボスの頭の上（ボスでなければ撃つ位置の少し上）</summary>
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

    /// <summary>実行時の攻撃データ（アセットを足さない。ギミックの危ない物の判定用）。json で欄を上書きする</summary>
    public static AttackData RuntimeData(string name, string json = null)
    {
        var d = ScriptableObject.CreateInstance<AttackData>();
        d.name = name;
        d.hideFlags = HideFlags.DontSave;
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, d); }
            catch (System.Exception e) { Debug.LogWarning($"{name}：攻撃データの値を入れられません（{e.Message}）"); }
        }
        return d;
    }
}
