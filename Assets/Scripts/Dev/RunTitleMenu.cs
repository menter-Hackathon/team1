using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Mac風のデスクトップからスキャン・訓練・図鑑へ進むタイトル。</summary>
public class RunTitleMenu : MonoBehaviour
{
    [Tooltip("GameSession が無いとき（このシーンを最初に開いたとき）に使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;

    enum Page { Menu, Mode }

    bool _scan;
    bool _scanning;
    List<AppUsage> _detectedApps;
    List<(string Name, int Seconds)> _topApps;
    List<AppUsageRow> _targets; // ボスの候補（上位10アプリ。アイコンを並べる）
    string _error;
    readonly List<MacDesktopGui.DockItem> _dockApps = new List<MacDesktopGui.DockItem>();
    readonly MacDesktopGui.WindowState _window = new MacDesktopGui.WindowState();
    Page _page = Page.Menu;
    int _focus;
    AppDexMenu _dex;
    Team1.Achievements.AchievementListWindow _achievements; // 2026-11-20：メニューの「じっせき」から開く
    MacDesktopFiles _files;

    bool AchievementsBlockInput => (_achievements != null && _achievements.BlocksParentInput) || Team1.Achievements.AchievementScanScreen.BlocksInput;

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void Start()
    {
        MacDesktopWallpaper.Ensure(this);
        _files=GetComponent<MacDesktopFiles>();
        if(_files==null) _files=gameObject.AddComponent<MacDesktopFiles>();
        // 図鑑はメニューの「ずかん」から開く（右上のボタンは出さない）
        _dex = FindObjectOfType<AppDexMenu>();
        if (_dex == null) _dex = gameObject.AddComponent<AppDexMenu>();
        _dex.ShowButton = false;
        _dex.HandleMinimizedExternally = true;
        _achievements = FindObjectOfType<Team1.Achievements.AchievementListWindow>();
        if (_achievements == null) _achievements = gameObject.AddComponent<Team1.Achievements.AchievementListWindow>();
        if(Session.Catalog!=null) foreach(var app in Session.Catalog.Apps)
            if(app!=null&&app.Profile!=null&&_dockApps.Count<4)
                _dockApps.Add(new MacDesktopGui.DockItem(app.DisplayName,app.Profile.Icon));
    }

    // ───────── キー ─────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !_window.IsVisible || (_dex != null && _dex.BlocksParentInput) || (_files!=null&&_files.BlocksParentInput) || AchievementsBlockInput || Session.IsTransitioning) return;
        if(MacDesktopGui.WindowShortcuts(_window)) return;
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
        int count = _page == Page.Menu ? 4 : ModeCount;
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
        _window.Open();
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
            else if (index == 2 && _achievements != null) _achievements.Open();
            else if (index == 3) Quit();
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
        GameSession session=Session;
        if(session.IsTransitioning) return;
        PixelGui.Begin(false);
        float w=PixelGui.Width,h=PixelGui.Height;
        MacDesktopGui.MenuBar(w,"Virus Desktop");
        Rect win=MacDesktopGui.CenteredWindow(w,h,_scan?540:420,_scan?306:298,!_scan);
        bool front=(_dex==null||!_dex.IsOpen)&&(_files==null||!_files.BlocksParentInput)&&!AchievementsBlockInput;
        win=_window.Apply(win,w,h,front);
        bool folderEnabled=GUI.enabled;
        GUI.enabled=folderEnabled&&front&&(!_window.IsVisible||!win.Contains(Event.current.mousePosition));
        int folder=MacDesktopGui.Folders(w);
        GUI.enabled=folderEnabled;
        if(folder>=0&&_files!=null) { _files.Open(folder); front=false; }
        bool was=GUI.enabled;
        bool active=front;
        var items=new List<MacDesktopGui.DockItem>(_dockApps);
        if(_window.IsMinimized) items.Add(new MacDesktopGui.DockItem("Virus Desktop",null,_window));
        if(_dex!=null&&_dex.IsMinimized) items.Add(new MacDesktopGui.DockItem("アプリ図鑑",null,_dex.Window));
        if(_files!=null&&_files.IsMinimized) items.Add(new MacDesktopGui.DockItem("Finder",null,_files.Window));
        GUI.enabled=was&&active&&(!_window.IsVisible||!win.Contains(Event.current.mousePosition));
        int dock=MacDesktopGui.Dock(w,h,items);
        if(dock==0)
        {
            _window.Open();
            if(!_scanning) { if(_scan) ReturnToMenu(); OpenPage(Page.Menu); }
        }
        else if(dock==1)
        {
            bool hidden=!_window.IsVisible; _window.Open();
            if(!hidden&&!_scanning) { if(_scan) ReturnToMenu(); OpenPage(Page.Mode); }
        }
        else if(dock>=2&&dock-2<items.Count)
        {
            if(items[dock-2].Window!=null) items[dock-2].Window.Open();
            else if(_dex!=null) _dex.Open();
        }
        GUI.enabled=was&&front;
        if(!_window.IsVisible) { GUI.enabled=was; PixelGui.End(); return; }
        Rect inner=MacDesktopGui.Window(win,_scan?"スクリーンタイム":"Virus Desktop",out var action);
        _window.Handle(action);
        if(!_window.IsVisible) { GUI.enabled=was; PixelGui.End(); return; }
        if(_scan) DrawScanPanel(session,inner);
        else if(_page==Page.Mode) DrawModePanel(inner);
        else DrawMenu(inner);
        GUI.enabled=was;
        PixelGui.End();
    }

    void DrawMenu(Rect inner)
    {
        float cx = inner.center.x;
        float y = inner.y + Mathf.Max(10f,(inner.height-210f)*0.5f);
        PixelGui.TextEn(new Rect(inner.x, y, inner.width, 10f), "~/Desktop", 8, MacDesktopGui.Blue, TextAnchor.MiddleCenter);
        GUIStyle logo = PixelGui.Style(true, 24, TextAnchor.MiddleCenter);
        PixelGui.ShadowText(new Rect(inner.x, y + 16f, inner.width, 28f), "VIRUS", logo, MacDesktopGui.Ink, new Color32(170,193,226,255), 1f);
        PixelGui.ShadowText(new Rect(inner.x, y + 44f, inner.width, 28f), "DESKTOP", logo, MacDesktopGui.Ink, new Color32(170,193,226,255), 1f);
        PixelGui.Text(new Rect(inner.x, y + 80f, inner.width, 16f), "このMacのデスクトップを取り戻せ", 12, PixelGui.BodyText, TextAnchor.MiddleCenter);
        string[] items = { "はじめる", "アプリ図鑑", "じっせき", "終了" };
        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rect(cx - 80f, y + 106f + i * 30f, 160f, 24f);
            if (MenuItem(r, items[i], i == _focus)) { _focus = i; Choose(i); }
            else if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseMove) _focus = i;
        }
    }

    void DrawModePanel(Rect inner)
    {
        float x = inner.x + 14f, y = inner.y + 10f, w = inner.width - 28f;
        PixelGui.TextEn(new Rect(x, y, 200f, 10f), "Virus Desktop.app", 8, MacDesktopGui.Blue);
        PixelGui.Text(new Rect(x, y + 14f, w, 20f), "はじめかたを選ぶ", 16, PixelGui.Ink);
        bool canScan = MacScreenTime.CanScan;
        var items = new List<(string label, string note)>();
        if (canScan) items.Add(("スキャン", "先週のスクリーンタイム・画像は保存しない"));
        items.Add(("訓練モード", "サンプルのデータ"));
        items.Add(("◀ もどる", ""));
        float iy = y + 16f;
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

    static bool MenuItem(Rect r,string text,bool focused,TextAnchor anchor=TextAnchor.MiddleCenter)
        => MacDesktopGui.Button(r,text,false,focused);

    void DrawScanPanel(GameSession session, Rect inner)
    {
        float x = inner.x + 14f, y = inner.y + 10f, w = inner.width - 28f;
        PixelGui.TextEn(new Rect(x, y, 200f, 10f), "Screen Time", 8, MacDesktopGui.Blue);
        if (_topApps == null)
        {
            // 2026-11-19：手順は短い1行ずつ（docs/ui-text-and-dex-design.md 1-3）
            PixelGui.Text(new Rect(x, y + 14f, w, 20f), _scanning ? "確認中" : "スキャン", 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x, y + 44f, w, 16f), "1. スクリーンタイムで「このMac」「先週」", 12, PixelGui.BodyText);
            PixelGui.Text(new Rect(x, y + 66f, w, 16f), "2. 「痕跡を読み取る」→「システム設定」を共有", 12, PixelGui.BodyText);
            if (_scanning)
            {
                // 読み取り中：メーターを流す
                float t = Time.unscaledTime % 1.2f / 1.2f;
                PixelGui.Meter(new Rect(x, y + 116f, 200f, 6f), 10, t, MacDesktopGui.Blue, PixelGui.Muted);
            }
        }
        else
        {
            // 2026-11-19：文字を減らした（docs/ui-text-and-dex-design.md 1-4）。時間は「12.5h」、ボスの候補はアイコンだけ
            PixelGui.Text(new Rect(x, y + 14f, w, 20f), "スキャン結果", 16, PixelGui.Ink);
            float ry = y + 40f;
            for (int i = 0; i < _topApps.Count; i++)
            {
                var app = _topApps[i];
                int minutes = app.Seconds / 60;
                string duration = minutes >= 60 ? $"{minutes / 60}時間 {minutes % 60}分" : minutes > 0 ? $"{minutes}分" : $"{app.Seconds}秒";
                var row = new Rect(x, ry, w, 28f);
                MacDesktopGui.Card(row,false);
                PixelGui.Text(new Rect(row.x+6,row.y+6,18,16),$"{i+1}",12,MacDesktopGui.Blue,TextAnchor.MiddleCenter);
                PixelGui.TextFit(new Rect(row.x + 32f, row.y + 5f, row.width - 150f, 16f), app.Name, 12, PixelGui.Ink);
                PixelGui.Text(new Rect(row.xMax - 110f, row.y + 5f, 100f, 16f), duration, 12, PixelGui.Ink, TextAnchor.MiddleRight);
                PixelGui.Fill(new Rect(row.x + 32f, row.yMax - 6f, (row.width - 44f) * app.Seconds / Mathf.Max(1, _topApps[0].Seconds), 2f), MacDesktopGui.Blue);
                ry += 30f;
            }
            // 2026-11-17（まさぴ〜）：上位10アプリが、ボスの候補（この中からランダムに3つ）。名前は出さず、アイコンを横に並べる
            if (_targets != null && _targets.Count > 0)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    AppVisual v = AppVisuals.Get(_targets[i]);
                    PixelGui.DrawSprite(new Rect(x + i * 20f, ry + 4f, 16f, 16f), v != null ? v.Icon16 : null);
                }
            }
            else PixelGui.Text(new Rect(x, ry + 2f, w, 16f), "アプリが見つかりません", 12, PixelGui.BodyText);
        }
        if (!string.IsNullOrEmpty(_error)) PixelGui.Text(new Rect(x, inner.yMax - 62f, w, 30f), _error, 12, PixelGui.DarkRed, TextAnchor.UpperLeft, true);

        if (_scanning) return;
        float by = inner.yMax - 30f;
        if (MacDesktopGui.Button(new Rect(x, by, 90f, 24f), "もどる")) ReturnToMenu();
        string primary = _detectedApps?.Count > 0 ? "駆除を開始 ▶" : "もう一度スキャン";
        if (MacDesktopGui.Button(new Rect(inner.xMax - 14f - 140f, by, 140f, 24f), primary, true)) ScanPrimary();
    }

    // 使用時間：1時間からは「12.5h」、それより短ければ「40m」（2026-11-19）
    static string Hours(int seconds)
    {
        if (seconds >= 3600) return $"{seconds / 3600.0:0.#}h";
        int minutes = seconds / 60;
        return minutes > 0 ? $"{minutes}m" : $"{seconds}s";
    }

    void ReturnToMenu()
    {
        GameAudio.Event("ui_cancel");
        _scan = false;
        _error = null;
        _detectedApps = null;
        _topApps = null;
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
        _targets = null;
        try
        {
            var result = await MacScreenTime.ScanAsync();
            if (this == null || result.ExitCode == 2) return;
            if (result.ExitCode != 0)
            {
                Debug.LogWarning($"スクリーンタイムを読み取れませんでした。\n{result.Error}");
                _error = "読み取れません。「システム設定」を共有して、もう一度";
                return;
            }

            var usages = MacScreenTime.ReadUsages(result.Output, catalog, out var topApps);
            if (topApps.Count > 0)
            {
                Team1.Achievements.AchievementService.Instance?.ReportScreenTimeScan(usages); // 実績「コンプリート」用：TOP10 のアプリ名だけ残す
                _detectedApps = usages;
                _topApps = topApps;
                _targets = usages.Where(app => !IsExcluded(Session, app)).OrderByDescending(app => app.UsageSeconds).Take(10)
                    .Select(app => new AppUsageRow { Name = app.DisplayName, BundleId = app.Id, Seconds = app.UsageSeconds }).ToList();
                AppVisuals.Prepare(_targets); // アイコンをまとめて読む（描くときに読みに行かない）
            }
            else _error = "アプリ一覧が見えません。一覧を出して、もう一度";
        }
        catch (System.Exception error)
        {
            if (this == null) return;
            Debug.LogWarning($"端末をスキャンできませんでした。\n{error.Message}");
            _error = "スキャンできません。もどる → 訓練モード";
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
