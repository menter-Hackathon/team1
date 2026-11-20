using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ゲーム内デスクトップの共通部品。PixelGui.Begin(false) の後で使う。</summary>
public static class MacDesktopGui
{
    /// <summary>画面に出すゲームの名前（窓の題名・メニューバー・Dock・通知）。2026-11-20 に「Virus Desktop」から変更。
    /// 保存の場所（productName＝Team1）は変えていない（変えると図鑑・実績・設定が読めなくなる）</summary>
    public const string AppName="ViruScan";

    public readonly struct DockItem
    {
        public readonly string Name;
        public readonly Sprite Icon;
        public readonly WindowState Window;
        public DockItem(string name,Sprite icon,WindowState window=null) { Name=name; Icon=icon; Window=window; }
    }

    public static readonly Color Blue=new Color32(0,112,245,255);
    public static readonly Color Ink=new Color32(31,39,51,255);
    public static readonly Color Muted=new Color32(101,111,127,255);
    public static readonly Color Paper=new Color32(248,249,251,255);
    public static readonly Color BorderColor=new Color32(186,194,207,255);
    static Font _menuFont;
    static int _shortcutFrame = -1;
    /// <summary>メニューバーのコントロールセンターのスイッチの押す所（仮想の画面の座標。MenuBar が描くたびに入る。MacControlCenter が使う。2026-11-20）</summary>
    public static Rect ControlCenterIcon { get; private set; }
    /// <summary>スイッチの後ろの明るい下じきの濃さ（0＝なし。開いている間・マウスが乗っている間。MacControlCenter が入れる）</summary>
    public static float ControlCenterGlow;

    public sealed class WindowDrag
    {
        Vector2 _offset, _grab;
        bool _dragging, _releaseCapture;
        int _capturedId;

        public Rect Position(Rect home,float width,float height)
        {
            Rect window=ClampWindowPosition(new Rect(home.position+_offset,home.size),width,height);
            _offset=window.position-home.position;
            return window;
        }

        public void Grab(Rect window,Vector2 mouse)
        {
            _grab=mouse-window.position; _dragging=true;
        }

        public Rect Move(Rect home,Vector2 mouse,float width,float height)
        {
            if(!_dragging) return Position(home,width,height);
            Rect window=ClampWindowPosition(new Rect(mouse-_grab,home.size),width,height);
            _offset=window.position-home.position;
            return window;
        }

        public void Release() { _dragging=false; _releaseCapture=true; }

        void CancelCapture()
        {
            if(_capturedId!=0&&GUIUtility.hotControl==_capturedId) GUIUtility.hotControl=0;
            _capturedId=0; _dragging=false; _releaseCapture=false;
        }

        public Rect Apply(Rect home,float width,float height,bool enabled=true)
        {
            Rect window=Position(home,width,height);
            Rect header=TitleDragRegion(window);
            int id=GUIUtility.GetControlID(0x4d4143,FocusType.Passive,header);
            Event e=Event.current;
            if(_releaseCapture) CancelCapture();
            if(!enabled)
            {
                CancelCapture();
                return window;
            }
            switch(e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if(e.button==0&&header.Contains(e.mousePosition))
                    {
                        Grab(window,e.mousePosition);
                        GUIUtility.hotControl=id; _capturedId=id; e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if(_dragging&&GUIUtility.hotControl==id)
                    {
                        window=Move(home,e.mousePosition,width,height);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if(e.button==0&&_dragging&&GUIUtility.hotControl==id)
                    {
                        GUIUtility.hotControl=0; Release(); e.Use();
                    }
                    break;
                case EventType.Ignore:
                    CancelCapture();
                    break;
            }
            return window;
        }
    }

    public static Rect TitleDragRegion(Rect window)
        => new Rect(window.x+58,window.y+1,window.width-66,26);

    // 一部を画面外へ動かせるが、つかみ直せるだけのタイトルバーを残す。
    public static Rect ClampWindowPosition(Rect window,float width,float height)
    {
        window.x=Mathf.Round(Mathf.Clamp(window.x,144f-window.width,width-144f));
        window.y=Mathf.Round(Mathf.Clamp(window.y,24f,Mathf.Max(24f,height-82f)));
        return window;
    }

    public enum WindowAction { None, Close, Minimize, Maximize }

    public sealed class WindowState
    {
        enum Visibility { Visible, Closed, Minimized }
        Visibility _visibility;
        readonly WindowDrag _drag=new WindowDrag();
        public bool IsVisible => _visibility==Visibility.Visible;
        public bool IsMinimized => _visibility==Visibility.Minimized;
        public bool IsClosed => _visibility==Visibility.Closed;
        public bool IsMaximized { get; private set; }

        public WindowState(bool open=true) { _visibility=open?Visibility.Visible:Visibility.Closed; }
        public void Open() => _visibility=Visibility.Visible;
        public void Close() { _visibility=Visibility.Closed; _drag.Release(); }
        public void Minimize() { _visibility=Visibility.Minimized; _drag.Release(); }
        public void ToggleMaximize() { IsMaximized=!IsMaximized; _drag.Release(); }
        public void Handle(WindowAction action)
        {
            if(action==WindowAction.Close) Close();
            else if(action==WindowAction.Minimize) Minimize();
            else if(action==WindowAction.Maximize) ToggleMaximize();
        }
        public Rect Layout(Rect home,float width,float height)
            => IsMaximized?MaximizedWindow(width,height):_drag.Position(home,width,height);
        public Rect Apply(Rect home,float width,float height,bool enabled=true)
        {
            Rect window=Layout(home,width,height);
            if(!IsVisible||!enabled) { _drag.Apply(home,width,height,false); return window; }
            Event e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&e.clickCount==2&&TitleDragRegion(window).Contains(e.mousePosition))
            {
                ToggleMaximize(); e.Use();
                return Layout(home,width,height);
            }
            if(IsMaximized) { _drag.Apply(home,width,height,false); return window; }
            return _drag.Apply(home,width,height);
        }
    }

