using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// バックアップ・復元・タイトルへ の演出（2026-11-20 まさぴ〜「GameOver画面の部分リトライを押したらそのステージが始まる時のバックアップデータで復元みたいな演出が欲しい。
/// なのでステージに入るごとにバックアップとってるふうな演出もしたいね。でGameOver画面でタイトルに戻る機能も欲しいからそれはそれで演出を考えて実装してみて」。
/// 設計：docs/backup-restore-design.md（Fable 5.2）。数値はすべて【仮】）。GameSession が自分と同じ物に付ける。
/// 3つを同じ印「バックアップのドライブ」（面にプレイヤーの顔の印。時計・回る矢印は描かない）でつなぐ。
/// ・A バックアップ：LoadCurrentStage で控え（時刻・アイコン）を取り、シーンを読み終わって明るくなり始めた所から 1.9 秒、右下の小さい黒い札に
///   「アプリ → 点 → ドライブ」と青いバー（左→右）→ 緑のチェックと時刻 → 引っ込む。ゲームは止めない（待ち 0 秒）。リトライでは出ない
/// ・B 復元：FailStage（ボタンも R も）の瞬間から真っ黒の上に同じ並びの大きい窓。緑のバーが右→左へ伸び、暗かったアイコンに色が戻る（1.4 秒＋暗転 0.4 秒）
/// ・C タイトルへ：ゲームオーバーから ReturnToTitle したときだけ。ドライブが沈んで消え → 顔の印と白いバー（起動し直す）→ 暗転 → タイトル（1.6 秒＋暗転 0.4 秒）
/// 字は B の窓の1行と、A の時刻だけ。赤は使わない。時間はすべて unscaledTime。
/// 切り戻し：Enabled = false（3つとも出ず、GameSession の切り替えは今までどおり）。
/// </summary>
public class BackupFx : MonoBehaviour
{
    /// <summary>false＝今までどおり（演出なし）。切り戻し用</summary>
    public static bool Enabled = true;

    // ───────── 時刻（設計書 2-3・3-3・4-2） ─────────
    const float PillIn = 0.20f, PillBarEnd = 1.10f, PillOutAt = 1.70f, PillEnd = 1.90f, PillHop = 0.08f;
    const float RestoreOpen = 0.15f, RewindAt = 0.20f, RestoreBarAt = 0.25f, RestoreDoneAt = 1.10f, RestoreEnd = 1.40f;
    const float SkipGuard = 0.30f, FlashLen = 0.12f, SparkleLen = 0.4f;
    const float QuitLedOff = 0.15f, QuitSinkEnd = 0.50f, QuitMarkIn = 0.65f, QuitBarAt = 0.55f, QuitEnd = 1.60f;

    enum Mode { None, Restore, Quit }

    static readonly Color PillBg = new Color32(28, 28, 30, 235);
    static readonly Color DimIcon = new Color(0.38f, 0.36f, 0.45f, 1f);
    static readonly string[] CheckRows = { "......#", ".....#.", "#...#..", ".#.#...", "..#...." };

    // 控え（LoadCurrentStage のときに取る。リトライでは取り直さない）
    string _time;     // "14:32"（無ければ null）
    Sprite _icon;     // そのステージのアイコン
    bool _armed;      // 次にシーンを読み終わったら札を出す

    float _pillAt = -1f; // 札が出始めた時刻（-1＝出ていない）
    bool _pillSounded;

    Mode _mode;
    float _startAt;      // B・C が始まった時刻
    Sprite _restoreIcon;
    string _restoreText;
    int _sounded;        // 鳴らした音（ビット）
    bool _quitSkipped;   // C を飛ばした（まだ鳴らしていない音は鳴らさない）

    /// <summary>B・C を見せ終わった（GameSession が暗くしてシーンを読む）</summary>
    public bool IsDone { get; private set; } = true;

    // ───────── GameSession から ─────────

    /// <summary>ステージに入る（LoadCurrentStage）：控えを取り、次にシーンを読み終わったら札を出す</summary>
    public void Arm(StagePlan stage)
    {
        _time = System.DateTime.Now.ToString("HH:mm");
        _icon = stage != null ? stage.Icon16 : null;
        _armed = true;
    }

    /// <summary>シーンを読み終わった：控えを取ったばかりなら札（A）を出す</summary>
    public void ShowPillIfArmed()
    {
        if (!_armed) return;
        _armed = false;
        _pillAt = Time.unscaledTime;
        _pillSounded = false;
    }

