using UnityEngine;

/// <summary>
/// Unity のシーンビューで物を選んだときに出る、移動ツールの3本の矢印（赤 X・緑 Y・青 Z）と、面を動かす小さな四角（2026-11-19）。
/// Unity スキルのボス版で、オブジェクトが出てくる位置の予告に使う。ドット絵ではなく、三角形のメッシュでなめらかに描く。
/// 原点＝矢印の付け根。出るときに少し大きくなり、予告のあいだ軽く点滅する
/// </summary>
public class MoveGizmo : MonoBehaviour
{
    // Unity エディタの軸の色
    static readonly Color AxisX = new Color(0.859f, 0.243f, 0.114f);
    static readonly Color AxisY = new Color(0.604f, 0.953f, 0.282f);
    static readonly Color AxisZ = new Color(0.227f, 0.478f, 0.973f);

    // 画面の上での向き（シーンビューを少し上・右から見たとき。Z は手前の左下へ）
    static readonly Vector2 DirX = new Vector2(1f, -0.16f).normalized;
    static readonly Vector2 DirY = Vector2.up;
    static readonly Vector2 DirZ = new Vector2(-0.72f, -0.5f).normalized * 0.8f;

    const float ShaftWidth = 0.035f;  // 軸の太さ（大きさに対して）
    const float HeadLength = 0.24f;   // 矢じりの長さ
    const float HeadWidth = 0.075f;   // 矢じりの半分の幅
    const float PlaneOffset = 0.16f;  // 面の四角の位置
    const float PlaneSize = 0.2f;     // 面の四角の大きさ
    const float PopTime = 0.12f;

    static Material _material;

    MeshRenderer _renderer;
    Mesh _mesh;
    float _start;

    /// <summary>parent の原点に矢印を出す。size：矢印の長さ（マス）</summary>
    public static MoveGizmo Create(Transform parent, float size, int sortingOrder)
    {
        var go = new GameObject("MoveGizmo");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var gizmo = go.AddComponent<MoveGizmo>();
        gizmo._mesh = BuildMesh(size);
        go.AddComponent<MeshFilter>().sharedMesh = gizmo._mesh;
        gizmo._renderer = go.AddComponent<MeshRenderer>();
        gizmo._renderer.sharedMaterial = SharedMaterial();
        gizmo._renderer.sortingOrder = sortingOrder;
        gizmo._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gizmo._renderer.receiveShadows = false;
        gizmo._start = Time.time;
        go.transform.localScale = Vector3.one * 0.6f;
        return gizmo;
    }

    static Material SharedMaterial()
    {
        if (_material == null)
        {
            _material = new Material(Shader.Find("Sprites/Default")) { name = "MoveGizmo" };
        }
        return _material;
    }

    void Update()
    {
        float t = Time.time - _start;
        float k = Mathf.Clamp01(t / PopTime);
        transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - (1f - k) * (1f - k));
        // 軽く点滅（消えはしない）
        if (_renderer != null) _renderer.enabled = t < PopTime || Mathf.FloorToInt((t - PopTime) / 0.12f) % 4 != 3;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    /// <summary>矢印3本と面の四角3つを、1つのメッシュにする（頂点の色で塗る）</summary>
    public static Mesh BuildMesh(float size)
    {
        var b = new Builder();
        // 面の四角（その面に垂直な軸の色。半透明）。奥から描く
        b.Quad(PlaneOffset * size, PlaneSize * size, DirX, DirZ, WithAlpha(AxisY, 0.55f));
        b.Quad(PlaneOffset * size, PlaneSize * size, DirY, DirZ, WithAlpha(AxisX, 0.55f));
        b.Quad(PlaneOffset * size, PlaneSize * size, DirX, DirY, WithAlpha(AxisZ, 0.55f));
        // 矢印（Z → X → Y の順に重ねる）
        b.Arrow(DirZ, size * DirZ.magnitude, size, AxisZ);
        b.Arrow(DirX, size, size, AxisX);
        b.Arrow(DirY, size, size, AxisY);
        return b.ToMesh();
    }

    static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    class Builder
    {
        readonly System.Collections.Generic.List<Vector3> _v = new System.Collections.Generic.List<Vector3>();
        readonly System.Collections.Generic.List<Color> _c = new System.Collections.Generic.List<Color>();
        readonly System.Collections.Generic.List<int> _t = new System.Collections.Generic.List<int>();

        void Tri(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            _c.Add(color); _c.Add(color); _c.Add(color);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2); // Sprites/Default は裏も描くので、片面でよい
        }

        public void Quad(float offset, float side, Vector2 u, Vector2 v, Color color)
        {
            Vector2 un = u.normalized, vn = v.normalized;
            Vector2 p0 = (un + vn) * offset;
            Vector2 p1 = p0 + un * side, p2 = p0 + un * side + vn * side, p3 = p0 + vn * side;
            Tri(p0, p1, p2, color);
            Tri(p0, p2, p3, color);
        }

        /// <summary>原点から dir の向きに length の長さの矢印。太さは size に合わせる</summary>
        public void Arrow(Vector2 dir, float length, float size, Color color)
        {
            Vector2 d = dir.normalized;
            Vector2 n = new Vector2(-d.y, d.x);
            float w = ShaftWidth * size * 0.5f;
            float head = HeadLength * size;
            Vector2 neck = d * Mathf.Max(0f, length - head);
            Vector2 tip = d * length;
            Tri(-n * w, neck - n * w, neck + n * w, color);
            Tri(-n * w, neck + n * w, n * w, color);
            float hw = HeadWidth * size;
            Tri(neck - n * hw, tip, neck + n * hw, color);
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = "MoveGizmo" };
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
