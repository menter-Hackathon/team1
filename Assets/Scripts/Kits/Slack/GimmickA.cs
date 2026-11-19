using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ステージのギミック A グループ（docs/stage-gimmick-variety-a.md）の出来事・決まりが使う小道具（2026-11-19）。
/// ・動かない足場だけを選ぶ（UI の足場の動く・消える・落ちる物は除く。UI の足場が無いステージは、今までのフォルダの足場）
/// ・足場の段の高さ（下の段・上の段）
/// ・横から来る四角い物（KitMotion.EdgeSweep は四角の大きさが1つなので、横長の板・タイル・封筒用）
/// ・端から決まった所まですべって来て止まる物（Slack の板）
/// ・決まった箱の中で跳ねる物（KitMotion.Bounce は部屋ぜんぶの箱なので、高さを決めたいとき。LINE・Discord）
/// ・小さいドットの字（3×5）
/// KitMotion の内側の道具（Make・WarnArrow・WarnLine・WarnFor など。同じアセンブリ）を使う。Kits/Common は変えない
/// </summary>
public static class GimmickA
{
    // ───────── 足場 ─────────

    /// <summary>
    /// 動かない足場の当たり判定の範囲（左から順）。UI の足場があれば、static のうち今乗れる物だけ。
    /// 無ければ、すり抜け床（フォルダ）のうち seconds 秒は乗れる物
    /// </summary>
    public static List<Bounds> SteadyPlatforms(Rect area, float seconds = 3f)
    {
        var list = new List<Bounds>();
        if (AppStagePlatforms.Active)
        {
            foreach (AppPlatform p in UnityEngine.Object.FindObjectsOfType<AppPlatform>())
            {
                if (p == null || !p.IsSteady || !p.IsSolidNow) continue;
                if (!p.TryGetComponent(out BoxCollider2D col) || !col.enabled) continue;
                Bounds b = col.bounds;
                if (b.center.x < area.xMin || b.center.x > area.xMax || b.center.y < area.yMin || b.center.y > area.yMax) continue;
                list.Add(b);
            }
        }
        else
        {
            foreach (Bounds b in TalkKitUtil.Platforms(area)) if (AppStagePlatforms.IsSolidFor(b, seconds)) list.Add(b);
        }
        list.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        return list;
    }

    /// <summary>画面の中の足場（動く・消える物もふくむ）の上の面の高さ（床から）。低い順・0.6 以上ちがう物だけ</summary>
    public static List<float> Tiers(Rect area, float floor)
    {
        var heights = new List<float>();
        foreach (Bounds b in TalkKitUtil.Platforms(area))
        {
            float h = b.max.y - floor;
            if (h < 1.2f || h > 9f) continue;
            heights.Add(h);
        }
        heights.Sort();
        var tiers = new List<float>();
        foreach (float h in heights) if (tiers.Count == 0 || h - tiers[tiers.Count - 1] > 0.6f) tiers.Add(h);
        return tiers;
    }

    /// <summary>下の段の足場の上の面の高さ（床から。無ければ fallback）</summary>
    public static float LowTier(Rect area, float floor, float fallback = 3f)
    {
        List<float> t = Tiers(area, floor);
        return t.Count > 0 ? t[0] : fallback;
    }

    /// <summary>上の段の足場の上の面の高さ（床から。無ければ fallback）</summary>
    public static float HighTier(Rect area, float floor, float fallback = 5f)
    {
        List<float> t = Tiers(area, floor);
        return t.Count > 1 ? t[1] : fallback;
    }

    /// <summary>x の足元（Ground とすり抜け床。動く・消える UI の足場は飛ばす）。無ければ false</summary>
    public static bool GroundBelow(Vector2 from, out float y, float distance = 30f)
    {
        y = 0f;
        RaycastHit2D hit = AppStagePlatforms.RaycastSteady(from, Vector2.down, distance, LayerMask.GetMask("Ground", "OneWayPlatform"));
        if (hit.collider == null) return false;
        y = hit.point.y;
        return true;
    }

    // ───────── 横から来る四角い物 ─────────

