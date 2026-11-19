using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 【仮】3D の物体（2026-11-19。docs/app-kits-2-design.md X1）。担当A の物。Unity・Blender・Minecraft が使う。
// ・当たり判定は 2D のまま。2D のコライダーを持つ親に、見た目だけの MeshRenderer の子（Kit3DVisual）を付ける。ドット絵にしない
// ・形は立方体・球・カプセル・円柱。いくつか組み合わせた形（サルの頭・TNT の帯）も作れる（Kit3DVisual.AddPart）
// ・向きは上の面が見えるように少し回す（x -20°・y 30°。Unity の向きは +20 だと下の面が見えるので、符号を逆にした）
// ・明かり：平行光を2つ（主の光と、カメラ側からの弱い光）、最初の物を出すときに作る。影は切る。スプライトは光を受けないので、ほかの絵は変わらない
// ・重なり：3D の物は z を手前（大きさの半径ぶん）に置く。メインのカメラ（正投影・z -10・near 0.3）にそのまま映り、
//   深さの書き込みで、z 0 のスプライトより手前に見える（プレイヤーが後ろに隠れることがある）。HUD（OnGUI）と文字（TextMesh）は上に出る
// ・シェーダー：Resources の Standard のマテリアル（Kits/Kit3D_Standard。ビルダーが作る）→ Standard → Legacy Shaders/Diffuse →
//   Hidden/Internal-Colored → Sprites/Default の順に探す。
//   ビルドで確かめた（2026-11-19）：Standard は Always Included に無く、今のビルドにも入っていない。Legacy Shaders/Diffuse は Always Included に入っている。
//   Resources のマテリアルが無いビルドでは Legacy Shaders/Diffuse になる。光を受けないシェーダーのときは、陰を頂点の色に焼き込む（逃げ道）
// ・不透明のシェーダーなので、うすくして消すことはできない。消えるときは点滅してから縮む（割り切り）
// ・数の上限：組（group）ごとに数える（既定 8）。越えたら古い物から消す。落ちている途中・予告の間の物は消さない

/// <summary>3D の物体の形</summary>
public enum Kit3DShape { Cube, Sphere, Capsule, Cylinder }

/// <summary>3D の物体の入口</summary>
public static class Kit3D
{
    /// <summary>1ドット（マス）</summary>
    public const float Px = 1f / 18f;
    /// <summary>既定の数の上限（組ごと）</summary>
    public const int DefaultCap = 8;
    /// <summary>既定の向き（上の面・前の面・右の面が見える）</summary>
    public static readonly Vector3 Tilt = new Vector3(-20f, 30f, 0f);
    /// <summary>3D の物の並び順（光を受けないシェーダーのときだけ効く）</summary>
    public const int SortingOrder = 40;

    /// <summary>Standard のマテリアル（Resources の中の場所。ビルダーが作る）</summary>
    public const string StandardMaterialResource = "Kits/Kit3D_Standard";

    /// <summary>主の光が来る向き（物 → 光。右上の手前から）</summary>
    static readonly Vector3 ToLight = new Vector3(0.3f, 0.7f, -0.65f).normalized;

    enum ShaderMode { None, Standard, Diffuse, Colored, Sprite }

    static ShaderMode _mode = ShaderMode.None;
    static Material _material;
    static Light _key, _fill;
    static readonly Dictionary<Kit3DShape, Mesh> Meshes = new Dictionary<Kit3DShape, Mesh>();
    static readonly List<Kit3DVisual> Live = new List<Kit3DVisual>();

    /// <summary>光を受けるシェーダーか（false なら陰を頂点の色に焼き込んだ物）</summary>
    public static bool IsLit { get { EnsureMaterial(); return _mode == ShaderMode.Standard || _mode == ShaderMode.Diffuse; } }

    /// <summary>今使っているシェーダーの名前（確かめる用）</summary>
    public static string ShaderName { get { EnsureMaterial(); return _material != null && _material.shader != null ? _material.shader.name : "(none)"; } }

    // ───────── 見た目 ─────────

