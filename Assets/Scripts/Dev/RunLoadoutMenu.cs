using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Mac風のウィンドウで相棒を選ぶ。選択・出撃・戻る操作は本編のラン状態につなぐ。</summary>
public class RunLoadoutMenu : MonoBehaviour
{
    [Tooltip("このシーンを直接再生したときに使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;
    [Tooltip("このシーンを直接再生したときに使う、カテゴリごとの能力の表")]
    [SerializeField] AbilityCatalog _abilities;

    static readonly string[] Ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };

    int _selected; // 選んでいるカード
    int _footer = -1; // キー・コントローラーで選んでいる下のボタン（-1 = カードの列、0 もどる・1 図鑑・2 出撃）。2026-11-20
    readonly MacDesktopGui.WindowState _window = new MacDesktopGui.WindowState();
    AppDexMenu _dex;
    MacDesktopFiles _files;

    GameSession Session => GameSession.Instance;
    IReadOnlyList<AbilityCandidate> Cards => Session != null && Session.Run != null ? Session.Run.Candidates : (IReadOnlyList<AbilityCandidate>)System.Array.Empty<AbilityCandidate>();

    /// <summary>選んでいるカードの番号（0 から）</summary>
    public int Selected => _selected;
    public bool CanConfirm => Session != null && !Session.IsTransitioning && _selected >= 0 && _selected < Cards.Count && Cards[_selected].Skill != null;

    void Start()
    {
        MacDesktopWallpaper.Ensure(this);
        _files=GetComponent<MacDesktopFiles>();
        if(_files==null) _files=gameObject.AddComponent<MacDesktopFiles>();
        _dex=FindObjectOfType<AppDexMenu>();
        if(_dex==null) _dex=gameObject.AddComponent<AppDexMenu>();
        _dex.ShowButton=false; _dex.HandleMinimizedExternally=true;
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
        if (!_window.IsVisible || (_dex!=null&&_dex.BlocksParentInput) || (_files!=null&&_files.BlocksParentInput) || Cards.Count == 0) return;
        if(MacDesktopGui.WindowShortcuts(_window)) return;
        // キーボードとコントローラー（十字キー・左スティックで選ぶ・A／START で決定・B で戻る）。2026-11-20
        // ↓ で下のボタンの列（もどる・図鑑・出撃）へ、↑ でカードの列へ戻る
        if (_footer >= 0)
        {
            int before = _footer;
            if (MenuInput.Left) _footer = Mathf.Max(0, _footer - 1);
            if (MenuInput.Right) _footer = Mathf.Min(2, _footer + 1);
            if (MenuInput.Up) _footer = -1;
            if (_footer != before) GameAudio.Event("ui_move");
            if (_footer >= 0 && MenuInput.Confirm()) { PressFooter(_footer); return; }
            if (MenuInput.Back) Back();
            return;
        }
        if (MenuInput.Left) Select(Mathf.Max(0, _selected - 1));
        if (MenuInput.Right) Select(Mathf.Min(Cards.Count - 1, _selected + 1));
        if (MenuInput.Down) { _footer = 2; GameAudio.Event("ui_move"); return; }
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
        for (int i = 0; i < keys.Length; i++)
        {
            if (kb != null && (kb[keys[i]].wasPressedThisFrame || kb[pad[i]].wasPressedThisFrame)) Select(i);
        }
        if (MenuInput.Confirm()) Confirm();
        if (MenuInput.Back) Back();
    }

    /// <summary>下のボタンを押す（キー・コントローラーから）。0 もどる・1 図鑑・2 出撃</summary>
    void PressFooter(int index)
    {
        if (index == 0) Back();
        else if (index == 1 && _dex != null) { GameAudio.Event("ui_decide"); _dex.Open(); }
        else if (index == 2) Confirm();
    }

    /// <summary>キー・コントローラーで選んでいるボタンの枠（マウスで押すときは出さない）</summary>
    void FooterFocus(Rect r, int index)
    {
        if (_footer != index || Event.current.type != EventType.Repaint) return;
        PixelGui.Border(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), MacDesktopGui.Blue, 2f);
    }

