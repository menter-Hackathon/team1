using System;
using System.Collections.Generic;

/// <summary>
/// 【仮】アプリごとのステージの出来事を登録する所（2026-11-19：アプリのキット）。
/// キットのファイルが [RuntimeInitializeOnLoadMethod] で Register を呼ぶ。StagePacer は、場面③で
/// まずここ（そのステージのアプリの出来事）を見て、無ければスキンの出来事（StageEventTable）を使う。
/// ステージ全体・ボス戦にもかかる決まり（Steam の実績解除など）は、キットが StagePacer.Started を購読して
/// pacer.AppName／BundleId を見て、自分で部品を付ける（ここには登録しない）
/// </summary>
public static class AppStageEvents
{
    struct Entry
    {
        public string Label;
        public string[] Names;
        public string[] Bundles;
        public Func<int, bool, StageEvent> Factory; // (ステージの番号, ラスボスか) → 出来事
        public int ExtraZako;
        public Func<bool> Active;  // 2026-11-19：false の間は無いのと同じ（キットのスイッチ）
        public string KitLabel;    // 2026-11-19：名前が空なら、AppKits に登録した名前で比べる
    }

    static readonly List<Entry> Entries = new List<Entry>();

    /// <summary>
    /// アプリの出来事を登録する。names はアプリの名前（単語ごとに比べる）、bundles はバンドルIDの前方一致。
    /// factory は場面③のたびに呼ばれ、新しい出来事を返す（null なら、スキンの出来事に任せる）
    /// </summary>
    public static void Register(string label, string[] names, string[] bundles, Func<int, bool, StageEvent> factory, int extraZako = 0)
    {
        Entries.RemoveAll(e => e.Label == label);
        Entries.Add(new Entry { Label = label, Names = names ?? Array.Empty<string>(), Bundles = bundles ?? Array.Empty<string>(), Factory = factory, ExtraZako = extraZako });
    }

    /// <summary>
    /// 【仮】キットの出来事を登録する（2026-11-19：StageEvents.Register が呼ぶ）。active が false の間は、無いのと同じ。
    /// names・bundles が空なら、AppKits に kitLabel で登録した名前で比べる
    /// </summary>
    public static void Register(string label, string[] names, string[] bundles, Func<int, bool, StageEvent> factory, int extraZako, Func<bool> active, string kitLabel)
    {
        Entries.RemoveAll(e => e.Label == label);
        Entries.Add(new Entry
        {
            Label = label, Names = names ?? Array.Empty<string>(), Bundles = bundles ?? Array.Empty<string>(),
            Factory = factory, ExtraZako = extraZako, Active = active, KitLabel = kitLabel,
        });
    }

    static bool Match(Entry e, string appName, string bundleId)
    {
        if (e.Active != null)
        {
            bool on;
            try { on = e.Active(); }
            catch (Exception) { on = false; }
            if (!on) return false;
        }
        var row = new AppUsageRow { Name = appName ?? "", BundleId = bundleId ?? "", Seconds = 1 };
        if (e.Names.Length == 0 && e.Bundles.Length == 0 && !string.IsNullOrEmpty(e.KitLabel))
            return AppMatch.Matches(row, AppKits.NamesOf(e.KitLabel), AppKits.BundlesOf(e.KitLabel));
        return AppMatch.Matches(row, e.Names, e.Bundles);
    }

    /// <summary>そのアプリの出来事（無ければ null）</summary>
    public static StageEvent For(string appName, string bundleId, int stageNumber, bool isFinal)
    {
        foreach (var e in Entries)
        {
            if (e.Factory != null && Match(e, appName, bundleId)) return e.Factory(stageNumber, isFinal);
        }
        return null;
    }

    /// <summary>そのアプリの出来事の雑魚の上乗せ（無ければ -1＝スキンの表に任せる）</summary>
    public static int ExtraZako(string appName, string bundleId)
    {
        foreach (var e in Entries)
        {
            if (e.Factory != null && Match(e, appName, bundleId)) return e.ExtraZako;
        }
        return -1;
    }

    /// <summary>そのアプリが登録されているか</summary>
    public static bool Has(string appName, string bundleId)
    {
        foreach (var e in Entries) if (Match(e, appName, bundleId)) return true;
        return false;
    }
}
