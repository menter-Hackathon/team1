using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】アプリのキットの実行時の入口（2026-11-19。docs/app-kits-1-design.md E1）。担当0 の物。
// ・アセットを作る側は Editor の IAppKit／KitAssetBuilder（AppKitsBuilder が表に入れる）。
// ・実行時は、キットのファイル（Kits/<アプリ>/<アプリ>Kit.cs）が [RuntimeInitializeOnLoadMethod] で Register を呼ぶ。
//   StageRules・StageEvents・SkillTexts・キットごとのスイッチは、ここの「ラベル」で引く。
// ・安全のスイッチ：AppKits.Enabled = false で、キットが全部止まる（代用ボス・カテゴリの能力・スキンの出来事に戻る）。

/// <summary>キットの3つの部分（キットごとに切れる）</summary>
public enum KitPart { Boss, Skill, Rule }

/// <summary>アプリのキットの登録と、安全のスイッチ</summary>
public static class AppKits
{
    /// <summary>【仮】false にすると、キットが全部止まる（今までどおりに戻る）。発表の前に具合が悪ければ、ここを false にする</summary>
    public static bool Enabled = true;

    /// <summary>
    /// 【仮】キットごとに切る所（発表の前に、具合の悪いキットだけ止める）。例：{ "Slack", KitPart.Rule }。
    /// コードで足すか、AppKits.Set で切る
    /// </summary>
    static readonly List<(string label, KitPart part)> OffAtStart = new List<(string, KitPart)>
    {
        // ("Slack", KitPart.Rule),
    };

    class Entry
    {
        public string Label;
        public string[] Names;
        public string[] Bundles;
    }

    static readonly List<Entry> Entries = new List<Entry>();
    static readonly HashSet<string> Off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static bool _offLoaded;
    static bool _indexLoaded;

    /// <summary>AppKitsBuilder が書く、キットの名前の表（Resources の中の場所）</summary>
    public const string IndexResource = "Kits/KitIndex";

    /// <summary>
    /// キットを登録する（実行時）。label は IAppKit.Label と同じにする（例："Slack"）。
    /// appNames はアプリの名前（単語ごとに比べる）、bundleIds はバンドルIDの前方一致。同じラベルは上書き
    /// </summary>
    public static void Register(string label, string[] appNames, string[] bundleIds)
    {
        if (string.IsNullOrEmpty(label)) return;
        Entries.RemoveAll(e => string.Equals(e.Label, label, StringComparison.OrdinalIgnoreCase));
        // 19章の別名も足す（KitAliases）
        Entries.Add(new Entry { Label = label, Names = KitAliases.WithNames(label, appNames), Bundles = KitAliases.WithBundles(label, bundleIds) });
    }

    /// <summary>登録してあるキットのラベル</summary>
    public static IEnumerable<string> Labels
    {
        get
        {
            LoadIndex();
            foreach (var e in Entries) yield return e.Label;
        }
    }

    /// <summary>そのキットのその部分が動くか（Enabled と、キットごとのスイッチ）</summary>
    public static bool IsOn(string label, KitPart part)
    {
        if (!Enabled) return false;
        if (string.IsNullOrEmpty(label)) return true;
        LoadOff();
        return !Off.Contains(Key(label, part));
    }

    /// <summary>キットごとのスイッチを変える（on＝false で止める）</summary>
    public static void Set(string label, KitPart part, bool on)
    {
        if (string.IsNullOrEmpty(label)) return;
        LoadOff();
        if (on) Off.Remove(Key(label, part));
        else Off.Add(Key(label, part));
    }

    /// <summary>そのアプリのキットのラベル（登録が無ければ null）。Enabled は見ない</summary>
    public static string FindLabel(string appName, string bundleId)
    {
        var row = new AppUsageRow { Name = appName ?? "", BundleId = bundleId ?? "", Seconds = 1 };
        return FindLabel(row);
    }

    /// <summary>そのアプリのキットのラベル（登録が無ければ null）。Enabled は見ない</summary>
    public static string FindLabel(AppUsageRow row)
    {
        LoadIndex();
        foreach (var e in Entries)
        {
            if (AppMatch.Matches(row, e.Names, e.Bundles)) return e.Label;
        }
        return null;
    }

    /// <summary>そのアプリが、そのキットに当たるか</summary>
    public static bool Matches(string label, string appName, string bundleId)
    {
        Entry e = Get(label);
        if (e == null) return false;
        var row = new AppUsageRow { Name = appName ?? "", BundleId = bundleId ?? "", Seconds = 1 };
        return AppMatch.Matches(row, e.Names, e.Bundles);
    }

    /// <summary>登録した名前（無ければ空）</summary>
    public static string[] NamesOf(string label) => Get(label)?.Names ?? Array.Empty<string>();

    /// <summary>登録したバンドルID（無ければ空）</summary>
    public static string[] BundlesOf(string label) => Get(label)?.Bundles ?? Array.Empty<string>();

    /// <summary>今のステージのキットのラベル（キットの無いステージ・ステージの外は null）</summary>
    public static string CurrentLabel
    {
        get
        {
            StagePacer p = StagePacer.Current;
            if (p != null) return FindLabel(p.AppName, p.BundleId);
            StageRuleHost h = StageRuleHost.Current;
            return h != null ? h.Label : null;
        }
    }

    static Entry Get(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        LoadIndex();
        foreach (var e in Entries) if (string.Equals(e.Label, label, StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }

    static string Key(string label, KitPart part) => label.ToLowerInvariant() + "/" + part;

    /// <summary>ビルダーが書いた名前の表を読む（Register した物は上書きしない）。1回だけ</summary>
    static void LoadIndex()
    {
        if (_indexLoaded) return;
        _indexLoaded = true;
        try
        {
            var asset = Resources.Load<TextAsset>(IndexResource);
            if (asset == null) return;
            var file = JsonUtility.FromJson<KitIndexFile>(asset.text);
            if (file == null || file.kits == null) return;
            foreach (var r in file.kits)
            {
                if (r == null || string.IsNullOrEmpty(r.label)) continue;
                if (Entries.Exists(e => string.Equals(e.Label, r.label, StringComparison.OrdinalIgnoreCase))) continue;
                Entries.Add(new Entry { Label = r.label, Names = KitAliases.WithNames(r.label, r.names), Bundles = KitAliases.WithBundles(r.label, r.bundles) });
            }
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    static void LoadOff()
    {
        if (_offLoaded) return;
        _offLoaded = true;
        foreach (var (label, part) in OffAtStart) Off.Add(Key(label, part));
    }
}

/// <summary>キットの名前の表（AppKitsBuilder が Resources/Kits/KitIndex.json に書く）</summary>
[Serializable]
public class KitIndexFile
{
    public KitIndexRow[] kits;
}

[Serializable]
public class KitIndexRow
{
    public string label;
    public string[] names;
    public string[] bundles;
}