    /// <summary>
    /// 見た目だけの 3D の物を parent の子に付ける（コライダーは付けない）。size はマス（立方体は幅・高さ・奥行き、球は直径、カプセル・円柱は幅・高さ・幅）。
    /// root：上限で消すときに消す物（null なら parent）。group：数の上限の組（null なら数えない）
    /// </summary>
    public static Kit3DVisual AddVisual(Transform parent, Kit3DShape shape, Vector3 size, Color color, GameObject root = null, string group = null, int cap = DefaultCap)
    {
        EnsureMaterial();
        EnsureLight();
        var go = new GameObject("Kit3D");
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        var v = go.AddComponent<Kit3DVisual>();
        v.Init(root != null ? root : parent != null ? parent.gameObject : go, group);
        // 回した立方体は、映ると 2D の箱より 1.5倍ほど横に広く見えるので、少し縮めて箱に合わせる（球はそのまま）
        v.SetFit(shape == Kit3DShape.Cube ? 0.8f : shape == Kit3DShape.Sphere ? 1f : 0.9f);
        v.AddPart(shape, Vector3.zero, size, color);
        Admit(v, cap);
        return v;
    }

    /// <summary>
    /// 3D の物を1つ置く（2D の入れ物＋見た目）。owner を渡すと、その人の出した物になる（ClearSpawned で消える）。
    /// 2D のコライダーは呼び出し元が付ける（返した物の gameObject に）
    /// </summary>
    public static Kit3DVisual Spawn(Kit3DShape shape, Vector2 position, Vector3 size, Color color, AttackRunner owner = null, string group = null, int cap = DefaultCap)
    {
        GameObject root = owner != null ? owner.CreateSpawnedObject($"Kit3D_{shape}", position) : new GameObject($"Kit3D_{shape}");
        root.transform.position = position;
        return AddVisual(root.transform, shape, size, color, root, group, cap);
    }

    /// <summary>
    /// 今の部品（弾・落下物・指定位置の判定）が出した物に、3D の見た目を付ける（元の絵はかくす）。
    /// 弾は進む向きに回るので、3D の物も一緒に回る（転がって見える）
    /// </summary>
    public static Kit3DVisual Dress(GameObject spawned, Kit3DShape shape, Vector3 size, Color color, string group = null, int cap = DefaultCap)
    {
        if (spawned == null) return null;
        SpriteRenderer sr = KitDress.FindVisual(spawned.transform);
        if (sr != null) sr.enabled = false;
        return AddVisual(spawned.transform, shape, size, color, spawned, group, cap);
    }

    /// <summary>z の位置（物の半径ぶん手前。カメラの近い面 -9.7 より奥）</summary>
    public static float ZFor(float radius) => -Mathf.Clamp(radius + 0.25f, 0.5f, 9f);

    // ───────── 予告・落ちる物 ─────────

    /// <summary>
    /// 予告：出る場所に、角だけの赤い枠と「!」（見本の動画と同じ形）。seconds 秒で消える（最後の 0.3秒は点滅）。
    /// owner を渡すと、その人の出した物になる（形態変化・正常化で消える）
    /// </summary>
    public static Kit3DBracket Bracket(AttackRunner owner, Vector2 center, Vector2 size, float seconds, bool bang = true, Color? color = null)
    {
        GameObject go = owner != null ? owner.CreateSpawnedObject("Kit3DBracket", center) : new GameObject("Kit3DBracket");
        go.transform.position = center;
        var b = go.AddComponent<Kit3DBracket>();
        b.Setup(size, seconds, bang, color ?? KitDress.Red);
        return b;
    }

    /// <summary>
    /// 落ちる物：予告（枠と「!」）→ 現れて落ちる（落ちている間だけ当たる）→ 止まると乗れる箱になり、Remain 秒で消える。
    /// owner は当たり判定の持ち主（ボスの AttackRunner・ギミックの ctx.Hazard）。data はダメージ・ノックバック
    /// </summary>
    public static Kit3DFaller Faller(AttackRunner owner, AttackData data, Kit3DShape shape, Vector2 at, Vector3 size, Color color, Kit3DFallOptions opt)
        => Kit3DFaller.Spawn(owner, data, shape, at, size, color, opt);

