using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finder・Premiere Pro・Terminal のボスの専用の技と、Finder・Premiere Pro の能力を用意する（2026-11-18。docs/app-boss-skills-design.md）。
/// ・Boss_Finder・Boss_PremierePro（Boss_Chrome の複製。浮いて移動）を作り、対応アプリの表の「代用ボスの専用の技」に入れる
/// ・Boss_Terminal の技を入れ替え、再起動の演出（そのボスだけの仕組み）を足す
/// ・Skill_App_Finder・Skill_App_PremierePro の攻撃データを、専用の能力にする
/// 技の中身は BossSkillAssets（FinderSkillAssets.cs など）が作る。まだできていない技（null）の枠は、今の複製の技のまま残す。
/// </summary>
public static class AppBossSkillsBuilder
{
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";

    [MenuItem("Tools/Team1/ボスの専用の技を用意")]
    public static void BuildFromMenu()
    {
        Build();
        Debug.Log("ボスの専用の技を用意しました（Finder・Premiere Pro・Terminal）");
    }

    /// <summary>コマンドラインから実行する用（-executeMethod AppBossSkillsBuilder.Build）</summary>
    public static void Build()
    {
        AbilitiesBuilder.EnsureAppSkills();
        FinalBossBuilder.BuildFinalBoss();
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        BossController chromePrefab = catalog != null ? catalog.Apps.FirstOrDefault(a => a.DisplayName == "Chrome")?.BossPrefab : null;

        // ── Finder ──
        BossProfile finder = GetOrCreateProfile("Boss_Finder", "Finder");
        ApplyPhases(finder, BossSkillAssets.FinderPhase1(), BossSkillAssets.FinderPhase2());
        SetAppSkill("Assets/ScriptableObjects/Skills/Skill_App_Finder.asset", BossSkillAssets.FinderSkill(), "zip圧縮");

        // ── Premiere Pro ──
        BossProfile premiere = GetOrCreateProfile("Boss_PremierePro", "Premiere Pro");
        ApplyPhases(premiere, BossSkillAssets.PremierePhase1(), BossSkillAssets.PremierePhase2());
        SetAppSkill("Assets/ScriptableObjects/Skills/Skill_App_PremierePro.asset", BossSkillAssets.PremiereSkill(), "エフェクトアイコン投げ");

        // 対応アプリの表の「代用ボスの専用の技」（空のときだけ入れる。手で直した値は残す）
        if (catalog != null)
        {
            PlayerTestSceneBuilder.SetProps(catalog, so =>
            {
                var arr = so.FindProperty("_appBosses");
                if (arr.arraySize > 0) return;
                arr.arraySize = 2;
                AppBoss(arr.GetArrayElementAtIndex(0), "Finder", new[] { "Finder" }, new[] { "com.apple.finder" }, chromePrefab, finder);
                AppBoss(arr.GetArrayElementAtIndex(1), "Premiere Pro", new[] { "Premiere Pro", "Adobe Premiere Pro" }, new[] { "com.adobe.PremierePro" }, chromePrefab, premiere);
            });
            EditorUtility.SetDirty(catalog);
        }

        // ── Terminal ──
        var terminal = AssetDatabase.LoadAssetAtPath<BossProfile>($"{BossFolder}/Boss_Terminal.asset");
        if (terminal != null)
        {
            ApplyPhases(terminal, BossSkillAssets.TerminalPhase1(), BossSkillAssets.TerminalPhase2());
            BossExtension[] ext = BossSkillAssets.TerminalExtensions();
            if (ext != null && ext.Length > 0)
            {
                PlayerTestSceneBuilder.SetProps(terminal, so =>
                {
                    var arr = so.FindProperty("_extensions");
                    arr.arraySize = ext.Length;
                    for (int i = 0; i < ext.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ext[i];
                    so.FindProperty("_transitionTime").floatValue = 3.5f; // 再起動の演出（docs 4-4）
                });
                EditorUtility.SetDirty(terminal);
            }
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Boss_Chrome を複製して、そのアプリのボスの設定を作る（無ければ）</summary>
    static BossProfile GetOrCreateProfile(string assetName, string displayName)
    {
        string path = $"{BossFolder}/{assetName}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        if (profile != null) return profile;
        AssetDatabase.CopyAsset($"{BossFolder}/Boss_Chrome.asset", path);
        profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_reward").objectReferenceValue = null; // 能力は代用ボスと同じく、アプリ専用の能力の表から渡る
            so.FindProperty("_gaugeMax").intValue = 100;
        });
        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>技の並びを入れる。null・空の形態は、今のまま（複製の技）残す</summary>
    static void ApplyPhases(BossProfile profile, AttackData[] phase1, AttackData[] phase2)
    {
        if (profile == null) return;
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            SetMoves(so.FindProperty("_phase1").FindPropertyRelative("Moves"), phase1);
            SetMoves(so.FindProperty("_phase2").FindPropertyRelative("Moves"), phase2);
        });
        EditorUtility.SetDirty(profile);
    }

    static void SetMoves(SerializedProperty arr, AttackData[] moves)
    {
        if (moves == null) return;
        var list = moves.Where(m => m != null).ToArray();
        if (list.Length == 0) return;
        arr.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    /// <summary>アプリ専用の能力の攻撃データと名前を入れる（攻撃データが null なら何もしない）</summary>
    static void SetAppSkill(string skillPath, AttackData attack, string displayName)
    {
        if (attack == null) return;
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPath);
        if (skill == null) return;
        PlayerTestSceneBuilder.SetProps(skill, so =>
        {
            so.FindProperty("_attack").objectReferenceValue = attack;
            string name = so.FindProperty("_displayName").stringValue;
            if (string.IsNullOrEmpty(name) || name.EndsWith("（仮）")) so.FindProperty("_displayName").stringValue = displayName;
        });
        EditorUtility.SetDirty(skill);
    }

    static void AppBoss(SerializedProperty e, string label, string[] names, string[] bundles, BossController prefab, BossProfile profile)
    {
        e.FindPropertyRelative("Label").stringValue = label;
        Strings(e.FindPropertyRelative("AppNames"), names);
        Strings(e.FindPropertyRelative("BundleIdPrefixes"), bundles);
        e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("Profile").objectReferenceValue = profile;
        var g = e.FindPropertyRelative("GaugeMax");
        g.FindPropertyRelative("Min").intValue = 80;
        g.FindPropertyRelative("Max").intValue = 120;
        var w = e.FindPropertyRelative("Phase2Wait");
        w.FindPropertyRelative("Min").floatValue = 0.8f;
        w.FindPropertyRelative("Max").floatValue = 0.6f;
    }

    static void Strings(SerializedProperty arr, string[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
