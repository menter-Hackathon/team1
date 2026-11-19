using System;
using System.Collections.Generic;
using System.Linq;

namespace Team1.Achievements
{
    /// <summary>
    /// 1日分の使用時間（アプリごと。1時間ごとの内訳があれば持つ）。
    /// 1日の区切りは 0:00〜23:59。
    /// </summary>
    public sealed class DayUsage
    {
        sealed class AppDay
        {
            public string Name;
            public int Total;
            public readonly int[] Hours = new int[24];
        }

        readonly Dictionary<string, AppDay> _apps = new Dictionary<string, AppDay>(StringComparer.OrdinalIgnoreCase);

        internal DayUsage(DateTime date) { Date = date.Date; }

        public DateTime Date { get; }

        /// <summary>1時間ごとの内訳がそろっているか（日ごとの合計だけのデータが混ざると false）</summary>
        public bool HasHourly { get; private set; } = true;

        /// <summary>この日に使ったアプリ（使用時間が 0 より大きいもの）</summary>
        public IEnumerable<string> Apps => _apps.Values.Where(a => a.Total > 0).Select(a => a.Name);
        public int AppCount => _apps.Values.Count(a => a.Total > 0);
        public int TotalSeconds => _apps.Values.Sum(a => a.Total);

        public int SecondsOf(string app) => app != null && _apps.TryGetValue(app.Trim(), out var a) ? a.Total : 0;

        /// <summary>fromHour 時台から toHourExclusive 時台の手前まで（全アプリの合計）。内訳が無い日は 0</summary>
        public int SecondsInHours(int fromHour, int toHourExclusive)
        {
            if (!HasHourly) return 0;
            int from = Math.Max(0, fromHour), to = Math.Min(24, toHourExclusive), sum = 0;
            foreach (var a in _apps.Values)
                for (int h = from; h < to; h++) sum += a.Hours[h];
            return sum;
        }

        public IEnumerable<(string App, int Seconds)> Entries => _apps.Values.Where(a => a.Total > 0).Select(a => (a.Name, a.Total));

        AppDay Get(string app)
        {
            string name = app.Trim();
            if (!_apps.TryGetValue(name, out var a)) _apps[name] = a = new AppDay { Name = name };
            return a;
        }

        internal void AddHourly(string app, int hour, int seconds)
        {
            var a = Get(app);
            a.Hours[hour] += seconds;
            a.Total += seconds;
        }

        internal void AddDaily(string app, int seconds)
        {
            HasHourly = false;
            Get(app).Total += seconds;
        }
    }

    /// <summary>
    /// 判定に使うスクリーンタイムのデータ（日ごと・アプリごと・1時間ごと）。
    /// 既存のスクリーンタイムの取得結果や、確認用の偽データから作る。セーブには保存しない。
    /// </summary>
    public sealed class ScreenTimeSnapshot
    {
        readonly SortedDictionary<DateTime, DayUsage> _days = new SortedDictionary<DateTime, DayUsage>();
        DateTime? _coverageStart;

        public ScreenTimeSnapshot(DateTime capturedAt, DateTime? coverageStart = null)
        {
            CapturedAt = capturedAt;
            _coverageStart = coverageStart?.Date;
        }

        /// <summary>データを取った日時（判定する時点）</summary>
        public DateTime CapturedAt { get; }

        /// <summary>読める範囲の最初の日（この日より前のことは分からない）。指定が無ければ、データのある最初の日</summary>
        public DateTime CoverageStart => _coverageStart ?? FirstDate ?? CapturedAt.Date;

        public IEnumerable<DayUsage> Days => _days.Values;
        public bool IsEmpty => _days.Count == 0;
        public DateTime? FirstDate => _days.Count > 0 ? _days.Keys.First() : (DateTime?)null;
        public DateTime? LastDate => _days.Count > 0 ? _days.Keys.Last() : (DateTime?)null;

        public DayUsage GetDay(DateTime date) => _days.TryGetValue(date.Date, out var d) ? d : null;

        DayUsage Day(DateTime date)
        {
            if (!_days.TryGetValue(date.Date, out var d)) _days[date.Date] = d = new DayUsage(date);
            return d;
        }

        /// <summary>使った時間をそのまま足す。時台・日をまたぐ分は分けて入れる（8:55 から 10 分なら 8時台に5分、9時台に5分）</summary>
        public ScreenTimeSnapshot AddSession(string app, DateTime start, int seconds)
        {
            if (string.IsNullOrWhiteSpace(app) || seconds <= 0) return this;
            DateTime t = start;
            int left = seconds;
            while (left > 0)
            {
                DateTime nextHour = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).AddHours(1);
                int chunk = (int)Math.Min(left, Math.Ceiling((nextHour - t).TotalSeconds));
                Day(t).AddHourly(app, t.Hour, chunk);
                left -= chunk;
                t = nextHour;
            }
            return this;
        }

        /// <summary>1時間ごとの使用時間（スクリーンタイムの1時間の棒1本分）</summary>
        public ScreenTimeSnapshot AddHourly(string app, DateTime date, int hour, int seconds)
        {
            if (string.IsNullOrWhiteSpace(app) || seconds <= 0 || hour < 0 || hour > 23) return this;
            Day(date).AddHourly(app, hour, seconds);
            return this;
        }

        /// <summary>日ごとの合計だけが分かるとき（その日は朝型の判定ができない）</summary>
        public ScreenTimeSnapshot AddDaily(string app, DateTime date, int seconds)
        {
            if (string.IsNullOrWhiteSpace(app) || seconds <= 0) return this;
            Day(date).AddDaily(app, seconds);
            return this;
        }

        /// <summary>from の日から toExclusive の日の手前まで、あるアプリの合計</summary>
        public int SecondsOf(string app, DateTime from, DateTime toExclusive)
        {
            int sum = 0;
            foreach (var d in DaysBetween(from, toExclusive)) sum += d.SecondsOf(app);
            return sum;
        }

        /// <summary>from の日から toExclusive の日の手前までに使ったアプリ</summary>
        public HashSet<string> AppsBetween(DateTime from, DateTime toExclusive)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in DaysBetween(from, toExclusive))
                foreach (var a in d.Apps) set.Add(a);
            return set;
        }

        public IEnumerable<DayUsage> DaysBetween(DateTime from, DateTime toExclusive)
        {
            DateTime a = from.Date, b = toExclusive.Date;
            return _days.Values.Where(d => d.Date >= a && d.Date < b);
        }
    }

    /// <summary>週の区切り（月曜始まり。スクリーンタイムの画面は日曜始まりだが、日ごとのデータから月曜始まりで計算する）</summary>
    public static class WeekMath
    {
        /// <summary>その日を含む週の月曜 0:00</summary>
        public static DateTime WeekStart(DateTime date)
        {
            int back = ((int)date.DayOfWeek + 6) % 7; // 月曜=0 … 日曜=6
            return date.Date.AddDays(-back);
        }

        public static bool IsWeekend(DateTime date) => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

        public static string DayOfWeekJa(DateTime date) => "日月火水木金土"[(int)date.DayOfWeek].ToString();

        /// <summary>「11/16（日）」</summary>
        public static string Label(DateTime date) => $"{date.Month}/{date.Day}（{DayOfWeekJa(date)}）";
    }
}
