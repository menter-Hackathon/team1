using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】アートボード（Illustrator の第2形態の技。2026-11-19。docs/app-kits-2-design.md 3-4）。場に残る技。
/// 部屋のまん中にアートボードの枠（幅 12マス。x＝-6〜6。左上に Artboard 1）。外側の左右に赤いしま（1.2秒）
/// → 外側が灰色になり、4秒残る（立っている間は当たる）→ 戻る。枠の中へ入れば当たらない（部屋の端から 4マス＝0.6秒）。
/// 技そのものは予告のあとに終わり、ボスは次の技へ進む（灰色の所は残る）
/// </summary>
[CreateAssetMenu(fileName = "Illustrator_Artboard", menuName = "Team1/Kits/Illustrator Artboard")]
public class IllustratorArtboardAttack : CustomAttack
{
    [Tooltip("アートボードの幅の半分（マス。部屋のまん中から）")]
    [SerializeField, Min(1f)] float _halfWidth = 6f;
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [Tooltip("灰色が残る秒数（当たり続ける）")]
    [SerializeField, Min(0.5f)] float _duration = 4f;
    [Tooltip("当たり直す間隔（秒）")]
    [SerializeField, Min(0.1f)] float _tick = 0.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float cx = arena.center.x, floor = arena.yMin, top = arena.yMax;
        float inL = cx - _halfWidth, inR = cx + _halfWidth;
        var style = new PathStrokeRegion.Style
        {
            Telegraph = _telegraph,
            Active = _duration,
            TickInterval = _tick,
            Linger = 0.3f,
            Fill = new Color(IllustratorArt.Gray.r, IllustratorArt.Gray.g, IllustratorArt.Gray.b, 0.8f),
            TelegraphFillAlpha = 0.12f,
            OutlinePx = 2f,
            Lingers = true,
            Order = 34,
        };
        PathStrokeRegion left = null, right = null;
        if (inL - arena.xMin > 0.2f)
            left = PathStrokeRegion.Spawn(owner, data, PathStrokeRegion.RectShape(new Vector2((arena.xMin + inL) * 0.5f, (floor + top) * 0.5f), new Vector2(inL - arena.xMin, top - floor)), new Vector2((arena.xMin + inL) * 0.5f, (floor + top) * 0.5f), style);
        if (arena.xMax - inR > 0.2f)
            right = PathStrokeRegion.Spawn(owner, data, PathStrokeRegion.RectShape(new Vector2((inR + arena.xMax) * 0.5f, (floor + top) * 0.5f), new Vector2(arena.xMax - inR, top - floor)), new Vector2((inR + arena.xMax) * 0.5f, (floor + top) * 0.5f), style);
        Stripes(left);
        Stripes(right);

        // アートボードの枠と名前（技の間ずっと）
        float life = _telegraph + _duration + 0.3f;
        Transform frame = PathStroke.Decor(owner, new Vector2(cx, (floor + top) * 0.5f), life, "AiArtboard");
        var lc = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        float w = 1.5f * KitDress.Px;
        PathStroke.Bar(frame, new Vector2(inL, floor), new Vector2(inL, top - 0.4f), w, lc, 36, "L");
        PathStroke.Bar(frame, new Vector2(inR, floor), new Vector2(inR, top - 0.4f), w, lc, 36, "R");
        PathStroke.Bar(frame, new Vector2(inL, top - 0.4f), new Vector2(inR, top - 0.4f), w, lc, 36, "T");
        TextMesh name = WorldText.Create(frame, "Artboard 1", 0.34f, new Color(0.15f, 0.15f, 0.15f, 1f), 37, true, TextAnchor.LowerLeft, "Name");
        name.transform.position = new Vector3(inL, top - 0.35f, 0f);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.2f);
    }

    /// <summary>予告の赤いしま（ななめの線）。予告が終わると消える</summary>
    void Stripes(PathStrokeRegion r)
    {
        if (r == null) return;
        Rect b = r.LocalBounds;
        var root = new GameObject("Stripes").transform;
        root.SetParent(r.Root, false);
        Vector2 c = r.Center;
        Color red = new Color(KitDress.Red.r, KitDress.Red.g, KitDress.Red.b, 0.55f);
        for (float x = b.xMin - b.height; x < b.xMax; x += 1.2f)
        {
            // 範囲の中だけの、ななめの線（下の端から上の端へ）
            Vector2 p0 = new Vector2(x, b.yMin), p1 = new Vector2(x + b.height, b.yMax);
            ClipX(ref p0, ref p1, b.xMin, b.xMax);
            if ((p1 - p0).sqrMagnitude < 0.04f) continue;
            PathStroke.Bar(root, c + p0, c + p1, 3f * KitDress.Px, red, 33, "Stripe");
        }
        r.StepChanged += (reg, step) => { if (step == PathStrokeRegion.Step.Active && root != null) Object.Destroy(root.gameObject); };
    }

    static void ClipX(ref Vector2 a, ref Vector2 b, float xMin, float xMax)
    {
        Vector2 d = b - a;
        if (Mathf.Abs(d.x) < 0.0001f) return;
        if (a.x < xMin) a += d * ((xMin - a.x) / d.x);
        if (b.x > xMax) b -= d * ((b.x - xMax) / d.x);
    }
}
