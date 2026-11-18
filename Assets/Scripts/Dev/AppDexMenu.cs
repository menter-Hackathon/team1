using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】アプリ図鑑の画面（OnGUI。2026-11-17 まさぴ〜決定）。UI 担当の本番画面ができたら差し替える。
/// ・ボスの候補（上位10アプリ）に入ったアプリを、ドット絵のアイコン（24 ドットを2倍）で並べる
/// ・まだ正常化していないアプリはグレー、正常化したアプリは色付きで「×回数」
/// ・上の帯に「色が付いた数 / 全体」、下の欄に選んだアプリの詳しい表示（回数・初めての日・手に入る能力）
/// ・閉じているときは、画面の右上に「図鑑」ボタンを出す（タイトル画面。リザルト画面はボタンを自分で出す）
/// ・ほかの画面より手前に描き、開いている間は下の画面にクリックを通さない。Esc か × で閉じる
/// </summary>
public class AppDexMenu : MonoBehaviour
{
    [Tooltip("閉じているときに、画面の右上に「図鑑」ボタンを出す")]
    [SerializeField] bool _showButton = true;

    static readonly Color Red = new Color32(0xc8, 0x24, 0x32, 0xff);
    static readonly Color Orange = new Color32(0xdf, 0x71, 0x26, 0xff);
    static readonly Color Dark = new Color32(0x22, 0x22, 0x22, 0xff);
    static readonly Color Grey = new Color32(0x8a, 0x8a, 0x8a, 0xff);

