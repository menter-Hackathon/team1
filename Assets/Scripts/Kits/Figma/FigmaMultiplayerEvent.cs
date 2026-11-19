using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Figma のステージの出来事「みんなで編集」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 10章）。
/// ・Diagonal（主）：飛びこんでくるほかの人のカーソル（名札つき）。4つの角と左右の壁の高さ 5 の 6か所から、毎回ちがう所。
///   プレイヤーへ予告の線 0.9秒 → 速さ 10 で飛ぶ → 反対の端で消える。5／6／8本・2.0秒おき（強さ 3 は 1.5秒おき）。
///   色は FigmaArt のカーソルの色（赤いカーソルは使わない。予告の線だけ赤）。通ったあとに細い色の線が 0.4秒残る（当たらない）
/// ・HitTarget：コメントのピン（紫のふちの白いしずく＋番号）。4.0秒は左、10.0秒は右の端の足場のすぐ上。5秒。
///   叩く（Resolve）：緑のチェック。そのとき飛んでいるカーソルが消え、次の 1本も来ない。叩かない：ピンの所に Pop が 1回（幅 2・予告 1.0秒）
/// ・FigmaRule の四角は、出来事の間も続く（置く物なので。決まりの側は直さない）
/// ・ピンの高さは、設計の「床から 6」ではなく「端の足場の上から 0.85」（床からだと約 5.7）にした（近接の判定は足から 0.2〜0.8 の高さ。
///   足場 4.83 の上に立って届く。数えただけで、ゲームの中ではまだ確かめていない）
/// </summary>
public class FigmaMultiplayerEvent : KitTimedEvent
{
    const float CursorSpeed = 10f, CursorWarn = 0.9f, CursorSize = 0.8f, WallHeight = 5f, TrailLife = 0.4f;
    const float PinLife = 5f, PinSize = 1f, PinAbovePlatform = 0.85f, PenaltySize = 2f, PenaltyWarn = 1f, PenaltyLife = 0.6f;
    static readonly Color[] Colors = { FigmaArt.CursorColor(1), FigmaArt.CursorColor(2), FigmaArt.Blue, FigmaArt.Purple, FigmaArt.Pink };
    static readonly string[] Names = { "PM", "Client", "Designer", "Dev", "Intern" };

    readonly List<KitMotionGroup> _cursors = new List<KitMotionGroup>();
    readonly Dictionary<KitMotionGroup, int> _who = new Dictionary<KitMotionGroup, int>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    GameObject _trails;
    bool _skipNext;
    int _shot;

    public FigmaMultiplayerEvent() : base("3 people joined") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(FigmaKit.Label, stage => new FigmaMultiplayerEvent());

    protected override void Build()
    {
        _trails = new GameObject("FigmaTrails");
        Track(_trails);
        int n = Level >= 3 ? 8 : Level == 2 ? 6 : 5;
        float every = Level >= 3 ? 1.5f : 2f;
        Every(1f, every, n, Shoot);
        At(4f, () => Pin(true, 1));
        At(10f, () => Pin(false, 2));
        EndAt(14f);
    }

    void Shoot(int i)
    {
        if (_skipNext) { _skipNext = false; return; } // ピンを叩いたので、次の 1本は来ない
        Rect a = KitMotion.Area;
        float wallY = KitMotion.FloorY() + WallHeight;
        // 6か所を、となり合わない順に回る
        Vector2 from = (i % 6) switch
        {
            0 => KitMotion.Corner(KitCorner.TopLeft),
            1 => new Vector2(a.xMax - 0.6f, wallY),
            2 => KitMotion.Corner(KitCorner.BottomLeft),
            3 => KitMotion.Corner(KitCorner.TopRight),
            4 => new Vector2(a.xMin + 0.6f, wallY),
            _ => KitMotion.Corner(KitCorner.BottomRight),
        };
        int who = _shot++ % Colors.Length;
        KitMotionGroup g = KitMotion.Diagonal(Owner, from, CursorSpeed, 1, 0f, CursorWarn, CursorSprite(Colors[who]), null, 1, CursorSize);
        if (g == null) return;
        _cursors.Add(g);
        _who[g] = who;
    }

    void Pin(bool left, int number)
    {
        Rect a = KitMotion.Area;
        float nearX = a.center.x + (left ? -8f : 8f);
        Vector2 at = FindEdgePlatform(nearX, out float x, out float top)
            ? new Vector2(x, top + PinAbovePlatform)
            : new Vector2(nearX, KitMotion.FloorY() + 4.83f + PinAbovePlatform);
        KitMotionTarget t = KitMotion.HitTarget(Owner, at, PinLife, () => OnPinResolved(at), () => OnPinMissed(at), PinSprite, Color.white, PinSize, 1, null, 0.5f);
        if (t == null || t.Body == null) return;
        TextMesh tm = WorldText.Create(t.Body, number.ToString(), 0.34f, FigmaArt.Purple, KitMotionItem.OrderBody + 1);
        tm.transform.localPosition = new Vector3(0f, 0.08f, -0.001f);
    }

    void OnPinResolved(Vector2 at)
    {
        FxAnim.Sprite(FxArt.Check, at + Vector2.up * 0.2f, FxKit.OrderToast - 1, 0.6f, RuntimeSprites.Hex("14AE5C"), 2f, "FigmaResolved");
        foreach (var g in _cursors) if (g != null && !g.IsDone) g.Cancel(); // 飛んでいる・予告中のカーソルを消す
        _cursors.Clear();
        _skipNext = true;
        GameAudio.Event("gm_figma_rect_place");
    }

