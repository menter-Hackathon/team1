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

    /// <summary>まとめる前の記録の控え（1回だけ書く）</summary>
    static string BackupPath => Path.Combine(Application.persistentDataPath, "app_dex.before-merge.json");

    /// <summary>
    /// 同じアプリの記録を1つにまとめる（2026-11-20 まさぴ〜決定：「Excel」と「Microsoft Excel」、「Chrome」と「Google Chrome-ウェブ・・」、
    /// 「Unity」と「Unity Hub」が別々に載っていたため）。identity：記録 → そのアプリの今の名前とバンドルID（ランが図鑑に書く名前と同じ決め方）。
    /// ・倒した回数は足す。初めて載った日・初めて倒した日は早い方。能力は、もともと今の名前だった記録の物（空なら、ほかの記録の物）
    /// ・何度呼んでも同じ（まとめる物が無ければ書かない）。まとめるときは、最初の1回だけ app_dex.before-merge.json に控えを残す
    /// ・確認用のラン（Suspended）の間は何もしない。まとめた記録の数を返す
    /// </summary>
    public static int MergeDuplicates(Func<AppDexEntry, (string name, string bundleId)> identity)
    {
        if (Suspended || identity == null) return 0;
        Data data = Load();
        var result = new List<AppDexEntry>();
        var fromOwnName = new HashSet<AppDexEntry>(); // もともと今の名前だった記録（その能力を優先する）
        int merged = 0;
        bool changed = false;
        foreach (AppDexEntry e in data.entries)
        {
            if (e == null) { changed = true; continue; }
            (string name, string bundleId) id;
            try { id = identity(e); }
            catch (Exception ex) { Debug.LogException(ex); id = (e.name, e.bundleId); }
            if (string.IsNullOrWhiteSpace(id.name)) id = (e.name, e.bundleId);
            string key = KeyOf(id.name);
            bool own = e.key == key;
            AppDexEntry target = result.FirstOrDefault(x => x.key == key);
            if (target == null)
            {
                if (!own)
                {
                    e.key = key;
                    e.name = id.name.Trim();
                    if (!string.IsNullOrEmpty(id.bundleId)) e.bundleId = id.bundleId;
                    changed = true;
                }
                if (own) fromOwnName.Add(e);
                result.Add(e);
                continue;
            }
            target.defeats += e.defeats;
            target.firstSeen = Earlier(target.firstSeen, e.firstSeen);
            target.firstDefeated = Earlier(target.firstDefeated, e.firstDefeated);
            if (string.IsNullOrEmpty(target.bundleId)) target.bundleId = e.bundleId ?? "";
            bool takeSkill = !string.IsNullOrEmpty(e.skill) && (string.IsNullOrEmpty(target.skill) || (own && !fromOwnName.Contains(target)));
            if (takeSkill) target.skill = e.skill;
            if (own) fromOwnName.Add(target);
            merged++;
            changed = true;
        }
        if (!changed) return 0;
        try
        {
            if (merged > 0 && File.Exists(FilePath) && !File.Exists(BackupPath)) File.Copy(FilePath, BackupPath);
        }
        catch (Exception ex) { Debug.LogWarning($"図鑑の記録の控えを残せませんでした\n{ex.Message}"); }
        data.entries = result;
        Save();
        if (merged > 0) Debug.Log($"図鑑：同じアプリの記録を {merged} 件まとめました（控え：{BackupPath}）");
        return merged;
    }

    /// <summary>日付（yyyy-MM-dd）の早い方。空は無いものとする</summary>
    static string Earlier(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? "";
        if (string.IsNullOrEmpty(b)) return a;
        return string.CompareOrdinal(a, b) <= 0 ? a : b;
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
