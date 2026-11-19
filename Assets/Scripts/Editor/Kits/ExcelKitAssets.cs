using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Excel のキットのアセット（2026-11-19。docs/app-kits-1-design.md 10章）。担当4。
/// ボス：浮く（Chrome の体）。第1形態＝オートフィル（床の行と足場の行を交互）・棒グラフ 4本、
/// 第2形態＝オートフィル（行 → 列 → 行）・棒グラフ 6本・円グラフ。能力：オートフィル（遠いほど強い行）。
/// 実行時の登録は Kits/Excel/ExcelKit.cs（ラベル "Excel"）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// 【要相談 5】Excel の能力は棒グラフからオートフィルに変えた（仕様書のかっこ内の扱い）。棒グラフは Office のほかのアプリの代わりに残る
/// </summary>
public class ExcelKitAssets : KitAssetBuilder
{
    public override string Label => "Excel";
    public override string[] AppNames => new[] { "Excel", "Microsoft Excel" };
    public override string[] BundleIds => new[] { "com.microsoft.Excel" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Autofill(1), BarGraph(1) };
    public override AttackData[] Phase2() => new[] { Autofill(2), BarGraph(2), Pie() };
    public override AttackData PlayerSkill() => AutofillSkill();

    public override string SkillName => "オートフィル";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "正面へセルを8マス伸ばす。遠くのセルで当てるほど、ゲージが大きく増える。";
    public override string[] SkillTags => new[] { "遠いほど強い", "貫通" };

    // ───────── ボスの技 ─────────

