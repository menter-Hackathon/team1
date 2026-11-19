using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】道に沿った線（2026-11-19。docs/app-kits-2-design.md X2）。担当D の物。Photoshop のブラシ・Illustrator のペンツール・Figma のペンツール（能力）が使う。
/// 流れ：赤い細い予告の線（点線にもできる）→ 太い線が道をなぞる（DrawTime。0 なら一度に）→ 判定（Active）→ 絵だけ少し残る（Linger）→ 消える。
/// ・判定は 1つの Hitbox（PolygonCollider2D に、区間ごとの回した四角を道として入れる）。同じ相手には 1回だけ当たる（ボスのゲージが区間の数だけ増えない）
/// ・なぞる間は、描けた所だけが当たる（先の丸い絵 Tip が、なぞる先を見せる）
/// ・owner.CreateSpawnedObject で作る CustomEffect なので、ボスの形態変化（50％）・正常化（100％）で消える
/// ・プレイヤーの能力から使ってもよい（owner＝プレイヤーの AttackRunner。予告 0 で）
/// ・点の列は Smooth（なめらかな曲線）・Quadratic（2次の曲線）で作れる
/// </summary>
public class PathStroke : CustomEffect
{
    /// <summary>見た目と時間（すべて【仮】）</summary>
    public class Style
    {
        /// <summary>予告の秒数（赤い細い線）。0 なら予告なし</summary>
        public float Telegraph = 0.9f;
        /// <summary>予告を点線にする</summary>
        public bool Dashed;
        /// <summary>予告の線の太さ（ドット）</summary>
        public float TelegraphPx = 2f;
        /// <summary>予告の色（危ない物は赤）</summary>
        public Color TelegraphColor = KitDress.Red;
        /// <summary>太い線の太さ（マス。判定と絵）</summary>
        public float Width = 1.2f;
        /// <summary>なぞる秒数（0 なら一度に全部）</summary>
        public float DrawTime;
        /// <summary>全部描けてから、判定を出している秒数</summary>
        public float Active = 0.3f;
        /// <summary>判定のあと、絵だけ残る秒数（うすくなって消える）</summary>
        public float Linger = 0.2f;
        /// <summary>線の色</summary>
        public Color Color = Color.white;
        /// <summary>線のまん中の色（無ければ一色）</summary>
        public Color? Core;
        /// <summary>なぞる先の絵（ブラシの丸など。無ければ出さない）</summary>
        public Sprite Tip;
        public float TipScale = 1f;
        /// <summary>場に残る技の効果か</summary>
        public bool Lingers;
        /// <summary>絵の重なりの順番</summary>
        public int Order = 18;
    }

    AttackRunner _owner;
    AttackData _data;
    Style _s;
    readonly List<Vector2> _pts = new List<Vector2>();
    readonly List<float> _cum = new List<float>();
    float _length;
    Transform _teleRoot, _strokeRoot;
    PolygonCollider2D _poly;
    Hitbox _hitbox;
    SpriteRenderer _tip;
    readonly List<SpriteRenderer> _strokeParts = new List<SpriteRenderer>();

    /// <summary>判定が始まったとき（なぞり始め）</summary>
    public event Action<PathStroke> Struck;
    /// <summary>だれかに当たったか</summary>
    public bool HitAny { get; private set; }
    /// <summary>道（ゲームの世界）</summary>
    public IReadOnlyList<Vector2> Points => _pts;
    /// <summary>道の長さ（マス）</summary>
    public float Length => _length;
    /// <summary>出してから消えるまでの秒数</summary>
    public float TotalTime => _s.Telegraph + _s.DrawTime + _s.Active + _s.Linger;

    public override bool Lingers => _s != null && _s.Lingers;

    /// <summary>道に沿った線を1本出す。points はゲームの世界の点の列（2つ以上）</summary>
    public static PathStroke Spawn(AttackRunner owner, AttackData data, IList<Vector2> points, Style style)
    {
        if (owner == null || data == null || points == null || points.Count < 2) return null;
        GameObject go = owner.CreateSpawnedObject($"PathStroke_{data.name}", Vector2.zero);
        go.transform.position = Vector3.zero;
        var s = go.AddComponent<PathStroke>();
        s._owner = owner;
        s._data = data;
        s._s = style ?? new Style();
        foreach (var p in points) s._pts.Add(p);
        s.Build();
        s.StartCoroutine(s.Run());
        return s;
    }

