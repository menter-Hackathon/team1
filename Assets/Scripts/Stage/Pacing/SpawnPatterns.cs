using System;
using System.Reflection;
using UnityEngine;

// 【仮】雑魚の出し方（組・群れ・はさみうち）の場所を決める（2026-11-19。docs/stage-pacing-design.md 2-2・5章）。担当1（流れ）の物。

/// <summary>雑魚を出す場所の道具（床の上・プレイヤーから離す・ボス部屋に入れない）</summary>
public static class SpawnPatterns
{
    /// <summary>プレイヤーから遠い方の端（右 1／左 -1）</summary>
    public static int FarSide(Rect view, float playerX) => playerX <= view.center.x ? 1 : -1;

    /// <summary>端（side：右 1／左 -1）の近くに、プレイヤーが来ているか（その端からは出せない）</summary>
    public static bool SideBlocked(Rect view, int side, float playerX, float minDistance)
    {
        float edge = side > 0 ? view.xMax - StagePaceTable.EdgeInset : view.xMin + StagePaceTable.EdgeInset;
        return Mathf.Abs(edge - playerX) < minDistance + 1.5f;
    }

    /// <summary>
    /// 列の1体分の足元。端（side）から内側へ、slot 番目（0.8マスずつ）。穴・壁の中・近すぎるときは、内側へ少しずつずらす
    /// </summary>
    public static bool TryLineSlot(Rect view, int side, int slot, ZakoSpawner spawner, Vector2 player, out Vector2 feet)
    {
        float edge = side > 0 ? view.xMax - StagePaceTable.EdgeInset : view.xMin + StagePaceTable.EdgeInset;
        float x = edge - side * slot * StagePaceTable.LineSpacing;
        for (int i = 0; i < 6; i++)
        {
            if (TryFeetAt(x - side * i * 0.5f, view, spawner, player, out feet)) return true;
        }
        feet = default;
        return false;
    }

    /// <summary>範囲の中の、どこか（今までの出し方と同じ決め方）</summary>
    public static bool TryRandom(Rect view, ZakoSpawner spawner, Vector2 player, out Vector2 feet)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float x = UnityEngine.Random.Range(view.xMin + 1f, view.xMax - 1f);
            if (TryFeetAt(x, view, spawner, player, out feet)) return true;
        }
        feet = default;
        return false;
    }

    /// <summary>x の真下の床の上（範囲の上から下へ調べる）。出せない場所なら false</summary>
    public static bool TryFeetAt(float x, Rect view, ZakoSpawner spawner, Vector2 player, out Vector2 feet)
    {
        feet = default;
        if (spawner == null || x < view.xMin + 0.5f || x > view.xMax - 0.5f) return false;
        float minDistance = spawner.MinDistance;
        if (Mathf.Abs(x - player.x) < minDistance) return false;
        var from = new Vector2(x, view.yMax - 0.5f);
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, view.height, spawner.GroundMask);
        if (hit.collider == null || hit.distance < 0.2f) return false; // 床が無い（穴）・壁の中
        feet = hit.point;
        if (spawner.AvoidBossRoom && BattleArena.Current != null && BattleArena.Current.Bounds.Contains(feet + Vector2.up * 0.5f)) return false;
        return Vector2.Distance(feet, player) >= minDistance;
    }
}

/// <summary>
/// 新しい雑魚のひな形の番号（担当3 の ZakoVariants.Small・Elite・Flyer・Charger）を読む。
/// ZakoVariants がまだ無くてもコンパイルが通るように、名前で探す。無い・-1 なら 0（歩く雑魚）で代わりに出す【仮】
/// </summary>
public static class PaceVariants
{
    static bool _looked;
    static Type _type;

    /// <summary>ひな形の番号（無ければ -1）。担当3 が StagePacer.Started の中で入れるので、出す直前に読む</summary>
    public static int Get(string name)
    {
        if (!_looked)
        {
            _looked = true;
            _type = Type.GetType("ZakoVariants");
        }
        if (_type == null) return -1;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        try
        {
            FieldInfo f = _type.GetField(name, flags);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(null);
            PropertyInfo p = _type.GetProperty(name, flags);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(null);
        }
        catch (Exception) { /* 読めなければ、歩く雑魚で代わりに出す */ }
        return -1;
    }
}