    /// <summary>オートフィル。第1形態：行を1本（床の行と足場の行を交互）。第2形態：行 → 列 → 行</summary>
    AttackData Autofill(int phase)
    {
        var custom = Custom<ExcelAutofillAttack>($"Excel_Autofill_P{phase}", so =>
        {
            OfficeKitEdit.B(so, "_phase2", phase >= 2);
            OfficeKitEdit.F(so, "_cellWidth", 2f);
            OfficeKitEdit.F(so, "_rowHeight", 1.2f);
            OfficeKitEdit.F(so, "_platformRowY", 3f);
            OfficeKitEdit.F(so, "_floorExtend", 1.7f);     // 赤 0.8 と合わせて 2.5秒（足場の上だけ安全）
            OfficeKitEdit.F(so, "_platformExtend", 0.4f);  // 赤 0.8 と合わせて 1.2秒
            OfficeKitEdit.F(so, "_columnWidth", 2.5f);
            OfficeKitEdit.F(so, "_columnExtend", 0.6f);
            OfficeKitEdit.F(so, "_redTime", 0.8f);
            OfficeKitEdit.F(so, "_hitTime", 0.4f);
            OfficeKitEdit.F(so, "_gap", 0.2f);
        });
        return CustomData($"Attack_Excel_Autofill_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "217346");
        });
    }

    /// <summary>棒グラフ。棒 幅 1.5×高さ 4、2.5マスおき、0.25秒ずつ遅れる（予告 0.8・判定 0.5）。第1形態 4本、第2形態 6本</summary>
    AttackData BarGraph(int phase)
    {
        int count = phase >= 2 ? 6 : 4;
        // 名前に BarGraph（Office のスキンの赤い棒グラフの絵。わざと）
        AttackData bar = Data($"Attack_Excel_BarGraph_Bar_P{phase}", AttackKind.Area, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.I(so, "_anchor", (int)AreaAnchor.Forward);
            OfficeKitEdit.F(so, "_forwardDistance", 0f);
            OfficeKitEdit.B(so, "_snapToGround", true);
            OfficeKitEdit.I(so, "_shape", (int)AreaShape.Box);
            OfficeKitEdit.V(so, "_areaSize", new Vector2(1.5f, 4f));
            OfficeKitEdit.I(so, "_areaCount", count); // スキンの絵の数（出すのは技の中身）
            OfficeKitEdit.F(so, "_telegraphTime", 0.8f);
            OfficeKitEdit.F(so, "_activeTime", 0.5f);
            OfficeKitEdit.F(so, "_persistDuration", 0f);
            OfficeKitEdit.F(so, "_windup", 0f);
            Color(so, "_color", "FF453A");
        });
        var custom = Custom<ExcelBarGraphAttack>($"Excel_BarGraph_P{phase}", so =>
        {
            OfficeKitEdit.O(so, "_bar", bar);
            OfficeKitEdit.I(so, "_count", count);
            OfficeKitEdit.F(so, "_spacing", 2.5f);
            OfficeKitEdit.F(so, "_stagger", 0.25f);
        });
        return CustomData($"Attack_Excel_BarGraph_P{phase}", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "FF453A");
        });
    }

    /// <summary>円グラフ（第2形態）。6つの切れ端が全方向へ（毎秒 8マス）→ 30度ずらしてもう1回</summary>
    AttackData Pie()
    {
        // 名前に TabShuriken（Office のスキンの「円グラフの切れ端」の絵）
        AttackData slice = Data("Attack_Excel_Pie_TabShuriken_Slice", AttackKind.Projectile, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            OfficeKitEdit.F(so, "_speed", 8f);
            OfficeKitEdit.V(so, "_hitSize", new Vector2(0.6f, 0.6f));
            OfficeKitEdit.F(so, "_lifetime", 3f);
            OfficeKitEdit.F(so, "_gravity", 0f);
            OfficeKitEdit.B(so, "_destroyOnTerrain", true);
            OfficeKitEdit.B(so, "_destroyOnHit", true);
            OfficeKitEdit.I(so, "_projectileCount", 6); // スキンの絵の数
            OfficeKitEdit.I(so, "_repeatCount", 2);
            OfficeKitEdit.F(so, "_windup", 0f);
            Color(so, "_color", "4472C4");
        });
        var custom = Custom<ExcelPieAttack>("Excel_Pie_P2", so =>
        {
            OfficeKitEdit.O(so, "_slice", slice);
            OfficeKitEdit.I(so, "_count", 6);
            OfficeKitEdit.I(so, "_volleys", 2);
            OfficeKitEdit.F(so, "_rotate", 30f);
            OfficeKitEdit.F(so, "_telegraph", 0.8f);
            OfficeKitEdit.F(so, "_height", 1.8f);
            OfficeKitEdit.F(so, "_diameter", 2.2f);
        });
        return CustomData("Attack_Excel_Pie_P2", custom, so =>
        {
            OfficeKitEdit.EnemyHit(so);
            Color(so, "_color", "4472C4");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>オートフィル（能力）。8マス（1マス 0.05秒）。ゲージ＝当たったセルの数字 × 2。雑魚は倒れる。クールタイム 3.5秒</summary>
    AttackData AutofillSkill()
    {
        var custom = Custom<ExcelAutofillSkill>("Excel_AutofillSkill", so =>
        {
            OfficeKitEdit.I(so, "_cells", 8);
            OfficeKitEdit.F(so, "_cellTime", 0.05f);
            OfficeKitEdit.F(so, "_cellWidth", 1f);
            OfficeKitEdit.F(so, "_height", 1.2f);
        });
        return CustomData("Attack_Excel_AutofillSkill", custom, so =>
        {
            OfficeKitEdit.F(so, "_cooldown", 3.5f);
            OfficeKitEdit.I(so, "_damage", 10);     // 雑魚は倒れる
            OfficeKitEdit.I(so, "_gaugeGain", 2);   // 数字 × 2
            OfficeKitEdit.V(so, "_knockback", new Vector2(3f, 2f));
            OfficeKitEdit.F(so, "_hitStun", 0.2f);
            Color(so, "_color", "217346");
        });
    }
}

/// <summary>【仮】Office のキット（Excel・PowerPoint・Word・Keynote）のアセットを作るときの小さい道具（2026-11-19。担当4）</summary>
public static class OfficeKitEdit
{
    public static void F(SerializedObject so, string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; else Missing(so, p); }
    public static void I(SerializedObject so, string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; else Missing(so, p); }
    public static void B(SerializedObject so, string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; else Missing(so, p); }
    public static void V(SerializedObject so, string p, Vector2 v) { var x = so.FindProperty(p); if (x != null) x.vector2Value = v; else Missing(so, p); }
    public static void S(SerializedObject so, string p, string v) { var x = so.FindProperty(p); if (x != null) x.stringValue = v ?? ""; else Missing(so, p); }
    public static void O(SerializedObject so, string p, Object v) { var x = so.FindProperty(p); if (x != null) x.objectReferenceValue = v; else Missing(so, p); }

    /// <summary>ボスの技の基本（ライフ -1・ノックバック (6,5)・のけぞり 0.25）</summary>
    public static void EnemyHit(SerializedObject so)
    {
        I(so, "_damage", 1);
        V(so, "_knockback", new Vector2(6f, 5f));
        F(so, "_hitStun", 0.25f);
    }

    static void Missing(SerializedObject so, string p) => Debug.LogWarning($"[OfficeKit] {so.targetObject.name} に欄 {p} がありません");
}
