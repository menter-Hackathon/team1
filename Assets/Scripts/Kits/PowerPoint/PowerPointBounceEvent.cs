using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】PowerPoint の場面③の出来事「バウンド」（2026-11-19。docs/stage-gimmick-variety-b.md 7章）。数値はすべて【仮】。
/// ・アニメーションの「バウンド」：図形（丸・星・矢印。直径 1.4）が、画面の左上か右上の角の外から斜め下へ入って、
///   床で跳ねながら（左右の壁では跳ね返る）進み、4回跳ねて消える。跳ねる高さは 1回ごとに低くなる（3.5 → 2.5 → 1.8 → 1.2。本物のバウンド）
/// ・1個目はプレイヤーから遠い方の角から、2.5秒おきに左右交互に 4個（ステージで 3〜5個）。同時に 2個まで
/// ・予告：入る角に矢印＋最初に落ちる床の場所に赤い印（1.0秒）。跳ねるたびに、次に落ちる場所へ小さい赤い印（0.3秒前）。跳ねた所に星の小さい効果
/// ・高く跳ねている間に下をくぐる。低くなったらジャンプで越える。上の足場にいれば届かない
/// ・KitMotion.Bounce は壁・天井で同じ高さに跳ね返る道なので、重さのある弧（GroupBMotion.HopPath）を自分で作る
/// </summary>
public class PowerPointBounceEvent : KitTimedEvent
{
    const float Size = 1.4f, Gravity = 26f, SpeedX = 5f, EnterHeight = 7.5f;
    static readonly float[] Hops = { 3.5f, 2.5f, 1.8f, 1.2f };
    const float Step = 2.5f, Warn = 1f, MarkLead = 0.3f;

    class Shape
    {
        public KitMotionMover Mover;
        public List<Vector2> Landings; // 床に着く（動き出してからの秒数, x）
        public int Marked;             // 印を出した数（最初の 1つは予告で出す）
        public int Landed;             // 跳ねた数
    }

    readonly int _stage;
    readonly List<Shape> _shapes = new List<Shape>();
    bool _fromLeft;

