using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>スキャン・能力・駆除記録をFinder風に表示。端末の実ファイルは操作しない。</summary>
public sealed class MacDesktopFiles : MonoBehaviour
{
    static readonly string[] Folders={"書類","ダウンロード","ゴミ箱"};
    readonly MacDesktopGui.WindowState _window=new MacDesktopGui.WindowState(false);
    List<MacDesktopFileContent.File> _files=new List<MacDesktopFileContent.File>();
    int _folder,_selected,_dismissedFrame=-1;
    Vector2 _listScroll,_previewScroll;
    AppDexMenu _dex;
    public MacDesktopGui.WindowState Window => _window;
    public bool IsMinimized => _window.IsMinimized;
    public bool BlocksParentInput => _window.IsVisible||_dismissedFrame==Time.frameCount;

    public void Open(int folder)
    {
        _folder=Mathf.Clamp(folder,0,Folders.Length-1); _selected=0;
        _listScroll=_previewScroll=Vector2.zero;
        RefreshContents(); _window.Open();
    }

    void RefreshContents()
    {
        string selected=_selected<_files.Count?_files[_selected].Name:null;
        var session=GameSession.Instance;
        _files=MacDesktopFileContent.Build(_folder,session?.LastUsages,session!=null&&session.HasUsageRecord,
            session!=null&&session.LastUsageIsSample,session?.Run,AppDex.Entries);
        int found=_files.FindIndex(f=>f.Name==selected);
        _selected=found>=0?found:Mathf.Clamp(_selected,0,Mathf.Max(0,_files.Count-1));
    }

    void Close() { _window.Close(); _dismissedFrame=Time.frameCount; }

    void Update()
    {
        if(!_window.IsVisible) return;
        if(_dex==null) _dex=GetComponent<AppDexMenu>();
        if(_dex!=null&&_dex.BlocksParentInput) return;
        if(MacDesktopGui.WindowShortcuts(_window))
        {
            if(!_window.IsVisible) _dismissedFrame=Time.frameCount;
            return;
        }
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void OnGUI()
    {
        if(GameSession.Instance!=null&&GameSession.Instance.IsTransitioning) return;
        int depth=GUI.depth; bool enabled=GUI.enabled;
        GUI.depth=-40;
        PixelGui.Begin(false);
        try
        {
            if(_dex==null) _dex=GetComponent<AppDexMenu>();
            GUI.enabled=enabled&&(_dex==null||!_dex.IsOpen);
            float w=PixelGui.Width,h=PixelGui.Height;
            Rect home=MacDesktopGui.CenteredWindow(w,h,570,286);
            if(!_window.IsVisible) { _window.Apply(home,w,h,false); return; }
            if(Event.current.type==EventType.Layout) RefreshContents();
            Rect win=_window.Apply(home,w,h,GUI.enabled);
            Rect inner=MacDesktopGui.Window(win,Folders[_folder],out var action);
            _window.Handle(action);
            if(!_window.IsVisible) { _dismissedFrame=Time.frameCount; return; }
            float sidebar=89,list=185;
            PixelGui.Fill(new Rect(inner.x,inner.y,sidebar,inner.height),new Color32(231,236,243,255));
            PixelGui.Text(new Rect(inner.x+8,inner.y+3,sidebar-16,18),"よく使う項目",12,MacDesktopGui.Muted);
            for(int i=0;i<Folders.Length;i++)
            {
                Rect r=new Rect(inner.x+3,inner.y+29+i*29,sidebar-6,25);
                if(i==_folder) MacDesktopGui.Rounded(r,new Color32(203,217,237,255),3);
                PixelGui.TextFit(r,Folders[i],12,MacDesktopGui.Ink,TextAnchor.MiddleCenter);
                if(GUI.Button(r,GUIContent.none,GUIStyle.none)) Open(i);
            }
            float x=inner.x+sidebar+8;
            PixelGui.Text(new Rect(x,inner.y,list,20),"名前",12,MacDesktopGui.Muted);
            Rect listArea=new Rect(x,inner.y+25,list,inner.height-44);
            _listScroll=GUI.BeginScrollView(listArea,_listScroll,
                new Rect(0,0,list-18,Mathf.Max(listArea.height,_files.Count*34)));
            for(int i=0;i<_files.Count;i++)
            {
                Rect row=new Rect(0,i*34,list-18,30);
                if(i==_selected) MacDesktopGui.Rounded(row,MacDesktopGui.Blue,3);
                PixelGui.Fill(new Rect(row.x+6,row.y+6,12,17),Color.white);
                PixelGui.Fill(new Rect(row.x+8,row.y+11,8,1),MacDesktopGui.Muted);
                PixelGui.Fill(new Rect(row.x+8,row.y+14,6,1),MacDesktopGui.Muted);
                PixelGui.TextFit(new Rect(row.x+23,row.y,row.width-27,row.height),_files[i].Name,12,
                    i==_selected?Color.white:MacDesktopGui.Ink);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none)) { _selected=i; _previewScroll=Vector2.zero; }
            }
            GUI.EndScrollView();
            PixelGui.Text(new Rect(x,inner.yMax-18,list,18),_files.Count+"項目",12,MacDesktopGui.Muted);
            Rect preview=new Rect(x+list+8,inner.y+3,inner.xMax-x-list-8,inner.height-6);
            Preview(preview);
        }
        finally { PixelGui.End(); GUI.depth=depth; GUI.enabled=enabled; }
        var type=Event.current.type;
        if(_window.IsVisible&&(type==EventType.MouseDown||type==EventType.MouseUp||type==EventType.MouseDrag||type==EventType.ScrollWheel||type==EventType.KeyDown))
            Event.current.Use();
    }

    void Preview(Rect r)
    {
        if(_files.Count==0)
        {
            PixelGui.Text(new Rect(r.x,r.y+8,r.width,r.height-16),MacDesktopFileContent.EmptyMessage(_folder),
                12,MacDesktopGui.Muted,TextAnchor.UpperLeft,true);
            return;
        }
        var file=_files[_selected];
        PixelGui.TextFit(new Rect(r.x,r.y,r.width,25),file.Title,12,MacDesktopGui.Ink);
        bool link=file.OpenDex&&_dex!=null;
        Rect area=new Rect(r.x,r.y+34,r.width,r.height-34-(link?38:0));
        float textWidth=Mathf.Max(30,area.width-18);
        float textHeight=PixelGui.Style(false,12,TextAnchor.UpperLeft,true).CalcHeight(new GUIContent(file.Body),textWidth)+8;
        _previewScroll=GUI.BeginScrollView(area,_previewScroll,
            new Rect(0,0,textWidth,Mathf.Max(area.height,textHeight)));
        PixelGui.Text(new Rect(0,0,textWidth,textHeight),file.Body,12,MacDesktopGui.Muted,TextAnchor.UpperLeft,true);
        GUI.EndScrollView();
        if(link&&MacDesktopGui.Button(new Rect(r.x,r.yMax-30,r.width,28),"図鑑を開く",true))
        {
            Close(); _dex.Open();
        }
    }
}
