using System.Collections.Generic;
using System.Linq;

/// <summary>1ステージ分の計画：どのアプリのボスが、どの強さで出るか</summary>
public class StagePlan
{
    public AppEntry App;
    /// <summary>過去7日間の合計使用時間（秒）</summary>
    public double UsageSeconds;
    /// <summary>強さ（0〜1）</summary>
    public float Strength;
    /// <summary>強さを反映したボスの設定（複製）</summary>
    public BossProfile Profile;
    /// <summary>手に入る能力。無ければ null</summary>
    public SkillDefinition Reward;
    /// <summary>代用ボス（対応表に無いアプリ）の見た目（アイコン・色）。対応表のボスは null</summary>
    public AppVisual Visual;
    public bool IsStandIn => Visual != null;

    public double UsageHours => UsageSeconds / 3600.0;
    public string DisplayName => App != null ? App.DisplayName : "?";
}

/// <summary>正常化したアプリの記録（リザルト用）</summary>
public class ClearRecord
{
    public StagePlan Stage;
    public SkillDefinition Reward;
}

/// <summary>
/// ラン状態（1回のプレイの間、シーンをまたいで残るデータ）。GameSession が持つ。
/// </summary>
public class RunState
{
    public readonly List<StagePlan> Stages;
    /// <summary>持っている能力（各ステージでプレイヤーにつなぐ）</summary>
    public readonly SkillLoadout Loadout = new SkillLoadout(SkillLoadout.DefaultCapacity);
    public readonly List<ClearRecord> Cleared = new List<ClearRecord>();

    /// <summary>今のステージを始めたときの能力（リトライで戻す）</summary>
    public SkillDefinition[] StageStartSlots;
    /// <summary>能力えらびのカード（使用時間の上位アプリの能力。2026-11-17：ゲームの流れ）</summary>
    public List<AbilityCandidate> Candidates = new List<AbilityCandidate>();
    public int CurrentIndex;
    public int RetryCount;
    /// <summary>このランで初めて正常化したアプリ（図鑑のキー）。リザルトで光らせる</summary>
    public readonly HashSet<string> NewDex = new HashSet<string>();

    public RunState(IEnumerable<StagePlan> stages)
    {
        Stages = stages.ToList();
    }

    public StagePlan Current => CurrentIndex >= 0 && CurrentIndex < Stages.Count ? Stages[CurrentIndex] : null;
    public bool IsFinished => CurrentIndex >= Stages.Count;
}

/// <summary>
/// 使用データ → ステージの計画を作る（仕様書 2.1 の 3〜5）。
/// ・対応アプリ表に無いアプリは捨てる（仕様書 6.2）
/// ・同じ対応アプリに当たるデータが複数あれば、使用時間を足す
/// ・1アプリ＝1ステージ。使用時間の短い順に並べる（一番使ったアプリが最後の一番手強いボス）
/// ・強さ t ＝ 使用時間 ÷ 最大の時間（0〜1）。ボスのゲージ最大値・第2形態の待ち時間を t で決める
///   （能力の強さは、使用時間ではなくレベルで決める。2026-11-17 決定。対応アプリ表の「能力の数値」の欄は使わない）
/// </summary>
public static class RunPlanner
{
    public static List<StagePlan> Plan(AppCatalog catalog, IEnumerable<AppUsage> usages)
    {
        var plans = new List<StagePlan>();
        if (catalog == null || usages == null) return plans;

        var seconds = new Dictionary<AppEntry, double>();
        foreach (var u in usages)
        {
            AppEntry app = catalog.Find(u.Id);
            if (app == null || app.BossPrefab == null || app.Profile == null) continue;
            seconds.TryGetValue(app, out double s);
            seconds[app] = s + u.UsageSeconds;
        }

        var apps = catalog.Apps.ToList();
        foreach (var pair in seconds.OrderBy(p => p.Value).ThenBy(p => apps.IndexOf(p.Key)))
        {
            plans.Add(CreatePlan(catalog, pair.Key, pair.Value));
        }
        return plans;
    }

    public static StagePlan CreatePlan(AppCatalog catalog, AppEntry app, double usageSeconds, SkillDefinition rewardOverride = null, AppVisual visual = null)
    {
        float t = catalog.StrengthFromSeconds(usageSeconds);

        // 複製しない（能力ごとのレベル・クールタイムを、同じ能力として数えるため）
        SkillDefinition reward = rewardOverride != null ? rewardOverride : app.Profile.Reward;
        BossProfile profile = app.Profile.CreateScaledCopy(app.GaugeMax.Lerp(t), app.Phase2Wait.Lerp(t), reward);
        if (visual != null) profile.SetLook(app.DisplayName, visual.Icon12);

        return new StagePlan
        {
            App = app,
            UsageSeconds = usageSeconds,
            Strength = t,
            Profile = profile,
            Reward = reward,
            Visual = visual,
        };
    }

