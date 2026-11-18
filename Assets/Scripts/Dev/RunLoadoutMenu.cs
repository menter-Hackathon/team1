using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】能力えらびの画面（2026-11-18：デザイン担当の画面イメージに合わせた）。UI 担当の本番画面ができたら消す。
/// 使用時間の上位3アプリのカード（GameSession.Run.Candidates）から、相棒を1つ選んでステージへ進む（2026-11-18 まさぴ〜決定）。
/// 相棒の能力を最初から持つ。ボスは今までどおり、上位10アプリからランダムに3つ（相棒のアプリが出ることもある）。
/// ・カード：順位（1ST〜3RD）・アプリのアイコンと名前・使用時間とメーター・固有能力（能力の名前・説明・タグ。説明とタグは【仮】）
/// ・選んでいるカードはオレンジの縁と「▶ 選択中」。右下の「◯◯ と出撃 ▶」で決定
/// 操作：← → か 1〜3 キー・クリックで選ぶ。Z・Enter・Space で決定。Esc か「◀ もどる」・× でタイトルへ
/// このシーンを直接再生したときは、サンプルの使用データでランを作る（開発用）。
/// </summary>
public class RunLoadoutMenu : MonoBehaviour
{
    [Tooltip("このシーンを直接再生したときに使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;
    [Tooltip("このシーンを直接再生したときに使う、カテゴリごとの能力の表")]
    [SerializeField] AbilityCatalog _abilities;

    static readonly string[] Ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };

    int _selected; // 選んでいるカード

    GameSession Session => GameSession.Instance;
    IReadOnlyList<AbilityCandidate> Cards => Session != null && Session.Run != null ? Session.Run.Candidates : (IReadOnlyList<AbilityCandidate>)System.Array.Empty<AbilityCandidate>();

    /// <summary>選んでいるカードの番号（0 から）</summary>
    public int Selected => _selected;
    public bool CanConfirm => Session != null && !Session.IsTransitioning && _selected >= 0 && _selected < Cards.Count && Cards[_selected].Skill != null;

    void Start()
    {
        if (Session != null && Session.Run != null) return;
        // 直接再生したとき：サンプルの使用データでランを作る
        var session = GameSession.EnsureExists(_catalog, _abilities);
        if (!session.PrepareRun(new SampleUsageProvider(), out string error)) Debug.LogError($"能力えらびを始められません：{error}", this);
    }

    /// <summary>カードを選ぶ（番号は 0 から）</summary>
    public void Select(int index)
    {
        if (Session == null || Session.IsTransitioning || index < 0 || index >= Cards.Count) return;
        _selected = index;
    }

    /// <summary>決定：選んだカードの能力（相棒）を持って、最初のステージへ</summary>
    public void Confirm()
    {
        if (!CanConfirm) return;
        Session.ConfirmLoadout(new List<SkillDefinition> { Cards[_selected].Skill });
    }

    void Back()
    {
        if (Session != null && !Session.IsTransitioning) Session.ReturnToTitle();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || Cards.Count == 0) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Select(Mathf.Max(0, _selected - 1));
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Select(Mathf.Min(Cards.Count - 1, _selected + 1));
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
        for (int i = 0; i < keys.Length; i++)
        {
            if (kb[keys[i]].wasPressedThisFrame || kb[pad[i]].wasPressedThisFrame) Select(i);
        }
        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Confirm();
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Back();
    }

