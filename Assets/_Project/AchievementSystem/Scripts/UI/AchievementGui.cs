using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>実績の画面で共通に使う描き方（IMGUI。既存の PixelGui の上に作る）</summary>
    public static class AchievementGui
    {
        /// <summary>古い Windows のツールチップの色（黒い枠、薄い黄色の背景）</summary>
        public static readonly Color TooltipBack = new Color32(255, 255, 225, 255);
        public static readonly Color TooltipLine = new Color32(0, 0, 0, 255);
        public static readonly Color LockedBack = new Color32(196, 198, 202, 255);
        public static readonly Color LockedLine = new Color32(150, 153, 160, 255);
        public static readonly Color BarBack = new Color32(222, 226, 233, 255);
        public static readonly Color BarFill = new Color32(0, 112, 245, 255);

        public static Color RankColor(AchievementRank rank) => rank switch
        {
            AchievementRank.Gold => new Color32(205, 156, 18, 255),
            AchievementRank.Silver => new Color32(128, 138, 154, 255),
            _ => new Color32(176, 104, 52, 255)
        };

        /// <summary>アイコン：解除済みはランク枠＋通常アイコン、未解除は灰色の背景＋_Locked アイコン</summary>
        public static void Icon(Rect r, AchievementDefinition def, AchievementRank rank, bool unlocked, Sprite frame)
        {
            Rect inner = new Rect(r.x + r.width * 0.1f, r.y + r.height * 0.1f, r.width * 0.8f, r.height * 0.8f);
            if (unlocked)
            {
                if (frame != null) PixelGui.DrawSprite(r, frame);
                else
                {
                    PixelGui.Fill(r, RankColor(rank));
                    PixelGui.Fill(PixelGui.Inset(r, 2f), PixelGui.White);
                }
                if (def != null) PixelGui.DrawSprite(inner, def.Icon);
            }
            else
            {
                PixelGui.Fill(r, LockedLine);
                PixelGui.Fill(PixelGui.Inset(r, 1f), LockedBack);
                if (def != null) PixelGui.DrawSprite(inner, def.IconLocked != null ? def.IconLocked : def.Icon);
            }
        }

        public static void Bar(Rect r, float ratio, Color fill)
        {
            PixelGui.Fill(r, BarBack);
            float w = Mathf.Round(r.width * Mathf.Clamp01(ratio));
            if (w > 0f) PixelGui.Fill(new Rect(r.x, r.y, w, r.height), fill);
        }

        /// <summary>進み具合の文字（「3 / 7」。割合の実績は「%」）</summary>
        public static string ProgressText(AchievementSpec spec, AchievementRecord record)
        {
            double progress = record != null ? record.progress : 0;
            if (spec.Condition is DetoxReductionCondition)
                return $"{Mathf.FloorToInt((float)(progress * 100))}% / {Mathf.RoundToInt((float)(spec.Target * 100))}%";
            if (spec.Condition is PurifiedTop10Condition)
                return record != null && !string.IsNullOrEmpty(record.detail) ? record.detail : $"{Mathf.RoundToInt((float)(progress / spec.Target * 100))}%";
            if (spec.Condition is WeekendWorkMinutesCondition)
                return $"{(int)progress}分 / {(int)spec.Target}分";
            return $"{(int)progress} / {(int)spec.Target}";
        }

        /// <summary>この GUI がマウス・キーを使い切る（後ろの画面に通さない）</summary>
        public static void EatInput()
        {
            EventType t = Event.current.type;
            if (t == EventType.MouseDown || t == EventType.MouseUp || t == EventType.MouseDrag || t == EventType.ScrollWheel || t == EventType.KeyDown)
                Event.current.Use();
        }
    }
}
