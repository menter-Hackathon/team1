using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Finder風のアプリ図鑑。検索・正常化フィルター・ウィンドウ操作に対応する。
/// 2026-11-20：正常化したアプリを選んで スペース／Z／Enter／ダブルクリック で「クイックルック」の小窓を開き、
/// ボスの姿（BossPortrait）・ボスの技・固有能力（SkillBriefs の1行・届く所・絵のタグ）を見られる。←→ で前／次、スペース／Esc／X で閉じる。
/// 図鑑の曲（bgm_dex）を流し、閉じたら前の曲に戻す。
/// </summary>
public class AppDexMenu : MonoBehaviour
{
    [Tooltip("閉じているときに、画面の右上に「図鑑」ボタンを出す")]
    [SerializeField] bool _showButton = true;

    List<AppDexEntry> _entries = new List<AppDexEntry>();
    int _selected = -1;
    int _detail = -1;       // 詳しい画面で開いているアプリの番号（-1＝一覧）
    Vector2 _scroll;
    int _cols = 6;
    readonly MacDesktopGui.WindowState _window = new MacDesktopGui.WindowState(false);
    readonly List<AppDexEntry> _visible = new List<AppDexEntry>();
    public enum Filter { All, Restored, Infected }
    Filter _filter;
    string _search="";
    bool _searchFocused, _clearFocus, _revealSelection;
    int _dismissedFrame = -1;
    string _backBgm;          // 図鑑を閉じたら戻す曲
    float _lastClickTime = -1f;
    int _lastClickIndex = -1;

    public MacDesktopGui.WindowState Window => _window;
    public bool IsMinimized => _window.IsMinimized;
    public bool BlocksParentInput => IsOpen || _dismissedFrame==Time.frameCount;
    public bool HandleMinimizedExternally { get; set; }

    public bool IsOpen => _window.IsVisible;
    public bool ShowButton { get => _showButton; set => _showButton = value; }
    /// <summary>詳しい画面を開いているか</summary>
    public bool IsDetailOpen => IsOpen && _detail >= 0;

    public void Open()
    {
        if(_window.IsMinimized) { _window.Open(); return; }
        _entries = AppDex.Entries.ToList();
        AppVisuals.Prepare(_entries.Select(e => e.Row)); // アイコンをまとめて読む（まだのアプリだけ）
        ApplyFilter();
        _detail = -1;
        _window.Open();
        if (GameAudio.CurrentBgm != "bgm_dex") _backBgm = GameAudio.CurrentBgm; // 閉じたら前の画面の曲に戻す
        GameAudio.PlayBgm("bgm_dex");
    }

    public void Close()
    {
        _window.Close(); _searchFocused=false; _clearFocus=true; _dismissedFrame=Time.frameCount;
        OnClosed();
    }

    // 閉じたとき：詳しい画面を閉じ、前の曲に戻す
    void OnClosed()
    {
        _detail = -1;
        GameAudio.Event("ui_cancel");
        if (!string.IsNullOrEmpty(_backBgm)) GameAudio.PlayBgm(_backBgm);
        _backBgm = null;
    }

    void ApplyFilter()
    {
        _visible.Clear();
        foreach(var entry in _entries)
        {
            if(MatchesEntry(entry,_filter,_search)) _visible.Add(entry);
        }
        _selected=_visible.Count>0?0:-1; _scroll=Vector2.zero; _revealSelection=true; _detail=-1;
    }