    public PowerPointBounceEvent(int stage) : base("Bounce")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        int count = GroupBMotion.Scaled(4, _stage);
        _fromLeft = KitMotion.PlayerPos.x >= View.center.x; // 1個目はプレイヤーから遠い方の角から
        for (int i = 0; i < count; i++)
        {
            int n = i;
            At(i * Step, () => Launch(n));
        }
        EndAt((count - 1) * Step + Warn + LifeSeconds() + 0.3f);
    }

    /// <summary>1個の図形が動いている秒数（予告のあと）</summary>
    float LifeSeconds()
    {
        float t = Mathf.Sqrt(2f * (EnterHeight - Size * 0.5f) / Gravity);
        foreach (float h in Hops) t += 2f * Mathf.Sqrt(2f * Gravity * h) / Gravity;
        return t;
    }

    void Launch(int index)
    {
        bool fromLeft = _fromLeft;
        _fromLeft = !_fromLeft;
        Rect v = View;
        float floor = Floor;
        Vector2 from = new Vector2(fromLeft ? v.xMin - 0.8f : v.xMax + 0.8f, floor + EnterHeight);
        var landings = new List<Vector2>();
        KitPath path = GroupBMotion.HopPath(from, fromLeft ? SpeedX : -SpeedX, Gravity, Hops, floor, Size * 0.5f, v, landings);
        KitMotionMover m = GroupBMotion.Mover(Owner, path, Vector2.one * Size, ShapeSprite(index % 3), Color.white, Warn, false, 1, "PPT_BounceShape", 0.8f, true);
        if (m == null) return;
        // 予告：入る角に矢印（斜め下へ）＋最初に落ちる床の場所に赤い印
        Vector2 corner = new Vector2(fromLeft ? v.xMin + 0.6f : v.xMax - 0.6f, Mathf.Min(floor + EnterHeight, v.yMax - 0.6f));
        KitMotion.WarnArrow(m.transform, corner, new Vector2(fromLeft ? 1f : -1f, -0.8f).normalized, m.Warn, KitMotion.Red);
        if (landings.Count > 0) LandMark(m.transform, landings[0].y, floor, m.Warn); // landings は（秒, x）
        _shapes.Add(new Shape { Mover = m, Landings = landings, Marked = 1 });
        GameAudio.Event("gm_powerpoint_point");
    }

    void LandMark(Transform parent, float x, float floor, float seconds)
    {
        KitMotionMark.Create(parent, KitMotionArt.Disc(9), new Vector2(x, floor + 0.08f), new Vector2(1.3f, 0.25f), 0f, KitMotion.Red,
            KitMotionItem.OrderWarn, seconds, "LandMark");
    }

    protected override void OnFrame(float clock)
    {
        float floor = Floor;
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            Shape s = _shapes[i];
            if (s.Mover == null || s.Mover.IsDone)
            {
                _shapes.RemoveAt(i);
                continue;
            }
            if (s.Mover.Warning) continue;
            float t = s.Mover.MoveTime;
            // 次に落ちる場所へ、小さい赤い印（0.3秒前）。印は動かない入れ物（この出来事の物）に置く
            while (s.Marked < s.Landings.Count && t >= s.Landings[s.Marked].x - MarkLead)
            {
                Vector2 l = s.Landings[s.Marked];
                var holder = new GameObject("PPT_LandMark");
                Track(holder);
                LandMark(holder.transform, l.y, floor, MarkLead);
                Object.Destroy(holder, MarkLead + 0.1f);
                s.Marked++;
            }
            // 跳ねた所に、星の小さい効果（最後は消える）
            while (s.Landed < s.Landings.Count && t >= s.Landings[s.Landed].x)
            {
                Vector2 l = s.Landings[s.Landed];
                Vector2 at = new Vector2(l.y, floor + 0.2f);
                bool last = s.Landed == s.Landings.Count - 1;
                FxAnim.Sprite(FxArt.Star4, at, 40, 0.3f, new Color(1f, 0.85f, 0.3f), last ? 1.6f : 1f, "PPT_BounceStar").Pop().Rise(0.2f);
                if (last) FxKit.Ring(at + Vector2.up * 0.5f, 1.6f, 0.2f, Color.white);
                s.Landed++;
            }
        }
    }

    /// <summary>跳ねる図形（25×25 ドット＝約 1.4マス）：0＝丸（オレンジ）・1＝星（黄）・2＝矢印（オレンジ）</summary>
    static Sprite ShapeSprite(int kind)
    {
        Color32 orange = new Color32(0xED, 0x7D, 0x31, 0xFF), yellow = new Color32(0xFF, 0xC0, 0x00, 0xFF);
        Color32 edge = new Color32(0xB8, 0x4E, 0x14, 0xFF), white = new Color32(255, 255, 255, 255);
        switch (kind)
        {
            case 0:
                return KitArt.Paint("ppt.event.circle", 25, 25, c =>
                {
                    c.Disc(12, 12, 12, edge);
                    c.Disc(12, 12, 10, orange);
                    c.Disc(8, 8, 2, white);
                });
            case 1:
                return KitArt.Paint("ppt.event.star", 25, 25, c =>
                {
                    // 5つの先の星（点が多角形の中か）
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (90f + 36f * i) * Mathf.Deg2Rad;
                        float r = i % 2 == 0 ? 12f : 5f;
                        pts[i] = new Vector2(12f + Mathf.Cos(a) * r, 12f - Mathf.Sin(a) * r);
                    }
                    for (int y = 0; y < 25; y++)
                        for (int x = 0; x < 25; x++)
                            if (Inside(pts, new Vector2(x + 0.5f, y + 0.5f))) c.Px(x, y, yellow);
                    c.Disc(12, 13, 1, white);
                });
            default:
                return KitArt.Paint("ppt.event.arrow", 25, 25, c =>
                {
                    c.Fill(2, 9, 12, 7, edge);
                    c.Fill(3, 10, 11, 5, orange);
                    for (int i = 0; i < 11; i++)
                    {
                        c.VLine(13 + i, 1 + i, 23 - 2 * i, edge);
                        if (i < 10) c.VLine(13 + i, 3 + i, Mathf.Max(1, 19 - 2 * i), orange);
                    }
                });
        }
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
        }
        return inside;
    }
}
