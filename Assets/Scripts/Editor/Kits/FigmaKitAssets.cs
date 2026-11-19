using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Figma のキットのアセット（2026-11-19。docs/app-kits-2-design.md 2-4）。担当D。数値はすべて【仮】。
/// 第1形態：共同編集のカーソル（3つ）・Frame（1つ）／第2形態：カーソル（5つ＋Client の枠）・Frame の Auto layout（3つ）・⌘D 複製。
/// 能力：ペンツール。ステージの決まり（共同編集のカーソルが四角を置く）は実行時の FigmaKit が登録する。
/// 攻撃データの名前には、スキンの枠の語を入れない（絵はキットが自分で描く）
/// </summary>
public class FigmaKitAssets : KitAssetBuilder
{
    public override string Label => "Figma";
    public override string[] AppNames => new[] { "Figma" };
    public override string[] BundleIds => new[] { "com.figma.Desktop" };
    public override bool HoverBoss => true;
    public override string SkillName => "ペンツール";
    public override SkillCategory Category => SkillCategory.Creative;
    public override string SkillDescription => "近くの敵を3体まで、ペンツールの線で順に結んで切る。";
    public override string[] SkillTags => new[] { "自動で狙う", "連続" };
    public override SkinId SkillSkin => SkinId.Creative;

    public override AttackData[] Phase1() => new[] { Cursor(1), Frame(1) };
    public override AttackData[] Phase2() => new[] { Cursor(2), Frame(2), Duplicate() };

    AttackData Cursor(int phase)
    {
        var custom = Custom<FigmaCursorAttack>($"Figma_Cursor_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 5 : 3;
            so.FindProperty("_enterTime").floatValue = 0.4f;
            so.FindProperty("_aimTime").floatValue = 0.8f;
            so.FindProperty("_interval").floatValue = phase >= 2 ? 0.75f : 0.9f;
            so.FindProperty("_rushSpeed").floatValue = 14f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.8f, 0.8f);
            so.FindProperty("_lastFrame").boolValue = phase >= 2;
            so.FindProperty("_frameSize").vector2Value = new Vector2(3f, 3f);
            so.FindProperty("_frameTelegraph").floatValue = 0.8f;
            so.FindProperty("_frameActive").floatValue = 0.15f;
        });
        return CustomData($"Attack_Figma_Cursor_P{phase}", custom, BossHit);
    }

    AttackData Frame(int phase)
    {
        var custom = Custom<FigmaFrameAttack>($"Figma_Frame_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 3 : 1;
            so.FindProperty("_size").vector2Value = new Vector2(4f, 3f);
            so.FindProperty("_gap").floatValue = 2.5f;
            so.FindProperty("_appear").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_active").floatValue = 0.15f;
        });
        return CustomData($"Attack_Figma_Frame_P{phase}", custom, BossHit);
    }

    AttackData Duplicate()
    {
        var custom = Custom<FigmaDuplicateAttack>("Figma_Duplicate_P2", so =>
        {
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_splitInterval").floatValue = 0.5f;
            so.FindProperty("_splits").intValue = 2;
            so.FindProperty("_fan1").floatValue = 20f;
            so.FindProperty("_fan2").floatValue = 40f;
        });
        return CustomData("Attack_Figma_Duplicate_P2", custom, so =>
        {
            BossHit(so);
            // 弾の欄（ひし形）
            so.FindProperty("_speed").floatValue = 6f;
            so.FindProperty("_lifetime").floatValue = 4f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.6f, 0.6f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            Color(so, "_color", "FFFFFF");
        });
    }

    public override AttackData PlayerSkill()
    {
        var custom = Custom<FigmaPenToolAttack>("Figma_PenTool", so =>
        {
            so.FindProperty("_maxTargets").intValue = 3;
            so.FindProperty("_range").floatValue = 12f;
            so.FindProperty("_straight").floatValue = 6f;
            so.FindProperty("_width").floatValue = 0.5f;
            so.FindProperty("_drawTime").floatValue = 0.12f;
            so.FindProperty("_active").floatValue = 0.1f;
        });
        return CustomData("Attack_App_Figma_PenTool", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_damage").intValue = 3;      // 雑魚は1回で倒れる
            so.FindProperty("_gaugeGain").intValue = 14;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.3f, 0.55f);
        });
    }

    /// <summary>ボスの技の共通の欄（ライフ1・ノックバック (6,5)・のけぞり 0.25）</summary>
    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
