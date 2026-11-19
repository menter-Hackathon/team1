using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// ・ボス：ワープ型（VS Code の体）。第1形態「TNT」「落ちる砂」、第2形態はその強い版＋「緑のブロック」
/// ・能力：TNT（クールタイム 7秒・ゲージ 24）
/// ・ステージの決まりは実行時のコード（Kits/Minecraft/MinecraftKit.cs・MinecraftRule.cs）
/// ・攻撃データの名前に、スキンの枠の語を入れない（bomb_blast・ability_bomb も入れない）
/// </summary>
public class MinecraftKitAssets : KitAssetBuilder
{
    public override string Label => "Minecraft";
    public override string[] AppNames => new[] { "Minecraft", "Minecraft Launcher" };
    public override string[] BundleIds => new[] { "com.mojang." };
    public override bool HoverBoss => false;
    public override string SkillName => "TNT";
    public override SkillCategory Category => SkillCategory.Game;
    public override string SkillDescription => "前にTNTを置く。少し待つと大きく爆発する。近接で押して転がせる。";
    public override string[] SkillTags => new[] { "設置", "広範囲" };

    public override AttackData[] Phase1() => new[] { Tnt(false), Sand(false) };
    public override AttackData[] Phase2() => new[] { Tnt(true), Sand(true), Creeper() };
    // 3D の物のマテリアル（Standard）を Resources に用意する（Unity のキットと同じ。無ければ作る）
    public override void AfterBuild(BossProfile boss, SkillDefinition skill) => Kit3DAssets.EnsureStandardMaterial();

    public override AttackData PlayerSkill() => TntSkill();

    AttackData Tnt(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<MinecraftTntAttack>($"Minecraft_Tnt_{tag}", so =>
        {
            so.FindProperty("_count").intValue = p2 ? 4 : 2;
            so.FindProperty("_spacing").floatValue = 2.5f;
            so.FindProperty("_fly").floatValue = 0.6f;
            so.FindProperty("_fuse").floatValue = 1.5f;
            so.FindProperty("_stagger").floatValue = p2 ? 0.4f : 0f;
            so.FindProperty("_radius").floatValue = 2.5f;
        });
        return CustomData($"Attack_Minecraft_Tnt_{tag}", custom, BossHit);
    }

    AttackData Sand(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<MinecraftSandAttack>($"Minecraft_Sand_{tag}", so =>
        {
            so.FindProperty("_columns").intValue = p2 ? 6 : 4;
            so.FindProperty("_stack").intValue = p2 ? 3 : 2;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_remain").floatValue = 6f;
        });
        return CustomData($"Attack_Minecraft_Sand_{tag}", custom, BossHit);
    }

    AttackData Creeper()
    {
        var custom = Custom<MinecraftCreeperAttack>("Minecraft_GreenBlock_P2", so =>
        {
            so.FindProperty("_count").intValue = 2;
            so.FindProperty("_smoke").floatValue = 0.8f;
            so.FindProperty("_speed").floatValue = 3.5f;
            so.FindProperty("_near").floatValue = 2f;
            so.FindProperty("_fuse").floatValue = 1f;
            so.FindProperty("_radius").floatValue = 2.5f;
            so.FindProperty("_maxWalk").floatValue = 6f;
        });
        return CustomData("Attack_Minecraft_GreenBlock_P2", custom, BossHit);
    }

    AttackData TntSkill()
    {
        var custom = Custom<MinecraftTntSkill>("Minecraft_TntSkill", so =>
        {
            so.FindProperty("_forward").floatValue = 1.5f;
            so.FindProperty("_fuse").floatValue = 1.2f;
            so.FindProperty("_radius").floatValue = 2.5f;
        });
        return CustomData("Attack_App_Minecraft_Tnt", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 7f;
            so.FindProperty("_gaugeGain").intValue = 24;
            so.FindProperty("_damage").intValue = 5;
            so.FindProperty("_knockback").vector2Value = new Vector2(8f, 6f);
            so.FindProperty("_hitStun").floatValue = 0.3f;
        });
    }

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
