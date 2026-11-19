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
        if (kb == null || !_window.IsVisible || (_dex!=null&&_dex.BlocksParentInput) || (_files!=null&&_files.BlocksParentInput) || Cards.Count == 0) return;
        if(MacDesktopGui.WindowShortcuts(_window)) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Select(Mathf.Max(0, _selected - 1));
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Select(Mathf.Min(Cards.Count - 1, _selected + 1));
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
        for (int i = 0; i < keys.Length; i++)
        {
            if (kb[keys[i]].wasPressedThisFrame || kb[pad[i]].wasPressedThisFrame) Select(i);
        }
        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Confirm();
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Back();
    }

    void OnGUI()
    {
        if(Session==null||Session.Run==null||Session.IsTransitioning) return;
        PixelGui.Begin(false);
        float w=PixelGui.Width,h=PixelGui.Height;
        MacDesktopGui.MenuBar(w,"Virus Desktop");
        Rect win=MacDesktopGui.CenteredWindow(w,h,584,252);
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
        int count=Mathf.Min(Cards.Count,3);
        float gap=10f,cardW=Mathf.Floor((inner.width-gap*(count-1))/Mathf.Max(1,count));
        float cardY=y+4f,cardH=154f;
        double maxSeconds=1;
        for(int i=0;i<count;i++) maxSeconds=System.Math.Max(maxSeconds,Cards[i].Seconds);
        for(int i=0;i<count;i++)
        {
            Rect card=new Rect(x+i*(cardW+gap),cardY,cardW,cardH);
            DrawCard(card,i,Cards[i],i==_selected,maxSeconds);
            if(GUI.Button(card,GUIContent.none,GUIStyle.none)) Select(i);
        }
        float footer=inner.yMax-26;
        if(MacDesktopGui.Button(new Rect(x,footer,76,26),"もどる")) Back();
        if(MacDesktopGui.Button(new Rect(x+88,footer,80,26),"図鑑")&&_dex!=null) _dex.Open();
        bool was=GUI.enabled; GUI.enabled=was&&CanConfirm;
        if(MacDesktopGui.Button(new Rect(inner.xMax-180,footer,180,26),"出撃 ▶",true)) Confirm();
        GUI.enabled=mainEnabled;
        PixelGui.End();
    }

    // 順位・アプリ・使用時間・能力名・短い1行に絞り、選択は青い枠で示す。
    void DrawCard(Rect r, int index, AbilityCandidate c, bool selected, double maxSeconds)
    {
        MacDesktopGui.Card(r, selected);
        float x = r.x + 10f, w = r.width - 20f;
        float y = r.y + 8f;
        Color accent = selected ? MacDesktopGui.Blue : MacDesktopGui.Muted;
        PixelGui.TextEn(new Rect(x, y + 2f, 60f, 10f), index < Ranks.Length ? Ranks[index] : $"{index + 1}TH", 8, accent);

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

        y = r.y + 82f;
        PixelGui.DottedLine(x, y, w, PixelGui.Muted);
        y += 10f;
        if (c.Skill != null)
        {
            PixelGui.DrawSprite(new Rect(x, y, 16f, 16f), c.Skill.Icon != null ? c.Skill.Icon : c.AppIcon);
            PixelGui.TextFit(new Rect(x + 20f, y - 1f, w - 20f, 18f), c.Skill.DisplayName, 16, PixelGui.Ink);
            PixelGui.TextFit(new Rect(x, y + 30f, w, 18f), SkillBriefs.Line(c.Skill), 12, PixelGui.BodyText);
        }
    }

}