    public static Rect MaximizedWindow(float width,float height) => new Rect(4,25,width-8,height-30);

    public static bool WindowShortcuts(WindowState window)
    {
        if(_shortcutFrame==Time.frameCount) return true;
        Keyboard kb=Keyboard.current;
        if(!window.IsVisible||kb==null||!(kb.leftMetaKey.isPressed||kb.rightMetaKey.isPressed)) return false;
        if(kb.wKey.wasPressedThisFrame) { window.Close(); _shortcutFrame=Time.frameCount; return true; }
        if(kb.mKey.wasPressedThisFrame) { window.Minimize(); _shortcutFrame=Time.frameCount; return true; }
        if((kb.leftCtrlKey.isPressed||kb.rightCtrlKey.isPressed)&&kb.fKey.wasPressedThisFrame)
        { window.ToggleMaximize(); _shortcutFrame=Time.frameCount; return true; }
        return false;
    }

    public static Rect CoverUv(float width,float height,float imageWidth,float imageHeight)
    {
        float screenAspect=width/Mathf.Max(1f,height), imageAspect=imageWidth/Mathf.Max(1f,imageHeight);
        if(screenAspect>imageAspect)
        {
            float h=imageAspect/screenAspect;
            return new Rect(0,(1f-h)*0.5f,1,h);
        }
        float w=screenAspect/imageAspect;
        return new Rect((1f-w)*0.5f,0,w,1);
    }

    public static Rect CenteredWindow(float width,float height,float desiredWidth,float desiredHeight,bool aboveDock=true)
    {
        float w=Mathf.Min(desiredWidth,width-28f);
        float available=height-30f-(aboveDock?62f:12f);
        float h=Mathf.Min(desiredHeight,available);
        return new Rect(Mathf.Round((width-w)*0.5f),Mathf.Round(30f+(available-h)*0.5f),w,h);
    }

    public static void Rounded(Rect r,Color color,int radius=4)
    {
        float cut=Mathf.Min(radius,Mathf.Min(r.width,r.height)*0.5f);
        PixelGui.Fill(new Rect(r.x,r.y+cut,r.width,r.height-cut*2),color);
        for(int row=0;row<cut;row++)
        {
            float inset=Mathf.Ceil(cut-Mathf.Sqrt(cut*cut-(cut-row-0.5f)*(cut-row-0.5f)));
            PixelGui.Fill(new Rect(r.x+inset,r.y+row,r.width-inset*2,1),color);
            PixelGui.Fill(new Rect(r.x+inset,r.yMax-row-1,r.width-inset*2,1),color);
        }
    }

    static void Circle(Vector2 center,int radius,Color color)
    {
        for(int y=-radius;y<=radius;y++)
        {
            int half=Mathf.FloorToInt(Mathf.Sqrt(radius*radius-y*y));
            PixelGui.Fill(new Rect(center.x-half,center.y+y,half*2+1,1),color);
        }
    }