    // ───────── 道具 ─────────

    /// <summary>乗れる地形（床・足場・置いた箱）のレイヤー</summary>
    public static int GroundMask => LayerMask.GetMask("Ground", "OneWayPlatform");
    /// <summary>ぶつかる地形（床・壁・置いた箱）のレイヤー</summary>
    public static int SolidMask => LayerMask.GetMask("Ground");

    /// <summary>
    /// x の位置で、fromY から下の床の上面を探す。platformOk が false なら、最初に当たったのが足場（すり抜け床）・置いた 3D の箱なら false
    /// </summary>
    public static bool FindFloor(float x, float fromY, out float floorY, bool platformOk = true)
    {
        floorY = 0f;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(GroundMask);
        var hits = new RaycastHit2D[6];
        int n = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, filter, hits, 40f);
        float best = float.PositiveInfinity;
        int bi = -1;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0f) continue;
            if (hits[i].distance < best) { best = hits[i].distance; bi = i; }
        }
        if (bi < 0) return false;
        floorY = hits[bi].point.y;
        if (platformOk) return true;
        Collider2D c = hits[bi].collider;
        if (c.gameObject.layer == LayerMask.NameToLayer("OneWayPlatform")) return false;
        if (c.GetComponentInParent<Kit3DVisual>() != null || c.GetComponentInParent<Kit3DFaller>() != null) return false;
        return true;
    }

    /// <summary>雑魚にダメージを入れる（ボス・ウイルス本体には入れない）。倒した数に入る。当てた数を返す</summary>
    public static int StrikeZako(Vector2 center, float radius, int amount, GameObject source)
    {
        int n = 0;
        foreach (Health h in RewardKit.ZakoInRadius(center, radius))
        {
            if (RewardKit.Strike(h, amount, center, source, SkinId.None, HitStrength.Heavy, null, null)) n++;
        }
        return n;
    }

    /// <summary>その範囲に、プレイヤー・雑魚・ボスの体（トリガーでないコライダー）が重なっているか（ignore の子は除く）</summary>
    public static bool BodyOverlaps(Vector2 center, Vector2 size, Transform ignore = null)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Player", "Enemy"));
        var res = new Collider2D[8];
        int n = Physics2D.OverlapBox(center, size, 0f, filter, res);
        for (int i = 0; i < n; i++)
        {
            if (res[i] == null) continue;
            if (ignore != null && res[i].transform.IsChildOf(ignore)) continue;
            return true;
        }
        // ボスの体（被弾範囲）
        return BossOverlaps(center, size);
    }

    static BossController[] _bosses = new BossController[0];
    static float _bossesAt = -10f;

    // シーンのボス（0.5秒ごとに探し直す。毎フレーム探さない）
    static BossController[] Bosses()
    {
        if (Time.time - _bossesAt > 0.5f || Array.Exists(_bosses, b => b == null))
        {
            _bosses = UnityEngine.Object.FindObjectsOfType<BossController>();
            _bossesAt = Time.time;
        }
        return _bosses;
    }

    /// <summary>ボスの体と重なっているか</summary>
    public static bool BossOverlaps(Vector2 center, Vector2 size)
    {
        foreach (var b in Bosses())
        {
            if (b == null || !b.isActiveAndEnabled || b.State == BossState.Normalized) continue;
            Bounds bb = b.BodyBounds;
            if (Mathf.Abs(bb.center.x - center.x) < bb.extents.x + size.x * 0.5f && Mathf.Abs(bb.center.y - center.y) < bb.extents.y + size.y * 0.5f) return true;
        }
        return false;
    }

    // ───────── 数の上限 ─────────

    static void Admit(Kit3DVisual v, int cap)
    {
        Live.RemoveAll(x => x == null);
        if (!string.IsNullOrEmpty(v.Group))
        {
            int count = 0;
            foreach (var x in Live) if (x.Group == v.Group) count++;
            while (count >= Mathf.Max(1, cap))
            {
                Kit3DVisual oldest = null;
                foreach (var x in Live)
                {
                    if (x.Group != v.Group || x.Busy || x.Root == null) continue;
                    if (oldest == null || x.BornAt < oldest.BornAt) oldest = x;
                }
                if (oldest == null) break; // 全部が落ちている途中なら、越えてもよい
                Live.Remove(oldest);
                UnityEngine.Object.Destroy(oldest.Root);
                count--;
            }
        }
        Live.Add(v);
    }

    internal static void Forget(Kit3DVisual v) => Live.Remove(v);

    /// <summary>その組の今の数</summary>
    public static int CountOf(string group)
    {
        Live.RemoveAll(x => x == null);
        int n = 0;
        foreach (var x in Live) if (x.Group == group) n++;
        return n;
    }

    // ───────── 材質・形・光 ─────────

    internal static Material Material { get { EnsureMaterial(); return _material; } }

    static void EnsureMaterial()
    {
        if (_material != null) return;
        // ビルダー（Editor/Kits/UnityKitAssets.cs の Kit3DAssets）が Resources に置いた Standard のマテリアル。Resources の物が指すシェーダーはビルドに入る
        Material baseMat = Resources.Load<Material>(StandardMaterialResource);
        if (baseMat != null && baseMat.shader != null && baseMat.shader.isSupported)
        {
            _mode = ShaderMode.Standard;
            _material = new Material(baseMat) { name = "Kit3D_Standard", hideFlags = HideFlags.DontSave };
            Debug.Log($"[Kit3D] シェーダー：{_material.shader.name}（Resources）");
            return;
        }
        Shader s = Shader.Find("Standard");
        ShaderMode mode = ShaderMode.Standard;
        if (s == null || !s.isSupported) { s = Shader.Find("Legacy Shaders/Diffuse"); mode = ShaderMode.Diffuse; }
        if (s == null || !s.isSupported) { s = Shader.Find("Hidden/Internal-Colored"); mode = ShaderMode.Colored; }
        if (s == null || !s.isSupported) { s = Shader.Find("Sprites/Default"); mode = ShaderMode.Sprite; }
        if (s == null) { mode = ShaderMode.None; return; }
        _mode = mode;
        _material = new Material(s) { name = "Kit3D_" + mode, hideFlags = HideFlags.DontSave };
        if (mode == ShaderMode.Standard)
        {
            if (_material.HasProperty("_Glossiness")) _material.SetFloat("_Glossiness", 0.15f);
            if (_material.HasProperty("_Metallic")) _material.SetFloat("_Metallic", 0f);
        }
        else if (mode == ShaderMode.Colored)
        {
            _material.SetInt("_SrcBlend", (int)BlendMode.One);
            _material.SetInt("_DstBlend", (int)BlendMode.Zero);
            _material.SetInt("_Cull", (int)CullMode.Back);
            _material.SetInt("_ZWrite", 1);
            _material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            _material.renderQueue = 2000;
        }
        Debug.Log($"[Kit3D] シェーダー：{s.name}");
    }

    static void EnsureLight()
    {
        if (!IsLit) return;
        if (_key == null) _key = MakeLight("Kit3DLight", Quaternion.LookRotation(-ToLight), 1.0f);
        // カメラ側からの弱い光（見える面がどれも真っ暗にならないように。まわりの光の設定に頼らない）
        if (_fill == null) _fill = MakeLight("Kit3DFill", Quaternion.LookRotation(Vector3.forward), 0.35f);
    }

    static Light MakeLight(string name, Quaternion rot, float intensity)
    {
        var go = new GameObject(name);
        go.transform.rotation = rot;
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.shadows = LightShadows.None;
        l.intensity = intensity;
        l.color = Color.white;
        return l;
    }

    /// <summary>形の網（光を受けないシェーダーのときは、陰を頂点の色に焼き込んだ写し）</summary>
    internal static Mesh MeshOf(Kit3DShape shape)
    {
        if (Meshes.TryGetValue(shape, out Mesh m) && m != null) return m;
        PrimitiveType t = shape == Kit3DShape.Sphere ? PrimitiveType.Sphere : shape == Kit3DShape.Capsule ? PrimitiveType.Capsule : shape == Kit3DShape.Cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        GameObject tmp = GameObject.CreatePrimitive(t);
        Mesh src = tmp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(tmp);
        m = IsLit ? src : Bake(src, _mode == ShaderMode.Sprite);
        Meshes[shape] = m;
        return m;
    }

    /// <summary>形ごとの、大きさ（マス）→ 網の倍率（球は直径1、カプセル・円柱は高さ2）</summary>
    internal static Vector3 ScaleFor(Kit3DShape shape, Vector3 size)
    {
        if (shape == Kit3DShape.Capsule || shape == Kit3DShape.Cylinder) return new Vector3(size.x, size.y * 0.5f, size.z);
        return size;
    }

    // 陰を頂点の色に焼き込む（逃げ道）。frontOnly：深さを書かないシェーダー用に、カメラを向いた面だけ残す
    static Mesh Bake(Mesh src, bool frontOnly)
    {
        Quaternion tilt = Quaternion.Euler(Tilt);
        Vector3[] v = src.vertices;
        Vector3[] nrm = src.normals;
        int[] tri = src.triangles;
        var colors = new Color[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 wn = tilt * nrm[i];
            float s = 0.45f + 0.55f * Mathf.Max(0f, Vector3.Dot(wn, ToLight)) + 0.15f * Mathf.Max(0f, -wn.z);
            s = Mathf.GammaToLinearSpace(Mathf.Clamp01(s));
            colors[i] = new Color(s, s, s, 1f);
        }
        var outTri = new List<int>(tri.Length);
        for (int i = 0; i + 2 < tri.Length; i += 3)
        {
            if (frontOnly)
            {
                Vector3 a = tilt * v[tri[i]], b = tilt * v[tri[i + 1]], c = tilt * v[tri[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.z > 0f) continue; // カメラ（-z）から見て裏
            }
            outTri.Add(tri[i]); outTri.Add(tri[i + 1]); outTri.Add(tri[i + 2]);
        }
        var m = new Mesh { name = src.name + "_Baked", hideFlags = HideFlags.DontSave };
        m.vertices = v;
        m.normals = nrm;
        m.colors = colors;
        m.triangles = outTri.ToArray();
        m.RecalculateBounds();
        return m;
    }
}

