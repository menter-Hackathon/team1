using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ボス正常化後の能力紹介。満杯のときは同じ画面で装備先を選ぶ。</summary>
public sealed class SkillRewardWindow : MonoBehaviour
{
    readonly MacDesktopGui.WindowDrag _drag = new MacDesktopGui.WindowDrag();
    StagePlan _stage;
    SkillDefinition _skill;
    SkillLoadout _loadout;
    PlayerController _player;
    bool _previousControl;
    float _previousTimeScale, _shownAt;
    public bool IsOpen { get; private set; }

    public void Show(StagePlan stage, SkillDefinition skill, SkillLoadout loadout)
    {
        _stage = stage;
        _skill = skill;
        _loadout = loadout;
        _shownAt = Time.unscaledTime;
        _player = FindObjectOfType<PlayerController>();
        if (_player != null) { _previousControl = _player.CanControl; _player.CanControl = false; }
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsOpen = true;
    }

    bool Pending => _loadout != null && _loadout.HasPending && _loadout.Pending == _skill;
    bool Ready => Time.unscaledTime - _shownAt >= 0.35f;

    void Update()
    {
        if (!IsOpen || !Ready) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (Pending)
        {
            if (kb.digit1Key.wasPressedThisFrame) Equip(0);
            else if (kb.digit2Key.wasPressedThisFrame) Equip(1);
            else if (kb.digit3Key.wasPressedThisFrame) Equip(2);
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Close();
    }

    void Equip(int slot)
    {
        if (!Pending || !Ready) return;
        if (_loadout.ReplaceWithPending(slot)) GameAudio.Event("ui_decide");
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = _previousTimeScale;
        if (_player != null) _player.CanControl = _previousControl;
        _drag.Release();
    }

    void OnDisable() => Close();

    void OnGUI()
    {
        if (!IsOpen || _skill == null) return;
        int depth = GUI.depth;
        GUI.depth = -200;
        PixelGui.Begin(false);
        PixelGui.Fill(new Rect(0, 0, PixelGui.Width, PixelGui.Height), new Color(0.01f, 0.02f, 0.04f, 0.65f));
        Rect home = MacDesktopGui.CenteredWindow(PixelGui.Width, PixelGui.Height, 420, Pending ? 280 : 238);
        Rect win = _drag.Apply(home, PixelGui.Width, PixelGui.Height);
        Rect inner = MacDesktopGui.Window(win, "アプリを正常化", out var action);
        float x = inner.x + 8, y = inner.y;
        Sprite icon = _stage != null && _stage.Icon16 != null ? _stage.Icon16 : _skill.Icon;
        PixelGui.DrawSprite(new Rect(x, y + 5, 38, 38), icon);
        PixelGui.TextFit(new Rect(x + 50, y + 5, inner.width - 62, 22), _stage != null ? _stage.DisplayName : "アプリ", 18, MacDesktopGui.Ink);
        PixelGui.Text(new Rect(x + 50, y + 29, inner.width - 62, 16), "スキルを入手", 12, MacDesktopGui.Blue);
        PixelGui.Fill(new Rect(x, y + 56, inner.width - 16, 1), MacDesktopGui.BorderColor);
        PixelGui.TextFit(new Rect(x, y + 68, inner.width - 16, 24), _skill.DisplayName, 20, MacDesktopGui.Ink);
        PixelGui.TextFit(new Rect(x, y + 100, inner.width - 16, 20), SkillBriefs.Line(_skill), 14, MacDesktopGui.Muted);
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && Ready;
        if (Pending)
        {
            PixelGui.Text(new Rect(x, y + 132, inner.width - 16, 18), "入れ替えるスキルを選ぶ", 12, MacDesktopGui.Ink);
            float width = (inner.width - 28) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var current = _loadout.Get(i);
                var r = new Rect(x + i * (width + 6), y + 155, width, 28);
                if (MacDesktopGui.Button(r, (i + 1) + "  " + (current != null ? current.DisplayName : "空き"))) Equip(i);
            }
            if (MacDesktopGui.Button(new Rect(inner.xMax - 152, win.yMax - 38, 144, 26), "今回は装備しない"))
            {
                _loadout.DeclinePending();
                GameAudio.Event("ui_decide");
                Close();
            }
        }
        else
        {
            PixelGui.Text(new Rect(x, y + 132, inner.width - 16, 16), "1・2・3キーでスキル発動", 12, MacDesktopGui.Muted);
            if (MacDesktopGui.Button(new Rect(inner.xMax - 152, win.yMax - 40, 144, 28), "次へ ▶", true)
                || (action == MacDesktopGui.WindowAction.Close && Ready))
            {
                GameAudio.Event("ui_decide");
                Close();
            }
        }
        GUI.enabled = enabled;
        PixelGui.End();
        GUI.depth = depth;
        var e = Event.current;
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel || e.type == EventType.KeyDown) e.Use();
    }
}