    public static void MenuBar(float width,string app,string status=null)
    {
        // デスクトップのメニューだけは実解像度で描く。ゲーム用整数拡大と
        // ドットフォントから分離し、文字を滑らかに、小さく保つ。
        Matrix4x4 previousMatrix=GUI.matrix;
        Color previousColor=GUI.color;
        bool previousEnabled=GUI.enabled;
        GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        try
        {
            if(_menuFont==null)
                _menuFont=Font.CreateDynamicFontFromOSFont(new[]{"Helvetica Neue","Hiragino Sans","Arial"},16);
            float w=width*PixelGui.Scale;
            float scale=Mathf.Clamp(Screen.height/810f,1f,2f), h=Mathf.Round(25*scale);
            Color text=new Color32(246,246,248,255);
            PixelGui.Fill(new Rect(0,0,w,h),new Color32(28,28,30,245));
            PixelGui.Fill(new Rect(0,h-1,w,1),new Color(1,1,1,0.12f));
            var style=new GUIStyle { font=_menuFont,fontSize=Mathf.RoundToInt(13*scale),alignment=TextAnchor.MiddleLeft,
                clipping=TextClipping.Clip,normal={textColor=PixelGui.ToGui(text)} };
            var apple=new GUIStyle(style) { fontSize=Mathf.RoundToInt(19*scale) };
            GUI.Label(new Rect(15*scale,-scale,25*scale,h),"\uF8FF",apple);
            float x=44*scale;
            var bold=new GUIStyle(style) { fontStyle=FontStyle.Bold };
            MenuLabel(app,bold,ref x,h,scale);
            float right=w-18*scale;
            string date=System.DateTime.Now.ToString("M月d日(ddd)  HH:mm",System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
            float dateWidth=style.CalcSize(new GUIContent(date)).x;
            GUI.Label(new Rect(right-dateWidth,0,dateWidth,h),date,style);
            right-=dateWidth+19*scale;
            // コントロールセンター：左右でつまみの位置が異なる二本のスイッチ。
            // 押す所を覚え、開いている間は後ろに明るい下じきを敷く（MacControlCenter。2026-11-20）
            ControlCenterIcon=new Rect((right-21*scale)/PixelGui.Scale,0,28*scale/PixelGui.Scale,h/PixelGui.Scale);
            if(ControlCenterGlow>0f) Rounded(new Rect(right-20*scale,3*scale,26*scale,h-6*scale),new Color(1,1,1,ControlCenterGlow),Mathf.RoundToInt(4*scale));
            for(int row=0;row<2;row++)
            {
                Rect track=new Rect(right-14*scale,(7+row*8)*scale,14*scale,5*scale);
                Rounded(track,new Color(1,1,1,0.85f),Mathf.RoundToInt(2*scale));
                Circle(new Vector2(track.x+(row==0?4:10)*scale,track.y+2*scale),Mathf.Max(1,Mathf.RoundToInt(1.5f*scale)),new Color32(38,38,40,255));
            }
            right-=34*scale;
            Rect battery=new Rect(right-24*scale,8*scale,23*scale,10*scale);
            Rounded(battery,new Color(1,1,1,0.65f),Mathf.RoundToInt(2*scale));
            Rounded(PixelGui.Inset(battery,scale),new Color32(28,28,30,255),Mathf.RoundToInt(scale));
            PixelGui.Fill(new Rect(battery.x+2*scale,battery.y+2*scale,17*scale,6*scale),text);
            PixelGui.Fill(new Rect(battery.xMax+scale,battery.y+3*scale,scale,4*scale),text);
            right-=43*scale;
            // Wi-Fi は携帯の棒グラフではなく三本の円弧。
            Vector2 wifi=new Vector2(right-8*scale,18*scale);
            for(int ring=0;ring<3;ring++)
            {
                float radius=(5+ring*4)*scale;
                for(int step=0;step<=32;step++)
                {
                    float angle=Mathf.Lerp(-Mathf.PI*0.76f,-Mathf.PI*0.24f,step/32f);
                    Circle(wifi+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,Mathf.Max(1,Mathf.RoundToInt(0.65f*scale)),text);
                }
            }
            Circle(wifi,Mathf.Max(1,Mathf.RoundToInt(scale)),text);
            right-=33*scale;
            foreach(string label in new[]{"ファイル","編集","表示","ウインドウ","ヘルプ"})
            {
                if(x+style.CalcSize(new GUIContent(label)).x+16*scale>right) break;
                MenuLabel(label,style,ref x,h,scale);
            }
            if(!string.IsNullOrEmpty(status))
            {
                float sw=style.CalcSize(new GUIContent(status)).x;
                if(right-sw>x+12*scale) GUI.Label(new Rect(right-sw,0,sw,h),status,style);
            }
            if(_menuFont.material!=null&&_menuFont.material.mainTexture!=null)
                _menuFont.material.mainTexture.filterMode=FilterMode.Bilinear;
        }
        finally { GUI.matrix=previousMatrix; GUI.color=previousColor; GUI.enabled=previousEnabled; }
    }

    static void MenuLabel(string label,GUIStyle style,ref float x,float height,float scale)
    {
        float size=style.CalcSize(new GUIContent(label)).x;
        GUI.Label(new Rect(x,0,size,height),label,style);
        x+=size+19*scale;
    }

    public static void Folder(Rect r)
    {
        PixelGui.Fill(new Rect(r.x,r.y,r.width*0.43f,6),new Color32(78,151,210,255));
        PixelGui.Fill(new Rect(r.x,r.y+4,r.width,r.height-4),new Color32(68,137,198,255));
        PixelGui.Fill(new Rect(r.x+1,r.y+8,r.width-2,r.height-9),new Color32(124,197,243,255));
        PixelGui.Fill(new Rect(r.x+2,r.y+8,r.width-4,2),new Color32(174,226,255,255));
    }

    public static int Folders(float width)
    {
        int clicked=-1;
        string[] names={"書類","ダウンロード"};
        for(int i=0;i<names.Length;i++)
        {
            float x=width-47,y=43+i*64;
            Folder(new Rect(x-16,y,32,26));
            PixelGui.ShadowText(new Rect(x-43,y+29,86,17),names[i],PixelGui.Style(false,12,TextAnchor.MiddleCenter),PixelGui.White,Ink,1);
            if(GUI.Button(new Rect(x-43,y-2,86,52),GUIContent.none,GUIStyle.none)) clicked=i;
        }
        return clicked;
    }

    public static void TrafficLights(Rect r)
    {
        Color[] colors={new Color32(255,95,87,255),new Color32(254,188,46,255),new Color32(40,200,64,255)};
        for(int i=0;i<3;i++) Circle(new Vector2(r.x+13+i*14,r.y+13),4,colors[i]);
    }

    public static Rect Window(Rect r,string title,out WindowAction action)
    {
        Rounded(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0.02f,0.04f,0.08f,0.28f),6);
        Rounded(r,BorderColor,6);
        Rounded(PixelGui.Inset(r,1),Paper,5);
        Rounded(new Rect(r.x+1,r.y+1,r.width-2,26),new Color32(231,234,240,255),5);
        PixelGui.Fill(new Rect(r.x+1,r.y+19,r.width-2,8),new Color32(231,234,240,255));
        PixelGui.Fill(new Rect(r.x+1,r.y+27,r.width-2,1),BorderColor);
        TrafficLights(r);
        PixelGui.TextFit(new Rect(r.x+65,r.y+4,r.width-130,19),title,12,Ink,TextAnchor.MiddleCenter);
        action=WindowAction.None;
        for(int i=0;i<3;i++)
        {
            Rect button=new Rect(r.x+6+i*14,r.y+6,14,14);
            if(button.Contains(Event.current.mousePosition)&&GUI.enabled)
            {
                Vector2 c=new Vector2(r.x+13+i*14,r.y+13);
                if(i==0)
                    for(int d=-2;d<=2;d++) { PixelGui.Fill(new Rect(c.x+d,c.y+d,1,1),Ink); PixelGui.Fill(new Rect(c.x+d,c.y-d,1,1),Ink); }
                else if(i==1) PixelGui.Fill(new Rect(c.x-2,c.y,5,1),Ink);
                else PixelGui.Border(new Rect(c.x-2,c.y-2,5,5),Ink,1);
            }
            if(GUI.Button(button,GUIContent.none,GUIStyle.none)) action=(WindowAction)(i+1);
        }
        return new Rect(r.x+12,r.y+36,r.width-24,r.height-48);
    }

