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
/// ・ほかの画面より手前に描き、開いている間は下の画面にクリックを通さない。矢印キーで選び、Esc・X・左上の赤い丸で閉じる
/// ・2026-11-19：ウィンドウを Mac の形にした（docs/mac-look-design.md 6章の 2）。並び・中身は前のまま
/// ・2026-11-19：文字を減らし、詳しい画面を足した（docs/ui-text-and-dex-design.md 1-6・3章）
///   一覧：見出しは消し、名前は正常化したアプリだけ。下の欄は細く（アイコン・名前・×回数・日付・能力・「Z ▶」。グレーのアプリは鍵の絵だけ）
///   詳しい画面：正常化したアプリで Z・Enter・Space かクリック。左にボスの姿（BossPortrait）、右に ボスの技・固有能力（1行・届く所・絵のタグ）。
///   ← → で前／次の正常化したアプリ、X・Esc・左上の赤い丸で一覧へ戻る。グレーのアプリは開かない
///   図鑑の記録（AppDex・app_dex.json）は読むだけ。この画面からは書かない
/// </summary>
public class AppDexMenu : MonoBehaviour
{
    [Tooltip("閉じているときに、画面の右上に「図鑑」ボタンを出す")]
    [SerializeField] bool _showButton = true;

    List<AppDexEntry> _entries = new List<AppDexEntry>();
    int _selected = -1;
    int _detail = -1;       // 詳しい画面で開いているアプリの番号（-1＝一覧）
    Vector2 _scroll;
    int _cols = 6;
    int _openedFrame = -1;  // 図鑑を開いたフレーム（開いたキーで、そのまま詳しい画面を開かないように）
    float _shakeUntil;      // グレーのアプリで決定したときの、枠のゆれ（0.15秒）
    int _pendingDetail = -1; // 一覧でクリックした正常化済みのアプリ（スクロールの外で開く）

    public bool IsOpen { get; private set; }
    public bool ShowButton { get => _showButton; set => _showButton = value; }
    /// <summary>詳しい画面を開いているか</summary>
    public bool IsDetailOpen => IsOpen && _detail >= 0;

    public void Open()
    {
        _entries = AppDex.Entries.ToList();
        AppVisuals.Prepare(_entries.Select(e => e.Row)); // アイコンをまとめて読む（まだのアプリだけ）
        _selected = _entries.Count > 0 ? 0 : -1;
        _detail = -1;
        _pendingDetail = -1;
        _scroll = Vector2.zero;
        _openedFrame = Time.frameCount;
        IsOpen = true;
        if (GameAudio.CurrentBgm != "bgm_dex") _backBgm = GameAudio.CurrentBgm; // 閉じたら前の画面の曲に戻す
        GameAudio.PlayBgm("bgm_dex");
    }

    string _backBgm;

    public void Close()
    {
        if (!IsOpen) return;
        _detail = -1; // 詳しい画面を開いたまま閉じられたときも、次は一覧から
        _pendingDetail = -1;
        IsOpen = false;
        GameAudio.Event("ui_cancel");
        if (!string.IsNullOrEmpty(_backBgm)) GameAudio.PlayBgm(_backBgm);
    }

    bool CanOpen(int index) => index >= 0 && index < _entries.Count && _entries[index].IsDefeated;

    /// <summary>詳しい画面を開く（正常化したアプリだけ）</summary>
    void OpenDetail(int index)
    {
        if (!CanOpen(index)) return;
        _selected = index;
        _detail = index;
        GameAudio.Event("ui_decide");
    }

    /// <summary>一覧へ戻る（選んでいるアプリは、最後に見ていたアプリ）</summary>
    void CloseDetail()
    {
        if (_detail >= 0 && _detail < _entries.Count) _selected = _detail;
        _detail = -1;
        GameAudio.Event("ui_cancel");
    }

    /// <summary>前／次の正常化したアプリへ（グレーは飛ばす。端で止まる）</summary>
    void StepDetail(int dir)
    {
        for (int i = _detail + dir; i >= 0 && i < _entries.Count; i += dir)
        {
            if (!_entries[i].IsDefeated) continue;
            _detail = i;
            _selected = i;
            GameAudio.Event("ui_move");
            return;
        }
    }

    // グレーのアプリは開かない：戻るの音と、枠が横にゆれる
    void Deny()
    {
        GameAudio.Event("ui_cancel");
        _shakeUntil = Time.unscaledTime + 0.15f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!IsOpen || kb == null || Time.frameCount == _openedFrame) return;
        bool back = kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
        bool ok = kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
        bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;

