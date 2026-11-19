using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Team1.Achievements.EditorTools
{
    /// <summary>
    /// CSV から ScriptableObject を作る：
    /// ・achievements.csv → Definitions/Ach_&lt;ID&gt;.asset（17個）
    /// ・app_categories.csv → Definitions/AppCategoryTable.asset（1個）
    /// ・まとめ → Resources/AchievementDatabase.asset（ゲームが読む）
    /// もう一度実行すると、同じアセットを上書きする（Inspector で変えた条件の値は、条件の種類が同じなら残す）。
    /// </summary>
    public static class AchievementCsvImporter
    {
        public const string Root = "Assets/_Project/AchievementSystem";
        const string DataDir = Root + "/Data";
        const string DefinitionDir = Root + "/Definitions";
        const string ResourcesDir = Root + "/Resources";

        [MenuItem("Tools/Achievements/CSV から定義を作る", priority = 0)]
        public static void Import()
        {
            AchievementIconImport.ReimportAll(); // アイコンを Sprite にしてから読む
            EnsureFolder(Root, "Definitions");
            EnsureFolder(Root, "Resources");

            var warnings = new List<string>();
            AppCategoryTable table = ImportCategories(warnings);
            List<AchievementDefinition> definitions = ImportAchievements(warnings);

            string dbPath = ResourcesDir + "/AchievementDatabase.asset";
            var db = LoadOrCreate<AchievementDatabase>(dbPath);
            db.EditorSet(definitions, table,
                Sprite(Root + "/Frames/AchFrame_Bronze.png", warnings),
                Sprite(Root + "/Frames/AchFrame_Silver.png", warnings),
                Sprite(Root + "/Frames/AchFrame_Gold.png", warnings));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            string message = $"実績 {definitions.Count}件、カテゴリ表 {table.Rows.Count}行 を作りました。";
            if (warnings.Count > 0) Debug.LogWarning("[実績] " + message + "\n" + string.Join("\n", warnings), db);
            else Debug.Log("[実績] " + message, db);
            EditorGUIUtility.PingObject(db);
        }

        static List<AchievementDefinition> ImportAchievements(List<string> warnings)
        {
            var list = new List<AchievementDefinition>();
            var csv = CsvTable.Parse(File.ReadAllText(DataDir + "/achievements.csv", Encoding.UTF8));
            foreach (var row in csv.Rows)
            {
                string id = csv.Get(row, "id");
                if (string.IsNullOrEmpty(id)) continue;
                if (!AchievementLabels.TryParseCategory(csv.Get(row, "category"), out var category)) warnings.Add($"{id}：カテゴリ「{csv.Get(row, "category")}」が分かりません");
                if (!System.Enum.TryParse(csv.Get(row, "rank"), true, out AchievementRank rank)) warnings.Add($"{id}：ランク「{csv.Get(row, "rank")}」が分かりません");
                if (!System.Enum.TryParse(csv.Get(row, "trigger"), true, out AchievementTrigger trigger)) warnings.Add($"{id}：タイミング「{csv.Get(row, "trigger")}」が分かりません");
                string type = csv.Get(row, "condition_type");
                if (AchievementConditions.Create(type) == null) warnings.Add($"{id}：条件の種類「{type}」が分かりません");
                if (!double.TryParse(csv.Get(row, "target"), NumberStyles.Float, CultureInfo.InvariantCulture, out double target))
                {
                    warnings.Add($"{id}：目標値「{csv.Get(row, "target")}」が数ではありません（1 にします）");
                    target = 1;
                }
                var def = LoadOrCreate<AchievementDefinition>($"{DefinitionDir}/Ach_{id}.asset");
                def.EditorSetup(id, csv.Get(row, "name"), csv.Get(row, "description"), category, rank, trigger, type, target,
                    csv.Get(row, "game_effect"),
                    Sprite($"{Root}/Icons/{csv.Get(row, "icon")}", warnings),
                    Sprite($"{Root}/Locked/{csv.Get(row, "icon_locked")}", warnings));
                EditorUtility.SetDirty(def);
                list.Add(def);
            }
            return list;
        }

        static AppCategoryTable ImportCategories(List<string> warnings)
        {
            var csv = CsvTable.Parse(File.ReadAllText(DataDir + "/app_categories.csv", Encoding.UTF8));
            var rows = new List<AppCategoryTable.Row>();
            foreach (var row in csv.Rows)
            {
                string name = csv.Get(row, "app_name");
                if (string.IsNullOrEmpty(name)) continue;
                string label = csv.Get(row, "category");
                var category = AppCategoryMap.ParseCategory(label);
                if (category == AppUsageCategory.Other && label != "その他") warnings.Add($"{name}：カテゴリ「{label}」が分かりません（その他にします）");
                rows.Add(new AppCategoryTable.Row
                {
                    appName = name,
                    category = category,
                    detoxTarget = csv.Get(row, "detox_target").Equals("TRUE", System.StringComparison.OrdinalIgnoreCase),
                    matchKey = csv.Get(row, "match_key"),
                    note = csv.Get(row, "note"),
                });
            }
            var table = LoadOrCreate<AppCategoryTable>(DefinitionDir + "/AppCategoryTable.asset");
            table.EditorSetRows(rows);
            EditorUtility.SetDirty(table);
            return table;
        }

        static Sprite Sprite(string path, List<string> warnings)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) warnings.Add($"画像が見つかりません（Sprite になっていない？）：{path}");
            return sprite;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