/// <summary>落ちる物の設定</summary>
public struct Kit3DFallOptions
{
    /// <summary>予告（枠と「!」）の秒数</summary>
    public float Telegraph;
    /// <summary>止まってから残る秒数（0 なら止まったら消える）</summary>
    public float Remain;
    /// <summary>止まったら乗れる箱・壁になる（地形のレイヤー）</summary>
    public bool Solid;
    /// <summary>止まったあと転がるマス（球）。転がっている間は当たる</summary>
    public float RollDistance;
    /// <summary>転がる向き（0 ならプレイヤーと反対の向き）</summary>
    public int RollDir;
    /// <summary>雑魚にも当たる（倒した数に入る）</summary>
    public bool HitZako;
    /// <summary>足場（すり抜け床）の上にも止まる</summary>
    public bool LandOnPlatforms;
    /// <summary>重力（マス/秒²）</summary>
    public float Gravity;
    /// <summary>当たる速さ（マス/秒。これより遅いと当たらない）</summary>
    public float MinHitSpeed;
    /// <summary>近接で壊せる回数（0 なら壊せない）</summary>
    public int BreakHits;
    /// <summary>予告の「!」を出す</summary>
    public bool Bang;
    /// <summary>数の上限の組と上限</summary>
    public string Group;
    public int Cap;
    /// <summary>止まったときの色の倍率（少し暗く）</summary>
    public float LandedShade;
    /// <summary>予告の角の枠を出さない（床の影だけ。Minecraft の砂）</summary>
    public bool NoBracket;
    /// <summary>床の影の色（危ない物の予告にするときは赤）</summary>
    public Color ShadowColor;

