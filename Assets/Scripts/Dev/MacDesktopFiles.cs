using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ゲーム内の架空ファイル。実際の端末のファイルにはアクセスしない。</summary>
public sealed class MacDesktopFiles : MonoBehaviour
{
    static readonly string[] Folders={"書類","ダウンロード","ゴミ箱"};
    static readonly string[][] Names={
        new[]{"企画書_最終版.txt","やること.todo","ウイルス対策メモ.txt"},
        new[]{"名称未設定.zip","無料メモリ増量.pkg","あとで読む.html"},
        new[]{"消したはずのバグ.txt","やる気"}
    };
    readonly MacDesktopGui.WindowState _window=new MacDesktopGui.WindowState(false);
    int _folder,_selected,_versions=1,_zipDepth,_dismissedFrame=-1;
    readonly bool[] _tasks=new bool[3];
    bool _installed,_motivation;
    float _bugReturnsAt=-1;
    AppDexMenu _dex;
    public MacDesktopGui.WindowState Window => _window;
    public bool IsMinimized => _window.IsMinimized;
    public bool BlocksParentInput => _window.IsVisible||_dismissedFrame==Time.frameCount;

    public void Open(int folder)
    {
        _folder=Mathf.Clamp(folder,0,Folders.Length-1); _selected=0; _window.Open();
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
            GUI.enabled=enabled&&(_dex==null||!_dex.IsOpen);
            float w=PixelGui.Width,h=PixelGui.Height;
            Rect home=MacDesktopGui.CenteredWindow(w,h,570,286);
            if(!_window.IsVisible) { _window.Apply(home,w,h,false); return; }
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
            for(int i=0;i<Names[_folder].Length;i++)
            {
                Rect row=new Rect(x,inner.y+25+i*34,list-4,30);
                if(i==_selected) MacDesktopGui.Rounded(row,MacDesktopGui.Blue,3);
                PixelGui.Fill(new Rect(row.x+6,row.y+6,12,17),Color.white);
                PixelGui.Fill(new Rect(row.x+8,row.y+11,8,1),MacDesktopGui.Muted);
                PixelGui.Fill(new Rect(row.x+8,row.y+14,6,1),MacDesktopGui.Muted);
                PixelGui.TextFit(new Rect(row.x+23,row.y,row.width-27,row.height),Names[_folder][i],12,
                    i==_selected?Color.white:MacDesktopGui.Ink);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none)) _selected=i;
            }
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
        string title=Names[_folder][_selected],body="",button=null;
        if(_folder==0)
        {
            if(_selected==0)
            {
                title="企画書_最終版"+(_versions>1?"_最終版×"+_versions:"")+".txt";
                body="これが最後の修正です。\n\n※ 前の最終版は念のため残す。\n\n保存された最終版："+_versions+"個";
                button="最終版として保存";
            }
            else if(_selected==1)
            {
                PixelGui.TextFit(new Rect(r.x,r.y,r.width,22),title,12,MacDesktopGui.Ink);
                string[] tasks={"バグを直す","バグの原因を探す","今日はもう寝る"};
                for(int i=0;i<tasks.Length;i++)
                    if(MacDesktopGui.Button(new Rect(r.x,r.y+34+i*30,r.width,25),(_tasks[i]?"✓ ":"□ ")+tasks[i],_tasks[i])) _tasks[i]=!_tasks[i];
                PixelGui.Text(new Rect(r.x,r.y+132,r.width,48),_tasks[0]&&_tasks[1]&&_tasks[2]?
                    "すべて完了！\n……バグから新しいタスクが届いた。":"チェックを付けるだけなら、\nすぐ終わる。",12,MacDesktopGui.Muted,TextAnchor.UpperLeft,true);
                return;
            }
            else body="知らないファイルは開かない。\n\n知らないアプリの中に入らない。\n\n中に入ってしまった場合：\nウイルスを全部倒す。";
        }
        else if(_folder==1)
        {
            if(_selected==0)
            {
                title="名称未設定"+(_zipDepth>0?"（展開 "+_zipDepth+" 回目）":"")+".zip";
                body=_zipDepth==0?"中身：1ファイル\n\n何を圧縮したのか、\n誰も覚えていない。":"展開しました。\n\n中から、別の zip が出てきた。\n\n中身：1ファイル";
                button="展開する";
            }
            else if(_selected==1)
            {
                body=_installed?"インストール完了？\n\nメモリは増えなかった。\n\n不安が増えた。":"メモリを無料で増やせます！\n\n提供元：不明\nレビュー：星5（1件）\n投稿者：提供元";
                if(!_installed) button="インストールしてみる";
            }
            else body="あとで読むために保存した。\n\n保存したことは覚えている。\n\n何の記事かは覚えていない。";
        }
        else if(_selected==0)
        {
            bool deleted=_bugReturnsAt>Time.unscaledTime;
            body=deleted?"削除しました。\n\n……。":_bugReturnsAt>=0?"バグが戻ってきました。\n\n「まだ再現します」":"昨日、確かに消したはず。\n\nなぜかここにいる。";
            if(!deleted) button="完全に削除する";
        }
        else
        {
            body=_motivation?"復元しました。\n\n保存先：どこか\n\n必要なときに見つからない。":"いつ捨てたのか思い出せない。\n\nまだ使えるかもしれない。";
            if(!_motivation) button="やる気を復元する";
        }
        PixelGui.TextFit(new Rect(r.x,r.y,r.width,25),title,12,MacDesktopGui.Ink);
        PixelGui.Text(new Rect(r.x,r.y+34,r.width,Mathf.Max(50,r.height-76)),body,12,MacDesktopGui.Muted,TextAnchor.UpperLeft,true);
        if(button!=null&&MacDesktopGui.Button(new Rect(r.x,r.yMax-30,r.width,28),button,true))
        {
            if(_folder==0) _versions++;
            else if(_folder==1&&_selected==0) _zipDepth++;
            else if(_folder==1) _installed=true;
            else if(_selected==0) _bugReturnsAt=Time.unscaledTime+1.5f;
            else _motivation=true;
        }
    }
}
