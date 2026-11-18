using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// アプリ専用の能力とラスボスを用意するエディタ拡張（2026-11-18 まさぴ〜決定）。
/// メニュー「Tools/Team1/アプリ専用の能力とラスボスを用意」か、コマンドライン（-executeMethod FinalBossBuilder.Build）から実行する。
/// ・アプリ専用の能力：AbilitiesBuilder.EnsureAppSkills（Zoom・Finder・Premiere Pro。中身は【仮】）
/// ・ラスボス Terminal：
///   ・ボスの設定 Boss_Terminal（無ければ VS Code の設定を複製し、技に Chrome の技を足す。手に入る能力は無し。中身は【仮】）
///   ・色 Theme_Terminal（黒い画面と緑の文字）
///   ・対応アプリ表の「ラスボス」の欄（空のときだけ入れる）：プレハブは VS Code のボス（着ているウィンドウは Terminal のアイコンから作る）、
///     ステージは代用ボスと同じ1画面。強さはいつも最大なので、ゲージ・待ち時間は範囲の最大側が使われる
/// すでにあるアセットや、手で直した欄は作り直さない。
/// </summary>
public static class FinalBossBuilder
{
    const string AppCatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string TerminalProfilePath = BossFolder + "/Boss_Terminal.asset";

    [MenuItem("Tools/Team1/アプリ専用の能力とラスボスを用意")]
    public static void Build()
    {
        AbilitiesBuilder.EnsureAppSkills();
        BuildFinalBoss();
        AssetDatabase.SaveAssets();
        Debug.Log("アプリ専用の能力とラスボスを用意しました");
    }

    public static void BuildFinalBoss()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(AppCatalogPath);
        if (catalog == null) return;
        AppEntry chrome = catalog.Apps.FirstOrDefault(a => a != null && a.DisplayName == "Chrome");
        AppEntry vscode = catalog.Apps.FirstOrDefault(a => a != null && a.DisplayName == "VS Code");
        if (chrome == null || vscode == null || chrome.Profile == null || vscode.Profile == null || vscode.BossPrefab == null)
        {
            Debug.LogError("対応アプリ表に Chrome と VS Code のボスがありません（先に「ランのシーンを作成」を実行してください）");
            return;
        }

        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(TerminalProfilePath);
        if (profile == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(vscode.Profile), TerminalProfilePath);
            profile = AssetDatabase.LoadAssetAtPath<BossProfile>(TerminalProfilePath);
            PlayerTestSceneBuilder.SetProps(profile, so =>
            {
                so.FindProperty("_displayName").stringValue = "Terminal";
                so.FindProperty("_reward").objectReferenceValue = null; // 最後のボスなので、能力は無し
                AddMoves(so.FindProperty("_phase1.Moves"), chrome.Profile.Phase1.Moves);
                AddMoves(so.FindProperty("_phase2.Moves"), chrome.Profile.Phase2.Moves);
            });
            EditorUtility.SetDirty(profile);
        }

        AppTheme theme = ArtAssets.GetOrCreateTerminalTheme();
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var f = so.FindProperty("_finalBoss");
            if (f.FindPropertyRelative("Profile").objectReferenceValue != null) return; // 入っていれば、手で直した値を残す
            f.FindPropertyRelative("DisplayName").stringValue = "Terminal";
            SetStrings(f.FindPropertyRelative("BundleIdPrefixes"), "com.apple.Terminal");
            SetStrings(f.FindPropertyRelative("ScreenTimeNames"), "ターミナル");
            f.FindPropertyRelative("Category").intValue = (int)SkillCategory.Dev;
            f.FindPropertyRelative("BossPrefab").objectReferenceValue = vscode.BossPrefab;
            f.FindPropertyRelative("Profile").objectReferenceValue = profile;
            f.FindPropertyRelative("SpawnPosition").vector2Value = vscode.SpawnPosition;
            f.FindPropertyRelative("StageScene").stringValue = catalog.StandInStageScene;
            f.FindPropertyRelative("Theme").objectReferenceValue = theme;
            f.FindPropertyRelative("GaugeMax.Min").intValue = 140;
            f.FindPropertyRelative("GaugeMax.Max").intValue = 160;
            f.FindPropertyRelative("Phase2Wait.Min").floatValue = 0.6f;
            f.FindPropertyRelative("Phase2Wait.Max").floatValue = 0.5f;
        });
        EditorUtility.SetDirty(catalog);
    }

    // 技の一覧の後ろに、まだ入っていない技を足す
    static void AddMoves(SerializedProperty arr, AttackData[] moves)
    {
        if (moves == null) return;
        foreach (AttackData m in moves)
        {
            if (m == null) continue;
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++) has |= arr.GetArrayElementAtIndex(i).objectReferenceValue == m;
            if (has) continue;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = m;
        }
    }

    static void SetStrings(SerializedProperty arr, params string[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