    /// <summary>既定の設定（Unity の技1）</summary>
    public static Kit3DFallOptions Default => new Kit3DFallOptions
    {
        Telegraph = 0.9f,
        Remain = 4f,
        Solid = true,
        LandOnPlatforms = true,
        Gravity = 30f,
        MinHitSpeed = 2f,
        Bang = true,
        Cap = Kit3D.DefaultCap,
        LandedShade = 0.82f,
        ShadowColor = new Color(0f, 0f, 0f, 0.45f),
    };
}

/// <summary>3D の見た目（MeshRenderer の子を持つ入れ物）。部品をいくつか重ねられる</summary>
public class Kit3DVisual : MonoBehaviour
{
    class Part
    {
        public MeshRenderer Renderer;
        public Kit3DShape Shape;
        public bool Outline;
        public Color Color;
        public Vector3 Offset;
        public Vector3 Size;
    }

    readonly List<Part> _parts = new List<Part>();
    MaterialPropertyBlock _mpb;
    Quaternion _tilt;
    float _spin;
    float _shade = 1f;
    float _flashUntil;
    bool _shown = true;
    float _radius;
    float _fit = 1f;
    float _scaleK = 1f;

    /// <summary>上限で消すときに消す物</summary>
    public GameObject Root { get; private set; }
    /// <summary>数の上限の組</summary>
    public string Group { get; private set; }
    /// <summary>true の間は上限で消さない（落ちている途中・予告の間）</summary>
    public bool Busy { get; set; }
    public float BornAt { get; private set; }

