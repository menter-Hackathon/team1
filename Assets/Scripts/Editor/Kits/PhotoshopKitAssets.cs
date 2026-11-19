using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Photoshop のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-1）。担当D。数値はすべて【仮】。
/// 第1形態：なげなわ選択（1回）・ブラシ（谷か山）／第2形態：なげなわ（3回。最後は大きい）・ブラシ（谷→山）・コピースタンプ（分身）。
/// 能力：コピースタンプ（自分の分身を置く）。ステージの決まり（レイヤーの目）は実行時の PhotoshopKit が登録する。
/// 攻撃データの名前には、スキンの枠の語を入れない
/// </summary>
public class PhotoshopKitAssets : KitAssetBuilder
{
    public override string Label => "Photoshop";
    public override string[] AppNames => new[] { "Photoshop", "Adobe Photoshop" };
    public override string[] BundleIds => new[] { "com.adobe.Photoshop" };
    public override bool HoverBoss => true;
    public override string SkillName => "コピースタンプ";
    public override SkillCategory Category => SkillCategory.Creative;
    public override string SkillDescription => "その場に自分の分身を置く。分身は4秒、前をくり返し切りつける。";
    public override string[] SkillTags => new[] { "設置", "持続" };
    public override SkinId SkillSkin => SkinId.Creative;

    public override AttackData[] Phase1() => new[] { Lasso(1), Brush(1) };
    public override AttackData[] Phase2() => new[] { Lasso(2), Brush(2), Clone() };

    AttackData Lasso(int phase)
    {
        var custom = Custom<PhotoshopLassoAttack>($"Photoshop_Lasso_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 3 : 1;
            so.FindProperty("_interval").floatValue = 0.6f;
            so.FindProperty("_size").vector2Value = new Vector2(4.5f, 3.5f);
            so.FindProperty("_lastSize").vector2Value = new Vector2(7f, 3.5f);
            so.FindProperty("_appear").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_active").floatValue = 0.15f;
            so.FindProperty("_linger").floatValue = 0.4f;
        });
        return CustomData($"Attack_Photoshop_Lasso_P{phase}", custom, BossHit);
    }

    AttackData Brush(int phase)
    {
        var custom = Custom<PhotoshopBrushAttack>($"Photoshop_Brush_P{phase}", so =>
        {
            so.FindProperty("_both").boolValue = phase >= 2;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_drawTime").floatValue = 0.5f;
            so.FindProperty("_width").floatValue = 1.4f;
            so.FindProperty("_active").floatValue = 0.12f;
            so.FindProperty("_linger").floatValue = 0.8f;
            so.FindProperty("_gap").floatValue = 0.7f;
        });
        return CustomData($"Attack_Photoshop_Brush_P{phase}", custom, BossHit);
    }

    AttackData Clone()
    {
        var custom = Custom<PhotoshopCloneAttack>("Photoshop_Clone_P2", so =>
        {
            so.FindProperty("_count").intValue = 3;
            so.FindProperty("_reticle").floatValue = 0.6f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_walkSpeed").floatValue = 3.5f;
            so.FindProperty("_life").floatValue = 3f;
            so.FindProperty("_minDistance").floatValue = 4f;
        });
        return CustomData("Attack_Photoshop_Clone_P2", custom, BossHit);
    }

    public override AttackData PlayerSkill()
    {
        var custom = Custom<PhotoshopStampSkill>("Photoshop_Stamp", so =>
        {
            so.FindProperty("_life").floatValue = 4f;
            so.FindProperty("_swingInterval").floatValue = 0.6f;
            so.FindProperty("_firstSwing").floatValue = 0.25f;
            so.FindProperty("_swingSize").vector2Value = new Vector2(1.8f, 1.4f);
            so.FindProperty("_swingForward").floatValue = 0.95f;
            so.FindProperty("_swingActive").floatValue = 0.12f;
        });
        return CustomData("Attack_App_Photoshop_Stamp", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 7f;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_gaugeGain").intValue = 3;   // 1回 3 × 最大 6回
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
        });
    }

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