    /// <summary>
    /// スクリーンタイムの上位 poolSize 個のアプリから、ランダムに stageCount 個を選んで、それぞれのステージのボスにする（2026-11-17 まさぴ〜決定）。
    /// ・対応表のアプリ（Chrome・VS Code）は、そのアプリのボス
    /// ・対応表に無いアプリは代用ボス：見た目はアイコンから、技はカテゴリの近いボス（ブラウザ → Chrome、開発 → VS Code、ほかは名前で決める）。
    ///   ステージは1画面（AppCatalog の「代用ボスのステージ」）。倒すと、そのアプリの能力（カテゴリの能力か、その場で作った能力）が手に入る
    /// ・カードにもボスにもしないアプリ（Finder・設定・iPhone のアプリなど）は選ばない
    /// ・並びは使用時間の短い順（一番使ったアプリが最後）
    /// </summary>
    public static List<StagePlan> PlanRandom(AppCatalog catalog, AbilityCatalog abilities, IEnumerable<AppUsage> usages, System.Random rng,
        int poolSize = 10, int stageCount = 3)
    {
        var plans = new List<StagePlan>();
        if (catalog == null || usages == null) return plans;
        var templates = catalog.Apps.Where(a => a != null && a.BossPrefab != null && a.Profile != null).ToList();
        if (templates.Count == 0) return plans;
        var top = BossPool(catalog, abilities, usages, poolSize);

        // ランダムに選ぶ（フィッシャー–イェーツ）
        for (int i = 0; i < top.Count; i++)
        {
            int j = rng.Next(i, top.Count);
            (top[i], top[j]) = (top[j], top[i]);
        }
        foreach (var p in top.Take(stageCount).OrderBy(p => p.seconds))
        {
            if (p.entry != null)
            {
                plans.Add(CreatePlan(catalog, p.entry, p.seconds));
                continue;
            }
            AppUsageRow row = p.row;
            row.Seconds = p.seconds;
            AbilityCategoryEntry category = abilities != null ? abilities.Find(row) : null;
            AppEntry template = templates.FirstOrDefault(a => category != null && a.Category == category.Category)
                ?? templates[(int)(PixelIcon.Hash(row.Name) % (uint)templates.Count)];
            AppVisual visual = AppVisuals.Get(row);
            SkillDefinition reward = category != null ? category.Skill : AppVisuals.Generate(row, abilities, null);
            var standIn = new AppEntry
            {
                DisplayName = row.Name,
                BundleIdPrefixes = string.IsNullOrEmpty(row.BundleId) ? System.Array.Empty<string>() : new[] { row.BundleId }, // 図鑑のアイコン用
                Category = category != null ? category.Category : SkillCategory.Unknown,
                BossPrefab = template.BossPrefab,
                Profile = template.Profile,
                SpawnPosition = template.SpawnPosition,
                StageScene = catalog.StandInStageScene,
                Theme = visual != null ? visual.Theme : template.Theme,
                GaugeMax = template.GaugeMax,
                Phase2Wait = template.Phase2Wait,
            };
            plans.Add(CreatePlan(catalog, standIn, p.seconds, reward, visual));
        }
        return plans;
    }

    /// <summary>
    /// ボスの候補：使用時間の上位 poolSize 個のアプリ（除外を除く）。同じアプリはまとめる（対応表のアプリは表の行で、ほかは名前で）。
    /// アプリ図鑑にも、この候補を載せる
    /// </summary>
    public static List<(AppEntry entry, AppUsageRow row, double seconds)> BossPool(AppCatalog catalog, AbilityCatalog abilities, IEnumerable<AppUsage> usages, int poolSize = 10)
    {
        var pool = new List<(AppEntry entry, AppUsageRow row, double seconds)>();
        if (catalog == null || usages == null) return pool;
        foreach (var u in usages)
        {
            if (u.UsageSeconds <= 0) continue;
            var row = new AppUsageRow { Name = u.DisplayName, BundleId = u.Id, Seconds = u.UsageSeconds };
            if (abilities != null && abilities.IsExcluded(row)) continue;
            AppEntry entry = catalog.Find(u.Id) ?? catalog.FindByName(u.DisplayName);
            if (entry != null && (entry.BossPrefab == null || entry.Profile == null)) entry = null;
            int i = pool.FindIndex(p => entry != null ? p.entry == entry
                : p.entry == null && string.Equals(p.row.Name, row.Name, System.StringComparison.OrdinalIgnoreCase));
            if (i >= 0) pool[i] = (pool[i].entry, pool[i].row, pool[i].seconds + u.UsageSeconds);
            else pool.Add((entry, row, u.UsageSeconds));
        }
        return pool.OrderByDescending(p => p.seconds).Take(poolSize).ToList();
    }
}
