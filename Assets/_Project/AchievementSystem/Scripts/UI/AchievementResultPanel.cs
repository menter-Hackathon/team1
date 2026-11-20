using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// リザルトの「今回解除した実績」の欄（RunResultScreen から呼ぶ）。
    /// ほかの画面と同じく短く：「今回3件達成！」とアイコンだけ。1件のときは、褒める一言と達成したアプリも出す
    /// </summary>
    public static class AchievementResultPanel
    {
        const float IconSize = 22f, Gap = 3f;

        public static void Draw(Rect r)
        {
            var service = AchievementService.Instance;
            if (service == null) return;
            var list = service.RunUnlocks;
            if (list.Count == 0)
            {
                PixelGui.Text(new Rect(r.x, r.y, r.width, 14f), "今回の達成はなし", 12, PixelGui.SubText);
                return;
            }
            PixelGui.Text(new Rect(r.x, r.y, r.width, 14f), $"今回{list.Count}件達成！", 12, PixelGui.Ink);

            float y = r.y + 17f;
            int fit = Mathf.Max(1, Mathf.FloorToInt((r.width + Gap) / (IconSize + Gap)));
            bool overflow = list.Count > fit;
            int shown = overflow ? fit - 1 : list.Count;
            for (int i = 0; i < shown; i++)
            {
                var spec = list[i].Spec;
                var icon = new Rect(r.x + i * (IconSize + Gap), y, IconSize, IconSize);
                AchievementGui.Icon(icon, service.Definition(spec.Id), spec.Rank, true, service.Frame(spec.Rank));
            }
            if (overflow)
                PixelGui.Text(new Rect(r.x + shown * (IconSize + Gap), y + 3f, 40f, 16f), $"+{list.Count - shown}", 12, PixelGui.Ink);
            else if (list.Count == 1)
            {
                // 1件だけなら、アイコンの横に褒める一言（と達成したアプリ）
                var u = list[0];
                float nx = r.x + IconSize + 6f;
                string praise = string.IsNullOrEmpty(u.Spec.Praise) ? u.Spec.Name : u.Spec.Praise;
                string line = string.IsNullOrEmpty(u.App) ? praise : $"{praise}　{u.App}で達成！";
                PixelGui.TextFit(new Rect(nx, y + 3f, r.xMax - nx, 16f), line, 12, PixelGui.Ink);
            }
        }
    }
}
