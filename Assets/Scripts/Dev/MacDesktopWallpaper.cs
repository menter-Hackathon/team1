using UnityEngine;
using UnityEngine.UI;

/// <summary>元の壁紙を通常の画像 UI として表示。Legacy GUI の色変換を通さない。</summary>
public sealed class MacDesktopWallpaper : MonoBehaviour
{
    Behaviour _owner;
    System.Func<bool> _visible;
    Canvas _canvas;
    RawImage _image;

    public static MacDesktopWallpaper Ensure(Behaviour owner,System.Func<bool> visible=null)
    {
        var wallpaper=owner.GetComponent<MacDesktopWallpaper>();
        if(wallpaper==null) wallpaper=owner.gameObject.AddComponent<MacDesktopWallpaper>();
        wallpaper._owner=owner;
        wallpaper._visible=visible;
        wallpaper.Refresh();
        return wallpaper;
    }

    void LateUpdate() => Refresh();

    public void Refresh()
    {
        bool visible=_owner!=null&&_owner.isActiveAndEnabled&&(_visible==null||_visible());
        Camera camera=Camera.main;
        if(!visible||camera==null)
        {
            if(_canvas!=null) _canvas.enabled=false;
            return;
        }
        if(_canvas==null)
        {
            var root=new GameObject("Desktop wallpaper (original PNG)",typeof(RectTransform),typeof(Canvas),typeof(RawImage));
            root.transform.SetParent(transform,false);
            _canvas=root.GetComponent<Canvas>();
            // カメラ内に描き、既存の OnGUI ウィンドウより奥に置く。
            // Overlay Canvas は OnGUI と表示順が競合するため使用しない。
            _canvas.renderMode=RenderMode.ScreenSpaceCamera;
            _canvas.overrideSorting=true;
            _canvas.sortingOrder=short.MinValue;
            _image=root.GetComponent<RawImage>();
            _image.texture=Resources.Load<Texture2D>("DesktopWallpaper");
            _image.color=Color.white;
            _image.raycastTarget=false;
            _image.maskable=false;
        }
        _canvas.enabled=true;
        _canvas.worldCamera=camera;
        _canvas.targetDisplay=camera.targetDisplay;
        _canvas.planeDistance=camera.nearClipPlane+0.01f;
        if(_image.texture!=null)
            _image.uvRect=MacDesktopGui.CoverUv(camera.pixelWidth,camera.pixelHeight,
                _image.texture.width,_image.texture.height);
    }

    void OnDisable()
    {
        if(_canvas!=null) _canvas.enabled=false;
    }
}
