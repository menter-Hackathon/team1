using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Team1.Achievements
{
    /// <summary>
    /// 起動時スキャン画面：前回の起動から解除した実績を、アイコン・名前・褒める一言・達成したアプリで並べる（「前回から8件達成！」）。
    /// 最後に「もうすぐ解除」の実績を1件、進み具合のバーつきで出す。解除が0件なら出さない（AchievementService が呼ばない）。
    /// 3件ずつのページ。Enter / Z で次へ（最後のページで閉じる）、Esc で閉じる。
    /// </summary>
    public sealed class AchievementScanScreen : MonoBehaviour
    {
        const int PerPage = 3;
        const float RowH = 50f;

        AchievementService _service;
        List<AchievementUnlock> _items;
        AchievementSpec _almost;
        int _page;
        int _closedFrame = -1;

        /// <summary>出している間（と閉じたフレーム）は、後ろのタイトルの操作を止める</summary>
        public static bool BlocksInput { get; private set; }

        public bool IsOpen => _items != null;
        int Pages => _items == null ? 0 : Mathf.Max(1, (_items.Count + PerPage - 1) / PerPage);

        void Awake() => _service = GetComponent<AchievementService>();

        public void Show(IEnumerable<AchievementUnlock> unlocks, AchievementSpec almost)
        {
            _items = unlocks?.Where(u => u?.Spec != null).ToList();
            if (_items == null || _items.Count == 0) { _items = null; return; }
            _almost = almost;
            _page = 0;
            BlocksInput = true;
        }

        void Close()
        {
            _items = null;
            _closedFrame = Time.frameCount;
            GameAudio.Event("ui_decide");
        }

        void Update()
        {
            if (RunTitleMenu.IsBootPlaying) return; // 起動の演出（ロック画面・デスクトップ）の間は待ち、タイトルの窓が開いてから出す（2026-11-20）
            if (!IsOpen)
            {
                if (BlocksInput && Time.frameCount > _closedFrame) BlocksInput = false;
                return;
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Next();
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _page = Mathf.Min(Pages - 1, _page + 1);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _page = Mathf.Max(0, _page - 1);
        }

        void Next()
        {
            if (_page < Pages - 1) { _page++; GameAudio.Event("ui_move"); }
            else Close();
        }

        void OnGUI()
        {
            GUI.depth = -90; // タイトル・一覧より手前（GUI.depth は戻さない）
            if (!IsOpen || RunTitleMenu.IsBootPlaying) return;
            PixelGui.Begin(false);
            float W = PixelGui.Width, H = PixelGui.Height;
            PixelGui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.04f, 0.08f, 0.45f));

            int rows = Mathf.Min(PerPage, _items.Count);
            float h = 48f + 22f + rows * RowH + (_almost != null ? 58f : 0f) + 36f;
            var win = new Rect(Mathf.Round((W - 440f) * 0.5f), Mathf.Round(Mathf.Max(4f, (H - h) * 0.5f)), 440f, h);
            Rect inner = MacDesktopGui.Window(win, "実績スキャン", out var action);
            if (action == MacDesktopGui.WindowAction.Close || action == MacDesktopGui.WindowAction.Minimize) { Close(); PixelGui.End(); return; }

            float x = inner.x + 2f, y = inner.y, w = inner.width - 4f;
            PixelGui.Text(new Rect(x, y, w, 18f), $"前回から{_items.Count}件達成！", 12, MacDesktopGui.Ink);
            y += 22f;
            for (int i = _page * PerPage; i < Mathf.Min(_items.Count, (_page + 1) * PerPage); i++)
            {
                DrawUnlock(new Rect(x, y, w, RowH - 4f), _items[i]);
                y += RowH;
            }
            if (_almost != null)
            {
                DrawAlmost(new Rect(x, y + 4f, w, 50f), _almost);
            }

            float by = win.yMax - 32f;
            if (Pages > 1)
            {
                PixelGui.Text(new Rect(win.x + 16f, by + 4f, 80f, 16f), $"{_page + 1} / {Pages}", 12, MacDesktopGui.Muted);
                bool was = GUI.enabled;
                GUI.enabled = was && _page > 0;
                if (MacDesktopGui.Button(new Rect(win.xMax - 16f - 100f - 8f - 40f, by, 40f, 24f), "◀")) _page--;
                GUI.enabled = was;
            }
            string label = _page < Pages - 1 ? "次へ ▶" : "OK";
            if (MacDesktopGui.Button(new Rect(win.xMax - 16f - 100f, by, 100f, 24f), label, true)) Next();
            PixelGui.End();
            AchievementGui.EatInput();
        }

        void DrawUnlock(Rect r, AchievementUnlock u)
        {
            MacDesktopGui.Card(r, false);
            AchievementSpec spec = u.Spec;
            AchievementGui.Icon(new Rect(r.x + 5f, r.y + 5f, 36f, 36f), _service.Definition(spec.Id), spec.Rank, true, _service.Frame(spec.Rank));
            // 実績一覧と同じく、名前・褒める一言・達成したアプリだけ（長い理由の文とランクの文字は出さない）
            string app = string.IsNullOrEmpty(u.App) ? null : $"{u.App}で達成！";
            float appW = app != null ? Mathf.Min(170f, PixelGui.Measure(app, PixelGui.Style(false, 12)) + 4f) : 0f;
            PixelGui.TextFit(new Rect(r.x + 48f, r.y + 4f, r.width - 60f - appW, 16f), spec.Name, 12, PixelGui.SubText);
            if (app != null) PixelGui.TextFit(new Rect(r.xMax - 6f - appW, r.y + 4f, appW, 16f), app, 12, MacDesktopGui.Blue, TextAnchor.MiddleRight);
            PixelGui.TextFit(new Rect(r.x + 48f, r.y + 23f, r.width - 54f, 16f), string.IsNullOrEmpty(spec.Praise) ? spec.Name : spec.Praise, 12, MacDesktopGui.Ink);
        }

        void DrawAlmost(Rect r, AchievementSpec spec)
        {
            PixelGui.Text(new Rect(r.x, r.y, r.width, 14f), "もうすぐ解除", 12, MacDesktopGui.Blue);
            var box = new Rect(r.x, r.y + 16f, r.width, r.height - 16f);
            MacDesktopGui.Card(box, false);
            AchievementGui.Icon(new Rect(box.x + 5f, box.y + 3f, 28f, 28f), _service.Definition(spec.Id), spec.Rank, false, null);
            // 解除した実績の行と同じく短く：褒める一言と名前、進み具合はバーだけ（数字は出さない）
            float nameW = Mathf.Min(150f, PixelGui.Measure(spec.Name, PixelGui.Style(false, 12)) + 4f);
            PixelGui.TextFit(new Rect(box.x + 40f, box.y + 2f, box.width - 52f - nameW, 16f), string.IsNullOrEmpty(spec.Praise) ? spec.Name : spec.Praise, 12, MacDesktopGui.Ink);
            PixelGui.TextFit(new Rect(box.xMax - 6f - nameW, box.y + 2f, nameW, 16f), spec.Name, 12, PixelGui.SubText, TextAnchor.MiddleRight);
            AchievementGui.Bar(new Rect(box.x + 40f, box.y + 22f, box.width - 46f, 5f), (float)_service.Engine.Ratio(spec), AchievementGui.BarFill);
        }
    }
}
