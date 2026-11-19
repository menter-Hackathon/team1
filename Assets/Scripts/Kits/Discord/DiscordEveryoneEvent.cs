using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Discord の場面③の出来事「@everyone」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Discord）。
/// ・左の壁（サーバーの列の丸）から、@ の玉が右へ打ち出される。ゆるい角度（水平から ±20度）で、自分の段の高さの帯（±0.6）の中を
///   上下に跳ねながら、右の壁で跳ね返って左の壁まで戻って消える（1往復。GimmickA.BouncePoints＋MoveAlong）
///   段は 3つ（床のすぐ上・下の段の足場の高さ・上の段の足場の高さ）。同時に 2つまで・2つの段まで（今いる玉と同じ段は選ばない）
/// ・予告 0.9（丸が光り、進む線の赤い点線）→ 毎秒 8マス。間は 2.6／2.0／1.6秒、数 3／4／6（強さ 1／2／3）【仮】。
///   5.6秒より後には出さない（同時に 2つまでなので、強さ 3 でも 3〜4個になる）
/// ・WallCrawl：1回（強さ 3 は 2回）、白い縦長の未読の印が、左下の角から 床 → 右の壁 → 天井 → 左の壁 と 1周（毎秒 9マス。予告 1.0）。床ではジャンプで越える
/// ・切断（DiscordRule）は出来事の間は出ない（StageRule の CanSpawnHazard）
/// 変えた所：玉は 2往復 → 1往復（12秒の中に 3〜4個収めるため）
/// </summary>
public class DiscordEveryoneEvent : KitTimedEvent
{
    const float Warn = 0.9f, Speed = 8f, Angle = 20f, Band = 0.6f, BallSize = 0.8f, LastLaunch = 5.6f;
    const float CrawlSpeed = 9f, CrawlWarn = 1.0f;

    class Ball
    {
        public KitMotionMover M;
        public int Lane;
    }

    readonly List<Ball> _balls = new List<Ball>();
    int _total, _fired;
    float _interval, _next;
    float[] _lanes;

    public DiscordEveryoneEvent() : base("@everyone") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(DiscordKit.Label, stage => new DiscordEveryoneEvent());

    protected override void Build()
    {
        _total = Level <= 1 ? 3 : Level == 2 ? 4 : 6;
        _interval = Level <= 1 ? 2.6f : Level == 2 ? 2.0f : 1.6f;
        Rect a = View;
        float floor = Floor;
        float low = GimmickA.LowTier(a, floor, 3f), high = GimmickA.HighTier(a, floor, low + 2f);
        _lanes = new[] { 0.75f, low + 0.75f, high + 0.75f }; // 床から（玉のまん中）
        At(2.5f, () => Crawl());
        if (Level >= 3) At(4.5f, () => Crawl());
        EndAt(11.6f); // 1画面のステージ（20秒で打ち切り）。最後の玉が戻りきるまで
    }

    protected override void OnFrame(float clock)
    {
        _balls.RemoveAll(b => b.M == null || b.M.IsDone);
        if (Quiet || _fired >= _total || clock < _next || clock > LastLaunch) return;
        if (_balls.Count >= 2) { _next = clock + 0.2f; return; }
        Launch();
        _fired++;
        _next = clock + _interval;
    }

    void Launch()
    {
        Rect a = View;
        float floor = Floor;
        // 段：今いる玉と同じ段は選ばない（2つの段まで）
        var lanes = new List<int>();
        for (int i = 0; i < _lanes.Length; i++)
        {
            if (floor + _lanes[i] + Band + BallSize * 0.5f > a.yMax) continue;
            bool used = false;
            foreach (Ball b in _balls) if (b.Lane == i) used = true;
            if (!used) lanes.Add(i);
        }
        if (lanes.Count == 0) return;
        int lane = lanes[Random.Range(0, lanes.Count)];
        float y = floor + _lanes[lane];
        float h = BallSize * 0.5f;
        float yMin = Mathf.Max(floor + h, y - Band), yMax = Mathf.Min(a.yMax - h, y + Band);
        var box = new Rect(a.xMin + h + 0.1f, yMin, Mathf.Max(0.5f, a.width - BallSize - 0.2f), Mathf.Max(0.1f, yMax - yMin));
        float deg = Random.value < 0.5f ? Angle : -Angle;
        Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        Vector2 from = new Vector2(box.xMin, y);
        // 右の壁で 1回跳ね返って、左の壁に着いたら止める（帯の上下で跳ねる分は数えない）
        int walls = 0;
        List<Vector2> pts = GimmickA.BouncePoints(from, dir, box, 60, (i, p, wall) => wall && ++walls >= 2);
        KitMotionMover m = GimmickA.MoveAlong(Owner, pts, Speed, Warn, AtBall, null, Vector2.one * BallSize, true, 1, true);
        if (m == null) return;
        _balls.Add(new Ball { M = m, Lane = lane });
        // サーバーの丸が光る（予告の間）
        KitMotionMark.Create(m.transform, KitMotionArt.Ring(18), from, Vector2.one * 1.1f, 0f, DiscordArt.Blurple, KitMotionItem.OrderMark, m.Warn, "Server");
        GameAudio.Event("gm_discord_vc_join", 1.2f);
    }

    void Crawl()
    {
        KitMotionMover m = KitMotion.WallCrawl(Owner, KitCorner.BottomLeft, KitTurn.CCW, CrawlSpeed, CrawlWarn, UnreadMark, Color.white, 1, 0.8f, 1f);
        if (m != null && m.Visual != null)
        {
            Vector3 s = m.Visual.transform.localScale;
            m.Visual.transform.localScale = new Vector3(s.x * 0.5f, s.y, 1f); // 縦長
        }
    }

    /// <summary>ブループルの丸に白い @（14×14）</summary>
    static Sprite AtBall
    {
        get
        {
            char[,] g = GimmickA.Grid(14, 14);
            GimmickA.Disc(g, 7f, 7f, 7f, 'B');
            string[] at =
            {
                ".XXXXX.",
                "X.....X",
                "X.XXX.X",
                "X.X.X.X",
                "X.XXXX.",
                "X......",
                ".XXXXX.",
            };
            for (int y = 0; y < at.Length; y++)
                for (int x = 0; x < at[y].Length; x++)
                    if (at[y][x] == 'X') g[3 + y, 3 + x] = 'W';
            var pal = new Dictionary<char, Color32> { { 'B', (Color32)DiscordArt.Blurple }, { 'W', new Color32(255, 255, 255, 255) } };
            return RuntimeSprites.FromAscii("DiscordEveryoneBall", GimmickA.Rows(g), pal);
        }
    }

    /// <summary>白い縦長の未読の印（6×12。角を丸く）</summary>
    static Sprite UnreadMark
    {
        get
        {
            char[,] g = GimmickA.Grid(6, 12);
            GimmickA.Fill(g, 1, 0, 4, 12, 'W');
            GimmickA.Fill(g, 0, 1, 6, 10, 'W');
            var pal = new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } };
            return RuntimeSprites.FromAscii("DiscordEveryoneUnread", GimmickA.Rows(g), pal);
        }
    }
}
