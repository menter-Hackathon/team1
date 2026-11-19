using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Illustrator のステージの出来事「ペンツール」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 12章）。
/// ・Pop（当たらない使い方）：アンカーポイント（白い四角に青いふち）が打たれる。① 左の低い足場の上（x −3.5・床から 4.5）→
///   ② 右の低い足場の上（x ＋3.5・4.5）→ ③ まん中（x 0・2.5）。前のアンカーと青い曲線でつながる（飾り。当たらない）
/// ・Orbit（主）：アンカーが打たれて 1.0秒後から、ハンドルの先の丸（青いふちの白い丸。幅 0.7）がアンカーのまわりを回る。
///   2つ（180°おき）・半径 2.8・80°/秒（強さ 1 は 60°/秒・強さ 3 は 3つ）・4.0秒。予告 1.0秒（輪のうすい赤い点線＋回る向きの印＋出る点で赤く点滅）。
///   アンカーと丸をつなぐ細い線は当たらない（見た目）。輪は同じ時に 1つだけ（前の輪が消える時に、次の輪が動き出す）
/// ・Diagonal（強さ 2・3 だけ）：7.0秒は上の左、12.0秒は上の右の角から、オレンジのペン先がプレイヤーへ（予告 0.9秒・速さ 9）
/// ・14.5秒：パスが閉じる（青い線が白く光って消える。飾り）
/// ・設計の「アンカー 1.0・5.5・10.0秒／輪 4.5秒」では、最後の輪が 16.5秒まで回り、終わり（14.5秒）に入らないので、
///   アンカーを 0.5・4.5・8.5秒、輪を 4.0秒（80°/秒で 320°）にした
/// </summary>
public class IllustratorPenEvent : KitTimedEvent
{
    const float Period = 4f, FirstAnchor = 0.5f, RingDelay = 1f, RingWarn = 1f, RingTime = 4f, RingRadius = 2.8f;
    const float HandleSize = 0.7f, AnchorSize = 0.5f, PenSpeed = 9f, PenWarn = 0.9f, PenSize = 0.8f;
    static readonly Color PathBlue = RuntimeSprites.Hex("4F9CFF");

    class Ring
    {
        public Vector2 Center;
        public KitMotionGroup Group;
        public readonly List<KitMotionMover> Handles = new List<KitMotionMover>();
        public readonly List<SpriteRenderer> Lines = new List<SpriteRenderer>();
    }

    readonly List<Ring> _rings = new List<Ring>();
    readonly List<KitMotionGroup> _pens = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    readonly List<SpriteRenderer> _pathDots = new List<SpriteRenderer>();
    readonly Vector2[] _anchors = new Vector2[3];
    GameObject _root;
    int _placed;

    public IllustratorPenEvent() : base("Pen Tool") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(IllustratorKit.Label, stage => new IllustratorPenEvent());

    protected override void Build()
    {
        _root = new GameObject("IllustratorPen");
        Track(_root);
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        _anchors[0] = new Vector2(a.center.x - 3.5f, floor + 4.5f);
        _anchors[1] = new Vector2(a.center.x + 3.5f, floor + 4.5f);
        _anchors[2] = new Vector2(a.center.x, floor + 2.5f);
        for (int k = 0; k < 3; k++)
        {
            int n = k;
            At(FirstAnchor + Period * k, () => PlaceAnchor(n));
            At(FirstAnchor + Period * k + RingDelay, () => StartRing(n));
        }
        if (Level >= 2)
        {
            At(7f, () => Pen(KitCorner.TopLeft));
            At(12f, () => Pen(KitCorner.TopRight));
        }
        At(14.5f, ClosePath);
        EndAt(15f);
    }

    void PlaceAnchor(int k)
    {
        Vector2 p = _anchors[k];
        // ペン先が来て打つ（0.3秒。飾り）→ アンカー（当たらない Pop）
        Track(KitMotionMark.Create(null, PenNib, p + new Vector2(0.45f, 0.45f), Vector2.one * PenSize, 135f, Color.white, KitMotionItem.OrderMark, 0.3f, "IllustratorPenTap").gameObject);
        float life = RingDelay + RingWarn + RingTime;
        KitMotion.Pop(Owner, p, 0f, life, 0, IllustratorArt.BoxHandle, Color.white, 0, AnchorSize);
        if (_placed > 0) DrawCurve(_anchors[k - 1], p);
        _placed++;
        GameAudio.Event("gm_illustrator_box");
    }

    void StartRing(int k)
    {
        Vector2 c = _anchors[k];
        float speed = Level <= 1 ? 60f : 80f;
        int count = Level >= 3 ? 3 : 2;
        var ring = new Ring { Center = c };
        ring.Group = KitMotion.Orbit(Owner, c, RingRadius, speed, count, RingTime, RingWarn, HandleDot, null, 1, HandleSize, 40f * k);
        if (ring.Group == null) return;
        foreach (var item in ring.Group.Items)
        {
            if (!(item is KitMotionMover m)) continue;
            ring.Handles.Add(m);
            ring.Lines.Add(AttackRunner.AddVisual(_root.transform, PlaceholderSprites.Square, PathBlue, Vector2.one * 0.1f, KitMotionItem.OrderWarn, "HandleLine"));
        }
        WarnRing(c, speed > 0f);
        _rings.Add(ring);
    }

