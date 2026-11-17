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
    /// <summary>強さを反映した、手に入る能力（複製）。無ければ null</summary>
    public SkillDefinition Reward;

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

    public static StagePlan CreatePlan(AppCatalog catalog, AppEntry app, double usageSeconds)
    {
        float t = catalog.StrengthFromSeconds(usageSeconds);

        SkillDefinition reward = app.Profile.Reward; // 複製しない（能力ごとのレベル・クールタイムを、同じ能力として数えるため）

        return new StagePlan
        {
            App = app,
            UsageSeconds = usageSeconds,
            Strength = t,
            Profile = app.Profile.CreateScaledCopy(app.GaugeMax.Lerp(t), app.Phase2Wait.Lerp(t), reward),
            Reward = reward,
        };
    }
}
