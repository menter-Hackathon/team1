using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】囲んだ範囲の判定（2026-11-19。docs/app-kits-2-design.md X2 の、閉じた道の版）。担当D の物。
/// Figma の Frame・Photoshop のなげなわ選択・Illustrator のスターツールとアートボード・Arc の自動アーカイブが使う。
/// 流れ：（安全の色で見せる Appear）→ 赤い予告（Telegraph。中心から広げる Grow もできる）→ 中を塗って判定（Active。TickInterval で残る床にもできる）→ 塗りが少し残る（Linger）→ 消える。
/// ・判定は 1つの Hitbox（PolygonCollider2D。同じ相手には 1回。TickInterval があれば、その間隔でまた当たる）
/// ・塗りの絵は、多角形をドットに塗った物（市松もようにもできる）。輪かくは細い線か、流れる点線（Ants）
/// ・飾り（名札など）は、キットが Root の子に付ける（一緒に消える）。今の段階は Current・StepChanged で分かる
/// ・owner.CreateSpawnedObject で作る CustomEffect なので、形態変化（50％）・正常化（100％）で消える
/// </summary>
public class PathStrokeRegion : CustomEffect
{
    /// <summary>段階</summary>
    public enum Step { Appear, Telegraph, Active, Linger }

    /// <summary>見た目と時間（すべて【仮】）</summary>
    public class Style
    {
        /// <summary>安全の色で見せる秒数（Figma の青い枠・Photoshop の白黒の点線）。0 ならすぐ赤</summary>
        public float Appear;
        /// <summary>安全のときの輪かくの色</summary>
        public Color AppearColor = Color.white;
        /// <summary>輪かくを流れる点線（白と黒）にする（Photoshop の選択範囲）</summary>
        public bool Ants;
        /// <summary>中心から広がる秒数（Appear か Telegraph の最初から）</summary>
        public float Grow;
        /// <summary>赤い予告の秒数（0.8 以上）</summary>
        public float Telegraph = 0.9f;
        /// <summary>判定の秒数</summary>
        public float Active = 0.15f;
        /// <summary>0 より大きいと、判定の間この間隔でまた当たる（残る床）</summary>
        public float TickInterval;
        /// <summary>判定のあと、塗りだけ見えている秒数</summary>
        public float Linger = 0.3f;
        /// <summary>塗りの色（市松もようなら 1つ目の色）</summary>
        public Color Fill = Color.white;
        /// <summary>市松もようにする（2つ目の色）</summary>
        public Color? Checker;
        public int CheckerCell = 4;
        /// <summary>予告の間の、うすい赤の塗りの濃さ（0 で塗らない）</summary>
        public float TelegraphFillAlpha = 0.18f;
        /// <summary>輪かくの太さ（ドット）</summary>
        public float OutlinePx = 2f;
        /// <summary>判定の間も輪かくを出す</summary>
        public bool OutlineWhileActive;
        /// <summary>場に残る技の効果か</summary>
        public bool Lingers;
        public int Order = 18;
    }

    static readonly Dictionary<string, Sprite> FillCache = new Dictionary<string, Sprite>();

    AttackRunner _owner;
    AttackData _data;
    Style _s;
    readonly List<Vector2> _local = new List<Vector2>(); // 真ん中からの点
    Rect _localBounds;
    Transform _outlineRoot;
    readonly List<SpriteRenderer> _outline = new List<SpriteRenderer>();
    SpriteRenderer _fill;
    PolygonCollider2D _poly;
    Hitbox _hitbox;
    float _stepStart;

    /// <summary>段階が変わったとき</summary>
    public event Action<PathStrokeRegion, Step> StepChanged;
    /// <summary>今の段階</summary>
    public Step Current { get; private set; }
    /// <summary>飾りを付ける所（真ん中。広がる間は大きさが変わる）</summary>
    public Transform Root => transform;
    /// <summary>真ん中（ゲームの世界）</summary>
    public Vector2 Center => transform.position;
    /// <summary>範囲（真ん中からの四角）</summary>
    public Rect LocalBounds => _localBounds;
    /// <summary>だれかに当たったか</summary>
    public bool HitAny { get; private set; }
    /// <summary>出してから消えるまでの秒数</summary>
    public float TotalTime => _s.Appear + _s.Telegraph + _s.Active + _s.Linger;

    public override bool Lingers => _s != null && _s.Lingers;

    /// <summary>囲んだ範囲を1つ出す。polygon はゲームの世界の点（時計回りでも反時計回りでもよい）、center は広がる中心</summary>
    public static PathStrokeRegion Spawn(AttackRunner owner, AttackData data, IList<Vector2> polygon, Vector2 center, Style style)
    {
        if (owner == null || data == null || polygon == null || polygon.Count < 3) return null;
        GameObject go = owner.CreateSpawnedObject($"PathRegion_{data.name}", center);
        var r = go.AddComponent<PathStrokeRegion>();
        r._owner = owner;
        r._data = data;
        r._s = style ?? new Style();
        foreach (var p in polygon) r._local.Add(p - center);
        r.Build();
        r.StartCoroutine(r.Run());
        return r;
    }

