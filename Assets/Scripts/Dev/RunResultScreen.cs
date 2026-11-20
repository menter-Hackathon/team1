using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】リザルト画面（2026-11-18：デザイン担当の画面イメージに合わせた）。UI 担当の本番画面ができたら消す。
/// ・表：クリアタイム（ステージで遊んだ時間）・駆除したウイルス（倒した雑魚）・正常化したアプリ・受けたダメージ・獲得した能力
///   正常化したアプリはアイコンで並べ、このランで初めて図鑑に色が付いたアプリは、きらきら光らせて「NEW」を付ける
/// ・右：防衛ランク（【仮】RunState.Rank）と相棒
/// ・ボタン：「ずかん」（アプリ図鑑）・「もう一度あそぶ」（同じ使用データで新しいラン）・「タイトルへ ▶」
/// 操作：← → で選び、Z か Enter で決定（最初は「タイトルへ」）。左上の赤い丸でもタイトルへ
/// 2026-11-19：ウィンドウを Mac の形にした（docs/mac-look-design.md 6章の 2）。並び・中身は前のまま
/// 2026-11-19：文字を減らした（docs/ui-text-and-dex-design.md 1-7）。表の見出しは短く（タイム・ウイルス・アプリ・ダメージ・能力）、
///   「能力」の行は 能力ごとに アイコン＋絵のタグ＋名前 の組。ボタンは「ずかん」「もう一度」「タイトルへ ▶」
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    AppDexMenu _dex;
    readonly MacDesktopGui.WindowState _window = new MacDesktopGui.WindowState();
    readonly List<MacDesktopGui.DockItem> _dockApps = new List<MacDesktopGui.DockItem>();
    int _focus = 2; // 0＝ずかん、1＝もう一度あそぶ、2＝タイトルへ

    void Awake()
    {
        _dex = GetComponent<AppDexMenu>();
        if (_dex == null) _dex = gameObject.AddComponent<AppDexMenu>();
        _dex.HandleMinimizedExternally = true;
        _dex.ShowButton = false; // ボタンはこの画面に出す
    }

    void Start()
    {
#if UNITY_EDITOR
        PrepareEditorPreview();
#endif
        var session = GameSession.Instance;
        MacDesktopWallpaper.Ensure(this);
        if (session != null && session.Run != null)
            foreach (var cleared in session.Run.Cleared)
                if (cleared.Stage != null)
                    _dockApps.Add(new MacDesktopGui.DockItem(cleared.Stage.DisplayName, cleared.Stage.Icon16));
        GameAudio.ResultShown(session != null ? session.Run : null);
    }

#if UNITY_EDITOR
    // RunResult を直接再生した場合だけサンプルを用意。図鑑の解除やセーブは行わない。
    static void PrepareEditorPreview()
    {
        if (GameSession.Instance != null && GameSession.Instance.Run != null) return;
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AppCatalog>("Assets/ScriptableObjects/Run/AppCatalog.asset");
        var abilities = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCatalog>("Assets/ScriptableObjects/Run/AbilityCatalog.asset");
        var session = GameSession.EnsureExists(catalog, abilities);
        session.RandomSeed = 42;
        if (!session.PrepareRun(new SampleUsageProvider(), out string error))
        {
            Debug.LogError("リザルト確認用データの作成に失敗：" + error);
            return;
        }
        session.AutoPickLoadout();
        var run = session.Run;
        run.PlaySeconds = 522f;
        run.Kills = 118;
        run.DamageTaken = 8;
        foreach (var stage in run.Stages)
            run.Cleared.Add(new ClearRecord { Stage = stage, Reward = stage.Reward });
        run.CurrentIndex = run.Stages.Count;
        Debug.Log("[リザルト確認] サンプルデータで表示中。図鑑の解除記録は変更していません。");
    }
