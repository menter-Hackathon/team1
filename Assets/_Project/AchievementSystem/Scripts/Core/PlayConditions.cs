using System;
using System.Linq;

namespace Team1.Achievements
{
    // 「プレイ」と「アプリ図鑑」の条件。対象のできごとでなければ Skip（進み具合も変えない）。

    /// <summary>Wave（ステージ）target をクリアした</summary>
    [Serializable]
    public class WaveClearedCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.StageCleared) return ConditionResult.Skip();
            int wave = p.StageIndex + 1;
            if (wave >= target) return ConditionResult.Unlock(wave, $"Wave {wave}（{p.AppName}）をクリアした");
            return ConditionResult.Progress(Math.Max(record.progress, wave));
        }
    }

    /// <summary>アプリボス（最終ウイルスボスではない）を target 回正常化した</summary>
    [Serializable]
    public class AppBossPurifiedCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.StageCleared || p.IsFinalBoss) return ConditionResult.Skip();
            record.count++;
            if (record.count >= target) return ConditionResult.Unlock(record.count, $"{p.AppName}のボスを正常化した");
            return ConditionResult.Progress(record.count);
        }
    }

    /// <summary>ダメージを受けずに Wave（ステージ）をクリアした</summary>
    [Serializable]
    public class NoDamageWaveCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.StageCleared) return ConditionResult.Skip();
            if (!p.TookDamage) return ConditionResult.Unlock(1, $"Wave {p.StageIndex + 1}（{p.AppName}）をノーダメージでクリアした");
            return ConditionResult.Progress(0);
        }
    }

    /// <summary>スキルを target 個装備した状態で Wave（ステージ）をクリアした</summary>
    [Serializable]
    public class WaveClearedWithSkillsCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.StageCleared) return ConditionResult.Skip();
            if (p.EquippedSkills >= target)
                return ConditionResult.Unlock(p.EquippedSkills, $"スキルを{p.EquippedSkills}つ装備して Wave {p.StageIndex + 1}（{p.AppName}）をクリアした");
            return ConditionResult.Progress(Math.Max(record.progress, p.EquippedSkills));
        }
    }

    /// <summary>同じ相棒で target 回クリアした（相棒ごとのクリア数はセーブにある。数を足すのはサービス側）</summary>
    [Serializable]
    public class ClearsWithSamePartnerCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.RunCompleted || ctx.Save == null) return ConditionResult.Skip();
            int clears = ctx.Save.PartnerClears(p.Partner);
            var best = ctx.Save.BestPartner();
            if (clears >= target) return ConditionResult.Unlock(clears, $"相棒「{p.Partner}」と{clears}回クリアした");
            return ConditionResult.Progress(best.Clears);
        }
    }

    /// <summary>最終ウイルスボスを倒した</summary>
    [Serializable]
    public class FinalBossDefeatedCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var p = ctx.Play;
            if (p == null || p.Kind != PlayEventKind.StageCleared || !p.IsFinalBoss) return ConditionResult.Skip();
            return ConditionResult.Unlock(1, $"最終ウイルスボス（{p.AppName}）を倒した");
        }
    }

    /// <summary>target 種類のアプリを正常化した</summary>
    [Serializable]
    public class PurifiedAppCountCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var log = ctx.Purify;
            if (log == null) return ConditionResult.Skip();
            int n = log.PurifiedApps.Count;
            if (n >= target)
                return ConditionResult.Unlock(n, string.IsNullOrEmpty(log.LastPurified)
                    ? $"{n}種類のアプリを正常化した"
                    : $"{log.LastPurified}を正常化して、{n}種類になった");
            return ConditionResult.Progress(n);
        }
    }

    /// <summary>使用時間TOP10のアプリ（相棒にしたアプリを除く）をすべて正常化した。進み具合は割合（0〜1）</summary>
    [Serializable]
    public class PurifiedTop10Condition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var log = ctx.Purify;
            if (log == null) return ConditionResult.Skip();
            var tops = log.TopApps
                .Where(a => !string.IsNullOrWhiteSpace(a) && (string.IsNullOrWhiteSpace(log.Partner) || !AppCategoryMap.SameApp(a, log.Partner)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tops.Count == 0) return ConditionResult.Skip("使用時間TOP10が分からない");
            int done = tops.Count(a => log.PurifiedApps.Any(p => AppCategoryMap.SameApp(a, p)));
            record.detail = $"{done}/{tops.Count}";
            double ratio = (double)done / tops.Count;
            if (done == tops.Count) return ConditionResult.Unlock(target, $"使用時間TOP10の{tops.Count}アプリをすべて正常化した");
            return ConditionResult.Progress(ratio * target);
        }
    }
}