    void Build()
    {
        _cum.Add(0f);
        for (int i = 1; i < _pts.Count; i++) _cum.Add(_cum[i - 1] + Vector2.Distance(_pts[i - 1], _pts[i]));
        _length = _cum[_cum.Count - 1];

        _teleRoot = Child("Telegraph");
        if (_s.Telegraph > 0f)
        {
            float w = Mathf.Max(1f, _s.TelegraphPx) * KitDress.Px;
            if (_s.Dashed) Dashes(_teleRoot, _pts, 0.28f, 0.16f, w, _s.TelegraphColor, _s.Order - 1);
            else for (int i = 1; i < _pts.Count; i++) Bar(_teleRoot, _pts[i - 1], _pts[i], w, _s.TelegraphColor, _s.Order - 1, "Tele");
        }
        _teleRoot.gameObject.SetActive(false);

        _strokeRoot = Child("Stroke");
        var hit = Child("Hit");
        var rb = hit.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        _poly = hit.gameObject.AddComponent<PolygonCollider2D>();
        _poly.isTrigger = true;
        _poly.pathCount = 0;
        _hitbox = _owner.AddHitbox(hit.gameObject, _data, _poly);
        _hitbox.Hit += (t, info) => HitAny = true;

        if (_s.Tip != null)
        {
            _tip = RuntimeSprites.Show(transform, _s.Tip, _pts[0], _s.Order + 2, _s.TipScale, "Tip");
            _tip.enabled = false;
        }
    }

    Transform Child(string name)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    IEnumerator Run()
    {
        // 予告（最後の 0.3秒は点滅）
        if (_s.Telegraph > 0f)
        {
            _teleRoot.gameObject.SetActive(true);
            if (_tip != null) { _tip.enabled = true; _tip.color = new Color(1f, 1f, 1f, 0.6f); }
            float end = Time.time + _s.Telegraph;
            while (Time.time < end)
            {
                float left = end - Time.time;
                _teleRoot.gameObject.SetActive(left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0);
                yield return null;
            }
            _teleRoot.gameObject.SetActive(false);
        }

        // なぞる
        Struck?.Invoke(this);
        if (_tip != null) { _tip.enabled = true; _tip.color = Color.white; }
        _hitbox.Activate();
        if (_owner != null) _owner.NotifyReleased(_data);
        if (_s.DrawTime > 0f)
        {
            float start = Time.time;
            while (Time.time - start < _s.DrawTime)
            {
                ShowUpTo(_length * Mathf.Clamp01((Time.time - start) / _s.DrawTime));
                yield return null;
            }
        }
        ShowUpTo(_length);
        if (_tip != null) _tip.enabled = false;

        // 判定
        float hitEnd = Time.time + _s.Active;
        while (Time.time < hitEnd) yield return null;
        _hitbox.Deactivate();
        _poly.enabled = false;

        // 絵だけ残る
        if (_s.Linger > 0f)
        {
            float start = Time.time;
            while (Time.time - start < _s.Linger)
            {
                float a = 1f - Mathf.Clamp01((Time.time - start) / _s.Linger);
                foreach (var sr in _strokeParts) if (sr != null) { Color c = sr.color; c.a = a; sr.color = c; }
                yield return null;
            }
        }
        Destroy(gameObject);
    }

    int _shownSegments;
    SpriteRenderer _partialOuter, _partialCore;

