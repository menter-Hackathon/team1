using UnityEngine;

/// <summary>Figmaの付箋と選択ハンドル。全て既存のドット密度で描画する。</summary>
public sealed class FigmaDesignAttack : MonoBehaviour
{
    public enum DesignKind { StickyNote, Selection, PlayerSticky }
    public DesignKind Kind { get; private set; }
    public bool Friendly { get; private set; }
    public bool Finished { get; private set; }
    public bool Dangerous { get; private set; }
    public bool Hit { get; set; }
    public Vector2 Previous { get; private set; }
    public Vector2 Position => transform.position;
    public Vector2 Anchor => Kind==DesignKind.PlayerSticky?Position:_center;

    public const float PaperScale = 0.7f;
    public const float PlayerStickyDelay = 1.5f;
    public const float EnemyStickyDelay = 2.2f;

    static readonly Color32 Dark = new Color32(40, 27, 58, 255);
    static readonly Color32 FigmaBlue = new Color32(13,153,255,255);
    static readonly Color32 FigmaPurple = new Color32(168,90,255,255);
    IllustratorPixelCanvas _canvas;
    Vector2 _center;
    Vector3[] _handles, _previousHandles;
    float _born, _warningTime, _duration;
    int _frame = -1;
    bool _wasDangerous, _attached;
    Color32 _paperColor;

    public static Vector2 StickyPosition(int lane, float horizontal, float vertical)
    {
        float x = -6.4f + Mathf.Clamp(lane,0,2)*6.4f + Mathf.Lerp(-1.25f,1.25f,horizontal);
        float y = vertical < 0.45f ? 0.85f : Mathf.Lerp(1.4f,5.8f,(vertical-0.45f)/0.55f);
        return IllustratorPixelCanvas.Snap(new Vector2(x,y));
    }

    public static Vector2 SelectionHalfSize => new Vector2(2.65f,1.95f);
    public Bounds PlacementBounds => new Bounds(Anchor,Kind==DesignKind.Selection
        ?(Vector3)(SelectionHalfSize*2f):new Vector3(1.6f,1.6f,0));

    public static Vector2 ClampSpawnPoint(Vector2 point,Vector2 halfSize)
    {
        point.x=Mathf.Clamp(point.x,-9.7f+halfSize.x,9.7f-halfSize.x);
        point.y=Mathf.Clamp(point.y,0.15f+halfSize.y,8.55f-halfSize.y);
        return IllustratorPixelCanvas.Snap(point);
    }

    public static bool OverlapsEnemy(Bounds area,Bounds enemy,float padding=0.6f)
    {
        return area.min.x<enemy.max.x+padding&&area.max.x>enemy.min.x-padding&&
            area.min.y<enemy.max.y+padding&&area.max.y>enemy.min.y-padding;
    }

    public static Vector3[] SelectionHandlePositions(Vector2 center,float progress,float pull=0f)
    {
        float t=Mathf.Clamp01(progress);
        t=t*t*t; // 溜めてから加速し、最後に一気に縮む。
        Vector2 half=Vector2.Lerp(new Vector2(2.2f,1.5f)+new Vector2(0.22f,0.18f)*Mathf.Clamp01(pull),Vector2.one*0.3f,t);
        return new[] { (Vector3)(center+new Vector2(-half.x,-half.y)),
            (Vector3)(center+new Vector2(half.x,-half.y)),(Vector3)(center+half),
            (Vector3)(center+new Vector2(-half.x,half.y)) };
    }

    public void InitializeSelection(Vector2 center)
    {
        Kind=DesignKind.Selection; _center=IllustratorPixelCanvas.Snap(center);
        _born=Time.time; _warningTime=1.2f; _duration=1.1f;
        _handles=SelectionHandlePositions(_center,0f);
        _previousHandles=(Vector3[])_handles.Clone();
        _canvas=new IllustratorPixelCanvas(transform,new[] { (Vector3)(_center-SelectionHalfSize),
            (Vector3)(_center+SelectionHalfSize) },0.15f,20);
        Tick();
    }

    // 右上を折り返した紙。描画と当たり判定で同じ輪郭を使う。
    public static Vector3[] StickyOutline(Vector2 center) => new[]
    {
        (Vector3)(center + (new Vector2(-0.8f, -0.7f) * PaperScale)),
        (Vector3)(center + (new Vector2(0.8f, -0.7f) * PaperScale)),
        (Vector3)(center + (new Vector2(0.8f, 0.38f) * PaperScale)),
        (Vector3)(center + (new Vector2(0.48f, 0.7f) * PaperScale)),
        (Vector3)(center + (new Vector2(-0.8f, 0.7f) * PaperScale))
    };

