using UnityEngine;

/// <summary>ゲーム内だけのロック解除とアプリ起動の演出。</summary>
public sealed class MacDesktopBoot
{
    public enum Step { Locked, Unlocking, Desktop, Bouncing, Launching, Ready }
    public const float BounceDuration=1.8f;
    public const float LaunchDuration=0.75f;
    public Step Current { get; private set; }
    float _started;
    static Font _font;
    public bool IsPlaying => Current!=Step.Ready;
    public bool ShowDesktop => Current!=Step.Locked;
    public bool ShowApp => Current==Step.Launching||Current==Step.Ready;

    public MacDesktopBoot(float now,bool play)
    { Current=play?Step.Locked:Step.Ready; _started=now; }

    public bool Unlock(float now)
    {
        if(Current!=Step.Locked||now-_started<0.25f) return false;
        Current=Step.Unlocking; _started=now;
        return true;
    }

    public bool CanUnlock(float now) => Current==Step.Locked&&now-_started>=0.25f;

    // 2026-11-20（まさぴ〜）：解除のあとはデスクトップで待ち、Dock のゲームのアイコンを押したら起動する
    public const float IdleHopDelay=1.5f,IdleHopInterval=2.4f,IdleHopTime=0.45f;

    /// <summary>デスクトップで待っているとき、ゲームのアプリを起動する（アイコンが跳ねてから窓が開く）。</summary>
    public bool Launch(float now)
    {
        if(Current!=Step.Desktop||now-_started<0.3f) return false;
        Current=Step.Bouncing; _started=now;
        return true;
    }

    /// <summary>デスクトップで待つ間、Dock のゲームのアイコンをときどき1回だけ跳ねさせる（押す所を字なしで見せる）。跳ねないときは -1。</summary>
    public float IdleHop(float now)
    {
        if(Current!=Step.Desktop) return -1;
        float t=now-_started-IdleHopDelay;
        if(t<0) return -1;
        t%=IdleHopInterval;
        return t<IdleHopTime?t/IdleHopTime*0.5f:-1;
    }

    public void Tick(float now)
    {
        while(Current!=Step.Locked&&Current!=Step.Desktop&&Current!=Step.Ready)
        {
            float duration=Current==Step.Bouncing?BounceDuration:Current==Step.Launching?LaunchDuration:0.65f; // Unlocking
            if(now-_started<duration) break;
            _started+=duration;
            Current=(Step)((int)Current+1);
        }
    }

    public float LaunchProgress(float now) => Current==Step.Ready?1:
        Current==Step.Launching?Mathf.Clamp01((now-_started)/LaunchDuration):0;
    public float BounceProgress(float now) => Current==Step.Bouncing?
        Mathf.Clamp01((now-_started)/BounceDuration):-1;
    public static float Ease(float t) { t=Mathf.Clamp01(t); return 1-(1-t)*(1-t)*(1-t); }

    public void DrawLock(float now,bool authenticating=false,bool touchIdAvailable=true,string status=null)
    {
        if(Current!=Step.Locked&&Current!=Step.Unlocking) return;
        float progress=Current==Step.Unlocking?Ease((now-_started)/0.65f):0;
        float alpha=1-progress;
        Matrix4x4 matrix=GUI.matrix;
        Color color=GUI.color;
        bool enabled=GUI.enabled;
        try
        {
            GUI.matrix=Matrix4x4.identity; GUI.color=Color.white;
            PixelGui.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,0.2f*alpha));
            float scale=Mathf.Clamp(Screen.height/720f,0.75f,2.5f);
            GUI.matrix=Matrix4x4.Translate(new Vector3(0,-progress*50*scale,0));
            GUI.color=new Color(1,1,1,alpha);
            GUI.enabled=enabled;
            if(_font==null) _font=Font.CreateDynamicFontFromOSFont(new[]{"Helvetica Neue","Hiragino Sans","Arial"},20);
            float center=Screen.width*0.5f;
            var date=System.DateTime.Now;
            Label(new Rect(0,Screen.height*0.065f,Screen.width,32*scale),
                date.ToString("M月d日 dddd",System.Globalization.CultureInfo.GetCultureInfo("ja-JP")),18*scale);
            GlassClock(new Rect(0,Screen.height*0.105f,Screen.width,124*scale),date.ToString("HH:mm"),100*scale,scale);

