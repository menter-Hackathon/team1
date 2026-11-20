using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// アプリ図鑑から戦う（2026-11-20 まさぴ〜「確認用に入っているアプリたちをアプリ図鑑の画面にマージし、アプリ図鑑画面から戦いに行けるようにしてください」）。
/// もとはタイトルの左下の確認用の板（BossSelectDebug。開発用のビルドだけ）。板は消して、ここにまとめた。
/// ・Roster：戦えるアプリの全部（対応表のボス → 代用ボスの専用の技 → アプリ専用の能力 → キット → ラスボス。同じアプリは1つ）
/// ・WithRoster：図鑑の記録に、まだ載っていないアプリを足した並び。足した物はメモリの中だけ（app_dex.json には書かない。灰色）
/// ・Fight：そのアプリのボスだけのランを始める（能力えらび → そのボスのステージ → リザルト）。【提案】灰色のアプリとも戦える
/// ・練習のラン（IsPractice）の間は、図鑑・実績に書かない（AppDex.Suspended）。クリア後のウイルスが消える演出もしない。
///   仮の使用データはデスクトップの書類に残さず（GameSession.PrepareRun）、タイトルのデスクトップの治った・感染は練習の前に戻す。
///   リザルトの「もう一度あそぶ」も同じボス（StageFilter をタイトルまで残す）
/// ・タイトルに戻ったら元に戻し（本番のビルドでも）、図鑑をそのアプリの詳しい画面で開き直す（TakeReturnKey）
/// </summary>
public static class AppDexBattle
{
    const string TitleScene = "RunTitle";
    /// <summary>【仮】使用時間（強さの真ん中くらい。確認用の板と同じ）</summary>
    const double UsageHours = 10.0;

    /// <summary>戦えるアプリ1つ分</summary>
    public struct App
    {
        /// <summary>表の名前（同じアプリが2つ並ばないように見る）</summary>
        public string Label;
        /// <summary>ランに渡す名前とバンドルID</summary>
        public string Name, BundleId;
        /// <summary>図鑑の記録と同じアプリか見る名前・バンドルID（前方一致）</summary>
        public string[] Names, Bundles;
        /// <summary>ラスボス（Terminal）</summary>
        public bool Final;
    }

    /// <summary>そのアプリ1つだけを使った想定の使用データ</summary>
    class Provider : IAppUsageProvider
    {
        readonly AppUsage _app;
        public Provider(AppUsage app) { _app = app; }
        public string Label => "図鑑";
        public bool TryLoad(out List<AppUsage> usages, out string error)
        {
            usages = new List<AppUsage> { _app };
            error = null;
            return true;
        }
    }

    /// <summary>図鑑から始めたランの途中（タイトルに戻るまで）</summary>
    public static bool IsPractice { get; private set; }
    static string _returnKey;
    static bool _curedBefore; // 練習の前に、タイトルのデスクトップが治っていたか（ステージに入ると感染に戻るので、タイトルで戻す）

    /// <summary>タイトルに戻ったとき、図鑑で開き直すアプリがある</summary>
    public static bool HasReturn => _returnKey != null;

