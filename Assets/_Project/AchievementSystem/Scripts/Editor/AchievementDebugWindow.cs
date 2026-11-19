using System.IO;
using UnityEditor;
using UnityEngine;

namespace Team1.Achievements.EditorTools
{
    /// <summary>
    /// 実績の確認用（Tools ＞ Achievements ＞ デバッグ）。
    /// ・偽のスクリーンタイムで起動時スキャンを再生する（プリセット、または JSON ファイル）
    /// ・実績を強制的に解除する（プレイ中は吹き出しも出る）
    /// ・セーブを消す
    /// ・再生したときの起動時スキャンに偽データを使う（EditorPrefs。AchievementService が読む）
    /// </summary>
    public class AchievementDebugWindow : EditorWindow
    {
        const string UseFakeKey = AchievementService.EditorUseFakeKey;
        const string PresetKey = AchievementService.EditorPresetKey;

        int _preset;
        Vector2 _scroll;

        [MenuItem("Tools/Achievements/デバッグ", priority = 20)]
        static void Open() => GetWindow<AchievementDebugWindow>("実績デバッグ");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("セーブ", AchievementSaveStore.FilePath, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("偽のスクリーンタイム", EditorStyles.boldLabel);
            _preset = EditorGUILayout.Popup("プリセット", EditorPrefs.GetInt(PresetKey, 0), FakeScreenTimePresets.Names);
            EditorPrefs.SetInt(PresetKey, _preset);
            bool useFake = EditorGUILayout.ToggleLeft("再生したとき、起動時スキャンに偽データを使う", EditorPrefs.GetBool(UseFakeKey, false));
            EditorPrefs.SetBool(UseFakeKey, useFake);

            var service = Application.isPlaying ? AchievementService.Instance : null;
            using (new EditorGUI.DisabledScope(service == null))
            {
                if (GUILayout.Button("プリセットで起動時スキャンを再生"))
                {
                    var unlocks = service.ReplayLaunchScan(FakeScreenTimePresets.Build(_preset, System.DateTime.Now));
                    Debug.Log($"[実績] 起動時スキャンを再生：{unlocks.Count}件 解除");
                }
                if (GUILayout.Button("JSON ファイルで起動時スキャンを再生"))
                {
                    string path = EditorUtility.OpenFilePanel("偽のスクリーンタイム（JSON）", Path.GetDirectoryName(JsonScreenTimeSource.DefaultPath), "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var unlocks = service.RunLaunchScan(JsonScreenTimeSource.FromFile(path));
                        Debug.Log($"[実績] 起動時スキャンを再生（{Path.GetFileName(path)}）：{unlocks.Count}件 解除。{service.LastScanError}");
                    }
                }
            }
            if (service == null) EditorGUILayout.HelpBox("再生中に使えます。", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("強制的に解除", EditorStyles.boldLabel);
            if (service != null)
            {
                foreach (var spec in service.Engine.Specs)
                {
                    bool done = service.Engine.Save.IsUnlocked(spec.Id);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{spec.Id}  {spec.Name}", done ? "解除済み" : $"{AchievementGui.ProgressText(spec, service.Engine.Save.Find(spec.Id))}");
                        using (new EditorGUI.DisabledScope(done))
                            if (GUILayout.Button("解除", GUILayout.Width(60))) service.ForceUnlock(spec.Id);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("セーブ", EditorStyles.boldLabel);
            if (GUILayout.Button("実績のセーブを消す"))
            {
                if (EditorUtility.DisplayDialog("実績のセーブを消す", "解除の記録・進み具合・数えた日を消します（アプリ図鑑は消しません）。", "消す", "やめる"))
                {
                    if (service != null) service.ResetSave();
                    else AchievementSaveStore.Delete();
                    Debug.Log("[実績] セーブを消しました");
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }
    }
}
