using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// ・ボス：浮く型（Chrome の体）。第1形態「巻き戻し」「トリム」、第2形態はその強い版＋「画面収録」
/// ・能力：トリム（クールタイム 4秒・ゲージ 14）
/// ・ステージの決まりは実行時のコード（Kits/QuickTime/QuickTimeKit.cs・QuickTimeRule.cs）
/// ・攻撃データの名前に、スキンの枠の語を入れない
/// </summary>
public class QuickTimeKitAssets : KitAssetBuilder
{
    public override string Label => "QuickTime";
    public override string[] AppNames => new[] { "QuickTime Player", "QuickTime" };
    public override string[] BundleIds => new[] { "com.apple.QuickTimePlayerX" };
    public override bool HoverBoss => true;
    public override string SkillName => "トリム";
    public override SkillCategory Category => SkillCategory.Media;
    public override string SkillDescription => "左右から黄色いつまみが閉じて、間の敵を切る。雑魚は目の前へ寄せる。";
    public override string[] SkillTags => new[] { "範囲", "引き寄せ" };

    public override AttackData[] Phase1() => new[] { Rewind(false), Trim(false) };
    public override AttackData[] Phase2() => new[] { Rewind(true), Trim(true), Record() };
    public override AttackData PlayerSkill() => TrimSkill();

    AttackData Rewind(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<QuickTimeRewindAttack>($"QuickTime_Rewind_{tag}", so =>
        {
            so.FindProperty("_count").intValue = p2 ? 5 : 3;
            so.FindProperty("_spread").floatValue = p2 ? 70f : 50f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_outSpeed").floatValue = 6f;
            so.FindProperty("_outTime").floatValue = 1.2f;
            so.FindProperty("_hold").floatValue = 0.8f;
            so.FindProperty("_backSpeed").floatValue = 9f;
            so.FindProperty("_oneAims").boolValue = p2;
            so.FindProperty("_size").floatValue = 0.9f;
        });
        return CustomData($"Attack_QuickTime_Rewind_{tag}", custom, BossHit);
    }

    AttackData Trim(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<QuickTimeTrimAttack>($"QuickTime_Trim_{tag}", so =>
        {
            so.FindProperty("_width").floatValue = 6f;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_width2").floatValue = p2 ? 4f : 0f;
            so.FindProperty("_telegraph2").floatValue = 1.4f;
        });
        return CustomData($"Attack_QuickTime_Trim_{tag}", custom, BossHit);
    }

    AttackData Record()
    {
        var custom = Custom<QuickTimeRecordAttack>("QuickTime_ScreenRecording_P2", so =>
        {
            so.FindProperty("_record").floatValue = 2f;
            so.FindProperty("_glow").floatValue = 0.8f;
            so.FindProperty("_speedUp").floatValue = 1.5f;
        });
        return CustomData("Attack_QuickTime_ScreenRecording_P2", custom, BossHit);
    }

    AttackData TrimSkill()
    {
        var custom = Custom<QuickTimeTrimSkill>("QuickTime_TrimSkill", so =>
        {
            so.FindProperty("_reach").floatValue = 3.5f;
            so.FindProperty("_close").floatValue = 0.2f;
            so.FindProperty("_height").floatValue = 2.4f;
        });
        return CustomData("Attack_App_QuickTime_Trim", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_gaugeGain").intValue = 14;
            so.FindProperty("_damage").intValue = 2;
            so.FindProperty("_knockback").vector2Value = new Vector2(1f, 2f); // 寄せるので、はじき飛ばさない
            so.FindProperty("_hitStun").floatValue = 0.35f;
        });
    }

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
