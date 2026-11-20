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
    Vector2 _lastModePointer;
    bool _hasModePointer;
    AppDexMenu _dex;
    Team1.Achievements.AchievementListWindow _achievements; // 2026-11-20：メニューの「じっせき」から開く
    MacDesktopFiles _files;
    MacDesktopBoot _boot;
    bool _authBusy,_touchIdAvailable,_autoAuthStarted;
    bool _bgmStarted; // タイトルの曲を流し始めたか（起動の演出があるときは、ゲームのアプリが開いてから）
    string _touchIdStatus;
    static bool _bootPlayed;
    public static bool IsBootPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBoot() { _bootPlayed=false; IsBootPlaying=false; }

    bool AchievementsBlockInput => (_achievements != null && _achievements.BlocksParentInput) || Team1.Achievements.AchievementScanScreen.BlocksInput;

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void Start()
    {
        MacDesktopWallpaper.Ensure(this);
        _boot=new MacDesktopBoot(Time.unscaledTime,!_bootPlayed);
        _bootPlayed=true; IsBootPlaying=_boot.IsPlaying;
        // タイトルの曲：起動の演出（ロック画面→デスクトップ）の間は鳴らさず、Dock からゲームのアプリを開いたところから流す。
        // ランから戻ったとき（演出なし）は、今までどおりすぐ流す（2026-11-20 まさぴ〜）
        if(_boot.IsPlaying) GameAudio.StopBgm(0.3f);
        else StartTitleBgm();
        var touchId=MacTouchId.CheckAvailability();
        _touchIdAvailable=touchId==MacTouchId.Availability.Ready;
        _touchIdStatus=touchId==MacTouchId.Availability.NotEnrolled?"Touch IDが未設定です":
            touchId==MacTouchId.Availability.LockedOut?"Touch IDがロックされています":
            touchId==MacTouchId.Availability.PluginMissing?"Touch IDの準備ができていません":
            !_touchIdAvailable?"Enterで解除できます":null;
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
        // 起動の演出：ロック画面（Touch ID か Enter で解除）→ デスクトップで待つ → Dock のゲームを開く（2026-11-20）。
        // 9ac66e42 にあった処理がマージで消えてロック画面から進めなかったので戻し、デスクトップで待つようにした
        if(_boot!=null&&_boot.IsPlaying)
        {
            float now=Time.unscaledTime;
            _boot.Tick(now);
            IsBootPlaying=_boot.IsPlaying;
            if(_boot.ShowApp) StartTitleBgm(); // 窓が開き始めたら曲を流す
            var kb=Keyboard.current;
            if(_boot.Current==MacDesktopBoot.Step.Locked)
            {
                if(MenuInput.Confirm(false)) { CompleteDesktopUnlock(); return; } // Enter・Z・A・START
                if(_authBusy&&kb!=null&&kb.escapeKey.wasPressedThisFrame) { MacTouchId.Cancel(); return; }
                if(!_autoAuthStarted&&_touchIdAvailable&&_boot.CanUnlock(now))
                {
                    _autoAuthStarted=true;
                    AuthenticateTouchId();
                }
            }
            else if(_boot.Current==MacDesktopBoot.Step.Desktop&&MenuInput.Confirm()) LaunchGameApp(); // マウスが無くても開ける
            return;
        }
        if (PauseMenu.BlocksInput) return; // 設定（歯車）を開いている間は、タイトルを操作しない（2026-11-20）
        if (!_window.IsVisible || (_dex != null && _dex.BlocksParentInput) || (_files!=null&&_files.BlocksParentInput) || AchievementsBlockInput || Session.IsTransitioning) return;
        if(MacDesktopGui.WindowShortcuts(_window)) return;
        // キーボードとコントローラー（十字キー・左スティック・A／START で決定・B で戻る）。2026-11-20
        bool up = MenuInput.Up;
        bool down = MenuInput.Down;
        bool ok = MenuInput.Confirm();
        bool back = MenuInput.Back;
        if (_scan)
        {
            if (_scanning) return;
            if (back) ReturnToMenu();
            else if (ok) ScanPrimary();
            return;
        }
        int count = _page == Page.Menu ? 5 : ModeCount; // はじめる・アプリ図鑑・じっせき・終了・右下の歯車（設定）
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

    /// <summary>デスクトップの Dock から、このゲームのアプリを開く（アイコンが跳ねてから窓が開く）</summary>
    void LaunchGameApp()
    {
        if(_boot!=null&&_boot.Launch(Time.unscaledTime)) GameAudio.Event("ui_decide");
    }

    void StartTitleBgm()
    {
        if(_bgmStarted) return;
        _bgmStarted=true;
        GameAudio.PlayBgm("bgm_title"); // タイトルの曲（2026-11-20：画面を Mac 仕様にしたときに消えていたので戻した）
    }

    void CompleteDesktopUnlock()
    {
        if(_boot==null||!_boot.CanUnlock(Time.unscaledTime)) return;
        if(_authBusy) { _authBusy=false; MacTouchId.Cancel(); }
        _touchIdStatus=null;
        if(_boot.Unlock(Time.unscaledTime)) GameAudio.Event("ui_decide");
    }

    async void AuthenticateTouchId()
    {
        if(_authBusy||_boot==null||!_boot.CanUnlock(Time.unscaledTime)) return;
        _autoAuthStarted=true;
        if(_touchIdAvailable)
        {
            _authBusy=true; _touchIdStatus=null;
            MacTouchId.Result result;
            try { result=await MacTouchId.AuthenticateAsync(); }
            catch(System.Exception error)
            {
                if(this!=null) { _authBusy=false; _touchIdStatus="Touch IDを利用できません。Enterキーで解除"; }
                Debug.LogWarning("Touch ID: "+error.Message);
                return;
            }
            if(this==null) return;
            _authBusy=false;
            if(_boot==null||_boot.Current!=MacDesktopBoot.Step.Locked) return;
            if(result!=MacTouchId.Result.Success)
            {
                if(result==MacTouchId.Result.Unavailable)
                { _touchIdAvailable=false; _touchIdStatus="Enterキーで解除できます"; }
                else if(result==MacTouchId.Result.EmbeddedViewUnavailable)
                { _touchIdAvailable=false; _touchIdStatus="Touch IDを表示できません。Enterキーで解除"; }
                else _touchIdStatus="Enterキーで解除できます";
                return;
            }
        }
        CompleteDesktopUnlock();
    }

    void OnDestroy()
    {
        if(_authBusy) MacTouchId.Cancel();
        if(_boot!=null&&_boot.IsPlaying) IsBootPlaying=false;
    }

    void OpenPage(Page page)
    {
        _window.Open();
        _page = page;
        _focus = 0;
        _hasModePointer=false;
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
            else if (index == 4) PauseMenu.OpenSettings(); // 右下の歯車：音量・キー設定（2026-11-20。音は上の ui_decide）
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
        if (_detectedApps?.Count > 0 && _targets?.Count > 0) Begin(new ManualUsageProvider(_detectedApps));
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
        if(_boot!=null&&_boot.IsPlaying)
        {
            DrawBoot(w,h);
            PixelGui.End();
            return;
        }
        MacDesktopGui.MenuBar(w,"Virus Desktop");
        bool mode=_page==Page.Mode;
        bool scanWaiting=_scan&&_topApps==null;
        bool compactScan=_scan&&string.IsNullOrEmpty(_error)&&(_scanning||_topApps!=null);
        Rect win=MacDesktopGui.CenteredWindow(w,h,scanWaiting?480:_scan?500:mode?480:420,
            scanWaiting?(_scanning?268:286):_scan?(string.IsNullOrEmpty(_error)?268:306):mode?(string.IsNullOrEmpty(_error)?254:286):298, // メニューは「じっせき」の分だけ高い
            !_scan||compactScan);
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
        // 右下の歯車：設定（音量・キー設定）。キー・コントローラーでは、メニューの「終了」の下（2026-11-20）
        GUI.enabled=was&&active&&!PauseMenu.IsOpen;
        if(SettingsGear(new Rect(w-40,h-84,30,30),_window.IsVisible&&!_scan&&_page==Page.Menu&&_focus==4)) OpenSettings(); // どの画面（はじめかた・スキャン）からでも、その画面のまま設定を開く
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
        Rect inner=MacDesktopGui.Window(win,_scan?"端末スキャン":mode?"はじめかた":"Virus Desktop",out var action);
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
        float cx=inner.center.x;
        float y=inner.y+Mathf.Max(10f,(inner.height-210f)*0.5f);
        PixelGui.TextEn(new Rect(inner.x,y,inner.width,10),"~/Desktop",8,MacDesktopGui.Blue,TextAnchor.MiddleCenter);
        GUIStyle logo=PixelGui.Style(true,24,TextAnchor.MiddleCenter);
        PixelGui.ShadowText(new Rect(inner.x,y+16,inner.width,28),"VIRUS",logo,MacDesktopGui.Ink,new Color32(170,193,226,255),1);
        PixelGui.ShadowText(new Rect(inner.x,y+44,inner.width,28),"DESKTOP",logo,MacDesktopGui.Ink,new Color32(170,193,226,255),1);
        PixelGui.Text(new Rect(inner.x,y+80,inner.width,16),"このMacのデスクトップを取り戻せ",12,PixelGui.BodyText,TextAnchor.MiddleCenter);
        string[] items={"はじめる","アプリ図鑑","じっせき","終了"};
        for(int i=0;i<items.Length;i++)
        {
            Rect r=new Rect(cx-80,y+106+i*30,160,24);
            if(MenuItem(r,items[i],i==_focus)) { _focus=i; Choose(i); }
            else if(GUI.enabled&&r.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseMove) _focus=i;
        }
    }

    void DrawBoot(float width,float height)
    {
        float now=Time.unscaledTime;
        if(_boot.ShowDesktop)
        {
            // 起動途中にクリックが背後のメニューへ届かないようにする。
            // デスクトップで待つ間だけ、Dock のクリックを通す（ゲームのアイコンだけが反応する。2026-11-20）
            bool waiting=_boot.Current==MacDesktopBoot.Step.Desktop;
            EventType type=Event.current.type;
            bool mouse=type==EventType.MouseDown||type==EventType.MouseUp||type==EventType.MouseDrag||type==EventType.MouseMove||
                type==EventType.ScrollWheel;
            if((mouse&&!waiting)||type==EventType.KeyDown) Event.current.Use();
            MacDesktopGui.Folders(width); // フォルダ・ほかの Dock のアイコンは、ゲームのアプリを開くまで押しても何もしない
            MacDesktopGui.MenuBar(width,_boot.ShowApp?"Virus Desktop":"Finder");
            bool launched=_boot.Current>=MacDesktopBoot.Step.Bouncing;
            float hop=_boot.Current==MacDesktopBoot.Step.Bouncing?_boot.BounceProgress(now):_boot.IdleHop(now);
            int dock=MacDesktopGui.Dock(width,height,_dockApps,launched?1:-1,hop); // 起動するまで、ゲームの下の点は出さない
            if(waiting&&dock==1) LaunchGameApp();
            if(waiting&&mouse&&Event.current.type!=EventType.Used) Event.current.Use();
            if(_boot.ShowApp)
            {
                float t=MacDesktopBoot.Ease(_boot.LaunchProgress(now));
                Rect target=MacDesktopGui.CenteredWindow(width,height,420,298); // 本番のメニューの窓と同じ高さ（じっせきの分。窓が跳ねないように）
                float dockWidth=(2+_dockApps.Count)*42+12;
                Vector2 origin=new Vector2((width-dockWidth)*0.5f+26,height-65);
                Vector2 position=Vector2.Lerp(origin,target.position,t);
                Vector2 size=Vector2.Lerp(new Vector2(80,40),target.size,t);
                Matrix4x4 matrix=GUI.matrix;
                try
                {
                    GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(position.x,position.y,0),Quaternion.identity,
                        new Vector3(size.x/target.width,size.y/target.height,1))*
                        Matrix4x4.Translate(new Vector3(-target.x,-target.y,0));
                    Rect inner=MacDesktopGui.Window(target,"Virus Desktop",out _);
                    DrawMenu(inner);
                }
                finally { GUI.matrix=matrix; }
            }
        }
        _boot.DrawLock(now,_authBusy,_touchIdAvailable,_touchIdStatus);
    }

    void DrawModePanel(Rect inner)
    {
        bool canScan=MacScreenTime.CanScan;
        int count=canScan?2:1;
        float gap=12,cardWidth=Mathf.Min(214,(inner.width-16-gap*(count-1))/count);
        float start=inner.center.x-(cardWidth*count+gap*(count-1))*0.5f;
        // Runtime の OnGUI は MouseMove を受け取らないことがあるため、座標変化で追う。
        // 描画前に選択を更新し、左右両方に青枠が残らないようにする。
        Vector2 pointer=Event.current.mousePosition;
        if(GUI.enabled&&Mouse.current!=null&&(!_hasModePointer||(_lastModePointer-pointer).sqrMagnitude>0.01f))
        {
            _lastModePointer=pointer; _hasModePointer=true;
            for(int i=0;i<count;i++)
                if(new Rect(start+i*(cardWidth+gap),inner.y+4,cardWidth,148).Contains(pointer)) _focus=i;
            if(new Rect(inner.x+8,inner.yMax-29,80,26).Contains(pointer)) _focus=ModeCount-1;
        }
        for(int i=0;i<count;i++)
        {
            bool scan=canScan&&i==0;
            Rect card=new Rect(start+i*(cardWidth+gap),inner.y+4,cardWidth,148);
            bool hover=GUI.enabled&&card.Contains(Event.current.mousePosition);
            bool selected=i==_focus;
            MacDesktopGui.Rounded(new Rect(card.x,card.y+2,card.width,card.height),new Color(0,0,0,0.06f),6);
            MacDesktopGui.Rounded(card,selected?MacDesktopGui.Blue:MacDesktopGui.BorderColor,6);
            MacDesktopGui.Rounded(PixelGui.Inset(card,selected?2:1),selected?
                new Color32(236,244,255,255):hover?new Color32(243,246,250,255):Color.white,5);
            Rect icon=new Rect(card.center.x-19,card.y+15,38,34);
            DrawModeIcon(icon,scan);
            PixelGui.Text(new Rect(card.x+8,card.y+60,card.width-16,22),scan?"スキャン":"訓練モード",
                16,MacDesktopGui.Ink,TextAnchor.MiddleCenter);
            PixelGui.Text(new Rect(card.x+8,card.y+90,card.width-16,18),scan?"先週の使用履歴で遊ぶ":"サンプルで遊ぶ",
                12,MacDesktopGui.Muted,TextAnchor.MiddleCenter);
            PixelGui.Text(new Rect(card.x+8,card.y+115,card.width-16,16),scan?"使っているアプリが敵になる":"スキャン不要",
                12,MacDesktopGui.Muted,TextAnchor.MiddleCenter);
            if(GUI.Button(card,GUIContent.none,GUIStyle.none)) { _focus=i; Choose(i); }
        }
        if(!string.IsNullOrEmpty(_error))
            PixelGui.Text(new Rect(inner.x+8,inner.y+160,inner.width-16,36),_error,12,PixelGui.DarkRed,TextAnchor.UpperLeft,true);
        PixelGui.Fill(new Rect(inner.x+8,inner.yMax-39,inner.width-16,1),new Color32(221,226,234,255));
        if(MenuItem(new Rect(inner.x+8,inner.yMax-29,80,26),"もどる",_focus==ModeCount-1))
        { _focus=ModeCount-1; Choose(_focus); }
    }

    static void DrawModeIcon(Rect r,bool scan)
    {
        Color accent=scan?MacDesktopGui.Blue:new Color32(93,113,146,255);
        MacDesktopGui.Rounded(r,accent,4);
        PixelGui.Fill(new Rect(r.x+3,r.y+3,r.width-6,3),new Color(1,1,1,0.65f));
        Rect screen=new Rect(r.x+3,r.y+9,r.width-6,r.height-12);
        MacDesktopGui.Rounded(screen,Color.white,2);
        if(scan)
        {
            float x=screen.center.x-9,y=screen.center.y-6;
            for(int i=0;i<4;i++)
            {
                float cx=x+(i%2)*13,cy=y+(i/2)*8;
                PixelGui.Fill(new Rect(cx,cy,5,5),new Color32(203,224,255,255));
            }
            PixelGui.Fill(new Rect(x-2,screen.center.y,23,2),accent);
        }
        else
        {
            for(int i=0;i<9;i++)
                PixelGui.Fill(new Rect(screen.center.x-4+i,screen.center.y-6+i/2,1,13-i),accent);
        }
    }

    /// <summary>右下の歯車をマウスで押した：今の画面（ページ・選んでいる項目）はそのままで、設定を開く</summary>
    static void OpenSettings()
    {
        if(PauseMenu.IsOpen) return;
        GameAudio.Event("ui_decide");
        PauseMenu.OpenSettings();
    }

    static bool MenuItem(Rect r,string text,bool focused,TextAnchor anchor=TextAnchor.MiddleCenter)
        => MacDesktopGui.Button(r,text,false,focused);

    // 歯車のドット絵（16×16。# が線）
    static readonly string[] Gear =
    {
        "......####......",
        "......####......",
        "..##..####..##..",
        "..############..",
        "...##########...",
        "..####....####..",
        "#####......#####",
        "#####......#####",
        "#####......#####",
        "#####......#####",
        "..####....####..",
        "...##########...",
        "..############..",
        "..##..####..##..",
        "......####......",
        "......####......",
    };

    /// <summary>右下の歯車のボタン（設定）。押されたら true。focused：キー・コントローラーで選んでいる</summary>
    static bool SettingsGear(Rect r,bool focused)
    {
        bool hover=r.Contains(Event.current.mousePosition)&&GUI.enabled;
        bool blue=focused||hover;
        MacDesktopGui.Rounded(r,blue?new Color32(0,81,188,255):MacDesktopGui.BorderColor,6);
        MacDesktopGui.Rounded(PixelGui.Inset(r,1),blue?MacDesktopGui.Blue:new Color(1f,1f,1f,0.92f),5);
        Color ink=blue?Color.white:MacDesktopGui.Ink;
        float ox=r.x+Mathf.Floor((r.width-Gear[0].Length)*0.5f), oy=r.y+Mathf.Floor((r.height-Gear.Length)*0.5f);
        for(int y=0;y<Gear.Length;y++)
            for(int x=0;x<Gear[y].Length;x++)
                if(Gear[y][x]=='#') PixelGui.Fill(new Rect(ox+x,oy+y,1,1),ink);
        if(hover||focused) PixelGui.ShadowText(new Rect(r.x-52,r.y+7,46,16),"設定",PixelGui.Style(false,12,TextAnchor.MiddleRight),PixelGui.White,MacDesktopGui.Ink,1);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }

    void DrawScanPanel(GameSession session, Rect inner)
    {
        float x = inner.x + 14f, y = inner.y + 10f, w = inner.width - 28f;
        if (_topApps == null)
        {
            DrawModeIcon(new Rect(inner.center.x-19,inner.y+3,38,34),true);
            PixelGui.Text(new Rect(inner.x,inner.y+45,inner.width,24),"スクリーンタイムを確認",16,
                MacDesktopGui.Ink,TextAnchor.MiddleCenter);
            PixelGui.Text(new Rect(inner.x,inner.y+74,inner.width,18),"別ウィンドウで、次の操作をしてください",12,
                MacDesktopGui.Muted,TextAnchor.MiddleCenter);
            Rect steps=new Rect(inner.x+18,inner.y+101,inner.width-36,60);
            MacDesktopGui.Rounded(steps,new Color32(232,238,247,255),5);
            string[] instructions={"「このMac」「先週」を表示","「痕跡を読み取る」→「システム設定」を共有"};
            for(int i=0;i<instructions.Length;i++)
            {
                float row=steps.y+8+i*25;
                Rect number=new Rect(steps.x+11,row+1,16,16);
                MacDesktopGui.Rounded(number,MacDesktopGui.Blue,3);
                PixelGui.Text(number,(i+1).ToString(),12,Color.white,TextAnchor.MiddleCenter);
                PixelGui.TextFit(new Rect(steps.x+37,row,steps.width-47,18),instructions[i],12,MacDesktopGui.Ink);
            }
            if (_scanning)
            {
                // 実測の進捗率ではないため、完了率を連想させない待機アニメーション。
                int activeDot=Mathf.FloorToInt(Time.unscaledTime*3)%3;
                for(int i=0;i<3;i++)
                    MacDesktopGui.Rounded(new Rect(inner.center.x-14+i*12,inner.y+184,5,5),
                        i==activeDot?MacDesktopGui.Blue:MacDesktopGui.BorderColor,2);
            }
        }
        else
        {
            float top=inner.y+4;
            PixelGui.Text(new Rect(x,top,w,22),"スキャン完了",16,MacDesktopGui.Ink);
            PixelGui.TextEn(new Rect(x+w-60,top+4,60,14),"TOP 3",8,MacDesktopGui.Muted,TextAnchor.MiddleRight);
            Rect list=new Rect(x,top+30,w,_topApps.Count*32);
            MacDesktopGui.Rounded(list,MacDesktopGui.BorderColor,5);
            MacDesktopGui.Rounded(PixelGui.Inset(list,1),Color.white,4);
            for (int i = 0; i < _topApps.Count; i++)
            {
                var app = _topApps[i];
                Rect row=new Rect(list.x+1,list.y+i*32+1,list.width-2,31);
                if(i>0) PixelGui.Fill(new Rect(row.x+10,row.y,row.width-20,1),new Color32(232,236,242,255));
                PixelGui.Text(new Rect(row.x+7,row.y+8,16,16),(i+1).ToString(),12,MacDesktopGui.Muted,TextAnchor.MiddleCenter);
                AppVisual visual=AppVisuals.Get(new AppUsageRow { Name=app.Name,Seconds=app.Seconds });
                PixelGui.DrawSprite(new Rect(row.x+30,row.y+3,24,24),visual?.Icon24);
                PixelGui.TextFit(new Rect(row.x+64,row.y+7,row.width-140,18),app.Name,12,MacDesktopGui.Ink);
                PixelGui.Text(new Rect(row.xMax-70,row.y+7,60,18),Hours(app.Seconds),12,MacDesktopGui.Muted,TextAnchor.MiddleRight);
            }
            Rect targets=new Rect(x,top+136,w,34);
            MacDesktopGui.Rounded(targets,new Color32(232,238,247,255),4);
            if (_targets != null && _targets.Count > 0)
            {
                PixelGui.Text(new Rect(targets.x+10,targets.y+9,60,16),"駆除候補",12,MacDesktopGui.Muted);
                for (int i = 0; i < _targets.Count; i++)
                {
                    AppVisual v = AppVisuals.Get(_targets[i]);
                    PixelGui.DrawSprite(new Rect(targets.x+80+i*24,targets.y+9,16,16),v?.Icon16);
                }
            }
            else PixelGui.Text(new Rect(targets.x+10,targets.y+8,targets.width-20,18),"駆除候補が見つかりません",12,MacDesktopGui.Muted);
        }
        if (!string.IsNullOrEmpty(_error)) PixelGui.Text(new Rect(x, inner.yMax - 76f, w, 32f), _error, 12, PixelGui.DarkRed, TextAnchor.UpperLeft, true);

        if (_scanning) return;
        float by = inner.yMax - 30f;
        PixelGui.Fill(new Rect(x,by-8,w,1),new Color32(221,226,234,255));
        if (MacDesktopGui.Button(new Rect(x, by, 90f, 24f), "もどる")) ReturnToMenu();
        bool ready=_detectedApps?.Count>0&&_targets?.Count>0;
        if (MacDesktopGui.Button(new Rect(inner.xMax - 14f - 140f, by, 140f, 24f),ready?"相棒を選ぶ ▶":"再スキャン",true))
            ScanPrimary();
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
                Session.RememberUsages(usages,false);
                _topApps = topApps;
                _targets = usages.Where(app => !IsExcluded(Session, app)).OrderByDescending(app => app.UsageSeconds).Take(10)
                    .Select(app => new AppUsageRow { Name = app.DisplayName, BundleId = app.Id, Seconds = app.UsageSeconds }).ToList();
                // 結果の上位3件と駆除候補のアイコンを先に読む。
                AppVisuals.Prepare(_targets.Concat(_topApps.Select(a=>new AppUsageRow { Name=a.Name,Seconds=a.Seconds })));
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