    public void InitializeSticky(Vector2 center, int colorIndex, float stagger)
    {
        Kind = DesignKind.StickyNote; _center = IllustratorPixelCanvas.Snap(center);
        Color32[] palette = { new Color32(255,216,91,255), new Color32(249,151,193,255),
            new Color32(119,216,231,255), new Color32(191,166,246,255) };
        _paperColor = palette[Mathf.Abs(colorIndex) % palette.Length];
        _born = Time.time + Mathf.Max(0, stagger); _warningTime = EnemyStickyDelay; _duration = 0.3f;
        _canvas = new IllustratorPixelCanvas(transform, new[] {
            (Vector3)(_center - new Vector2(2.2f, 2.2f)),
            (Vector3)(_center + new Vector2(2.2f, 2.2f)) }, 0.1f, 19);
        _canvas.Clear(); _canvas.Apply();
        Tick();
    }

    public static void DrawStickyPaper(IllustratorPixelCanvas canvas, Vector2 center, Color32 color, int coverage)
    {
        Vector3[] paper = StickyOutline(center);
        var shadow = new Color32(40,27,58,255);
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                Vector3 point = canvas.World(x,y);
                if (IllustratorGeometry.Contains(paper, point + (new Vector3(-0.11f,0.11f) * PaperScale)))
                    canvas.Dot(x,y,shadow,coverage);
                if (IllustratorGeometry.Contains(paper, point)) canvas.Dot(x,y,color,coverage);
            }
        canvas.Path(paper,paper.Length,1,shadow,coverage,true);
        Vector3 fold = center + (new Vector2(0.48f,0.38f) * PaperScale);
        Color32 underside = new Color32((byte)(color.r*0.73f),(byte)(color.g*0.73f),(byte)(color.b*0.73f),255);
        for(int y=0;y<canvas.Height;y++) for(int x=0;x<canvas.Width;x++)
        {
            Vector3 p=canvas.World(x,y);
            if(p.x>=fold.x&&p.y>=fold.y&&p.x+p.y<=fold.x+fold.y+0.32f*PaperScale)
                canvas.Dot(x,y,underside,coverage);
        }
        canvas.Line(fold,fold+Vector3.right*(0.32f*PaperScale),1,shadow,coverage);
        canvas.Line(fold,fold+Vector3.up*(0.32f*PaperScale),1,shadow,coverage);
        // 読ませる文字は入れず、手書きメモの短い線だけにする。
        for(int row=0;row<3;row++)
        {
            Vector3 a=center+(new Vector2(-0.52f,0.22f-row*0.28f) * PaperScale);
            canvas.Line(a,a+Vector3.right*(row==1?0.64f:0.94f)*PaperScale,1,underside,coverage);
        }
    }

    public static bool StickyTrapTouches(Bounds target, Vector2 position)
    {
        return IllustratorGeometry.PolygonHits(StickyOutline(position),target);
    }

    public static bool StickyBurstHits(Bounds target, Vector2 position)
    {
        Vector2 nearest = new Vector2(Mathf.Clamp(position.x,target.min.x,target.max.x),
            Mathf.Clamp(position.y,target.min.y,target.max.y));
        return (nearest-position).sqrMagnitude <= 1.35f*1.35f;
    }

    public void InitializePlayerSticky(Vector2 origin)
    {
        Kind=DesignKind.PlayerSticky; Friendly=true;
        _center=Vector2.zero; _paperColor=new Color32(255,216,91,255);
        _born=Time.time; _warningTime=PlayerStickyDelay; _duration=0.3f;
        _canvas=new IllustratorPixelCanvas(transform,new[] {new Vector3(-2.2f,-2.2f),new Vector3(2.2f,2.2f)},0.1f,24);
        transform.position=IllustratorPixelCanvas.Snap(origin); Previous=transform.position;
        Tick();
    }

    public bool TryAttachTo(Transform target, Bounds bounds)
    {
        float elapsed=Time.time-_born;
        if(Kind!=DesignKind.PlayerSticky||_attached||Hit||elapsed<0.25f||elapsed>=_warningTime||!StickyTrapTouches(bounds,Position)) return false;
        Vector2 point=bounds.ClosestPoint(Position);
        Dangerous=false; _attached=true;
        transform.position=IllustratorPixelCanvas.Snap(point);
        transform.SetParent(target,true);
        float remaining=Mathf.Min(0.65f,_warningTime-elapsed);
        _born=Time.time; _warningTime=remaining; _duration=0.3f; _frame=-1;
        return true;
    }

    public void Tick()
    {
        float elapsed = Time.time - _born, active = elapsed - _warningTime;
        if (elapsed < 0f) return; // 後続の付箋は予告も含めて時間差で出現する。
        Dangerous = active >= 0f && active < _duration;
        if(Kind==DesignKind.Selection) Dangerous=active>=0f&&active<0.65f;
        Finished = active >= _duration + 0.25f;
        Previous = transform.position;
        if(Kind==DesignKind.Selection)
        {
            _previousHandles=_handles;
            float pull=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((elapsed-0.35f)/0.85f));
            _handles=SelectionHandlePositions(_center,Mathf.Clamp01(active/0.65f),pull);
        }
        int frame = Mathf.FloorToInt(elapsed * 12f);
        if (frame == _frame && Dangerous == _wasDangerous && !(Kind==DesignKind.Selection&&Dangerous)) return;
        _frame = frame; _wasDangerous = Dangerous;
        _canvas.Clear();
        float fade = Mathf.Clamp01((_duration + 0.25f - active) / 0.25f);
        if (Kind == DesignKind.Selection) DrawSelection(active, fade);
        else DrawSticky(active, fade);
        _canvas.Apply();
    }

    public static void DrawSelectionHandles(IllustratorPixelCanvas canvas,Vector3[] handles,int coverage,bool active)
    {
        // 選択枠は細い補助線。ダメージを与えるのは四隅のハンドルだけ。
        canvas.Path(handles,handles.Length,active?2:1,FigmaBlue,coverage,true);
        for(int i=0;i<handles.Length;i++)
        {
            Vector3 point=handles[i];
            Color32 color=(i&1)==0?FigmaBlue:FigmaPurple;
            canvas.Line(point,point,7,Dark,coverage);
            canvas.Line(point,point,5,color,coverage);
            canvas.Line(point,point,3,new Color32(235,247,255,255),coverage);
        }
    }

    void DrawSelection(float active,float fade)
    {
        if(active>=0.65f)
        {
            float t=Mathf.Clamp01((active-0.65f)/0.45f);
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4f;
                Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));
                Vector3 point=(Vector3)_center+direction*Mathf.Lerp(0.45f,1.65f,t);
                _canvas.Line(point,point+direction*0.3f,2,(i&1)==0?FigmaBlue:FigmaPurple,
                    Mathf.CeilToInt(16f*(1f-t)));
            }
            return;
        }
        if(active>0f)
        {
            Vector3[] trail=SelectionHandlePositions(_center,Mathf.Clamp01(active/0.65f-0.14f),1f);
            for(int i=0;i<4;i++) _canvas.Line(trail[i],_handles[i],2,FigmaPurple,6);
        }
        DrawSelectionHandles(_canvas,_handles,Mathf.CeilToInt(fade*16),active>=0f);
    }

    public static bool SelectionHandlesHit(Bounds target,Vector3[] previous,Vector3[] current)
    {
        for(int i=0;i<current.Length;i++)
            if(IllustratorGeometry.SegmentHits(target,previous[i],current[i],0.2f)) return true;
        return false;
    }

    void DrawSticky(float active, float fade)
    {
        if(active>=0f)
        {
            DrawStickyBurst(_canvas,Mathf.Clamp01(active/0.3f),Mathf.CeilToInt(fade*16),_center);
            return;
        }
        int coverage=Time.time-_born<0.25f?3:16;
        DrawStickyPaper(_canvas,_center,_paperColor,coverage);
            Vector3 a=_center+new Vector2(-0.67f,-0.85f)*PaperScale;
            _canvas.Line(a,a+Vector3.right*(1.34f*PaperScale),2,Dark);
            _canvas.Line(a,a+Vector3.right*(1.34f*PaperScale*Mathf.Clamp01(1f+active/_warningTime)),1,
                Friendly?FigmaBlue:new Color32(210,70,102,255));
    }

    public static void DrawStickyBurst(IllustratorPixelCanvas canvas,float progress,int coverage,Vector2 center=default)
    {
        Color32[] colors={new Color32(255,216,91,255),new Color32(249,151,193,255),
            new Color32(119,216,231,255),new Color32(191,166,246,255)};
        // 紙が四方へ剥がれる。全面を光らせず、小さな紙片だけを広げる。
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));
            Vector3 point=(Vector3)center+direction*Mathf.Lerp(0.4f,1.9f,progress);
            canvas.Line(point-direction*0.2f,point+direction*0.2f,3,colors[i%colors.Length],coverage);
        }
    }

    public bool Intersects(Bounds target)
    {
        if (!Dangerous || Hit) return false;
        if (Kind == DesignKind.Selection) return SelectionHandlesHit(target,_previousHandles,_handles);
        return StickyBurstHits(target,Anchor);
    }

    void OnDestroy() { _canvas?.Dispose(); }
}