    public static bool Button(Rect r,string label,bool primary=false,bool focused=false)
    {
        bool hover=r.Contains(Event.current.mousePosition)&&GUI.enabled;
        bool blue=primary||focused;
        Rounded(r,blue?new Color32(0,81,188,255):BorderColor,4);
        Rounded(PixelGui.Inset(r,1),blue?(hover?new Color32(28,135,255,255):Blue):
            (hover?new Color32(230,239,252,255):Color.white),3);
        PixelGui.Text(new Rect(r.x+8,r.y-1,r.width-16,r.height),label,12,blue?Color.white:Ink,TextAnchor.MiddleCenter);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }

    public static void Card(Rect r,bool selected)
    {
        Rounded(r,selected?Blue:BorderColor,5);
        Rounded(PixelGui.Inset(r,selected?2:1),Color.white,4);
    }

    public static void DockPlate(Rect r)
    {
        Rounded(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,0.22f),7);
        Rounded(r,new Color(0.85f,0.87f,0.9f,0.28f),7);
        Rounded(PixelGui.Inset(r,1),new Color(0.16f,0.17f,0.19f,0.86f),6);
        PixelGui.Fill(new Rect(r.x+7,r.y+1,r.width-14,1),new Color(1,1,1,0.18f));
    }

    /// <returns>クリックした項目。0はFinder、1はゲーム、2以降はアプリ。なしは-1。</returns>
    public static int Dock(float width,float height,IReadOnlyList<DockItem> apps,int active=1,float launchProgress=-1)
    {
        int count=2+(apps!=null?apps.Count:0);
        float w=count*42f+12f;
        Rect dock=new Rect(Mathf.Round((width-w)*0.5f),height-54,w,48);
        DockPlate(dock);
        int clicked=-1;
        for(int i=0;i<count;i++)
        {
            Rect r=new Rect(dock.x+9+i*42,dock.y+6,30,30);
            bool hover=r.Contains(Event.current.mousePosition)&&GUI.enabled;
            Rect icon=r; if(hover) icon.y-=3;
            if(i==1&&launchProgress>=0) icon.y-=Mathf.Abs(Mathf.Sin(launchProgress*Mathf.PI*2))*7;
            string name=i==0?"Finder":i==1?AppName:apps[i-2].Name;
            if(i==0) FinderIcon(icon);
            else if(i==1)
            {
                Rounded(icon,Blue,5);
                var frames=GameArt.Instance!=null?GameArt.Instance.ZakoWalk:null;
                if(frames!=null&&frames.Length>0) PixelGui.DrawSprite(PixelGui.Inset(icon,3),frames[0]);
            }
            else
            {
                if(apps[i-2].Window!=null) MiniWindowIcon(icon);
                else
                {
                    Rounded(icon,Color.white,5);
                    if(apps[i-2].Icon!=null) PixelGui.DrawSprite(PixelGui.Inset(icon,2),apps[i-2].Icon);
                }
            }
            if(i==active) Circle(new Vector2(r.center.x,dock.yMax-5),1,Color.white);
            if(hover)
            {
                Rect label=new Rect(r.center.x-54,dock.y-24,108,18);
                Rounded(label,Paper,3);
                PixelGui.TextFit(label,name,12,Ink,TextAnchor.MiddleCenter);
            }
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)) clicked=i;
        }
        return clicked;
    }

    public static void MiniWindowIcon(Rect r)
    {
        Rounded(new Rect(r.x,r.y+2,r.width,r.height-4),BorderColor,3);
        Rounded(new Rect(r.x+1,r.y+3,r.width-2,r.height-6),Paper,2);
        PixelGui.Fill(new Rect(r.x+2,r.y+4,r.width-4,5),new Color32(221,227,237,255));
        PixelGui.Fill(new Rect(r.x+4,r.y+5,2,2),new Color32(255,95,87,255));
        PixelGui.Fill(new Rect(r.x+8,r.y+5,2,2),new Color32(254,188,46,255));
        PixelGui.Fill(new Rect(r.x+12,r.y+5,2,2),new Color32(40,200,64,255));
        PixelGui.Fill(new Rect(r.x+7,r.y+14,r.width-14,2),Blue);
        PixelGui.Fill(new Rect(r.x+7,r.y+19,r.width-18,1),Muted);
    }

    static void FinderIcon(Rect r)
    {
        Rounded(r,new Color32(113,185,251,255),5);
        PixelGui.Fill(new Rect(r.center.x,r.y+2,r.width*0.5f-2,r.height-4),new Color32(219,237,253,255));
        PixelGui.Fill(new Rect(r.x+7,r.y+9,2,4),Ink);
        PixelGui.Fill(new Rect(r.x+21,r.y+9,2,4),Ink);
        PixelGui.Fill(new Rect(r.x+8,r.y+22,14,2),Ink);
        PixelGui.Fill(new Rect(r.x+14,r.y+14,2,8),Ink);
    }
}
