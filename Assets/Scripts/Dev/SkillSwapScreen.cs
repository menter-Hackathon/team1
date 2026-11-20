using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】入れ替えの画面（2026-11-20：docs/window-stage-and-pickup-design.md 2-3。担当 C）。
/// ボスが落としたスキル（RewardDrop）を拾った瞬間に開く。ゲームは止まる（Time.timeScale = 0）。
/// ・上：新しいスキル（アイコン・名前・1行・届く所の図・絵のタグ・長い説明 2行まで）
/// ・下：今の 3枠と「いれない」。空いた枠に入れる・持っているスキルと入れ替える・入れない、をえらぶ
/// ・もう持っているスキル：カードは見せるが、えらべない。「OK」だけ
/// 決めたら 0.3秒 の動き → 呼んだ側の onClosed（StageDirector が CompleteStage → 暗転 → デスクトップ）。
/// 入力は Keyboard.current・Gamepad.current・マウスを直接読む（PlayerInputReader はさわらない）。時間はすべて unscaledTime。
/// 中で例外が出ても止まらない：空いた枠に入れる（無ければ入れない）だけにして、onClosed を呼ぶ。数値はすべて【仮】
/// </summary>
[DefaultExecutionOrder(-1000)] // マウスのクリックを、後ろのギミック（盾・タブ）より先に取る
public class SkillSwapScreen : MonoBehaviour
{
    /// <summary>えらぶ所の番号：0〜2＝枠、3＝いれない</summary>
    public const int SkipIndex = 3;

    const float InputGuard = 0.35f;   // 開いてすぐは受けつけない（攻撃ボタンの連打で決まらないように）
    const float CloseTime = 0.3f;     // 決めてから閉じるまでの動き
    const float WinW = 480f, WinH = 264f;
    const float CardW = 100f, CardH = 86f, CardGap = 6f, SkipW = 56f;

    static readonly Color Dim = new Color(0.05f, 0.07f, 0.12f, 0.45f);
    static readonly Color EmptyFill = new Color32(242, 244, 248, 255);

    /// <summary>開いている間だけ</summary>
    public static SkillSwapScreen Current { get; private set; }

    SkillDefinition _reward;
    SkillLoadout _loadout;
    PlayerController _controller;
    Sprite _appIcon;
    Action _onClosed;
    bool _owned;
    int _ownedSlot = -1;
    int _selected;
    float _openedAt;
    float _closingAt = -1f;
    int _choice = -1;          // 決めた所（閉じる動きで使う）
    bool _finished;
    bool _stickArmed;
    bool _logged;
    string _descFit;
    float _descFitWidth = -1f;

    /// <summary>今えらんでいる所（0〜2＝枠、3＝いれない）</summary>
    public int Selected => _selected;
    public SkillDefinition Reward => _reward;
    /// <summary>もう持っているスキル（「OK」だけ）</summary>
    public bool IsOwned => _owned;
    /// <summary>決めて、閉じる動きの途中</summary>
    public bool IsClosing => _closingAt >= 0f;

    bool Interactive => !_finished && _closingAt < 0f && Time.unscaledTime - _openedAt >= InputGuard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    /// <summary>
    /// 画面を開く。reward か skills が無ければ開かずに onClosed を呼んで null を返す。
    /// onClosed は、決めて閉じたとき（または中で失敗したとき）に 1回だけ呼ぶ。Abort では呼ばない
    /// </summary>
    public static SkillSwapScreen Open(SkillDefinition reward, PlayerSkills skills, Sprite appIcon, Action onClosed)
    {
        if (reward == null || skills == null || skills.Loadout == null)
        {
            if (skills != null) ApplyFallback(skills.Loadout, reward);
            onClosed?.Invoke();
            return null;
        }
        if (Current != null) Current.Abort();
        var go = new GameObject("SkillSwapScreen");
        var screen = go.AddComponent<SkillSwapScreen>();
        try { screen.Init(reward, skills, appIcon, onClosed); }
        catch
        {
            screen.Abort(); // 途中で失敗：時間を戻して消す（呼んだ側が代わりの手で進む）
            throw;
        }
        return screen;
    }

