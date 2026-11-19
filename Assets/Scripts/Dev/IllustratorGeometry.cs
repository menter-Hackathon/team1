using UnityEngine;

/// <summary>描いたパスと当たり判定で同じ座標を使うための2D図形処理。</summary>
public static class IllustratorGeometry
{
    public static Vector3 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    public static Vector3[] Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int segments = 64)
    {
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++) points[i] = Cubic(a, b, c, d, i / (float)segments);
        return points;
    }

    public static bool SegmentHits(Bounds bounds, Vector3 a, Vector3 b, float radius = 0f)
    {
        Vector3 min = bounds.min, max = bounds.max;
        float enter = 0f, exit = 1f;
        return Clip(a.x, b.x - a.x, min.x - radius, max.x + radius, ref enter, ref exit)
            && Clip(a.y, b.y - a.y, min.y - radius, max.y + radius, ref enter, ref exit);
    }

    static bool Clip(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (System.Math.Abs(delta) < 0.000001f) return origin >= min && origin <= max;
        float a = (min - origin) / delta, b = (max - origin) / delta;
        if (a > b) { float swap = a; a = b; b = swap; }
        enter = System.Math.Max(enter, a);
        exit = System.Math.Min(exit, b);
        return enter <= exit;
    }

    public static bool PolygonHits(Vector3[] polygon, Bounds bounds)
    {
        if (polygon == null || polygon.Length < 3) return false;
        if (Contains(polygon, bounds.center)) return true;
        for (int i = 0; i < polygon.Length; i++)
            if (SegmentHits(bounds, polygon[i], polygon[(i + 1) % polygon.Length])) return true;
        return false;
    }

    public static bool Contains(Vector3[] polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector3 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y)
                && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}
