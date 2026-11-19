using UnityEngine;

/// <summary>
/// 【仮】Safari のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-4）。担当B。
/// ・ボス（浮く）：第1形態 コンパス・タブ一覧 ／ 第2形態 その強い版＋プライベートブラウズ
/// ・能力：リーダー表示（敵の弾を消す。身を守る能力）
/// ・ステージの決まり（トラッカーをブロック）は実行時のコード（Kits/Safari/SafariKit.cs）
/// 数値はすべて【仮】。作るたびに入れ直す（コードの数値が正）。
/// </summary>
public class SafariKitAssets : KitAssetBuilder
{
    public override string Label => SafariKit.Label;
    public override string[] AppNames => SafariKit.Names;
    public override string[] BundleIds => SafariKit.Bundles;
    public override bool HoverBoss => true;

    public override string SkillName => "リーダー表示";
    public override SkillCategory Category => SkillCategory.Browser;
    public override SkinId SkillSkin => SkinId.Browser;
    public override string SkillDescription => "画面の敵の弾を全部消す。まわりの敵にも当たる";
    public override string[] SkillTags => new[] { "守る", "まわり" };

    public override AttackData[] Phase1() => new[] { Compass(false), TabGrid(false) };
    public override AttackData[] Phase2() => new[] { Compass(true), TabGrid(true), Private() };
    public override AttackData PlayerSkill() => Reader();

    // ───────── ボスの技 ─────────

    AttackData Needle() => Data("Attack_Safari_CompassNeedle", AttackKind.Projectile, so =>
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_speed").floatValue = 18f;
        so.FindProperty("_lifetime").floatValue = 2f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(1.1f, 0.35f);
        so.FindProperty("_aim").intValue = (int)AimMode.Target;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_color").colorValue = UnityEngine.Color.white;
    });

    AttackData Compass(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        AttackData needle = Needle();
        var custom = Custom<SafariCompassAttack>($"Safari_Compass_{s}", so =>
        {
            so.FindProperty("_bullet").objectReferenceValue = needle;
            so.FindProperty("_count").intValue = p2 ? 5 : 3;
            so.FindProperty("_spin").floatValue = 0.5f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_both").boolValue = p2;
        });
        return CustomData($"Attack_Safari_Compass_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData TabGrid(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        var custom = Custom<SafariTabGridAttack>($"Safari_TabGrid_{s}", so =>
        {
            so.FindProperty("_repeat").intValue = p2 ? 2 : 1;
            so.FindProperty("_telegraph").floatValue = 1.4f;
            so.FindProperty("_hitTime").floatValue = 0.2f;
            so.FindProperty("_gap").floatValue = 0.35f;
            so.FindProperty("_red").intValue = 4;
        });
        return CustomData($"Attack_Safari_TabGrid_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData PrivateShot() => Data("Attack_Safari_PrivateShot", AttackKind.Projectile, so =>
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_speed").floatValue = 9f;
        so.FindProperty("_lifetime").floatValue = 3f;
        so.FindProperty("_hitSize").vector2Value = new Vector2(0.6f, 0.6f);
        so.FindProperty("_aim").intValue = (int)AimMode.Target;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_color").colorValue = UnityEngine.Color.white;
    });

    AttackData Private()
    {
        AttackData shot = PrivateShot();
        var custom = Custom<SafariPrivateAttack>("Safari_Private_P2", so =>
        {
            so.FindProperty("_bullet").objectReferenceValue = shot;
            so.FindProperty("_duration").floatValue = 3.5f;
            so.FindProperty("_shots").intValue = 2;
            so.FindProperty("_firstDelay").floatValue = 0.5f;
            so.FindProperty("_lineTime").floatValue = 0.8f;
            so.FindProperty("_interval").floatValue = 1.4f;
        });
        return CustomData("Attack_Safari_Private_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_lingering").boolValue = true;
        });
    }

    // ───────── 能力 ─────────

    AttackData Reader()
    {
        var custom = Custom<SafariReaderAttack>("Safari_Reader", so =>
        {
            so.FindProperty("_radius").floatValue = 5f;
            so.FindProperty("_hitTime").floatValue = 0.15f;
            so.FindProperty("_flash").floatValue = 0.3f;
        });
        return CustomData("Attack_App_Safari_Reader", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 6f;
            so.FindProperty("_gaugeGain").intValue = 8;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_knockback").vector2Value = new Vector2(6f, 4f);
            so.FindProperty("_hitStun").floatValue = 0.2f;
        });
    }
}
