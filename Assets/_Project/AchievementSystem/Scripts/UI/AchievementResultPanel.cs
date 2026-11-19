using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>リザルトの「今回解除した実績」の欄（RunResultScreen から呼ぶ）</summary>
    public static class AchievementResultPanel
    {
        const float IconSize = 22f, Gap = 3f;

        public static void Draw(Rect r)
        {
            var service = AchievementService.Instance;
            PixelGui.Text(new Rect(r.x, r.y, r.width, 14f), "今回解除した実績", 12, PixelGui.SubText);
            if (service == null) return;
            var list = service.RunUnlocks;
            float y = r.y + 17f;
            if (list.Count == 0)
            {
                PixelGui.Text(new Rect(r.x, y + 3f, r.width, 16f), "なし", 12, PixelGui.SubText);
                return;
            }
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
            else if (list.Count <= 2)
            {
                // 少ないときは名前も出す
                float nx = r.x + shown * (IconSize + Gap) + 4f;
                string names = list.Count == 1 ? list[0].Spec.Name : $"{list[0].Spec.Name}・{list[1].Spec.Name}";
                PixelGui.TextFit(new Rect(nx, y + 3f, r.xMax - nx, 16f), names, 12, PixelGui.Ink);
            }
        }
    }
}
