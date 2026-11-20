using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>macOS の LocalAuthentication による認証。指紋そのものは扱わない。</summary>
public static class MacTouchId
{
    public enum Availability { Ready=0, Unavailable=1, NotEnrolled=2, LockedOut=3, PluginMissing=4 }
    public enum Result { Success=0, Cancelled=1, Unavailable=2, Failed=3, EmbeddedViewUnavailable=4 }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void NativeReply(int request,int result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int IntCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void VoidCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void BeginCall(int request,NativeReply reply,int embedded);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void RectCall(double x,double y,double width,double height);
    static readonly NativeReply Reply=OnReply;
    static TaskCompletionSource<Result> _pending;
    static int _request;
    static IntPtr _library;
    static IntCall _availability;
    static VoidCall _cancel;
    static BeginCall _begin;
    static RectCall _position;
    static bool _reportedLoadError;
    public static string LoadError { get; private set; }
    const string LibraryName="libTeam1TouchIdEmbedded.dylib";

    [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="dlopen")]
    static extern IntPtr OpenLibrary([MarshalAs(UnmanagedType.LPUTF8Str)] string path,int flags);
    [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="dlsym")]
    static extern IntPtr FindSymbol(IntPtr library,[MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="dlerror")]
    static extern IntPtr LibraryError();

    static bool IsMac => Application.platform==RuntimePlatform.OSXEditor||Application.platform==RuntimePlatform.OSXPlayer;

    static T Bind<T>(IntPtr library,string name) where T:Delegate
    {
        IntPtr symbol=FindSymbol(library,name);
        if(symbol==IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return Marshal.GetDelegateForFunctionPointer<T>(symbol);
    }

    // Editor のプラグイン名キャッシュに依存せず、ビルドでも実際の配置先から読む。
    internal static bool LoadFromPaths(string[] paths)
    {
        if(_library!=IntPtr.Zero) return true;
        LoadError="Touch IDプラグインが見つかりません";
        foreach(string path in paths)
        {
            if(!File.Exists(path)) continue;
            IntPtr library=OpenLibrary(path,2|4); // RTLD_NOW | RTLD_LOCAL
            if(library==IntPtr.Zero)
            {
                LoadError=Marshal.PtrToStringAnsi(LibraryError())??"ネイティブライブラリの読み込みに失敗しました";
                continue;
            }
            try
            {
                var version=Bind<IntCall>(library,"Team1TouchIdBridgeVersion");
                if(version()!=4) { LoadError="Touch IDプラグインのバージョンが一致しません"; continue; }
                _availability=Bind<IntCall>(library,"Team1TouchIdAvailability");
                _cancel=Bind<VoidCall>(library,"Team1TouchIdCancel");
                _position=Bind<RectCall>(library,"Team1TouchIdSetViewRect");
                _begin=Bind<BeginCall>(library,"Team1TouchIdBegin");
                _library=library; LoadError=null;
                // 認証の非同期処理がコードを参照するため、ドメイン再読み込み時にも dlclose しない。
                return true;
            }
            catch(EntryPointNotFoundException error) { LoadError=error.Message; }
        }
        return false;
    }

    static bool EnsureLoaded()
    {
        if(!IsMac) return false;
        if(_library!=IntPtr.Zero) return true;
        string data=Application.dataPath;
        string[] paths=Application.isEditor?new[]{Path.Combine(data,"Plugins","macOS",LibraryName)}:new[]{
            Path.Combine(data,"Plugins",LibraryName),Path.Combine(data,"PlugIns",LibraryName),
            Path.Combine(data,"Plugins","macOS",LibraryName),Path.Combine(data,"Frameworks",LibraryName)};
        if(LoadFromPaths(paths)) return true;
        if(!_reportedLoadError) { _reportedLoadError=true; Debug.LogWarning("Touch ID: "+LoadError); }
        return false;
    }

    public static Availability CheckAvailability()
    {
        if(!IsMac) return Availability.Unavailable;
        return EnsureLoaded()?(Availability)_availability():Availability.PluginMissing;
    }

    public static Task<Result> AuthenticateAsync()
    {
        if(_pending!=null) return _pending.Task;
        if(CheckAvailability()!=Availability.Ready) return Task.FromResult(Result.Unavailable);
        var completion=new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending=completion;
        int request=++_request;
        try { _begin(request,Reply,1); }
        catch(Exception)
        { _pending=null; completion.TrySetResult(Result.Failed); throw; }
        return completion.Task;
    }

    public static void PositionView(Rect screenRect)
    {
        if(EnsureLoaded()) _position(screenRect.x,screenRect.y,screenRect.width,screenRect.height);
    }

    internal static Rect MapGameRect(Rect local,Vector2 renderSize,Rect gameScreen)
    {
        float sx=gameScreen.width/Mathf.Max(1,renderSize.x),sy=gameScreen.height/Mathf.Max(1,renderSize.y);
        return new Rect(gameScreen.x+local.x*sx,gameScreen.y+local.y*sy,local.width*sx,local.height*sy);
    }

    public static void PositionInGameView(Rect localRect)
    {
#if UNITY_EDITOR
        // ゲームのピクセル座標と Editor のウィンドウ座標は異なる。
        // Game ビューの実際の描画領域へ変換し、ウィンドウ最下部へのフォールバックを避ける。
        if(TryGetGameScreen(out Rect gameScreen))
        {
            PositionView(MapGameRect(localRect,new Vector2(Screen.width,Screen.height),gameScreen));
            return;
        }
#endif
        Vector2 top=GUIUtility.GUIToScreenPoint(localRect.position);
        Vector2 bottom=GUIUtility.GUIToScreenPoint(localRect.position+localRect.size);
        PositionView(new Rect(top,bottom-top));
    }

#if UNITY_EDITOR
    static UnityEditor.EditorWindow _gameView;
    static bool TryGetGameScreen(out Rect screen)
    {
        screen=default;
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if(type==null) return false;
        var focused=UnityEditor.EditorWindow.focusedWindow;
        if(focused!=null&&focused.GetType()==type) _gameView=focused;
        if(_gameView==null)
            foreach(var view in Resources.FindObjectsOfTypeAll(type))
                if(view is UnityEditor.EditorWindow window) { _gameView=window; break; }
        if(_gameView==null) return false;
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        var targetProperty=type.GetProperty("targetInView",flags);
        var viewProperty=type.GetProperty("viewInWindow",flags);
        if(targetProperty==null||viewProperty==null) return false;
        Rect target=(Rect)targetProperty.GetValue(_gameView);
        Rect viewport=(Rect)viewProperty.GetValue(_gameView);
        if(target.width<=0||target.height<=0) return false;
        screen=new Rect(_gameView.position.position+viewport.position+target.position,target.size);
        return true;
    }
#endif

    [AOT.MonoPInvokeCallback(typeof(NativeReply))]
    static void OnReply(int request,int result)
    {
        if(request!=_request) return;
        var pending=_pending; _pending=null;
        pending?.TrySetResult(result>=0&&result<=4?(Result)result:Result.Failed);
    }

    public static void Cancel()
    {
        var pending=_pending; _pending=null; _request++;
        if(pending==null) return;
        if(_library!=IntPtr.Zero) _cancel();
        pending.TrySetResult(Result.Cancelled);
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void RegisterEditorCleanup()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload-=Cancel;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload+=Cancel;
    }
#endif
}
