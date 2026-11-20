using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 一時停止のメニュー（2026-11-20）。ゲーム中（プレイヤーがいるシーン）に Esc（コントローラーは START）で開く。自分で立ち上がる（シーンに置かない）。
/// ・ゲームの時間を止め（Time.timeScale = 0）、プレイヤーの入力を止め、鳴っている技の音を止める（BGM は流したまま）
/// ・ゲームにもどる／音量（全体・BGM・効果音。PlayerPrefs に残る）／キー設定（KeyBindings）／タイトルにもどる（確認つき。ランの中だけ）
/// ・操作：↑↓ 選ぶ、←→ 音量を変える、Enter・Z・Space・A で決める、Esc・Backspace・B でもどる（最初のページなら閉じる）、START で閉じる。マウスでも押せる
/// 見た目はタイトル・能力えらびと同じ Mac 風のウィンドウ（MacDesktopGui）
/// ・タイトルの歯車から OpenSettings で「設定」として開ける（ゲームは止めない。音量・キー設定・閉じる）。2026-11-20
/// </summary>
public class PauseMenu : MonoBehaviour
{
    enum Page { Main, Volume, Keys, ConfirmTitle }

    static PauseMenu _i;
    /// <summary>一時停止している間 true（設定として開いているときは false）</summary>
    public static bool IsPaused => _i != null && _i._paused && !_i._settingsOnly;
    /// <summary>メニューが開いている間 true（一時停止・設定のどちらでも）</summary>
    public static bool IsOpen => _i != null && _i._paused;
    /// <summary>ほかの画面がキー入力を受け付けないとき true（開いている間と、閉じたのと同じフレーム）</summary>
    public static bool BlocksInput => _i != null && (_i._paused || _i._closedFrame == Time.frameCount);

    /// <summary>設定として開く（タイトルの歯車。ゲームの時間・入力・音は止めない）</summary>
    public static void OpenSettings()
    {
        Boot();
        if (_i == null || _i._paused) return;
        _i._paused = true;
        _i._settingsOnly = true;
        _i._page = Page.Main;
        _i._focus = 0;
        _i._capturing = null;
        _i._openedFrame = Time.frameCount;
    }

