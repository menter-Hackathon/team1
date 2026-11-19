using UnityEngine;

/// <summary>
/// 【仮】LINE のキットのアセット（2026-11-19。docs/app-kits-1-design.md 4章）。数値はすべて【仮】。
/// ・第1形態：スタンプ連打（3個）・トークの吹き出し（出現。名前に PopupAppear を入れて Chat のスキンの「大きい吹き出し」の絵）
/// ・第2形態：スタンプ連打（5個）・トークの吹き出し・怒りのメッセージ
/// ・能力：スタンプ（正面 3マス先に「ドン」。ゲージ +10）
/// ・ギミック（既読スルー）は実行時の LineRule（Kits/LINE/LineKit.cs で登録）
/// </summary>
public class LINEKitAssets : KitAssetBuilder
{
    public override string Label => LineKit.Label;
    public override string[] AppNames => LineKit.Names;
    public override string[] BundleIds => LineKit.Bundles;
    public override bool HoverBoss => true;
    public override string SkillName => LineKit.SkillName;
    public override SkillCategory Category => SkillCategory.SocialChat;
    public override string SkillDescription => LineKit.SkillDescription;
    public override string[] SkillTags => LineKit.SkillTags;

    public override AttackData[] Phase1() => new[] { StampBarrage("P1", 3), Talk("P1") };
    public override AttackData[] Phase2() => new[] { StampBarrage("P2", 5), Talk("P2"), Angry() };

    AttackData StampBarrage(string phase, int count)
    {
        AttackData drop = Data("Attack_LINE_StampDrop", AttackKind.Spawn, so =>
        {
            TalkEdit.E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            TalkEdit.I(so, "_spawnCount", 1);
            TalkEdit.V(so, "_objectSize", new Vector2(3f, 2f));
            TalkEdit.F(so, "_fallSpeed", 10f);
            TalkEdit.F(so, "_landRemainTime", 0.15f);
            TalkEdit.B(so, "_becomesPlatform", false);
            TalkEdit.F(so, "_telegraphTime", 0.8f);
        });
        var barrage = Custom<LineStampBarrageAttack>($"Line_StampBarrage_{phase}", so =>
        {
            TalkEdit.O(so, "_drop", drop);
            TalkEdit.I(so, "_count", count);
            TalkEdit.F(so, "_interval", 0.35f);
        });
        return CustomData($"Attack_LINE_StampBarrage_{phase}", barrage);
    }

    AttackData Talk(string phase)
    {
        // 出現（ポップアップ出現と同じ置き方）。名前に PopupAppear ＝ Chat のスキンの「大きい吹き出し」
        return Data($"Attack_LINE_Talk_PopupAppear_{phase}", AttackKind.Spawn, so =>
        {
            TalkEdit.F(so, "_windup", 0f);
            TalkEdit.E(so, "_placement", (int)SpawnPlacement.AppearAroundTarget);
            TalkEdit.I(so, "_spawnCount", 2);
            TalkEdit.V(so, "_objectSize", new Vector2(3f, 2f));
            TalkEdit.F(so, "_telegraphTime", 0.8f);
            TalkEdit.F(so, "_activeTime", 0.1f);
            TalkEdit.F(so, "_landRemainTime", 2.5f);
            TalkEdit.B(so, "_becomesPlatform", true);
            TalkEdit.F(so, "_minDistanceFromTarget", 3f);
            TalkEdit.V(so, "_appearTopRange", new Vector2(2.5f, 6f));
            TalkEdit.F(so, "_reachHeight", 3f);
            TalkEdit.C(so, "_color", new Color(0.55f, 0.88f, 0.33f, 1f));
        });
    }

    AttackData Angry()
    {
        var angry = Custom<LineAngryAttack>("Line_Angry_P2", so =>
        {
            TalkEdit.F(so, "_blink", 1.5f);
            TalkEdit.F(so, "_diameter", 4f);
            TalkEdit.F(so, "_stagger", 0.12f);
        });
        return CustomData("Attack_LINE_Angry_P2", angry);
    }

    public override AttackData PlayerSkill()
    {
        var stamp = Custom<LineStampSkill>("Line_StampSkill", so =>
        {
            TalkEdit.V(so, "_size", new Vector2(3f, 2f));
            TalkEdit.F(so, "_forward", 3f);
            TalkEdit.F(so, "_drop", 0.25f);
            TalkEdit.F(so, "_active", 0.2f);
        });
        return CustomData("Attack_Ability_LINE_Stamp", stamp, so =>
        {
            TalkEdit.F(so, "_cooldown", 2.5f);
            TalkEdit.I(so, "_gaugeGain", 10);
            TalkEdit.I(so, "_damage", 3);
            TalkEdit.V(so, "_knockback", new Vector2(3f, 4f));
        });
    }
}
