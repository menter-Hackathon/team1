using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Slack のキットのアセット（2026-11-19。docs/app-kits-1-design.md 2章）。数値はすべて【仮】。
/// ・第1形態：@channel（入力の欄に @channel → メンションの柱 3本。名前に Breakpoint を入れて Chat のスキンの「メンションの連打」の絵）・リアクションの雨
/// ・第2形態：@channel 5本・リアクションの雨・ハドル
/// ・能力：リアクション（親指を投げる。当てると 5秒ゲージ ×1.5）
/// ・ギミック（未読がたまる）は実行時の SlackRule（Kits/Slack/SlackKit.cs で登録）
/// </summary>
public class SlackKitAssets : KitAssetBuilder
{
    public override string Label => SlackKit.Label;
    public override string[] AppNames => SlackKit.Names;
    public override string[] BundleIds => SlackKit.Bundles;
    public override bool HoverBoss => true;
    public override string SkillName => SlackKit.SkillName;
    public override SkillCategory Category => SkillCategory.SocialChat;
    public override string SkillDescription => SlackKit.SkillDescription;
    public override string[] SkillTags => SlackKit.SkillTags;

    public override AttackData[] Phase1() => new[] { Mention("P1", 3, 4f), ReactionRain("P1") };
    public override AttackData[] Phase2() => new[] { Mention("P2", 5, 3f), ReactionRain("P2"), Huddle() };

    // ───────── @channel ─────────

    AttackData Mention(string phase, int count, float spacing)
    {
        // 中の柱（名前に Breakpoint ＝ Chat のスキンの「メンションの連打」の絵と予告の @）
        AttackData inner = Data($"Attack_Slack_Mention_Breakpoint_{phase}", AttackKind.Area, so =>
        {
            TalkEdit.F(so, "_windup", 0f);
            TalkEdit.E(so, "_anchor", (int)AreaAnchor.TargetPosition);
            TalkEdit.B(so, "_snapToGround", true);
            TalkEdit.E(so, "_shape", (int)AreaShape.Box);
            TalkEdit.V(so, "_areaSize", new Vector2(1.5f, 4f));
            TalkEdit.I(so, "_areaCount", count);
            TalkEdit.F(so, "_areaSpacing", spacing);
            TalkEdit.F(so, "_areaStagger", 0f);
            TalkEdit.B(so, "_areaForwardOrder", false);
            TalkEdit.F(so, "_telegraphTime", 0.8f);
            TalkEdit.F(so, "_activeTime", 0.5f);
            TalkEdit.F(so, "_persistDuration", 0f);
            TalkEdit.C(so, "_color", new Color(0.29f, 0.08f, 0.29f, 0.9f));
        });
        var typing = Custom<TalkTypingAttack>($"Slack_AtChannel_{phase}_Typing", so =>
        {
            TalkEdit.O(so, "_inner", inner);
            TalkEdit.S(so, "_text", "@channel");
            TalkEdit.F(so, "_typeTime", 0.8f);
            TalkEdit.C(so, "_accent", SlackArt.Purple);
        });
        return CustomData($"Attack_Slack_AtChannel_{phase}", typing);
    }

    // ───────── リアクションの雨 ─────────

    AttackData ReactionRain(string phase)
    {
        AttackData tag = Data("Attack_Slack_ReactionTag", AttackKind.Spawn, so =>
        {
            TalkEdit.E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            TalkEdit.I(so, "_spawnCount", 1);
            TalkEdit.V(so, "_objectSize", new Vector2(1.5f, 1.5f));
            TalkEdit.F(so, "_fallSpeed", 9f);
            TalkEdit.F(so, "_landRemainTime", 0f);
            TalkEdit.B(so, "_becomesPlatform", false);
            TalkEdit.F(so, "_telegraphTime", 0.8f);
        });
        var rain = Custom<SlackReactionRainAttack>($"Slack_ReactionRain_{phase}", so =>
        {
            TalkEdit.O(so, "_drop", tag);
            TalkEdit.I(so, "_count", 5);
            TalkEdit.F(so, "_spacing", 3f);
            TalkEdit.F(so, "_placeInterval", 0.15f);
            TalkEdit.F(so, "_hold", phase == "P2" ? 0.5f : 0.6f);
            TalkEdit.F(so, "_dropInterval", phase == "P2" ? 0.2f : 0.25f);
            TalkEdit.F(so, "_messageWidth", 14f);
        });
        return CustomData($"Attack_Slack_ReactionRain_{phase}", rain);
    }