    /// <summary>
    /// 画面が使えないときの代わり：空いたいちばん前の枠に入れる（無い・もう持っているなら入れない）。
    /// RewardDrop・StageDirector も、失敗したときにこれを使う
    /// </summary>
    public static void ApplyFallback(SkillLoadout loadout, SkillDefinition reward)
    {
        try
        {
            if (loadout == null || reward == null || loadout.Contains(reward)) return;
            SkillDefinition[] slots = loadout.Snapshot();
            int empty = Array.IndexOf(slots, null);
            if (empty < 0) return;
            slots[empty] = reward;
            loadout.Restore(slots);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    void Init(SkillDefinition reward, PlayerSkills skills, Sprite appIcon, Action onClosed)
    {
        _reward = reward;
        _loadout = skills.Loadout;
        _appIcon = appIcon;
        _onClosed = onClosed;
        _ownedSlot = _loadout.IndexOf(reward);
        _owned = _ownedSlot >= 0;
        _selected = _owned ? SkipIndex : FirstEmptyOr0();
        _openedAt = Time.unscaledTime;
        _stickArmed = false; // 開いた瞬間にスティックを倒していても、一度もどすまで動かさない
        Current = this;

        _controller = skills.GetComponent<PlayerController>();
        if (_controller == null) _controller = skills.GetComponentInParent<PlayerController>();
        if (_controller != null) _controller.CanControl = false; // X 長押しの「捨てる」も止まる（PlayerSkills.CanOperate）
        Time.timeScale = 0f;
        MousePointer.ShowCursor();
    }

    int SlotCount => _loadout != null ? Mathf.Min(SkipIndex, _loadout.Capacity) : 0;

    int FirstEmptyOr0()
    {
        for (int i = 0; i < SlotCount; i++)
            if (_loadout.Get(i) == null) return i;
        return 0;
    }

    // ───────── 確認用の口 ─────────

    /// <summary>えらぶ所を変える（0〜2＝枠、3＝いれない）。もう持っているスキル・閉じる途中は何もしない</summary>
    public void Select(int index) => SetSelected(index, "ui_move");

    /// <summary>決める（0.35秒 の待ちは無視）</summary>
    public void Confirm()
    {
        if (_finished || _closingAt >= 0f) return;
        try { Decide(); }
        catch (Exception e) { Fail(e); }
    }

    /// <summary>何もせずに閉じる（onClosed は呼ばない。ライフ 0 などで落ちたスキルを消すとき）。止めた時間は戻す</summary>
    public void Abort()
    {
        if (_finished) return;
        _finished = true;
        _onClosed = null;
        if (Current == this) Current = null;
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    // ───────── えらぶ・決める ─────────

    void SetSelected(int index, string sound)
    {
        if (_finished || _closingAt >= 0f || _owned) return;
        index = Mathf.Clamp(index, 0, SkipIndex);
        if (index < SkipIndex && index >= SlotCount) return;
        if (index == _selected) return;
        _selected = index;
        GameAudio.Event(sound);
    }

    void Decide()
    {
        if (_finished || _closingAt >= 0f) return;
        _choice = _owned ? SkipIndex : _selected;
        if (_choice < SkipIndex)
        {
            // 枠 i に入れる（空いていても入っていても同じ。SkillLoadout に「i 番へ入れる」は無いので、写して戻す）。
            // 変わった枠だけ SlotChanged（Reason＝Loaded）が出て、HUD・パッシブはこれで直る
            SkillDefinition[] slots = _loadout.Snapshot();
            if (_choice < slots.Length)
            {
                slots[_choice] = _reward;
                _loadout.Restore(slots);
            }
        }
        GameAudio.Event("ui_decide");
        _closingAt = Time.unscaledTime;
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        if (Current == this) Current = null;
        // PlayerController.CanControl は戻さない（このあと暗転する）。時間は GameSession.Transition も 1 に戻すが、
        // 次へ進めなかったときに止まったままにならないよう、先に戻しておく
        Time.timeScale = 1f;
        Action closed = _onClosed;
        _onClosed = null;
        try { closed?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        Destroy(gameObject);
    }

    // 中で失敗した：空いた枠に入れる（無ければ入れない）だけにして、先へ進む（ランを止めない）
    void Fail(Exception e)
    {
        if (!_logged)
        {
            _logged = true;
            Debug.LogError($"入れ替えの画面で例外。空いた枠に入れて先へ進みます：{e}");
        }
        if (_finished) return;
        if (_closingAt < 0f && !_owned) ApplyFallback(_loadout, _reward);
        Finish();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (!_finished)
        {
            // 閉じずに消えた（シーンの切り替えなど）：止めた時間だけは戻す
            _finished = true;
            Time.timeScale = 1f;
        }
    }

    // ───────── 入力 ─────────

    void Update()
    {
        if (_finished) return;
        try
        {
            if (_controller != null && _controller.CanControl) _controller.CanControl = false; // ほかの所が戻しても切っておく
            MousePointer.Consume(); // 画面の後ろの盾・タブにクリックを渡さない（自分のクリックは OnGUI で取るので関係ない）
            if (_closingAt >= 0f)
            {
                if (Time.unscaledTime - _closingAt >= CloseTime) Finish();
                return;
            }
            if (!Interactive) return;
            ReadInput();
        }
        catch (Exception e) { Fail(e); }
    }

    void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        int step = 0;
        bool confirm = false, cancel = false;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetSelected(0, "ui_move");
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetSelected(1, "ui_move");
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetSelected(2, "ui_move");
            confirm |= kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            cancel |= kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
        }
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) step = -1;
            if (pad.dpad.right.wasPressedThisFrame) step = 1;
            float sx = pad.leftStick.ReadValue().x;
            if (_stickArmed && Mathf.Abs(sx) > 0.6f) { step = sx < 0f ? -1 : 1; _stickArmed = false; }
            else if (Mathf.Abs(sx) < 0.3f) _stickArmed = true;
            confirm |= pad.buttonSouth.wasPressedThisFrame;
            cancel |= pad.buttonEast.wasPressedThisFrame;
        }
        else _stickArmed = true;

        if (step != 0) SetSelected(Mathf.Clamp(_selected + step, 0, SkipIndex), "ui_move");
        if (cancel)
        {
            if (_owned) { Decide(); return; } // 「OK」だけの画面は、どのボタンでも閉じる
            // 「いれない」をえらぶだけ（すぐには閉じない。もう一度 決めるボタンで閉じる）
            if (_selected != SkipIndex) { _selected = SkipIndex; GameAudio.Event("ui_cancel"); }
        }
        if (confirm) Decide();
    }

