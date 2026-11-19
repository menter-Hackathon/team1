using System;
using System.Linq;

namespace Team1.Achievements
{
    // 「PCの使い方」の条件（起動時に判定）。ScreenTime が null（取れなかった）なら飛ばし、次の起動時にまとめて判定する。

    /// <summary>朝型の日（fromHour 時台〜toHourExclusive の手前の合計が minSeconds 以上の日）を数える</summary>
    [Serializable]
    public class MorningDaysCondition : IAchievementCondition
    {
        public int fromHour = 5;
        public int toHourExclusive = 9;
        public int minSeconds = 600;

        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime? last = record.LastCountedDay;
            DateTime? lastMorning = null;
            int lastMorningSeconds = 0;
            foreach (var day in st.Days)
            {
                if (last.HasValue && day.Date <= last.Value) continue; // 数えた日は二重に数えない
                // 朝の時間が終わった日だけ数える（今日は toHourExclusive 時を過ぎてから。判定する時点より後の日は数えない）
                bool settled = day.Date < ctx.Now.Date || (day.Date == ctx.Now.Date && ctx.Now.Hour >= toHourExclusive);
                if (!settled || !day.HasHourly) break; // 決まっていない・内訳が無い日は、次の判定で
                int seconds = day.SecondsInHours(fromHour, toHourExclusive);
                if (seconds >= minSeconds)
                {
                    record.count++;
                    lastMorning = day.Date;
                    lastMorningSeconds = seconds;
                }
                record.LastCountedDay = day.Date;
            }
            if (record.count >= target)
                return ConditionResult.Unlock(record.count, lastMorning.HasValue
                    ? $"{WeekMath.Label(lastMorning.Value)}は{fromHour}時台〜{toHourExclusive - 1}時台に{lastMorningSeconds / 60}分使った（朝型の日 {record.count}日目）"
                    : $"朝型の日が{record.count}日になった");
            return ConditionResult.Progress(record.count);
        }
    }

    /// <summary>エンタメ・SNSのアプリのどれかが、先週より target（割合）以上減った</summary>
    [Serializable]
    public class DetoxReductionCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime thisWeek = WeekMath.WeekStart(ctx.Now);
            DateTime lastWeek = thisWeek.AddDays(-7);
            if (st.CoverageStart > lastWeek) return ConditionResult.Skip("先週のデータが無い");

            string bestApp = null;
            AppUsageCategory bestCategory = AppUsageCategory.Other;
            double best = double.NegativeInfinity;
            foreach (string app in st.AppsBetween(lastWeek, thisWeek))
            {
                var category = ctx.Categories.Resolve(app);
                if (!AppCategoryMap.IsDetoxTarget(category)) continue; // 作業・その他は対象外
                int before = st.SecondsOf(app, lastWeek, thisWeek);
                if (before <= 0) continue;
                int now = st.SecondsOf(app, thisWeek, ctx.Now.Date.AddDays(1));
                double reduction = 1.0 - (double)now / before;
                if (reduction > best) { best = reduction; bestApp = app; bestCategory = category; }
            }
            if (bestApp == null) return ConditionResult.Progress(0);
            if (best >= target - 1e-9)
            {
                int percent = (int)Math.Floor(best * 100 + 1e-6);
                record.detail = bestApp;
                return ConditionResult.Unlock(best, $"{bestApp}（{AppCategoryMap.Label(bestCategory)}）の使用時間が先週より{percent}%減った");
            }
            return ConditionResult.Progress(Math.Max(0, best));
        }
    }

    /// <summary>今週使ったアプリの中に、それより前の記録（読める範囲の過去の週）に一度も出てこないアプリがある</summary>
    [Serializable]
    public class NewAppCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime thisWeek = WeekMath.WeekStart(ctx.Now);
            if (st.CoverageStart >= thisWeek) return ConditionResult.Skip("今週より前の記録が無い");

            var earlier = st.AppsBetween(st.CoverageStart, thisWeek);
            DateTime end = ctx.Now.Date.AddDays(1);
            string found = st.AppsBetween(thisWeek, end)
                .Where(a => !earlier.Contains(a))
                .OrderByDescending(a => st.SecondsOf(a, thisWeek, end))
                .ThenBy(a => a, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (found == null) return ConditionResult.Progress(0);
            record.detail = found; // 解除のきっかけになったアプリ（敵の候補への組み込みは今回はしない）
            return ConditionResult.Unlock(1, $"「{found}」が今週はじめて記録に出てきた");
        }
    }

    /// <summary>1日に target 種類以上のアプリを使った</summary>
    [Serializable]
    public class AppsPerDayCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime? from = ctx.Save?.LastScanAt?.Date; // 前回の判定日以降（その日も含めて見直す）
            DayUsage bestDay = null;
            foreach (var day in st.Days)
            {
                if (from.HasValue && day.Date < from.Value) continue;
                if (bestDay == null || day.AppCount > bestDay.AppCount) bestDay = day;
            }
            double best = Math.Max(record.progress, bestDay?.AppCount ?? 0);
            if (bestDay != null && bestDay.AppCount >= target)
                return ConditionResult.Unlock(bestDay.AppCount, $"{WeekMath.Label(bestDay.Date)}に{bestDay.AppCount}種類のアプリを使った");
            return ConditionResult.Progress(best);
        }
    }

    /// <summary>target 日連続でPCを使った（記録には今の連続日数と最後に数えた日を残す）</summary>
    [Serializable]
    public class StreakDaysCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            if (st.IsEmpty) return ConditionResult.Progress(record.count);

            DateTime today = ctx.Now.Date;
            DateTime? last = record.LastCountedDay;
            DateTime d;
            if (!last.HasValue) d = st.CoverageStart;
            else if (last.Value.AddDays(1) < st.CoverageStart) { record.count = 0; d = st.CoverageStart; } // 間の日が分からない＝途切れたとみなす
            else d = last.Value.AddDays(1);

            for (; d <= today; d = d.AddDays(1))
            {
                bool used = (st.GetDay(d)?.TotalSeconds ?? 0) > 0;
                if (used)
                {
                    bool continues = record.LastCountedDay == d.AddDays(-1);
                    record.count = continues ? record.count + 1 : 1;
                    record.LastCountedDay = d;
                }
                else if (d < today)
                {
                    record.count = 0; // 使わなかった日で途切れる
                    record.LastCountedDay = d;
                }
                else break; // 今日まだ使っていない：今日は次の判定で
            }
            if (record.count >= target)
            {
                DateTime end = record.LastCountedDay ?? today;
                DateTime start = end.AddDays(-(record.count - 1));
                return ConditionResult.Unlock(record.count, $"{WeekMath.Label(start)}〜{WeekMath.Label(end)}の{record.count}日連続でPCを使った");
            }
            return ConditionResult.Progress(record.count);
        }
    }

    /// <summary>土曜か日曜に、作業カテゴリのアプリをその日の合計で target 分以上使った</summary>
    [Serializable]
    public class WeekendWorkMinutesCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime? from = ctx.Save?.LastScanAt?.Date;
            double bestMinutes = record.progress;
            foreach (var day in st.Days)
            {
                if (!WeekMath.IsWeekend(day.Date) || (from.HasValue && day.Date < from.Value)) continue;
                var work = day.Entries.Where(e => ctx.Categories.Resolve(e.App) == AppUsageCategory.Work).OrderByDescending(e => e.Seconds).ToList();
                int seconds = work.Sum(e => e.Seconds);
                double minutes = seconds / 60.0;
                if (seconds >= target * 60)
                {
                    string top = string.Join("・", work.Take(2).Select(e => e.App));
                    return ConditionResult.Unlock(Math.Floor(minutes), $"{WeekMath.Label(day.Date)}に作業アプリ（{top}）を{seconds / 60}分使った");
                }
                bestMinutes = Math.Max(bestMinutes, Math.Floor(minutes));
            }
            return ConditionResult.Progress(bestMinutes);
        }
    }
}
