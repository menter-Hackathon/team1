using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// アプリのキットを全部集めて、アセットにする（2026-11-19）。メニュー「Tools/Team1/アプリのキットを用意」・-executeMethod AppKitsBuilder.Build。
/// ・ボス：Boss_Chrome か Boss_VSCode を複製して Boss_Kit_〇〇 を作り、技を入れる。対応アプリの表の「代用ボスの専用の技」に入れる（同じ名前の行は上書き）
/// ・能力：Skill_App_〇〇 を作り、能力の表の「アプリ専用の能力」に入れる（同じ名前の行は上書き）
/// ・最後に IAppKitFixup を全部呼ぶ
/// キットの攻撃データは、キットが BossSkillAssets の道具で作る（無ければ作る。数値を入れ直すときは消してから）
/// </summary>
public static class AppKitsBuilder
{
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string SkillFolder = "Assets/ScriptableObjects/Skills";

    /// <summary>
    /// キットの能力のアセットの道（ふつうは Skill_App_&lt;ラベル&gt;）。
    /// 大文字・小文字だけ違う名前の別のアセットがあるとき（hiroto さんの Skill_App_Line と、キットの Skill_App_LINE など）は、
    /// Mac では同じファイルになってしまうので、Skill_App_&lt;ラベル&gt;_Kit を使う（2026-11-20）
    /// </summary>
    static string KitSkillPath(string label)
    {
        string name = $"Skill_App_{label}.asset";
        string kitPath = $"{SkillFolder}/Skill_App_{label}_Kit.asset";
        if (System.IO.File.Exists(kitPath)) return kitPath;
        if (System.IO.Directory.Exists(SkillFolder))
        {
            foreach (string f in System.IO.Directory.GetFiles(SkillFolder, "*.asset"))
            {
                string onDisk = System.IO.Path.GetFileName(f);
                if (onDisk.Equals(name, StringComparison.OrdinalIgnoreCase) && !onDisk.Equals(name, StringComparison.Ordinal)) return kitPath;
            }
        }
        return $"{SkillFolder}/{name}";
    }
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";

    [MenuItem("Tools/Team1/アプリのキットを用意")]
    public static void BuildFromMenu()
    {
        Build();
        Debug.Log("アプリのキットを用意しました");
    }

    public static void Build()
    {
        AppBossSkillsBuilder.Build(); // Finder・Premiere Pro・Terminal（先に）
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        var abilities = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilitiesBuilder.CatalogPath);
        BossController chromePrefab = catalog != null ? catalog.Apps.FirstOrDefault(a => a.DisplayName == "Chrome")?.BossPrefab : null;
        BossController vscodePrefab = catalog != null ? catalog.Apps.FirstOrDefault(a => a.DisplayName == "VS Code")?.BossPrefab : null;

        var kits = new List<IAppKit>();
        foreach (Type t in TypeCache.GetTypesDerivedFrom<IAppKit>())
        {
            if (t.IsAbstract || t.IsInterface || t.GetConstructor(Type.EmptyTypes) == null) continue;
            try { kits.Add((IAppKit)Activator.CreateInstance(t)); }
            catch (Exception e) { Debug.LogWarning($"キット {t.Name} を作れません：{e.Message}"); }
        }
        kits.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
        Debug.Log($"[AppKits] キット {kits.Count} 個：{string.Join(", ", kits.Select(k => k.Label))}");

        foreach (var kit in kits)
        {
            try { BuildKit(kit, catalog, abilities, kit.HoverBoss ? chromePrefab : vscodePrefab); }
            catch (Exception e) { Debug.LogError($"[AppKits] {kit.Label} を作れません：{e}"); }
        }
        try { WriteRuntimeFiles(kits); }
        catch (Exception e) { Debug.LogError($"[AppKits] 実行時の表（Resources/Kits）を書けません：{e}"); }

