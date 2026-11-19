using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メッセージ（Apple の Messages）のキットのアセット（2026-11-19。docs/app-kits-1-design.md 16章）。数値はすべて【仮】。
/// ・ボスはワープ（VS Code の体）
/// ・第1形態：スラム（落下＋左右の波。DropQuake）・花火（3か所）
/// ・第2形態：スラム 2連続・花火 5か所・レーザー（床をなぞる光の帯）
/// ・能力：花火（空中に 3発）
/// ・ギミック（風船）は実行時の MessagesRule（Kits/Messages/MessagesKit.cs で登録）
/// </summary>
public class MessagesKitAssets : KitAssetBuilder
{
    public override string Label => MessagesKit.Label;
    public override string[] AppNames => MessagesKit.Names;
    public override string[] BundleIds => MessagesKit.Bundles;
    public override bool HoverBoss => false;
    public override string SkillName => MessagesKit.SkillName;
    public override SkillCategory Category => SkillCategory.SocialChat;
    public override string SkillDescription => MessagesKit.SkillDescription;
    public override string[] SkillTags => MessagesKit.SkillTags;

    public override AttackData[] Phase1() => new[] { Slam("P1", 1), Fireworks("P1", 3) };

    public override AttackData[] Phase2()
    {
        var list = new List<AttackData> { Slam("P2", 2), Fireworks("P2", 5) };
        AttackData laser = Laser();
        if (laser != null) list.Add(laser);
        return list.ToArray();
    }

    AttackData Slam(string phase, int count)
    {
        AttackData fall = Data("Attack_Messages_SlamBubble", AttackKind.Spawn, so =>
        {
            TalkEdit.E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            TalkEdit.I(so, "_spawnCount", 1);
            TalkEdit.V(so, "_objectSize", new Vector2(4f, 2.5f));
            TalkEdit.F(so, "_fallSpeed", 14f);
            TalkEdit.F(so, "_landRemainTime", 0.3f);
            TalkEdit.B(so, "_becomesPlatform", false);
            TalkEdit.F(so, "_telegraphTime", 0.8f);
        });
        var slam = Custom<MessagesSlamAttack>($"Messages_Slam_{phase}", so =>
        {
            TalkEdit.O(so, "_fall", fall);
            TalkEdit.I(so, "_count", count);
            TalkEdit.F(so, "_interval", 0.9f);
        });
        return CustomData($"Attack_Messages_Slam_{phase}", slam);
    }

    AttackData Fireworks(string phase, int count)
    {
        var fw = Custom<MessagesFireworksAttack>($"Messages_Fireworks_{phase}", so =>
        {
            TalkEdit.I(so, "_count", count);
            TalkEdit.F(so, "_spacing", 3.5f);
            TalkEdit.F(so, "_diameter", 3f);
            TalkEdit.F(so, "_telegraph", 0.8f);
            TalkEdit.F(so, "_stagger", 0.3f);
            TalkEdit.F(so, "_active", 0.4f);
        });
        return CustomData($"Attack_Messages_Fireworks_{phase}", fw);
    }

    /// <summary>レーザー（第2形態。④）</summary>
    AttackData Laser()
    {
        var laser = Custom<MessagesLaserAttack>("Messages_Laser_P2", so =>
        {
            TalkEdit.F(so, "_telegraph", 0.8f);
            TalkEdit.F(so, "_sweep", 1.6f);
            TalkEdit.V(so, "_spot", new Vector2(1.1f, 0.8f));
            TalkEdit.F(so, "_platformHit", 1f);
            TalkEdit.F(so, "_beamWidth", 0.35f);
        });
        return CustomData("Attack_Messages_Laser_P2", laser);
    }

    public override AttackData PlayerSkill()
    {
        var skill = Custom<MessagesFireworkSkill>("Messages_FireworkSkill", so =>
        {
            TalkEdit.F(so, "_forward", 4f);
            TalkEdit.F(so, "_height", 3f);
            TalkEdit.F(so, "_diameter", 4f);
            TalkEdit.I(so, "_count", 3);
            TalkEdit.F(so, "_interval", 0.2f);
            TalkEdit.F(so, "_active", 0.25f);
        });
        return CustomData("Attack_Ability_Messages_Firework", skill, so =>
        {
            TalkEdit.F(so, "_cooldown", 3f);
            TalkEdit.I(so, "_gaugeGain", 4);
            TalkEdit.I(so, "_damage", 3);
            TalkEdit.V(so, "_knockback", new Vector2(3f, 4f));
        });
    }
}
