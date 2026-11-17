using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】能力えらびの画面（2026-11-17：ゲームの流れ）。UI 担当の本番画面ができたら消す。
/// 使用時間の長いアプリから作った5枚のカード（GameSession.Run.Candidates）から、3つ選んでステージへ進む。
/// ・カード：アプリの色の帯・能力のアイコン・能力の名前・元のアプリと使用時間
/// ・下の3つの枠：選んだ順に入る（ステージでの 1・2・3 の枠になる）。3つそろうと ▶ が押せる
/// 操作：カードをクリック、または 1〜5 キーで選ぶ／外す。Enter・Space で決定
/// このシーンを直接再生したときは、サンプルの使用データでランを作る（開発用）。
/// </summary>
public class RunLoadoutMenu : MonoBehaviour
{
    [Tooltip("このシーンを直接再生したときに使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;
    [Tooltip("このシーンを直接再生したときに使う、カテゴリごとの能力の表")]
    [SerializeField] AbilityCatalog _abilities;

    readonly List<int> _picked = new List<int>(); // 選んだカードの番号（選んだ順）
    Texture2D _white;
    GUIStyle _name, _app, _rank, _order;

    GameSession Session => GameSession.Instance;
    IReadOnlyList<AbilityCandidate> Cards => Session != null && Session.Run != null ? Session.Run.Candidates : (IReadOnlyList<AbilityCandidate>)System.Array.Empty<AbilityCandidate>();
    int PickCount => Session != null && Session.Abilities != null ? Session.Abilities.PickCount : 3;

    /// <summary>選んだカードの番号（選んだ順）</summary>
    public IReadOnlyList<int> Picked => _picked;
    public bool CanConfirm => _picked.Count == PickCount && Session != null && !Session.IsTransitioning;

    void Start()
    {
        if (Session != null && Session.Run != null) return;
        // 直接再生したとき：サンプルの使用データでランを作る
        var session = GameSession.EnsureExists(_catalog, _abilities);
        if (!session.PrepareRun(new SampleUsageProvider(), out string error)) Debug.LogError($"能力えらびを始められません：{error}", this);
    }

    /// <summary>カードを選ぶ／外す（番号は 0 から）</summary>
    public void Toggle(int index)
    {
        if (Session == null || Session.IsTransitioning || index < 0 || index >= Cards.Count) return;
        if (_picked.Remove(index)) return;
        if (_picked.Count < PickCount) _picked.Add(index);
    }

    /// <summary>決定：選んだ能力を持って、最初のステージへ</summary>
    public void Confirm()
    {
        if (!CanConfirm) return;
        var skills = new List<SkillDefinition>();
        foreach (int i in _picked) skills.Add(Cards[i].Skill);
        Session.ConfirmLoadout(skills);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5 };
        for (int i = 0; i < keys.Length; i++)
        {
            if (kb[keys[i]].wasPressedThisFrame || kb[pad[i]].wasPressedThisFrame) Toggle(i);
        }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Confirm();
    }

    void OnGUI()
    {
        if (Session == null || Session.Run == null) return;
        PixelGui.Begin();
        EnsureStyles();
        GameArt art = GameArt.Instance;

        int count = Cards.Count;
        float gap = 8f;
        float cardW = Mathf.Min(104f, (PixelGui.Width - 24f - gap * (count - 1)) / Mathf.Max(1, count));
        float cardH = 150f;
        float totalW = cardW * count + gap * (count - 1);
        float slotsH = 64f;
        float top = Mathf.Max(8f, (PixelGui.Height - cardH - 24f - slotsH) * 0.5f);
        float left = (PixelGui.Width - totalW) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            int order = _picked.IndexOf(i);
            float lift = order >= 0 ? -8f : 0f; // 選んだカードは少し持ち上げる
            var r = new Rect(left + i * (cardW + gap), top + 8f + lift, cardW, cardH);
            DrawCard(r, i, Cards[i], order, art);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Toggle(i);
        }

        // 下の枠：選んだ順に能力が入る。3つそろうと ▶ が押せる
        int pick = PickCount;
        float slot = 40f;
        float buttonW = 64f;
        float rowW = pick * slot + (pick - 1) * 6f + 14f + buttonW;
        float sx = (PixelGui.Width - rowW) * 0.5f;
        float sy = top + cardH + 28f;
        for (int k = 0; k < pick; k++)
        {
            var sr = new Rect(sx + k * (slot + 6f), sy, slot, slot);
            bool filled = k < _picked.Count;
            Sprite frame = art != null ? (filled ? art.SlotSelected : art.Slot) : null;
            if (frame != null) PixelGui.DrawSprite(sr, frame);
            else Fill(sr, new Color(0f, 0f, 0f, 0.35f));
            if (filled)
            {
                AbilityCandidate c = Cards[_picked[k]];
                Color theme = c.Skill != null && c.Skill.Theme != null ? c.Skill.Theme.Main : Color.white;
                Fill(new Rect(sr.x + 3f, sr.yMax - 6f, sr.width - 6f, 3f), theme);
                if (c.Skill != null) PixelGui.DrawSprite(new Rect(sr.x + 4f, sr.y + 4f, 32f, 32f), c.Skill.Icon);
            }
        }
        bool wasEnabled = GUI.enabled;
        GUI.enabled = CanConfirm;
        if (GUI.Button(new Rect(sx + rowW - buttonW, sy + 4f, buttonW, slot - 8f), "▶", PixelGui.Button)) Confirm();
        GUI.enabled = wasEnabled;

        PixelGui.End();
    }