            float y=Screen.height-180*scale;
            Rect avatar=new Rect(center-27*scale,y,54*scale,54*scale);
            Glass(avatar,alpha,false,scale);
            MacDesktopGui.Rounded(new Rect(center-8*scale,y+10*scale,16*scale,16*scale),
                new Color(1,1,1,0.82f*alpha),Mathf.RoundToInt(8*scale));
            MacDesktopGui.Rounded(new Rect(center-17*scale,y+31*scale,34*scale,16*scale),
                new Color(1,1,1,0.82f*alpha),Mathf.RoundToInt(8*scale));
            Label(new Rect(center-120*scale,y+60*scale,240*scale,26*scale),"Player",15*scale);
            Rect instruction=new Rect(center-190*scale,y+91*scale,380*scale,24*scale);
            Label(instruction,
                touchIdAvailable?"Enterキー または Touch IDで解除":"Enterキーで解除",13*scale,
                FontStyle.Normal,new Color(1,1,1,0.8f));
            if(touchIdAvailable&&Current==Step.Locked)
            {
                MacTouchId.PositionInGameView(new Rect(center-14*scale,y+145*scale,28*scale,28*scale));
            }
            if(!string.IsNullOrEmpty(status))
                Label(new Rect(center-190*scale,y+128*scale,380*scale,22*scale),status,11*scale);
            if(_font.material!=null&&_font.material.mainTexture!=null) _font.material.mainTexture.filterMode=FilterMode.Bilinear;
        }
        finally { GUI.matrix=matrix; GUI.color=color; GUI.enabled=enabled; }
    }

    static void GlassClock(Rect rect,string text,float size,float scale)
    {
        Rect shadow=rect; shadow.position+=new Vector2(0,2*scale);
        Label(shadow,text,size,FontStyle.Bold,new Color(0,0,0,0.2f));
        Rect highlight=rect; highlight.position+=new Vector2(-0.6f*scale,-0.8f*scale);
        Label(highlight,text,size,FontStyle.Bold,new Color(0.9f,0.96f,1,0.32f));
        Rect edge=rect; edge.position+=new Vector2(0.6f*scale,0.8f*scale);
        Label(edge,text,size,FontStyle.Bold,new Color(1,1,1,0.14f));
        Label(rect,text,size,FontStyle.Bold,new Color(0.94f,0.98f,1,0.44f));
    }

    // 背景を透かし、上縁の反射と薄い影でガラスの厚みを表す。
    static void Glass(Rect rect,float alpha,bool hover,float scale)
    {
        int radius=Mathf.RoundToInt(rect.height*0.5f);
        MacDesktopGui.Rounded(new Rect(rect.x,rect.y+2*scale,rect.width,rect.height),new Color(0,0,0,0.15f*alpha),radius);
        int height=Mathf.CeilToInt(rect.height);
        float rim=Mathf.Max(1,scale*0.65f),r=rect.height*0.5f;
        for(int row=0;row<height;row++)
        {
            float dy=row+0.5f-r;
            float cut=r-Mathf.Sqrt(Mathf.Max(0,r*r-dy*dy));
            float span=rect.width-cut*2;
            if(span<=0) continue;
            float t=row/Mathf.Max(1f,height-1);
            float reflection=Mathf.Lerp(0.42f,0.12f,t);
            if(row<rim||row>=height-rim)
                PixelGui.Fill(new Rect(rect.x+cut,rect.y+row,span,1),new Color(0.92f,0.97f,1,reflection*alpha));
            else
            {
                PixelGui.Fill(new Rect(rect.x+cut,rect.y+row,rim,1),new Color(0.92f,0.97f,1,reflection*alpha));
                PixelGui.Fill(new Rect(rect.xMax-cut-rim,rect.y+row,rim,1),new Color(0.92f,0.97f,1,reflection*alpha));
                float fill=(hover?0.16f:0.09f)+Mathf.Max(0,1-t*3)*0.09f;
                PixelGui.Fill(new Rect(rect.x+cut+rim,rect.y+row,Mathf.Max(0,span-2*rim),1),new Color(0.87f,0.94f,1,fill*alpha));
            }
        }
    }

    static void Label(Rect rect,string text,float size,FontStyle weight=FontStyle.Normal,Color? tint=null)
    {
        var style=new GUIStyle { font=_font,fontSize=Mathf.RoundToInt(size),fontStyle=weight,
            alignment=TextAnchor.MiddleCenter,normal={textColor=PixelGui.ToGui(tint??Color.white)} };
        GUI.Label(rect,text,style);
    }
}