    /// <summary>
    /// 横から：KitMotion.EdgeSweep と同じ（端に矢印と赤い線の予告 → 反対の端まで横切る。count 個を gap 秒おき）。
    /// 体は size（横×たて）の四角。y は床からの高さ（まん中）。dress：出たあと、1つずつ見た目を足すとき（何個目か）
    /// </summary>
    public static KitMotionGroup SweepBox(KitOwner owner, KitSide side, float y, Vector2 size, float speed = 7f, int count = 1, float gap = 0.6f,
        float wave = 0f, float warn = KitMotion.DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float waveLength = 4f,
        Action<KitMotionMover, int> dress = null)
    {
        Rect a = KitMotion.Area;
        float wy = KitMotion.FloorY() + y;
        var g = KitMotion.Make<KitMotionGroup>(owner, null, "GimmickSweep", a.center);
        bool harm = damage > 0;
        float w = KitMotion.WarnFor(g.Owner, warn, harm);
        count = Mathf.Max(1, count);
        gap = Mathf.Max(0f, gap);
        float lane = w + (count - 1) * gap;
        Color wc = KitMotion.WarnColor(harm, color);
        Color bc = KitMotion.BodyColor(sprite, color);
        foreach (KitSide s in Lanes(side))
        {
            KitSide lan = s;
            bool fromLeft = lan == KitSide.Left;
            KitMotion.WarnArrow(g.transform, new Vector2(fromLeft ? a.xMin + 0.55f : a.xMax - 0.55f, wy), fromLeft ? Vector2.right : Vector2.left, lane, wc);
            if (wave <= 0.01f) KitMotion.WarnLine(g.transform, new Vector2(a.xMin, wy), new Vector2(a.xMax, wy), lane, wc);
            else KitMotionMark.Create(g.transform, PlaceholderSprites.Square, new Vector2(a.center.x, wy), new Vector2(a.width, wave * 2f + size.y),
                0f, new Color(wc.r, wc.g, wc.b, 0.14f), KitMotionItem.OrderWarn, lane, "WaveBand");
            for (int i = 0; i < count; i++)
            {
                int n = i;
                g.At(w + i * gap, () =>
                {
                    KitPath path = KitPath.Sweep(lan, wy, speed, wave, waveLength, size.x * 0.5f + 0.5f, a);
                    var m = KitMotion.Make<KitMotionMover>(owner, g, "Sweep", path.At(0f));
                    m.BuildBody(sprite, bc, size, false, damage);
                    m.Setup(path, 0f, false);
                    if (dress != null) KitMotion.Safe(() => dress(m, n));
                });
            }
        }
        g.MinLife = lane;
        if (harm) GameAudio.Telegraph();
        return g;
    }

    /// <summary>
    /// 端からすべって来て止まる：画面の左か右の端（高さ y＝世界）から toX まで、毎秒 speed マス。予告：端の矢印と、止まる所までの赤い線。
    /// 着いたら Finished（自分で最後まで行ったときだけ）。体は size の四角
    /// </summary>
    public static KitMotionMover Slide(KitOwner owner, KitSide from, float y, float toX, Vector2 size, float speed = 5f, float warn = KitMotion.DefaultWarn,
        Sprite sprite = null, Color? color = null, int damage = 1)
    {
        Rect a = KitMotion.Area;
        bool fromLeft = from == KitSide.Left;
        Vector2 start = new Vector2(fromLeft ? a.xMin - size.x * 0.5f - 0.2f : a.xMax + size.x * 0.5f + 0.2f, y);
        Vector2 end = new Vector2(toX, y);
        var m = KitMotion.Make<KitMotionMover>(owner, null, "GimmickSlide", start);
        bool harm = damage > 0;
        float w = KitMotion.WarnFor(m.Owner, warn, harm);
        m.BuildBody(sprite, KitMotion.BodyColor(sprite, color), size, false, damage);
        Color wc = KitMotion.WarnColor(harm, color);
        KitMotion.WarnArrow(m.transform, new Vector2(fromLeft ? a.xMin + 0.55f : a.xMax - 0.55f, y), fromLeft ? Vector2.right : Vector2.left, w, wc);
        KitMotion.WarnLine(m.transform, new Vector2(fromLeft ? a.xMin : a.xMax, y), end, w, wc);
        m.Setup(KitPath.Line(start, end, speed), w, false);
        if (harm) GameAudio.Telegraph();
        return m;
    }

    // ───────── 決まった箱の中で跳ねる ─────────

