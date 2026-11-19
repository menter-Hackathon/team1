using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Cursor のキットのアセット（2026-11-19。docs/app-kits-2-design.md 3-3）。担当B。
/// ・ボス（ワープ）：第1形態 Tab 補完・Accept / Reject ／ 第2形態 その強い版＋Generating...
/// ・能力：⌘K（その場で作る能力のひな形から、ランダムに1つ。ひな形は AbilityCatalog から写す）
/// ・ステージの決まり（Tab の候補）は実行時のコード（Kits/Cursor/CursorKit.cs）
/// 数値はすべて【仮】。作るたびに入れ直す（コードの数値が正）。
/// </summary>
public class CursorKitAssets : KitAssetBuilder
{
    public override string Label => CursorKit.Label;
    public override string[] AppNames => CursorKit.Names;
    public override string[] BundleIds => CursorKit.Bundles;
    public override bool HoverBoss => false;

    public override string SkillName => "コマンドK"; // ⌘ の字はフォントに無いことがあるので、読みで【仮】
    public override SkillCategory Category => SkillCategory.Dev;
    public override SkinId SkillSkin => SkinId.Dev;
    public override string SkillDescription => "AI まかせ。押すたびに、ちがう能力がランダムに出る";
    public override string[] SkillTags => new[] { "ランダム", "AI" };

    public override AttackData[] Phase1() => new[] { Tab(false), AcceptReject(false) };
    public override AttackData[] Phase2() => new[] { Tab(true), AcceptReject(true), Generating() };
    public override AttackData PlayerSkill() => CmdK();

    // ───────── ボスの技 ─────────

    AttackData Tab(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        var custom = Custom<CursorTabAttack>($"Cursor_Tab_{s}", so =>
        {
            so.FindProperty("_count").intValue = p2 ? 4 : 2;
            so.FindProperty("_interval").floatValue = p2 ? 0.8f : 0.9f;
            so.FindProperty("_ghost").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_hitTime").floatValue = 0.15f;
            so.FindProperty("_size").vector2Value = new Vector2(2f, 2f);
            so.FindProperty("_lookAhead").floatValue = 0.8f;
            so.FindProperty("_evenIsCurrent").boolValue = p2;
        });
        return CustomData($"Attack_Cursor_Tab_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData AcceptReject(bool p2)
    {
        string s = p2 ? "P2" : "P1";
        var custom = Custom<CursorAcceptRejectAttack>($"Cursor_AcceptReject_{s}", so =>
        {
            so.FindProperty("_sections").intValue = p2 ? 3 : 2;
            so.FindProperty("_repeat").intValue = p2 ? 2 : 1;
            so.FindProperty("_telegraph").floatValue = 1.8f;
            so.FindProperty("_hitTime").floatValue = 0.2f;
            so.FindProperty("_gap").floatValue = 0.3f;
        });
        return CustomData($"Attack_Cursor_AcceptReject_{s}", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    AttackData Generating()
    {
        var custom = Custom<CursorGeneratingAttack>("Cursor_Generating_P2", so =>
        {
            so.FindProperty("_rowGap").floatValue = 0.6f;
            so.FindProperty("_ghost").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_typeSpeed").floatValue = 20f;
            so.FindProperty("_linger").floatValue = 0.3f;
            so.FindProperty("_height").floatValue = 1f;
        });
        return CustomData("Attack_Cursor_Generating_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_lingering").boolValue = true;
        });
    }

    // ───────── 能力 ─────────

    AttackData CmdKFallback() => Data("Attack_App_Cursor_CmdKSlash", AttackKind.Area, so =>
    {
        so.FindProperty("_anchor").intValue = (int)AreaAnchor.Forward;
        so.FindProperty("_forwardDistance").floatValue = 1.6f;
        so.FindProperty("_snapToGround").boolValue = true;
        so.FindProperty("_shape").intValue = (int)AreaShape.Box;
        so.FindProperty("_areaSize").vector2Value = new Vector2(2.4f, 1.8f);
        so.FindProperty("_areaCount").intValue = 1;
        so.FindProperty("_telegraphTime").floatValue = 0f;
        so.FindProperty("_activeTime").floatValue = 0.15f;
        so.FindProperty("_damage").intValue = 2;
        so.FindProperty("_gaugeGain").intValue = 8;
        so.FindProperty("_color").colorValue = new UnityEngine.Color(0.54f, 0.54f, 0.54f, 0.8f);
    });

    AttackData CmdK()
    {
        AttackData fallback = CmdKFallback();
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilitiesBuilder.CatalogPath);
        var custom = Custom<CursorCmdKAttack>("Cursor_CmdK", so =>
        {
            var arr = so.FindProperty("_templates");
            AbilityTemplate[] t = catalog != null ? catalog.Templates : new AbilityTemplate[0];
            int n = 0;
            foreach (var x in t) if (x != null && x.Attack != null) n++;
            arr.arraySize = n;
            int i = 0;
            foreach (var x in t)
            {
                if (x == null || x.Attack == null) continue;
                arr.GetArrayElementAtIndex(i++).objectReferenceValue = x.Attack;
            }
            so.FindProperty("_fallback").objectReferenceValue = fallback;
            so.FindProperty("_preview").floatValue = 0.15f;
        });
        if (catalog == null || catalog.Templates.Length == 0) Debug.LogWarning("[Cursor] ⌘K：ひな形が見つかりません。正面の判定だけになります");
        return CustomData("Attack_App_Cursor_CmdK", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_gaugeGain").intValue = 0; // ゲージは出たひな形のまま
            so.FindProperty("_damage").intValue = 1;
        });
    }
}
