using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>オフラインで走ってくる物の種類（2026-11-19）</summary>
public enum ChromeObstacle
{
    SmallCactus = 0,
    BigCactus = 1,
    LowBird = 2,
    HighBird = 3,
}

/// <summary>【仮】オフラインの数値（2026-11-19。docs/app-feedback-v2-design.md 2-3）。ボスの技とステージの出来事で同じ物を使う</summary>
[Serializable]
public class ChromeOfflineSettings
{
    [Tooltip("【仮】予告の秒数（最初の物が出るまで）")]
    public float Telegraph = 1f;
    [Tooltip("【仮】走ってくる速さ（マス/秒）")]
    public float Speed = 8f;
    [Tooltip("【仮】物と物の間（秒）")]
    public float Gap = 0.7f;
    [Tooltip("【仮】サボテンの次が低い翼竜のときの間（秒）。ジャンプして着地するまで 約0.68秒＋0.3秒")]
    public float JumpGap = 1.0f;
    [Tooltip("【仮】壁から出る何秒前に、赤い「！」を出すか")]
    public float WarnLead = 0.8f;
    [Tooltip("【仮】終わりの秒数（幕と地面の線が消える）")]
    public float EndTime = 0.5f;
    [Tooltip("【仮】白っぽい幕の濃さ")]
    public float VeilAlpha = 0.3f;
    [Tooltip("【仮】低い翼竜のまん中の高さ（床から、マス。判定 0.5 なので 1.5〜2.0）")]
    public float LowBirdY = 1.75f;
    [Tooltip("【仮】高い翼竜のまん中の高さ（床から、マス。3.2〜3.7）")]
    public float HighBirdY = 3.45f;
}

/// <summary>
/// 【仮】オフラインの進行（2026-11-19。docs/app-feedback-v2-design.md 2-3・2-5）。
/// ・ボス部屋（CreateInRoom）：出てくる側の壁から反対の壁へ。ボスのウィンドウの中身をオフラインのページにする。全部が通りすぎたら終わる
/// ・ステージ（CreateInView）：画面（カメラ）の右の端から左へ。床の高さに合わせて走り、穴に来たら消える。決めた秒数で終わる（幕はその間ずっと）
/// 出した物は全部この GameObject の子（形態変化・正常化・出来事の終わりで、いっしょに消える）。幕もこの子（カメラに合わせて毎フレーム動かす）
/// </summary>
public class ChromeOfflineEffect : CustomEffect
{
    /// <summary>出す物1つ分の予定（技が始まってから At 秒で、壁・画面の端から出る）</summary>
    public struct Planned
    {
        public ChromeObstacle Kind;
        public float At;
    }

    class Obstacle
    {
        public ChromeObstacle Kind;
        public Transform Root;
        public Rigidbody2D Body;
        public Hitbox Hit;
        public SpriteRenderer Art;
        public Sprite[] Frames;
        public Transform Ground;
        public float Lift;     // 床から足元までの高さ
        public float GroundY;
        public float Born;
        public float FadeAt = -1f;
    }

    class Warn
    {
        public SpriteRenderer R;
        public float From, To;
        public float Lift;
        public bool Low;
    }

    const int VeilOrder = 9;          // 床・足場・背景より手前、キャラクターより後ろ
    const int GroundOrder = 12;
    const int ObstacleOrder = 20;
    const int AlertOrder = 51;
    const int PageOrder = 42;
    const float BirdFlap = 0.15f;
    const float MaxSeconds = 30f;