    bool _paused; // メニューが開いている（一時停止・設定のどちらでも）
    bool _settingsOnly; // 設定として開いている（ゲームは止めていない）
    int _closedFrame = -1;
    Page _page;
    int _focus;
    float _prevScale = 1f;
    PlayerInputReader _reader;
    bool _readerWasEnabled;
    int _openedFrame;
    KeyBindings.Entry _capturing; // キーを待っている操作（無ければ null）
    int _captureFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _i = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_i != null) return;
        var go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        _i = go.AddComponent<PauseMenu>();
    }

    bool InRun => GameSession.Instance != null && GameSession.Instance.Run != null;

    // ───────── 開く・閉じる ─────────

    bool CanPause(out PlayerInputReader reader)
    {
        reader = null;
        if (GameSession.Instance != null && GameSession.Instance.IsTransitioning) return false;
        if (Time.timeScale <= 0f && !HitStop.IsStopping) return false; // ゲームオーバー・デスクトップの画面など、ほかの所が止めている
        reader = FindObjectOfType<PlayerInputReader>();
        return reader != null && reader.isActiveAndEnabled;
    }

    void Open(PlayerInputReader reader)
    {
        _paused = true;
        _settingsOnly = false;
        _reader = reader;
        _readerWasEnabled = reader.enabled;
        reader.enabled = false; // ジャンプ・攻撃などを受け付けない
        _prevScale = HitStop.IsStopping ? 1f : Time.timeScale; // ヒットストップの途中なら、戻すときはふつうの速さ
        Time.timeScale = 0f;
        GameAudio.SetGamePaused(true);
        GameAudio.Event("ui_decide");
        _page = Page.Main;
        _focus = 0;
        _capturing = null;
        _openedFrame = Time.frameCount;
    }

    void Close(bool sound = true)
    {
        if (!_paused) return;
        _paused = false;
        _capturing = null;
        _closedFrame = Time.frameCount;
        if (_settingsOnly)
        {
            _settingsOnly = false;
            if (sound) GameAudio.Event("ui_cancel");
            return;
        }
        if (Time.timeScale <= 0f) Time.timeScale = _prevScale > 0f ? _prevScale : 1f;
        if (_reader != null) _reader.enabled = _readerWasEnabled; // 有効になるときに、変えたキーが反映される
        _reader = null;
        GameAudio.SetGamePaused(false);
        if (sound) GameAudio.Event("ui_cancel");
    }

    void OnDestroy()
    {
        if (_paused) Close(false);
        if (_i == this) _i = null;
    }

    // ───────── 入力 ─────────

    void Update()
    {
        if (!_paused)
        {
            if (MenuInput.Pause && CanPause(out PlayerInputReader reader)) Open(reader);
            return;
        }
        if (!_settingsOnly && _reader == null) { Close(false); return; } // シーンが変わった
        if (Time.frameCount == _openedFrame) return;   // 開いたのと同じ押しで閉じない
        if (_capturing != null) { Capture(); return; }

        bool back = MenuInput.Pause || MenuInput.Back; // Esc は両方に入っている
        if (back)
        {
            if (_page == Page.Main) Close();
            else GoBack();
            return;
        }

        int count = RowCount;
        int before = _focus;
        if (MenuInput.Up) _focus = (_focus + count - 1) % count;
        if (MenuInput.Down) _focus = (_focus + 1) % count;
        if (_page == Page.ConfirmTitle)
        {
            if (MenuInput.Left) _focus = 0;
            if (MenuInput.Right) _focus = 1;
        }
        if (_focus != before) GameAudio.Event("ui_move");

        if (_page == Page.Volume && _focus < 3)
        {
            int step = (MenuInput.Right ? 1 : 0) - (MenuInput.Left ? 1 : 0);
            if (step != 0) ChangeVolume(_focus, step * 0.1f);
        }
        if (MenuInput.Confirm()) Choose(_focus);
    }

    int RowCount => _page switch
    {
        Page.Main => MainItems.Length,
        Page.Volume => 4,
        Page.Keys => KeyBindings.Entries.Length + 2,
        _ => 2,
    };

    void Choose(int index)
    {
        switch (_page)
        {
            case Page.Main:
                MainItem item = MainItems[Mathf.Clamp(index, 0, MainItems.Length - 1)];
                if (item == MainItem.Resume || item == MainItem.Close) { Close(); return; }
                GameAudio.Event("ui_decide");
                if (item == MainItem.Volume) OpenPage(Page.Volume);
                else if (item == MainItem.Keys) OpenPage(Page.Keys);
                else if (item == MainItem.Title) { OpenPage(Page.ConfirmTitle); _focus = 1; } // 最初は「いいえ」
                break;
            case Page.Volume:
                if (index == 3) GoBack();
                break;
            case Page.Keys:
                int n = KeyBindings.Entries.Length;
                if (index < n)
                {
                    GameAudio.Event("ui_decide");
                    _capturing = KeyBindings.Entries[index];
                    _captureFrame = Time.frameCount;
                }
                else if (index == n)
                {
                    GameAudio.Event("ui_decide");
                    KeyBindings.ResetAll();
                }
                else GoBack();
                break;
            case Page.ConfirmTitle:
                if (index == 0) ReturnToTitle();
                else GoBack();
                break;
        }
    }

    void OpenPage(Page page)
    {
        _page = page;
        _focus = 0;
    }

    void GoBack()
    {
        GameAudio.Event("ui_cancel");
        MainItem from = _page switch { Page.Volume => MainItem.Volume, Page.Keys => MainItem.Keys, Page.ConfirmTitle => MainItem.Title, _ => MainItem.Resume };
        _page = Page.Main;
        _focus = Mathf.Max(0, System.Array.IndexOf(MainItems, from));
    }

    enum MainItem { Resume, Volume, Keys, Title, Close }

    /// <summary>最初のページの項目：一時停止は「もどる・音量・キー設定・（ランなら）タイトル」、設定は「音量・キー設定・閉じる」</summary>
    MainItem[] MainItems => _settingsOnly
        ? new[] { MainItem.Volume, MainItem.Keys, MainItem.Close }
        : InRun ? new[] { MainItem.Resume, MainItem.Volume, MainItem.Keys, MainItem.Title }
        : new[] { MainItem.Resume, MainItem.Volume, MainItem.Keys };

    static string LabelOf(MainItem item) => item switch
    {
        MainItem.Resume => "ゲームにもどる",
        MainItem.Volume => "音量",
        MainItem.Keys => "キー設定",
        MainItem.Title => "タイトルにもどる",
        _ => "閉じる",
    };

    void ReturnToTitle()
    {
        GameSession session = GameSession.Instance;
        GameAudio.Event("ui_decide");
        Close(false);
        if (session != null) session.ReturnToTitle();
    }

    // ───────── 音量 ─────────

    static readonly string[] VolumeLabels = { "全体", "BGM", "効果音" };

    static float GetVolume(int i) => i == 0 ? GameAudio.MasterVolume : i == 1 ? GameAudio.BgmVolume : GameAudio.SeVolume;

    void SetVolume(int i, float v)
    {
        v = Mathf.Clamp01(Mathf.Round(v * 10f) / 10f); // 10% きざみ
        if (Mathf.Approximately(v, GetVolume(i))) return;
        if (i == 0) GameAudio.MasterVolume = v;
        else if (i == 1) GameAudio.BgmVolume = v;
        else GameAudio.SeVolume = v;
        GameAudio.Event("ui_move"); // 変えた大きさで鳴る（BGM はそのまま流れている曲の大きさが変わる）
    }

    void ChangeVolume(int i, float delta) => SetVolume(i, GetVolume(i) + delta);

    // ───────── キー設定 ─────────

    void Capture()
    {
        if (Time.frameCount == _captureFrame) return; // 決めたときの Enter を拾わない
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if (kb == null || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            _capturing = null;
            GameAudio.Event("ui_cancel");
            return;
        }
        foreach (KeyControl key in kb.allKeys)
        {
            if (key == null || !key.wasPressedThisFrame) continue;
            if (key.keyCode == Key.Escape)
            {
                _capturing = null;
                GameAudio.Event("ui_cancel");
                return;
            }
            KeyBindings.Set(_capturing, "<Keyboard>/" + key.name);
            _capturing = null;
            GameAudio.Event("ui_decide");
            return;
        }
    }

    // ───────── 描く ─────────

    void OnGUI()
    {
        if (!_paused) return;
        // HUD・タイトル・図鑑（-50）より手前。最後に入っている値で重なり順が決まるので、元の値には戻さない
        GUI.depth = -200;
        PixelGui.Begin(false);
        float w = PixelGui.Width, h = PixelGui.Height;
        PixelGui.Fill(new Rect(0, 0, w, h), new Color(0.02f, 0.03f, 0.06f, _settingsOnly ? 0.35f : 0.55f));

        float winH = _page switch { Page.Main => MainItems.Length >= 4 ? 200 : 172, Page.Volume => 190, Page.Keys => 340, _ => 150 };
        Rect win = MacDesktopGui.CenteredWindow(w, h, _page == Page.Keys ? 400 : 340, winH, false);
        string title = _page switch { Page.Volume => "音量", Page.Keys => "キー設定（キーボード）", Page.ConfirmTitle => "タイトルにもどる", _ => _settingsOnly ? "設定" : "一時停止" };
        Rect inner = MacDesktopGui.Window(win, title, out MacDesktopGui.WindowAction action);
        if (action == MacDesktopGui.WindowAction.Close)
        {
            if (_page == Page.Main) Close(); else GoBack();
        }
        else if (_paused)
        {
            switch (_page)
            {
                case Page.Main: DrawMain(inner); break;
                case Page.Volume: DrawVolume(inner); break;
                case Page.Keys: DrawKeys(inner); break;
                case Page.ConfirmTitle: DrawConfirm(inner); break;
            }
        }
        // 後ろのメニュー・HUD のボタンを押させない
        GUI.Button(new Rect(0, 0, w, h), GUIContent.none, GUIStyle.none);
        PixelGui.End();
    }

    /// <summary>マウスを乗せたら、その行を選ぶ</summary>
    void Hover(Rect r, int index)
    {
        if (Event.current.type == EventType.MouseMove && r.Contains(Event.current.mousePosition) && _capturing == null) _focus = index;
    }

    void Hint(Rect inner, string text)
        => PixelGui.Text(new Rect(inner.x, inner.yMax - 16, inner.width, 16), text, 12, MacDesktopGui.Muted, TextAnchor.MiddleCenter);

    void DrawMain(Rect inner)
    {
        MainItem[] items = MainItems;
        for (int i = 0; i < items.Length; i++)
        {
            Rect r = new Rect(inner.x + 40, inner.y + 4 + i * 32, inner.width - 80, 26);
            Hover(r, i);
            if (MacDesktopGui.Button(r, LabelOf(items[i]), false, i == _focus)) { _focus = i; Choose(i); return; }
        }
        Hint(inner, _settingsOnly ? "↑↓ 選択　A 決定　Esc／B 閉じる" : "↑↓ 選択　A 決定　Esc／START 閉じる");
    }

    void DrawVolume(Rect inner)
    {
        for (int i = 0; i < 3; i++)
        {
            Rect row = new Rect(inner.x + 8, inner.y + 4 + i * 30, inner.width - 16, 26);
            Hover(row, i);
            if (i == _focus) MacDesktopGui.Rounded(row, new Color32(222, 234, 252, 255), 4);
            PixelGui.Text(new Rect(row.x + 10, row.y + 3, 70, 20), VolumeLabels[i], 12, MacDesktopGui.Ink);
            float v = GetVolume(i);
            Rect minus = new Rect(row.x + 82, row.y + 4, 18, 18), plus = new Rect(row.xMax - 66, row.y + 4, 18, 18);
            Rect meter = new Rect(minus.xMax + 6, row.y + 10, plus.x - minus.xMax - 12, 7);
            if (MacDesktopGui.Button(minus, "-")) { _focus = i; ChangeVolume(i, -0.1f); }
            if (MacDesktopGui.Button(plus, "+")) { _focus = i; ChangeVolume(i, 0.1f); }
            PixelGui.Meter(meter, 10, v, i == _focus ? MacDesktopGui.Blue : MacDesktopGui.Muted, MacDesktopGui.BorderColor);
            // メーターをクリックすると、その位置の大きさにする
            Event e = Event.current;
            if (e.type == EventType.MouseDown && new Rect(meter.x, row.y, meter.width, row.height).Contains(e.mousePosition))
            {
                _focus = i;
                SetVolume(i, (e.mousePosition.x - meter.x) / meter.width);
                e.Use();
            }
            PixelGui.TextEn(new Rect(plus.xMax + 6, row.y + 9, 40, 8), $"{Mathf.RoundToInt(v * 100f)}%", 8, MacDesktopGui.Ink);
        }
        Rect back = new Rect(inner.x + inner.width * 0.5f - 60, inner.y + 100, 120, 24);
        Hover(back, 3);
        if (MacDesktopGui.Button(back, "もどる", false, _focus == 3)) GoBack();
        Hint(inner, "↑↓ 選択　←→ 変更　Esc／B もどる");
    }

    void DrawKeys(Rect inner)
    {
        var entries = KeyBindings.Entries;
        const float rowH = 17f;
        for (int i = 0; i < entries.Length; i++)
        {
            Rect row = new Rect(inner.x + 4, inner.y + i * rowH, inner.width - 8, rowH - 1);
            Hover(row, i);
            bool focused = i == _focus;
            bool waiting = _capturing == entries[i];
            if (focused) MacDesktopGui.Rounded(row, waiting ? MacDesktopGui.Blue : new Color32(222, 234, 252, 255), 3);
            PixelGui.Text(new Rect(row.x + 8, row.y, 220, rowH - 1), entries[i].Label, 12, waiting ? Color.white : MacDesktopGui.Ink);
            string key = waiting ? "キーを押してください" : KeyBindings.DisplayName(KeyBindings.Get(entries[i]));
            PixelGui.Text(new Rect(row.xMax - 168, row.y, 160, rowH - 1), key, 12, waiting ? Color.white : focused ? MacDesktopGui.Blue : MacDesktopGui.Ink, TextAnchor.MiddleRight);
            if (_capturing == null && GUI.Button(row, GUIContent.none, GUIStyle.none)) { _focus = i; Choose(i); return; }
        }
        float y = inner.y + entries.Length * rowH + 6;
        Rect reset = new Rect(inner.x + 20, y, 160, 22), back = new Rect(inner.xMax - 140, y, 120, 22);
        Hover(reset, entries.Length);
        Hover(back, entries.Length + 1);
        if (MacDesktopGui.Button(reset, "初期設定にもどす", false, _focus == entries.Length) && _capturing == null) { _focus = entries.Length; Choose(_focus); }
        if (MacDesktopGui.Button(back, "もどる", false, _focus == entries.Length + 1) && _capturing == null) GoBack();
        Hint(inner, _capturing != null ? "Esc でやめる（同じキーの操作とは入れ替え）" : "↑↓ 選択　Enter 変更　Esc もどる　※パッドは固定");
    }

    void DrawConfirm(Rect inner)
    {
        PixelGui.Text(new Rect(inner.x, inner.y + 4, inner.width, 20), "タイトルにもどりますか？", 16, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
        PixelGui.Text(new Rect(inner.x, inner.y + 28, inner.width, 18), "今のランの進み具合は失われます。", 12, MacDesktopGui.Muted, TextAnchor.MiddleCenter);
        Rect yes = new Rect(inner.x + inner.width * 0.5f - 130, inner.y + 58, 120, 26), no = new Rect(inner.x + inner.width * 0.5f + 10, inner.y + 58, 120, 26);
        Hover(yes, 0);
        Hover(no, 1);
        if (MacDesktopGui.Button(yes, "はい", false, _focus == 0)) { ReturnToTitle(); return; }
        if (MacDesktopGui.Button(no, "いいえ", false, _focus == 1)) GoBack();
    }
}
