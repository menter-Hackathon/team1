using System;
using System.Collections.Generic;
using System.Linq;

namespace Team1.Achievements
{
    public interface IAppCategoryResolver
    {
        AppUsageCategory Resolve(string appName);
    }

    /// <summary>
    /// アプリ名とカテゴリの対応（AppCategoryTable の中身を、Unity に依存しない形にしたもの）。
    /// 照合のしかたは既存のスクリーンタイムの照合（MacScreenTime）と同じ：名前が同じか「名前 -」で始まる（「Discord - 一緒にしゃべっ…」など）。大文字・小文字は区別しない。
    /// </summary>
    public sealed class AppCategoryMap : IAppCategoryResolver
    {
        public static readonly AppCategoryMap Empty = new AppCategoryMap();

        readonly List<(string Key, AppUsageCategory Category)> _keys = new List<(string, AppUsageCategory)>();

        public AppCategoryMap Add(AppUsageCategory category, params string[] keys)
        {
            foreach (var k in keys ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(k)) _keys.Add((k.Trim(), category));
            return this;
        }

        public AppUsageCategory Resolve(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName)) return AppUsageCategory.Other;
            string name = appName.Trim();
            foreach (var (key, category) in _keys)
                if (Matches(name, key)) return category;
            return AppUsageCategory.Other;
        }

        public static bool Matches(string name, string key) =>
            !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key) &&
            (name.Trim().Equals(key.Trim(), StringComparison.OrdinalIgnoreCase) ||
             name.Trim().StartsWith(key.Trim() + " -", StringComparison.OrdinalIgnoreCase));

        /// <summary>どちらかがもう一方の名前として通るか（相棒の名前と図鑑の名前を比べるとき）</summary>
        public static bool SameApp(string a, string b) => Matches(a, b) || Matches(b, a);

        public static bool IsDetoxTarget(AppUsageCategory c) => c == AppUsageCategory.Entertainment || c == AppUsageCategory.Sns;

        public static AppUsageCategory ParseCategory(string label) => (label ?? "").Trim() switch
        {
            "エンタメ" => AppUsageCategory.Entertainment,
            "SNS" => AppUsageCategory.Sns,
            "作業" => AppUsageCategory.Work,
            _ => AppUsageCategory.Other
        };

        public static string Label(AppUsageCategory c) => c switch
        {
            AppUsageCategory.Entertainment => "エンタメ",
            AppUsageCategory.Sns => "SNS",
            AppUsageCategory.Work => "作業",
            _ => "その他"
        };

        /// <summary>
        /// 照合に使う名前。match_key があればそれを「|」で区切って使い、空ならアプリ名を使う。
        /// アプリ名の「Excel / Word / PowerPoint」のようなまとめ書きは「 / 」で分ける。
        /// </summary>
        public static string[] KeysOf(string appName, string matchKey)
        {
            string source = string.IsNullOrWhiteSpace(matchKey) ? appName ?? "" : matchKey;
            string[] parts = string.IsNullOrWhiteSpace(matchKey)
                ? source.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)
                : source.Split('|');
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        }
    }
}