    /// <summary>出ている札を消す（「次に出す」の印は下ろさない。Arm の直後に Transition の最初でも呼ばれる）</summary>
    public void StopPill() => _pillAt = -1f;

    /// <summary>B（復元）を始める。この呼び出しのフレームから真っ黒に塗る</summary>
    public void BeginRestore(StagePlan stage)
    {
        Begin(Mode.Restore);
        _restoreIcon = stage != null && stage.Icon16 != null ? stage.Icon16 : _icon;
        _restoreText = string.IsNullOrEmpty(_time) ? "バックアップから復元中…" : _time + " のバックアップから復元中…";
    }

    /// <summary>C（タイトルへ）を始める。Run は使わない（先に null になる）</summary>
    public void BeginQuit()
    {
        Begin(Mode.Quit);
        _armed = false;
        _time = null; // ランは終わり（次のランの控えは、次の Arm で取る）
        _icon = null;
    }

    /// <summary>B・C を消す</summary>
    public void Hide()
    {
        _mode = Mode.None;
        IsDone = true;
    }

    void Begin(Mode mode)
    {
        _mode = mode;
        _startAt = Time.unscaledTime;
        _sounded = 0;
        _quitSkipped = false;
        IsDone = false;
        _pillAt = -1f;
    }

    float T => Time.unscaledTime - _startAt;
    float PillT => Time.unscaledTime - _pillAt;

    // ───────── 進み・音・早送り ─────────

    void Update()
    {
        if (_pillAt >= 0f)
        {
            GameSession s = GameSession.Instance;
            if (!Enabled || PillT >= PillEnd || (s != null && s.IsGameOver)) _pillAt = -1f;
            else if (!_pillSounded && PillT >= PillBarEnd)
            {
                _pillSounded = true;
                GameAudio.Event("backup_done");
            }
        }
        if (_mode == Mode.None) return;
        float t = T;
        if (_mode == Mode.Restore)
        {
            if (t >= SkipGuard && t < RestoreDoneAt && SkipPressed(true)) { _startAt = Time.unscaledTime - RestoreDoneAt; t = RestoreDoneAt; }
            if (t >= RewindAt) SoundOnce(1, "restore_rewind");
            if (t >= RestoreDoneAt) SoundOnce(2, "restore_done");
            if (t >= RestoreEnd) IsDone = true;
        }
        else
        {
            if (!IsDone && t >= SkipGuard && SkipPressed(false)) { IsDone = true; _quitSkipped = true; }
            if (!_quitSkipped)
            {
                if (t >= QuitLedOff) SoundOnce(1, "ui_cancel");
                if (t >= QuitSinkEnd) SoundOnce(2, "title_reboot");
            }
            if (t >= QuitEnd) IsDone = true;
        }
    }

    void SoundOnce(int bit, string key)
    {
        if ((_sounded & bit) != 0) return;
        _sounded |= bit;
        GameAudio.Event(key);
    }

