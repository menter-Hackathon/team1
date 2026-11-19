using UnityEngine;

/// <summary>
/// 【仮】Xcode のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-2）。担当B。
/// ・ボス（ワープ）：第1形態 Build（ハンマー）・Simulator ／ 第2形態 その強い版＋999+ warnings
/// ・能力：ハンマー
/// ・ステージの決まり（Build）は実行時のコード（Kits/Xcode/XcodeKit.cs）
/// 数値はすべて【仮】。作るたびに入れ直す（コードの数値が正）。
/// </summary>
public class XcodeKitAssets : KitAssetBuilder
{
    public override string Label => XcodeKit.Label;
    public override string[] AppNames => XcodeKit.Names;
    public override string[] BundleIds => XcodeKit.Bundles;
    public override bool HoverBoss => false;

    public override string SkillName => "ハンマー";
    public override SkillCategory Category => SkillCategory.Dev;
    public override SkinId SkillSkin => SkinId.Dev;
    public override string SkillDescription => "少しためて、正面に大きいハンマーをふり下ろす。とどめは Build Succeeded";
    public override string[] SkillTags => new[] { "近い", "ため" };

    public override AttackData[] Phase1() => new[] { Hammer(false), Simulator(false) };
    public override AttackData[] Phase2() => new[] { Hammer(true), Simulator(true), Warnings() };
    public override AttackData PlayerSkill() => HammerSkill();

    // ───────── ボスの技 ─────────

    AttackData Shock() => Data("Attack_Xcode_HammerShock", AttackKind.Projectile, so =>
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_speed").floatValue = 8f;
        so.FindProperty("_lifetime").floatValue = 3f / 8f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(0.6f, 0.8f);
        so.FindProperty("_aim").intValue = (int)AimMode.Forward;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_color").colorValue = UnityEngine.Color.white;
    });

    AttackData Hammer(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        AttackData shock = Shock();
        var custom = Custom<XcodeHammerAttack>($"Xcode_Hammer_{s}", so =>
        {
            so.FindProperty("_shock").objectReferenceValue = shock;
            so.FindProperty("_count").intValue = p2 ? 3 : 1;
            so.FindProperty("_interval").floatValue = 1.5f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_width").floatValue = 3f;
        });
        return CustomData($"Attack_Xcode_Hammer_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData Notification() => Data("Attack_Xcode_SimNotification", AttackKind.Projectile, so =>
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_speed").floatValue = 8f;
        so.FindProperty("_lifetime").floatValue = 3.5f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(0.8f, 0.5f);
        so.FindProperty("_aim").intValue = (int)AimMode.Target;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_color").colorValue = UnityEngine.Color.white;
    });

    AttackData Simulator(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        AttackData bullet = Notification();
        var custom = Custom<XcodeSimulatorAttack>($"Xcode_Simulator_{s}", so =>
        {
            so.FindProperty("_bullet").objectReferenceValue = bullet;
            so.FindProperty("_phones").intValue = p2 ? 3 : 2;
            so.FindProperty("_shots").intValue = p2 ? 2 : 1;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_aimTime").floatValue = 0.8f;
            so.FindProperty("_minDistance").floatValue = 3f;
            so.FindProperty("_size").vector2Value = new Vector2(2f, 4f);
        });
        return CustomData($"Attack_Xcode_Simulator_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData Warnings()
    {
        var custom = Custom<XcodeWarningsAttack>("Xcode_Warnings_P2", so =>
        {
            so.FindProperty("_waves").intValue = 3;
            so.FindProperty("_interval").floatValue = 1.9f;
            so.FindProperty("_telegraph").floatValue = 1.0f;
            so.FindProperty("_gapWidth").floatValue = 2.5f;
            so.FindProperty("_near").floatValue = 4.5f;
            so.FindProperty("_column").floatValue = 1f;
            so.FindProperty("_fallSpeed").floatValue = 18f;
        });
        return CustomData("Attack_Xcode_Warnings_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_lingering").boolValue = true;
        });
    }

    // ───────── 能力 ─────────

    AttackData HammerSkill()
    {
        var custom = Custom<XcodeHammerSkill>("Xcode_HammerSkill", so =>
        {
            so.FindProperty("_charge").floatValue = 0.35f;
            so.FindProperty("_size").vector2Value = new Vector2(3f, 3f);
            so.FindProperty("_hitTime").floatValue = 0.15f;
        });
        return CustomData("Attack_App_Xcode_Hammer", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_gaugeGain").intValue = 20;
            so.FindProperty("_damage").intValue = 4;
            so.FindProperty("_knockback").vector2Value = new Vector2(8f, 6f);
            so.FindProperty("_hitStun").floatValue = 0.3f;
        });
    }
}
