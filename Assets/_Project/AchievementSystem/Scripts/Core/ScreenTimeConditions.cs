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
            string lastMorningApp = null;
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
                    lastMorningApp = day.TopAppInHours(fromHour, toHourExclusive);
                }
                record.LastCountedDay = day.Date;
            }
            if (record.count >= target)
                return ConditionResult.Unlock(record.count, lastMorning.HasValue
                    ? $"{WeekMath.Label(lastMorning.Value)}は{fromHour}時台〜{toHourExclusive - 1}時台に{lastMorningSeconds / 60}分使った（朝型の日 {record.count}日目）"
                    : $"朝型の日が{record.count}日になった", lastMorningApp);
            return ConditionResult.Progress(record.count);
        }
    }

    /// <summary>
    /// エンタメ・SNSのアプリのどれかが、1週間（月曜〜日曜）の使用時間で、前の週より target（割合）以上減った。
    /// ・判定は週が終わってから（週の途中は、使う時間が増えるかもしれないので解除しない）
    /// ・その週で達成できなければ、次の週にやり直し（record.lastCountedDay＝最後に判定し終えた週の月曜。同じ週は二度判定しない）
    /// ・週の途中の進み具合は「先週の同じ曜日まで」と比べたペース（表示だけ）
    /// </summary>
    [Serializable]
    public class DetoxReductionCondition : IAchievementCondition
    {
        public ConditionResult Evaluate(AchievementContext ctx, AchievementRecord record, double target)
        {
            var st = ctx.ScreenTime;
            if (st == null) return ConditionResult.Skip("スクリーンタイムが取れていない");
            DateTime thisWeek = WeekMath.WeekStart(ctx.Now);
            // 読める範囲で、まるごと入っている最初の週
            DateTime firstWeek = WeekMath.WeekStart(st.CoverageStart);
            if (firstWeek < st.CoverageStart) firstWeek = firstWeek.AddDays(7);
            if (firstWeek >= thisWeek) return ConditionResult.Skip("先週のデータが無い");

            // 終わった週を、前の週と比べる（前回判定した週の次から）
            DateTime? judged = record.LastCountedDay;
            DateTime week = judged.HasValue ? judged.Value.AddDays(7) : firstWeek.AddDays(7);
            if (week < firstWeek.AddDays(7)) week = firstWeek.AddDays(7);
            for (; week.AddDays(7) <= thisWeek; week = week.AddDays(7))
            {
                var (app, category, reduction) = BestReduction(st, ctx.Categories, week.AddDays(-7), week, week, week.AddDays(7));
                record.LastCountedDay = week;
                if (app != null && reduction >= target - 1e-9)
                {
                    int percent = (int)Math.Floor(reduction * 100 + 1e-6);
                    record.detail = app;
                    return ConditionResult.Unlock(reduction,
                        $"{app}（{AppCategoryMap.Label(category)}）の使用時間が前の週より{percent}%減った（{WeekMath.Label(week)}〜{WeekMath.Label(week.AddDays(6))}）", app);
                }
            }

            // 今週の途中：先週の同じ曜日までと比べたペース
            int days = (ctx.Now.Date - thisWeek).Days + 1;
            DateTime lastWeek = thisWeek.AddDays(-7);
            if (lastWeek < firstWeek) return ConditionResult.Progress(0);
            var pace = BestReduction(st, ctx.Categories, lastWeek, lastWeek.AddDays(days), thisWeek, ctx.Now.Date.AddDays(1));
            return ConditionResult.Progress(pace.App == null ? 0 : Math.Max(0, pace.Reduction));
        }

        /// <summary>エンタメ・SNSのアプリで、いちばん減った割合（前の期間に使っていたアプリだけ。先週の使用時間に下限は設けない）</summary>
        static (string App, AppUsageCategory Category, double Reduction) BestReduction(ScreenTimeSnapshot st, IAppCategoryResolver categories,
            DateTime beforeFrom, DateTime beforeTo, DateTime afterFrom, DateTime afterTo)
        {
            string bestApp = null;
            AppUsageCategory bestCategory = AppUsageCategory.Other;
            double best = double.NegativeInfinity;
            foreach (string app in st.AppsBetween(beforeFrom, beforeTo).OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
            {
                var category = categories.Resolve(app);
                if (!AppCategoryMap.IsDetoxTarget(category)) continue; // 作業・その他は対象外
                int before = st.SecondsOf(app, beforeFrom, beforeTo);
                if (before <= 0) continue;
                int after = st.SecondsOf(app, afterFrom, afterTo);
                double reduction = 1.0 - (double)after / before;
                if (reduction > best) { best = reduction; bestApp = app; bestCategory = category; }
            }
            return (bestApp, bestCategory, best);
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
            return ConditionResult.Unlock(1, $"「{found}」が今週はじめて記録に出てきた", found);
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
                    return ConditionResult.Unlock(Math.Floor(minutes), $"{WeekMath.Label(day.Date)}に作業アプリ（{top}）を{seconds / 60}分使った", work[0].App);
                }
                bestMinutes = Math.Max(bestMinutes, Math.Floor(minutes));
            }
            return ConditionResult.Progress(bestMinutes);
        }
    }
}
