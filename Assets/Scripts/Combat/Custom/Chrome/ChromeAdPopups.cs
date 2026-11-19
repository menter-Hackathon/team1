using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ポップアップ広告の形4種類（2026-11-19：本物の Chrome だけ。優先度 B。docs/app-feedback-v2-design.md 2-2）。
/// 形：四角 3×2（今の形）、横長のバナー 4×1.2、たて長 1.6×3、小さい四角 2×1.6。形ごとの攻撃データ（名前に PopupAppear。窓の絵が付く）を _shapes に持つ。
/// ・1枚目（プレイヤーに重なる物）は、いつも「四角」か「小さい四角」（避ける距離を今より増やさない）。たて長・バナーは、2枚目以降だけ
/// ・判定・足場の大きさ・予告の点線の枠は、形の攻撃データの「物の大きさ」（FallingObject がそのまま使う）。デザインは絵の側で別に引く（BrowserSkin の袋）
/// ・置き方の決まりは今のまま（1枚目は相手の体に重ねる。ほかは相手から離れた空中。ボス・地形に重ねない。ジャンプで上に乗れる高さ）
/// </summary>
[CreateAssetMenu(fileName = "Chrome_AdPopups", menuName = "Team1/Custom Attack/Chrome Ad Popups")]
public class ChromeAdPopups : CustomAttack
{
    [Tooltip("形ごとの攻撃データ（四角・バナー・たて長・小さい四角の順。種類 Spawn、置き方 AppearAroundTarget）")]
    [SerializeField] AttackData[] _shapes;
    [Tooltip("【仮】出す枚数（第1形態 2・第2形態 4）")]
    [SerializeField, Min(1)] int _count = 2;
    [Tooltip("【仮】となりの足場から、横にこれだけ離れていても乗れると見なす（マス）")]
    [SerializeField, Min(0f)] float _reachSide = 1.5f;

    const int SquareIndex = 0, SmallIndex = 3;

    public override bool CanRun(AttackRunner owner, AttackData data) => Shape(SquareIndex) != null;

    AttackData Shape(int i) => _shapes != null && i >= 0 && i < _shapes.Length ? _shapes[i] : null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 target = owner.TargetPoint ?? owner.OriginPoint;
        Bounds boss = BrowserDevFx.OwnerBounds(owner);
        var objects = new List<FallingObject>();
        var taken = new List<(Vector2 c, Vector2 size)>();
        AttackData first = Random.value < 0.5f || Shape(SmallIndex) == null ? Shape(SquareIndex) : Shape(SmallIndex);
        float appearAt = Time.time + first.TelegraphTime;

        // 1つ目：相手の体に重ねる（四角か小さい四角）
        Vector2 c0 = FirstPosition(owner, first, arena, target, boss);
        taken.Add((c0, first.ObjectSize));
        objects.Add(FallingObject.SpawnInPlace(owner, first, c0, appearAt, 0));

        // 2つ目から：形を4種類から引いて、離れた空中に
        for (int i = 1; i < _count; i++)
        {
            AttackData shape = null;
            for (int tries = 0; tries < 8 && shape == null; tries++) shape = Shape(Random.Range(0, _shapes.Length));
            if (shape == null) shape = first;
            Vector2 c = RandomPosition(owner, shape, arena, target, boss, taken);
            taken.Add((c, shape.ObjectSize));
            objects.Add(FallingObject.SpawnInPlace(owner, shape, c, appearAt, i));
        }
        owner.NotifyReleased(data);

