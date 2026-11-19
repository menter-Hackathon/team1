using System;
using System.IO;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// 実績のセーブ（Application.persistentDataPath/achievements.json）。アプリ図鑑（app_dex.json）と同じやり方。
    /// </summary>
    public static class AchievementSaveStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "achievements.json");

        public static AchievementSaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return (JsonUtility.FromJson<AchievementSaveData>(File.ReadAllText(FilePath)) ?? new AchievementSaveData()).Normalize();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"実績の記録を読めませんでした（新しく始めます）\n{e.Message}");
            }
            return new AchievementSaveData();
        }

        public static void Save(AchievementSaveData data)
        {
            if (data == null) return;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"実績の記録を保存できませんでした\n{e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"実績の記録を消せませんでした\n{e.Message}");
            }
        }
    }
}
