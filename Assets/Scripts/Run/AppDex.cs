using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>図鑑の1アプリ分（保存する中身）</summary>
[Serializable]
public class AppDexEntry
{
    public string key;
    public string name;
    public string bundleId;
    /// <summary>初めて上位10アプリに入った日（yyyy-MM-dd）</summary>
    public string firstSeen;
    /// <summary>正常化した回数</summary>
    public int defeats;
    /// <summary>初めて正常化した日（yyyy-MM-dd）。まだなら空</summary>
    public string firstDefeated;
    /// <summary>正常化して手に入れた能力の名前（最後のもの）</summary>
    public string skill;

    public bool IsDefeated => defeats > 0;
    public AppUsageRow Row => new AppUsageRow { Name = name, BundleId = bundleId, Seconds = 1 };
}

/// <summary>
/// アプリ図鑑の記録（2026-11-17 まさぴ〜決定：何度も遊んでもらうための機能）。
/// ・使用時間の上位10アプリ（ボスの候補）に一度でも入ったアプリを載せる（最初はグレー）
/// ・そのアプリのボスを正常化すると、色が付く（回数・初めての日・手に入れた能力を残す）
/// ・この Mac に保存する（Application.persistentDataPath/app_dex.json）。ゲームを閉じても残る
/// 同じアプリは名前でまとめる（対応表のアプリは表の名前「Chrome」「VS Code」、ほかはスクリーンタイムの名前）。
/// </summary>
public static class AppDex
{
    [Serializable]
    class Data
    {
        public List<AppDexEntry> entries = new List<AppDexEntry>();
    }

    static Data _data;

    /// <summary>
    /// 【確認用】true の間は、図鑑に何も書かない（2026-11-18：遊ぶボスを選ぶ確認用のラン。本物の app_dex.json を変えないため）
    /// </summary>
    public static bool Suspended { get; set; }

    static string FilePath => Path.Combine(Application.persistentDataPath, "app_dex.json");

    public static IReadOnlyList<AppDexEntry> Entries => Load().entries;
    public static int Count => Load().entries.Count;
    public static int DefeatedCount => Load().entries.Count(e => e.IsDefeated);

    /// <summary>アプリのキー（名前を小文字にしたもの）</summary>
    public static string KeyOf(string name) => "dex_" + (name ?? "").Trim().ToLowerInvariant();

    public static AppDexEntry Find(string name)
    {
        string key = KeyOf(name);
        return Load().entries.FirstOrDefault(e => e.key == key);
    }

    /// <summary>ボスの候補（上位10アプリ）を載せる。まだ載っていないアプリだけ足す</summary>
    public static void RecordSeen(IEnumerable<(string name, string bundleId)> apps)
    {
        if (Suspended) return;
        Data data = Load();
        bool changed = false;
        foreach (var (name, bundleId) in apps)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            string key = KeyOf(name);
            AppDexEntry e = data.entries.FirstOrDefault(x => x.key == key);
            if (e != null)
            {
                if (string.IsNullOrEmpty(e.bundleId) && !string.IsNullOrEmpty(bundleId)) { e.bundleId = bundleId; changed = true; }
                continue;
            }
            data.entries.Add(new AppDexEntry { key = key, name = name.Trim(), bundleId = bundleId ?? "", firstSeen = Today(), skill = "" });
            changed = true;
        }
        if (changed) Save();
    }

    /// <summary>正常化した。初めての正常化なら true（リザルトで光らせる）</summary>
    public static bool RecordDefeat(string name, string bundleId, SkillDefinition reward)
    {
        if (Suspended || string.IsNullOrWhiteSpace(name)) return false;
        RecordSeen(new[] { (name, bundleId) });
        AppDexEntry e = Find(name);
        bool first = e.defeats == 0;
        e.defeats++;
        if (first) e.firstDefeated = Today();
        if (reward != null) e.skill = reward.DisplayName;
        Save();
        return first;
    }

    /// <summary>記録を消す（確認用）</summary>
    public static void Clear()
    {
        _data = new Data();
        Save();
    }

    static Data Load()
    {
        if (_data != null) return _data;
        _data = new Data();
        try
        {
            if (File.Exists(FilePath)) _data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"図鑑の記録を読めませんでした（新しく始めます）\n{e.Message}");
            _data = new Data();
        }
        if (_data.entries == null) _data.entries = new List<AppDexEntry>();
        return _data;
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Load(), true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"図鑑の記録を保存できませんでした\n{e.Message}");
        }
    }

    static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
}