    void OnPinMissed(Vector2 at)
    {
        KitMotion.Pop(Owner, at, PenaltyWarn, PenaltyLife, 0, null, FigmaArt.Purple, 1, PenaltySize);
    }

    protected override void OnFrame(float clock)
    {
        _cursors.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _cursors)
        {
            int who = _who.TryGetValue(g, out int w) ? w : 0;
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || m.Body == null || !_dressed.Add(m)) continue;
                Color c = Colors[who];
                FigmaArt.NameTag(m.Body, Names[who], c, new Vector2(0.15f, -0.3f), KitMotionItem.OrderBody + 1);
                m.gameObject.AddComponent<FigmaCursorTrail>().Setup(_trails.transform, c, TrailLife);
            }
        }
        _dressed.RemoveWhere(x => x == null);
    }

    /// <summary>nearX に近い、動かない端の足場（床から 3.5〜6）の上の面</summary>
    static bool FindEdgePlatform(float nearX, out float x, out float top)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        x = nearX; top = floor;
        float best = 3f;
        bool found = false;
        foreach (var c in Physics2D.OverlapAreaAll(new Vector2(a.xMin, floor + 3.5f), new Vector2(a.xMax, floor + 6f), LayerMask.GetMask("OneWayPlatform")))
        {
            if (c == null || c.isTrigger || AppStagePlatforms.IsUnsteady(c)) continue;
            Bounds b = c.bounds;
            float d = Mathf.Abs(b.center.x - nearX);
            if (d >= best) continue;
            best = d;
            x = b.center.x;
            top = b.max.y;
            found = true;
        }
        return found;
    }

    /// <summary>カーソル（FigmaArt.Cursor と同じ形を、14×14 のまん中に。四角い大きさで描いても形がくずれないように）</summary>
    static Sprite CursorSprite(Color c)
    {
        string key = "FigmaEvent_Cursor_" + ColorUtility.ToHtmlStringRGB(c);
        return RuntimeSprites.FromAscii(key, new[]
        {
            "..w...........",
            "..ww..........",
            "..wcw.........",
            "..wccw........",
            "..wcccw.......",
            "..wccccw......",
            "..wcccccw.....",
            "..wccccccw....",
            "..wcccccccw...",
            "..wccccwwwww..",
            "..wccwcw......",
            "..wcw.wcw.....",
            "..ww..wcw.....",
            ".......ww.....",
        }, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) }, { 'c', c } });
    }

    /// <summary>コメントのピン（16×16。紫のふちの白いしずく）</summary>
    static Sprite PinSprite => RuntimeSprites.FromAscii("FigmaEvent_Pin", new[]
    {
        ".....pppppp.....",
        "...pppwwwwppp...",
        "..ppwwwwwwwwpp..",
        ".ppwwwwwwwwwwpp.",
        ".pwwwwwwwwwwwwp.",
        "ppwwwwwwwwwwwwpp",
        "pwwwwwwwwwwwwwwp",
        "pwwwwwwwwwwwwwwp",
        "pwwwwwwwwwwwwwwp",
        "pwwwwwwwwwwwwwwp",
        "ppwwwwwwwwwwwwpp",
        ".pwwwwwwwwwwwwp.",
        ".ppwwwwwwwwwwpp.",
        "..ppwwwwwwwwpp..",
        "...ppppwppppp...",
        "pppp...ppp......",
    }, new Dictionary<char, Color32> { { 'p', FigmaArt.Purple }, { 'w', new Color32(255, 255, 255, 255) } });
}

/// <summary>【仮】飛ぶカーソルのうしろに、細い色の線（小さい点を置いて life 秒で消す。当たらない飾り）。2026-11-19</summary>
public class FigmaCursorTrail : MonoBehaviour
{
    const float Step = 0.12f;
    Transform _parent;
    Color _color;
    float _life;
    Vector2 _last;
    bool _started;

    public void Setup(Transform parent, Color color, float life)
    {
        _parent = parent;
        _color = color;
        _life = life;
        _last = transform.position;
    }

    void LateUpdate()
    {
        if (_parent == null) return;
        Vector2 p = transform.position;
        if (!_started) { _started = true; _last = p; return; }
        float dist = Vector2.Distance(p, _last);
        int n = Mathf.Min(20, Mathf.FloorToInt(dist / Step));
        for (int i = 1; i <= n; i++)
        {
            Vector2 at = Vector2.Lerp(_last, p, i / (float)n);
            var go = new GameObject("TrailDot");
            go.transform.SetParent(_parent, false);
            go.transform.position = at;
            SpriteRenderer sr = AttackRunner.AddVisual(go.transform, PlaceholderSprites.Square, _color, Vector2.one * 2f * KitDress.Px, KitMotionItem.OrderWarn, "Dot");
            go.AddComponent<FigmaTrailFade>().Setup(sr, _life);
        }
        if (n > 0) _last = p;
    }
}

/// <summary>【仮】線の点を life 秒でうすくして消す</summary>
public class FigmaTrailFade : MonoBehaviour
{
    SpriteRenderer _sr;
    Color _base;
    float _born, _life;

    public void Setup(SpriteRenderer sr, float life)
    {
        _sr = sr;
        _base = sr != null ? sr.color : Color.white;
        _born = Time.time;
        _life = Mathf.Max(0.05f, life);
    }

    void Update()
    {
        float k = (Time.time - _born) / _life;
        if (k >= 1f) { Destroy(gameObject); return; }
        if (_sr != null) _sr.color = new Color(_base.r, _base.g, _base.b, _base.a * (1f - k));
    }
}