    void OnGUI()
    {
        if (Session == null || Session.Run == null || Session.IsTransitioning) return;
        PixelGui.Begin();
        float W = PixelGui.Width, H = PixelGui.Height;
        float ox = Mathf.Round((W - 640f) * 0.5f), oy = Mathf.Round(Mathf.Max(0f, (H - 360f) * 0.5f));

        // 下のバーと「◀ もどる」
        float barY = PixelGui.BottomBar(34f);
        if (PixelGui.BevelButton(new Rect(8f, barY + 7f, 70f, 22f), "◀ もどる")) Back();

        // ウィンドウ
        var win = new Rect(ox + 28f, oy + 14f, 584f, Mathf.Min(292f, barY - oy - 22f));
        Rect inner = PixelGui.Window(win);
        if (PixelGui.CloseButton(new Rect(win.xMax - 30f, win.y - 6f, 44f, 44f))) Back();

        float x = inner.x + 12f, y = inner.y + 8f;
        PixelGui.TextEn(new Rect(x, y, 140f, 10f), Session.IsSampleRun ? "SAMPLE_DATA.EXE" : "USAGE_SCAN.EXE", 8, PixelGui.LabelPurple);
        float labelW = PixelGui.Measure(Session.IsSampleRun ? "SAMPLE_DATA.EXE" : "USAGE_SCAN.EXE", PixelGui.Style(true, 8));
        PixelGui.Text(new Rect(x + labelW + 8f, y - 3f, 200f, 16f), Session.IsSampleRun ? "－ 訓練モード" : "－ スキャン完了", 12, PixelGui.LabelPurple);
        PixelGui.Text(new Rect(x, y + 14f, inner.width - 24f, 20f), $"よく使うアプリ TOP{Mathf.Min(3, Cards.Count)} から、相棒を1つ選ぼう", 16, PixelGui.Ink);
        PixelGui.Text(new Rect(x, y + 36f, inner.width - 24f, 16f), "相棒の能力は最初から使える。よく使うアプリのどれか3つが、ボスとして登場する。", 12, PixelGui.SubText);

        // カード
        int count = Mathf.Min(Cards.Count, 3);
        float gap = 10f;
        float cardW = Mathf.Floor((inner.width - 24f - gap * (count - 1)) / Mathf.Max(1, count));
        float cardY = y + 58f, cardH = 182f;
        double maxSeconds = 1;
        for (int i = 0; i < count; i++) maxSeconds = System.Math.Max(maxSeconds, Cards[i].Seconds);
        for (int i = 0; i < count; i++)
        {
            var r = new Rect(x + i * (cardW + gap), cardY, cardW, cardH);
            DrawCard(r, i, Cards[i], i == _selected, maxSeconds);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Select(i);
        }
        PixelGui.Text(new Rect(x, cardY + cardH + 6f, 300f, 16f), "← → で選択　Z で決定", 12, PixelGui.SubText);

        // 出撃ボタン（ウィンドウの下の縁に重ねる）
        string name = CanConfirm ? (Cards[_selected].IsFiller ? Cards[_selected].Skill.DisplayName : Cards[_selected].AppName) : "";
        string label = $"{PixelGui.Fit(name, PixelGui.Style(false, 12), 120f)} と出撃 ▶";
        float bw = Mathf.Max(150f, Mathf.Ceil(PixelGui.Measure(label, PixelGui.Style(false, 12))) + 32f);
        bool was = GUI.enabled;
        GUI.enabled = CanConfirm;
        if (PixelGui.PrimaryButton(new Rect(win.xMax - bw - 20f, win.yMax - 16f, bw, 30f), label, true)) Confirm();
        GUI.enabled = was;
        PixelGui.End();
    }

    void DrawCard(Rect r, int index, AbilityCandidate c, bool selected, double maxSeconds)
    {
        PixelGui.CardBox(r, selected);
        float x = r.x + 10f, w = r.width - 20f;
        float y = r.y + 8f;
        Color accent = selected ? PixelGui.Orange : PixelGui.FrameDark;
        PixelGui.TextEn(new Rect(x, y + 2f, 60f, 10f), index < Ranks.Length ? Ranks[index] : $"{index + 1}TH", 8, accent);
        if (selected) PixelGui.Chip(new Rect(r.xMax - 70f, y, 60f, 16f), "▶ 選択中");

        // アプリ（アイコン 16 ドットを2倍）・名前・使用時間
        y += 20f;
        if (!c.IsFiller)
        {
            if (c.AppIcon != null) PixelGui.DrawSprite(new Rect(x, y, 32f, 32f), c.AppIcon);
            PixelGui.TextFit(new Rect(x + 38f, y, w - 38f, 18f), c.AppName, 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x + 38f, y + 19f, w - 38f, 14f), $"1週間で {c.Seconds / 3600.0:0.#} 時間", 12, PixelGui.SubText);
            PixelGui.Meter(new Rect(x, y + 38f, w, 5f), 10, (float)(c.Seconds / maxSeconds), accent, PixelGui.Muted);
        }
        else
        {
            PixelGui.Text(new Rect(x, y + 6f, w, 18f), "おまけの能力", 16, PixelGui.SubText);
        }

        // 固有能力
        y += 50f;
        PixelGui.DottedLine(x, y, w, PixelGui.Muted);
        y += 6f;
        PixelGui.Text(new Rect(x, y, w, 14f), "固有能力", 12, PixelGui.LabelPurple);
        if (c.Skill != null)
        {
            PixelGui.DrawSprite(new Rect(x, y + 16f, 16f, 16f), c.Skill.Icon);
            PixelGui.TextFit(new Rect(x + 20f, y + 15f, w - 20f, 18f), c.Skill.DisplayName, 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x, y + 36f, w, 30f), SkillTexts.Description(c.Skill), 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);
            float tx = x;
            foreach (string tag in SkillTexts.Tags(c.Skill)) tx += PixelGui.OutlineTag(tx, r.yMax - 26f, tag) + 4f;
        }
    }

}