        foreach (Type t in TypeCache.GetTypesDerivedFrom<IAppKitFixup>())
        {
            if (t.IsAbstract || t.IsInterface || t.GetConstructor(Type.EmptyTypes) == null) continue;
            try { ((IAppKitFixup)Activator.CreateInstance(t)).Apply(); Debug.Log($"[AppKits] 直し {t.Name}"); }
            catch (Exception e) { Debug.LogError($"[AppKits] 直し {t.Name} に失敗：{e}"); }
        }
        if (catalog != null) EditorUtility.SetDirty(catalog);
        if (abilities != null) EditorUtility.SetDirty(abilities);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 実行時の表を書く（2026-11-19）：キットの名前（AppKits が読む。キットのファイルで AppKits.Register を忘れても、名前で見分けられる）と、
    /// カードの説明（SkillTexts が読む）。中身が変わったときだけ書く
    /// </summary>
    static void WriteRuntimeFiles(List<IAppKit> kits)
    {
        var index = new KitIndexFile { kits = kits.Select(k => new KitIndexRow { label = k.Label, names = k.AppNames ?? new string[0], bundles = k.BundleIds ?? new string[0] }).ToArray() };
        var texts = new KitSkillTextFile
        {
            texts = kits.OfType<IAppKitExtras>().Where(x => !string.IsNullOrEmpty(x.SkillDescription))
                .Select(x => new KitSkillTextRow { name = string.IsNullOrEmpty(((IAppKit)x).SkillName) ? ((IAppKit)x).Label : ((IAppKit)x).SkillName, description = x.SkillDescription, tags = x.SkillTags ?? new string[0] })
                .ToArray(),
        };
        PlayerTestSceneBuilder.EnsureFolder("Assets/Resources/Kits");
        WriteIfChanged($"Assets/Resources/{AppKits.IndexResource}.json", JsonUtility.ToJson(index, true));
        WriteIfChanged($"Assets/Resources/{SkillTexts.KitResource}.json", JsonUtility.ToJson(texts, true));
    }

    static void WriteIfChanged(string path, string json)
    {
        string full = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
        if (System.IO.File.Exists(full) && System.IO.File.ReadAllText(full) == json) return;
        System.IO.File.WriteAllText(full, json);
        AssetDatabase.ImportAsset(path);
    }

