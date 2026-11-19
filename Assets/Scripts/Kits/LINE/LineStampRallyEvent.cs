using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】LINE の場面③の出来事「スタンプ連打」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 LINE）。
/// ・右下の入力欄（＋のボタンの所）と、左の友だちの列から、交互にスタンプが斜め上（45〜60度）へ打ち出される。
///   壁・床・高さ 4 の見えない天井で 3回跳ねて、4回目に当たる所で消える（GimmickA.MoveAlong。足場は通りぬける）
/// ・予告 0.9（最初の場所でうすく点滅＋最初に跳ねる所までの赤い線）→ 毎秒 7マス。間は 2.4／1.9／1.5秒、数 4／5／7（強さ 1／2／3）【仮】
///   同時に場にあるのは 2つまで（強さ 3 は 3つ）。終わりに間に合わない分は出さない（強さ 3 は 6つになる）
/// ・跳ねる高さは床から 4マスまで（上の段の足場の上は安全になる時がある）。乗ると落ちる足場（スタンプ）の ±1.5マスには、最初の着地を置かない
/// ・跳ねるたびに小さい白い輪（ポン）。スタンプは LineArt.Stamp の顔と親指（赤いハートのスタンプは出さない）
/// </summary>
public class LineStampRallyEvent : KitTimedEvent
{
    const float Warn = 0.9f, Speed = 7f, Cap = 4f, LastLaunch = 7.7f;
    static readonly Vector2 Size = new Vector2(1.2f, 0.8f);

    class Live
    {
        public KitMotionMover M;
        public Vector2 Last;
        public Vector2 Vel;
    }

    readonly List<Live> _live = new List<Live>();
    int _total, _maxLive, _fired;
    float _interval, _next;

    public LineStampRallyEvent() : base("Stickers") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(LineKit.Label, stage => new LineStampRallyEvent());

    protected override void Build()
    {
        _total = Level <= 1 ? 4 : Level == 2 ? 5 : 7;
        _interval = Level <= 1 ? 2.4f : Level == 2 ? 1.9f : 1.5f;
        _maxLive = Level >= 3 ? 3 : 2;
        _next = 0f;
        EndAt(10.6f);
    }

    protected override void OnFrame(float clock)
    {
        // 跳ねた所に小さい白い輪（向きが変わったら）
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            Live l = _live[i];
            if (l.M == null || l.M.IsDone)
            {
                FxKit.Ring(l.Last, 0.9f, 0.2f, Color.white); // 最後の所で消える
                _live.RemoveAt(i);
                continue;
            }
            Vector2 p = l.M.transform.position;
            Vector2 v = p - l.Last;
            if (l.M.MoveTime > 0.05f && v.sqrMagnitude > 1e-6f && l.Vel.sqrMagnitude > 1e-6f
                && (Mathf.Sign(v.x) != Mathf.Sign(l.Vel.x) && Mathf.Abs(l.Vel.x) > 1e-4f || Mathf.Sign(v.y) != Mathf.Sign(l.Vel.y) && Mathf.Abs(l.Vel.y) > 1e-4f))
                FxKit.Ring(l.Last, 0.9f, 0.2f, Color.white);
            if (v.sqrMagnitude > 1e-6f) l.Vel = v;
            l.Last = p;
        }

        if (Quiet || _fired >= _total || clock < _next || clock > LastLaunch) return;
        if (_live.Count >= _maxLive) { _next = clock + 0.2f; return; }
        Launch(_fired);
        _fired++;
        _next = clock + _interval;
    }

    void Launch(int index)
    {
        Rect a = View;
        float floor = Floor;
        float hx = Size.x * 0.5f, hy = Size.y * 0.5f;
        float top = Mathf.Min(floor + Cap, a.yMax) - hy;
        var box = new Rect(a.xMin + hx, floor + hy, Mathf.Max(0.5f, a.width - Size.x), Mathf.Max(0.5f, top - floor - hy));
        bool fromRight = index % 2 == 0;
        Vector2 from = fromRight ? new Vector2(a.xMax - 1.0f, floor + 0.9f) : new Vector2(a.xMin + 1.0f, floor + 2.2f);

        // 角度（45〜60度）：最初の着地が、乗ると落ちる足場の近くにならない物
        var falls = new List<float>();
        foreach (AppPlatformFall f in Object.FindObjectsOfType<AppPlatformFall>()) if (f != null) falls.Add(f.transform.position.x);
        float[] angles = { 45f, 50f, 55f, 60f };
        int start = Random.Range(0, angles.Length);
        List<Vector2> pts = null;
        for (int k = 0; k < angles.Length; k++)
        {
            float deg = angles[(start + k) % angles.Length];
            float rad = deg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad) * (fromRight ? -1f : 1f), Mathf.Sin(rad));
            List<Vector2> cand = GimmickA.BouncePoints(from, dir, box, 4);
            pts = cand;
            if (!NearFall(cand, box, falls)) break;
        }
        if (pts == null || pts.Count < 2) return;
        Sprite stamp = LineArt.Stamp(index % 2); // 0＝顔・1＝親指（2＝ハートは出さない）
        KitMotionMover m = GimmickA.MoveAlong(Owner, pts, Speed, Warn, stamp, null, Size, true, 1);
        if (m == null) return;
        _live.Add(new Live { M = m, Last = pts[0] });
        GameAudio.Event("gm_line_message", 1f + 0.08f * (index % 3));
    }

    /// <summary>最初の床の着地が、乗ると落ちる足場の x の ±1.5 に入るか</summary>
    static bool NearFall(List<Vector2> pts, Rect box, List<float> falls)
    {
        if (falls.Count == 0) return false;
        for (int i = 1; i < pts.Count; i++)
        {
            if (Mathf.Abs(pts[i].y - box.yMin) > 0.05f) continue;
            foreach (float fx in falls) if (Mathf.Abs(pts[i].x - fx) < 1.5f) return true;
            return false;
        }
        return false;
    }
}