    void DrawCard(Rect r, int index, AbilityCandidate c, int order, GameArt art)
    {
        Color theme = c.Skill != null && c.Skill.Theme != null ? c.Skill.Theme.Main : new Color(0.5f, 0.5f, 0.5f);
        bool selected = order >= 0;
        if (selected) Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), theme); // 選んだカードはアプリの色で縁どる
        GUI.Box(r, GUIContent.none, PixelGui.Panel);

        // アプリの色の帯（ウィンドウのタイトルバー）と、キーの番号
        var bar = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, 14f);
        Fill(bar, theme);
        GUI.Label(new Rect(bar.x + 3f, bar.y - 1f, 20f, 16f), (index + 1).ToString(), _rank);

        // 能力のアイコン（16 ドットを3倍）
        if (c.Skill != null) PixelGui.DrawSprite(new Rect(r.center.x - 24f, r.y + 28f, 48f, 48f), c.Skill.Icon);

        // 能力の名前・元のアプリと使用時間（埋めたカードはアプリなし）
        GUI.Label(new Rect(r.x + 2f, r.y + 82f, r.width - 4f, 20f), c.Skill != null ? c.Skill.DisplayName : "-", _name);
        if (!c.IsFiller)
        {
            GUI.Label(new Rect(r.x + 2f, r.y + 104f, r.width - 4f, 16f), c.AppName, _app);
            GUI.Label(new Rect(r.x + 2f, r.y + 120f, r.width - 4f, 16f), $"{c.Seconds / 3600.0:0.#} h", _app);
        }

        // 選んだ順の番号（1・2・3 の枠になる）
        if (selected)
        {
            var badge = new Rect(r.xMax - 20f, r.y - 8f, 22f, 22f);
            Fill(badge, theme);
            Fill(new Rect(badge.x + 2f, badge.y + 2f, badge.width - 4f, badge.height - 4f), Color.white);
            GUI.Label(badge, (order + 1).ToString(), _order);
        }
    }

    void EnsureStyles()
    {
        if (_name != null) return;
        var dark = new Color32(0x22, 0x22, 0x22, 0xff);
        // 「ブレークポイント」（全角8文字）がカードの幅に収まる大きさ
        _name = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
        _name.normal.textColor = dark;
        _app = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, clipping = TextClipping.Clip };
        _app.normal.textColor = new Color32(0x51, 0x51, 0x51, 0xff);
        _rank = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        _rank.normal.textColor = Color.white;
        _order = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        _order.normal.textColor = dark;
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
}