    public static bool MatchesEntry(AppDexEntry entry,Filter filter,string query)
    {
        if(entry==null) return false;
        if(filter==Filter.Restored&&!entry.IsDefeated||filter==Filter.Infected&&entry.IsDefeated) return false;
        return string.IsNullOrWhiteSpace(query)||(entry.name??"").IndexOf(query.Trim(),System.StringComparison.OrdinalIgnoreCase)>=0;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!IsOpen || kb == null) return;
        if(MacDesktopGui.WindowShortcuts(_window))
        {
            if(!IsOpen) { _searchFocused=false; _clearFocus=true; _dismissedFrame=Time.frameCount; if(!_window.IsMinimized) OnClosed(); }
            return;
        }
        // 詳しい画面（クイックルック）を開いている間：←→ で前／次、スペース・Esc・X で一覧に戻る
        if(_detail>=0)
        {
            if(kb.escapeKey.wasPressedThisFrame||kb.spaceKey.wasPressedThisFrame||kb.xKey.wasPressedThisFrame||kb.backspaceKey.wasPressedThisFrame) { CloseDetail(); return; }
            if(kb.leftArrowKey.wasPressedThisFrame||kb.aKey.wasPressedThisFrame) StepDetail(-1);
            if(kb.rightArrowKey.wasPressedThisFrame||kb.dKey.wasPressedThisFrame) StepDetail(1);
            return;
        }
        // 閉じる：Esc、検索欄の外なら X／Backspace、コントローラーの B／SELECT（2026-11-20）
        if(kb.escapeKey.wasPressedThisFrame||(!_searchFocused&&(kb.xKey.wasPressedThisFrame||MenuInput.Back))) { Close(); return; }
        if(_searchFocused) return;
        // 絞り込み（すべて・正常化済・未正常化）を Q／E、コントローラーの L／R で切り替える（端から端へ回る）。2026-11-20
        int tab = (MenuInput.NextTab ? 1 : 0) - (MenuInput.PrevTab ? 1 : 0);
        if (tab != 0)
        {
            int n = System.Enum.GetValues(typeof(Filter)).Length;
            _filter = (Filter)(((int)_filter + tab + n) % n);
            ApplyFilter();
            GameAudio.Event("ui_move");
        }
        if(_visible.Count==0) return;
        int step = 0;
        if (MenuInput.Left) step = -1;
        if (MenuInput.Right) step = 1;
        if (MenuInput.Up) step = -_cols;
        if (MenuInput.Down) step = _cols;
        if(step!=0) { _selected=Mathf.Clamp(_selected+step,0,_visible.Count-1); _revealSelection=true; }
    }

    void OnGUI()
    {
        if(GameSession.Instance!=null&&GameSession.Instance.IsTransitioning) return;
        int depth=GUI.depth; GUI.depth=-50;
        PixelGui.Begin(false);
        if(_clearFocus) { GUI.FocusControl(null); _clearFocus=false; }
        if(!IsOpen)
        {
            _window.Apply(MacDesktopGui.CenteredWindow(PixelGui.Width,PixelGui.Height,600,318,false),PixelGui.Width,PixelGui.Height,false);
            if(_showButton&&MacDesktopGui.Button(new Rect(PixelGui.Width-86,8,76,24),"図鑑")) Open();
            if(IsMinimized&&!HandleMinimizedExternally)
            {
                Rect bar=new Rect(PixelGui.Width-158,PixelGui.Height-46,148,38);
                MacDesktopGui.DockPlate(bar);
                MacDesktopGui.MiniWindowIcon(new Rect(bar.x+5,bar.y+3,30,30));
                PixelGui.Text(new Rect(bar.x+41,bar.y+9,100,18),"アプリ図鑑",12,MacDesktopGui.Ink);
                if(GUI.Button(bar,GUIContent.none,GUIStyle.none)) _window.Open();
            }
            PixelGui.End(); GUI.depth=depth; return;
        }
        DrawDex();
        PixelGui.End(); GUI.depth=depth;
        EventType t=Event.current.type;
        if(IsOpen&&(t==EventType.MouseDown||t==EventType.MouseUp||t==EventType.MouseDrag||t==EventType.ScrollWheel||t==EventType.KeyDown)) Event.current.Use();
    }

    void DrawDex()
    {
        float w=PixelGui.Width,h=PixelGui.Height;
        MacDesktopGui.MenuBar(w,"アプリ図鑑");
        Rect home=MacDesktopGui.CenteredWindow(w,h,600,318,false);
        Rect win=_window.Apply(home,w,h);
        Rect inner=MacDesktopGui.Window(win,"アプリ図鑑",out var action);
        if(action==MacDesktopGui.WindowAction.Close) { Close(); return; }
        _window.Handle(action);
        if(!IsOpen) { _dismissedFrame=Time.frameCount; return; }

        Rect sidebar=new Rect(inner.x-11,inner.y-8,104,inner.height+19);
        PixelGui.Fill(sidebar,new Color32(232,237,245,255));
        PixelGui.Text(new Rect(sidebar.x+10,sidebar.y+10,90,18),"よく使う項目",12,MacDesktopGui.Muted);
        PixelGui.TextEn(new Rect(sidebar.x+10,sidebar.y+26,90,8),"Q/E  L/R",8,MacDesktopGui.Muted); // 切り替えのボタン（2026-11-20）
        string[] filters={"すべて","正常化済","未正常化"};
        for(int i=0;i<filters.Length;i++)
        {
            Rect row=new Rect(sidebar.x+6,sidebar.y+36+i*29,92,25);
            if((int)_filter==i) MacDesktopGui.Rounded(row,MacDesktopGui.Blue,4);
            PixelGui.Fill(new Rect(row.x+8,row.y+9,7,7),i==1?new Color32(40,180,104,255):i==2?PixelGui.Lilac:MacDesktopGui.Blue);
            PixelGui.Text(new Rect(row.x+22,row.y+2,row.width-25,20),filters[i],12,(int)_filter==i?Color.white:MacDesktopGui.Ink);
            if(_detail<0&&GUI.Button(row,GUIContent.none,GUIStyle.none))
            {
                _filter=(Filter)i; ApplyFilter(); _searchFocused=false; GUI.FocusControl(null);
            }
        }
        PixelGui.Text(new Rect(sidebar.x+10,sidebar.yMax-26,90,18),$"{_entries.Count} 項目",12,MacDesktopGui.Muted);

        float x=inner.x+106,y=inner.y,contentW=inner.width-106;
        PixelGui.Text(new Rect(x,y,contentW-150,21),"アプリケーション",16,MacDesktopGui.Ink);
        Rect search=new Rect(inner.xMax-140,y,140,23);
        MacDesktopGui.Rounded(search,MacDesktopGui.BorderColor,5);
        MacDesktopGui.Rounded(PixelGui.Inset(search,1),Color.white,4);
        GUI.SetNextControlName("MacDexSearch");
        GUIStyle input=new GUIStyle(PixelGui.Style(false,12)); input.normal.textColor=PixelGui.ToGui(MacDesktopGui.Ink);
        string query=_detail<0?GUI.TextField(new Rect(search.x+8,search.y+1,search.width-16,21),_search,input):_search; // 詳しい画面の間は触れない
        if(_detail>=0) PixelGui.Text(new Rect(search.x+8,search.y+2,search.width-16,18),_search,12,MacDesktopGui.Ink);
        _searchFocused=GUI.GetNameOfFocusedControl()=="MacDexSearch";
        if(query!=_search) { _search=query; ApplyFilter(); }
        if(string.IsNullOrEmpty(_search)&&!_searchFocused) PixelGui.Text(new Rect(search.x+8,search.y+2,100,18),"検索",12,MacDesktopGui.Muted);
        int done=_entries.Count(e=>e.IsDefeated);
        PixelGui.Text(new Rect(x,y+25,125,16),$"正常化 {done} / {_entries.Count}",12,MacDesktopGui.Blue);
        PixelGui.Meter(new Rect(x+130,y+31,110,4),Mathf.Max(1,_entries.Count),_entries.Count>0?done/(float)_entries.Count:0f,MacDesktopGui.Blue,PixelGui.Muted);
        if(_visible.Count==0)
        {
            PixelGui.Text(new Rect(x,y+65,contentW,65),_entries.Count==0?
                "まだアプリがありません\nスキャンか訓練モードで遊ぶと、ここにアプリが並びます。":"一致するアプリがありません",12,MacDesktopGui.Muted,TextAnchor.MiddleCenter,true);
            return;
        }

        const float cellW=84,cellH=76,detailH=58;
        Rect grid=new Rect(x,y+44,contentW,inner.yMax-(y+44)-detailH-8);
        _cols=Mathf.Max(1,Mathf.FloorToInt((grid.width-12)/cellW));
        int rows=Mathf.CeilToInt(_visible.Count/(float)_cols);
        Rect content=new Rect(0,0,grid.width-16,rows*cellH);
        if(_revealSelection)
        {
            float top=(_selected/_cols)*cellH;
            if(top<_scroll.y) _scroll.y=top;
            if(top+cellH>_scroll.y+grid.height) _scroll.y=top+cellH-grid.height;
            _revealSelection=false;
        }
        _scroll=GUI.BeginScrollView(grid,_scroll,content);
        for(int i=0;i<_visible.Count;i++)
        {
            AppDexEntry entry=_visible[i];
            Rect cell=new Rect(i%_cols*cellW,i/_cols*cellH,cellW,cellH);
            Rect icon=new Rect(cell.x+18,cell.y+6,48,48);
            Rect plate=new Rect(icon.x-4,icon.y-4,56,56);
            MacDesktopGui.Card(plate,i==_selected);
            PixelGui.DrawSprite(icon,IconOf(entry,24));
            if(entry.IsDefeated)
            {
                Rect badge=new Rect(plate.xMax-20,plate.y-3,24,13);
                MacDesktopGui.Rounded(badge,MacDesktopGui.Blue,3);
                PixelGui.TextEn(new Rect(badge.x,badge.y+2,badge.width,9),$"x{Mathf.Min(99,entry.defeats)}",8,Color.white,TextAnchor.MiddleCenter);
            }
            PixelGui.TextFit(new Rect(cell.x+2,plate.yMax+2,cellW-4,14),entry.name,12,MacDesktopGui.Ink,TextAnchor.MiddleCenter);
            if(_detail<0&&GUI.Button(cell,GUIContent.none,GUIStyle.none))
            {
                // 同じアイコンをすぐもう一度押す（ダブルクリック）と、詳しい画面を開く
                bool twice=_lastClickIndex==i&&Time.unscaledTime-_lastClickTime<0.4f;
                if(_selected!=i) GameAudio.Event("ui_move");
                _selected=i; _searchFocused=false; GUI.FocusControl(null);
                _lastClickIndex=i; _lastClickTime=Time.unscaledTime;
                if(twice) TryOpenDetail(i);
            }
        }
        GUI.EndScrollView();
        if(_selected<0||_selected>=_visible.Count) return;
        AppDexEntry s=_visible[_selected];
        Rect detail=new Rect(x,inner.yMax-detailH,contentW,detailH);
        MacDesktopGui.Card(detail,false);
        PixelGui.DrawSprite(new Rect(detail.x+6,detail.y+5,48,48),IconOf(s,24));
        PixelGui.TextFit(new Rect(detail.x+62,detail.y+7,detail.width*0.5f-64,18),s.name,16,MacDesktopGui.Ink);
        string line=s.IsDefeated?$"正常化 {s.defeats}回　初回 {ShortDate(s.firstDefeated)}":"まだ正常化していない";
        PixelGui.TextFit(new Rect(detail.x+62,detail.y+32,detail.width*0.5f-64,16),line,12,MacDesktopGui.Muted);
        float sx=detail.x+detail.width*0.55f;
        PixelGui.Text(new Rect(sx,detail.y+7,120,14),"固有能力",12,MacDesktopGui.Blue);
        if(s.IsDefeated)
        {
            PixelGui.DrawSprite(new Rect(sx,detail.y+28,16,16),SkillIconOf(s));
            PixelGui.TextFit(new Rect(sx+22,detail.y+27,detail.xMax-sx-28,18),string.IsNullOrEmpty(s.skill)?"-":s.skill,16,MacDesktopGui.Ink);
        }
        else PixelGui.Text(new Rect(sx,detail.y+27,detail.xMax-sx-6,18),"？？？",16,MacDesktopGui.Muted);
        if(s.IsDefeated&&_detail<0) PixelGui.TextEn(new Rect(detail.xMax-74,detail.yMax-14,70,9),"SPACE ▶",8,MacDesktopGui.Blue,TextAnchor.MiddleRight);
        if(_detail>=0) DrawQuickLook(win);
    }

    // ───────── 詳しい画面（クイックルック。2026-11-20） ─────────

    void TryOpenDetail(int index)
    {
        if(index<0||index>=_visible.Count) return;
        if(!_visible[index].IsDefeated) { GameAudio.Event("ui_cancel"); return; } // まだ正常化していないアプリは開かない
        _detail=index; _selected=index;
        GameAudio.Event("ui_decide");
    }

    void CloseDetail()
    {
        _detail=-1;
        GameAudio.Event("ui_cancel");
    }

    // 前／次の正常化したアプリへ（見えている並びの中で）
    void StepDetail(int dir)
    {
        for(int i=_detail+dir;i>=0&&i<_visible.Count;i+=dir)
        {
            if(!_visible[i].IsDefeated) continue;
            _detail=i; _selected=i; _revealSelection=true;
            GameAudio.Event("ui_move");
            return;
        }
    }

    void DrawQuickLook(Rect dexWindow)
    {
        if(_detail<0||_detail>=_visible.Count) { _detail=-1; return; }
        AppDexEntry s=_visible[_detail];
        // 図鑑のウィンドウの上に、少し小さいウィンドウを重ねる（Mac のクイックルック）
        PixelGui.Fill(new Rect(dexWindow.x,dexWindow.y,dexWindow.width,dexWindow.height),new Color(0.05f,0.07f,0.12f,0.25f));
        float qw=Mathf.Min(520f,dexWindow.width-24f),qh=Mathf.Min(268f,dexWindow.height-24f);
        Rect q=new Rect(Mathf.Round(dexWindow.center.x-qw*0.5f),Mathf.Round(dexWindow.center.y-qh*0.5f+6f),qw,qh);
        Rect inner=MacDesktopGui.Window(q,s.name,out var action);
        if(action!=MacDesktopGui.WindowAction.None) { CloseDetail(); return; }

        // 左：ボスの姿
        float pw=Mathf.Min(200f,inner.width*0.42f),ph=inner.height-24f;
        BossPortrait portrait=BossPortrait.For(s);
        portrait.Draw(new Rect(inner.x,inner.y,pw,ph));
        // 前／次（正常化したアプリの中の何番目か）
        int count=0,pos=0;
        for(int i=0;i<_visible.Count;i++)
        {
            if(!_visible[i].IsDefeated) continue;
            if(i==_detail) pos=count;
            count++;
        }
        if(QuickNav(new Rect(inner.x,inner.y+ph+4f,24f,18f),"‹",pos>0)) StepDetail(-1);
        if(QuickNav(new Rect(inner.x+pw-24f,inner.y+ph+4f,24f,18f),"›",pos<count-1)) StepDetail(1);
        PixelGui.TextEn(new Rect(inner.x+24f,inner.y+ph+9f,pw-48f,9f),$"{pos+1} / {count}",8,MacDesktopGui.Muted,TextAnchor.MiddleCenter);

        // 右：アプリ・ボスの技・固有能力
        float rx=inner.x+pw+12f,rw=inner.xMax-rx,y=inner.y;
        PixelGui.DrawSprite(new Rect(rx,y,24f,24f),IconOf(s,24));
        PixelGui.TextFit(new Rect(rx+30f,y+1f,rw-30f,18f),s.name,16,MacDesktopGui.Ink);
        Rect badge=new Rect(rx+30f,y+20f,24f,12f);
        MacDesktopGui.Rounded(badge,MacDesktopGui.Blue,3);
        PixelGui.TextEn(new Rect(badge.x,badge.y+2f,badge.width,8f),$"x{Mathf.Min(99,s.defeats)}",8,Color.white,TextAnchor.MiddleCenter);
        PixelGui.Text(new Rect(badge.xMax+6f,y+17f,80f,14f),ShortDate(s.firstDefeated),12,MacDesktopGui.Muted);

        y+=40f;
        PixelGui.Fill(new Rect(rx,y,rw,1f),MacDesktopGui.BorderColor);
        PixelGui.Text(new Rect(rx,y+4f,rw,14f),"ボスの技",12,MacDesktopGui.Blue);
        float cw=Mathf.Floor((rw-4f)*0.5f);
        for(int k=0;k<portrait.Moves.Count&&k<4;k++)
            portrait.DrawMove(new Rect(rx+(k%2)*(cw+4f),y+22f+(k/2)*24f,cw,20f),portrait.Moves[k]);

        y+=74f;
        PixelGui.Fill(new Rect(rx,y,rw,1f),MacDesktopGui.BorderColor);
        PixelGui.Text(new Rect(rx,y+4f,rw,14f),"固有能力",12,MacDesktopGui.Blue);
        Rect slot=new Rect(rx,y+22f,32f,32f);
        MacDesktopGui.Card(slot,false);
        bool hasSkill=!string.IsNullOrEmpty(s.skill);
        PixelGui.DrawSprite(PixelGui.Inset(slot,4f),hasSkill?SkillIconOf(s):IconOf(s,16));
        if(!hasSkill) return;
        SkillDefinition def=SkillDefOf(s);
        PixelGui.TextFit(new Rect(rx+40f,y+24f,rw-40f,18f),s.skill,16,MacDesktopGui.Ink);
        PixelGui.TextFit(new Rect(rx+40f,y+42f,rw-40f,14f),SkillBriefs.Line(s.skill,def),12,MacDesktopGui.Muted);
        PixelGui.ReachMap(rx,y+60f,SkillBriefs.Reach(s.skill));
        PixelGui.PictoTags(rx+64f,y+62f,SkillBriefs.Pictos(s.skill,def),rw-64f);
    }

    static bool QuickNav(Rect r,string label,bool enabled)
    {
        if(!enabled) { MacDesktopGui.Rounded(r,new Color32(225,229,236,255),4); PixelGui.Text(r,label,12,MacDesktopGui.BorderColor,TextAnchor.MiddleCenter); return false; }
        return MacDesktopGui.Button(r,label);
    }

    // 図鑑のアプリの能力の定義（名前が合うときだけ。その場で作った能力も）
    static SkillDefinition SkillDefOf(AppDexEntry e)
    {
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        SkillDefinition skill = abilities != null ? abilities.SkillFor(e.Row) : null;
        if (skill == null) skill = AppVisuals.Get(e.Row)?.Generated;
        return skill != null && skill.DisplayName == e.skill ? skill : null;
    }

    /// <summary>アプリのアイコン（正常化していなければグレー）</summary>
    static Sprite IconOf(AppDexEntry e, int size)
    {
        AppVisual v = AppVisuals.Get(e.Row);
        if (v == null) return null;
        Sprite icon = size >= 24 ? v.Icon24 : v.Icon16;
        return e.IsDefeated ? icon : PixelIcon.Grayscale(icon);
    }

    /// <summary>
    /// 手に入る能力のアイコン（アプリ専用の能力 → カテゴリの能力の順にそのアイコン。
    /// その場で作った能力と、アイコンがまだ空のアプリ専用の能力は、アプリのアイコン）
    /// </summary>
    static Sprite SkillIconOf(AppDexEntry e)
    {
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        SkillDefinition skill = abilities != null ? abilities.SkillFor(e.Row) : null;
        if (skill != null && skill.Icon != null) return skill.Icon;
        return AppVisuals.Get(e.Row)?.Icon16;
    }

    static string ShortDate(string date) => date != null && date.Length >= 10 ? $"{int.Parse(date.Substring(5, 2))}/{int.Parse(date.Substring(8, 2))}" : "-";
}