    /// <summary>予告：回る輪のうすい赤い点線と、回る向きの印（RingWarn 秒）</summary>
    void WarnRing(Vector2 c, bool ccw)
    {
        Color red = new Color(KitMotion.Red.r, KitMotion.Red.g, KitMotion.Red.b, 0.45f);
        const int dots = 40;
        for (int i = 0; i < dots; i++)
        {
            float a = Mathf.PI * 2f * i / dots;
            Track(KitMotionMark.Create(_root.transform, PlaceholderSprites.Square, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius,
                Vector2.one * 2f * KitDress.Px, 0f, red, KitMotionItem.OrderWarn, RingWarn, "RingDot").gameObject);
        }
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            Vector2 at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius;
            float deg = (45f + 90f * i) + (ccw ? 90f : -90f); // 接線の向き
            Track(KitMotionMark.Create(_root.transform, KitMotionArt.Chevron, at, new Vector2(0.28f, 0.4f), deg, KitMotion.Red, KitMotionItem.OrderMark, RingWarn, "RingDir").gameObject);
        }
    }

    void Pen(KitCorner corner)
    {
        KitMotionGroup g = KitMotion.Diagonal(Owner, KitMotion.Corner(corner), PenSpeed, 1, 0f, PenWarn, PenNib, null, 1, PenSize);
        if (g != null) _pens.Add(g);
    }

    /// <summary>前のアンカーから今のアンカーへ、青い曲線（点を並べる。当たらない）</summary>
    void DrawCurve(Vector2 from, Vector2 to)
    {
        Vector2 ctrl = (from + to) * 0.5f + Vector2.up * 2f;
        const int n = 36;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector2 p = (1f - t) * (1f - t) * from + 2f * (1f - t) * t * ctrl + t * t * to;
            SpriteRenderer sr = AttackRunner.AddVisual(_root.transform, PlaceholderSprites.Square, PathBlue, Vector2.one * 2f * KitDress.Px, KitMotionItem.OrderZone, "PathDot");
            sr.transform.position = p;
            _pathDots.Add(sr);
        }
    }

    /// <summary>パスが閉じる：最後のアンカーから最初のアンカーへ線を足して、全部白く光って消える</summary>
    void ClosePath()
    {
        if (_placed >= 3) DrawCurve(_anchors[2], _anchors[0]);
        foreach (var sr in _pathDots)
        {
            if (sr == null) continue;
            sr.color = Color.white;
            Object.Destroy(sr.gameObject, 0.4f);
        }
        _pathDots.Clear();
        GameAudio.Event("gm_illustrator_scale_reset");
    }

    protected override void OnFrame(float clock)
    {
        // アンカーとハンドルの丸をつなぐ細い線
        for (int r = _rings.Count - 1; r >= 0; r--)
        {
            Ring ring = _rings[r];
            bool alive = ring.Group != null && !ring.Group.IsDone;
            for (int i = 0; i < ring.Lines.Count; i++)
            {
                SpriteRenderer line = ring.Lines[i];
                if (line == null) continue;
                KitMotionMover h = i < ring.Handles.Count ? ring.Handles[i] : null;
                if (!alive || h == null || h.IsDone)
                {
                    Object.Destroy(line.gameObject);
                    ring.Lines[i] = null;
                    continue;
                }
                SetLine(line, ring.Center, h.transform.position);
            }
            if (!alive) _rings.RemoveAt(r);
        }
        // ペン先：飛ぶ向きに回す
        _pens.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _pens)
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || m.Path == null || !_dressed.Add(m)) continue;
                Vector2 d = m.Path.At(0.1f) - m.Path.At(0f);
                if (d.sqrMagnitude > 1e-6f) m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                if (m.Body != null && m.Body.TryGetComponent(out BoxCollider2D box)) box.size = new Vector2(PenSize * 0.8f, PenSize * 0.4f);
            }
        _dressed.RemoveWhere(x => x == null);
    }

    static void SetLine(SpriteRenderer sr, Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        Transform t = sr.transform;
        t.position = (from + to) * 0.5f;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude) / Mathf.Max(0.001f, native.x), KitDress.Px / Mathf.Max(0.001f, native.y), 1f);
    }

    /// <summary>ハンドルの先の丸（7×7。白に青いふち）</summary>
    static Sprite HandleDot => RuntimeSprites.FromAscii("IllustratorEvent_Handle", new[]
    {
        "..bbb..",
        ".bwwwb.",
        "bwwwwwb",
        "bwwwwwb",
        "bwwwwwb",
        ".bwwwb.",
        "..bbb..",
    }, new Dictionary<char, Color32> { { 'b', IllustratorArt.BoxBlue }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>ペン先（右向き。14×14 の中に、オレンジのペン先）</summary>
    static Sprite PenNib => RuntimeSprites.FromAscii("IllustratorEvent_PenNib", new[]
    {
        "..............",
        "..............",
        "..............",
        "..............",
        "kkkkoo........",
        "kkkkoooo......",
        "kkkkoowoooo...",
        "kkkkoowoooooo.",
        "kkkkoooo......",
        "kkkkoo........",
        "..............",
        "..............",
        "..............",
        "..............",
    }, new Dictionary<char, Color32> { { 'o', IllustratorArt.Orange }, { 'w', new Color32(255, 255, 255, 255) }, { 'k', IllustratorArt.Brown } });
}