        // 全部が着地する（消える）まで待つ（今のポップアップ出現と同じ）
        float timeout = Time.time + 30f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }

    Vector2 FirstPosition(AttackRunner owner, AttackData shape, Rect arena, Vector2 target, Bounds boss)
    {
        Vector2 size = shape.ObjectSize, half = size * 0.5f;
        Vector2 p = target;
        p.x = Mathf.Clamp(p.x, arena.xMin + half.x, arena.xMax - half.x);
        owner.TryFindGround(target.x, target.y, out float groundY);
        if (p.y - half.y < groundY) p.y = groundY + half.y;
        p.y = Mathf.Min(p.y, arena.yMax - half.y);
        if (Overlaps(p, size, boss, 0f))
        {
            float dir = p.x >= boss.center.x ? 1f : -1f;
            float need = dir > 0f ? boss.max.x - (p.x - half.x) : (p.x + half.x) - boss.min.x;
            float maxShift = half.x + 0.3f;
            p.x = Mathf.Clamp(p.x + dir * Mathf.Min(need + 0.05f, maxShift), arena.xMin + half.x, arena.xMax - half.x);
        }
        return p;
    }

    Vector2 RandomPosition(AttackRunner owner, AttackData shape, Rect arena, Vector2 target, Bounds boss, List<(Vector2 c, Vector2 size)> taken)
    {
        Vector2 size = shape.ObjectSize, half = size * 0.5f;
        float topMin = arena.yMin + shape.AppearTopRange.x;
        float topMax = Mathf.Min(arena.yMin + shape.AppearTopRange.y, arena.yMax);
        Vector2? best = null;
        float bestOverlap = float.PositiveInfinity;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            float x = Random.Range(arena.xMin + half.x, arena.xMax - half.x);
            float top = Random.Range(topMin, Mathf.Max(topMin, topMax));
            var c = new Vector2(x, top - half.y);
            if (c.y - half.y < arena.yMin + 0.3f) continue;
            if (Vector2.Distance(c, target) < shape.MinDistanceFromTarget) continue;
            if (Overlaps(c, size, boss, 0.2f)) continue;
            if (Physics2D.OverlapBox(c, size * 0.95f, 0f, owner.SolidMask | owner.GroundMask) != null) continue;
            if (!CanReachTop(owner, c, size, shape.ReachHeight, arena)) continue;
            float overlap = 0f;
            foreach (var t in taken) overlap += OverlapArea(c, size, t.c, t.size);
            if (overlap <= 0f) return c;
            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                best = c;
            }
        }
        if (best is Vector2 found) return found;
        float side = target.x <= arena.center.x ? 1f : -1f;
        float fx = Mathf.Clamp(target.x + side * (shape.MinDistanceFromTarget + half.x), arena.xMin + half.x, arena.xMax - half.x);
        return new Vector2(fx, Mathf.Max(topMin - half.y, arena.yMin + half.y)); // 床にめりこませない
    }

    /// <summary>近くの床・足場から、ジャンプで上の端に乗れるか（真下と左右 reachSide の床の、いちばん高い所から）</summary>
    bool CanReachTop(AttackRunner owner, Vector2 c, Vector2 size, float reach, Rect arena)
    {
        Vector2 half = size * 0.5f;
        float top = c.y + half.y, bottom = c.y - half.y;
        float highest = arena.yMin;
        for (float x = c.x - half.x - _reachSide; x <= c.x + half.x + _reachSide + 0.01f; x += 0.5f)
        {
            if (x < arena.xMin || x > arena.xMax) continue;
            bool under = x >= c.x - half.x && x <= c.x + half.x;
            if (owner.TryFindGround(x, under ? bottom - 0.05f : top - 0.1f, out float y)) highest = Mathf.Max(highest, y);
        }
        return top - highest <= reach;
    }

    static bool Overlaps(Vector2 c, Vector2 size, Bounds b, float margin)
    {
        Vector2 half = size * 0.5f;
        return c.x + half.x > b.min.x - margin && c.x - half.x < b.max.x + margin &&
               c.y + half.y > b.min.y - margin && c.y - half.y < b.max.y + margin;
    }

    static float OverlapArea(Vector2 a, Vector2 sa, Vector2 b, Vector2 sb)
    {
        float w = Mathf.Min(a.x + sa.x * 0.5f, b.x + sb.x * 0.5f) - Mathf.Max(a.x - sa.x * 0.5f, b.x - sb.x * 0.5f);
        float h = Mathf.Min(a.y + sa.y * 0.5f, b.y + sb.y * 0.5f) - Mathf.Max(a.y - sa.y * 0.5f, b.y - sb.y * 0.5f);
        return w > 0f && h > 0f ? w * h : 0f;
    }
}
