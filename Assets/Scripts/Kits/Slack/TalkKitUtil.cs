using System.Collections.Generic;
using UnityEngine;

// 【仮】キット1・担当1（Slack・LINE・メッセージ・Teams）で使い回す小さい道具（2026-11-19）。
// ・床の高さ・足場・プレイヤーの体・ボスの体
// ・その場で作る攻撃データ（ギミックの危ない物。アセットを持てない StageRule 用。ボスの技の基本：ダメージ 1・ノックバック (6,5)・のけぞり 0.25）
// ・判定を作る（AddTrigger）
// 名前がぶつからないように Talk〜 で始める。ほかの担当のフォルダからは使わない。

/// <summary>担当1 のキットの道具</summary>
public static class TalkKitUtil
{
    static readonly List<Collider2D> Hits = new List<Collider2D>();
    static readonly Dictionary<string, AttackData> Made = new Dictionary<string, AttackData>();

    /// <summary>
    /// その場で攻撃データを作る（同じ名前は使い回す）。json は AttackData の欄（"_damage":1 など）。
    /// 名前に枠の語（breakpoint など）を入れない（スキンの絵が付かないように）
    /// </summary>
    public static AttackData RuntimeData(string name, string json = null)
    {
        if (Made.TryGetValue(name, out AttackData d) && d != null) return d;
        d = ScriptableObject.CreateInstance<AttackData>();
        d.name = name;
        d.hideFlags = HideFlags.DontSave;
        try { if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, d); }
        catch (System.Exception e) { Debug.LogException(e); }
        Made[name] = d;
        return d;
    }

    /// <summary>トリガーの判定を付ける（Kinematic の Rigidbody2D も）</summary>
    public static T AddTrigger<T>(GameObject go) where T : Collider2D
    {
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }
        var col = go.AddComponent<T>();
        col.isTrigger = true;
        return col;
    }

    /// <summary>x の位置の床の高さ（Ground。すり抜け床は見ない）。fromY から下を探す。無ければ部屋の床</summary>
    public static float FloorY(float x, float fromY)
    {
        int ground = LayerMask.GetMask("Ground");
        if (ground != 0)
        {
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(ground);
            var hits = new RaycastHit2D[4];
            int n = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, filter, hits, 60f);
            for (int i = 0; i < n; i++) if (hits[i].distance > 0.001f) return hits[i].point.y;
        }
        return BattleArena.GetBounds().yMin;
    }

    /// <summary>x の位置で、fromY から下にある「乗れる面」（床かすり抜け床）の高さ。無ければ床</summary>
    public static float StandY(float x, float fromY)
    {
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(mask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, filter, hits, 60f);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0.001f) continue;
            if (hits[i].collider.GetComponentInParent<KitProp>() != null) continue;
            return hits[i].point.y;
        }
        return FloorY(x, fromY);
    }

    /// <summary>範囲の中の足場（すり抜け床。部屋の床・落下物・技の物・置物は除く）。左から順</summary>
    public static List<Bounds> Platforms(Rect area)
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
            if (b.size.x >= area.width * 0.5f) continue;
            if (col.GetComponentInParent<FallingObject>() != null) continue;
            if (col.GetComponentInParent<CustomEffect>() != null) continue;
            if (col.GetComponentInParent<KitProp>() != null) continue;
            if (!area.Contains(b.center)) continue;
            result.Add(b);
        }
        Hits.Clear();
        result.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        return result;
    }

    /// <summary>プレイヤーの体（移動用の当たり判定）。いなければ false</summary>
    public static bool PlayerBody(Transform player, out Bounds b)
    {
        b = default;
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        foreach (var col in player.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) { b = col.bounds; return true; }
        }
        b = new Bounds(player.position + Vector3.up * 0.5f, new Vector3(0.7f, 1f, 0f));
        return true;
    }

    /// <summary>攻撃する人の体の範囲（ボスなら被弾範囲。無ければ位置のまわり）</summary>
    public static Bounds OwnerBody(AttackRunner owner)
    {
        if (owner == null) return new Bounds(Vector3.zero, Vector3.one);
        if (owner.TryGetComponent(out BossController boss)) return boss.BodyBounds;
        foreach (var col in owner.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds;
        }
        return new Bounds(owner.OriginPoint, new Vector3(1f, 1f, 0f));
    }

    /// <summary>今の画面（ボス部屋にいれば部屋、ほかはカメラ）</summary>
    public static Rect AreaFor(Transform target)
    {
        Rect arena = BattleArena.GetBounds();
        if (target == null || arena.Contains(target.position)) return arena;
        return ScreenUi.ViewRect();
    }

    /// <summary>攻撃する人が、この技を続けているか（形態変化・正常化・止められたら false）</summary>
    public static bool StillRunning(AttackRunner owner, AttackData data)
        => owner != null && owner.isActiveAndEnabled && (data == null || owner.Current == data);
}