    internal void Init(GameObject root, string group)
    {
        Root = root;
        Group = group;
        BornAt = Time.time;
        _tilt = Quaternion.Euler(Kit3D.Tilt);
        transform.localRotation = _tilt;
        _mpb = new MaterialPropertyBlock();
    }

    void OnDestroy() => Kit3D.Forget(this);

    /// <summary>映る大きさを 2D の箱に合わせる倍率（立方体 0.8・球 1。部品の位置も一緒に縮む）</summary>
    public void SetFit(float fit)
    {
        _fit = Mathf.Max(0.1f, fit);
        transform.localScale = Vector3.one * _fit * _scaleK;
        if (_parts.Count > 0) Refit();
    }

    /// <summary>部品を足す（offset は物の中の位置。物と一緒に回る）。足した部品の MeshRenderer を返す</summary>
    public MeshRenderer AddPart(Kit3DShape shape, Vector3 offset, Vector3 size, Color color)
    {
        var go = new GameObject("Part_" + shape);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        go.transform.localScale = Kit3D.ScaleFor(shape, size);
        go.AddComponent<MeshFilter>().sharedMesh = Kit3D.MeshOf(shape);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Kit3D.Material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.sortingOrder = Kit3D.SortingOrder;
        mr.enabled = _shown;
        _parts.Add(new Part { Renderer = mr, Shape = shape, Color = color, Offset = offset, Size = size });
        Refit();
        Apply();
        return mr;
    }

    /// <summary>
    /// 選んでいる物のオレンジの輪かく（Blender）：今の部品を少し大きくした同じ形を、後ろ（画面の奥）に重ねる。grow はマス
    /// </summary>
    public void AddOutline(Color color, float grow = 0.12f)
    {
        var snapshot = new List<Part>(_parts);
        // 上の面の向きは画面の奥へ 0.29 しか傾いていないので、大きくした分より奥へ下げないと、上の面がオレンジで隠れる
        Vector3 back = Quaternion.Inverse(_tilt) * new Vector3(0f, 0f, (grow / 0.29f + 0.1f) / _fit);
        foreach (var p in snapshot)
        {
            if (p.Outline) continue;
            MeshRenderer mr = AddPart(p.Shape, p.Offset + back, p.Size + Vector3.one * grow * 2f, color);
            _parts[_parts.Count - 1].Outline = true;
            mr.gameObject.name = "Outline_" + p.Shape;
        }
        Apply();
    }

    /// <summary>輪かくを見せる・かくす</summary>
    public void ShowOutline(bool on)
    {
        foreach (var p in _parts) if (p.Outline && p.Renderer != null) p.Renderer.enabled = on && _shown;
    }

