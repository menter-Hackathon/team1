using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】スターツール（Illustrator のボスの技2。2026-11-19。docs/app-kits-2-design.md 3-4）。
/// 3か所（プレイヤーの位置・左右の足場のどちらかの上・床のランダムな所）に、順に（0.4秒おき）：
/// 中心の点から赤い輪かくが広がる（0.8秒。だ円・六角形・星。半径 2.2マス）→ オレンジで塗られる（判定 0.15秒）。
/// 第2形態：5か所。星の 5つの先から、小さい三角の弾が外へ飛ぶ（毎秒 6マス。弾の数値は攻撃データの弾の欄）。
/// 判定は塗った形と同じ（PathStrokeRegion）。見えている形の外なら当たらない
/// </summary>
[CreateAssetMenu(fileName = "Illustrator_Shape", menuName = "Team1/Kits/Illustrator Shape")]
public class IllustratorShapeAttack : CustomAttack
{
    [Tooltip("か所の数（第1形態 3・第2形態 5）")]
    [SerializeField, Min(1)] int _count = 3;
    [SerializeField, Min(0f)] float _interval = 0.4f;
    [Tooltip("形の半径（マス）")]
    [SerializeField, Min(0.5f)] float _radius = 2.2f;
    [Tooltip("赤い輪かくが広がる予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(0.05f)] float _active = 0.15f;
    [Tooltip("オン：星の先から弾が飛ぶ")]
    [SerializeField] bool _tipShots;

    static readonly int[] Shapes1 = { 0, 1, 2 };       // だ円・六角形・星
    static readonly int[] Shapes2 = { 2, 1, 2, 0, 2 }; // 第2形態は星を多く

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        int n = Mathf.Max(1, _count);
        int platformSide = Random.value < 0.5f ? -1 : 1;
        PathStrokeRegion last = null;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            Vector2 surface = Spot(owner, arena, i, ref platformSide);
            Vector2 c = surface + new Vector2(0f, _radius * 0.8f);
            int shape = (_tipShots ? Shapes2 : Shapes1)[i % (_tipShots ? Shapes2.Length : Shapes1.Length)];
            last = SpawnShape(owner, data, c, shape);
        }
        if (last != null) yield return new WaitForSeconds(last.TotalTime);
    }

    /// <summary>i 番目の場所（下の面の点）：0＝プレイヤー、1＝足場の上、2＝床のランダム、3＝プレイヤー、4＝反対の足場</summary>
    Vector2 Spot(AttackRunner owner, Rect arena, int i, ref int side)
    {
        float floor = arena.yMin;
        switch (i % 5)
        {
            case 0:
            case 3:
                {
                    Vector2 p = owner.TargetPoint ?? arena.center;
                    float y = owner.TryFindGround(p.x, p.y, out float g) ? g : floor;
                    return new Vector2(p.x, y);
                }
            case 1:
            case 4:
                {
                    if (i % 5 == 4) side = -side;
                    float x = arena.center.x + side * 3.5f;
                    float y = owner.TryFindGround(x, floor + 5f, out float g) ? g : floor + 3f;
                    return new Vector2(x, y);
                }
            default:
                return new Vector2(Random.Range(arena.xMin + 2f, arena.xMax - 2f), floor);
        }
    }

    PathStrokeRegion SpawnShape(AttackRunner owner, AttackData data, Vector2 c, int shape)
    {
        List<Vector2> poly = shape == 0 ? PathStrokeRegion.Ellipse(c, new Vector2(_radius * 2f, _radius * 1.6f))
            : shape == 1 ? PathStrokeRegion.RegularPolygon(c, _radius, 6, 90f)
            : PathStrokeRegion.Star(c, _radius, _radius * 0.45f, 5, 90f);
        var style = new PathStrokeRegion.Style
        {
            Grow = _telegraph,
            Telegraph = _telegraph,
            Active = _active,
            Linger = 0.3f,
            Fill = new Color(IllustratorArt.Orange.r, IllustratorArt.Orange.g, IllustratorArt.Orange.b, 0.9f),
            OutlinePx = 1.5f,
            Order = 40,
        };
        PathStrokeRegion r = PathStrokeRegion.Spawn(owner, data, poly, c, style);
        if (r == null) return null;
        RuntimeSprites.Show(r.Root, IllustratorArt.Anchor, Vector2.zero, 45, 1f, "Center"); // 中心の点
        if (shape == 2 && _tipShots)
        {
            r.StepChanged += (reg, step) =>
            {
                if (step != PathStrokeRegion.Step.Active || owner == null) return;
                for (int k = 0; k < 5; k++)
                {
                    float a = (90f + k * 72f) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Projectile p = Projectile.Spawn(owner, data, c + d * _radius, d, k);
                    if (p != null) KitDress.Swap(p.gameObject, IllustratorArt.Triangle);
                }
            };
        }
        return r;
    }
}