    // R（復元のときだけ）・Z・Enter・Space・マウスの左・ゲームパッドの下のボタン
    static bool SkipPressed(bool allowR)
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
            || kb.spaceKey.wasPressedThisFrame || (allowR && kb.rKey.wasPressedThisFrame))) return true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var pad = Gamepad.current;
        return pad != null && pad.buttonSouth.wasPressedThisFrame;
    }

    // ───────── 描く ─────────

    void OnGUI()
    {
        if (!Enabled) return;
        if (_mode != Mode.None)
        {
            GUI.depth = -900; // ゲームオーバー・入れ替え・デスクトップより手前、暗転（-1000）より奥
            PixelGui.Begin(false);
            float W = PixelGui.Width, H = PixelGui.Height;
            PixelGui.Fill(new Rect(0f, 0f, W, H), Color.black);
            if (_mode == Mode.Restore) DrawRestore(W, H, T);
            else DrawQuit(W, H, T);
            PixelGui.End();
            var e = Event.current;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel || e.type == EventType.KeyDown) e.Use();
            return;
        }
        if (_pillAt < 0f) return;
        GameSession s = GameSession.Instance;
        if (s != null && s.IsGameOver) return; // 切り替えの最初で StopPill されるので、出ている間の切り替えは明けの暗転だけ（その下でうっすら見える。設計書 2-3）
        GUI.depth = 5; // HUD（10）より手前、一時停止（-200）より奥
        PixelGui.Begin(false);
        DrawPill(PixelGui.Width, PixelGui.Height, PillT);
        PixelGui.End();
    }

    // A：右下の札（設計書 2-2・2-3）
    void DrawPill(float W, float H, float b)
    {
        float slide;
        if (b < PillIn) { float k = 1f - b / PillIn; slide = Mathf.Round(124f * k * k); }
        else if (b >= PillOutAt) { float k = Mathf.Clamp01((b - PillOutAt) / (PillEnd - PillOutAt)); slide = Mathf.Round(124f * k * k); }
        else slide = 0f;
        var p = new Rect(W - 124f + slide, H - 26f, 118f, 20f);
        MacDesktopGui.Rounded(p, PillBg, 5);
        PixelGui.Fill(new Rect(p.x + 5f, p.y, 108f, 1f), new Color(1f, 1f, 1f, 0.12f));
        DrawIcon(new Rect(p.x + 5f, p.y + 2f, 16f, 16f), _icon, Color.white);
        bool done = b >= PillBarEnd;
        int lit = !done && b >= PillIn ? Mathf.FloorToInt((b - PillIn) / 0.1f) % 3 : -1;
        for (int i = 0; i < 3; i++)
            PixelGui.Fill(new Rect(p.x + 26f + i * 5f, p.y + 9f, 2f, 2f), i == lit ? MacDesktopGui.Blue : new Color(1f, 1f, 1f, 0.25f));
        float hop = done && b < PillBarEnd + PillHop ? 1f : 0f;
        DrawDrive(p.x + 42f, p.y + 3f - hop, 1, done ? PixelGui.HpGreen : BusyLed());
        if (!done)
        {
            float bar = Mathf.Clamp01((b - PillIn) / (PillBarEnd - PillIn));
            MacUi.Gui.Progress(new Rect(p.x + 66f, p.y + 8f, 46f, 4f), bar, MacLook.Dark);
        }
        else
        {
            DrawCheck(p.x + 66f, p.y + 7f);
            if (!string.IsNullOrEmpty(_time)) PixelGui.TextEn(new Rect(p.x + 76f, p.y + 2f, 40f, 16f), _time, 8, Color.white, TextAnchor.MiddleLeft);
        }
    }

    // B：真っ黒の上の窓（設計書 3-2・3-3）
    void DrawRestore(float W, float H, float r)
    {
        var wn = new Rect(Mathf.Round((W - 232f) * 0.5f), Mathf.Round((H - 112f) * 0.5f) - 4f, 232f, 112f);
        if (r < RestoreOpen)
        {
            float k = r / RestoreOpen, e = 1f - (1f - k) * (1f - k), s = Mathf.Lerp(0.6f, 1f, e);
            var box = new Rect(Mathf.Round(wn.center.x - wn.width * s * 0.5f), Mathf.Round(wn.center.y - wn.height * s * 0.5f),
                Mathf.Round(wn.width * s), Mathf.Round(wn.height * s));
            MacDesktopGui.Rounded(box, MacDesktopGui.BorderColor, 6);
            MacDesktopGui.Rounded(PixelGui.Inset(box, 1f), MacDesktopGui.Paper, 5);
            return;
        }
        bool enabled = GUI.enabled;
        GUI.enabled = false; // 信号機を押せないように
        MacDesktopGui.Window(wn, "", out _);
        GUI.enabled = enabled;
        bool done = r >= RestoreDoneAt;
        var icon = new Rect(wn.x + 52f, wn.y + 36f, 32f, 32f);
        DrawIcon(icon, _restoreIcon, done ? Color.white : DimIcon);
        int lit = !done && r >= RestoreBarAt ? 4 - Mathf.FloorToInt((r - RestoreBarAt) / 0.08f) % 5 : -1;
        for (int i = 0; i < 5; i++)
            PixelGui.Fill(new Rect(wn.x + 94f + i * 9f, wn.y + 51f, 3f, 3f), done || i == lit ? PixelGui.HpGreen : MacUi.Light4);
        DrawDrive(wn.x + 144f, wn.y + 38f, 2, done ? PixelGui.HpGreen : BusyLed());
        var bar = new Rect(wn.x + 16f, wn.y + 78f, 200f, 6f);
        MacUi.Gui.Progress(bar, 0f, MacLook.Light);
        float pr = done ? 1f : Mathf.Clamp01((r - RestoreBarAt) / (RestoreDoneAt - RestoreBarAt));
        float w = Mathf.Round(200f * pr);
        if (w >= 3f) MacUi.Gui.RoundFill(new Rect(bar.xMax - w, bar.y, w, 6f), 1, PixelGui.HpGreen); // 右から左へ伸びる
        PixelGui.TextFit(new Rect(wn.x + 16f, wn.y + 90f, 200f, 14f), _restoreText, 12, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
        if (done) DrawRestored(icon, r - RestoreDoneAt);
    }

    // C：ドライブが沈んで消え、顔の印と白いバー（設計書 4-2）
    static void DrawQuit(float W, float H, float q)
    {
        float cx = Mathf.Round(W * 0.5f), cy = Mathf.Round(H * 0.5f);
        if (q < QuitSinkEnd)
        {
            float k = q < QuitLedOff ? 0f : (q - QuitLedOff) / (QuitSinkEnd - QuitLedOff);
            DrawDrive(cx - 18f, cy - 24f + Mathf.Round(10f * k * k), 2, q < QuitLedOff ? PixelGui.HpGreen : MacUi.Gray1, 1f - k);
        }
        if (q < QuitSinkEnd) return;
        float a = Mathf.Clamp01((q - QuitSinkEnd) / (QuitMarkIn - QuitSinkEnd));
        Color mark = MacUi.Light1;
        mark.a = a;
        PixelGui.DrawSprite(new Rect(cx - 12f, cy - 26f, 24f, 24f), MacUi.Mark(24), mark);
        if (q < QuitBarAt) return;
        float t = q < 0.90f ? Mathf.Lerp(0f, 0.6f, (q - QuitBarAt) / (0.90f - QuitBarAt))
            : q < 1.10f ? 0.6f
            : Mathf.Lerp(0.6f, 1f, Mathf.Clamp01((q - 1.10f) / 0.35f));
        MacUi.Gui.Progress(new Rect(cx - 48f, cy + 12f, 96f, 4f), t, MacLook.Dark, MacUi.Light1);
    }

    // できた瞬間：アイコンを広げた白い四角が消え、きらめく（DesktopScreen.DrawRestore と同じ描き方）
    static void DrawRestored(Rect r, float since)
    {
        if (since < FlashLen) PixelGui.Fill(PixelGui.Inset(r, -2f), new Color(1f, 1f, 1f, 1f - since / FlashLen));
        if (since > SparkleLen) return;
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(since * 12f) % frames.Length)];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.center.x - fr.width), Mathf.Round(r.center.y - fr.height), fr.width * 2f, fr.height * 2f), f);
    }

    static void DrawIcon(Rect r, Sprite icon, Color tint)
    {
        if (icon == null) { PixelGui.Fill(r, PixelGui.Frame); return; }
        PixelGui.DrawSprite(r, icon, tint);
    }

    // 緑のチェック（7×5）
    static void DrawCheck(float x, float y)
    {
        for (int row = 0; row < CheckRows.Length; row++)
            for (int col = 0; col < CheckRows[row].Length; col++)
                if (CheckRows[row][col] == '#') PixelGui.Fill(new Rect(x + col, y + row, 1f, 1f), PixelGui.HpGreen);
    }

    // 動いているランプ：青と灰を 1秒に 8回 入れ替え
    static Color BusyLed() => Mathf.FloorToInt(Time.unscaledTime * 8f) % 2 == 0 ? MacDesktopGui.Blue : MacUi.Light5;

    static Color A(Color c, float a) { c.a *= a; return c; }

    /// <summary>共通の印「バックアップのドライブ」（18×14 ドット × s。x・y は左上。a は全体の不透明度。設計書 1-1）。GameOverScreen も使う</summary>
    public static void DrawDrive(float x, float y, int s, Color led, float a = 1f)
    {
        if (a <= 0f) return;
        s = Mathf.Max(1, s);
        x = Mathf.Round(x); y = Mathf.Round(y);
        MacDesktopGui.Rounded(new Rect(x, y, 18 * s, 14 * s), A(MacUi.Gray2, a), 2 * s);
        MacDesktopGui.Rounded(new Rect(x + s, y + s, 16 * s, 12 * s), A(MacUi.Light2, a), s);
        PixelGui.Fill(new Rect(x + 2 * s, y + s, 14 * s, s), A(Color.white, a));
        PixelGui.Fill(new Rect(x + 2 * s, y + 10 * s, 14 * s, 2 * s), A(MacUi.Light4, a));
        PixelGui.DrawSprite(new Rect(x + 3 * s, y + 2 * s, 7 * s, 7 * s), MacUi.Mark(7), A(MacUi.Gray3, a));
        for (int i = 0; i < 3; i++) PixelGui.Fill(new Rect(x + 12 * s, y + (3 + i * 2) * s, 3 * s, s), A(MacUi.Light5, a));
        PixelGui.Fill(new Rect(x + 12 * s, y + 10 * s, 3 * s, 2 * s), A(led, a));
    }
}
