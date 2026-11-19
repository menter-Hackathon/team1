using System;

namespace Team1.Achievements
{
    // 実績システムの共通の型。ここ（Core フォルダ）は UnityEngine を使わない純粋な C# にしておく（EditMode テストと dotnet で確かめるため）。

    /// <summary>一覧の左に出すカテゴリ</summary>
    public enum AchievementCategory { PcUsage, Play, Dex }

    /// <summary>ランク。一覧での枠の色にだけ使う（報酬はなし）</summary>
    public enum AchievementRank { Bronze, Silver, Gold }

    /// <summary>判定のタイミング</summary>
    public enum AchievementTrigger { LaunchScan, InPlay, OnPurify }

    /// <summary>アプリのカテゴリ（AppCategoryTable で決める）。表にないアプリは Other で、デトックスにも週末エンジニアにも使わない</summary>
    public enum AppUsageCategory { Other, Entertainment, Sns, Work }

    public enum ConditionStatus { Progress, Unlocked, Skipped }

    /// <summary>条件を1回判定した結果</summary>
    public readonly struct ConditionResult
    {
        public readonly ConditionStatus Status;
        /// <summary>進み具合の現在値（目標値は定義の Target）</summary>
        public readonly double Current;
        /// <summary>解除の理由（解除したとき）、または飛ばした理由</summary>
        public readonly string Reason;
        /// <summary>達成したアプリ（分からない・決まらない実績は null）</summary>
        public readonly string App;

        ConditionResult(ConditionStatus status, double current, string reason, string app = null)
        {
            Status = status;
            Current = current;
            Reason = reason;
            App = app;
        }

        public bool IsUnlocked => Status == ConditionStatus.Unlocked;
        public bool IsSkipped => Status == ConditionStatus.Skipped;

        public static ConditionResult Unlock(double current, string reason, string app = null) =>
            new ConditionResult(ConditionStatus.Unlocked, current, reason, string.IsNullOrWhiteSpace(app) ? null : app.Trim());
        public static ConditionResult Progress(double current) => new ConditionResult(ConditionStatus.Progress, current, null);
        /// <summary>判定に使うデータが無い（スクリーンタイムが取れない・対象のイベントではない）。進み具合も変えない</summary>
        public static ConditionResult Skip(string why = null) => new ConditionResult(ConditionStatus.Skipped, 0, why);
    }

    /// <summary>実績1件の定義（ScriptableObject から作る、Unity に依存しない形）</summary>
    public sealed class AchievementSpec
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>一覧に出す、褒める1行（「朝にPCに触れた！」）</summary>
        public string Praise;
        public AchievementCategory Category;
        public AchievementRank Rank;
        public AchievementTrigger Trigger;
        public double Target = 1;
        public IAchievementCondition Condition;
        /// <summary>ゲームへの影響（新入りだけ。表示用）</summary>
        public string GameEffect;
    }

    /// <summary>解除の知らせ</summary>
    public sealed class AchievementUnlock
    {
        public AchievementSpec Spec;
        public string Reason;
        /// <summary>達成したアプリ（無ければ null）</summary>
        public string App;
        public DateTime UnlockedAt;
        /// <summary>デバッグで強制的に解除した</summary>
        public bool Forced;
    }

    public static class AchievementLabels
    {
        public static string Category(AchievementCategory c) => c switch
        {
            AchievementCategory.PcUsage => "PCの使い方",
            AchievementCategory.Play => "プレイ",
            AchievementCategory.Dex => "アプリ図鑑",
            _ => "?"
        };

        public static string Rank(AchievementRank r) => r switch
        {
            AchievementRank.Bronze => "ブロンズ",
            AchievementRank.Silver => "シルバー",
            AchievementRank.Gold => "ゴールド",
            _ => "?"
        };

        public static bool TryParseCategory(string label, out AchievementCategory category)
        {
            switch ((label ?? "").Trim())
            {
                case "PCの使い方": case "PcUsage": category = AchievementCategory.PcUsage; return true;
                case "プレイ": case "Play": category = AchievementCategory.Play; return true;
                case "アプリ図鑑": case "Dex": category = AchievementCategory.Dex; return true;
                default: category = AchievementCategory.Play; return false;
            }
        }
    }
}
