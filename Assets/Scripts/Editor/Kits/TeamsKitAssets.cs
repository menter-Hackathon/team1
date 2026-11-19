using UnityEngine;

/// <summary>
/// 【仮】Microsoft Teams のキットのアセット（2026-11-19。docs/app-kits-1-design.md 17章）。数値はすべて【仮】。
/// ・第1形態：画面共有（プレイヤーのまわりの赤い枠）・挙手（手の柱 3本。絵は KitDressed で手に差し替え）
/// ・第2形態：画面共有 2回・挙手 5本・参加しました（上の列 6枚。名前に TabMultiply を入れて Chat のスキンの「通知の板」の絵）
/// ・能力：挙手（真上の柱。絵は手）
/// ・ギミック（ブレイクアウトルーム）は場面③の出来事（Kits/Teams/TeamsKit.cs で StageEvents に登録）
/// </summary>
public class TeamsKitAssets : KitAssetBuilder
{
    public override string Label => TeamsKit.Label;
    public override string[] AppNames => TeamsKit.Names;
    public override string[] BundleIds => TeamsKit.Bundles;
    public override bool HoverBoss => true;
    public override string SkillName => TeamsKit.SkillName;
    public override SkillCategory Category => SkillCategory.SocialChat;
    public override string SkillDescription => TeamsKit.SkillDescription;
    public override string[] SkillTags => TeamsKit.SkillTags;

    public override AttackData[] Phase1() => new[] { Share("P1", 1), RaiseHand("P1", 3) };
    public override AttackData[] Phase2() => new[] { Share("P2", 2), RaiseHand("P2", 5), Joined() };

    AttackData Share(string phase, int times)
    {
        var share = Custom<TeamsShareAttack>($"Teams_Share_{phase}", so =>
        {
            TalkEdit.V(so, "_size", new Vector2(8f, 6f));
            TalkEdit.F(so, "_telegraph", 0.9f);
            TalkEdit.F(so, "_active", 0.4f);
            TalkEdit.I(so, "_times", times);
        });
        return CustomData($"Attack_Teams_Share_{phase}", share);
    }

    AttackData RaiseHand(string phase, int count)
    {
        AttackData inner = Data($"Attack_Teams_HandColumn_{phase}", AttackKind.Area, so =>
        {
            TalkEdit.F(so, "_windup", 0f);
            TalkEdit.E(so, "_anchor", (int)AreaAnchor.TargetPosition);
            TalkEdit.B(so, "_snapToGround", true);
            TalkEdit.E(so, "_shape", (int)AreaShape.Box);
            TalkEdit.V(so, "_areaSize", new Vector2(1.5f, 4.5f));
            TalkEdit.I(so, "_areaCount", count);
            TalkEdit.F(so, "_areaSpacing", 3f);
            TalkEdit.F(so, "_areaStagger", 0f);
            TalkEdit.B(so, "_areaForwardOrder", false);
            TalkEdit.F(so, "_telegraphTime", 0.8f);
            TalkEdit.F(so, "_activeTime", 0.5f);
            TalkEdit.C(so, "_color", (Color)TeamsArt.HandYellow);
        });
        return Dressed($"Attack_Teams_RaiseHand_{phase}", inner, TeamsKit.HandArt);
    }

    AttackData Joined()
    {
        // 上の列（名前に TabMultiply ＝ Chat のスキンの「通知の板」）
        return Data("Attack_Teams_Joined_TabMultiply_P2", AttackKind.Spawn, so =>
        {
            TalkEdit.E(so, "_placement", (int)SpawnPlacement.TopRow);
            TalkEdit.I(so, "_spawnCount", 6);
            TalkEdit.V(so, "_objectSize", new Vector2(2f, 1.5f));
            TalkEdit.F(so, "_placeInterval", 0.15f);
            TalkEdit.F(so, "_placeSpacing", 3f);
            TalkEdit.F(so, "_dropHoldTime", 0.6f);
            TalkEdit.F(so, "_dropInterval", 0.25f);
            TalkEdit.F(so, "_fallSpeed", 10f);
            TalkEdit.F(so, "_landRemainTime", 0f);
            TalkEdit.B(so, "_becomesPlatform", false);
            TalkEdit.C(so, "_color", (Color)TeamsArt.PurpleDark);
        });
    }

    public override AttackData PlayerSkill()
    {
        AttackData inner = Data("Attack_Ability_Teams_HandColumn", AttackKind.Area, so =>
        {
            TalkEdit.F(so, "_windup", 0f);
            TalkEdit.E(so, "_anchor", (int)AreaAnchor.Forward);
            TalkEdit.F(so, "_forwardDistance", 0f);
            TalkEdit.B(so, "_snapToGround", true);
            TalkEdit.E(so, "_shape", (int)AreaShape.Box);
            TalkEdit.V(so, "_areaSize", new Vector2(1.5f, 6f));
            TalkEdit.I(so, "_areaCount", 1);
            TalkEdit.F(so, "_telegraphTime", 0f);
            TalkEdit.F(so, "_activeTime", 0.3f);
            TalkEdit.F(so, "_cooldown", 2.5f);
            TalkEdit.I(so, "_gaugeGain", 10);
            TalkEdit.I(so, "_damage", 3);
            TalkEdit.V(so, "_knockback", new Vector2(2f, 6f));
            TalkEdit.C(so, "_color", (Color)TeamsArt.HandYellow);
        });
        return Dressed("Attack_Ability_Teams_RaiseHand", inner, TeamsKit.HandTallArt);
    }
}