    bool _scroll;
    bool _lingers;
    AttackRunner _owner;
    AttackData _data;
    ChromeOfflineSettings _cfg;
    Rect _arena;
    float _floorY;
    bool _fromRight;
    float _start;
    float _holdUntil = -1f;
    float _nightAt = -1f;      // 恐竜ゲームの「夜」（幕が暗い灰色になる。飾りだけ。2-5 の B）
    const float NightLength = 2.5f;
    static readonly Color NightColor = RuntimeSprites.Hex("2B2B2B");
    float _closeAt = -1f;
    readonly List<Planned> _plan = new List<Planned>();
    int _next;
    readonly List<Obstacle> _obstacles = new List<Obstacle>();
    readonly List<Warn> _warns = new List<Warn>();
    SpriteRenderer _veil, _sideAlert, _page;
    SpriteRenderer _scrollAlert;   // ステージ：次に出る物の赤い「！」（画面の右の端。出る高さ）
    Transform _groundLine;
    readonly List<SpriteRenderer> _groundParts = new List<SpriteRenderer>();

    public override bool Lingers => _lingers;

    // ───────── 予定の作り方 ─────────

    /// <summary>並びの文字（S・B・L・H）から予定を作る。間は Gap、サボテンの次が低い翼竜のときだけ JumpGap</summary>
    public static List<Planned> PlanFromSequence(string sequence, ChromeOfflineSettings cfg)
    {
        var list = new List<Planned>();
        if (string.IsNullOrEmpty(sequence) || cfg == null) return list;
        float at = cfg.Telegraph;
        ChromeObstacle? prev = null;
        foreach (char c in sequence)
        {
            ChromeObstacle k;
            switch (char.ToUpperInvariant(c))
            {
                case 'S': k = ChromeObstacle.SmallCactus; break;
                case 'B': k = ChromeObstacle.BigCactus; break;
                case 'L': k = ChromeObstacle.LowBird; break;
                case 'H': k = ChromeObstacle.HighBird; break;
                default: continue;
            }
            if (prev.HasValue)
            {
                bool cactusThenLow = (prev == ChromeObstacle.SmallCactus || prev == ChromeObstacle.BigCactus) && k == ChromeObstacle.LowBird;
                at += cactusThenLow ? cfg.JumpGap : cfg.Gap;
            }
            list.Add(new Planned { Kind = k, At = at });
            prev = k;
        }
        return list;
    }

    /// <summary>ボス部屋の技の長さ（最後の物が反対の壁に着いて、幕が消えるまで）</summary>
    public static float RoomDuration(List<Planned> plan, ChromeOfflineSettings cfg, float roomWidth)
    {
        float last = plan != null && plan.Count > 0 ? plan[plan.Count - 1].At : cfg.Telegraph;
        return last + (roomWidth + 2f) / Mathf.Max(0.1f, cfg.Speed) + cfg.EndTime;
    }

    // ───────── 出し方 ─────────