    /// <summary>box（物のまん中が動ける範囲）の中を、dir の向きから跳ね返る点（最初の点から）。stop が true を返した点で止める</summary>
    public static List<Vector2> BouncePoints(Vector2 from, Vector2 dir, Rect box, int maxBounces, Func<int, Vector2, bool, bool> stop = null)
    {
        var pts = new List<Vector2>();
        Vector2 p = new Vector2(Mathf.Clamp(from.x, box.xMin, box.xMax), Mathf.Clamp(from.y, box.yMin, box.yMax));
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(1f, 1f).normalized;
        pts.Add(p);
        for (int i = 0; i < Mathf.Max(0, maxBounces); i++)
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
            bool wall = tx <= ty + 1e-4f;
            if (stop != null && stop(i, p, wall)) return pts;
            if (wall) d.x = -d.x;
            if (ty <= tx + 1e-4f) d.y = -d.y;
        }
        return pts;
    }

    /// <summary>
    /// 跳ねる物を、点の並び（折れ線）で動かす。予告：最初の点でうすく点滅＋最初の区間の赤い線（warn 秒）。
    /// 体は size（横×たて）。circle なら丸い判定
    /// </summary>
    public static KitMotionMover MoveAlong(KitOwner owner, IList<Vector2> points, float speed, float warn, Sprite sprite, Color? color, Vector2 size,
        bool circle = true, int damage = 1, bool warnWholeLane = false)
    {
        if (points == null || points.Count < 2) return null;
        KitPath path = KitPath.Polyline(points, speed);
        Vector2 p0 = points[0];
        var m = KitMotion.Make<KitMotionMover>(owner, null, "GimmickBounce", p0);
        bool harm = damage > 0;
        float w = KitMotion.WarnFor(m.Owner, warn, harm);
        m.BuildBody(sprite, KitMotion.BodyColor(sprite, color), size, circle, damage);
        Color wc = KitMotion.WarnColor(harm, color);
        KitMotion.WarnLine(m.transform, p0, points[1], w, wc);
        if (warnWholeLane)
            for (int i = 2; i < points.Count; i++) KitMotion.WarnLine(m.transform, points[i - 1], points[i], w, new Color(wc.r, wc.g, wc.b, 0.45f));
        m.Setup(path, w, true);
        if (harm) GameAudio.Telegraph();
        return m;
    }

    // ───────── 小さいドットの字 ─────────

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'A', new[] { ".X.", "X.X", "XXX", "X.X", "X.X" } },
        { 'D', new[] { "XX.", "X.X", "X.X", "X.X", "XX." } },
        { 'E', new[] { "XXX", "X..", "XX.", "X..", "XXX" } },
        { 'I', new[] { "XXX", ".X.", ".X.", ".X.", "XXX" } },
        { 'K', new[] { "X.X", "XX.", "X..", "XX.", "X.X" } },
        { 'L', new[] { "X..", "X..", "X..", "X..", "XXX" } },
        { 'P', new[] { "XX.", "X.X", "XX.", "X..", "X.." } },
        { '[', new[] { "XX", "X.", "X.", "X.", "XX" } },
        { ']', new[] { "XX", ".X", ".X", ".X", "XX" } },
    };

    /// <summary>3×5 の字を、grid（[y, x]）の (x0, y0) から ch で書く。vertical なら字を縦に並べる（1ドットあける）</summary>
    public static void Stamp(char[,] grid, string text, int x0, int y0, char ch, bool vertical = false)
    {
        int gh = grid.GetLength(0), gw = grid.GetLength(1);
        int cx = x0, cy = y0;
        foreach (char c in text)
        {
            if (!Glyphs.TryGetValue(char.ToUpperInvariant(c), out string[] g)) { if (vertical) cy += 6; else cx += 4; continue; }
            for (int y = 0; y < g.Length; y++)
                for (int x = 0; x < g[y].Length; x++)
                {
                    if (g[y][x] != 'X') continue;
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && py >= 0 && px < gw && py < gh) grid[py, px] = ch;
                }
            if (vertical) cy += 6;
            else cx += g[0].Length + 1;
        }
    }

    /// <summary>grid（[y, x]。'.' は透明）を文字の行にする（RuntimeSprites.FromAscii 用）</summary>
    public static string[] Rows(char[,] grid)
    {
        int h = grid.GetLength(0), w = grid.GetLength(1);
        var rows = new string[h];
        var sb = new System.Text.StringBuilder(w);
        for (int y = 0; y < h; y++)
        {
            sb.Length = 0;
            for (int x = 0; x < w; x++) sb.Append(grid[y, x] == '\0' ? '.' : grid[y, x]);
            rows[y] = sb.ToString();
        }
        return rows;
    }

    /// <summary>w×h の grid（全部 '.'）</summary>
    public static char[,] Grid(int w, int h)
    {
        var g = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[y, x] = '.';
        return g;
    }

    /// <summary>grid の四角を塗る</summary>
    public static void Fill(char[,] g, int x0, int y0, int w, int h, char ch)
    {
        int gh = g.GetLength(0), gw = g.GetLength(1);
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < gw && y < gh) g[y, x] = ch;
    }

    /// <summary>grid の丸を塗る（まん中 cx, cy・半径 r。ドットの中心で比べる）</summary>
    public static void Disc(char[,] g, float cx, float cy, float r, char ch)
    {
        int gh = g.GetLength(0), gw = g.GetLength(1);
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) g[y, x] = ch;
            }
    }

    static IEnumerable<KitSide> Lanes(KitSide side)
    {
        if (side != KitSide.Right) yield return KitSide.Left;
        if (side != KitSide.Left) yield return KitSide.Right;
    }
}
