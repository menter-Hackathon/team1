using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class MacScreenTime
{
    // macOS のスクリーンタイム → アプリとWebサイトのアクティビティを開く。
    const string UsageSettingsUrl = "x-apple.systempreferences:com.apple.Screen-Time-Settings.extension?path=app-usage";
    const string HelperBundle = "Team1 Screen Time Capture.app";

    [Serializable]
    sealed class ScanResult
    {
        public ScanEntry[] items = Array.Empty<ScanEntry>();
    }

    [Serializable]
    sealed class ScanEntry
    {
        public string name = "";
        public int seconds = 0;
    }

    internal static bool CanOpen => Application.platform == RuntimePlatform.OSXEditor
        || Application.platform == RuntimePlatform.OSXPlayer;
    internal static bool CanScan => CanOpen && (Application.isEditor || File.Exists(PlayerHelper(Application.dataPath)));

    public static void Open()
    {
        if (CanOpen) Application.OpenURL(UsageSettingsUrl);
    }

    internal static Task<(int ExitCode, string Output, string Error)> ScanAsync()
    {
        string dataPath = Application.dataPath;
        if (Application.isEditor)
            return Task.Run(() => RunProjectTool(Path.GetDirectoryName(dataPath), "--scan", UsageSettingsUrl));

        string helper = PlayerHelper(dataPath);
        return Task.Run(() => RunProcess(helper, "--scan", UsageSettingsUrl));
    }

    internal static List<AppUsage> ReadUsages(string json, AppCatalog catalog, out List<(string Name, int Seconds)> topApps)
    {
        topApps = new List<(string Name, int Seconds)>(3);
        var usages = new List<AppUsage>();
        var seen = new HashSet<AppEntry>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var apps = catalog == null ? Array.Empty<AppEntry>() : catalog.Apps;

        var result = JsonUtility.FromJson<ScanResult>("{\"items\":" + json + "}");
        var rows = result?.items ?? Array.Empty<ScanEntry>();
        foreach (var row in rows.Where(row => row != null && row.seconds > 0 && row.seconds <= 7 * 24 * 3600 &&
            !string.IsNullOrWhiteSpace(row.name)).OrderByDescending(row => row.seconds))
        {
            string name = row.name.Trim();
            if (topApps.Count < 3) topApps.Add((name, row.seconds));
            bool matched = false;
            foreach (var app in apps)
            {
                if (app == null || app.BossPrefab == null || app.Profile == null || app.BundleIdPrefixes == null) continue;
                if (!MatchesDisplayName(name, app.DisplayName) &&
                    (app.ScreenTimeNames == null || !Array.Exists(app.ScreenTimeNames, alias => MatchesDisplayName(name, alias)))) continue;
                string id = Array.Find(app.BundleIdPrefixes, prefix => !string.IsNullOrWhiteSpace(prefix));
                if (id != null && seen.Add(app)) usages.Add(new AppUsage(id, app.DisplayName, row.seconds));
                matched = true;
                break;
            }
            // 2026-11-17（まさぴ〜）：対応表に無いアプリも、名前だけで渡す（上位10アプリからランダムに3つをボスにするため）。
            // 除外（Finder・設定・iPhone のアプリなど）と代用ボスは、ゲーム側（AbilityCatalog・RunPlanner）で決める
            if (!matched && seenNames.Add(name)) usages.Add(new AppUsage("", name, row.seconds));
        }
        return usages;
    }

    static bool MatchesDisplayName(string name, string label) => !string.IsNullOrWhiteSpace(label) &&
        (name.Equals(label, StringComparison.OrdinalIgnoreCase) || name.StartsWith(label + " -", StringComparison.OrdinalIgnoreCase));

    public static (int ExitCode, string Output, string Error) RunProjectTool(string project, string argument, string destination = null)
    {
        string script = Path.Combine(project, "Tools", "ScreenTimeCapture", "run.sh");
        string build = Path.Combine(project, "Library", "ScreenTimeCapture", argument == "--embed" ? "Build" : "Editor");
        return destination == null ? RunProcess("/bin/sh", script, build, argument) : RunProcess("/bin/sh", script, build, argument, destination);
    }

    static string PlayerHelper(string dataPath) => Path.Combine(dataPath, "Helpers", HelperBundle, "Contents", "MacOS", "ScreenTimeCapture");

    static (int ExitCode, string Output, string Error) RunProcess(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo(path, string.Join(" ", Array.ConvertAll(arguments, Quote)))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var process = Process.Start(start);
        if (process == null) throw new InvalidOperationException("撮影用アプリを起動できませんでした。");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult().Trim(), error.Trim());
    }

    static string Quote(string argument) => "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
