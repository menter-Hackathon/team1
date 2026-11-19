using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Word のキットのアセット（2026-11-19。docs/app-kits-1-design.md 12章）。担当4。
/// ボス：ワープ（VS Code の体）。第1形態＝取り消し線（低 → 高 → 低）・コメント 2つ、
/// 第2形態＝取り消し線（5回・順番が変わる）・コメント 3つ・レイアウトくずれ（DropQuake）。能力：太字（自分を強くする）。
/// 実行時の登録は Kits/Word/WordKit.cs（ラベル "Word"）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class WordKitAssets : KitAssetBuilder
{
    public override string Label => "Word";
    public override string[] AppNames => new[] { "Word", "Microsoft Word" };
    public override string[] BundleIds => new[] { "com.microsoft.Word" };
    public override bool HoverBoss => false;

    public override AttackData[] Phase1() => new[] { Strikethrough(1), Comment(1) };
    public override AttackData[] Phase2() => new[] { Strikethrough(2), Comment(2), Layout() };
    public override AttackData PlayerSkill() => BoldSkill();

    public override string SkillName => "太字";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "まわりに B の衝撃。4秒、ふちが太くなり、近接が長く届いて、近接のゲージが1.5倍になる。";
    public override string[] SkillTags => new[] { "強化", "近接" };

    // ───────── ボスの技 ─────────

    /// <summary>取り消し線。低（床から 0.6）・高（3.6）。太さ 0.5、判定 0.15秒、間 0.9秒。第1形態 3回（低 → 高 → 低）、第2形態 5回（ばらばら）</summary>
    AttackData Strikethrough(int phase)
    {
        var custom = Custom<WordStrikethroughAttack>($"Word_Strikethrough_P{phase}", so =>
        {
            OfficeKitEdit.I(so, "_count", phase >= 2 ? 5 : 3);
            OfficeKitEdit.B(so, "_random", phase >= 2);
            OfficeKitEdit.F(so, "_interval", 0.9f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
            OfficeKitEdit.F(so, "_hitTime", 0.15f);
            OfficeKitEdit.F(so, "_thickness", 0.5f);
            OfficeKitEdit.F(so, "_lowY", 0.6f);
            OfficeKitEdit.F(so, "_highY", 3.6f);
        });
        return CustomData($"Attack_Word_Strikethrough_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "E81123");
        });
    }

    /// <summary>コメント。黄色い吹き出し（3×2）がプレイヤーの近くに出る（出現の枠の予告 0.8秒）→ 足場として 3秒。第1形態 2つ、第2形態 3つ</summary>
    AttackData Comment(int phase)
    {
        // 中身は Chrome のポップアップ出現と同じ形（名前に枠の語を入れない。絵は word.comment に差し替え）
        AttackData inner = Data($"Attack_Word_Comment_Inner_P{phase}", AttackKind.Spawn, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.I(so, "_placement", (int)SpawnPlacement.AppearAroundTarget);
            OfficeKitEdit.I(so, "_spawnCount", phase >= 2 ? 3 : 2);
            OfficeKitEdit.F(so, "_minDistanceFromTarget", 3f);
            OfficeKitEdit.V(so, "_objectSize", new Vector2(3f, 2f));
            OfficeKitEdit.F(so, "_telegraphTime", 0.8f);
            OfficeKitEdit.F(so, "_activeTime", 0.1f);
            OfficeKitEdit.F(so, "_landRemainTime", 3f);
            OfficeKitEdit.B(so, "_becomesPlatform", true);
            OfficeKitEdit.V(so, "_appearTopRange", new Vector2(2.5f, 6f));
            OfficeKitEdit.F(so, "_reachHeight", 3f);
            Color(so, "_color", "FFFFFF");
        });
        return Dressed($"Attack_Word_Comment_P{phase}", inner, "word.comment", false, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.F(so, "_cooldown", 0f);
        });
    }

    /// <summary>レイアウトくずれ（第2形態）。大きい画像（4×3）がプレイヤーの位置に落ちる（影の予告 0.9秒）→ 着地で文字の波が左右へ（高さ 0.8、毎秒 9マス）</summary>
    AttackData Layout()
    {
        AttackData image = Data("Attack_Word_LayoutImage", AttackKind.Spawn, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.I(so, "_placement", (int)SpawnPlacement.AroundTarget);
            OfficeKitEdit.I(so, "_spawnCount", 1);
            OfficeKitEdit.V(so, "_objectSize", new Vector2(4f, 3f));
            OfficeKitEdit.F(so, "_fallSpeed", 16f);
            OfficeKitEdit.F(so, "_telegraphTime", 0.9f);
            OfficeKitEdit.F(so, "_landRemainTime", 0.5f);
            OfficeKitEdit.B(so, "_becomesPlatform", false);
            OfficeKitEdit.F(so, "_activeTime", 0.1f);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<DropQuakeAttack>("Word_Layout_P2", so =>
        {
            OfficeKitEdit.O(so, "_fall", image);
            OfficeKitEdit.O(so, "_wave", null);
            OfficeKitEdit.I(so, "_count", 1);
            OfficeKitEdit.F(so, "_interval", 0.5f);
            OfficeKitEdit.B(so, "_aimEach", true);
            OfficeKitEdit.B(so, "_waves", true);
            OfficeKitEdit.F(so, "_waveSpeed", 9f);
            OfficeKitEdit.F(so, "_waveHeight", 0.8f);
            OfficeKitEdit.F(so, "_waveWidth", 0.7f);
            OfficeKitEdit.S(so, "_fallArt", "word.image");
            OfficeKitEdit.S(so, "_waveArt", "word.textwave");
        });
        return CustomData("Attack_Word_Layout_P2", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "2B579A");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>太字。B の衝撃（直径 3・ゲージ +5）＋ 4秒の強化（近接のゲージ ×1.5・近接の届く長さ ×1.3）。クールタイム 8秒</summary>
    AttackData BoldSkill()
    {
        var custom = Custom<WordBoldSkill>("Word_BoldSkill", so =>
        {
            OfficeKitEdit.F(so, "_diameter", 3f);
            OfficeKitEdit.F(so, "_duration", 4f);
            OfficeKitEdit.F(so, "_gaugeRate", 1.5f);
            OfficeKitEdit.F(so, "_reach", 1.3f);
        });
        return CustomData("Attack_Word_BoldSkill", custom, so =>
        {
            OfficeKitEdit.F(so, "_cooldown", 8f);
            OfficeKitEdit.I(so, "_damage", 3);
            OfficeKitEdit.I(so, "_gaugeGain", 5);
            OfficeKitEdit.V(so, "_knockback", new Vector2(5f, 4f));
            OfficeKitEdit.F(so, "_hitStun", 0.25f);
            Color(so, "_color", "2B579A");
        });
    }
}