    void OnCardClicked(int index)
    {
        if (!Interactive || _owned) return;
        if (index == _selected) Decide();
        else SetSelected(index, "ui_move");
    }

    // ───────── 描く ─────────

    void OnGUI()
    {
        if (_finished) return;
        bool begun = false;
        try
        {
            GUI.depth = -400; // HUD より手前、デスクトップ（-500）・暗転（-1000）より奥
            PixelGui.Begin(false);
            begun = true;
            Draw();
        }
        catch (ExitGUIException) { throw; }
        catch (Exception e) { Fail(e); }
        finally
        {
            if (begun) PixelGui.End();
        }
    }

    void Draw()
    {
        float W = PixelGui.Width, H = PixelGui.Height;
        PixelGui.Fill(new Rect(0f, 0f, W, H), Dim);
        Rect win = MacDesktopGui.CenteredWindow(W, H, WinW, WinH, false);
        Rect inner = MacDesktopGui.Window(win, _owned ? "もっているスキル" : "あたらしいスキル", out _); // 信号機は押しても何もしない

        float closeT = _closingAt >= 0f ? Mathf.Clamp01((Time.unscaledTime - _closingAt) / CloseTime) : 0f;
        float x = inner.x, y = inner.y, w = inner.width;
        Sprite newIcon = IconOf(_reward, _appIcon);

        // ── 上：新しいスキル ──
        var plate = new Rect(x, y, 56f, 56f);
        MacDesktopGui.Card(plate, false);
        var bigIcon = new Rect(x + 4f, y + 4f, 48f, 48f);
        if (_closingAt < 0f) PixelGui.DrawSprite(bigIcon, newIcon);
        if (_appIcon != null && _reward.Icon != null) Badge(new Rect(plate.xMax - 14f, plate.yMax - 14f, 18f, 18f), _appIcon);

        float tx = x + 66f, tw = w - 66f;
        PixelGui.TextFit(new Rect(tx, y - 1f, tw, 18f), _reward.DisplayName, 16, MacDesktopGui.Ink);
        string line = SkillBriefs.Line(_reward);
        if (!string.IsNullOrEmpty(line)) PixelGui.TextFit(new Rect(tx, y + 19f, tw, 14f), line, 12, MacDesktopGui.Muted);
        SkillReach reach = SkillBriefs.Reach(_reward.DisplayName);
        float px = tx;
        if (reach != SkillReach.None)
        {
            PixelGui.ReachMap(tx, y + 37f, reach);
            px = tx + 64f;
        }
        PixelGui.PictoTags(px, y + 38f, SkillBriefs.Pictos(_reward), tx + tw - px);
        string desc = DescriptionFit(tw, line);
        if (!string.IsNullOrEmpty(desc))
            PixelGui.Text(new Rect(tx, y + 58f, tw, 28f), desc, 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);

        // ── 下：3枠と「いれない」 ──
        float sepY = y + 92f;
        PixelGui.Fill(new Rect(x, sepY, w, 1f), MacDesktopGui.BorderColor);
        float cy = sepY + 8f;
        Rect[] iconRects = new Rect[SkipIndex];
        for (int i = 0; i < SkipIndex; i++)
        {
            var r = new Rect(x + i * (CardW + CardGap), cy, CardW, CardH);
            iconRects[i] = new Rect(Mathf.Round(r.center.x - 16f), r.y + 12f, 32f, 32f);
            DrawSlot(i, r, iconRects[i], newIcon);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) OnCardClicked(i);
        }
        var skip = new Rect(x + SkipIndex * (CardW + CardGap), cy, SkipW, CardH);
        if (!_owned)
        {
            DrawSkip(skip, closeT);
            if (GUI.Button(skip, GUIContent.none, GUIStyle.none)) OnCardClicked(SkipIndex);
        }

