using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Blender のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// ・ボス：浮く型（Chrome の体）。第1形態「G（グラブ）」「S（スケール）」、第2形態はその強い版＋「Suzanne」
/// ・能力：S スケール（クールタイム 4.5秒・ゲージ 16）
/// ・ステージの決まりは実行時のコード（Kits/Blender/BlenderKit.cs・BlenderRule.cs）
/// ・攻撃データの名前に、スキンの枠の語を入れない（3D の物は 3D のまま）
/// </summary>
public class BlenderKitAssets : KitAssetBuilder
{
    public override string Label => "Blender";
    public override string[] AppNames => new[] { "Blender" };
    public override string[] BundleIds => new[] { "org.blenderfoundation." };
    public override bool HoverBoss => true;
    public override string SkillName => "Sスケール";
    public override SkillCategory Category => SkillCategory.Creative;
    public override string SkillDescription => "前に3Dの立方体を置き、一瞬で大きく広げて敵を押しのける。";
    public override string[] SkillTags => new[] { "範囲", "押しのけ" };

    public override AttackData[] Phase1() => new[] { Grab(false), Scale(false) };
    public override AttackData[] Phase2() => new[] { Grab(true), Scale(true), Suzanne() };
    // 3D の物のマテリアル（Standard）を Resources に用意する（Unity のキットと同じ。無ければ作る）
    public override void AfterBuild(BossProfile boss, SkillDefinition skill) => Kit3DAssets.EnsureStandardMaterial();

    public override AttackData PlayerSkill() => ScaleSkill();

    // 技1：G（グラブ）
    AttackData Grab(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<BlenderGrabAttack>($"Blender_Grab_{tag}", so =>
        {
            so.FindProperty("_size").floatValue = 1.5f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_speed").floatValue = 14f;
            so.FindProperty("_thenDrop").boolValue = p2;
            so.FindProperty("_dropSpeed").floatValue = 18f;
        });
        return CustomData($"Attack_Blender_Grab_{tag}", custom, BossHit);
    }

    // 技2：S（スケール）。第2形態は、縮むときに 4つのかけら
    AttackData Scale(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        AttackData shard = p2 ? Data("Attack_Blender_ScaleShard_P2", AttackKind.Projectile, so =>
        {
            BossHit(so);
            so.FindProperty("_speed").floatValue = 7f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.9f, 0.9f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_windup").floatValue = 0f;
        }) : null;
        var custom = Custom<BlenderScaleAttack>($"Blender_Scale_{tag}", so =>
        {
            so.FindProperty("_small").floatValue = 0.5f;
            so.FindProperty("_big").floatValue = 5f;
            so.FindProperty("_telegraph").floatValue = 1f;
            so.FindProperty("_grow").floatValue = 0.2f;
            so.FindProperty("_shrink").floatValue = 0.5f;
            so.FindProperty("_shard").objectReferenceValue = shard;
        });
        return CustomData($"Attack_Blender_Scale_{tag}", custom, BossHit);
    }

    // 技3（第2形態）：Suzanne
    AttackData Suzanne()
    {
        var custom = Custom<BlenderSuzanneAttack>("Blender_Suzanne_P2", so =>
        {
            so.FindProperty("_diameter").floatValue = 2.5f;
            so.FindProperty("_bounces").intValue = 3;
            so.FindProperty("_step").floatValue = 4f;
            so.FindProperty("_hop").floatValue = 0.9f;
            so.FindProperty("_apex").floatValue = 4f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
        });
        return CustomData("Attack_Blender_Suzanne_P2", custom, BossHit);
    }

    // 能力：S スケール
    AttackData ScaleSkill()
    {
        var custom = Custom<BlenderScaleSkill>("Blender_ScaleSkill", so =>
        {
            so.FindProperty("_forward").floatValue = 2f;
            so.FindProperty("_size").floatValue = 3f;
            so.FindProperty("_grow").floatValue = 0.15f;
        });
        return CustomData("Attack_App_Blender_ScaleSkill", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4.5f;
            so.FindProperty("_gaugeGain").intValue = 16;
            so.FindProperty("_damage").intValue = 3;
            so.FindProperty("_knockback").vector2Value = new Vector2(9f, 5f); // 雑魚を外へ押す
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
