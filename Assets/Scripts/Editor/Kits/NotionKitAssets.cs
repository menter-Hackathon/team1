using UnityEngine;

/// <summary>
/// 【仮】Notion のキットのアセット（2026-11-19。docs/app-kits-1-design.md 8章。担当0）。数値はすべて【仮】。
/// ・ボス（ワープ）：P1 ブロックを積む・トグル（柱 3本）／P2 ブロックを積む（4個）・トグル（5本）・ビューの切り替え（ZoneStrike）
/// ・能力：ブロック（クールタイム 5秒・ゲージ +8）
/// 攻撃データの名前に、枠の語（breakpoint など）を入れない（Office のスキンの絵を付けず、Notion の絵に差し替える）
/// </summary>
public class NotionKitAssets : KitAssetBuilder
{
    public override string Label => NotionKit.Label;
    public override string[] AppNames => NotionKit.AppNames;
    public override string[] BundleIds => NotionKit.BundleIds;
    public override bool HoverBoss => false;
    public override string SkillName => NotionKit.SkillName;
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => NotionKit.SkillDescription;
    public override string[] SkillTags => NotionKit.SkillTags;

    public override AttackData[] Phase1() => new[] { BlockStack("P1", 3), Toggle("P1", 3) };
    public override AttackData[] Phase2() => new[] { BlockStack("P2", 4), Toggle("P2", 5), ViewSwitch() };
    public override AttackData PlayerSkill() => BlockSkill();

    // ───────── ボスの技 ─────────

    /// <summary>ブロックを積む：白いブロック（4×1）がプレイヤーの位置に 0.5秒おきに落ちる。着地後 4秒、足場</summary>
    AttackData BlockStack(string phase, int count)
    {
        AttackData block = Data($"Attack_Notion_Block_Fall_{phase}", AttackKind.Spawn, so =>
        {
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(4f, 1f);
            so.FindProperty("_fallSpeed").floatValue = 12f;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 4f;
            so.FindProperty("_becomesPlatform").boolValue = true;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<NotionBlockStack>($"NotionBlockStack_{phase}", so =>
        {
            so.FindProperty("_block").objectReferenceValue = block;
            so.FindProperty("_count").intValue = count;
            so.FindProperty("_interval").floatValue = 0.5f;
            so.FindProperty("_art").stringValue = NotionKit.ArtBlock;
        });
        return CustomData($"Attack_Notion_BlockStack_{phase}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    /// <summary>トグル：床に ▶ の行が count つ（足元と左右 3.5マスおき）。0.8秒の予告 → 中身の箱（幅 2×高さ 2.8）が 0.5秒</summary>
    AttackData Toggle(string phase, int count)
    {
        AttackData inner = Data($"Attack_Notion_Toggle_Area_{phase}", AttackKind.Area, so =>
        {
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.TargetPosition;
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(2f, 2.8f);
            so.FindProperty("_areaCount").intValue = count;
            so.FindProperty("_areaSpacing").floatValue = 3.5f;
            so.FindProperty("_areaStagger").floatValue = 0f;
            so.FindProperty("_areaForwardOrder").boolValue = false;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_activeTime").floatValue = 0.5f;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "F1F1EF");
        });
        return Dressed($"Attack_Notion_Toggle_{phase}", inner, NotionKit.ArtToggle, false);
    }

    /// <summary>ビューの切り替え（第2形態）：床を 4列に分け、左→右、右→左とカードの束が落ちる</summary>
    AttackData ViewSwitch()
    {
        var custom = Custom<NotionViewSwitch>("NotionViewSwitch_P2", so =>
        {
            so.FindProperty("_columns").intValue = 4;
            so.FindProperty("_height").floatValue = 6f;
            so.FindProperty("_tabTime").floatValue = 0.8f;
            so.FindProperty("_stagger").floatValue = 0.45f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_active").floatValue = 0.4f;
            so.FindProperty("_gap").floatValue = 0.3f;
        });
        return CustomData("Attack_Notion_ViewSwitch_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFFFFF");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>ブロック：正面 1.5マス先の床に 2×2 のブロック。出る瞬間ゲージ +8。6秒、弾を止める壁</summary>
    AttackData BlockSkill()
    {
        var custom = Custom<NotionBlockSkill>("NotionBlockSkill", so =>
        {
            so.FindProperty("_forward").floatValue = 1.5f;
            so.FindProperty("_size").vector2Value = new Vector2(2f, 2f);
            so.FindProperty("_life").floatValue = 6f;
            so.FindProperty("_hitTime").floatValue = 0.15f;
        });
        return CustomData("Attack_Notion_Skill_Block", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_gaugeGain").intValue = 8;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_knockback").vector2Value = new Vector2(4f, 4f);
        });
    }
}