    /// <summary>半径（いちばん外の部品まで）</summary>
    public float Radius => _radius;

    void Refit()
    {
        float r = 0f;
        foreach (var p in _parts) r = Mathf.Max(r, p.Offset.magnitude + p.Size.magnitude * 0.5f);
        _radius = r * Mathf.Max(transform.localScale.x, Mathf.Max(transform.localScale.y, transform.localScale.z));
        Vector3 lp = transform.localPosition;
        lp.z = Kit3D.ZFor(_radius);
        transform.localPosition = lp;
    }

    /// <summary>いちばん下の点の高さ（親から見た位置。今の向き・倍率で）</summary>
    public float LowestY()
    {
        Quaternion r = transform.localRotation;
        float k = transform.localScale.x;
        Vector3 ax = r * Vector3.right, ay = r * Vector3.up, az = r * Vector3.forward;
        float min = float.PositiveInfinity;
        foreach (var p in _parts)
        {
            Vector3 c = r * (p.Offset * k);
            Vector3 h = p.Size * 0.5f * k;
            float ext = p.Shape == Kit3DShape.Sphere ? h.x : Mathf.Abs(ax.y) * h.x + Mathf.Abs(ay.y) * h.y + Mathf.Abs(az.y) * h.z;
            min = Mathf.Min(min, c.y - ext);
        }
        if (float.IsInfinity(min)) min = 0f;
        return transform.localPosition.y + min;
    }

    /// <summary>見せる・かくす</summary>
    public void Show(bool on)
    {
        _shown = on;
        foreach (var p in _parts) if (p.Renderer != null) p.Renderer.enabled = on;
    }

    public bool IsShown => _shown;

    /// <summary>色の倍率（1 ＝ そのまま。止まった物は 0.8 など）</summary>
    public void SetShade(float k) { _shade = k; Apply(); }

    /// <summary>白く光らせる（seconds 秒）</summary>
    public void Flash(float seconds) { _flashUntil = Time.time + seconds; Apply(); }

    /// <summary>部品の色を変える（index 0 ＝ 最初の部品）</summary>
    public void SetPartColor(int index, Color color)
    {
        if (index < 0 || index >= _parts.Count) return;
        _parts[index].Color = color;
        Apply();
    }

    /// <summary>画面の奥への軸のまわりに回す（転がる・回る）。角度は度。足していく</summary>
    public void Spin(float degrees)
    {
        _spin += degrees;
        transform.localRotation = Quaternion.AngleAxis(_spin, Vector3.forward) * _tilt;
    }

    /// <summary>向きを決める（既定の向きに、ほかの回転を足す）</summary>
    public void SetExtraRotation(Quaternion extra) => transform.localRotation = extra * _tilt;

    /// <summary>全体の倍率（縮んで消える・S スケール）</summary>
    public void SetScale(float k)
    {
        _scaleK = Mathf.Max(0.001f, k);
        transform.localScale = Vector3.one * _scaleK * _fit;
        Refit();
    }

    void Update()
    {
        if (_flashUntil > 0f && Time.time >= _flashUntil) { _flashUntil = 0f; Apply(); }
    }

    void Apply()
    {
        bool flash = Time.time < _flashUntil;
        foreach (var p in _parts)
        {
            if (p.Renderer == null) continue;
            float k = p.Outline ? 1f : _shade;
            Color c = flash ? Color.white : new Color(p.Color.r * k, p.Color.g * k, p.Color.b * k, 1f);
            _mpb.Clear();
            _mpb.SetColor("_Color", c);
            p.Renderer.SetPropertyBlock(_mpb);
        }
    }
}

/// <summary>予告の角だけの枠と「!」（Kit3D.Bracket が作る）。時間で消える。ボスの形態変化でも消える</summary>
public class Kit3DBracket : CustomEffect
{
    SpriteRenderer _frame, _bang;
    float _endAt;