    /// <summary>道の最初から dist マスまでを見せて、当たるようにする</summary>
    void ShowUpTo(float dist)
    {
        // 描き終わった区間
        while (_shownSegments < _pts.Count - 1 && _cum[_shownSegments + 1] <= dist + 0.0001f)
        {
            Vector2 a = _pts[_shownSegments], b = _pts[_shownSegments + 1];
            AddStrokeSegment(a, b, true);
            _shownSegments++;
        }
        // 途中の区間
        Vector2 tipPos = _pts[Mathf.Min(_shownSegments, _pts.Count - 1)];
        bool partial = false;
        if (_shownSegments < _pts.Count - 1)
        {
            Vector2 a = _pts[_shownSegments], b = _pts[_shownSegments + 1];
            float segLen = _cum[_shownSegments + 1] - _cum[_shownSegments];
            float k = segLen > 0.0001f ? Mathf.Clamp01((dist - _cum[_shownSegments]) / segLen) : 1f;
            Vector2 m = Vector2.Lerp(a, b, k);
            tipPos = m;
            if (k > 0.01f)
            {
                partial = true;
                if (_partialOuter == null) _partialOuter = Bar(_strokeRoot, a, m, _s.Width, _s.Color, _s.Order, "Partial");
                else SetBar(_partialOuter, a, m, _s.Width);
                if (_s.Core.HasValue)
                {
                    if (_partialCore == null) _partialCore = Bar(_strokeRoot, a, m, _s.Width * 0.45f, _s.Core.Value, _s.Order + 1, "PartialCore");
                    else SetBar(_partialCore, a, m, _s.Width * 0.45f);
                }
            }
            SetPaths(m);
        }
        if (_partialOuter != null) _partialOuter.enabled = partial;
        if (_partialCore != null) _partialCore.enabled = partial;
        if (_shownSegments >= _pts.Count - 1) SetPaths(null);
        if (_tip != null) _tip.transform.localPosition = tipPos;
    }

    void AddStrokeSegment(Vector2 a, Vector2 b, bool joint)
    {
        _strokeParts.Add(Bar(_strokeRoot, a, b, _s.Width, _s.Color, _s.Order, "Outer"));
        if (_s.Core.HasValue) _strokeParts.Add(Bar(_strokeRoot, a, b, _s.Width * 0.45f, _s.Core.Value, _s.Order + 1, "Core"));
        if (joint)
        {
            int d = Mathf.Max(3, Mathf.RoundToInt(_s.Width * RuntimeSprites.PixelsPerUnit));
            var j = RuntimeSprites.Show(_strokeRoot, FxArt.Disc(d), b, _s.Order, 1f, "Joint");
            j.color = _s.Color;
            _strokeParts.Add(j);
            if (_strokeParts.Count <= 3) // 最初の点にも丸
            {
                var j0 = RuntimeSprites.Show(_strokeRoot, FxArt.Disc(d), a, _s.Order, 1f, "Joint");
                j0.color = _s.Color;
                _strokeParts.Add(j0);
            }
        }
    }

    /// <summary>当たる道を入れ直す（描けた区間＋途中の点 partialEnd まで）</summary>
    void SetPaths(Vector2? partialEnd)
    {
        int full = _shownSegments;
        int count = full + (partialEnd.HasValue ? 1 : 0);
        _poly.pathCount = count;
        float h = _s.Width * 0.5f;
        for (int i = 0; i < full; i++) _poly.SetPath(i, Quad(_pts[i], _pts[i + 1], h));
        if (partialEnd.HasValue) _poly.SetPath(full, Quad(_pts[full], partialEnd.Value, h));
    }