    List<AppDexEntry> _entries = new List<AppDexEntry>();
    int _selected = -1;
    Vector2 _scroll;
    Texture2D _white;
    GUIStyle _title, _count, _name, _nameGrey, _badge, _detailName, _detail, _empty;

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
        if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }

    void OnGUI()
    {
        GUI.depth = -50; // タイトル・リザルトの画面より手前
        if (GameSession.Instance != null && GameSession.Instance.IsTransitioning) return;
        PixelGui.Begin(false);
        EnsureStyles();
        if (!IsOpen)
        {
            if (_showButton && GUI.Button(new Rect(PixelGui.Width - 84f, 8f, 76f, 26f), "図鑑", PixelGui.Button)) Open();
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
        var panel = new Rect(24f, 14f, W - 48f, H - 28f);
        Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.35f)); // 後ろを少し暗く
        GUI.Box(panel, GUIContent.none, PixelGui.Panel);
        // タイトルバー（ウィルスの赤）・数・閉じる
        var bar = new Rect(panel.x + 4f, panel.y + 4f, panel.width - 8f, 20f);
        Fill(bar, Red);
        GUI.Label(new Rect(bar.x + 8f, bar.y, 200f, 20f), "アプリ図鑑", _title);
        GUI.Label(new Rect(bar.xMax - 150f, bar.y, 110f, 20f), $"{_entries.Count(e => e.IsDefeated)} / {_entries.Count}", _count);
        var close = new Rect(bar.xMax - 20f, bar.y + 3f, 14f, 14f);
        Fill(close, Orange);
        GUI.Label(close, "×", _count);
        if (GUI.Button(close, GUIContent.none, GUIStyle.none)) { Close(); return; }

        if (_entries.Count == 0)
        {
            GUI.Label(new Rect(panel.x, panel.y + 60f, panel.width, 60f), "まだアプリがありません\nスキャンか訓練モードで遊ぶと、ボスの候補のアプリが載ります", _empty);
            return;
        }

        // アイコンの一覧（多ければ縦にスクロール）
        const float cellW = 84f, cellH = 74f;
        var gridArea = new Rect(panel.x + 12f, bar.yMax + 8f, panel.width - 24f, panel.height - 24f - 20f - 70f);
        int cols = Mathf.Max(1, Mathf.FloorToInt(gridArea.width / cellW));
        int rows = Mathf.CeilToInt(_entries.Count / (float)cols);
        var content = new Rect(0f, 0f, gridArea.width - 16f, rows * cellH);
        _scroll = GUI.BeginScrollView(gridArea, _scroll, content);
        for (int i = 0; i < _entries.Count; i++)
        {
            AppDexEntry e = _entries[i];
            var cell = new Rect((i % cols) * cellW, (i / cols) * cellH, cellW, cellH);
            var iconRect = new Rect(cell.x + (cellW - 48f) * 0.5f, cell.y + 4f, 48f, 48f);
            if (i == _selected) Frame(new Rect(iconRect.x - 3f, iconRect.y - 3f, 54f, 54f), Red, 2f);
            PixelGui.DrawSprite(iconRect, IconOf(e, 24));
            if (e.IsDefeated)
            {
                var badge = new Rect(iconRect.xMax - 14f, iconRect.y - 4f, 22f, 12f);
                Fill(badge, Red);
                GUI.Label(badge, $"×{e.defeats}", _badge);
            }
            GUI.Label(new Rect(cell.x, iconRect.yMax + 2f, cellW, 14f), e.name, e.IsDefeated ? _name : _nameGrey);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) _selected = i;
        }
        GUI.EndScrollView();

        // 選んだアプリの詳しい表示
        if (_selected < 0 || _selected >= _entries.Count) return;
        AppDexEntry s = _entries[_selected];
        var detail = new Rect(panel.x + 10f, panel.yMax - 66f, panel.width - 20f, 56f);
        Fill(detail, new Color32(0x51, 0x51, 0x51, 0xff));
        Fill(new Rect(detail.x + 2f, detail.y + 2f, detail.width - 4f, detail.height - 4f), new Color32(0xf4, 0xf4, 0xf4, 0xff));
        PixelGui.DrawSprite(new Rect(detail.x + 6f, detail.y + 4f, 48f, 48f), IconOf(s, 24));
        GUI.Label(new Rect(detail.x + 62f, detail.y + 4f, detail.width * 0.5f, 22f), s.name, _detailName);
        string line = s.IsDefeated ? $"正常化 {s.defeats}回　はじめて {ShortDate(s.firstDefeated)}" : "まだ正常化していない";
        GUI.Label(new Rect(detail.x + 62f, detail.y + 28f, detail.width * 0.5f, 20f), line, _detail);
        float sx = detail.x + detail.width * 0.58f;
        if (s.IsDefeated)
        {
            PixelGui.DrawSprite(new Rect(sx, detail.y + 12f, 32f, 32f), SkillIconOf(s));
            GUI.Label(new Rect(sx + 38f, detail.y + 18f, detail.xMax - sx - 42f, 20f), $"能力：{(string.IsNullOrEmpty(s.skill) ? "-" : s.skill)}", _detail);
        }
        else
        {
            GUI.Label(new Rect(sx, detail.y + 18f, detail.xMax - sx - 4f, 20f), "能力：？？？", _detail);
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

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _title.normal.textColor = Color.white;
        _count = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight, fontSize = 11 };
        _count.normal.textColor = Color.white;
        _name = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        _name.normal.textColor = Dark;
        _nameGrey = new GUIStyle(_name);
        _nameGrey.normal.textColor = Grey;
        _badge = new GUIStyle(_name) { fontSize = 8, fontStyle = FontStyle.Bold };
        _badge.normal.textColor = Color.white;
        _detailName = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
        _detailName.normal.textColor = Dark;
        _detail = new GUIStyle(GUI.skin.label) { fontSize = 10, clipping = TextClipping.Clip };
        _detail.normal.textColor = new Color32(0x51, 0x51, 0x51, 0xff);
        _empty = new GUIStyle(_detail) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };
    }

    void Fill(Rect r, Color color)
    {
        if (_white == null)
        {
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
        Color saved = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, _white);
        GUI.color = saved;
    }

    void Frame(Rect r, Color color, float t)
    {
        Fill(new Rect(r.x, r.y, r.width, t), color);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
        Fill(new Rect(r.x, r.y, t, r.height), color);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), color);
    }
}