        var button = new Rect(inner.xMax - 68f, cy + CardH - 24f, 68f, 24f);
        if (MacDesktopGui.Button(button, _owned ? "OK" : "決定", true, true) && Interactive) Decide();

        // いちばん下の 1行：えらんでいる枠にスキルがあれば、比べるために「いま：名前 — 1行」
        SkillDefinition current = !_owned && _selected < SlotCount ? _loadout.Get(_selected) : null;
        if (current != null && _closingAt < 0f)
        {
            string brief = SkillBriefs.Line(current);
            string text = string.IsNullOrEmpty(brief) ? $"いま：{current.DisplayName}" : $"いま：{current.DisplayName} — {brief}";
            PixelGui.TextFit(new Rect(x, cy + CardH + 4f, w - 76f, 14f), text, 12, MacDesktopGui.Muted);
        }

        // ── 閉じる動き：新しいアイコンが、えらんだ枠へ入る（いれない：うすくなって消える） ──
        if (_closingAt >= 0f && newIcon != null)
        {
            float e = 1f - (1f - closeT) * (1f - closeT);
            if (_choice >= 0 && _choice < SkipIndex)
            {
                Rect to = iconRects[_choice];
                var r = new Rect(Mathf.Lerp(bigIcon.x, to.x, e), Mathf.Lerp(bigIcon.y, to.y, e),
                    Mathf.Lerp(bigIcon.width, to.width, e), Mathf.Lerp(bigIcon.height, to.height, e));
                PixelGui.DrawSprite(r, newIcon);
            }
            else PixelGui.DrawSprite(bigIcon, newIcon, new Color(1f, 1f, 1f, 1f - closeT));
        }
    }

    void DrawSlot(int i, Rect r, Rect icon, Sprite newIcon)
    {
        SkillDefinition s = i < SlotCount ? _loadout.Get(i) : null;
        bool closing = _closingAt >= 0f;
        bool sel = !_owned && (closing ? _choice == i : _selected == i);
        if (s == null)
        {
            if (sel) MacDesktopGui.Card(r, true);
            else
            {
                MacDesktopGui.Rounded(r, EmptyFill, 5);
                PixelGui.DashedBorder(PixelGui.Inset(r, 2f), MacDesktopGui.BorderColor);
            }
            if (!sel) Plus(icon.center, MacDesktopGui.Muted);
            else if (!closing) PixelGui.DrawSprite(icon, newIcon, new Color(1f, 1f, 1f, 0.35f)); // 「これが入る」
        }
        else
        {
            MacDesktopGui.Card(r, sel);
            float alpha = !sel ? 1f : closing ? 0f : 0.5f;
            if (alpha > 0f) PixelGui.DrawSprite(icon, IconOf(s, null), new Color(1f, 1f, 1f, alpha));
            PixelGui.TextFit(new Rect(r.x + 4f, r.y + 47f, r.width - 8f, 14f), s.DisplayName, 12, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
            PixelGui.PictoTags(r.x + 5f, r.y + 65f, SkillBriefs.Pictos(s), r.width - 10f);
            if (_owned && i == _ownedSlot) Check(new Rect(r.xMax - 20f, r.y + 5f, 15f, 15f));
        }
        PixelGui.TextEn(new Rect(r.x + 6f, r.y + 6f, 12f, 9f), (i + 1).ToString(), 8, sel ? MacDesktopGui.Blue : MacDesktopGui.Muted, TextAnchor.UpperLeft);
        if (sel && !closing && newIcon != null)
        {
            // 右上に新しいスキルのアイコン（20）を重ねる
            var o = new Rect(r.xMax - 27f, r.y + 4f, 22f, 22f);
            MacDesktopGui.Rounded(o, MacDesktopGui.Blue, 4);
            MacDesktopGui.Rounded(PixelGui.Inset(o, 1f), Color.white, 3);
            PixelGui.DrawSprite(PixelGui.Inset(o, 1f), newIcon);
        }
    }

    void DrawSkip(Rect r, float closeT)
    {
        bool sel = _closingAt >= 0f ? _choice == SkipIndex : _selected == SkipIndex;
        MacDesktopGui.Card(r, sel);
        Color c = sel ? MacDesktopGui.Blue : MacDesktopGui.Muted;
        Cross(new Vector2(Mathf.Round(r.center.x), r.y + 28f), c);
        PixelGui.Text(new Rect(r.x, r.y + 50f, r.width, 14f), "いれない", 12, sel ? MacDesktopGui.Blue : MacDesktopGui.Ink, TextAnchor.MiddleCenter);
    }

    // ───────── 小さい部品 ─────────

    static Sprite IconOf(SkillDefinition skill, Sprite fallback)
    {
        if (skill == null) return fallback;
        if (skill.Icon != null) return skill.Icon;
        if (fallback != null) return fallback;
        RunState run = GameSession.Instance != null ? GameSession.Instance.Run : null;
        return run != null ? run.SourceIcon(skill) : null;
    }

    // 元のアプリの小さいアイコン（白い札。HUD の枠と同じ）
    static void Badge(Rect r, Sprite icon)
    {
        MacDesktopGui.Rounded(r, MacDesktopGui.BorderColor, 3);
        MacDesktopGui.Rounded(PixelGui.Inset(r, 1f), Color.white, 2);
        PixelGui.DrawSprite(PixelGui.Inset(r, 1f), icon);
    }

    static void Plus(Vector2 c, Color color)
    {
        c = new Vector2(Mathf.Round(c.x), Mathf.Round(c.y));
        PixelGui.Fill(new Rect(c.x - 6f, c.y - 1f, 12f, 2f), color);
        PixelGui.Fill(new Rect(c.x - 1f, c.y - 6f, 2f, 12f), color);
    }

    static void Cross(Vector2 c, Color color)
    {
        for (int d = -5; d <= 4; d++)
        {
            PixelGui.Fill(new Rect(c.x + d, c.y + d, 2f, 2f), color);
            PixelGui.Fill(new Rect(c.x + d, c.y - d - 1f, 2f, 2f), color);
        }
    }

    // ✓ の印（もう持っている枠）
    static void Check(Rect r)
    {
        MacDesktopGui.Rounded(r, MacDesktopGui.Blue, 7);
        int[,] pts = { { 3, 7 }, { 4, 8 }, { 5, 9 }, { 6, 8 }, { 7, 7 }, { 8, 6 }, { 9, 5 }, { 10, 4 } };
        for (int i = 0; i < pts.GetLength(0); i++)
            PixelGui.Fill(new Rect(r.x + pts[i, 0], r.y + pts[i, 1], 2f, 2f), Color.white);
    }

    // 長い説明を 2行（高さ 28）に収める。1行の説明と同じなら出さない
    string DescriptionFit(float width, string line)
    {
        if (Mathf.Approximately(width, _descFitWidth)) return _descFit;
        _descFitWidth = width;
        _descFit = null;
        string desc = SkillTexts.Description(_reward);
        if (string.IsNullOrEmpty(desc)) return null;
        if (!string.IsNullOrEmpty(line))
        {
            string head = line.TrimEnd('。');
            if (desc.TrimEnd('。') == head) return null;
            // 1行の説明が長い説明の最初の文なら、そのあとだけ（同じ文を 2回 出さない）
            if (head.Length > 0 && desc.StartsWith(head, StringComparison.Ordinal))
            {
                desc = desc.Substring(head.Length).TrimStart('。', ' ', '　');
                if (desc.Length == 0) return null;
            }
        }
        GUIStyle style = PixelGui.Style(false, 12, TextAnchor.UpperLeft, true);
        if (style.font == null) { _descFitWidth = -1f; return desc; } // フォントがまだ無い：次に測り直す
        const float maxH = 30f;
        if (style.CalcHeight(new GUIContent(desc), width) <= maxH) return _descFit = desc;
        for (int n = desc.Length - 1; n > 0; n--)
        {
            string t = desc.Substring(0, n) + "…";
            if (style.CalcHeight(new GUIContent(t), width) <= maxH) return _descFit = t;
        }
        return _descFit = "…";
    }
}
