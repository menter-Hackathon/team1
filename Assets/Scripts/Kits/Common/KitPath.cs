using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】KitMotion の道すじ（2026-11-19。docs/stage-gimmick-variety.md 1章）。担当0 の物。
// ・「動き出してからの秒数 → 場所」の関数と、長さ（秒）。KitMotion の動く部品・Catch・HitTarget が使う
// ・組み合わせの例：HitTarget(…, path: KitPath.Orbit(boss.transform, 2.5f, 90f))（回る的）、
//   Catch(owner, KitPath.Sweep(KitSide.Left, floor + 3.5f, 3.5f, wave: 1f), …)（波打って流れてくる良い物）、
//   Catch(owner, KitPath.Bounce(from, dir, 6f, 2, KitMotion.BoxFor(0.8f)).Then(6f), …)（跳ねて止まる）

/// <summary>画面の左右</summary>
public enum KitSide { Left, Right, Both }

/// <summary>画面の角</summary>
public enum KitCorner { BottomLeft, BottomRight, TopLeft, TopRight }

/// <summary>
/// まわる向き（画面で見て）。CW＝時計回り：左下からなら、左の壁を上へ → 天井を右へ。
/// CCW＝反時計回り：左下からなら、床を右へ → 右の壁を上へ → 天井を左へ
/// </summary>
public enum KitTurn { CW, CCW }

/// <summary>道すじ（秒 → 場所）</summary>
public sealed class KitPath
{
    readonly Func<float, Vector2> _at;
    bool _logged;

    /// <summary>道の長さ（秒）。終わりの無い道は PositiveInfinity</summary>
    public float Duration { get; }

    /// <summary>関数から作る（at：動き出してからの秒数 → 場所。seconds：長さ）</summary>
    public KitPath(Func<float, Vector2> at, float seconds)
    {
        _at = at ?? (_ => Vector2.zero);
        Duration = float.IsNaN(seconds) ? 0f : Mathf.Max(0f, seconds);
    }

    /// <summary>t 秒の場所（0〜Duration に丸める）</summary>
    public Vector2 At(float t)
    {
        t = Mathf.Clamp(t, 0f, Duration);
        try { return _at(t); }
        catch (Exception e)
        {
            if (!_logged) Debug.LogException(e);
            _logged = true;
            return Vector2.zero;
        }
    }

    /// <summary>最後の場所</summary>
    public Vector2 End => At(float.IsInfinity(Duration) ? 0f : Duration);

    /// <summary>終わりの場所で、さらに seconds 秒止まる道</summary>
    public KitPath Then(float seconds)
    {
        if (float.IsInfinity(Duration)) return this;
        float d = Duration;
        Vector2 end = End;
        return new KitPath(t => t <= d ? At(t) : end, d + Mathf.Max(0f, seconds));
    }

    /// <summary>ずっと同じ場所（seconds 秒）</summary>
    public static KitPath Stay(Vector2 point, float seconds = float.PositiveInfinity) => new KitPath(_ => point, seconds);

    /// <summary>from から to へ、毎秒 speed マスでまっすぐ（着いたら終わり）</summary>
    public static KitPath Line(Vector2 from, Vector2 to, float speed)
    {
        float len = (to - from).magnitude;
        float dur = speed > 0.01f ? len / speed : 0f;
        return new KitPath(t => dur <= 0f ? to : Vector2.Lerp(from, to, t / dur), dur);
    }

    /// <summary>from から dir の向きへ、毎秒 speed マスで seconds 秒</summary>
    public static KitPath Ray(Vector2 from, Vector2 dir, float speed, float seconds)
    {
        Vector2 v = (dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right) * speed;
        return new KitPath(t => from + v * t, seconds);
    }

    /// <summary>from から dir の向きへ、毎秒 speed マスで、area（＋margin）の外に出るまで</summary>
    public static KitPath Out(Vector2 from, Vector2 dir, float speed, Rect area, float margin = 1f)
    {
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
        Rect box = Grow(area, margin);
        float dist = ExitDistance(from, d, box);
        return Ray(from, d, speed, speed > 0.01f ? dist / speed : 0f);
    }