    internal void Setup(Vector2 size, float seconds, bool bang, Color color)
    {
        _endAt = Time.time + Mathf.Max(0.05f, seconds);
        _frame = RuntimeSprites.Show(transform, Kit3DArt.Corners(size), Vector2.zero, 18, 1f, "Corners");
        _frame.color = color;
        if (bang)
        {
            _bang = RuntimeSprites.Show(transform, Kit3DArt.Bang, Vector2.zero, 18, 1f, "Bang");
            _bang.color = color;
        }
    }

    /// <summary>残りの秒数</summary>
    public float Remaining => Mathf.Max(0f, _endAt - Time.time);

    void Update()
    {
        float left = _endAt - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        bool on = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        if (_frame != null) _frame.enabled = on;
        if (_bang != null) _bang.enabled = on;
    }
}

/// <summary>Kit3D の共通の絵（予告の枠と「!」）</summary>
public static class Kit3DArt
{
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', new Color32(255, 255, 255, 255) } };

    /// <summary>角だけの枠（太さ 2ドット、角の長さ 6〜10ドット。白。色は SpriteRenderer で付ける）</summary>
    public static Sprite Corners(Vector2 size)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(size.x * 18f)), h = Mathf.Max(8, Mathf.RoundToInt(size.y * 18f));
        int arm = Mathf.Clamp(Mathf.Min(w, h) / 4, 4, 10);
        const int t = 2;
        var rows = new string[h];
        var line = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool nearX = x < arm || x >= w - arm;
                bool nearY = y < arm || y >= h - arm;
                bool edgeX = x < t || x >= w - t;
                bool edgeY = y < t || y >= h - t;
                line[x] = (edgeX && nearY) || (edgeY && nearX) ? 'X' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii($"Kit3D_Corners_{w}x{h}", rows, Mono);
    }

    /// <summary>「!」（4×12 ドット。白）</summary>
    public static Sprite Bang => RuntimeSprites.FromAscii("Kit3D_Bang", new[]
    {
        ".XX.",
        "XXXX",
        "XXXX",
        "XXXX",
        "XXXX",
        ".XX.",
        ".XX.",
        ".XX.",
        "....",
        ".XX.",
        "XXXX",
        ".XX.",
    }, Mono);

    static readonly Color AxisX = RuntimeSprites.Hex("E2463E");
    static readonly Color AxisY = RuntimeSprites.Hex("8CD23C");
    static readonly Color AxisZ = RuntimeSprites.Hex("3D84E6");

    /// <summary>
    /// 3D の軸の印（OnGUI の中で呼ぶ。PixelGui.Begin のあと）：右へ赤 x、上へ緑 y、手前へ青 z（ななめ）。Unity の Scene ビュー・Blender の右上の印
    /// </summary>
    public static void DrawAxesGui(float cx, float cy)
    {
        PixelGui.Fill(new Rect(cx - 15f, cy - 15f, 30f, 30f), new Color(0.16f, 0.16f, 0.16f, 0.55f));
        PixelGui.Fill(new Rect(cx, cy - 1f, 10f, 2f), AxisX);
        PixelGui.Fill(new Rect(cx + 10f, cy - 3f, 3f, 6f), AxisX);
        PixelGui.TextEn(new Rect(cx + 6f, cy + 3f, 8f, 8f), "x", 8, AxisX);
        PixelGui.Fill(new Rect(cx - 1f, cy - 10f, 2f, 10f), AxisY);
        PixelGui.Fill(new Rect(cx - 3f, cy - 13f, 6f, 3f), AxisY);
        PixelGui.TextEn(new Rect(cx + 3f, cy - 15f, 8f, 8f), "y", 8, AxisY);
        for (int i = 0; i < 7; i++) PixelGui.Fill(new Rect(cx - 2f - i, cy + 1f + i, 2f, 2f), AxisZ);
        PixelGui.Fill(new Rect(cx - 11f, cy + 7f, 4f, 4f), AxisZ);
        PixelGui.Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), Color.white);
    }

    /// <summary>床の影（落下物と同じ絵。無ければ円）</summary>
    public static Sprite Shadow => GameArt.Instance != null && GameArt.Instance.Shadow != null ? GameArt.Instance.Shadow : PlaceholderSprites.Circle;
}
