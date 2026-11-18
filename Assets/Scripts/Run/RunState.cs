using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    /// <summary>アプリのアイコン（16 ドット）。ランを作るときに1回だけ用意する（画面の描画中にアイコンを読みに行かないため）</summary>
    public Sprite Icon16;
    /// <summary>ラスボス（Terminal。毎回、最後のステージ。2026-11-18）</summary>
    public bool IsFinal;
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

/// <summary>感染したデスクトップの画面の Dock に並べるアプリ1つ（2026-11-18：メンターのFBから）</summary>
public class DockApp
{
    public string Name;
    /// <summary>16 ドットのアイコン（無ければ null）</summary>
    public Sprite Icon;
    /// <summary>このランでボスになるステージの番号（0 から）。ボスでなければ -1</summary>
    public int Stage = -1;
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

    // リザルトに出す数（2026-11-18：画面のイメージから。ステージで遊んでいた時間だけ数える）
    /// <summary>ステージで遊んだ秒数（リトライの分も含む。切り替え・ゲームオーバーの画面は含まない）</summary>
    public float PlaySeconds;
    /// <summary>倒した雑魚の数（駆除したウイルス）</summary>
    public int Kills;
    /// <summary>受けたダメージ（ライフが減った数）</summary>
    public int DamageTaken;
    /// <summary>最後にゲームオーバーになったステージ</summary>
    public StagePlan LastFailed;
    /// <summary>相棒（能力えらびで選んだカード。2026-11-18：画面のイメージ）</summary>
    public AbilityCandidate Partner;
    /// <summary>感染したデスクトップの画面の Dock（使用時間の長い順。ボスのアプリは必ず入る）</summary>
    public List<DockApp> Dock = new List<DockApp>();

    public RunState(IEnumerable<StagePlan> stages)
    {
        Stages = stages.ToList();
    }

    public StagePlan Current => CurrentIndex >= 0 && CurrentIndex < Stages.Count ? Stages[CurrentIndex] : null;
    public bool IsFinished => CurrentIndex >= Stages.Count;

    /// <summary>
    /// 能力の元になったアプリのアイコン（能力の枠に小さく重ねる。2026-11-18：メンターのFB「アプリの技が分かりにくい」）。
    /// 相棒の能力なら相棒のアプリ、ボスの報酬ならそのボスのアプリ。カテゴリの能力は複数のアプリで同じものなので、先に当たった方。分からなければ null
    /// </summary>
    public Sprite SourceIcon(SkillDefinition skill)
    {
        if (skill == null) return null;
        if (Partner.Skill == skill && Partner.AppIcon != null) return Partner.AppIcon;
        foreach (StagePlan stage in Stages)
        {
            if (stage.Reward == skill && stage.Icon16 != null) return stage.Icon16;
        }
        foreach (AbilityCandidate c in Candidates)
        {
            if (c.Skill == skill && c.AppIcon != null) return c.AppIcon;
        }
        return null;
    }

    /// <summary>
    /// 【仮】防衛ランク（2026-11-18）：100 点から、受けたダメージ1つで4点、リトライ1回で15点、1ステージ5分を超えた分は1分ごとに2点引く。
    /// 90 点以上 S、75 以上 A、55 以上 B、35 以上 C、それより下は D
    /// </summary>
    public string Rank
    {
        get
        {
            float overMinutes = Mathf.Max(0f, PlaySeconds / 60f - 5f * Mathf.Max(1, Stages.Count));
            float score = 100f - DamageTaken * 4f - RetryCount * 15f - overMinutes * 2f;
            return score >= 90f ? "S" : score >= 75f ? "A" : score >= 55f ? "B" : score >= 35f ? "C" : "D";
        }
    }
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
    ///   ステージは1画面（AppCatalog の「代用ボスのステージ」）。倒すと、そのアプリの能力が手に入る
    ///   （アプリ専用の能力 → カテゴリの能力 → その場で作った能力の順。2026-11-18：Zoom・Finder・Premiere Pro は専用の能力）
    /// ・カードにもボスにもしないアプリ（設定・iPhone のアプリなど）と、ラスボスのアプリ（Terminal）は選ばない
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
                SkillDefinition own = abilities != null ? abilities.FindAppSkill(p.row)?.Skill : null; // 対応表のアプリでも、専用の能力があればそれ
                plans.Add(CreatePlan(catalog, p.entry, p.seconds, own));
                continue;
            }
            AppUsageRow row = p.row;
            row.Seconds = p.seconds;
            AbilityCategoryEntry category = abilities != null ? abilities.Find(row) : null;
            AppEntry template = templates.FirstOrDefault(a => category != null && a.Category == category.Category)
                ?? templates[(int)(PixelIcon.Hash(row.Name) % (uint)templates.Count)];
            AppVisual visual = AppVisuals.Get(row);
            SkillDefinition reward = abilities != null ? abilities.SkillFor(row) : null;
            if (reward == null) reward = AppVisuals.Generate(row, abilities, null);
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
    /// ラスボスのステージ（2026-11-18：Terminal。毎回、最後に足す）。強さはいつも最大、手に入る能力は無し（プロフィールの能力）。
    /// 見た目は代用ボスと同じ作り方：本物のアイコンから、ゲージの横のアイコンと着ているウィンドウを作る。ステージの色は表のテーマ
    /// </summary>
    public static StagePlan CreateFinalPlan(AppCatalog catalog)
    {
        AppEntry boss = catalog != null ? catalog.FinalBoss : null;
        if (boss == null) return null;
        string bundle = boss.BundleIdPrefixes?.FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "";
        AppVisual visual = AppVisuals.Get(new AppUsageRow { Name = boss.DisplayName, BundleId = bundle, Seconds = 1 });
        StagePlan plan = CreatePlan(catalog, boss, catalog.FullStrengthHours * 3600.0, null, visual);
        plan.IsFinal = true;
        return plan;
    }

    /// <summary>
    /// ボスの候補：使用時間の上位 poolSize 個のアプリ（除外とラスボスのアプリを除く）。同じアプリはまとめる（対応表のアプリは表の行で、ほかは名前で）。
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
            if (catalog.IsFinalBossApp(u.Id, u.DisplayName)) continue; // ラスボス（Terminal）は毎回最後に出るので、候補にしない
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
