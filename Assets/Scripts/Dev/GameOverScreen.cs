using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ゲーム内のクラッシュ演出。ウィンドウ増殖→暗転→再起動メッセージ→再開の選択。</summary>
public class GameOverScreen : MonoBehaviour
{
    public const float StartDelay = 0.4f;
    public const float CascadeSeconds = 2.8f;
    const float AlertDelay = 1.7f;
    public const float BlackoutSeconds = 0.35f;
    public const float MessageSeconds = 4.5f;
    const int MaxWindows = 60;
    const string PreviewKey = "Team1.PreviewMacGameOver";
    int _focus, _tick;
    readonly Vector4[] _windows = new Vector4[MaxWindows];
    float _shownAt = -1f;
    bool _choices;
    static Font _font;
    GameSession Session => GameSession.Instance;
    bool Visible => Session != null && Session.IsGameOver && !Session.IsTransitioning;
    float Age => _shownAt < 0f ? -StartDelay : Time.unscaledTime - _shownAt - StartDelay;
    public static int WindowCountAt(float age) => Mathf.Clamp(4 + Mathf.FloorToInt(Mathf.Max(0f, age) * 30f), 4, MaxWindows);

    void Update()
    {
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool(PreviewKey, false) && Session != null && Session.Run != null && !Session.IsTransitioning)
        {
            UnityEditor.SessionState.SetBool(PreviewKey, false);
            Session.Run.CurrentIndex = 0;
            Session.ShowGameOver();
        }
#endif
        if (!Visible) { _shownAt = -1f; return; }
        var kb = Keyboard.current;
        // 演出・暗転・メッセージのどの段階でも、Rは直接リトライする。
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            GameAudio.Event("ui_decide");
            Session.FailStage();
            return;
        }
        if (_shownAt < 0f)
        {
            _shownAt = Time.unscaledTime;
            _focus = _tick = 0;
            _choices = false;
            // 20枚ずつ、3つの束に分かれて斜めに連なる。
            // 束の開始位置だけ変え、束の中では同じサイズで少しずつずらす。
            var random = new System.Random(System.Environment.TickCount);
            for (int stack = 0; stack < 3; stack++)
            {
                float anchorX = stack == 0 ? 0.02f : stack == 1 ? 0.55f : 0.18f;
                float anchorY = stack == 0 ? 0.02f : stack == 1 ? 0.18f : 0.85f;
                float width = Mathf.Lerp(330f, 390f, (float)random.NextDouble());
                float height = Mathf.Lerp(200f, 235f, (float)random.NextDouble());
                for (int step = 0; step < 20; step++)
                    _windows[stack * 20 + step] = new Vector4(anchorX, anchorY, width, height);
            }
            MacDesktopWallpaper.Ensure(this, () => Visible && Age >= 0f && Age < CascadeSeconds);
        }
        if (Age < 0f) return;
        if (Age < CascadeSeconds)
        {
            int tick = WindowCountAt(Age) / 6;
            if (tick > _tick)
            {
                _tick = tick;
                GameAudio.Event("gameover_tick", 1f + tick * 0.035f);
            }
            return;
        }
        if (Time.unscaledTime - _shownAt < 0.4f) return; // 倒れた瞬間の押しっぱなしで決めない
        // 再起動の画面のあと、何かキー・クリック・コントローラー、または 4.5 秒で選ぶ板を出す（d1dc07f0 のれっきーさんの処理。7ef855e2 のマージで消えていたので戻した。2026-11-20）
        float messageAge = Age - CascadeSeconds - BlackoutSeconds;
        if (messageAge < 0.6f) return;
        if (!_choices)
        {
            bool pressed = (kb != null && kb.anyKey.wasPressedThisFrame) || MenuInput.Confirm();
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (messageAge >= MessageSeconds || pressed || clicked)
            {
                _choices = true;
                GameAudio.Event("ui_move");
            }
            return; // このキーを選択の決定として再利用しない
        }
        int before = _focus;
        // キーボードとコントローラー（2026-11-20）。Space では決めない（今までどおり）
        if (MenuInput.Left) _focus = 0;
        if (MenuInput.Right) _focus = 1;
        if (_focus != before) GameAudio.Event("ui_move");
        if (MenuInput.Confirm(allowSpace: false)) Choose(_focus);
    }

    void Choose(int index)
    {
        if (!Visible || !_choices) return;
        GameAudio.Event("ui_decide");
        if (index == 0) Session.FailStage();
        else Session.ReturnToTitle();
    }

    void OnGUI()
    {
        if (!Visible || Age < 0f) return;
        int depth = GUI.depth;
        GUI.depth = -300; // ステージの表示より手前、シーン遷移の暗転より奥
        PixelGui.Begin(false);
        if (Age < CascadeSeconds) DrawCascade(PixelGui.Width, PixelGui.Height);
        else
        {
            PixelGui.Fill(new Rect(0, 0, PixelGui.Width, PixelGui.Height), Color.black);
            if (Age >= CascadeSeconds + BlackoutSeconds) DrawRestartMessage();
        }
        Label(new Rect(PixelGui.Width - 132, PixelGui.Height - 18, 122, 15), "R：すぐにリトライ", 9,
            new Color(0.7f, 0.72f, 0.76f), TextAnchor.MiddleRight);
        PixelGui.End();
        GUI.depth = depth;
        var e = Event.current;
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel || e.type == EventType.KeyDown) e.Use();
    }

    void DrawCascade(float width, float height)
    {
        MacDesktopGui.MenuBar(width, "Finder");
        // 背景はタイトルと同じ壁紙。ウィンドウは描画だけで、実際のアプリは開かない。
        bool enabled = GUI.enabled;
        GUI.enabled = false;
        MacDesktopGui.Folders(width);
        MacDesktopGui.Dock(width, height, null);
        int count = WindowCountAt(Age);
        for (int i = 0; i < count; i++)
        {
            Vector4 placement = _windows[i];
            float w = Mathf.Min(placement.z, width - 24f);
            float h = Mathf.Min(placement.w, height - 48f);
            // 画面内の別の場所から20枚の束が重なる。クランプで末尾が一点に潰れないようにする。
            int step = i % 20;
            float baseX = Mathf.Lerp(4f, Mathf.Max(4f, width - w - 12f), placement.x);
            float baseY = Mathf.Lerp(26f, Mathf.Max(26f, height - h - 24f), placement.y);
            float x = Mathf.Round(baseX + step * 11f);
            float y = Mathf.Round(baseY + step * 7f);
            Rect win = new Rect(x, y, w, h);
            string name = i % 3 == 0 ? "Finder" : i % 3 == 1 ? "書類" : "ダウンロード";
            Rect inner = MacDesktopGui.Window(win, "", out _);
            Color infection = i % 2 == 0 ? new Color32(145, 235, 65, 255) : new Color32(235, 66, 165, 255);
            PixelGui.Fill(new Rect(win.x + 1, win.y + 28, win.width - 2, win.height - 30), new Color32(24, 18, 33, 255));
            PixelGui.Fill(new Rect(win.x + 52, win.y + 2, win.width - 56, 24), new Color32(53, 24, 53, 255));
            PixelGui.Border(win, new Color(infection.r, infection.g, infection.b, 0.65f), 1);
            Label(new Rect(win.x + 53, win.y + 5, win.width - 57, 17), name + " — INFECTED", 10, infection);
            Label(new Rect(win.x + 6, win.y + 31, win.width - 12, 15), "INFECTED", 9, infection, TextAnchor.MiddleCenter, true);
            // 破損したデータの帯。狭い窓でも文字やウイルスを外にはみ出させない。
            for (int j = 0; j < 7; j++)
            {
                float lineY = win.y + 54 + j * 18;
                if (lineY > win.yMax - 5) break;
                PixelGui.Fill(new Rect(win.x + 7, lineY, (win.width - 14) * (j == 1 ? 0.5f : 0.85f), 2),
                    new Color(infection.r, infection.g, infection.b, 0.4f));
            }
            Sprite[] virus = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
            if (virus != null && virus.Length > 0 && h >= 60)
                PixelGui.DrawSprite(new Rect(win.xMax - 25, win.yMax - 25, 20, 20),
                    virus[(Mathf.FloorToInt(Age * 5f) + i) % virus.Length]);
            Label(new Rect(win.x + 10, win.y + 73, win.width - 20, 20), "破損_■■■  /  アクセス拒否", 11, infection);
            int noiseFrame = Mathf.FloorToInt(Age * 5f);
            for (int j = 0; j < 8; j++)
            {
                float nx = win.x + 5 + (i * 29 + j * 47) % Mathf.Max(1, (int)w - 30);
                float ny = win.y + 32 + (i * 13 + j * 37 + noiseFrame * 3) % Mathf.Max(1, (int)h - 42);
                PixelGui.Fill(new Rect(nx, ny, j % 2 == 0 ? 24 : 5, 2),
                    new Color(infection.r, infection.g, infection.b, 0.45f));
            }
        }
        GUI.enabled = enabled;
        if (Age >= AlertDelay)
        {
            // レイアウト全体を85%に縮小し、文字とアイコンの比率を保つ。
            Matrix4x4 alertMatrix = GUI.matrix;
            const float alertScale = 0.85f;
            Vector2 alertOrigin = new Vector2((width - 480f * alertScale) * 0.5f,
                Mathf.Max(35f, (height - 180f * alertScale) * 0.45f));
            GUI.matrix = alertMatrix * Matrix4x4.TRS(alertOrigin, Quaternion.identity,
                new Vector3(alertScale, alertScale, 1f));
            Rect alert = new Rect(0f, 0f, 480f, 180f);
            MacDesktopGui.Rounded(new Rect(alert.x + 2, alert.y + 4, alert.width, alert.height), new Color(0, 0, 0, 0.35f), 12);
            MacDesktopGui.Rounded(alert, new Color32(235, 235, 235, 255), 12);
            MacDesktopGui.Rounded(new Rect(alert.x, alert.y, alert.width, 25), new Color32(213, 213, 213, 255), 12);
            PixelGui.Fill(new Rect(alert.x, alert.y + 14, alert.width, 11), new Color32(213, 213, 213, 255));
            PixelGui.Fill(new Rect(alert.x + 1, alert.y + 25, alert.width - 2, 1), new Color32(176, 176, 176, 255));
            // 警告アイコンは図形で描き、フォントによる絵文字の見た目の違いを避ける。
            float cx = alert.x + 59, tip = alert.y + 48;
            for (int line = 0; line < 67; line++)
            {
                float half = 2f + line * 0.49f;
                PixelGui.Fill(new Rect(cx - half, tip + line + 2, half * 2, 1), new Color(0.45f, 0.36f, 0.06f, 0.25f));
                PixelGui.Fill(new Rect(cx - half, tip + line, half * 2, 1), new Color32(255, 218, 48, 255));
            }
            MacDesktopGui.Rounded(new Rect(cx - 4, tip + 17, 8, 29), Color.white, 3);
            MacDesktopGui.Rounded(new Rect(cx - 4, tip + 51, 8, 8), Color.white, 3);
            Label(new Rect(alert.x + 116, alert.y + 43, alert.width - 132, 32),
                "問題が発生したためシステム終了しました", 12, MacDesktopGui.Ink, TextAnchor.UpperLeft, true);
            Label(new Rect(alert.x + 116, alert.y + 78, alert.width - 140, 43),
                "詳細を確認してAppleにレポートを送信するには、“レポート”をクリックしてください。", 11, MacDesktopGui.Ink, TextAnchor.UpperLeft);
            Rect ignore = new Rect(alert.xMax - 203, alert.yMax - 46, 64, 25);
            Rect report = new Rect(alert.xMax - 123, alert.yMax - 46, 103, 25);
            // 増殖演出中の見た目用。実際の操作はR、または最後のリトライ選択で行う。
            foreach (Rect button in new[] { ignore, report })
            {
                MacDesktopGui.Rounded(button, new Color32(194, 194, 194, 255), 4);
                MacDesktopGui.Rounded(PixelGui.Inset(button, 1), Color.white, 3);
            }
            Label(ignore, "無視", 12, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
            Label(report, "レポート…", 12, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
            MacDesktopGui.Rounded(new Rect(alert.x + 23, alert.yMax - 43, 18, 18), Color.white, 8);
            Label(new Rect(alert.x + 23, alert.yMax - 43, 18, 18), "?", 12, MacDesktopGui.Ink, TextAnchor.MiddleCenter);
            GUI.matrix = alertMatrix;

        }
    }

    void DrawRestartMessage()
    {
        // OS標準に近い滑らかな文字で描く（ゲーム側のドットフォントは使わない）。
        Matrix4x4 matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        float scale = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.8f);
        float width = Mathf.Min(Screen.width - 40f, 780f * scale);
        float left = (Screen.width - width) * 0.5f;
        float top = Screen.height * 0.5f - 142f * scale;
        DrawPower(new Vector2(Screen.width * 0.5f, top + 45 * scale), 48 * scale);
        Label(new Rect(left, top + 126 * scale, width, 42 * scale), "問題が発生したためコンピュータを再起動しました。",
            Mathf.RoundToInt(20 * scale), Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(left, top + 174 * scale, width, 48 * scale), "起動を続けるには、何かキーを押すか、数秒待ってください。",
            Mathf.RoundToInt(16 * scale), Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(left, top + 237 * scale, width, 54 * scale),
            "Your computer restarted because of a problem.\nPress a key or wait a few seconds to continue starting up.",
            Mathf.RoundToInt(15 * scale), new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);
        GUI.matrix = matrix;
        if (!_choices) return;
        float w = PixelGui.Width, h = PixelGui.Height;
        Rect panel = new Rect((w - 304) * 0.5f, h - 76, 304, 60);
        MacDesktopGui.Rounded(panel, new Color32(35, 36, 39, 255), 6);
        Label(new Rect(panel.x, panel.y + 4, panel.width, 18), "このステージから再開しますか？", 11, Color.white, TextAnchor.MiddleCenter);
        if (MacDesktopGui.Button(new Rect(panel.x + 10, panel.y + 27, 136, 26), "リトライ", _focus == 0)) Choose(0);
        if (MacDesktopGui.Button(new Rect(panel.x + 158, panel.y + 27, 136, 26), "タイトルへ", _focus == 1)) Choose(1);
        if (BackupFx.Enabled) BackupFx.DrawDrive(panel.x + 20, panel.y + 33, 1, PixelGui.HpGreen); // リトライ＝バックアップから戻す、の印（2026-11-20。docs/backup-restore-design.md 5章）
    }

    static void DrawPower(Vector2 center, float radius)
    {
        Matrix4x4 saved = GUI.matrix;
        Color color = new Color(0.7f, 0.7f, 0.7f);
        // 上部の切れた輪と縦棒。記号フォントの有無に依存しない。
        for (int i = 0; i < 72; i++)
        {
            float angle = Mathf.Lerp(42f, 318f, i / 71f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;
            GUI.matrix = saved;
            GUIUtility.RotateAroundPivot(angle, pos);
            PixelGui.Fill(new Rect(pos.x - radius * 0.052f, pos.y - radius * 0.09f, radius * 0.104f, radius * 0.18f), color);
        }
        GUI.matrix = saved;
        PixelGui.Fill(new Rect(center.x - radius * 0.08f, center.y - radius * 1.12f, radius * 0.16f, radius * 1.12f), color);
    }

    static void Label(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
    {
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Hiragino Sans", "Arial" }, 16);
        var style = new GUIStyle(GUI.skin.label) { font = _font, fontSize = size, alignment = anchor,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = true };
        style.normal.textColor = PixelGui.ToGui(color);
        GUI.Label(rect, text, style);
    }
}
