using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Illustrator のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-4）。担当D。数値はすべて【仮】。
/// 第1形態：ペンツール（1本）・スターツール（3か所）／第2形態：ペンツール（2本）・スターツール（5か所＋星の先の弾）・アートボード（場に残る技）。
/// 能力：スターツール。ステージの決まり（拡大・縮小ツール）は実行時の IllustratorKit が登録する。
/// 攻撃データの名前には、スキンの枠の語を入れない
/// </summary>
public class IllustratorKitAssets : KitAssetBuilder
{
    public override string Label => "Illustrator";
    public override string[] AppNames => new[] { "Illustrator", "Adobe Illustrator" };
    public override string[] BundleIds => new[] { "com.adobe.illustrator" };
    public override bool HoverBoss => true;
    public override string SkillName => "スターツール";
    public override SkillCategory Category => SkillCategory.Creative;
    public override string SkillDescription => "前に星を描いて切る。星の5つの先が、小さい弾になって飛ぶ。";
    public override string[] SkillTags => new[] { "範囲", "拡散" };
    public override SkinId SkillSkin => SkinId.Creative;

    public override AttackData[] Phase1() => new[] { Pen(1), Shape(1) };
    public override AttackData[] Phase2() => new[] { Pen(2), Shape(2), Artboard() };

    AttackData Pen(int phase)
    {
        var custom = Custom<IllustratorPenAttack>($"Illustrator_Pen_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 2 : 1;
            so.FindProperty("_nextDelay").floatValue = 0.5f;
            so.FindProperty("_anchorTime").floatValue = 0.2f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_width").floatValue = 1.2f;
            so.FindProperty("_active").floatValue = 0.3f;
            so.FindProperty("_midHeights").vector2Value = new Vector2(0.6f, 4f);
            so.FindProperty("_wallHeights").vector2Value = new Vector2(0.5f, 5f);
        });
        return CustomData($"Attack_Illustrator_Pen_P{phase}", custom, BossHit);
    }

    AttackData Shape(int phase)
    {
        var custom = Custom<IllustratorShapeAttack>($"Illustrator_Shape_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 5 : 3;
            so.FindProperty("_interval").floatValue = 0.4f;
            so.FindProperty("_radius").floatValue = 2.2f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_active").floatValue = 0.15f;
            so.FindProperty("_tipShots").boolValue = phase >= 2;
        });
        return CustomData($"Attack_Illustrator_Shape_P{phase}", custom, so =>
        {
            BossHit(so);
            // 星の先の弾（第2形態）
            so.FindProperty("_speed").floatValue = 6f;
            so.FindProperty("_lifetime").floatValue = 2.5f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.4f, 0.4f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
        });
    }

    AttackData Artboard()
    {
        var custom = Custom<IllustratorArtboardAttack>("Illustrator_Artboard_P2", so =>
        {
            so.FindProperty("_halfWidth").floatValue = 6f;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_duration").floatValue = 4f;
            so.FindProperty("_tick").floatValue = 0.5f;
        });
        return CustomData("Attack_Illustrator_Artboard_P2", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = true; // 場に残る技
        });
    }

    public override AttackData PlayerSkill()
    {
        AttackData tip = Data("Attack_App_Illustrator_StarTip", AttackKind.Projectile, so => Tip(so, 3));
        AttackData tipNoGauge = Data("Attack_App_Illustrator_StarTipZako", AttackKind.Projectile, so => Tip(so, 0));
        var custom = Custom<IllustratorStarSkill>("Illustrator_Star", so =>
        {
            so.FindProperty("_forward").floatValue = 2f;
            so.FindProperty("_radius").floatValue = 2f;
            so.FindProperty("_active").floatValue = 0.12f;
            so.FindProperty("_tip").objectReferenceValue = tip;
            so.FindProperty("_tipNoGauge").objectReferenceValue = tipNoGauge;
        });
        return CustomData("Attack_App_Illustrator_Star", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 3.5f;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_gaugeGain").intValue = 12;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 3f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
        });
    }

    /// <summary>星の先の弾（プレイヤーの弾）</summary>
    static void Tip(SerializedObject so, int gauge)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_gaugeGain").intValue = gauge;
        so.FindProperty("_knockback").vector2Value = new Vector2(2f, 1.5f);
        so.FindProperty("_hitStun").floatValue = 0.1f;
        so.FindProperty("_speed").floatValue = 10f;
        so.FindProperty("_lifetime").floatValue = 0.5f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(0.35f, 0.35f);
        so.FindProperty("_gravity").floatValue = 0f;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        Color(so, "_color", "FFFFFF");
    }

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
