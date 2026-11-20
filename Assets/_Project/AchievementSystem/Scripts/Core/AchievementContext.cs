using System;
using System.Collections.Generic;

namespace Team1.Achievements
{
    /// <summary>判定に渡すデータ</summary>
    public sealed class AchievementContext
    {
        /// <summary>判定する時点</summary>
        public DateTime Now = DateTime.Now;
        /// <summary>スクリーンタイム（起動時の判定）。取れなかったら null</summary>
        public ScreenTimeSnapshot ScreenTime;
        public IAppCategoryResolver Categories = AppCategoryMap.Empty;
        /// <summary>プレイ中のできごと（プレイ中の判定）</summary>
        public PlayEvent Play;
        /// <summary>正常化の記録（正常化したときの判定）</summary>
        public PurifyLog Purify;
        /// <summary>ゲーム内の記録（エンジンが入れる）</summary>
        public AchievementSaveData Save;
    }

    public enum PlayEventKind { StageCleared, RunCompleted }

    /// <summary>
    /// プレイ中のできごと。このゲームの「Wave」はステージ1つ分（ボスの正常化まで）。
    /// </summary>
    public sealed class PlayEvent
    {
        public PlayEventKind Kind;
        /// <summary>クリアしたステージの番号（0 から。Wave 1 ＝ 0）</summary>
        public int StageIndex;
        /// <summary>クリアしたのが最終ウイルスボス（Terminal）のステージか</summary>
        public bool IsFinalBoss;
        /// <summary>このステージで被弾したか（リトライしたら、やり直したところから数える）</summary>
        public bool TookDamage;
        /// <summary>クリアしたときに装備していたスキルの数</summary>
        public int EquippedSkills;
        /// <summary>ステージのアプリ名</summary>
        public string AppName;
        /// <summary>相棒の名前（ランのクリア）</summary>
        public string Partner;
    }

    /// <summary>正常化の記録（アプリ図鑑から作る）</summary>
    public sealed class PurifyLog
    {
        /// <summary>これまでに正常化したアプリ（種類。最終ウイルスボスは含めない）</summary>
        public HashSet<string> PurifiedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>使用時間TOP10のアプリ（最新のスキャン）</summary>
        public List<string> TopApps = new List<string>();
        /// <summary>相棒にしたアプリ（TOP10 から除く）</summary>
        public string Partner;
        /// <summary>今回正常化したアプリ</summary>
        public string LastPurified;
    }
}
