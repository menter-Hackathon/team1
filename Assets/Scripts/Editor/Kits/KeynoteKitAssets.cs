using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Keynote のキットのアセット（2026-11-19。docs/app-kits-1-design.md 13章）。担当4。
/// ボス：浮く（Chrome の体）。第1形態＝マジックムーブ 3回・大きい数字 1・2・3、
/// 第2形態＝マジックムーブ 5回・大きい数字・One more thing...。能力：マジックムーブ（無敵の突進）。
/// 実行時の登録は Kits/Keynote/KeynoteKit.cs（ラベル "Keynote"）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class KeynoteKitAssets : KitAssetBuilder
{
    public override string Label => "Keynote";
    public override string[] AppNames => new[] { "Keynote" };
    public override string[] BundleIds => new[] { "com.apple.iWork.Keynote" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { MagicMove(1), BigNumbers(1) };
    public override AttackData[] Phase2() => new[] { MagicMove(2), BigNumbers(2), OneMoreThing() };
    public override AttackData PlayerSkill() => DashSkill();

    public override string SkillName => "マジックムーブ";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "正面へなめらかに5マス動く。動いている間は無敵で、通り道の敵に当たる。";
    public override string[] SkillTags => new[] { "突進", "無敵" };

    // ───────── ボスの技 ─────────

    /// <summary>マジックムーブ。丸（直径 2.5）が点線の予告 0.8秒のあと、0.35秒でプレイヤーのいた所へ。第1形態 3回、第2形態 5回</summary>
    AttackData MagicMove(int phase)
    {
        var custom = Custom<KeynoteMagicMoveAttack>($"Keynote_MagicMove_P{phase}", so =>
        {
            OfficeKitEdit.I(so, "_hops", phase >= 2 ? 5 : 3);
            OfficeKitEdit.F(so, "_diameter", 2.5f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
            OfficeKitEdit.F(so, "_moveTime", 0.35f);
            OfficeKitEdit.F(so, "_arriveHit", 0.1f);
            OfficeKitEdit.F(so, "_side", 2.2f);
        });
        return CustomData($"Attack_Keynote_MagicMove_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "0A84FF");
        });
    }

    /// <summary>大きい数字。1・2・3（2.5×3）が 0.5秒おきにプレイヤーの位置へ落ちる（影の予告 0.8秒）。第2形態は 0.4秒おき</summary>
    AttackData BigNumbers(int phase)
    {
        AttackData fall = Data("Attack_Keynote_BigNumber", AttackKind.Spawn, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.I(so, "_placement", (int)SpawnPlacement.AroundTarget);
            OfficeKitEdit.I(so, "_spawnCount", 1);
            OfficeKitEdit.V(so, "_objectSize", new Vector2(2.5f, 3f));
            OfficeKitEdit.F(so, "_fallSpeed", 15f);
            OfficeKitEdit.F(so, "_telegraphTime", 0.8f);
            OfficeKitEdit.F(so, "_landRemainTime", 0.6f);
            OfficeKitEdit.B(so, "_becomesPlatform", false);
            OfficeKitEdit.F(so, "_activeTime", 0.1f);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<DropQuakeAttack>($"Keynote_BigNumbers_P{phase}", so =>
        {
            OfficeKitEdit.O(so, "_fall", fall);
            OfficeKitEdit.O(so, "_wave", null);
            OfficeKitEdit.I(so, "_count", 3);
            OfficeKitEdit.F(so, "_interval", phase >= 2 ? 0.4f : 0.5f);
            OfficeKitEdit.B(so, "_aimEach", true);
            OfficeKitEdit.B(so, "_waves", false);
            OfficeKitEdit.S(so, "_fallArt", "keynote.number");
            OfficeKitEdit.S(so, "_waveArt", "");
        });
        return CustomData($"Attack_Keynote_BigNumbers_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "FFFFFF");
        });
    }

    /// <summary>One more thing...（第2形態）。暗転＋スポットライト、12方向の線（0.8秒）→ 白い星 12発（毎秒 7マス）→ 15度ずらしてもう12発</summary>
    AttackData OneMoreThing()
    {
        AttackData star = Data("Attack_Keynote_Star", AttackKind.Projectile, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.F(so, "_speed", 7f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(0.5f, 0.5f));
            OfficeKitEdit.F(so, "_lifetime", 3.5f);
            OfficeKitEdit.F(so, "_gravity", 0f);
            OfficeKitEdit.B(so, "_destroyOnTerrain", true);
            OfficeKitEdit.B(so, "_destroyOnHit", true);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<KeynoteOneMoreThingAttack>("Keynote_OneMoreThing_P2", so =>
        {
            OfficeKitEdit.O(so, "_star", star);
            OfficeKitEdit.I(so, "_count", 12);
            OfficeKitEdit.I(so, "_volleys", 2);
            OfficeKitEdit.F(so, "_rotate", 15f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
            OfficeKitEdit.F(so, "_dim", 0.45f);
        });
        return CustomData("Attack_Keynote_OneMoreThing_P2", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "FFFFFF");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>マジックムーブ（能力）。正面へ 5マス・0.2秒・無敵。通り道の敵にゲージ +10（雑魚は倒れる）。クールタイム 3.5秒</summary>
    AttackData DashSkill()
    {
        var custom = Custom<KeynoteDashSkill>("Keynote_DashSkill", so =>
        {
            OfficeKitEdit.F(so, "_distance", 5f);
            OfficeKitEdit.F(so, "_time", 0.2f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(1.2f, 1.2f));
            OfficeKitEdit.F(so, "_exitSpeed", 6f);
        });
        return CustomData("Attack_Keynote_DashSkill", custom, so =>
        {
            OfficeKitEdit.F(so, "_cooldown", 3.5f);
            OfficeKitEdit.I(so, "_damage", 10);
            OfficeKitEdit.I(so, "_gaugeGain", 10);
            OfficeKitEdit.V(so, "_knockback", new Vector2(4f, 3f));
            OfficeKitEdit.F(so, "_hitStun", 0.2f);
            Color(so, "_color", "0A84FF");
        });
    }
}