    // ───────── ハドル ─────────

    AttackData Huddle()
    {
        var huddle = Custom<SlackHuddleAttack>("Slack_Huddle_P2", so =>
        {
            TalkEdit.F(so, "_diameter", 8f);
            TalkEdit.F(so, "_telegraph", 1f);
            TalkEdit.F(so, "_pulse", 0.4f);
            TalkEdit.F(so, "_gap", 0.8f);
            TalkEdit.I(so, "_pulses", 2);
        });
        return CustomData("Attack_Slack_Huddle_P2", huddle);
    }

    // ───────── 能力：リアクション ─────────

    public override AttackData PlayerSkill()
    {
        AttackData shot = Data("Attack_Ability_Slack_Thumb", AttackKind.Projectile, so =>
        {
            TalkEdit.I(so, "_damage", 3);
            TalkEdit.I(so, "_gaugeGain", 6);
            TalkEdit.V(so, "_knockback", new Vector2(3f, 3f));
            TalkEdit.F(so, "_speed", 12f);
            TalkEdit.F(so, "_lifetime", 8f / 12f);
            TalkEdit.V(so, "_hitSize", new Vector2(0.8f, 0.8f));
            TalkEdit.E(so, "_aim", (int)AimMode.Forward);
            TalkEdit.V(so, "_spawnOffset", new Vector2(0.3f, 0.8f));
            TalkEdit.B(so, "_destroyOnTerrain", true);
            TalkEdit.B(so, "_destroyOnHit", true);
            TalkEdit.I(so, "_projectileCount", 1);
            TalkEdit.I(so, "_repeatCount", 1);
        });
        var skill = Custom<SlackReactionSkill>("Slack_ReactionSkill", so =>
        {
            TalkEdit.O(so, "_shot", shot);
            TalkEdit.F(so, "_buffSeconds", 5f);
            TalkEdit.F(so, "_buffMultiplier", 1.5f);
        });
        return CustomData("Attack_Ability_Slack_Reaction", skill, so =>
        {
            TalkEdit.F(so, "_cooldown", 3.5f);
            TalkEdit.I(so, "_gaugeGain", 6);
            TalkEdit.I(so, "_damage", 3);
        });
    }
}

/// <summary>
/// 【仮】担当1 のキットのアセットの欄に値を入れる道具（2026-11-19）。欄の名前を間違えたら、エラーを出して先へ進む
/// （null のまま進んで、キットごと作られないことを防ぐ）
/// </summary>
public static class TalkEdit
{
    static SerializedProperty P(SerializedObject so, string name)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogError($"[TalkKit] 欄 {name} が {so.targetObject.GetType().Name}（{so.targetObject.name}）にありません");
        return p;
    }

    public static void F(SerializedObject so, string name, float v) { var p = P(so, name); if (p != null) p.floatValue = v; }
    public static void I(SerializedObject so, string name, int v) { var p = P(so, name); if (p != null) p.intValue = v; }
    public static void E(SerializedObject so, string name, int v) { var p = P(so, name); if (p != null) p.intValue = v; }
    public static void B(SerializedObject so, string name, bool v) { var p = P(so, name); if (p != null) p.boolValue = v; }
    public static void S(SerializedObject so, string name, string v) { var p = P(so, name); if (p != null) p.stringValue = v; }
    public static void V(SerializedObject so, string name, Vector2 v) { var p = P(so, name); if (p != null) p.vector2Value = v; }
    public static void C(SerializedObject so, string name, Color v) { var p = P(so, name); if (p != null) p.colorValue = v; }
    public static void O(SerializedObject so, string name, Object v) { var p = P(so, name); if (p != null) p.objectReferenceValue = v; }
}
