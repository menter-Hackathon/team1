using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Team1.Achievements
{
    /// <summary>
    /// 条件ごとの判定。定義に [SerializeReference] で持たせる。
    /// record には、その実績の記録（数えた日など）が入っていて、書きかえてよい。
    /// </summary>
    public interface IAchievementCondition
    {
        ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target);
    }

    /// <summary>condition_type の値から条件を作る</summary>
    public static class AchievementConditions
    {
        public static readonly string[] Types =
        {
            "MorningDays", "DetoxReduction", "NewApp", "AppsPerDay", "StreakDays", "WeekendWorkMinutes",
            "WaveCleared", "AppBossPurified", "NoDamageWave", "WaveClearedWithSkills", "ClearsWithSamePartner", "FinalBossDefeated",
            "PurifiedAppCount", "PurifiedTop10"
        };

        public static IAchievementCondition Create(string type) => (type ?? "").Trim() switch
        {
            "MorningDays" => new MorningDaysCondition(),
            "DetoxReduction" => new DetoxReductionCondition(),
            "NewApp" => new NewAppCondition(),
            "AppsPerDay" => new AppsPerDayCondition(),
            "StreakDays" => new StreakDaysCondition(),
            "WeekendWorkMinutes" => new WeekendWorkMinutesCondition(),
            "WaveCleared" => new WaveClearedCondition(),
            "AppBossPurified" => new AppBossPurifiedCondition(),
            "NoDamageWave" => new NoDamageWaveCondition(),
            "WaveClearedWithSkills" => new WaveClearedWithSkillsCondition(),
            "ClearsWithSamePartner" => new ClearsWithSamePartnerCondition(),
            "FinalBossDefeated" => new FinalBossDefeatedCondition(),
            "PurifiedAppCount" => new PurifiedAppCountCondition(),
            "PurifiedTop10" => new PurifiedTop10Condition(),
            _ => null
        };

        /// <summary>一覧の詳細に出す「判定に使うデータ」</summary>
        public static string DataSource(IAchievementCondition c) => c switch
        {
            MorningDaysCondition m => $"スクリーンタイム：1時間ごとの使用時間（{m.fromHour}時台〜{m.toHourExclusive - 1}時台の合計）",
            DetoxReductionCondition _ => "スクリーンタイム：アプリごとの使用時間（先週と今週。月曜始まり）＋アプリのカテゴリ",
            NewAppCondition _ => "スクリーンタイム：今週使ったアプリと、読める範囲の過去の週",
            AppsPerDayCondition _ => "スクリーンタイム：日ごとに使ったアプリの数",
            StreakDaysCondition _ => "スクリーンタイム：日ごとの使用の有無",
            WeekendWorkMinutesCondition _ => "スクリーンタイム：土日のアプリごとの使用時間＋アプリのカテゴリ（作業）",
            WaveClearedCondition _ => "プレイ：ステージのクリア",
            AppBossPurifiedCondition _ => "プレイ：アプリボスの正常化",
            NoDamageWaveCondition _ => "プレイ：ステージ中の被弾",
            WaveClearedWithSkillsCondition _ => "プレイ：クリアしたときの装備スキルの数",
            ClearsWithSamePartnerCondition _ => "プレイ：相棒ごとのクリア回数",
            FinalBossDefeatedCondition _ => "プレイ：最終ウイルスボスの撃破",
            PurifiedAppCountCondition _ => "アプリ図鑑：正常化したアプリの種類",
            PurifiedTop10Condition _ => "アプリ図鑑：使用時間TOP10（最新のスキャン）と正常化の記録",
            _ => "-"
        };
    }

    /// <summary>
    /// 実績の判定（Unity に依存しない）。解除・進み具合・数えた日をセーブデータに書く（ファイルへの保存は呼び出し側）。
    /// </summary>
    public sealed class AchievementEngine
    {
        readonly List<AchievementSpec> _specs;

        public AchievementEngine(IEnumerable<AchievementSpec> specs, AchievementSaveData save)
        {
            _specs = (specs ?? Enumerable.Empty<AchievementSpec>()).Where(s => s != null && !string.IsNullOrEmpty(s.Id)).ToList();
            Save = (save ?? new AchievementSaveData()).Normalize();
        }

        public IReadOnlyList<AchievementSpec> Specs => _specs;
        public AchievementSaveData Save { get; private set; }

        public AchievementSpec Find(string id) => _specs.FirstOrDefault(s => s.Id == id);
        public AchievementRecord Record(string id) => Save.GetOrCreate(id);

        /// <summary>
        /// あるタイミングの実績をまとめて判定し、新しく解除したものを返す。
        /// 起動時（LaunchScan）でスクリーンタイムが取れていなければ何もしない（前回の判定日時も変えない＝次の起動時にまとめて判定）。
        /// </summary>
        public List<AchievementUnlock> Evaluate(AchievementTrigger trigger, AchievementContext ctx)
        {
            var unlocked = new List<AchievementUnlock>();
            if (ctx == null) return unlocked;
            if (trigger == AchievementTrigger.LaunchScan && ctx.ScreenTime == null) return unlocked;
            ctx.Save = Save;
            if (ctx.Categories == null) ctx.Categories = AppCategoryMap.Empty;

            foreach (var spec in _specs)
            {
                if (spec.Trigger != trigger || spec.Condition == null) continue;
                var record = Save.GetOrCreate(spec.Id);
                if (record.unlocked) continue; // 同じ実績は2回解除しない
                ConditionResult r = spec.Condition.Evaluate(ctx, record, spec.Target);
                if (r.IsSkipped) continue;
                record.progress = Math.Min(r.Current, spec.Target);
                if (!r.IsUnlocked) continue;
                Unlock(record, spec, r.Reason, ctx.Now);
                unlocked.Add(new AchievementUnlock { Spec = spec, Reason = r.Reason, UnlockedAt = ctx.Now });
            }
            if (trigger == AchievementTrigger.LaunchScan) Save.LastScanAt = ctx.Now;
            return unlocked;
        }

        /// <summary>確認用：強制的に解除する。すでに解除していれば null</summary>
        public AchievementUnlock ForceUnlock(string id, DateTime now, string reason = "デバッグで解除した")
        {
            var spec = Find(id);
            if (spec == null) return null;
            var record = Save.GetOrCreate(id);
            if (record.unlocked) return null;
            Unlock(record, spec, reason, now);
            return new AchievementUnlock { Spec = spec, Reason = reason, UnlockedAt = now, Forced = true };
        }

        static void Unlock(AchievementRecord record, AchievementSpec spec, string reason, DateTime now)
        {
            record.unlocked = true;
            record.unlockedAt = now.ToString("o", CultureInfo.InvariantCulture);
            record.reason = reason ?? "";
            record.progress = spec.Target;
        }

        /// <summary>進み具合（0〜1）</summary>
        public double Ratio(AchievementSpec spec)
        {
            var record = Save.Find(spec.Id);
            if (record == null) return 0;
            if (record.unlocked) return 1;
            return spec.Target > 0 ? Math.Max(0, Math.Min(1, record.progress / spec.Target)) : 0;
        }

        /// <summary>「もうすぐ解除」：まだ解除していなくて、いちばん進んでいる実績（同じなら目標に近い方）。無ければ null</summary>
        public AchievementSpec AlmostThere(IEnumerable<string> exclude = null)
        {
            var skip = new HashSet<string>(exclude ?? Enumerable.Empty<string>());
            return _specs
                .Where(s => !skip.Contains(s.Id) && !Save.IsUnlocked(s.Id))
                .Select(s => (Spec: s, Ratio: Ratio(s)))
                .Where(x => x.Ratio > 0 && x.Ratio < 1)
                .OrderByDescending(x => x.Ratio)
                .ThenBy(x => x.Spec.Target)
                .Select(x => x.Spec)
                .FirstOrDefault();
        }

        public (int Unlocked, int Total) Count(AchievementCategory? category = null)
        {
            var list = _specs.Where(s => category == null || s.Category == category.Value).ToList();
            return (list.Count(s => Save.IsUnlocked(s.Id)), list.Count);
        }

        /// <summary>セーブを消す（確認用）</summary>
        public void Reset() => Save = new AchievementSaveData();
    }
}
