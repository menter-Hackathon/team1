using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】アプリ図鑑の画面（OnGUI。2026-11-17 まさぴ〜決定。2026-11-18 画面のイメージの色と部品に合わせた）。UI 担当の本番画面ができたら差し替える。
/// ・ボスの候補（上位10アプリ）に入ったアプリを、ドット絵のアイコン（24 ドットを2倍）で並べる
/// ・まだ正常化していないアプリはグレー、正常化したアプリは色付きで「×回数」
/// ・上に「色が付いた数 / 全体」とメーター、下の欄に選んだアプリの詳しい表示（回数・初めての日・手に入る能力）
/// ・ShowButton なら、閉じているときに画面の右上に「ずかん」ボタンを出す（タイトル・リザルトは自分のメニューから Open する）
/// ・ほかの画面より手前に描き、開いている間は下の画面にクリックを通さない。矢印キーで選び、Esc・X・× で閉じる
/// </summary>
public class AppDexMenu : MonoBehaviour
{
    [Tooltip("閉じているときに、画面の右上に「図鑑」ボタンを出す")]
    [SerializeField] bool _showButton = true;

    List<AppDexEntry> _entries = new List<AppDexEntry>();
    int _selected = -1;
    Vector2 _scroll;
    int _cols = 6;

    public bool IsOpen { get; private set; }
    public bool ShowButton { get => _showButton; set => _showButton = value; }

    public void Open()
    {
        _entries = AppDex.Entries.ToList();
        AppVisuals.Prepare(_entries.Select(e => e.Row)); // アイコンをまとめて読む（まだのアプリだけ）
        _selected = _entries.Count > 0 ? 0 : -1;
        _scroll = Vector2.zero;
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    void Update()
    {
        var kb = Keyboard.current;
        if (!IsOpen || kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Close();
        if (_entries.Count == 0) return;
        int step = 0;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) step = -_cols;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) step = _cols;
        if (step != 0) _selected = Mathf.Clamp(_selected + step, 0, _entries.Count - 1);
    }

    void OnGUI()
    {
        GUI.depth = -50; // タイトル・リザルトの画面より手前
        if (GameSession.Instance != null && GameSession.Instance.IsTransitioning) return;
        PixelGui.Begin(false);
        if (!IsOpen)
        {
            if (_showButton && PixelGui.SecondaryButton(new Rect(PixelGui.Width - 86f, 8f, 76f, 24f), "ずかん")) Open();
            PixelGui.End();
            return;
        }
        DrawDex();
        PixelGui.End();
        // 開いている間は、下の画面にクリックやキーを通さない
        EventType t = Event.current.type;
        if (t == EventType.MouseDown || t == EventType.MouseUp || t == EventType.MouseDrag || t == EventType.ScrollWheel || t == EventType.KeyDown) Event.current.Use();
    }

    void DrawDex()
    {
        float W = PixelGui.Width, H = PixelGui.Height;
        PixelGui.Fill(new Rect(0f, 0f, W, H), new Color(0.12f, 0.03f, 0.19f, 0.55f)); // 後ろを暗い紫に
        var win = new Rect(Mathf.Round((W - 600f) * 0.5f), Mathf.Round(Mathf.Max(8f, (H - 340f) * 0.5f)), 600f, Mathf.Min(340f, H - 16f));
        Rect inner = PixelGui.Window(win);
        if (PixelGui.CloseButton(new Rect(win.xMax - 30f, win.y - 6f, 44f, 44f))) { Close(); return; }

        float x = inner.x + 12f, y = inner.y + 8f;
        PixelGui.TextEn(new Rect(x, y, 200f, 10f), "APP_DEX.EXE", 8, PixelGui.LabelPurple);
        PixelGui.Text(new Rect(x, y + 14f, 200f, 20f), "アプリ図鑑", 16, PixelGui.Ink);
        int done = _entries.Count(e => e.IsDefeated);
        PixelGui.TextEn(new Rect(x + 110f, y + 19f, 120f, 10f), $"{done} / {_entries.Count}", 8, PixelGui.LabelPurple);
        PixelGui.Meter(new Rect(x + 180f, y + 20f, 120f, 6f), Mathf.Max(1, _entries.Count), _entries.Count > 0 ? done / (float)_entries.Count : 0f, PixelGui.Orange, PixelGui.Muted);

        if (_entries.Count == 0)
        {
            PixelGui.Text(new Rect(inner.x, inner.y + 90f, inner.width, 40f), "まだアプリがありません\nスキャンか訓練モードで遊ぶと、ボスの候補のアプリが載ります", 12, PixelGui.SubText, TextAnchor.MiddleCenter, true);
            return;
        }

        // アイコンの一覧（多ければ縦にスクロール）
        const float cellW = 80f, cellH = 76f;
        float detailH = 58f;
        var gridArea = new Rect(x, y + 40f, inner.width - 24f, inner.yMax - (y + 40f) - detailH - 14f);
        _cols = Mathf.Max(1, Mathf.FloorToInt((gridArea.width - 12f) / cellW));
        int rows = Mathf.CeilToInt(_entries.Count / (float)_cols);
        var content = new Rect(0f, 0f, gridArea.width - 16f, rows * cellH);
        // 選んだアプリが見えるようにスクロールする
        float selTop = (_selected / _cols) * cellH;
        if (selTop < _scroll.y) _scroll.y = selTop;
        if (selTop + cellH > _scroll.y + gridArea.height) _scroll.y = selTop + cellH - gridArea.height;
        _scroll = GUI.BeginScrollView(gridArea, _scroll, content);
        for (int i = 0; i < _entries.Count; i++)
        {
            AppDexEntry e = _entries[i];
            var cell = new Rect((i % _cols) * cellW, (i / _cols) * cellH, cellW, cellH);
            var iconRect = new Rect(cell.x + (cellW - 48f) * 0.5f, cell.y + 6f, 48f, 48f);
            var plate = new Rect(iconRect.x - 4f, iconRect.y - 4f, 56f, 56f);
            if (i == _selected) { PixelGui.Fill(plate, PixelGui.DarkRed); PixelGui.Fill(PixelGui.Inset(plate, 1f), PixelGui.Orange); PixelGui.Fill(PixelGui.Inset(plate, 3f), PixelGui.White); }
            else { PixelGui.Fill(plate, PixelGui.Frame); PixelGui.Fill(PixelGui.Inset(plate, 2f), e.IsDefeated ? PixelGui.White : PixelGui.Card); }
            PixelGui.DrawSprite(iconRect, IconOf(e, 24));
            if (e.IsDefeated)
            {
                var badge = new Rect(plate.xMax - 20f, plate.y - 4f, 24f, 12f);
                PixelGui.Fill(badge, PixelGui.Orange);
                PixelGui.TextEn(new Rect(badge.x, badge.y + 2f, badge.width, 8f), $"x{Mathf.Min(99, e.defeats)}", 8, PixelGui.White, TextAnchor.MiddleCenter);
            }
            PixelGui.TextFit(new Rect(cell.x + 2f, plate.yMax + 2f, cellW - 4f, 14f), e.name, 12, e.IsDefeated ? PixelGui.Ink : PixelGui.SubText, TextAnchor.MiddleCenter);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) _selected = i;
        }
        GUI.EndScrollView();