#endif

    void Update()
    {
        if (_dex != null && _dex.IsOpen) return;
        if (GameSession.Instance == null) return;
        int before = _focus;
        // キーボードとコントローラー（2026-11-20）。Space では決めない（今までどおり）
        if (MenuInput.Left) _focus = Mathf.Max(0, _focus - 1);
        if (MenuInput.Right) _focus = Mathf.Min(2, _focus + 1);
        if (_focus != before) GameAudio.Event("ui_move");
        if (MenuInput.Confirm(allowSpace: false)) Choose(_focus);
    }

    void Choose(int index)
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        GameAudio.Event(index == 1 ? "ui_start" : "ui_decide");
        if (index == 0 && _dex != null) _dex.Open();
        else if (index == 1 && session.CanPlayAgain) session.PlayAgain(out _);
        else if (index == 2) session.ReturnToTitle();
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning || session.IsGameOver) return;
        // 図鑑が前面の間は背後のレポートを描かず、入力も渡さない。
        // 別 MonoBehaviour の OnGUI の描画順に依存して覆い隠すのを防ぐ。
        if (_dex != null && _dex.BlocksParentInput) return;
        PixelGui.Begin(false);
        RunState run = session.Run;
        float W = PixelGui.Width, H = PixelGui.Height;
        float ox = Mathf.Round((W - 640f) * 0.5f), oy = Mathf.Round(Mathf.Max(0f, (H - 360f) * 0.5f));
        MacDesktopGui.MenuBar(W, "Virus Desktop");
        var home = new Rect(ox + 80f, oy + 24f, 478f, Mathf.Min(286f, H - oy - 92f));
        Rect win = _window.Apply(home, W, H);
        bool wasBackgroundEnabled = GUI.enabled;
        GUI.enabled = wasBackgroundEnabled && (!_window.IsVisible || !win.Contains(Event.current.mousePosition));
        MacDesktopGui.Folders(W);
        var dock = new List<MacDesktopGui.DockItem>(_dockApps);
        if (_window.IsMinimized) dock.Add(new MacDesktopGui.DockItem("システム復旧完了", null, _window));
        if (_dex != null && _dex.IsMinimized) dock.Add(new MacDesktopGui.DockItem("アプリ図鑑", null, _dex.Window));
        int clicked = MacDesktopGui.Dock(W, H, dock);
        if (clicked == 1) _window.Open();
        else if (clicked >= 2 && clicked - 2 < dock.Count && dock[clicked - 2].Window != null)
            dock[clicked - 2].Window.Open();
        GUI.enabled = wasBackgroundEnabled;
        if (!_window.IsVisible || (_dex != null && _dex.BlocksParentInput)) { PixelGui.End(); return; }

        bool dexOpen = false;
        Rect inner = MacDesktopGui.Window(win, "システム復旧レポート", out var action);
        if (action == MacDesktopGui.WindowAction.Close) { Choose(2); PixelGui.End(); return; }
        _window.Handle(action);
        if (!_window.IsVisible) { PixelGui.End(); return; }

        float x = inner.x + 12f, y = inner.y + 8f;
        PixelGui.Text(new Rect(x, y, 300f, 28f), "システム復旧完了", 24, PixelGui.Ink);
        // 2026-11-20：今回解除した実績（題名の右。表より上）
        Team1.Achievements.AchievementResultPanel.Draw(new Rect(x + 214f, y - 4f, inner.xMax - 12f - (x + 214f), 38f));
        // 2026-11-19：文字を減らした（docs/ui-text-and-dex-design.md 1-7）。説明の文は消し、表を 16 上へ。「能力」の行を 16 高くして、能力ごとの小さい組を出す

        // 表
        float rowH = 29f, abilityH = rowH + 16f;
        var table = new Rect(x, y + 36f, 284f, 4f + rowH * 4f + abilityH + 1f);
        PixelGui.WhiteBox(table);
        float lx = table.x + 12f, rx = table.xMax - 12f;
        string[] labels = { "タイム", "ウイルス", "アプリ", "ダメージ", "能力" };
        for (int i = 0; i < labels.Length; i++)
        {
            float ry = table.y + 4f + i * rowH;
            bool ability = i == labels.Length - 1;
            PixelGui.Text(new Rect(lx, ability ? ry + (abilityH - 16f) * 0.5f : ry + 6f, ability ? 40f : 140f, 16f), labels[i], 12, PixelGui.BodyText);
            if (!ability) PixelGui.DottedLine(lx, ry + rowH - 1f, table.width - 24f, PixelGui.Muted);
        }
        if (run != null)
        {
            Value(rx, table.y + 4f, PixelGui.Clock(run.PlaySeconds));
            Value(rx, table.y + 4f + rowH, run.Kills.ToString());
            Value(rx, table.y + 4f + rowH * 2f, run.Cleared.Count.ToString());
            DrawClearedApps(run, rx - 20f, table.y + 4f + rowH * 2f);
            Value(rx, table.y + 4f + rowH * 3f, run.DamageTaken.ToString());
            DrawAbilities(run, rx, table.y + 4f + rowH * 4f);
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
        bool was = GUI.enabled;
        GUI.enabled = !dexOpen;
        float by = win.yMax - 16f;
        if (PixelGui.SecondaryButton(new Rect(win.x + 20f, by, 80f, 28f), "ずかん", _focus == 0 && !dexOpen)) Choose(0);
        GUI.enabled = !dexOpen && session.CanPlayAgain;
        if (PixelGui.SecondaryButton(new Rect(win.xMax - 252f, by, 112f, 28f), "もう一度", _focus == 1 && !dexOpen)) Choose(1);
        GUI.enabled = !dexOpen;
        if (PixelGui.PrimaryButton(new Rect(win.xMax - 128f, by, 112f, 28f), "タイトルへ ▶", _focus == 2 && !dexOpen)) Choose(2);
        GUI.enabled = was;
        PixelGui.End();
    }

    static void Value(float right, float rowY, string text) =>
        PixelGui.TextEn(new Rect(right - 120f, rowY + 8f, 120f, 10f), text, 8, PixelGui.Ink, TextAnchor.MiddleRight);

    /// <summary>
    /// 「能力」の行：ボスから手に入れた能力1つにつき、幅 72 の組を右から（最大3つ。ラスボスは能力なし）。
    /// 組＝能力のアイコン（枠 20）＋絵のタグの絵だけ（9×9、間 2）、下に能力の名前（12）。1行の説明は出さない（図鑑で見られる）
    /// </summary>
    static void DrawAbilities(RunState run, float right, float rowY)
    {
        const float groupW = 72f;
        float gx = right - groupW;
        int shown = 0;
        for (int i = run.Cleared.Count - 1; i >= 0 && shown < 3; i--)
        {
            SkillDefinition reward = run.Cleared[i].Reward;
            if (reward == null) continue;
            var box = new Rect(gx, rowY + 4f, 20f, 20f);
            PixelGui.Fill(box, PixelGui.SlotBg);
            PixelGui.DrawSprite(PixelGui.Inset(box, 2f), reward.Icon != null ? reward.Icon : run.Cleared[i].Stage.Icon16); // アイコンが空の能力は、アプリのアイコンで
            var pictos = SkillBriefs.Pictos(reward);
            for (int k = 0; k < pictos.Count && k < 3; k++)
                PixelGui.DrawPicto(box.xMax + 4f + k * (SkillPictos.Size + 2f), box.y + Mathf.Floor((box.height - SkillPictos.Size) * 0.5f), pictos[k]);
            PixelGui.TextFit(new Rect(gx, box.yMax + 2f, groupW, 14f), reward.DisplayName, 12, PixelGui.Ink);
            gx -= groupW;
            shown++;
        }
    }

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
