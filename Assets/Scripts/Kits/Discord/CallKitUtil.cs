using System.Collections.Generic;
using UnityEngine;

// 【仮】キット1・担当2（Discord・カレンダー・メール・Zoom）で使い回す小さい道具（2026-11-19）。
// ・足場を探す・床の高さ・プレイヤーの体・足場の上に立っているか
// ・その場で作る攻撃データ（ギミックの危ない物の数値。ボスの技の基本と同じ：ダメージ 1・ノックバック (6,5)・のけぞり 0.25）
// ・短い時間だけ出る判定（CallTimedHit）
// ほかの担当のフォルダからは使わない（名前がぶつからないように Call〜 で始める）。

/// <summary>担当2 のキットの道具</summary>
public static class CallKitUtil
{
    static readonly List<Collider2D> Hits = new List<Collider2D>();

    /// <summary>
    /// 範囲の中の足場（すり抜け床。部屋の床・落下物の足場・技が出した物は除く）。左から順。
    /// 同じ物は1つだけ
    /// </summary>
    public static List<Bounds> FindPlatforms(Rect area)
    {
        var result = new List<Bounds>();
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWay < 0) return result;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(1 << oneWay);
        Hits.Clear();
        Physics2D.OverlapArea(new Vector2(area.xMin, area.yMin + 0.5f), new Vector2(area.xMax, area.yMax), filter, Hits);
        var seen = new HashSet<Collider2D>();
        foreach (Collider2D col in Hits)
        {
            if (col == null || !col.enabled || !seen.Add(col)) continue;
            Bounds b = col.bounds;
            if (b.size.x >= area.width * 0.5f) continue;                     // 部屋の床
            if (col.GetComponentInParent<FallingObject>() != null) continue;  // 落下物の足場
            if (col.GetComponentInParent<CustomEffect>() != null) continue;   // 技が出した物
            if (col.GetComponentInParent<KitProp>() != null) continue;
            if (!area.Contains(b.center)) continue;
            result.Add(b);
        }
        Hits.Clear();
        result.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        return result;
    }

    /// <summary>x の位置の床の高さ（部屋の床・地面。すり抜け床は見ない）。無ければ範囲の下の端</summary>
    public static float FloorY(Rect area, float x)
    {
        int ground = LayerMask.GetMask("Ground");
        if (ground != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, area.yMax), Vector2.down, area.height + 2f, ground);
            if (hit.collider != null) return hit.point.y;
        }
        return area.yMin;
    }

    /// <summary>プレイヤー（無ければ null）</summary>
    public static PlayerController Player => Object.FindObjectOfType<PlayerController>();

    /// <summary>プレイヤーの体（移動用の当たり判定）の範囲</summary>
    public static bool TryPlayerBounds(out Bounds bounds)
    {
        bounds = default;
        PlayerController pc = Player;
        if (pc == null) return false;
        return TryBodyBounds(pc.transform, out bounds);
    }

    /// <summary>体（トリガーでない当たり判定）の範囲</summary>
    public static bool TryBodyBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        if (t == null) return false;
        foreach (var col in t.GetComponents<Collider2D>())
        {
            if (col.isTrigger || !col.enabled) continue;
            bounds = col.bounds;
            return true;
        }
        bounds = new Bounds(t.position + Vector3.up * 0.5f, new Vector3(0.7f, 1f, 0f));
        return true;
    }

    /// <summary>プレイヤーが、その足場の上に立っているか（足の下の端が足場の上の面の近く・横が足場の中）</summary>
    public static bool StandingOn(Bounds platform, Bounds player, bool grounded)
    {
        if (!grounded) return false;
        float feet = player.min.y;
        if (feet < platform.max.y - 0.2f || feet > platform.max.y + 0.25f) return false;
        float x = player.center.x;
        return x >= platform.min.x - 0.1f && x <= platform.max.x + 0.1f;
    }

    /// <summary>ギミックの危ない物の数値（ボスの技の基本と同じ）。名前は当たった絵の種類に使う</summary>
    public static AttackData HazardData(string name)
    {
        var d = ScriptableObject.CreateInstance<AttackData>();
        d.name = name;
        d.hideFlags = HideFlags.DontSave;
        return d;
    }

    /// <summary>
    /// 短い時間だけ出る判定を1つ出す（owner の出した物として登録）。delay 秒待ってから active 秒。
    /// circle なら size.x が直径。返した物は自分で消える
    /// </summary>
    public static CallTimedHit TimedHit(AttackRunner owner, AttackData data, Vector2 center, Vector2 size, bool circle, float delay, float active, string errorKind = null)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject($"CallHit_{data.name}", center);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        Collider2D col;
        if (circle)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = Mathf.Max(0.05f, size.x * 0.5f);
            col = c;
        }
        else
        {
            var b = go.AddComponent<BoxCollider2D>();
            b.size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
            col = b;
        }
        col.isTrigger = true;
        Hitbox hb = owner.AddHitbox(go, data, col);
        if (!string.IsNullOrEmpty(errorKind)) hb.SetErrorKind(errorKind);
        var t = go.AddComponent<CallTimedHit>();
        t.Setup(hb, delay, active);
        return t;
    }

    /// <summary>当たった相手がボス（ゲージ）なら、そのボス</summary>
    public static BossController BossOf(IDamageable target)
    {
        var c = target as Component;
        return c != null ? c.GetComponentInParent<BossController>() : null;
    }

    /// <summary>落下物の影の上に、赤い「！」を足す（影と一緒に消える）</summary>
    public static void AddExclaim(FallingObject f)
    {
        if (f == null) return;
        Transform shadow = f.transform.Find("Shadow");
        if (shadow == null) return;
        RuntimeSprites.Show(shadow, CallArt.Exclaim, new Vector2(0f, 0.55f), 17, 1f, "Exclaim");
    }

    /// <summary>色の透明度だけ変える</summary>
    public static Color Alpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}

/// <summary>短い時間だけ出る判定（CallKitUtil.TimedHit が作る）。ボスの形態変化でも消える（CustomEffect）</summary>
public class CallTimedHit : CustomEffect
{
    Hitbox _hitbox;
    float _onAt, _offAt;
    bool _on;

    /// <summary>判定</summary>
    public Hitbox Hitbox => _hitbox;

    internal void Setup(Hitbox hitbox, float delay, float active)
    {
        _hitbox = hitbox;
        _onAt = Time.time + Mathf.Max(0f, delay);
        _offAt = _onAt + Mathf.Max(0.02f, active);
        if (delay <= 0f) TurnOn();
    }

    void TurnOn()
    {
        _on = true;
        if (_hitbox != null) _hitbox.Activate();
    }

    void Update()
    {
        if (!_on && Time.time >= _onAt) TurnOn();
        if (Time.time >= _offAt)
        {
            if (_hitbox != null) _hitbox.Deactivate();
            Destroy(gameObject);
        }
    }
}
