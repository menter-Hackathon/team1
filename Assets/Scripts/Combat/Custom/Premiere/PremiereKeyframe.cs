using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キーフレーム（Premiere Pro のボスの技。2026-11-19。docs/app-feedback-v2-design.md 5-1）。エフェクトアイコンの雨と入れ替え。数値は【仮】。
/// キーフレーム（◆）を打つと、クリップが ◆ から ◆ へ動く。
/// 流れ：予告（1.0秒）◆ を順に空中に打ち、◆ と ◆ の間を赤い点線（動きの道すじ）でつなぐ。道すじは動かない。最後の 0.3秒は点滅 →
/// 本体：ボスの体からクリップ（紫の四角）が出て、道すじの上を ◆1 → ◆2 → ◆3 と動く（◆ の手前でゆっくり、間で速く。平均で毎秒 12マス）→
/// 終わり：最後の ◆ でクリップがはじけて消える。◆ はクリップが通るたびに消える。
/// ◆1＝ボスの体の下、◆2＝技が始まった瞬間のプレイヤーの体の位置、◆3＝◆2 からボスと反対の側へ 6マス・床から 3マス。
/// 第2形態：◆4＝◆3 の真下の床、◆5＝◆2 の所へ戻る。クリップ2つ（0.35秒あけて、同じ道すじ）。◆ を打つ間隔だけ 0.06秒（予告は短くしない）
/// 出したあとは PremiereKeyframeEffect が自分で進む（技はそれが終わるまで待つ）
/// </summary>
public class PremiereKeyframe : CustomAttack
{
    [Header("キーフレーム（5-1）")]
    [Tooltip("◆ の数（第1形態 3、第2形態 5）")]
    [SerializeField, Range(3, 5)] int _points = 3;
    [Tooltip("クリップの数（第1形態 1、第2形態 2）")]
    [SerializeField, Min(1)] int _clipCount = 1;
    [Tooltip("クリップとクリップの間（秒）")]
    [SerializeField, Min(0f)] float _clipGap = 0.35f;
    [Tooltip("◆3：◆2 からボスと反対の側へ、この距離（マス）")]
    [SerializeField, Min(0f)] float _awayDistance = 6f;
    [Tooltip("◆3 の高さ（床から、マス）")]
    [SerializeField, Min(0f)] float _highPoint = 3f;

    [Header("時間・速さ")]
    [Tooltip("予告（秒）。短くしない")]
    [SerializeField, Min(0.8f)] float _telegraphTime = 1f;
    [Tooltip("◆ を打つ間隔（秒。第1形態 0.1、第2形態 0.06）")]
    [SerializeField, Min(0f)] float _placeInterval = 0.1f;
    [Tooltip("クリップの平均の速さ（マス/秒）")]
    [SerializeField, Min(1f)] float _averageSpeed = 12f;
    [Tooltip("クリップの判定（円の直径、マス）")]
    [SerializeField, Min(0.1f)] float _hitDiameter = 1f;

    public int ClipCount => Mathf.Max(1, _clipCount);
    public float ClipGap => _clipGap;
    public float TelegraphTime => _telegraphTime;
    public float PlaceInterval => _placeInterval;
    public float AverageSpeed => _averageSpeed;
    public float HitDiameter => _hitDiameter;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        List<Vector2> path = PickPath(owner);
        PremiereKeyframeEffect effect = PremiereKeyframeEffect.Spawn(owner, data, this, path);
        owner.NotifyReleased(data);
        while (effect != null && !effect.Finished) yield return null;
    }

    List<Vector2> PickPath(AttackRunner owner)
    {
        Rect arena = BattleArena.GetBounds();
        float minX = arena.xMin + 0.8f, maxX = arena.xMax - 0.8f;
        float minY = arena.yMin + 0.5f, maxY = arena.yMax - PremiereArt.HudInset - 0.6f;
        Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minY, Mathf.Max(minY, maxY)));

        Vector2 boss = owner.OriginPoint;
        if (owner.TryGetComponent(out BossController bc))
        {
            Bounds b = bc.BodyBounds;
            boss = new Vector2(b.center.x, b.min.y - 0.4f);
        }
        Vector2 p1 = Clamp(boss);
        Vector2 p2 = Clamp(owner.TargetPoint ?? new Vector2(arena.center.x, arena.yMin + 0.5f));
        float side = Mathf.Sign(p2.x - p1.x);
        if (Mathf.Abs(p2.x - p1.x) < 0.01f) side = p2.x < arena.center.x ? 1f : -1f;
        Vector2 p3 = Clamp(new Vector2(p2.x + side * _awayDistance, arena.yMin + _highPoint));
        // 部屋の端で詰まって ◆2 と重なるときは、反対の側へ
        if (Mathf.Abs(p3.x - p2.x) < 2f) p3 = Clamp(new Vector2(p2.x - side * _awayDistance, arena.yMin + _highPoint));

        var path = new List<Vector2> { p1, p2, p3 };
        if (_points >= 4) path.Add(Clamp(new Vector2(p3.x, arena.yMin + 0.5f)));
        if (_points >= 5) path.Add(p2);
        return path;
    }
}