        // 選んだアプリの詳しい表示
        if (_selected < 0 || _selected >= _entries.Count) return;
        AppDexEntry s = _entries[_selected];
        var detail = new Rect(x, inner.yMax - detailH - 6f, inner.width - 24f, detailH);
        PixelGui.WhiteBox(detail);
        PixelGui.DrawSprite(new Rect(detail.x + 6f, detail.y + 5f, 48f, 48f), IconOf(s, 24));
        PixelGui.TextFit(new Rect(detail.x + 62f, detail.y + 8f, detail.width * 0.5f - 62f, 18f), s.name, 16, PixelGui.Ink);
        string line = s.IsDefeated ? $"正常化 {s.defeats}回　はじめて {ShortDate(s.firstDefeated)}" : "まだ正常化していない";
        PixelGui.Text(new Rect(detail.x + 62f, detail.y + 32f, detail.width * 0.5f - 62f, 16f), line, 12, PixelGui.SubText);
        float sx = detail.x + detail.width * 0.55f;
        PixelGui.Text(new Rect(sx, detail.y + 8f, 120f, 14f), "固有能力", 12, PixelGui.LabelPurple);
        if (s.IsDefeated)
        {
            var box = new Rect(sx, detail.y + 26f, 24f, 24f);
            PixelGui.Fill(box, PixelGui.SlotBg);
            PixelGui.DrawSprite(new Rect(box.x + 4f, box.y + 4f, 16f, 16f), SkillIconOf(s));
            PixelGui.TextFit(new Rect(sx + 30f, detail.y + 28f, detail.xMax - sx - 36f, 18f), string.IsNullOrEmpty(s.skill) ? "-" : s.skill, 16, PixelGui.Ink);
        }
        else
        {
            PixelGui.Text(new Rect(sx, detail.y + 28f, detail.xMax - sx - 6f, 18f), "？？？", 16, PixelGui.SubText);
        }
    }

    /// <summary>アプリのアイコン（正常化していなければグレー）</summary>
    static Sprite IconOf(AppDexEntry e, int size)
    {
        AppVisual v = AppVisuals.Get(e.Row);
        if (v == null) return null;
        Sprite icon = size >= 24 ? v.Icon24 : v.Icon16;
        return e.IsDefeated ? icon : PixelIcon.Grayscale(icon);
    }

    /// <summary>手に入る能力のアイコン（カテゴリの能力ならそのアイコン、その場で作った能力ならアプリのアイコン）</summary>
    static Sprite SkillIconOf(AppDexEntry e)
    {
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        AbilityCategoryEntry category = abilities != null ? abilities.Find(e.Row) : null;
        if (category != null && category.Skill != null && category.Skill.Icon != null) return category.Skill.Icon;
        return AppVisuals.Get(e.Row)?.Icon16;
    }

    static string ShortDate(string date) => date != null && date.Length >= 10 ? $"{int.Parse(date.Substring(5, 2))}/{int.Parse(date.Substring(8, 2))}" : "-";
}