        // 詳しい画面：← → で前／次、Z・X どちらでも一覧へ（1回押して1段だけ戻る）
        if (_detail >= 0)
        {
            if (back || ok) CloseDetail();
            else if (left) StepDetail(-1);
            else if (right) StepDetail(1);
            return;
        }

        if (back)
        {
            Close();
            return;
        }
        if (_entries.Count == 0) return;
        if (ok)
        {
            if (CanOpen(_selected)) OpenDetail(_selected);
            else Deny();
            return;
        }
        int step = 0;
        if (left) step = -1;
        if (right) step = 1;
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) step = -_cols;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) step = _cols;
        if (step != 0)
        {
            int before = _selected;
            _selected = Mathf.Clamp(_selected + step, 0, _entries.Count - 1);
            if (_selected != before) GameAudio.Event("ui_move");
        }
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
        // 2026-11-19：Mac の形。題名はタイトルバーのまん中（前は APP_DEX.EXE を中の左上に）。閉じるのは左上の赤い丸＝前の右上の ×
        Rect inner = PixelGui.Window(win, "App Dex", PixelGui.LabelPurple);

        // 詳しい画面（同じウィンドウの中身を入れ替える）。左上の赤い丸は「一覧へ戻る」
        if (_detail >= 0 && CanOpen(_detail))
        {
            if (PixelGui.WindowClose(win)) { CloseDetail(); return; }
            DrawDetail(inner);
            return;
        }
        _detail = -1;
        if (PixelGui.WindowClose(win)) { Close(); return; }
        DrawGrid(inner);
        if (_pendingDetail >= 0)
        {
            int i = _pendingDetail;
            _pendingDetail = -1;
            OpenDetail(i);
        }
    }

    // ───────── 一覧 ─────────

    void DrawGrid(Rect inner)
    {
        float x = inner.x + 12f, y = inner.y + 8f;
        // 見出し「アプリ図鑑」は消し、色が付いた数とメーターを左の端へ（2026-11-19：1-6）
        int done = _entries.Count(e => e.IsDefeated);
        PixelGui.TextEn(new Rect(x, y + 19f, 120f, 10f), $"{done} / {_entries.Count}", 8, PixelGui.LabelPurple);
        PixelGui.Meter(new Rect(x + 70f, y + 20f, 120f, 6f), Mathf.Max(1, _entries.Count), _entries.Count > 0 ? done / (float)_entries.Count : 0f, PixelGui.Orange, PixelGui.Muted);

        if (_entries.Count == 0)
        {
            PixelGui.Text(new Rect(inner.x, inner.y + 90f, inner.width, 40f), "まだアプリがありません", 12, PixelGui.SubText, TextAnchor.MiddleCenter);
            return;
        }

        // アイコンの一覧（多ければ縦にスクロール）。下の欄が細くなった分（58 → 30）、一覧を広くした
        const float cellW = 80f, cellH = 76f;
        var detail = new Rect(x, inner.yMax - 36f, inner.width - 24f, 30f);
        var gridArea = new Rect(x, y + 40f, inner.width - 24f, detail.y - 8f - (y + 40f));
        _cols = Mathf.Max(1, Mathf.FloorToInt((gridArea.width - 12f) / cellW));
        int rows = Mathf.CeilToInt(_entries.Count / (float)_cols);
        var content = new Rect(0f, 0f, gridArea.width - 16f, rows * cellH);
        // 選んだアプリが見えるようにスクロールする
        float selTop = (Mathf.Max(0, _selected) / _cols) * cellH;
        if (selTop < _scroll.y) _scroll.y = selTop;
        if (selTop + cellH > _scroll.y + gridArea.height) _scroll.y = selTop + cellH - gridArea.height;
        bool shaking = Time.unscaledTime < _shakeUntil;
        float shake = shaking ? (Mathf.FloorToInt(Time.unscaledTime / 0.0375f) % 2 == 0 ? 2f : -2f) : 0f;
        _scroll = GUI.BeginScrollView(gridArea, _scroll, content);
        for (int i = 0; i < _entries.Count; i++)
        {
            AppDexEntry e = _entries[i];
            var cell = new Rect((i % _cols) * cellW, (i / _cols) * cellH, cellW, cellH);
            float dx = i == _selected ? shake : 0f;
            var iconRect = new Rect(cell.x + (cellW - 48f) * 0.5f + dx, cell.y + 6f, 48f, 48f);
            var plate = new Rect(iconRect.x - 4f, iconRect.y - 4f, 56f, 56f);
            if (i == _selected) { PixelGui.Fill(plate, PixelGui.DarkRed); PixelGui.Fill(PixelGui.Inset(plate, 1f), PixelGui.Orange); PixelGui.Fill(PixelGui.Inset(plate, 3f), PixelGui.White); }
            else { PixelGui.Fill(plate, PixelGui.Frame); PixelGui.Fill(PixelGui.Inset(plate, 2f), e.IsDefeated ? PixelGui.White : PixelGui.Card); }
            PixelGui.DrawSprite(iconRect, IconOf(e, 24));
            if (e.IsDefeated)
            {
                Badge(plate.xMax - 20f, plate.y - 4f, e.defeats);
                PixelGui.TextFit(new Rect(cell.x + 2f, plate.yMax + 2f, cellW - 4f, 14f), e.name, 12, PixelGui.Ink, TextAnchor.MiddleCenter); // 名前は正常化したアプリだけ
            }
            // クリック：正常化したアプリはすぐ詳しい画面（選ぶのも同時）。グレーのアプリは選ぶだけ
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
            {
                _selected = i;
                if (e.IsDefeated) _pendingDetail = i;
            }
        }
        GUI.EndScrollView();

        // 下の細い欄（3-2）
        if (_selected < 0 || _selected >= _entries.Count) return;
        AppDexEntry s = _entries[_selected];
        PixelGui.WhiteBox(detail);
        PixelGui.DrawSprite(new Rect(detail.x + 4f, detail.y + 3f, 24f, 24f), IconOf(s, 24));
        float sx = Mathf.Round(detail.x + detail.width * 0.55f);
        if (!s.IsDefeated)
        {
            // グレーのアプリ：名前も文も出さず、能力の所に鍵の絵（9×9 を2倍）
            PixelGui.DrawTint(new Rect(sx + 1f, detail.y + 6f, SkillPictos.Size * 2f, SkillPictos.Size * 2f), SkillPictos.Lock, PixelGui.SubText);
            return;
        }
        PixelGui.TextFit(new Rect(detail.x + 34f, detail.y + 6f, 150f, 18f), s.name, 16, PixelGui.Ink);
        Badge(detail.x + 190f, detail.y + 9f, s.defeats);
        PixelGui.Text(new Rect(detail.x + 220f, detail.y + 7f, 40f, 16f), ShortDate(s.firstDefeated), 12, PixelGui.SubText);
        var box = new Rect(sx, detail.y + 5f, 20f, 20f);
        PixelGui.Fill(box, PixelGui.SlotBg);
        PixelGui.DrawSprite(PixelGui.Inset(box, 2f), SkillIconOf(s));
        var chip = new Rect(detail.xMax - 40f, detail.y + 7f, 34f, 16f);
        PixelGui.TextFit(new Rect(box.xMax + 6f, detail.y + 7f, chip.x - 6f - (box.xMax + 6f), 16f), string.IsNullOrEmpty(s.skill) ? "-" : s.skill, 12, PixelGui.Ink);
        PixelGui.Chip(chip, "Z ▶");
        if (GUI.Button(chip, GUIContent.none, GUIStyle.none)) OpenDetail(_selected);
    }

    // ×回数の札（オレンジ・英字 8）
    static void Badge(float x, float y, int defeats)
    {
        var badge = new Rect(x, y, 24f, 12f);
        PixelGui.Fill(badge, PixelGui.Orange);
        PixelGui.TextEn(new Rect(badge.x, badge.y + 2f, badge.width, 8f), $"x{Mathf.Min(99, defeats)}", 8, PixelGui.White, TextAnchor.MiddleCenter);
    }

    // ───────── 詳しい画面（3-3） ─────────

    void DrawDetail(Rect inner)
    {
        AppDexEntry s = _entries[_detail];
        float ix = inner.x, iy = inner.y;
        float rx = ix + 252f, rw = inner.width - 264f; // 右の列

        // 左：ボスの姿
        BossPortrait portrait = BossPortrait.For(s);
        portrait.Draw(new Rect(ix + 12f, iy + 8f, 228f, 196f));

        // 前／次（正常化したアプリの中の順番）
        int count = 0, pos = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (!_entries[i].IsDefeated) continue;
            if (i == _detail) pos = count;
            count++;
        }
        if (NavButton(new Rect(ix + 12f, iy + 212f, 24f, 20f), "‹", pos > 0)) StepDetail(-1);
        if (NavButton(new Rect(ix + 216f, iy + 212f, 24f, 20f), "›", pos < count - 1)) StepDetail(1);
        PixelGui.TextEn(new Rect(ix + 36f, iy + 217f, 180f, 10f), $"{pos + 1} / {count}", 8, PixelGui.LabelPurple, TextAnchor.MiddleCenter);

        // 右上：アプリ（「正常化」「はじめて」の字は出さない）
        PixelGui.DrawSprite(new Rect(rx, iy + 8f, 32f, 32f), IconOf(s, 16));
        PixelGui.TextFit(new Rect(ix + 290f, iy + 8f, inner.width - 302f, 18f), s.name, 16, PixelGui.Ink);
        Badge(ix + 290f, iy + 30f, s.defeats);
        PixelGui.Text(new Rect(ix + 320f, iy + 28f, 60f, 14f), ShortDate(s.firstDefeated), 12, PixelGui.SubText);

        // ボスの技（2列×2段、最大4つ）
        PixelGui.DottedLine(rx, iy + 50f, rw, PixelGui.Muted);
        PixelGui.Text(new Rect(rx, iy + 56f, rw, 14f), "ボスの技", 12, PixelGui.LabelPurple);
        float cw = Mathf.Floor((rw - 4f) * 0.5f);
        for (int k = 0; k < portrait.Moves.Count && k < 4; k++)
            portrait.DrawMove(new Rect(rx + (k % 2) * (cw + 4f), iy + 74f + (k / 2) * 26f, cw, 22f), portrait.Moves[k]);

        // 固有能力：アイコン・名前・1行・届く所の図・絵のタグ（名前が空なら枠だけ）
        PixelGui.DottedLine(rx, iy + 128f, rw, PixelGui.Muted);
        PixelGui.Text(new Rect(rx, iy + 134f, rw, 14f), "固有能力", 12, PixelGui.LabelPurple);
        var slot = new Rect(rx, iy + 152f, 36f, 36f);
        PixelGui.Fill(slot, PixelGui.SlotBg);
        bool hasSkill = !string.IsNullOrEmpty(s.skill);
        PixelGui.DrawSprite(PixelGui.Inset(slot, 2f), hasSkill ? SkillIconOf(s) : IconOf(s, 16));
        if (!hasSkill) return;
        SkillDefinition def = SkillDefOf(s);
        PixelGui.TextFit(new Rect(ix + 296f, iy + 160f, inner.width - 308f, 18f), s.skill, 16, PixelGui.Ink);
        PixelGui.TextFit(new Rect(rx, iy + 196f, rw, 14f), SkillBriefs.Line(s.skill, def), 12, PixelGui.BodyText);
        PixelGui.ReachMap(rx, iy + 216f, SkillBriefs.Reach(s.skill));
        PixelGui.PictoTags(rx, iy + 240f, SkillBriefs.Pictos(s.skill, def), rw);
    }

    // 前／次のボタン（端では薄くして押せない）
    static bool NavButton(Rect r, string text, bool enabled)
    {
        bool was = GUI.enabled;
        GUI.enabled = was && enabled;
        bool clicked = PixelGui.SecondaryButton(r, text);
        GUI.enabled = was;
        if (!enabled) PixelGui.Fill(new Rect(r.x, r.y, r.width + 2f, r.height + 2f), new Color(PixelGui.Panel.r, PixelGui.Panel.g, PixelGui.Panel.b, 0.65f));
        return clicked && enabled;
    }

    /// <summary>図鑑の能力の名前に合う能力（表に無い能力の1行・タグを、能力の説明から作るため）。名前が違えば null</summary>
    static SkillDefinition SkillDefOf(AppDexEntry e)
    {
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        SkillDefinition skill = abilities != null ? abilities.SkillFor(e.Row) : null;
        if (skill == null) skill = AppVisuals.Get(e.Row)?.Generated; // その場で作った能力
        return skill != null && skill.DisplayName == e.skill ? skill : null;
    }

    /// <summary>アプリのアイコン（正常化していなければグレー）</summary>
    static Sprite IconOf(AppDexEntry e, int size)
    {
        AppVisual v = AppVisuals.Get(e.Row);
        if (v == null) return null;
        Sprite icon = size >= 24 ? v.Icon24 : v.Icon16;
        return e.IsDefeated ? icon : PixelIcon.Grayscale(icon);
    }

    /// <summary>
    /// 手に入る能力のアイコン（アプリ専用の能力 → カテゴリの能力の順にそのアイコン。
    /// その場で作った能力と、アイコンがまだ空のアプリ専用の能力は、アプリのアイコン）
    /// </summary>
    static Sprite SkillIconOf(AppDexEntry e)
    {
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        SkillDefinition skill = abilities != null ? abilities.SkillFor(e.Row) : null;
        if (skill != null && skill.Icon != null) return skill.Icon;
        return AppVisuals.Get(e.Row)?.Icon16;
    }

    static string ShortDate(string date) => date != null && date.Length >= 10 ? $"{int.Parse(date.Substring(5, 2))}/{int.Parse(date.Substring(8, 2))}" : "-";
}
