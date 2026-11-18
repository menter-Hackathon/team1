using UnityEngine;

/// <summary>
/// 【仮】エフェクトアイコンの効果（爆発・落雷・もや・着地した所の火）を出す道具（2026-11-18。docs/app-boss-skills-design.md 3章）。
/// 判定は今ある「指定位置の判定」（AreaStrike）をそのまま使い、その子に絵（PremiereBurst など）を付ける。
/// 絵は判定と一緒に消える。判定の攻撃データの色は透明にしておき、絵はこちらで出す（着地した所の火だけは、判定の色も残す）
/// </summary>
public static class PremiereFx
{
    /// <summary>
    /// 爆発（円）。center が円の真ん中。攻撃データは Area・円（大きさの x が直径）・出す場所は TargetPosition・床に置かない・予告 0
    /// </summary>
    public static AreaStrike Explosion(AttackRunner owner, AttackData data, Vector2 center)
    {
        if (owner == null || data == null) return null;
        AreaStrike strike = AreaStrike.Spawn(owner, data, center, Vector2.up, 0f);
        PremiereBurst.Attach(strike.transform, data.AreaSize.x, Mathf.Max(0.2f, data.HitWindow));
        return strike;
    }

    /// <summary>
    /// 落雷（縦の柱）。point の真下の床から立つ（攻撃データは Area・箱・出す場所は TargetPosition・床の上に置く・予告 0。
    /// 高さが部屋の高さ以上なら、部屋の床から画面の上まで）
    /// </summary>
    public static AreaStrike Bolt(AttackRunner owner, AttackData data, Vector2 point)
    {
        if (owner == null || data == null) return null;
        AreaStrike strike = AreaStrike.Spawn(owner, data, point, Vector2.up, 0f);
        PremiereBolt.Attach(strike.transform, data.AreaSize.x, data.AreaSize.y, Mathf.Max(0.2f, data.HitWindow));
        return strike;
    }

    /// <summary>もや（箱の持続）。center が箱の真ん中。攻撃データは Area・箱・床に置かない・持続あり・予告 0</summary>
    public static AreaStrike Fog(AttackRunner owner, AttackData data, Vector2 center)
    {
        if (owner == null || data == null) return null;
        AreaStrike strike = AreaStrike.Spawn(owner, data, center, Vector2.up, 0f);
        PremiereFog.Attach(strike.transform, data.AreaSize, Mathf.Max(0.2f, data.HitWindow));
        return strike;
    }

    /// <summary>着地した所の火（箱の持続）。groundPoint の床の上に置く。攻撃データは Area・箱・床の上に置く・持続あり・予告 0</summary>
    public static AreaStrike FireFloor(AttackRunner owner, AttackData data, Vector2 groundPoint)
    {
        if (owner == null || data == null) return null;
        AreaStrike strike = AreaStrike.Spawn(owner, data, groundPoint, Vector2.up, 0f);
        PremiereFlames.Attach(strike.transform, data.AreaSize);
        return strike;
    }

    /// <summary>
    /// アイコンの効果を出す（能力のエフェクトアイコン投げ）。point は当たった所、normal は当たった面の向き（相手に当たったときは 0）。
    /// 炎は当たった所から少し面の外側を真ん中に、もやは面の上に下の端を付ける。雷は当たった所の真下の床から立つ
    /// </summary>
    public static AreaStrike IconEffect(AttackRunner owner, AttackData data, PremiereIcon kind, Vector2 point, Vector2 normal)
    {
        if (owner == null || data == null) return null;
        Vector2 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.zero;
        switch (kind)
        {
            case PremiereIcon.Fire:
                return Explosion(owner, data, point + n * 0.5f); // 【仮】ボムの爆発と同じく、半マス外側
            case PremiereIcon.Thunder:
                return Bolt(owner, data, point);
            default:
                return Fog(owner, data, point + n * (data.AreaSize.y * 0.5f));
        }
    }
}