    /// <summary>ボス部屋で出す（ボスの技）。fromRight＝右の壁から出る</summary>
    public static ChromeOfflineEffect CreateInRoom(AttackRunner owner, AttackData data, ChromeOfflineSettings cfg, List<Planned> plan, Rect arena, bool fromRight)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("ChromeOffline", new Vector2(arena.center.x, arena.yMin));
        var fx = go.AddComponent<ChromeOfflineEffect>();
        fx.Init(owner, data, cfg, plan, false, true);
        fx._arena = arena;
        fx._floorY = arena.yMin;
        fx._fromRight = fromRight;
        fx.BuildRoom();
        return fx;
    }

    /// <summary>
    /// 画面の右の端から出す（ステージの出来事。2-5）。holdSeconds 秒で終わる（幕はその間ずっと）。
    /// 走ってくる物は床の高さに合わせる。穴の上には出さない（出る所に床が無ければ、少し待つ）
    /// </summary>
    public static ChromeOfflineEffect CreateInView(AttackRunner owner, AttackData data, ChromeOfflineSettings cfg, List<Planned> plan, float holdSeconds)
    {
        if (owner == null || data == null || cfg == null) return null;
        Rect view = ScreenUi.ViewRect();
        GameObject go = owner.CreateSpawnedObject("ChromeOfflineStage", view.center);
        var fx = go.AddComponent<ChromeOfflineEffect>();
        fx.Init(owner, data, cfg, plan, true, false);
        fx._holdUntil = Mathf.Max(1f, holdSeconds);
        fx.BuildVeil();
        fx._scrollAlert = BrowserDevFx.Show(fx.transform, DevArt.Alert(), Vector2.zero, AlertOrder, 1f, "Alert");
        fx._scrollAlert.enabled = false;
        return fx;
    }

    /// <summary>今すぐ終わりにする（幕がうすくなって消える。走っている物も消える）</summary>
    /// <summary>at 秒（出してから）に、恐竜ゲームの「夜」を1回はさむ（幕が暗い灰色になって戻る。飾りだけ。docs 2-5 の B）</summary>
    public void SetNight(float at) => _nightAt = at;

    public void Close()
    {
        if (_closeAt < 0f)
        {
            _closeAt = Time.time;
            if (_scroll) GameAudio.Event("gm_chrome_online"); // ステージの出来事のときだけ（ボスの技では鳴らさない）
        }
    }

    void Init(AttackRunner owner, AttackData data, ChromeOfflineSettings cfg, List<Planned> plan, bool scroll, bool lingers)
    {
        _owner = owner;
        _data = data;
        _cfg = cfg;
        _scroll = scroll;
        _lingers = lingers;
        _start = Time.time;
        if (plan != null) _plan.AddRange(plan);
        _plan.Sort((a, b) => a.At.CompareTo(b.At));
    }

    void BuildVeil()
    {
        Vector2 v = FxKit.ViewSize();
        _veil = BrowserDevFx.Rect(transform, Vector2.zero, v + Vector2.one, BrowserDevFx.WithAlpha(ChromeArt.Paper, 0f), VeilOrder, "Veil");
        PlaceVeil();
    }

    void PlaceVeil()
    {
        if (_veil == null) return;
        Camera cam = Camera.main;
        Vector2 c = cam != null ? (Vector2)cam.transform.position : ScreenUi.ViewRect().center;
        _veil.transform.position = new Vector3(c.x, c.y, 0f);
    }

    void BuildRoom()
    {
        BuildVeil();
        // 地面の線（床の上。出てくる側から伸びる）
        float w = _arena.width;
        _groundLine = ChromeArt.GroundLine(transform, new Vector2(_arena.xMin - transform.position.x, 0f), w, GroundOrder);
        _groundParts.AddRange(_groundLine.GetComponentsInChildren<SpriteRenderer>());
        _groundLine.localScale = new Vector3(0f, 1f, 1f);
        // 出てくる側の壁ぎわの床に、赤い「！」
        _sideAlert = BrowserDevFx.Show(transform, DevArt.Alert(), Vector2.zero, AlertOrder, 1f, "SideAlert");
        _sideAlert.transform.position = new Vector2(WallX(0.5f), _floorY + 0.45f);
        _sideAlert.enabled = false;
        // ボスのウィンドウの中身：オフラインのページ
        _page = BrowserDevFx.Show(transform, ChromeArt.OfflinePage(), Vector2.zero, PageOrder, 1f, "OfflinePage");
        _page.transform.localScale = Vector3.zero;
        PlacePage();
        // 1つずつの赤い「！」（出る高さ・出る 0.8秒前から）
        foreach (Planned p in _plan)
        {
            var r = BrowserDevFx.Show(transform, DevArt.Alert(), Vector2.zero, AlertOrder, 1f, "Alert");
            r.enabled = false;
            float lift = CenterLift(p.Kind);
            r.transform.position = new Vector2(WallX(0.5f), _floorY + lift);
            _warns.Add(new Warn { R = r, From = p.At - _cfg.WarnLead, To = p.At, Lift = lift, Low = lift < 1f });
        }
    }

    void PlacePage()
    {
        if (_page == null || _owner == null) return;
        Bounds b = BrowserDevFx.OwnerBounds(_owner);
        _page.transform.position = new Vector2(b.center.x, b.center.y);
    }

    /// <summary>出てくる側の壁から inset マス内側の x（ボス部屋）</summary>
    float WallX(float inset) => _fromRight ? _arena.xMax - inset : _arena.xMin + inset;

    float Dir => _scroll ? -1f : (_fromRight ? -1f : 1f);

    // ───────── 物の大きさ ─────────

    static Vector2 HitSize(ChromeObstacle k) => k switch
    {
        ChromeObstacle.SmallCactus => new Vector2(0.4f, 0.8f),
        ChromeObstacle.BigCactus => new Vector2(1.2f, 1.1f),
        _ => new Vector2(1.0f, 0.5f),
    };

    static float ArtHalfWidth(ChromeObstacle k) => k == ChromeObstacle.SmallCactus ? 0.31f : k == ChromeObstacle.BigCactus ? 0.7f : 0.62f;

    /// <summary>判定の下の端の高さ（床から）</summary>
    float FootLift(ChromeObstacle k) => k switch
    {
        ChromeObstacle.LowBird => _cfg.LowBirdY - 0.25f,
        ChromeObstacle.HighBird => _cfg.HighBirdY - 0.25f,
        _ => 0f,
    };

    /// <summary>まん中の高さ（床から。赤い「！」の高さ）</summary>
    float CenterLift(ChromeObstacle k) => FootLift(k) + HitSize(k).y * 0.5f;

    // ───────── 進行 ─────────

    void Update()
    {
        float now = Time.time;
        float t = now - _start;
        bool blink = Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        if (t > MaxSeconds) Close();
        if (_scroll && _holdUntil > 0f && t >= _holdUntil) Close();

        // 幕：0.3秒でかかる。終わりはうすくなって消える
        float closeK = _closeAt >= 0f ? Mathf.Clamp01((now - _closeAt) / Mathf.Max(0.05f, _cfg.EndTime)) : 0f;
        if (_veil != null)
        {
            PlaceVeil();
            float a = _cfg.VeilAlpha * Mathf.Clamp01(t / 0.3f) * (1f - closeK);
            // 夜：0.4秒で暗くなり、NightLength 秒で戻る
            float night = 0f;
            if (_nightAt >= 0f && t >= _nightAt && t < _nightAt + NightLength)
            {
                float n = t - _nightAt;
                night = Mathf.Clamp01(Mathf.Min(n, NightLength - n) / 0.4f);
            }
            Color veil = Color.Lerp(ChromeArt.Paper, NightColor, night);
            _veil.color = BrowserDevFx.WithAlpha(veil, Mathf.Lerp(a, a * 1.3f, night));
        }
        if (_groundLine != null)
        {
            float grow = Mathf.Clamp01(t / Mathf.Max(0.1f, _cfg.Telegraph * 0.6f));
            _groundLine.localScale = new Vector3(grow, 1f, 1f);
            // 出てくる側から伸びる（右から来るなら、右の端を固定）
            float left = _arena.xMin - transform.position.x;
            _groundLine.localPosition = new Vector3(_fromRight ? left + _arena.width * (1f - grow) : left, 0f, 0f);
            foreach (var sr in _groundParts) if (sr != null) sr.color = BrowserDevFx.WithAlpha(ChromeArt.Grey, 1f - closeK);
        }
        if (_page != null)
        {
            PlacePage();
            float s = _closeAt >= 0f ? 1f - closeK : BrowserDevFx.Pop(t, 0.12f);
            _page.transform.localScale = new Vector3(s, s, 1f);
        }

        // 1つずつの赤い「！」
        bool lowWarn = false;
        foreach (var w in _warns)
        {
            bool on = _closeAt < 0f && t >= w.From && t < w.To;
            w.R.enabled = on && blink;
            if (on && w.Low) lowWarn = true;
            if (on && _scroll) w.R.transform.position = ScrollWarnPos(w.Lift);
        }
        if (_sideAlert != null) _sideAlert.enabled = _closeAt < 0f && t < _cfg.Telegraph && !lowWarn && blink;
        // ステージ：次に出る物の赤い「！」（出る 0.8秒前から。穴で出るのを待ったときも、いっしょにずれる）
        if (_scrollAlert != null)
        {
            bool on = _closeAt < 0f && _next < _plan.Count && t >= _plan[_next].At - _cfg.WarnLead && t < _plan[_next].At;
            _scrollAlert.enabled = on && blink;
            if (on)
            {
                ChromeObstacle k = _plan[_next].Kind == ChromeObstacle.HighBird ? ChromeObstacle.LowBird : _plan[_next].Kind;
                _scrollAlert.transform.position = ScrollWarnPos(CenterLift(k));
            }
        }

        // 出す
        while (_closeAt < 0f && _next < _plan.Count && t >= _plan[_next].At)
        {
            if (!SpawnObstacle(_plan[_next].Kind))
            {
                // ステージ：出る所に床が無い（穴）→ 0.2秒あとにもう一度
                Planned p = _plan[_next];
                p.At = t + 0.2f;
                _plan[_next] = p;
                break;
            }
            if (_scroll)
            {
                // ステージの出来事のときだけ。翼竜は少し高い音
                ChromeObstacle kind = _plan[_next].Kind;
                GameAudio.Event("gm_chrome_obstacle", kind == ChromeObstacle.LowBird || kind == ChromeObstacle.HighBird ? 1.3f : 1f);
            }
            _next++;
        }

        // 絵（翼竜の羽）と、消えていく物
        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle o = _obstacles[i];
            if (o.Root == null)
            {
                _obstacles.RemoveAt(i);
                continue;
            }
            if (o.Frames != null && o.Frames.Length > 1) o.Art.sprite = o.Frames[Mathf.FloorToInt((now - o.Born) / BirdFlap) % 2];
            if (_closeAt >= 0f && o.FadeAt < 0f) Fade(o);
            if (o.FadeAt >= 0f)
            {
                float k = (now - o.FadeAt) / 0.15f;
                if (k >= 1f)
                {
                    Destroy(o.Root.gameObject);
                    _obstacles.RemoveAt(i);
                    continue;
                }
                o.Art.color = new Color(1f, 1f, 1f, 1f - k);
            }
        }

        // ボス部屋：全部出し終えて、全部通りすぎたら終わる
        if (!_scroll && _closeAt < 0f && _next >= _plan.Count && _obstacles.Count == 0) Close();
        if (_closeAt >= 0f && closeK >= 1f && _obstacles.Count == 0) Destroy(gameObject);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Rect view = ScreenUi.ViewRect();
        foreach (Obstacle o in _obstacles)
        {
            if (o.Body == null || o.FadeAt >= 0f) continue;
            Vector2 p = o.Body.position;
            float x = p.x + Dir * _cfg.Speed * dt;
            float half = ArtHalfWidth(o.Kind);
            if (_scroll)
            {
                // 床の高さに合わせる。穴に来たら消える。画面の左の端で消える
                if (!FindGround(x, o.GroundY, out float gy)) { Fade(o); continue; }
                o.GroundY = gy;
                if (x < view.xMin - half) { Fade(o); continue; }
            }
            else
            {
                // 反対の壁に着いたら消える
                bool arrived = Dir > 0f ? x > _arena.xMax - half : x < _arena.xMin + half;
                if (arrived) { Fade(o); continue; }
            }
            o.Body.MovePosition(new Vector2(x, o.GroundY + o.Lift));
        }
    }

    bool SpawnObstacle(ChromeObstacle kind)
    {
        float half = ArtHalfWidth(kind);
        float x, groundY;
        if (_scroll)
        {
            Rect view = ScreenUi.ViewRect();
            x = view.xMax + half;
            float probe = view.center.y;
            if (Pacer() is StagePacer pacer && pacer.Player != null) probe = pacer.Player.position.y + 1f;
            if (!FindGroundBelow(x, Mathf.Max(probe, view.yMin + 3f), out groundY)) return false;
            if (!FindGround(x + Dir * 1.0f, groundY, out _)) return false; // 進む先のすぐそばが穴なら出さない
        }
        else
        {
            x = WallX(half);
            groundY = _floorY;
        }
        if (_scroll && (kind == ChromeObstacle.HighBird)) kind = ChromeObstacle.LowBird; // 道中は高い翼竜を出さない

        Vector2 hit = HitSize(kind);
        float lift = FootLift(kind);
        var root = new GameObject($"Offline_{kind}");
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector2(x, groundY + lift);
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = hit;
        box.offset = new Vector2(0f, hit.y * 0.5f);
        Hitbox hb = _owner.AddHitbox(root, _data, box, true);
        hb.Activate();

        var o = new Obstacle { Kind = kind, Root = root.transform, Body = rb, Hit = hb, Lift = lift, GroundY = groundY, Born = Time.time };
        Color edge = ChromeArt.DangerRed;
        bool flip = Dir > 0f; // 翼竜の絵は左向き。右へ進むときは返す
        switch (kind)
        {
            case ChromeObstacle.SmallCactus:
                o.Art = BrowserDevFx.Show(root.transform, ChromeArt.SmallCactus(edge), new Vector2(0f, 9f * BrowserDevFx.Dot), ObstacleOrder, 1f, "Cactus");
                break;
            case ChromeObstacle.BigCactus:
                o.Art = BrowserDevFx.Show(root.transform, ChromeArt.BigCactus(edge), new Vector2(0f, 11.5f * BrowserDevFx.Dot), ObstacleOrder, 1f, "Cactus");
                break;
            default:
                o.Frames = new[] { ChromeArt.Bird(true, edge), ChromeArt.Bird(false, edge) };
                o.Art = BrowserDevFx.Show(root.transform, o.Frames[0], new Vector2(0f, hit.y * 0.5f), ObstacleOrder, 1f, "Bird");
                o.Art.flipX = flip;
                break;
        }
        // ステージ：足元に短い地面の線（いっしょに動く。道中の床は形がばらばらなので、部屋いっぱいの線は引かない）
        if (_scroll && lift < 0.01f)
        {
            o.Ground = ChromeArt.GroundLine(root.transform, new Vector2(-half - 0.4f, -lift), half * 2f + 0.8f, GroundOrder);
        }
        _obstacles.Add(o);
        return true;
    }

    void Fade(Obstacle o)
    {
        if (o.FadeAt >= 0f) return;
        o.FadeAt = Time.time;
        if (o.Hit != null) o.Hit.Deactivate();
    }

    Vector2 ScrollWarnPos(float lift)
    {
        Rect view = ScreenUi.ViewRect();
        float x = view.xMax - 0.5f;
        float probe = view.center.y;
        if (Pacer() is StagePacer pacer && pacer.Player != null) probe = pacer.Player.position.y + 1f;
        float gy = FindGroundBelow(x, Mathf.Max(probe, view.yMin + 3f), out float g) ? g : view.yMin + 1f;
        return new Vector2(x, gy + lift);
    }

    static StagePacer Pacer() => StagePacer.Current;

    /// <summary>x の真下の床（今の床から 1マス上〜3マス下）。無ければ false</summary>
    bool FindGround(float x, float currentY, out float groundY) => Ray(new Vector2(x, currentY + 1f), 4f, out groundY);

    /// <summary>x の、高さ fromY から真下の床。無ければ false</summary>
    bool FindGroundBelow(float x, float fromY, out float groundY) => Ray(new Vector2(x, fromY), 30f, out groundY);

    bool Ray(Vector2 from, float length, out float groundY)
    {
        groundY = from.y;
        int mask = _owner != null && _owner.GroundMask.value != 0 ? _owner.GroundMask.value : LayerMask.GetMask("Ground", "OneWayPlatform");
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(mask);
        var hits = new RaycastHit2D[6];
        int n = Physics2D.Raycast(from, Vector2.down, filter, hits, length);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0f) continue;
            if (hits[i].collider.attachedRigidbody != null && hits[i].collider.attachedRigidbody.transform.IsChildOf(transform)) continue;
            groundY = hits[i].point.y;
            return true;
        }
        return false;
    }
}