    void OnGUI()
    {
        if(Session==null||Session.Run==null||Session.IsTransitioning) return;
        PixelGui.Begin(false);
        float w=PixelGui.Width,h=PixelGui.Height;
        MacDesktopGui.MenuBar(w,"Virus Desktop");
        Rect win=MacDesktopGui.CenteredWindow(w,h,584,306,false);
        bool front=(_dex==null||!_dex.IsOpen)&&(_files==null||!_files.BlocksParentInput);
        win=_window.Apply(win,w,h,front);
        bool folderEnabled=GUI.enabled;
        GUI.enabled=folderEnabled&&front&&(!_window.IsVisible||!win.Contains(Event.current.mousePosition));
        int folder=MacDesktopGui.Folders(w);
        GUI.enabled=folderEnabled;
        if(folder>=0&&_files!=null) { _files.Open(folder); front=false; }
        var apps=new List<MacDesktopGui.DockItem>();
        for(int i=0;i<Mathf.Min(Cards.Count,3);i++)
            apps.Add(new MacDesktopGui.DockItem(Cards[i].IsFiller?(Cards[i].Skill!=null?Cards[i].Skill.DisplayName:"能力"):Cards[i].AppName,
                Cards[i].AppIcon!=null?Cards[i].AppIcon:Cards[i].Skill!=null?Cards[i].Skill.Icon:null));
        if(_window.IsMinimized) apps.Add(new MacDesktopGui.DockItem("相棒を選ぶ",null,_window));
        if(_dex!=null&&_dex.IsMinimized) apps.Add(new MacDesktopGui.DockItem("アプリ図鑑",null,_dex.Window));
        if(_files!=null&&_files.IsMinimized) apps.Add(new MacDesktopGui.DockItem("Finder",null,_files.Window));
        bool mainEnabled=GUI.enabled;
        GUI.enabled=mainEnabled&&front&&(!_window.IsVisible||!win.Contains(Event.current.mousePosition));
        int dock=MacDesktopGui.Dock(w,h,apps,_selected+2);
        if(dock==0) Back();
        else if(dock==1) _window.Open();
        else if(dock>=2&&dock-2<apps.Count)
        {
            if(apps[dock-2].Window!=null) apps[dock-2].Window.Open();
            else { Select(dock-2); _window.Open(); }
        }
        GUI.enabled=mainEnabled&&front;
        if(!_window.IsVisible) { GUI.enabled=mainEnabled; PixelGui.End(); return; }
        Rect inner=MacDesktopGui.Window(win,"相棒を選ぶ",out var action);
        _window.Handle(action);
        if(!_window.IsVisible) { GUI.enabled=mainEnabled; PixelGui.End(); return; }
        float x=inner.x,y=inner.y;
        PixelGui.Text(new Rect(x,y,inner.width,20),$"よく使うアプリ TOP{Mathf.Min(3,Cards.Count)} から、相棒を1つ選ぼう",16,MacDesktopGui.Ink);
        PixelGui.Text(new Rect(x,y+23,inner.width,16),Session.IsSampleRun?
            "訓練モード：サンプルのアプリから、最初に使う能力を選びます。":
            "選んだ相棒の能力を最初から装備して、デスクトップの駆除へ。",12,MacDesktopGui.Muted);
        int count=Mathf.Min(Cards.Count,3);
        float gap=10f,cardW=Mathf.Floor((inner.width-gap*(count-1))/Mathf.Max(1,count));
        float cardY=y+44f,cardH=176f;
        double maxSeconds=1;
        for(int i=0;i<count;i++) maxSeconds=System.Math.Max(maxSeconds,Cards[i].Seconds);
        for(int i=0;i<count;i++)
        {
            Rect card=new Rect(x+i*(cardW+gap),cardY,cardW,cardH);
            DrawCard(card,i,Cards[i],i==_selected,maxSeconds);
            if(GUI.Button(card,GUIContent.none,GUIStyle.none)) { Select(i); _footer=-1; }
        }
        float footer=inner.yMax-26;
        Rect backRect=new Rect(x,footer,76,26), dexRect=new Rect(x+280,footer,80,26), goRect=new Rect(inner.xMax-180,footer,180,26);
        FooterFocus(backRect,0); FooterFocus(dexRect,1); FooterFocus(goRect,2);
        if(MacDesktopGui.Button(backRect,"もどる",false,_footer==0)) Back();
        PixelGui.Text(new Rect(x+88,footer+4,180,18),"←→↓ 選択　Enter 決定",12,MacDesktopGui.Muted);
        if(MacDesktopGui.Button(dexRect,"図鑑",false,_footer==1)&&_dex!=null) _dex.Open();
        string appName=CanConfirm?(Cards[_selected].IsFiller?Cards[_selected].Skill.DisplayName:Cards[_selected].AppName):"";
        string label=$"{PixelGui.Fit(appName,PixelGui.Style(false,12),110)} と出撃";
        bool was=GUI.enabled; GUI.enabled=was&&CanConfirm;
        if(MacDesktopGui.Button(goRect,label,true)) Confirm();
        GUI.enabled=mainEnabled;
        PixelGui.End();
    }

    // カードの中の並び（docs/ui-text-and-dex-design.md 1-5 の図。r.y からのずれ）
    void DrawCard(Rect r, int index, AbilityCandidate c, bool selected, double maxSeconds)
    {
        MacDesktopGui.Card(r, selected);
        float x = r.x + 10f, w = r.width - 20f;
        float y = r.y + 8f;
        Color accent = selected ? MacDesktopGui.Blue : MacDesktopGui.Muted;
        PixelGui.TextEn(new Rect(x, y + 2f, 60f, 10f), index < Ranks.Length ? Ranks[index] : $"{index + 1}TH", 8, accent);
        if(selected) PixelGui.Text(new Rect(r.xMax-65,y,55,16),"選択中",12,MacDesktopGui.Blue,TextAnchor.MiddleRight);

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

        // 固有能力
        y += 50f;
        PixelGui.DottedLine(x, y, w, PixelGui.Muted);
        y += 6f;
        PixelGui.Text(new Rect(x, y, w, 14f), "固有能力", 12, MacDesktopGui.Blue);
        if (c.Skill != null)
        {
            PixelGui.DrawSprite(new Rect(x, y + 16f, 16f, 16f), c.Skill.Icon != null ? c.Skill.Icon : c.AppIcon); // アイコンが空の能力は、アプリのアイコンで
            PixelGui.TextFit(new Rect(x + 20f, y + 15f, w - 20f, 18f), c.Skill.DisplayName, 16, PixelGui.Ink);
            PixelGui.Text(new Rect(x, y + 36f, w, 30f), SkillTexts.Description(c.Skill), 12, PixelGui.BodyText, TextAnchor.UpperLeft, true);
            float tx = x;
            foreach(string tag in SkillTexts.Tags(c.Skill))
            {
                float tw=Mathf.Ceil(PixelGui.Measure(tag,PixelGui.Style(false,12)))+12;
                if(tx+tw>r.xMax-8) break;
                Rect badge=new Rect(tx,r.yMax-23,tw,16);
                MacDesktopGui.Rounded(badge,new Color32(231,240,253,255),3);
                PixelGui.Text(badge,tag,12,MacDesktopGui.Blue,TextAnchor.MiddleCenter);
                tx+=tw+4;
            }
        }
    }

}
