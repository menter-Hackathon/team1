using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Arc のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-5）。担当D。数値はすべて【仮】。
/// 第1形態：Space の切り替え（低い帯 2本）・Little Arc（2か所）／第2形態：Space（低・高・低）・Little Arc（3か所×2発）・自動アーカイブ。
/// 能力：Zap。ステージの決まり（Space）は実行時の ArcKit が登録する。攻撃データの名前には、スキンの枠の語を入れない
/// </summary>
public class ArcKitAssets : KitAssetBuilder
{
    public override string Label => "Arc";
    public override string[] AppNames => new[] { "Arc" };
    public override string[] BundleIds => new[] { "company.thebrowser.Browser" };
    public override bool HoverBoss => true;
    public override string SkillName => "Zap";
    public override SkillCategory Category => SkillCategory.Browser;
    public override string SkillDescription => "いちばん近い敵に稲妻を落とす。まわりの敵の弾も消す。";
    public override string[] SkillTags => new[] { "自動で狙う", "弾を消す" };
    public override SkinId SkillSkin => SkinId.Browser;

    public override AttackData[] Phase1() => new[] { Space(1), LittleArc(1) };
    public override AttackData[] Phase2() => new[] { Space(2), LittleArc(2), Archive() };

    AttackData Space(int phase)
    {
        var custom = Custom<ArcSpaceAttack>($"Arc_Space_P{phase}", so =>
        {
            so.FindProperty("_phase2").boolValue = phase >= 2;
            so.FindProperty("_interval").floatValue = phase >= 2 ? 0.9f : 1.6f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_speed").floatValue = 11f;
            so.FindProperty("_width").floatValue = 1.5f;
            so.FindProperty("_lowTop").floatValue = 2f;
            so.FindProperty("_highBottom").floatValue = 1.6f;
        });
        return CustomData($"Attack_Arc_Space_P{phase}", custom, BossHit);
    }

    AttackData LittleArc(int phase)
    {
        var custom = Custom<ArcLittleArcAttack>($"Arc_LittleArc_P{phase}", so =>
        {
            so.FindProperty("_windows").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_shots").intValue = phase >= 2 ? 2 : 1;
            so.FindProperty("_shotGap").floatValue = 0.3f;
            so.FindProperty("_frameTime").floatValue = 0.8f;
            so.FindProperty("_aimTime").floatValue = 0.8f;
            so.FindProperty("_size").vector2Value = new Vector2(3f, 2.2f);
            so.FindProperty("_heights").vector2Value = new Vector2(4f, 7f);
            so.FindProperty("_stagger").floatValue = 0.25f;
        });
        return CustomData($"Attack_Arc_LittleArc_P{phase}", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_speed").floatValue = 9f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.5f, 0.5f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
        });
    }

    AttackData Archive()
    {
        var custom = Custom<ArcArchiveAttack>("Arc_Archive_P2", so =>
        {
            so.FindProperty("_tabs").intValue = 5;
            so.FindProperty("_times").intValue = 4;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_active").floatValue = 0.2f;
            so.FindProperty("_height").floatValue = 5f;
        });
        return CustomData("Attack_Arc_Archive_P2", custom, BossHit);
    }

    public override AttackData PlayerSkill()
    {
        var custom = Custom<ArcZapSkill>("Arc_Zap", so =>
        {
            so.FindProperty("_range").floatValue = 6f;
            so.FindProperty("_clearRadius").floatValue = 2f;
            so.FindProperty("_fizzle").floatValue = 4f;
            so.FindProperty("_hitSize").floatValue = 1.2f;
            so.FindProperty("_active").floatValue = 0.1f;
        });
        return CustomData("Attack_App_Arc_Zap", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 3f;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_gaugeGain").intValue = 12;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.3f, 0.6f);
        });
    }

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
