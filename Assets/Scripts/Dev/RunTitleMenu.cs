using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】タイトル画面：使用データの読み込み方を選んでランを始める（仕様書 8.3）。UI 担当の本番画面ができたら消す。
/// スクリーンタイムのスキャン、またはサンプルでの訓練からランを始める。
/// 2026-11-18：デザイン担当の画面イメージの色と並びに合わせた（描き方だけ。スキャンの流れはそのまま）。
/// ・壁紙の上にウィンドウ。ロゴ「VIRUS DESKTOP」（マゼンタの影）・仮タイトル
/// ・2026-11-19：ウィンドウを Mac の形にした（docs/mac-look-design.md 6章の 2。左上に信号機、題名はまん中で「~/Desktop/Infected.app」）。
///   信号機は飾り（前の右上の × と同じく押せない）。並び・中身は前のまま
/// ・メニュー：はじめる・ずかん・おわる（せっていは中身が無いので置かない。2026-11-18 まさぴ〜決定）。↑↓ で選び、Z か Enter で決定
/// ・はじめる → 「端末をスキャン」か「訓練モード」を選ぶ → スキャンなら今までどおりの読み取りと結果
/// ・下のバーに「PRESS Z / ENTER」と版
/// </summary>
public class RunTitleMenu : MonoBehaviour
{
    [Tooltip("GameSession が無いとき（このシーンを最初に開いたとき）に使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;

    enum Page { Menu, Mode }

    bool _scan;
    bool _scanning;
    List<AppUsage> _detectedApps;
    List<(string Name, int Seconds)> _topApps;
    string _error;
    Vector2 _scroll;
    Page _page = Page.Menu;
    int _focus;
    AppDexMenu _dex;

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void Start()
    {
        // 図鑑はメニューの「ずかん」から開く（右上のボタンは出さない）
        _dex = FindObjectOfType<AppDexMenu>();
        if (_dex == null) _dex = gameObject.AddComponent<AppDexMenu>();
        _dex.ShowButton = false;
        GameAudio.PlayBgm("bgm_title");
    }

    // ───────── キー ─────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || (_dex != null && _dex.IsOpen) || Session.IsTransitioning) return;
        bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
        bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        bool ok = kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool back = kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
        if (_scan)
        {
            if (_scanning) return;
            if (back) ReturnToMenu();
            else if (ok) ScanPrimary();
            return;
        }
        int count = _page == Page.Menu ? 3 : ModeCount;
        if (up) _focus = (_focus + count - 1) % count;
        if (down) _focus = (_focus + 1) % count;
        if (up || down) GameAudio.Event("ui_move");
        if (ok) Choose(_focus);
        if (back && _page == Page.Mode)
        {
            GameAudio.Event("ui_cancel");
            OpenPage(Page.Menu);
        }
    }

    int ModeCount => MacScreenTime.CanScan ? 3 : 2;

    void OpenPage(Page page)
    {
        _page = page;
        _focus = 0;
        _error = null;
    }

    void Choose(int index)
    {
        GameAudio.Event(_page == Page.Mode && index == ModeCount - 1 ? "ui_cancel" : "ui_decide"); // はじめるときは ui_start（Begin）が勝つ
        if (_page == Page.Menu)
        {
            if (index == 0) OpenPage(Page.Mode);
            else if (index == 1 && _dex != null) _dex.Open();
            else if (index == 2) Quit();
            return;
        }
        // はじめかた：スキャン（できるとき）・訓練モード・もどる
        bool canScan = MacScreenTime.CanScan;
        if (canScan && index == 0)
        {
            _scan = true;
            Scan(Session.Catalog);
        }
        else if (index == (canScan ? 1 : 0)) Begin(new SampleUsageProvider());
        else OpenPage(Page.Menu);
    }

    void ScanPrimary()
    {
        GameAudio.Event("ui_decide");
        if (_detectedApps?.Count > 0) Begin(new ManualUsageProvider(_detectedApps));
        else Scan(Session.Catalog);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────── 描く ─────────

    void OnGUI()
    {
        GameSession session = Session;
        if (session.IsTransitioning) return;
        PixelGui.Begin();
        float W = PixelGui.Width, H = PixelGui.Height;
        float ox = Mathf.Round((W - 640f) * 0.5f), oy = Mathf.Round(Mathf.Max(0f, (H - 360f) * 0.5f));
        float barY = PixelGui.BottomBar(34f);
        PixelGui.TextEn(new Rect(12f, barY + 13f, 200f, 10f), "PRESS Z / ENTER", 8, PixelGui.White);
        PixelGui.Text(new Rect(W - 172f, barY + 9f, 160f, 16f), "prototype ver 0.1", 12, PixelGui.White, TextAnchor.MiddleRight);

        bool wide = _scan;
        var win = wide
            ? new Rect(Mathf.Round((W - 540f) * 0.5f), oy + 10f, 540f, Mathf.Min(306f, barY - oy - 20f))
            : new Rect(ox + 112f, oy + 58f, 420f, Mathf.Min(256f, barY - oy - 66f));
        if (!wide) DrawDecoration(win); // 広いウィンドウ（スキャン）のときは後ろがほとんど隠れるので描かない
        // 題名は Mac の書き方（前は C:\DESKTOP\INFECTED.EXE・START.EXE・USAGE_SCAN.EXE）。図鑑を開いている間は後ろのウィンドウ（信号機が灰）
        string title = _scan ? "Usage Scan" : _page == Page.Mode ? "Start" : "~/Desktop/Infected.app";
        Rect inner = PixelGui.Window(win, title, PixelGui.LabelPurple, _dex == null || !_dex.IsOpen);

        if (_scan) DrawScanPanel(session, inner);
        else if (_page == Page.Mode) DrawModePanel(inner);
        else DrawMenu(inner);
        PixelGui.End();
    }

    // 後ろのウィンドウ・その上に立つプレイヤー・ウイルス（飾り）
    void DrawDecoration(Rect win)
    {
        var back = new Rect(win.x - 82f, win.y - 10f, 280f, 200f);
        PixelGui.Window(back, null, PixelGui.Ink, false); // 後ろのウィンドウ（信号機は灰）
        PixelGui.Fill(new Rect(back.x + 9f, back.yMax - 30f, back.width - 18f, 21f), PixelGui.Frame);
        GameArt art = GameArt.Instance;
        if (art == null) return;
        PixelGui.DrawPlayer(back.x + 60f, back.y);
        if (art.ZakoWalk != null && art.ZakoWalk.Length > 0)
        {
            Sprite z = art.ZakoWalk[Mathf.FloorToInt(Time.unscaledTime * 6f) % art.ZakoWalk.Length];
            Rect zr = z.textureRect;
            float zw = zr.width * 2f, zh = zr.height * 2f;
            PixelGui.DrawSprite(new Rect(back.x - zw * 0.3f, back.yMax - zh * 0.6f, zw, zh), z, new Color(0.75f, 0.75f, 0.75f));
            PixelGui.DrawSprite(new Rect(win.xMax - zw * 0.5f, win.yMax - zh * 0.5f, zw, zh), z, new Color(1f, 0.55f, 1f));
            // マゼンタのかけら（ウイルスから散る）
            for (int i = 0; i < 4; i++)
            {
                float t = (Time.unscaledTime * 0.7f + i * 0.25f) % 1f;
                PixelGui.Fill(new Rect(win.xMax + zw * 0.4f + i * 5f, win.yMax - zh * 0.4f - t * 40f, 3f, 3f), new Color(0.93f, 0.22f, 1f, 1f - t));
            }
        }
    }

    void DrawMenu(Rect inner)
    {
        float cx = inner.center.x;
        float y = inner.y + 10f; // 題名（前はここに C:\DESKTOP\INFECTED.EXE）は、タイトルバーのまん中へ移した
        GUIStyle logo = PixelGui.Style(true, 24, TextAnchor.MiddleCenter);
        PixelGui.ShadowText(new Rect(inner.x, y + 16f, inner.width, 28f), "VIRUS", logo, PixelGui.Ink, PixelGui.Magenta, 2f);
        PixelGui.ShadowText(new Rect(inner.x, y + 44f, inner.width, 28f), "DESKTOP", logo, PixelGui.Ink, PixelGui.Magenta, 2f);
        PixelGui.Text(new Rect(inner.x, y + 80f, inner.width, 16f), "デスクトップを取り戻せ（仮タイトル）", 12, PixelGui.BodyText, TextAnchor.MiddleCenter);
        string[] items = { "はじめる", "ずかん", "おわる" };
        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rect(cx - 80f, y + 106f + i * 30f, 160f, 24f);
            if (MenuItem(r, items[i], i == _focus)) { _focus = i; Choose(i); }
            else if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseMove) _focus = i;
        }
    }