    /// <summary>
    /// 画面の端から端へ横切る（side＝Left：左の端から右へ。Right：右から左へ）。y は高さ（世界の y）。
    /// wave：上下の振れ幅（マス。0 ならまっすぐ）。waveLength：波1つの長さ（マス）。area：省略で今の画面
    /// </summary>
    public static KitPath Sweep(KitSide side, float y, float speed, float wave = 0f, float waveLength = 4f, float margin = 1f, Rect? area = null)
    {
        Rect a = area ?? KitMotion.Area;
        bool fromLeft = side != KitSide.Right;
        float x0 = fromLeft ? a.xMin - margin : a.xMax + margin;
        float x1 = fromLeft ? a.xMax + margin : a.xMin - margin;
        float len = Mathf.Abs(x1 - x0);
        float dur = speed > 0.01f ? len / speed : 0f;
        float k = Mathf.PI * 2f / Mathf.Max(0.5f, waveLength);
        return new KitPath(t =>
        {
            float s = Mathf.Min(t * speed, len);
            float x = x0 + (fromLeft ? s : -s);
            return new Vector2(x, y + wave * Mathf.Sin(s * k));
        }, dur);
    }

    /// <summary>
    /// center（動く物。体のまん中）のまわりを回る。speed：度/秒（＋で反時計回り）。startDeg：最初の角度（0＝右）。
    /// reverseAt：その秒数で逆回りにする（0 ならしない）。center が消えたら、最後の場所のまわり
    /// </summary>
    public static KitPath Orbit(Transform center, float radius, float speed, float startDeg = 0f, float seconds = float.PositiveInfinity, float reverseAt = 0f)
    {
        Vector2 last = center != null ? KitMotion.CenterOf(center) : KitMotion.Area.center;
        return Orbit(() =>
        {
            if (center != null) last = KitMotion.CenterOf(center);
            return last;
        }, radius, speed, startDeg, seconds, reverseAt);
    }

    /// <summary>決まった点のまわりを回る（ほかは Transform の形と同じ）</summary>
    public static KitPath Orbit(Vector2 center, float radius, float speed, float startDeg = 0f, float seconds = float.PositiveInfinity, float reverseAt = 0f)
        => Orbit(() => center, radius, speed, startDeg, seconds, reverseAt);

    /// <summary>関数で決まる点のまわりを回る</summary>
    public static KitPath Orbit(Func<Vector2> center, float radius, float speed, float startDeg = 0f, float seconds = float.PositiveInfinity, float reverseAt = 0f)
    {
        return new KitPath(t =>
        {
            float tt = reverseAt > 0f && t > reverseAt ? 2f * reverseAt - t : t;
            float a = (startDeg + speed * tt) * Mathf.Deg2Rad;
            Vector2 c = center != null ? center() : Vector2.zero;
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }, seconds);
    }

    /// <summary>
    /// box の中を、ふちで跳ね返りながら進む（bounces 回跳ねたら、まっすぐ box の外へ出て終わり）。
    /// box は「物のまん中」が動ける範囲（KitMotion.BoxFor(物の大きさ) で作る）。from が外なら中に入れる
    /// </summary>
    public static KitPath Bounce(Vector2 from, Vector2 dir, float speed, int bounces, Rect box, float margin = 1f)
    {
        var pts = new List<Vector2>();
        Vector2 p = new Vector2(Mathf.Clamp(from.x, box.xMin, box.xMax), Mathf.Clamp(from.y, box.yMin, box.yMax));
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(1f, 1f).normalized;
        pts.Add(p);
        for (int i = 0; i < Mathf.Max(0, bounces); i++)
        {
            float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
            if (d.x > 1e-4f) tx = (box.xMax - p.x) / d.x;
            else if (d.x < -1e-4f) tx = (box.xMin - p.x) / d.x;
            if (d.y > 1e-4f) ty = (box.yMax - p.y) / d.y;
            else if (d.y < -1e-4f) ty = (box.yMin - p.y) / d.y;
            float t = Mathf.Max(0.001f, Mathf.Min(tx, ty));
            if (float.IsInfinity(t)) break;
            p += d * t;
            pts.Add(p);
            if (tx <= ty + 1e-4f) d.x = -d.x;
            if (ty <= tx + 1e-4f) d.y = -d.y;
        }
        float exit = ExitDistance(p, d, Grow(box, margin + 0.5f));
        pts.Add(p + d * exit);
        return Polyline(pts, speed);
    }