    static void BuildKit(IAppKit kit, AppCatalog catalog, AbilityCatalog abilities, BossController prefab)
    {
        string label = kit.Label;
        BossProfile builtProfile = null;
        SkillDefinition builtSkill = null;
        // ── ボス ──
        AttackData[] p1 = kit.Phase1()?.Where(m => m != null).ToArray();
        if (p1 != null && p1.Length > 0)
        {
            AttackData[] p2 = kit.Phase2()?.Where(m => m != null).ToArray();
            if (p2 == null || p2.Length == 0) p2 = p1;
            string path = $"{BossFolder}/Boss_Kit_{label}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
            if (profile == null)
            {
                AssetDatabase.CopyAsset($"{BossFolder}/{(kit.HoverBoss ? "Boss_Chrome" : "Boss_VSCode")}.asset", path);
                profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
            }
            PlayerTestSceneBuilder.SetProps(profile, so =>
            {
                so.FindProperty("_displayName").stringValue = label;
                so.FindProperty("_reward").objectReferenceValue = null;
                Moves(so.FindProperty("_phase1").FindPropertyRelative("Moves"), p1);
                Moves(so.FindProperty("_phase2").FindPropertyRelative("Moves"), p2);
            });
            EditorUtility.SetDirty(profile);
            builtProfile = profile;
            if (catalog != null)
            {
                PlayerTestSceneBuilder.SetProps(catalog, so =>
                {
                    SerializedProperty e = Row(so.FindProperty("_appBosses"), label);
                    e.FindPropertyRelative("Label").stringValue = label;
                    Strings(e.FindPropertyRelative("AppNames"), KitAliases.WithNames(label, kit.AppNames)); // 19章の別名も（2026-11-19）
                    Strings(e.FindPropertyRelative("BundleIdPrefixes"), KitAliases.WithBundles(label, kit.BundleIds));
                    e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
                    e.FindPropertyRelative("Profile").objectReferenceValue = profile;
                    var g = e.FindPropertyRelative("GaugeMax");
                    var w = e.FindPropertyRelative("Phase2Wait");
                    if (kit is IAppKitExtras ex)
                    {
                        // 足した欄があれば、いつも入れ直す（2026-11-19）
                        g.FindPropertyRelative("Min").intValue = ex.GaugeMax.Min; g.FindPropertyRelative("Max").intValue = ex.GaugeMax.Max;
                        w.FindPropertyRelative("Min").floatValue = ex.Phase2Wait.Min; w.FindPropertyRelative("Max").floatValue = ex.Phase2Wait.Max;
                    }
                    if (g.FindPropertyRelative("Max").intValue == 0) { g.FindPropertyRelative("Min").intValue = 80; g.FindPropertyRelative("Max").intValue = 120; }
                    if (w.FindPropertyRelative("Min").floatValue == 0f) { w.FindPropertyRelative("Min").floatValue = 0.8f; w.FindPropertyRelative("Max").floatValue = 0.6f; }
                    var fk = e.FindPropertyRelative("FromKit"); // キットの行の印（AppKits.Enabled = false で使わない。2026-11-19）
                    if (fk != null) fk.boolValue = true;
                });
            }
        }

        // ── 能力 ──
        AttackData skillAttack = kit.PlayerSkill();
        if (skillAttack != null && abilities != null)
        {
            string path = KitSkillPath(label);
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
            {
                PlayerTestSceneBuilder.EnsureFolder(SkillFolder);
                skill = ScriptableObject.CreateInstance<SkillDefinition>();
                AssetDatabase.CreateAsset(skill, path);
            }
            PlayerTestSceneBuilder.SetProps(skill, so =>
            {
                so.FindProperty("_displayName").stringValue = string.IsNullOrEmpty(kit.SkillName) ? label : kit.SkillName;
                so.FindProperty("_category").intValue = (int)kit.Category;
                so.FindProperty("_attack").objectReferenceValue = skillAttack;
                if (kit is IAppKitExtras ex)
                {
                    // 能力の見た目のスキン・アイコン（2026-11-19）
                    var sk = so.FindProperty("_skin");
                    if (sk != null) sk.intValue = (int)ex.SkillSkin;
                    if (ex.SkillIcon != null) so.FindProperty("_icon").objectReferenceValue = ex.SkillIcon;
                }
            });
            EditorUtility.SetDirty(skill);
            builtSkill = skill;
            PlayerTestSceneBuilder.SetProps(abilities, so =>
            {
                SerializedProperty e = Row(so.FindProperty("_appSkills"), label);
                e.FindPropertyRelative("Label").stringValue = label;
                e.FindPropertyRelative("Skill").objectReferenceValue = skill;
                Strings(e.FindPropertyRelative("AppNames"), KitAliases.WithNames(label, kit.AppNames)); // 19章の別名も（2026-11-19）
                Strings(e.FindPropertyRelative("BundleIdPrefixes"), KitAliases.WithBundles(label, kit.BundleIds));
                var fk = e.FindPropertyRelative("FromKit"); // キットの行の印（2026-11-19）
                if (fk != null) fk.boolValue = true;
            });
        }

        // 足したい設定（2026-11-19）
        if (kit is IAppKitExtras extras)
        {
            try { extras.AfterBuild(builtProfile, builtSkill); }
            catch (Exception e) { Debug.LogError($"[AppKits] {label} の AfterBuild に失敗：{e}"); }
        }
    }

    /// <summary>表の中の、Label が同じ行（無ければ足した行）</summary>
    static SerializedProperty Row(SerializedProperty arr, string label)
    {
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("Label").stringValue == label) return e;
        }
        arr.arraySize++;
        var added = arr.GetArrayElementAtIndex(arr.arraySize - 1);
        return added;
    }

    static void Moves(SerializedProperty arr, AttackData[] moves)
    {
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
    }

    static void Strings(SerializedProperty arr, string[] values)
    {
        values = values ?? Array.Empty<string>();
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