    void DrawModePanel(Rect inner)
    {
        float x = inner.x + 14f, y = inner.y + 10f, w = inner.width - 28f; // 題名「Start」はタイトルバー
        PixelGui.Text(new Rect(x, y + 14f, w, 20f), "はじめかたを選ぶ", 16, PixelGui.Ink);
        bool canScan = MacScreenTime.CanScan;
        var items = new List<(string label, string note)>();
        if (canScan) items.Add(("端末をスキャン", "先週のスクリーンタイムから、ボスになるアプリを見つける。共有したウィンドウを読み取り、画像は保存しない。"));
        items.Add(("訓練モード", "サンプルの使用データで、すぐに遊べる。"));
        items.Add(("◀ もどる", ""));
        float iy = y + 42f;
        GUIStyle noteStyle = PixelGui.Style(false, 12, TextAnchor.UpperLeft, true);
        for (int i = 0; i < items.Count; i++)
        {
            var r = new Rect(x, iy, 170f, 24f);
            if (MenuItem(r, items[i].label, i == _focus, TextAnchor.MiddleLeft)) { _focus = i; Choose(i); }
            iy += 28f;
            if (string.IsNullOrEmpty(items[i].note)) continue;
            float nh = Mathf.Ceil(noteStyle.CalcHeight(new GUIContent(items[i].note), w - 22f));
            PixelGui.Text(new Rect(x + 22f, iy, w - 22f, nh), items[i].note, 12, PixelGui.SubText, TextAnchor.UpperLeft, true);
            iy += nh + 8f;
        }
        if (!string.IsNullOrEmpty(_error)) PixelGui.Text(new Rect(x, inner.yMax - 40f, w, 32f), _error, 12, PixelGui.DarkRed, TextAnchor.UpperLeft, true);
    }

