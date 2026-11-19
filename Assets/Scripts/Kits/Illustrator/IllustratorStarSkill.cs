using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】スターツール（Illustrator の能力。2026-11-19。docs/app-kits-2-design.md 3-4）。
/// 正面 2マスに星を描く（半径 2マス。ゲージは攻撃データの共通の欄。12）。星の 5つの先が小さい弾になって飛ぶ（1発 3）。
/// ボスに当たる弾は 2発まで：前を向いた 2つの先だけ「先の弾」（ゲージ 3）、ほかの 3つは「雑魚用の先の弾」（ゲージ 0）。
/// ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "Illustrator_Star", menuName = "Team1/Kits/Illustrator Star")]
public class IllustratorStarSkill : CustomAttack
{
    [Tooltip("体の前のずれ（マス）")]
    [SerializeField] float _forward = 2f;
    [Tooltip("星の半径（マス）")]
    [SerializeField, Min(0.3f)] float _radius = 2f;
    [SerializeField, Min(0.02f)] float _active = 0.12f;
    [Tooltip("先の弾（ゲージあり）。弾の欄の数値を使う")]
    [SerializeField] AttackData _tip;
    [Tooltip("先の弾（ゲージなし。雑魚用）。空なら上と同じ")]
    [SerializeField] AttackData _tipNoGauge;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        int facing = owner.Facing >= 0 ? 1 : -1;
        Vector2 c = owner.OriginPoint + new Vector2(facing * _forward, 0.7f);
        var style = new PathStrokeRegion.Style
        {
            Telegraph = 0f,
            TelegraphFillAlpha = 0f,
            Active = _active,
            Linger = 0.25f,
            Fill = new Color(IllustratorArt.Orange.r, IllustratorArt.Orange.g, IllustratorArt.Orange.b, 0.85f),
            OutlineWhileActive = true,
            OutlinePx = 1f,
            Order = 30,
        };
        PathStrokeRegion r = PathStrokeRegion.Spawn(owner, data, PathStrokeRegion.Star(c, _radius, _radius * 0.45f, 5, 90f), c, style);
        if (r != null) RuntimeSprites.Show(r.Root, IllustratorArt.Anchor, Vector2.zero, 33, 1f, "Center");

        // 星の先の弾（前を向いた 2つだけ、ボスのゲージあり）
        var tips = new List<(Vector2 dir, float score)>();
        for (int k = 0; k < 5; k++)
        {
            float a = (90f + k * 72f) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            tips.Add((d, d.x * facing));
        }
        tips.Sort((x, y) => y.score.CompareTo(x.score));
        for (int i = 0; i < tips.Count; i++)
        {
            AttackData td = i < 2 ? _tip : (_tipNoGauge != null ? _tipNoGauge : _tip);
            if (td == null) continue;
            Projectile p = Projectile.Spawn(owner, td, c + tips[i].dir * _radius, tips[i].dir, i);
            if (p != null) KitDress.Swap(p.gameObject, IllustratorArt.Triangle);
        }
        owner.NotifyReleased(data);
        yield return null;
    }
}