    /// <summary>開き直すアプリのキー（1回だけ。無ければ null）</summary>
    public static string TakeReturnKey()
    {
        string key = _returnKey;
        _returnKey = null;
        return key;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        IsPractice = false;
        _returnKey = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // タイトルに戻ったら、図鑑に書くのを元に戻す（練習のランは終わり）
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TitleScene) return;
        if (IsPractice) MacDesktopInfection.Cured = _curedBefore; // 全部クリアして治ったデスクトップは、練習のあとも治ったまま
        IsPractice = false;
        AppDex.Suspended = false;
        if (GameSession.Instance != null) GameSession.Instance.StageFilter = null;
    }

    /// <summary>戦えるアプリの全部（確認用の板と同じ集め方。同じ名前・同じバンドルIDは1つ）</summary>
    public static List<App> Roster(GameSession session)
    {
        var list = new List<App>();
        AppCatalog catalog = session != null ? session.Catalog : null;
        if (catalog == null) return list;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string label, string[] names, string[] bundles, bool final = false)
        {
            if (string.IsNullOrWhiteSpace(label) || seen.Contains(label)) return;
            string name = First(names) ?? label;
            string bundle = First(bundles) ?? "";
            if (!final && catalog.IsFinalBossApp(bundle, name)) return; // ラスボスのアプリは、最後にラスボスとして1つ
            var row = new AppUsageRow { Name = name, BundleId = bundle, Seconds = 1 };
            if (!final && session.Abilities != null && session.Abilities.IsExcluded(row)) return; // カードにもボスにもしないアプリ
            if (bundle.Length > 0 && !seenBundles.Add(bundle)) return;
            seen.Add(label);
            list.Add(new App
            {
                Label = label,
                Name = name,
                BundleId = bundle,
                Names = (names ?? Array.Empty<string>()).Append(label).ToArray(),
                Bundles = bundles ?? Array.Empty<string>(),
                Final = final,
            });
        }
        foreach (AppEntry a in catalog.Apps)
            if (a != null && a.BossPrefab != null && a.Profile != null) Add(a.DisplayName, Prepend(a.DisplayName, a.ScreenTimeNames), a.BundleIdPrefixes);
        foreach (AppBossEntry e in catalog.AppBosses)
            if (e != null) Add(e.Label, e.AppNames, e.BundleIdPrefixes);
        if (session.Abilities != null)
            foreach (AppSkillEntry e in session.Abilities.AppSkills)
                if (e != null) Add(e.Label, e.AppNames, e.BundleIdPrefixes);
        foreach (string label in AppKits.Labels) Add(label, AppKits.NamesOf(label), AppKits.BundlesOf(label));
        AppEntry boss = catalog.FinalBoss; // ラスボスは最後
        if (boss != null) Add(boss.DisplayName, Prepend(boss.DisplayName, boss.ScreenTimeNames), boss.BundleIdPrefixes, true);
        return list;
    }

    /// <summary>
    /// 図鑑の記録に、まだ載っていない戦えるアプリを足した並び（記録 → 足したアプリの順）。
    /// 足したアプリは倒した回数 0（灰色）で、ランが図鑑に書くのと同じ名前（RunPlanner.DexIdentity）。保存はしない
    /// </summary>
    public static List<AppDexEntry> WithRoster(IEnumerable<AppDexEntry> recorded, GameSession session)
    {
        var result = recorded != null ? recorded.Where(e => e != null).ToList() : new List<AppDexEntry>();
        AppCatalog catalog = session != null ? session.Catalog : null;
        if (catalog == null) return result;
        int count = result.Count;
        var keys = new HashSet<string>(result.Select(e => e.key));
        foreach (App app in Roster(session))
        {
            (string name, string bundleId) = RunPlanner.DexIdentity(catalog, app.Name, app.BundleId);
            if (string.IsNullOrWhiteSpace(name)) continue;
            string key = AppDex.KeyOf(name);
            if (keys.Contains(key)) continue;
            bool recordedAlready = false;
            for (int i = 0; i < count && !recordedAlready; i++) recordedAlready = Covers(catalog, app, key, result[i]);
            if (recordedAlready) continue;
            keys.Add(key);
            result.Add(new AppDexEntry { key = key, name = name.Trim(), bundleId = bundleId ?? "", firstSeen = "", firstDefeated = "", skill = "" });
        }
        return result;
    }

    // 図鑑の記録 e が、そのアプリか（記録はスクリーンタイムの名前で、バンドルIDの無い物も多い。名前は単語ごと・「名前 - 説明」の形も見る）
    static bool Covers(AppCatalog catalog, App app, string key, AppDexEntry e)
    {
        bool finalEntry = catalog.IsFinalBossApp(e.bundleId, e.name);
        if (app.Final || finalEntry) return app.Final && finalEntry;
        if (e.key == key) return true;
        if (AppMatch.Matches(e.Row, app.Names, app.Bundles)) return true;
        return app.Names.Any(n => AppCatalog.NameMatches(e.name, n));
    }

    /// <summary>今、そのアプリと戦いに行けるか（タイトルで、ランの外。カードにもボスにもしないアプリは行けない）</summary>
    public static bool CanFight(GameSession session, AppDexEntry e)
    {
        if (session == null || session.Catalog == null || e == null || string.IsNullOrWhiteSpace(e.name)) return false;
        if (session.Run != null || session.IsTransitioning || session.IsGameOver) return false;
        if (session.Catalog.IsFinalBossApp(e.bundleId, e.name)) return session.Catalog.FinalBoss != null;
        return session.Abilities == null || !session.Abilities.IsExcluded(e.Row);
    }

    /// <summary>そのアプリのボスだけのランを始める（能力えらびの画面へ）。始められなければ false（図鑑に書くのは元のまま）</summary>
    public static bool Fight(GameSession session, AppDexEntry e)
    {
        if (!CanFight(session, e)) return false;
        bool final = session.Catalog.IsFinalBossApp(e.bundleId, e.name);
        // ラスボスはボスの候補に入らず毎回最後に足されるので、何か1つのアプリ（Finder）でランを作ってから、ラスボスだけにしぼる
        AppUsage usage = final
            ? new AppUsage("com.apple.finder", "Finder", UsageHours * 3600.0)
            : new AppUsage(e.bundleId ?? "", e.name, UsageHours * 3600.0);
        _curedBefore = MacDesktopInfection.Cured;
        IsPractice = true;
        AppDex.Suspended = true;
        session.StageFilter = final
            ? (Func<List<StagePlan>, List<StagePlan>>)(plans => plans.Where(p => p.IsFinal).ToList())
            : plans => plans.Where(p => !p.IsFinal).ToList();
        if (session.StartRun(new Provider(usage), out string error))
        {
            _returnKey = e.key;
            return true;
        }
        Debug.LogWarning($"図鑑から戦いに行けませんでした（{e.name}）：{error}");
        IsPractice = false;
        AppDex.Suspended = false;
        session.StageFilter = null;
        return false;
    }

    static string First(string[] values) => values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    static string[] Prepend(string first, string[] rest) => new[] { first }.Concat(rest ?? Array.Empty<string>()).ToArray();
}
