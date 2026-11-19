using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Team1.Achievements
{
    /// <summary>実績1件分の記録（保存する）</summary>
    [Serializable]
    public class AchievementRecord
    {
        public string id = "";
        public bool unlocked;
        /// <summary>解除した日時（ISO 8601）。まだなら空</summary>
        public string unlockedAt = "";
        /// <summary>解除の理由（一覧の詳細に出す）</summary>
        public string reason = "";
        /// <summary>進み具合（現在値）</summary>
        public double progress;
        /// <summary>日数を数える実績で、最後に数えた日（yyyy-MM-dd）。同じ日を二重に数えないため</summary>
        public string lastCountedDay = "";
        /// <summary>数えた数（朝型の日数・連続日数・正常化の回数など）</summary>
        public int count;
        /// <summary>補足（新入りで見つかったアプリの名前など）</summary>
        public string detail = "";
        /// <summary>達成したアプリ（一覧に出す）。無ければ空</summary>
        public string app = "";

        public DateTime? LastCountedDay
        {
            get => AchievementSaveData.ParseDay(lastCountedDay);
            set => lastCountedDay = value.HasValue ? AchievementSaveData.DayKey(value.Value) : "";
        }

        public DateTime? UnlockedAt => AchievementSaveData.ParseTime(unlockedAt);
    }

    [Serializable]
    public class PartnerClearCount
    {
        public string partner = "";
        public int clears;
    }

    /// <summary>
    /// 実績のセーブ。使用履歴そのもの（アプリごとの使用時間）は保存しない。
    /// 保存するのは、実績ごとの解除日時・進み具合・最後に数えた日、最後にスキャンした日時、相棒ごとのクリア数、
    /// 「コンプリート」の判定に使う最新の使用時間TOP10のアプリ名（時間は持たない）。
    /// </summary>
    [Serializable]
    public class AchievementSaveData
    {
        public int version = 1;
        /// <summary>最後に起動時スキャンで判定した日時（ISO 8601）。空なら、まだ一度も判定していない</summary>
        public string lastScanAt = "";
        public List<AchievementRecord> records = new List<AchievementRecord>();
        public List<PartnerClearCount> partnerClears = new List<PartnerClearCount>();
        /// <summary>最新のスキャンでの使用時間TOP10（ボスの候補）のアプリ名</summary>
        public List<string> topApps = new List<string>();

        public DateTime? LastScanAt
        {
            get => ParseTime(lastScanAt);
            set => lastScanAt = value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : "";
        }

        public AchievementRecord Find(string id) => records?.FirstOrDefault(r => r != null && r.id == id);

        public AchievementRecord GetOrCreate(string id)
        {
            if (records == null) records = new List<AchievementRecord>();
            var r = Find(id);
            if (r == null) records.Add(r = new AchievementRecord { id = id });
            return r;
        }

        public bool IsUnlocked(string id) => Find(id)?.unlocked == true;

        public int PartnerClears(string partner)
        {
            if (string.IsNullOrWhiteSpace(partner)) return 0;
            return partnerClears?.FirstOrDefault(p => string.Equals(p.partner, partner.Trim(), StringComparison.OrdinalIgnoreCase))?.clears ?? 0;
        }

        public int AddPartnerClear(string partner)
        {
            if (string.IsNullOrWhiteSpace(partner)) return 0;
            if (partnerClears == null) partnerClears = new List<PartnerClearCount>();
            var p = partnerClears.FirstOrDefault(x => string.Equals(x.partner, partner.Trim(), StringComparison.OrdinalIgnoreCase));
            if (p == null) partnerClears.Add(p = new PartnerClearCount { partner = partner.Trim() });
            return ++p.clears;
        }

        public (string Partner, int Clears) BestPartner()
        {
            var p = partnerClears?.OrderByDescending(x => x.clears).FirstOrDefault();
            return p == null ? (null, 0) : (p.partner, p.clears);
        }

        /// <summary>JsonUtility で読んだ後、null のリストを空にする</summary>
        public AchievementSaveData Normalize()
        {
            if (records == null) records = new List<AchievementRecord>();
            if (partnerClears == null) partnerClears = new List<PartnerClearCount>();
            if (topApps == null) topApps = new List<string>();
            records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
            return this;
        }

        public static string DayKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime? ParseDay(string text) =>
            !string.IsNullOrEmpty(text) && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.Date : (DateTime?)null;

        public static DateTime? ParseTime(string text) =>
            !string.IsNullOrEmpty(text) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
                ? t : (DateTime?)null;
    }
}