    /// <summary>
    /// box のふちを這う（start の角から、dir の向きに laps 周）。box は「物のまん中」が動ける範囲（KitMotion.BoxFor）
    /// </summary>
    public static KitPath Perimeter(Rect box, KitCorner start, KitTurn dir, float speed, float laps = 1f)
    {
        // 反時計回り（画面で見て）の角の順：左下 → 右下 → 右上 → 左上
        var ccw = new[] { new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin), new Vector2(box.xMax, box.yMax), new Vector2(box.xMin, box.yMax) };
        int s = start == KitCorner.BottomLeft ? 0 : start == KitCorner.BottomRight ? 1 : start == KitCorner.TopRight ? 2 : 3;
        int step = dir == KitTurn.CCW ? 1 : 3; // 時計回りは逆の順
        var loop = new List<Vector2>(5);
        for (int i = 0; i <= 4; i++) loop.Add(ccw[(s + step * i) % 4]);
        float perimeter = 2f * (box.width + box.height);
        float total = perimeter * Mathf.Max(0.01f, laps);
        float dur = speed > 0.01f ? total / speed : 0f;
        KitPath one = Polyline(loop, 1f); // 1周（距離で引く）
        return new KitPath(t =>
        {
            float dist = Mathf.Min(t * speed, total);
            float m = perimeter > 0.01f ? dist % perimeter : 0f;
            if (dist >= total - 1e-4f && Mathf.Abs(laps - Mathf.Round(laps)) < 1e-3f) m = 0f; // ちょうど周りきった
            return one.At(m);
        }, dur);
    }

    /// <summary>from から to へ、seconds 秒でゆるい弧（height：まん中でどれだけ上にふくらむか。マイナスで下）</summary>
    public static KitPath Arc(Vector2 from, Vector2 to, float height, float seconds)
    {
        float dur = Mathf.Max(0.05f, seconds);
        return new KitPath(t =>
        {
            float k = t / dur;
            return Vector2.Lerp(from, to, k) + Vector2.up * (4f * height * k * (1f - k));
        }, dur);
    }

    /// <summary>点をつないだ折れ線を、毎秒 speed マスで</summary>
    public static KitPath Polyline(IList<Vector2> points, float speed)
    {
        if (points == null || points.Count == 0) return Stay(Vector2.zero, 0f);
        var pts = new List<Vector2>(points);
        var cum = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        float total = cum[pts.Count - 1];
        float dur = speed > 0.01f ? total / speed : 0f;
        return new KitPath(t =>
        {
            float s = Mathf.Min(t * speed, total);
            for (int i = 1; i < pts.Count; i++)
            {
                if (s > cum[i] && i < pts.Count - 1) continue;
                float seg = cum[i] - cum[i - 1];
                return seg > 1e-5f ? Vector2.Lerp(pts[i - 1], pts[i], (s - cum[i - 1]) / seg) : pts[i];
            }
            return pts[pts.Count - 1];
        }, dur);
    }

    // ───────── 道具 ─────────

    static Rect Grow(Rect r, float m) => new Rect(r.xMin - m, r.yMin - m, r.width + m * 2f, r.height + m * 2f);

    /// <summary>p から d の向きに進んで、box から出るまでの距離（外にいれば、入って出るまで）</summary>
    internal static float ExitDistance(Vector2 p, Vector2 d, Rect box)
    {
        float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
        if (d.x > 1e-4f) tx = (box.xMax - p.x) / d.x;
        else if (d.x < -1e-4f) tx = (box.xMin - p.x) / d.x;
        if (d.y > 1e-4f) ty = (box.yMax - p.y) / d.y;
        else if (d.y < -1e-4f) ty = (box.yMin - p.y) / d.y;
        float t = Mathf.Min(tx, ty);
        if (float.IsInfinity(t) || t < 0f) t = 0f;
        return Mathf.Min(t, 200f);
    }
}