    static Vector2[] Quad(Vector2 a, Vector2 b, float half)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 0.0001f) d = Vector2.right * 0.01f;
        Vector2 n = new Vector2(-d.y, d.x).normalized * half;
        Vector2 t = d.normalized * half * 0.5f; // 区間のつなぎ目を少し重ねる
        return new[] { a - t + n, b + t + n, b + t - n, a - t - n };
    }

    // ───────── 道具（キットからも使ってよい） ─────────

    /// <summary>
    /// 飾りを付ける入れ物を作る（life 秒で消える。owner の出した物なので、50％・100％でも消える）。位置は pos、大きさ 1・回っていない。
    /// owner が無ければ、ただの物（時間で消える）
    /// </summary>
    public static Transform Decor(AttackRunner owner, Vector2 pos, float life, string name = "KitDecor")
    {
        GameObject go = owner != null ? owner.CreateSpawnedObject(name, pos) : new GameObject(name);
        go.transform.position = pos;
        var d = go.AddComponent<PathStrokeDecor>();
        d.DieAt = life > 0f ? Time.time + life : float.PositiveInfinity;
        return go.transform;
    }

    /// <summary>a から b への線（回した四角）を parent の子に作る。parent は回っていない・大きさ 1 の物にする</summary>
    public static SpriteRenderer Bar(Transform parent, Vector2 a, Vector2 b, float width, Color color, int order, string name = "Bar")
    {
        Vector2 d = b - a;
        float len = Mathf.Max(0.01f, d.magnitude);
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = (Vector3)((a + b) * 0.5f) + new Vector3(0f, 0f, parent.position.z);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSprites.Square;
        Vector2 native = sr.sprite.bounds.size;
        go.transform.localScale = new Vector3(len / native.x, width / native.y, 1f);
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>Bar で作った線を、a から b へ置き直す</summary>
    public static void SetBar(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
    {
        if (sr == null) return;
        Vector2 d = b - a;
        float len = Mathf.Max(0.01f, d.magnitude);
        Transform t = sr.transform;
        t.position = (Vector3)((a + b) * 0.5f) + new Vector3(0f, 0f, t.position.z);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        t.localScale = new Vector3(len / native.x, width / native.y, 1f);
    }

    /// <summary>点の列に沿った点線（dash マスの線と gap マスのすき間）を parent の子に作る。作った線を返す</summary>
    public static List<SpriteRenderer> Dashes(Transform parent, IList<Vector2> pts, float dash, float gap, float width, Color color, int order, bool closed = false)
    {
        var list = new List<SpriteRenderer>();
        int n = pts.Count;
        int segs = closed ? n : n - 1;
        float phase = 0f; // 今の区間の、点線の位置
        for (int i = 0; i < segs; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n];
            float len = Vector2.Distance(a, b);
            if (len < 0.0001f) continue;
            float t = 0f;
            float cycle = Mathf.Max(0.02f, dash + gap);
            int guard = 0;
            // 2026-11-19：小数の誤差で step が 0 近くになると phase が進まず、抜けられなくなっていた（ゲームが固まる）。
            // 1回に必ず少しは進め、回数にも上限を付ける
            while (t < len && guard++ < 4096)
            {
                float inCycle = Mathf.Repeat(phase, cycle);
                float step;
                if (inCycle < dash)
                {
                    step = Mathf.Max(0.001f, Mathf.Min(dash - inCycle, len - t));
                    float t1 = Mathf.Min(len, t + step);
                    Vector2 p0 = Vector2.Lerp(a, b, t / len), p1 = Vector2.Lerp(a, b, t1 / len);
                    if (t1 - t > 0.01f) list.Add(Bar(parent, p0, p1, width, color, order, "Dash"));
                }
                else
                {
                    step = Mathf.Max(0.001f, Mathf.Min(cycle - inCycle, len - t));
                }
                t += step; phase += step;
                if (phase > cycle * 1000f) phase = Mathf.Repeat(phase, cycle); // 大きくなりすぎると誤差がふえるので戻す
            }
        }
        return list;
    }

    /// <summary>点を通るなめらかな曲線（Catmull-Rom）。perSegment：点と点の間を何個に割るか</summary>
    public static List<Vector2> Smooth(IList<Vector2> pts, int perSegment = 4)
    {
        var o = new List<Vector2>();
        if (pts == null || pts.Count == 0) return o;
        if (pts.Count < 3) { o.AddRange(pts); return o; }
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            for (int k = 0; k < perSegment; k++)
            {
                float t = k / (float)perSegment, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        o.Add(pts[pts.Count - 1]);
        return o;
    }

    /// <summary>2次の曲線（a から b へ、c に引っぱられる）。n：区間の数</summary>
    public static List<Vector2> Quadratic(Vector2 a, Vector2 c, Vector2 b, int n = 9)
    {
        var o = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            o.Add(u * u * a + 2f * u * t * c + t * t * b);
        }
        return o;
    }
}

/// <summary>飾りの入れ物（PathStroke.Decor が作る）。時間で消える。ボスの形態変化でも消える（CustomEffect）</summary>
public class PathStrokeDecor : CustomEffect
{
    internal float DieAt = float.PositiveInfinity;

    void Update()
    {
        if (Time.time >= DieAt) Destroy(gameObject);
    }
}