    void Build()
    {
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var p in _local) { xMin = Mathf.Min(xMin, p.x); yMin = Mathf.Min(yMin, p.y); xMax = Mathf.Max(xMax, p.x); yMax = Mathf.Max(yMax, p.y); }
        _localBounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        // 塗り（予告の間はうすい赤、判定で本当の色）
        Sprite fill = FillSprite(_local, _localBounds, Color.white, null, 4);
        var fgo = new GameObject("Fill");
        fgo.layer = gameObject.layer;
        fgo.transform.SetParent(transform, false);
        fgo.transform.localPosition = _localBounds.center;
        _fill = fgo.AddComponent<SpriteRenderer>();
        _fill.sprite = fill;
        _fill.sortingOrder = _s.Order - 2;
        _fill.enabled = false;

        // 輪かく
        _outlineRoot = new GameObject("Outline").transform;
        _outlineRoot.gameObject.layer = gameObject.layer;
        _outlineRoot.SetParent(transform, false);
        BuildOutline(_s.Ants && _s.Appear > 0f);

        // 判定
        var hit = new GameObject("Hit");
        hit.layer = gameObject.layer;
        hit.transform.SetParent(transform, false);
        var rb = hit.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        _poly = hit.AddComponent<PolygonCollider2D>();
        _poly.isTrigger = true;
        _poly.pathCount = 1;
        _poly.SetPath(0, _local.ToArray());
        _poly.enabled = false;
        _hitbox = _owner.AddHitbox(hit, _data, _poly);
        _hitbox.Hit += (t, info) => HitAny = true;
    }

    void BuildOutline(bool ants)
    {
        foreach (var o in _outline) if (o != null) Destroy(o.gameObject);
        _outline.Clear();
        Vector3 keep = transform.localScale; // 広がる途中でも、大きさ 1 で作ってから戻す
        transform.localScale = Vector3.one;
        float w = Mathf.Max(1f, _s.OutlinePx) * KitDress.Px;
        // 輪かくは、真ん中を原点にした線（親が広がると一緒に広がる）
        var pts = new List<Vector2>(_local.Count);
        foreach (var p in _local) pts.Add((Vector2)transform.position + p);
        if (ants) _outline.AddRange(PathStroke.Dashes(_outlineRoot, pts, 0.2f, 0.0001f, w, Color.white, _s.Order, true));
        else for (int i = 0; i < pts.Count; i++) _outline.Add(PathStroke.Bar(_outlineRoot, pts[i], pts[(i + 1) % pts.Count], w, Color.white, _s.Order, "Edge"));
        // 角の丸め（線の継ぎ目を埋める四角）
        if (!ants) foreach (var p in pts)
        {
            var c = PathStroke.Bar(_outlineRoot, p - new Vector2(w * 0.5f, 0f), p + new Vector2(w * 0.5f, 0f), w, Color.white, _s.Order, "Corner");
            _outline.Add(c);
        }
        transform.localScale = keep;
    }

    void SetOutline(Color a, Color? b = null)
    {
        for (int i = 0; i < _outline.Count; i++) if (_outline[i] != null) _outline[i].color = b.HasValue && i % 2 == 1 ? b.Value : a;
    }

    void SetStep(Step s)
    {
        Current = s;
        _stepStart = Time.time;
        try { StepChanged?.Invoke(this, s); }
        catch (Exception e) { Debug.LogException(e); }
    }

    float GrowK(float since) => _s.Grow > 0f ? Mathf.Clamp01(since / _s.Grow) : 1f;

    IEnumerator Run()
    {
        float born = Time.time;
        void ApplyGrow()
        {
            float k = GrowK(Time.time - born);
            float e = 1f - (1f - k) * (1f - k);
            transform.localScale = new Vector3(Mathf.Max(0.02f, e), Mathf.Max(0.02f, e), 1f);
        }

        // 安全の色で見せる
        if (_s.Appear > 0f)
        {
            SetStep(Step.Appear);
            while (Time.time - _stepStart < _s.Appear)
            {
                ApplyGrow();
                if (_s.Ants)
                {
                    bool flip = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
                    SetOutline(flip ? Color.white : Color.black, flip ? Color.black : Color.white);
                }
                else SetOutline(_s.AppearColor);
                yield return null;
            }
            if (_s.Ants) BuildOutline(false);
        }

        // 赤い予告
        SetStep(Step.Telegraph);
        if (_s.TelegraphFillAlpha > 0f)
        {
            _fill.enabled = true;
            Color c = KitDress.Red;
            c.a = _s.TelegraphFillAlpha;
            _fill.color = c;
        }
        while (Time.time - _stepStart < _s.Telegraph)
        {
            ApplyGrow();
            float left = _s.Telegraph - (Time.time - _stepStart);
            bool on = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            SetOutline(on ? KitDress.Red : new Color(1f, 0.27f, 0.23f, 0.25f));
            yield return null;
        }
        transform.localScale = Vector3.one;

        // 判定
        SetStep(Step.Active);
        if (_s.Checker.HasValue) _fill.sprite = FillSprite(_local, _localBounds, _s.Fill, _s.Checker, _s.CheckerCell);
        _fill.enabled = true;
        _fill.color = _s.Checker.HasValue ? Color.white : _s.Fill;
        _outlineRoot.gameObject.SetActive(_s.OutlineWhileActive);
        _poly.enabled = true;
        _hitbox.Activate();
        if (_owner != null) _owner.NotifyReleased(_data);
        float next = Time.time + (_s.TickInterval > 0f ? _s.TickInterval : float.PositiveInfinity);
        while (Time.time - _stepStart < _s.Active)
        {
            if (Time.time >= next) { _hitbox.Activate(); next += _s.TickInterval; }
            yield return null;
        }
        _hitbox.Deactivate();
        _poly.enabled = false;

        // 塗りだけ残る
        SetStep(Step.Linger);
        Color baseC = _fill.color;
        while (Time.time - _stepStart < _s.Linger)
        {
            float a = 1f - Mathf.Clamp01((Time.time - _stepStart) / Mathf.Max(0.01f, _s.Linger));
            _fill.color = new Color(baseC.r, baseC.g, baseC.b, baseC.a * a);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>今の段階になってからの秒数</summary>
    public float StepTime => Time.time - _stepStart;

    // ───────── 塗りの絵 ─────────

    /// <summary>多角形（真ん中からの点）をドットに塗った絵。checker があれば市松もよう（cell ドット）</summary>
    public static Sprite FillSprite(IList<Vector2> local, Rect bounds, Color a, Color? checker, int cell)
    {
        int w = Mathf.Clamp(Mathf.CeilToInt(bounds.width * RuntimeSprites.PixelsPerUnit), 1, 512);
        int h = Mathf.Clamp(Mathf.CeilToInt(bounds.height * RuntimeSprites.PixelsPerUnit), 1, 512);
        var key = new System.Text.StringBuilder(64);
        key.Append(w).Append('x').Append(h).Append(':');
        foreach (var p in local) key.Append(Mathf.RoundToInt(p.x * 18f)).Append(',').Append(Mathf.RoundToInt(p.y * 18f)).Append(';');
        key.Append(ColorUtility.ToHtmlStringRGBA(a));
        if (checker.HasValue) key.Append('/').Append(ColorUtility.ToHtmlStringRGBA(checker.Value)).Append(cell);
        string k = key.ToString();
        if (FillCache.TryGetValue(k, out Sprite cached) && cached != null) return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[w * h];
        Color32 ca = a, cb = checker ?? a, none = new Color32(0, 0, 0, 0);
        cell = Mathf.Max(1, cell);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(bounds.xMin + (x + 0.5f) / RuntimeSprites.PixelsPerUnit, bounds.yMin + (y + 0.5f) / RuntimeSprites.PixelsPerUnit);
                px[y * w + x] = Inside(local, p) ? ((x / cell + y / cell) % 2 == 0 ? ca : cb) : none;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        s.hideFlags = HideFlags.DontSave;
        if (FillCache.Count > 64) FillCache.Clear();
        FillCache[k] = s;
        return s;
    }

    /// <summary>点が多角形の中か</summary>
    public static bool Inside(IList<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-6f) + a.x) inside = !inside;
        }
        return inside;
    }

    // ───────── 形 ─────────

    /// <summary>四角（真ん中・大きさ）</summary>
    public static List<Vector2> RectShape(Vector2 center, Vector2 size)
    {
        Vector2 h = size * 0.5f;
        return new List<Vector2> { center + new Vector2(-h.x, -h.y), center + new Vector2(h.x, -h.y), center + new Vector2(h.x, h.y), center + new Vector2(-h.x, h.y) };
    }

    /// <summary>だ円（真ん中・横と縦の長さ）</summary>
    public static List<Vector2> Ellipse(Vector2 center, Vector2 size, int n = 28)
    {
        var o = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float t = i * Mathf.PI * 2f / n;
            o.Add(center + new Vector2(Mathf.Cos(t) * size.x * 0.5f, Mathf.Sin(t) * size.y * 0.5f));
        }
        return o;
    }

    /// <summary>星（とがった先の数 points、外の半径・内の半径）。rotDeg：1つ目の先の向き（90 で真上）</summary>
    public static List<Vector2> Star(Vector2 center, float outer, float inner, int points = 5, float rotDeg = 90f)
    {
        var o = new List<Vector2>(points * 2);
        for (int i = 0; i < points * 2; i++)
        {
            float r = i % 2 == 0 ? outer : inner;
            float t = (rotDeg + i * 180f / points) * Mathf.Deg2Rad;
            o.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
        }
        return o;
    }

    /// <summary>正多角形（辺の数 sides）</summary>
    public static List<Vector2> RegularPolygon(Vector2 center, float radius, int sides, float rotDeg = 90f)
    {
        var o = new List<Vector2>(sides);
        for (int i = 0; i < sides; i++)
        {
            float t = (rotDeg + i * 360f / sides) * Mathf.Deg2Rad;
            o.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
        }
        return o;
    }
}