    // メニューの1行：選んでいる行はオレンジの帯に「▶」
    static bool MenuItem(Rect r, string text, bool focused, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        if (focused || hover)
        {
            PixelGui.Fill(r, PixelGui.DarkRed);
            PixelGui.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 3f, r.height - 3f), PixelGui.Orange);
            string t = text.StartsWith("◀") ? text : "▶ " + text;
            PixelGui.Text(new Rect(r.x + (anchor == TextAnchor.MiddleLeft ? 8f : 0f), r.y - 1f, r.width - 8f, r.height), t, 12, PixelGui.White, anchor);
        }
        else
        {
            PixelGui.Text(new Rect(r.x + (anchor == TextAnchor.MiddleLeft ? 22f : 0f), r.y - 1f, r.width - 8f, r.height), text, 12, PixelGui.Ink, anchor);
        }
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void DrawScanPanel(GameSession session, Rect inner)
    {
        float x = inner.x + 14f, y = inner.y + 10f, w = inner.width - 28f; // 題名「Usage Scan」はタイトルバー
        if (_topApps == null)
        {
            PixelGui.Text(new Rect(x, y + 14f, w, 20f), _scanning ? "スクリーンタイムを確認中" : "端末をスキャン", 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x, y + 44f, w, 30f), "1. システム設定のスクリーンタイムで、このMacと「先週」を選びます。", 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);
            PixelGui.Text(new Rect(x, y + 76f, w, 30f), "2. 案内パネルの「痕跡を読み取る」を押し、共有画面で「システム設定」を選びます。", 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);
            if (_scanning)
            {
                // 読み取り中：メーターを流す
                float t = Time.unscaledTime % 1.2f / 1.2f;
                PixelGui.Meter(new Rect(x, y + 116f, 200f, 6f), 10, t, PixelGui.Orange, PixelGui.Muted);
            }
        }
        else
        {
            PixelGui.Text(new Rect(x, y + 14f, w, 20f), "スキャン結果", 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x, y + 36f, w, 16f), "使用時間の上位3アプリ", 12, PixelGui.SubText);
            float ry = y + 56f;
            for (int i = 0; i < _topApps.Count; i++)
            {
                var app = _topApps[i];
                int minutes = app.Seconds / 60;
                string duration = minutes >= 60 ? $"{minutes / 60}時間 {minutes % 60}分" : minutes > 0 ? $"{minutes}分" : $"{app.Seconds}秒";
                var row = new Rect(x, ry, w, 30f);
                PixelGui.WhiteBox(row);
                PixelGui.Chip(new Rect(row.x + 6f, row.y + 6f, 18f, 16f), $"{i + 1}");
                PixelGui.TextFit(new Rect(row.x + 32f, row.y + 5f, row.width - 150f, 16f), app.Name, 12, PixelGui.Ink);
                PixelGui.Text(new Rect(row.xMax - 110f, row.y + 5f, 100f, 16f), duration, 12, PixelGui.Ink, TextAnchor.MiddleRight);
                PixelGui.Fill(new Rect(row.x + 32f, row.yMax - 6f, (row.width - 44f) * app.Seconds / Mathf.Max(1, _topApps[0].Seconds), 2f), PixelGui.Orange);
                ry += 34f;
            }
            // 2026-11-17（まさぴ〜）：上位10アプリが、ボスの候補（この中からランダムに3つ）
            string targets = _detectedApps.Count > 0
                ? $"ボスの候補　{string.Join("、", _detectedApps.Where(app => !IsExcluded(session, app)).OrderByDescending(app => app.UsageSeconds).Take(10).Select(app => app.DisplayName))}"
                : "駆除対象のアプリは見つかりませんでした。";
            PixelGui.Text(new Rect(x, ry + 2f, w, 44f), targets, 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);
            if (_detectedApps.Count > 0)
                PixelGui.Text(new Rect(x, ry + 46f, w, 16f), "利用時間は、ボスの強さに反映されます。", 12, PixelGui.SubText);
        }
        if (!string.IsNullOrEmpty(_error)) PixelGui.Text(new Rect(x, inner.yMax - 62f, w, 30f), _error, 12, PixelGui.DarkRed, TextAnchor.UpperLeft, true);

        if (_scanning) return;
        float by = inner.yMax - 30f;
        if (PixelGui.SecondaryButton(new Rect(x, by, 90f, 24f), "◀ もどる")) ReturnToMenu();
        string primary = _detectedApps?.Count > 0 ? "駆除を開始 ▶" : "もう一度スキャン";
        if (PixelGui.PrimaryButton(new Rect(inner.xMax - 14f - 140f, by, 140f, 24f), primary, true)) ScanPrimary();
    }

    void ReturnToMenu()
    {
        GameAudio.Event("ui_cancel");
        _scan = false;
        _error = null;
        _detectedApps = null;
        _topApps = null;
        _scroll = Vector2.zero;
    }

    // 2026-11-17（まさぴ〜）：ゲームがボスにしないアプリ（Finder・設定・iPhone のアプリなど）は、候補に出さない
    static bool IsExcluded(GameSession session, AppUsage app) => session.Abilities != null &&
        session.Abilities.IsExcluded(new AppUsageRow { Name = app.DisplayName, BundleId = app.Id, Seconds = app.UsageSeconds });

    async void Scan(AppCatalog catalog)
    {
        _scanning = true;
        _error = null;
        _detectedApps = null;
        _topApps = null;
        try
        {
            var result = await MacScreenTime.ScanAsync();
            if (this == null || result.ExitCode == 2) return;
            if (result.ExitCode != 0)
            {
                Debug.LogWarning($"スクリーンタイムを読み取れませんでした。\n{result.Error}");
                _error = "読み取りに失敗しました。共有するウィンドウに「システム設定」を選び、もう一度試してください。";
                return;
            }

            var usages = MacScreenTime.ReadUsages(result.Output, catalog, out var topApps);
            if (topApps.Count > 0)
            {
                _detectedApps = usages;
                _topApps = topApps;
            }
            else _error = "アプリ一覧を読み取れませんでした。スクリーンタイムでアプリ一覧を表示してから、もう一度試してください。";
        }
        catch (System.Exception error)
        {
            if (this == null) return;
            Debug.LogWarning($"端末をスキャンできませんでした。\n{error.Message}");
            _error = "端末をスキャンできませんでした。「戻る」から訓練モードを選べます。";
        }
        finally
        {
            _scanning = false;
        }
    }

    void Begin(IAppUsageProvider provider)
    {
        if (!Session.StartRun(provider, out string error)) _error = error;
        else GameAudio.Event("ui_start");
    }
}
