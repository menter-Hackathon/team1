using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】能力えらびの画面（2026-11-18：デザイン担当の画面イメージに合わせた）。UI 担当の本番画面ができたら消す。
/// 使用時間の上位3アプリのカード（GameSession.Run.Candidates）から、相棒を1つ選んでステージへ進む（2026-11-18 まさぴ〜決定）。
/// 相棒の能力を最初から持つ。ボスは今までどおり、上位10アプリからランダムに3つ（相棒のアプリが出ることもある）。
/// ・カード：順位（1ST〜3RD）・アプリのアイコンと名前・使用時間とメーター・固有能力（能力の名前・説明・タグ。説明とタグは【仮】）
/// ・選んでいるカードはオレンジの縁と「▶ 選択中」。右下の「◯◯ と出撃 ▶」で決定
/// 操作：← → か 1〜3 キー・クリックで選ぶ。Z・Enter・Space で決定。Esc か「◀ もどる」・左上の赤い丸でタイトルへ
/// 2026-11-19：ウィンドウを Mac の形にした（docs/mac-look-design.md 6章の 2）。並び・中身は前のまま
/// 2026-11-19：文字を減らした（docs/ui-text-and-dex-design.md 1-5）。選んでいるカードは「▶」の札だけ、ボタンは「出撃 ▶」。
///   能力は 名前・アイコン 32・届く所の図・1行・絵のタグ（SkillBriefs）で見せる
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
        GameAudio.PlayBgm("bgm_loadout");
        if (Session != null && Session.Run != null) return;
        // 直接再生したとき：サンプルの使用データでランを作る
        var session = GameSession.EnsureExists(_catalog, _abilities);
        if (!session.PrepareRun(new SampleUsageProvider(), out string error)) Debug.LogError($"能力えらびを始められません：{error}", this);
    }

    /// <summary>カードを選ぶ（番号は 0 から）</summary>
    public void Select(int index)
    {
        if (Session == null || Session.IsTransitioning || index < 0 || index >= Cards.Count) return;
        if (index != _selected) GameAudio.Event("ui_card");
        _selected = index;
    }

    /// <summary>決定：選んだカードの能力（相棒）を持って、最初のステージへ</summary>
    public void Confirm()
    {
        if (!CanConfirm) return;
        GameAudio.Event("ui_decide");
        Session.ConfirmLoadout(new List<SkillDefinition> { Cards[_selected].Skill });
    }

    void Back()
    {
        if (Session == null || Session.IsTransitioning) return;
        GameAudio.Event("ui_cancel");
        Session.ReturnToTitle();
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

        // ウィンドウ（2026-11-19：Mac の形。題名はタイトルバーのまん中。閉じるのは左上の赤い丸＝前の右上の ×）
        var win = new Rect(ox + 28f, oy + 14f, 584f, Mathf.Min(292f, barY - oy - 22f));
        Rect inner = PixelGui.Window(win, Session.IsSampleRun ? "Sample Data" : "Usage Scan", PixelGui.LabelPurple);
        if (PixelGui.WindowClose(win)) Back();

        float x = inner.x + 12f, y = inner.y + 8f;
        // 2026-11-19：文字を減らした（docs/ui-text-and-dex-design.md 1-5）。「スキャン完了／訓練モード」と説明の文は消し、見出しは「相棒を選ぶ」だけ
        PixelGui.Text(new Rect(x, y + 14f, inner.width - 24f, 20f), "相棒を選ぶ", 16, PixelGui.Ink);

        // カード（空いた所の分だけ上へ上げて高くした）
        int count = Mathf.Min(Cards.Count, 3);
        float gap = 10f;
        float cardW = Mathf.Floor((inner.width - 24f - gap * (count - 1)) / Mathf.Max(1, count));
        float cardY = y + 38f, cardH = 202f;
        double maxSeconds = 1;
        for (int i = 0; i < count; i++) maxSeconds = System.Math.Max(maxSeconds, Cards[i].Seconds);
        for (int i = 0; i < count; i++)
        {
            var r = new Rect(x + i * (cardW + gap), cardY, cardW, cardH);
            DrawCard(r, i, Cards[i], i == _selected, maxSeconds);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Select(i);
        }

        // 出撃ボタン（ウィンドウの下の縁に重ねる。相棒はカードの縁で分かるので、名前は出さない）
        const float bw = 110f;
        bool was = GUI.enabled;
        GUI.enabled = CanConfirm;
        if (PixelGui.PrimaryButton(new Rect(win.xMax - bw - 20f, win.yMax - 16f, bw, 30f), "出撃 ▶", true)) Confirm();
        GUI.enabled = was;
        PixelGui.End();
    }

    // カードの中の並び（docs/ui-text-and-dex-design.md 1-5 の図。r.y からのずれ）
    void DrawCard(Rect r, int index, AbilityCandidate c, bool selected, double maxSeconds)
    {
        PixelGui.CardBox(r, selected);
        float x = r.x + 10f, w = r.width - 20f;
        Color accent = selected ? PixelGui.Orange : PixelGui.FrameDark;
        PixelGui.TextEn(new Rect(x, r.y + 10f, 60f, 10f), index < Ranks.Length ? Ranks[index] : $"{index + 1}TH", 8, accent);
        if (selected) PixelGui.Chip(new Rect(r.xMax - 10f - 16f, r.y + 8f, 16f, 16f), "▶"); // 選んでいるカード（縁のオレンジでも分かる）

        // アプリ（アイコン 16 ドットを2倍）・名前・使用時間（0.5 時間単位）
        float ay = r.y + 28f;
        if (!c.IsFiller)
        {
            if (c.AppIcon != null) PixelGui.DrawSprite(new Rect(x, ay, 32f, 32f), c.AppIcon);
            PixelGui.TextFit(new Rect(x + 38f, ay, w - 38f, 18f), c.AppName, 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x + 38f, ay + 19f, w - 38f, 14f), $"{System.Math.Round(c.Seconds / 1800.0) / 2.0:0.#}h", 12, PixelGui.SubText);
            PixelGui.Meter(new Rect(x, r.y + 66f, w, 5f), 10, (float)(c.Seconds / maxSeconds), accent, PixelGui.Muted);
        }
        else
        {
            PixelGui.Text(new Rect(x, ay + 6f, w, 18f), "おまけ", 16, PixelGui.SubText);
        }

        // 固有能力：名前（幅いっぱい）・アイコン 32・届く所の図・1行・絵のタグ
        PixelGui.DottedLine(x, r.y + 78f, w, PixelGui.Muted);
        PixelGui.Text(new Rect(x, r.y + 84f, w, 14f), "固有能力", 12, PixelGui.LabelPurple);
        if (c.Skill == null) return;
        string skillName = c.Skill.DisplayName;
        int nameSize = PixelGui.Measure(skillName, PixelGui.Style(false, 16)) <= w ? 16 : 12; // 16 で入らない長い名前（エフェクトアイコン投げ）は 12 で、切らずに出す
        PixelGui.TextFit(new Rect(x, r.y + 100f, w, 18f), skillName, nameSize, PixelGui.Ink);
        var slot = new Rect(x, r.y + 122f, 36f, 36f);
        PixelGui.Fill(slot, PixelGui.SlotBg);
        PixelGui.DrawSprite(PixelGui.Inset(slot, 2f), c.Skill.Icon != null ? c.Skill.Icon : c.AppIcon); // アイコンが空の能力は、アプリのアイコンで
        PixelGui.ReachMap(x + 46f, r.y + 132f, SkillBriefs.Reach(skillName));
        PixelGui.TextFit(new Rect(x, r.y + 162f, w, 14f), SkillBriefs.Line(skillName, c.Skill), 12, PixelGui.BodyText);
        PixelGui.PictoTags(x, r.y + 180f, SkillBriefs.Pictos(skillName, c.Skill), w);
    }

}
