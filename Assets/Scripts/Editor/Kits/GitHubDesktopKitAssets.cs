using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】GitHub Desktop のキットのアセット（2026-11-19。docs/app-kits-2-design.md 2-2）。担当B。
/// ・ボス（ワープ）：第1形態 diff の行・Push origin ／ 第2形態 その強い版＋Merge conflict
/// ・能力：Revert（2回押し）
/// ・ステージの決まり（マージの衝突）は実行時のコード（Kits/GitHubDesktop/GitHubDesktopKit.cs）
/// 数値はすべて【仮】。作るたびに入れ直す（コードの数値が正）。
/// </summary>
public class GitHubDesktopKitAssets : KitAssetBuilder
{
    public override string Label => GitHubDesktopKit.Label;
    public override string[] AppNames => GitHubDesktopKit.Names;
    public override string[] BundleIds => GitHubDesktopKit.Bundles;
    public override bool HoverBoss => false;

    public override string SkillName => "Revert";
    public override SkillCategory Category => SkillCategory.Dev;
    public override SkinId SkillSkin => SkinId.Dev;
    public override string SkillDescription => "1回目で今の場所を保存。2回目でそこへ戻り、通り道の敵に当たる";
    public override string[] SkillTags => new[] { "移動", "2回押し" };

    public override AttackData[] Phase1() => new[] { Diff(false), Push(false) };
    public override AttackData[] Phase2() => new[] { Diff(true), Push(true), MergeConflict() };
    public override AttackData PlayerSkill() => Revert();

    // ───────── ボスの技 ─────────

    AttackData Diff(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        var custom = Custom<GitHubDiffAttack>($"GitHub_Diff_{s}", so =>
        {
            so.FindProperty("_count").intValue = p2 ? 3 : 2;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_hitTime").floatValue = 0.15f;
            so.FindProperty("_gap").floatValue = p2 ? 0.3f : 0.35f;
            so.FindProperty("_lowerTop").floatValue = 2.2f;
            so.FindProperty("_upperBottom").floatValue = 2.8f;
            so.FindProperty("_upperTop").floatValue = 6.5f;
            so.FindProperty("_splitLast").boolValue = p2;
        });
        return CustomData($"Attack_GitHub_Diff_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData PushBullet() => Data("Attack_GitHub_PushCommit", AttackKind.Projectile, so =>
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_speed").floatValue = 8f;
        so.FindProperty("_lifetime").floatValue = 3.5f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(0.6f, 0.6f);
        so.FindProperty("_aim").intValue = (int)AimMode.Target;
        so.FindProperty("_projectileCount").intValue = 1;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_color").colorValue = UnityEngine.Color.white;
    });

    AttackData Push(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        AttackData bullet = PushBullet();
        var custom = Custom<GitHubPushAttack>($"GitHub_Push_{s}", so =>
        {
            so.FindProperty("_bullet").objectReferenceValue = bullet;
            so.FindProperty("_count").intValue = p2 ? 2 : 3;
            so.FindProperty("_interval").floatValue = 0.2f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_branch").boolValue = p2;
            so.FindProperty("_branchAt").floatValue = 2f;
            so.FindProperty("_branchAngle").floatValue = 22f;
        });
        return CustomData($"Attack_GitHub_Push_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData MergeConflict()
    {
        var custom = Custom<GitHubMergeConflictAttack>("GitHub_MergeConflict_P2", so =>
        {
            so.FindProperty("_telegraph").floatValue = 1.0f;
            so.FindProperty("_close").floatValue = 2.0f;
            so.FindProperty("_hold").floatValue = 0.5f;
            so.FindProperty("_open").floatValue = 0.5f;
            so.FindProperty("_safeWidth").floatValue = 3f;
        });
        return CustomData("Attack_GitHub_MergeConflict_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_lingering").boolValue = true;
        });
    }

    // ───────── 能力 ─────────

    AttackData Revert()
    {
        var custom = Custom<GitHubRevertAttack>("GitHub_Revert", so =>
        {
            so.FindProperty("_keep").floatValue = 6f;
            so.FindProperty("_width").floatValue = 1.2f;
            so.FindProperty("_hitTime").floatValue = 0.15f;
            so.FindProperty("_invincible").floatValue = 0.3f;
        });
        return CustomData("Attack_App_GitHubDesktop_Revert", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_gaugeGain").intValue = 16;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_knockback").vector2Value = new Vector2(5f, 4f);
            so.FindProperty("_hitStun").floatValue = 0.2f;
        });
    }
}
