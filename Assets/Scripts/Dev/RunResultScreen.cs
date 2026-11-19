using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】リザルト画面（2026-11-18：デザイン担当の画面イメージに合わせた）。UI 担当の本番画面ができたら消す。
/// ・表：クリアタイム（ステージで遊んだ時間）・駆除したウイルス（倒した雑魚）・正常化したアプリ・受けたダメージ・獲得した能力
///   正常化したアプリはアイコンで並べ、このランで初めて図鑑に色が付いたアプリは、きらきら光らせて「NEW」を付ける
/// ・右：防衛ランク（【仮】RunState.Rank）と相棒
/// ・ボタン：「ずかん」（アプリ図鑑）・「もう一度あそぶ」（同じ使用データで新しいラン）・「タイトルへ ▶」
/// 操作：← → で選び、Z か Enter で決定（最初は「タイトルへ」）
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    AppDexMenu _dex;
    int _focus = 2; // 0＝ずかん、1＝もう一度あそぶ、2＝タイトルへ

    void Awake()
    {
        _dex = GetComponent<AppDexMenu>();
        if (_dex == null) _dex = gameObject.AddComponent<AppDexMenu>();
        _dex.ShowButton = false; // ボタンはこの画面に出す
    }

    void Update()
    {
        if (_dex != null && _dex.IsOpen) return;
        var kb = Keyboard.current;
        if (kb == null || GameSession.Instance == null) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _focus = Mathf.Max(0, _focus - 1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _focus = Mathf.Min(2, _focus + 1);
        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Choose(_focus);
    }

    void Choose(int index)
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        if (index == 0 && _dex != null) _dex.Open();
        else if (index == 1 && session.CanPlayAgain) session.PlayAgain(out _);
        else if (index == 2) session.ReturnToTitle();
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        PixelGui.Begin();
        RunState run = session.Run;
        float W = PixelGui.Width, H = PixelGui.Height;
        float ox = Mathf.Round((W - 640f) * 0.5f), oy = Mathf.Round(Mathf.Max(0f, (H - 360f) * 0.5f));
        float barY = PixelGui.BottomBar(34f);

        // 後ろのウィンドウ（飾り）と、手前のウィンドウ
        PixelGui.Window(new Rect(ox + 60f, oy + 38f, 200f, 210f));
        var win = new Rect(ox + 80f, oy + 18f, 478f, Mathf.Min(286f, barY - oy - 26f));
        Rect inner = PixelGui.Window(win);
        if (PixelGui.CloseButton(new Rect(win.xMax - 30f, win.y - 6f, 44f, 44f))) Choose(2);

        float x = inner.x + 12f, y = inner.y + 8f;
        PixelGui.TextEn(new Rect(x, y, 200f, 10f), "SCAN_REPORT.TXT", 8, PixelGui.LabelGreen);
        PixelGui.Text(new Rect(x, y + 14f, 300f, 28f), "システム復旧完了", 24, PixelGui.Ink);
        PixelGui.Text(new Rect(x, y + 44f, inner.width - 24f, 16f), "すべてのウイルスを駆除し、デスクトップを取り戻した。", 12, PixelGui.SubText);

        // 表
        var table = new Rect(x, y + 66f, 284f, 150f);
        PixelGui.WhiteBox(table);
        float rowH = 29f, lx = table.x + 12f, rx = table.xMax - 12f;
        string[] labels = { "クリアタイム", "駆除したウイルス", "正常化したアプリ", "受けたダメージ", "獲得した能力" };
        for (int i = 0; i < labels.Length; i++)
        {
            float ry = table.y + 4f + i * rowH;
            PixelGui.Text(new Rect(lx, ry + 6f, 140f, 16f), labels[i], 12, PixelGui.BodyText);
            if (i < labels.Length - 1) PixelGui.DottedLine(lx, ry + rowH - 1f, table.width - 24f, PixelGui.Muted);
        }
        if (run != null)
        {
            Value(rx, table.y + 4f, PixelGui.Clock(run.PlaySeconds));
            Value(rx, table.y + 4f + rowH, run.Kills.ToString());
            Value(rx, table.y + 4f + rowH * 2f, run.Cleared.Count.ToString());
            DrawClearedApps(run, rx - 20f, table.y + 4f + rowH * 2f);
            Value(rx, table.y + 4f + rowH * 3f, run.DamageTaken.ToString());
            // 獲得した能力：ボスから手に入れた能力のアイコン（右から）
            float ix = rx - 20f;
            for (int i = run.Cleared.Count - 1; i >= 0; i--)
            {
                SkillDefinition reward = run.Cleared[i].Reward;
                if (reward == null) continue;
                var box = new Rect(ix, table.y + 4f + rowH * 4f + 4f, 20f, 20f);
                PixelGui.Fill(box, PixelGui.SlotBg);
                PixelGui.DrawSprite(PixelGui.Inset(box, 2f), reward.Icon != null ? reward.Icon : run.Cleared[i].Stage.Icon16); // アイコンが空の能力は、アプリのアイコンで
                ix -= 24f;
            }
        }

        // 防衛ランク
        var rank = new Rect(table.xMax + 12f, table.y, inner.xMax - 12f - table.xMax - 12f, 92f);
        PixelGui.Fill(rank, PixelGui.Orange);
        PixelGui.Fill(PixelGui.Inset(rank, 2f), PixelGui.Ink);
        PixelGui.Text(new Rect(rank.x, rank.y + 8f, rank.width, 16f), "防衛ランク", 12, PixelGui.Peach, TextAnchor.MiddleCenter);
        PixelGui.ShadowText(new Rect(rank.x, rank.y + 30f, rank.width, 48f), run != null ? run.Rank : "-",
            PixelGui.Style(true, 40, TextAnchor.MiddleCenter), PixelGui.Orange, PixelGui.DarkRed, 3f);

        // 相棒
        var partner = new Rect(rank.x, rank.yMax + 10f, rank.width, table.yMax - rank.yMax - 10f);
        PixelGui.WhiteBox(partner);
        PixelGui.DrawPlayer(partner.x + 8f, partner.yMax - 3f);
        PixelGui.Text(new Rect(partner.x + 44f, partner.y + 6f, 60f, 14f), "相棒", 12, PixelGui.SubText);
        string partnerName = run != null && run.Partner.Skill != null ? (run.Partner.IsFiller ? run.Partner.Skill.DisplayName : run.Partner.AppName) : "-";
        PixelGui.TextFit(new Rect(partner.x + 44f, partner.y + 22f, partner.width - 50f, 16f), partnerName, 12, PixelGui.Ink);

        // ボタン（ウィンドウの下の縁に重ねる）
        bool dexOpen = _dex != null && _dex.IsOpen;
        bool was = GUI.enabled;
        GUI.enabled = !dexOpen;
        float by = win.yMax - 16f;
        if (PixelGui.SecondaryButton(new Rect(win.x + 20f, by, 80f, 28f), "ずかん", _focus == 0 && !dexOpen)) Choose(0);
        GUI.enabled = !dexOpen && session.CanPlayAgain;
        if (PixelGui.SecondaryButton(new Rect(win.xMax - 252f, by, 112f, 28f), "もう一度あそぶ", _focus == 1 && !dexOpen)) Choose(1);
        GUI.enabled = !dexOpen;
        if (PixelGui.PrimaryButton(new Rect(win.xMax - 128f, by, 112f, 28f), "タイトルへ ▶", _focus == 2 && !dexOpen)) Choose(2);
        GUI.enabled = was;
        PixelGui.End();
    }

    static void Value(float right, float rowY, string text) =>
        PixelGui.TextEn(new Rect(right - 120f, rowY + 8f, 120f, 10f), text, 8, PixelGui.Ink, TextAnchor.MiddleRight);

    // 正常化したアプリのアイコン（数の左に並べる）。初めて図鑑に色が付いたアプリは光らせて「NEW」
    static void DrawClearedApps(RunState run, float right, float rowY)
    {
        float ix = right - 16f;
        for (int i = run.Cleared.Count - 1; i >= 0; i--)
        {
            StagePlan stage = run.Cleared[i].Stage;
            var row = new AppUsageRow { Name = stage.App.DisplayName, BundleId = stage.App.BundleIdPrefixes?.FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "", Seconds = 1 };
            Sprite icon = stage.Visual != null ? stage.Visual.Icon16 : AppVisuals.Get(row)?.Icon16;
            var r = new Rect(ix, rowY + 8f, 16f, 16f);
            PixelGui.DrawSprite(r, icon);
            if (run.NewDex.Contains(AppDex.KeyOf(stage.App.DisplayName)))
            {
                DrawSparkle(r);
                var badge = new Rect(r.x - 3f, r.y - 9f, 22f, 9f);
                PixelGui.Fill(badge, PixelGui.Magenta);
                PixelGui.TextEn(new Rect(badge.x + 1f, badge.y + 1f, badge.width, 8f), "NEW", 8, PixelGui.White, TextAnchor.MiddleCenter);
            }
            ix -= 26f;
        }
    }

    // きらきら（ゲームの絵のきらめきを、コマ送りでアイコンに重ねる）
    static void DrawSparkle(Rect icon)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.FloorToInt(Time.unscaledTime * 10f) % frames.Length];
        Rect r = f.textureRect;
        PixelGui.DrawSprite(new Rect(icon.center.x - r.width * 0.5f, icon.center.y - r.height * 0.5f, r.width, r.height), f);
    }
}
