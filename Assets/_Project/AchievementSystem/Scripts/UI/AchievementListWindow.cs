using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Team1.Achievements
{
    /// <summary>
    /// 実績一覧（タイトルのメニュー「じっせき」から開く）。見た目はアプリ図鑑と同じデスクトップのウィンドウ。
    /// ・上：解除数／全体数と達成率のバー
    /// ・左：カテゴリ（すべて／PCの使い方／プレイ／アプリ図鑑）と各カテゴリの解除数
    /// ・中央：アイコンのグリッド（解除済みはランク枠＋通常アイコン、未解除は灰色の背景＋_Locked アイコン。途中まで進んだ実績にはバー）
    /// ・右：選んだ実績の名前、褒める1行（CSV の praise）、達成したアプリだけ（条件・進み具合・判定のデータ・達成日は出さない）
    /// キー：矢印で選ぶ、Q / E でカテゴリ、Esc・X で閉じる。
    /// </summary>
    public sealed class AchievementListWindow : MonoBehaviour
    {
        const float WinW = 600f, WinH = 318f, SideW = 136f, DetailW = 176f, Cell = 48f, Cols = 5;

        static readonly AchievementCategory?[] Filters = { null, AchievementCategory.PcUsage, AchievementCategory.Play, AchievementCategory.Dex };

        readonly MacDesktopGui.WindowState _window = new MacDesktopGui.WindowState(false);
        readonly List<AchievementSpec> _visible = new List<AchievementSpec>();
        int _filter;
        int _selected;
        int _dismissedFrame = -1;

        public bool IsOpen => _window.IsVisible;
        public bool BlocksParentInput => IsOpen || _dismissedFrame == Time.frameCount;

        static AchievementService Service => AchievementService.Instance;

        public void Open()
        {
            _window.Open();
            ApplyFilter();
        }

        public void Close()
        {
            _window.Close();
            _dismissedFrame = Time.frameCount;
        }

        void ApplyFilter()
        {
            _visible.Clear();
            if (Service != null)
                _visible.AddRange(Service.Engine.Specs.Where(s => Filters[_filter] == null || s.Category == Filters[_filter].Value));
            _selected = _visible.Count > 0 ? Mathf.Clamp(_selected, 0, _visible.Count - 1) : -1;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (!IsOpen || kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Close(); return; }
            if (kb.qKey.wasPressedThisFrame) { _filter = (_filter + Filters.Length - 1) % Filters.Length; _selected = 0; ApplyFilter(); }
            if (kb.eKey.wasPressedThisFrame) { _filter = (_filter + 1) % Filters.Length; _selected = 0; ApplyFilter(); }
            if (_visible.Count == 0) return;
            int step = 0;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) step = -(int)Cols;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) step = (int)Cols;
            if (step != 0)
            {
                _selected = Mathf.Clamp(_selected + step, 0, _visible.Count - 1);
                GameAudio.Event("ui_move");
            }
        }

        void OnGUI()
        {
            // タイトル・図鑑のウィンドウより手前（GUI.depth は戻さない。ほかの画面と同じやり方）
            GUI.depth = -60;
            if (!IsOpen || Service == null) return;
            if (GameSession.Instance != null && GameSession.Instance.IsTransitioning) return;
            PixelGui.Begin(false);
            float W = PixelGui.Width, H = PixelGui.Height;
            Rect win = _window.Apply(MacDesktopGui.CenteredWindow(W, H, WinW, WinH, false), W, H);
            Rect inner = MacDesktopGui.Window(win, "じっせき", out var action);
            if (action == MacDesktopGui.WindowAction.Close || action == MacDesktopGui.WindowAction.Minimize) Close();
            else if (action == MacDesktopGui.WindowAction.Maximize) _window.ToggleMaximize();
            if (IsOpen) Draw(inner);
            PixelGui.End();
            if (IsOpen) AchievementGui.EatInput();
        }

        void Draw(Rect inner)
        {
            var engine = Service.Engine;

            // 上：解除数／全体数と達成率
            var (unlocked, total) = engine.Count();
            float ratio = total > 0 ? (float)unlocked / total : 0f;
            PixelGui.Text(new Rect(inner.x, inner.y, 150f, 18f), $"解除 {unlocked} / {total}", 12, MacDesktopGui.Ink);
            AchievementGui.Bar(new Rect(inner.x + 110f, inner.y + 7f, inner.width - 170f, 6f), ratio, AchievementGui.BarFill);
            PixelGui.Text(new Rect(inner.xMax - 54f, inner.y, 54f, 18f), $"{Mathf.RoundToInt(ratio * 100)}%", 12, MacDesktopGui.Blue, TextAnchor.MiddleRight);

            float top = inner.y + 24f, height = inner.yMax - top;

            // 左：カテゴリ
            var side = new Rect(inner.x, top, SideW, height);
            PixelGui.Fill(side, new Color32(232, 237, 245, 255));
            for (int i = 0; i < Filters.Length; i++)
            {
                var row = new Rect(side.x + 4f, side.y + 6f + i * 26f, side.width - 8f, 22f);
                if (i == _filter) MacDesktopGui.Rounded(row, MacDesktopGui.Blue, 4);
                Color ink = i == _filter ? Color.white : MacDesktopGui.Ink;
                string label = Filters[i] == null ? "すべて" : AchievementLabels.Category(Filters[i].Value);
                var (u, t) = engine.Count(Filters[i]);
                PixelGui.TextFit(new Rect(row.x + 6f, row.y + 3f, row.width - 40f, 16f), label, 12, ink);
                PixelGui.Text(new Rect(row.xMax - 40f, row.y + 3f, 36f, 16f), $"{u}/{t}", 12, i == _filter ? Color.white : MacDesktopGui.Muted, TextAnchor.MiddleRight);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && _filter != i)
                {
                    _filter = i;
                    _selected = 0;
                    ApplyFilter();
                    GameAudio.Event("ui_move");
                }
            }
            PixelGui.Text(new Rect(side.x + 6f, side.yMax - 20f, side.width - 12f, 16f), "Q / E：切り替え", 12, MacDesktopGui.Muted);

            // 中央：アイコンのグリッド
            float gx = side.xMax + 10f, gy = top + 4f;
            for (int i = 0; i < _visible.Count; i++)
            {
                AchievementSpec spec = _visible[i];
                var cell = new Rect(gx + (i % (int)Cols) * Cell, gy + (i / (int)Cols) * (Cell + 4f), Cell, Cell + 4f);
                bool isUnlocked = engine.Save.IsUnlocked(spec.Id);
                if (i == _selected) MacDesktopGui.Rounded(cell, new Color32(198, 220, 252, 255), 4);
                var icon = new Rect(cell.x + 4f, cell.y + 2f, 40f, 40f);
                AchievementGui.Icon(icon, Service.Definition(spec.Id), spec.Rank, isUnlocked, Service.Frame(spec.Rank));
                double r = engine.Ratio(spec);
                if (!isUnlocked && r > 0) AchievementGui.Bar(new Rect(icon.x, icon.yMax + 3f, icon.width, 3f), (float)r, AchievementGui.BarFill);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) { _selected = i; GameAudio.Event("ui_move"); }
            }
            if (_visible.Count == 0)
                PixelGui.Text(new Rect(gx, gy, 240f, 18f), "実績の定義がありません", 12, MacDesktopGui.Muted);

            // 右：詳細
            var detail = new Rect(inner.xMax - DetailW, top, DetailW, height);
            PixelGui.Fill(new Rect(detail.x - 6f, detail.y, 1f, detail.height), MacDesktopGui.BorderColor);
            if (_selected >= 0 && _selected < _visible.Count) DrawDetail(detail, _visible[_selected]);
        }

        void DrawDetail(Rect r, AchievementSpec spec)
        {
            AchievementRecord record = Service.Engine.Save.Find(spec.Id);
            bool isUnlocked = record != null && record.unlocked;
            AchievementGui.Icon(new Rect(r.x, r.y + 2f, 44f, 44f), Service.Definition(spec.Id), spec.Rank, isUnlocked, Service.Frame(spec.Rank));
            PixelGui.TextFit(new Rect(r.x + 52f, r.y + 16f, r.width - 52f, 16f), spec.Name, 12, isUnlocked ? MacDesktopGui.Ink : MacDesktopGui.Muted);

            // 褒める1行と、達成したアプリだけ
            string praise = string.IsNullOrEmpty(spec.Praise) ? spec.Name : spec.Praise;
            PixelGui.TextFit(new Rect(r.x, r.y + 58f, r.width, 16f), praise, 12, isUnlocked ? MacDesktopGui.Ink : MacDesktopGui.Muted);
            if (isUnlocked)
                PixelGui.TextFit(new Rect(r.x, r.y + 78f, r.width, 16f), AchievedLine(record), 12, MacDesktopGui.Blue);
        }

        /// <summary>「11/19　Unityで達成！」。アプリが分からない実績は「11/19　達成！」</summary>
        static string AchievedLine(AchievementRecord record)
        {
            System.DateTime? at = record.UnlockedAt;
            string date = at.HasValue ? $"{at.Value.Month}/{at.Value.Day}　" : "";
            return string.IsNullOrEmpty(record.app) ? $"{date}達成！" : $"{date}{record.app}で達成！";
        }
    }
}
