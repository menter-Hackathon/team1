#if UNITY_EDITOR_OSX
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal sealed class ScreenTimeWindow : EditorWindow
{
    static bool _capturing;

    [MenuItem("Tools/Team1/スクリーンタイム")]
    static void ShowWindow() => GetWindow<ScreenTimeWindow>("スクリーンタイム");

    [PostProcessBuild(100)]
    static void AddScannerToBuild(BuildTarget target, string outputPath)
    {
        if (target != BuildTarget.StandaloneOSX) return;
        string project = Path.GetDirectoryName(Application.dataPath);
        var result = MacScreenTime.RunProjectTool(project, "--embed", Path.GetFullPath(outputPath));
        if (result.ExitCode != 0) throw new BuildFailedException("撮影用アプリを同梱できませんでした。\n" + result.Error);
    }

    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(_capturing))
        {
            if (GUILayout.Button("スクリーンタイムを開く")) MacScreenTime.Open();

            EditorGUILayout.HelpBox(
                "対象のMacと「先週」のアプリ一覧を表示してください。「システム設定」のウィンドウを選ぶと、撮影して使用時間の上位3件をConsoleに表示します。",
                MessageType.Info);

            if (GUILayout.Button(_capturing ? "撮影・読み取り中…" : "撮影して使用時間の上位3件を表示")) Capture();
        }
    }

    async void Capture()
    {
        _capturing = true;
        try
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            string folder = Path.Combine(project, "Temp", "ScreenTimeCaptures");
            Directory.CreateDirectory(folder);
            string png = Path.Combine(folder, $"screen-time-{Guid.NewGuid():N}.png");

            var result = await Task.Run(() => MacScreenTime.RunProjectTool(project, png));
            if (result.ExitCode == 2) return;
            if ((result.ExitCode != 0 && result.ExitCode != 3) || !File.Exists(png) || new FileInfo(png).Length == 0)
            {
                Debug.LogError($"スクリーンタイムを撮影できませんでした。\n{result.Error}");
                return;
            }

            if (result.ExitCode == 3)
                Debug.LogWarning($"スクリーンタイムの画像を読み取れませんでした。\n{result.Error}\n{png}");
            else if (string.IsNullOrWhiteSpace(result.Output))
                Debug.LogWarning($"画像からアプリ名と使用時間を読み取れませんでした。アプリ一覧が見えていることを確認してください。\n{png}");
            else
                Debug.Log($"[スクリーンタイム] 使用時間 Top 3（画像内）\n{result.Output}\n{png}");
            EditorUtility.RevealInFinder(png);
        }
        catch (Exception error)
        {
            Debug.LogError($"スクリーンタイムを撮影できませんでした。\n{error.Message}");
        }
        finally
        {
            _capturing = false;
            if (this != null) Repaint();
        }
    }

}
#endif
