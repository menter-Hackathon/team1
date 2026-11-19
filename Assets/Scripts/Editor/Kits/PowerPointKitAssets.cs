using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】PowerPoint のキットのアセット（2026-11-19。docs/app-kits-1-design.md 11章）。担当4。
/// ボス：浮く（Chrome の体）。第1形態＝箇条書き（高 → 中 → 低）・スライド落下 2枚、
/// 第2形態＝箇条書き（順番が変わる）・スライド落下 3枚・フライイン。能力：フライイン（画面の横の全部）。
/// 実行時の登録は Kits/PowerPoint/PowerPointKit.cs（ラベル "PowerPoint"）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class PowerPointKitAssets : KitAssetBuilder
{
    public override string Label => "PowerPoint";
    public override string[] AppNames => new[] { "PowerPoint", "Microsoft PowerPoint" };
    public override string[] BundleIds => new[] { "com.microsoft.Powerpoint" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Bullets(1), SlideDrop(1) };
    public override AttackData[] Phase2() => new[] { Bullets(2), SlideDrop(2), FlyIn() };
    public override AttackData PlayerSkill() => FlyInSkill();

    public override string SkillName => "フライイン";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "後ろから大きい矢印が飛んできて、画面の端まで横切る。通り道の敵すべてに当たる。";
    public override string[] SkillTags => new[] { "横いっぱい", "貫通" };

    // ───────── ボスの技 ─────────

    /// <summary>箇条書き。文の行（2.5×0.6、毎秒 11マス）が 高・中・低 の順に（間 0.6秒）。第2形態は順番が変わる</summary>
    AttackData Bullets(int phase)
    {
        AttackData line = Data("Attack_PowerPoint_BulletLine", AttackKind.Projectile, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.F(so, "_speed", 11f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(2.5f, 0.6f));
            OfficeKitEdit.F(so, "_lifetime", 2.6f);
            OfficeKitEdit.F(so, "_gravity", 0f);
            OfficeKitEdit.B(so, "_destroyOnTerrain", true);
            OfficeKitEdit.B(so, "_destroyOnHit", false);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<PowerPointBulletsAttack>($"PowerPoint_Bullets_P{phase}", so =>
        {
            OfficeKitEdit.O(so, "_line", line);
            OfficeKitEdit.B(so, "_shuffle", phase >= 2);
            OfficeKitEdit.F(so, "_dotInterval", 0.25f);
            OfficeKitEdit.F(so, "_interval", 0.6f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
            OfficeKitEdit.F(so, "_side", 1.6f);
        });
        return CustomData($"Attack_PowerPoint_Bullets_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "D24726");
        });
    }

    /// <summary>スライド落下。スライド（3×2）がプレイヤーの位置に 0.5秒おきに（影の予告 0.8秒）→ 着地後 3秒、足場。第1形態 2枚、第2形態 3枚</summary>
    AttackData SlideDrop(int phase)
    {
        // 名前に ErrorWindowFall（Office のスキンのスライドの絵）
        AttackData fall = Data("Attack_PowerPoint_Slide_ErrorWindowFall", AttackKind.Spawn, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.I(so, "_placement", (int)SpawnPlacement.AroundTarget);
            OfficeKitEdit.I(so, "_spawnCount", 1);
            OfficeKitEdit.V(so, "_objectSize", new Vector2(3f, 2f));
            OfficeKitEdit.F(so, "_fallSpeed", 14f);
            OfficeKitEdit.F(so, "_telegraphTime", 0.8f);
            OfficeKitEdit.F(so, "_landRemainTime", 3f);
            OfficeKitEdit.B(so, "_becomesPlatform", true);
            OfficeKitEdit.F(so, "_activeTime", 0.1f);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<DropQuakeAttack>($"PowerPoint_SlideDrop_P{phase}", so =>
        {
            OfficeKitEdit.O(so, "_fall", fall);
            OfficeKitEdit.O(so, "_wave", null);
            OfficeKitEdit.I(so, "_count", phase >= 2 ? 3 : 2);
            OfficeKitEdit.F(so, "_interval", 0.5f);
            OfficeKitEdit.B(so, "_aimEach", true);
            OfficeKitEdit.B(so, "_waves", false);
            OfficeKitEdit.S(so, "_fallArt", "");
            OfficeKitEdit.S(so, "_waveArt", "");
        });
        return CustomData($"Attack_PowerPoint_SlideDrop_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "D24726");
        });
    }

    /// <summary>フライイン（第2形態）。壁から壁へ矢印（3×1、毎秒 14マス）：左から低 → 右から高 → 左から低 → 右から低（間 0.6秒）</summary>
    AttackData FlyIn()
    {
        AttackData arrow = Data("Attack_PowerPoint_FlyInArrow", AttackKind.Projectile, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.F(so, "_speed", 14f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(3f, 1f));
            OfficeKitEdit.F(so, "_lifetime", 2f);
            OfficeKitEdit.F(so, "_gravity", 0f);
            OfficeKitEdit.B(so, "_destroyOnTerrain", true);
            OfficeKitEdit.B(so, "_destroyOnHit", false);
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<PowerPointFlyInAttack>("PowerPoint_FlyIn_P2", so =>
        {
            OfficeKitEdit.O(so, "_arrow", arrow);
            SetInts(so.FindProperty("_dirs"), new[] { 1, -1, 1, -1 });
            SetInts(so.FindProperty("_heights"), new[] { 0, 2, 0, 0 });
            OfficeKitEdit.F(so, "_interval", 0.6f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
        });
        return CustomData("Attack_PowerPoint_FlyIn_P2", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "D24726");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>フライイン（能力）。矢印 3×1、毎秒 20マス。通り道の敵に1回ずつ（雑魚は倒れる・ボスにゲージ +12）。クールタイム 3.5秒</summary>
    AttackData FlyInSkill()
    {
        var custom = Custom<PowerPointFlyInSkill>("PowerPoint_FlyInSkill", so =>
        {
            OfficeKitEdit.F(so, "_maxBehind", 10f);
            OfficeKitEdit.F(so, "_height", 0.6f); // 床から 0.1 あける（判定の半径 0.5）
        });
        return CustomData("Attack_PowerPoint_FlyInSkill", custom, so =>
        {
            OfficeKitEdit.F(so, "_cooldown", 3.5f);
            OfficeKitEdit.I(so, "_damage", 10);
            OfficeKitEdit.I(so, "_gaugeGain", 12);
            OfficeKitEdit.V(so, "_knockback", new Vector2(4f, 3f));
            OfficeKitEdit.F(so, "_hitStun", 0.2f);
            OfficeKitEdit.F(so, "_speed", 20f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(3f, 1f));
            OfficeKitEdit.F(so, "_lifetime", 1.6f);
            OfficeKitEdit.F(so, "_gravity", 0f);
            OfficeKitEdit.B(so, "_destroyOnTerrain", true);
            OfficeKitEdit.B(so, "_destroyOnHit", false);
            Color(so, "_color", "FFFFFF");
        });
    }

    static void SetInts(SerializedProperty arr, int[] values)
    {
        if (arr == null) return;
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).intValue = values[i];
    }
}
