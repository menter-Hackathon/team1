using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ペンツール（Figma の能力。2026-11-19。docs/app-kits-2-design.md 2-4）。
/// 近い敵から順に最大 3体にアンカーポイント（白い四角）を打ち、プレイヤーから順に線で結ぶ。線の上の敵に当たる（同じ敵には 1回）。
/// ボスだけのときはボスに 1つ（ゲージは攻撃データの共通の欄。14）。雑魚は1回で倒れる（ダメージ 3）。敵がいなければ正面 6マスにまっすぐ。
/// 線は PathStroke（予告なし）。ステージの決まりに頼らない（どのステージでも動く）
/// </summary>
[CreateAssetMenu(fileName = "Figma_PenTool", menuName = "Team1/Kits/Figma Pen Tool")]
public class FigmaPenToolAttack : CustomAttack
{
    [Tooltip("結ぶ敵の数の上限")]
    [SerializeField, Min(1)] int _maxTargets = 3;
    [Tooltip("狙える距離（マス）")]
    [SerializeField, Min(1f)] float _range = 12f;
    [Tooltip("敵がいないとき、正面にのばす長さ（マス）")]
    [SerializeField, Min(1f)] float _straight = 6f;
    [Tooltip("線の太さ（マス）")]
    [SerializeField, Min(0.1f)] float _width = 0.5f;
    [Tooltip("線を引く秒数")]
    [SerializeField, Min(0f)] float _drawTime = 0.12f;
    [Tooltip("引き終えてから当たる秒数")]
    [SerializeField, Min(0.02f)] float _active = 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 start = owner.OriginPoint + new Vector2(owner.Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        var pts = new List<Vector2> { start };
        foreach (Hurtbox h in LockOnStrike.FindTargets(owner))
        {
            if (pts.Count > _maxTargets) break;
            Vector2 c = h.Collider.bounds.center;
            if (Vector2.Distance(start, c) > _range) continue;
            pts.Add(c);
        }
        if (pts.Count == 1) pts.Add(start + new Vector2(owner.Facing * _straight, 0f));

        var style = new PathStroke.Style
        {
            Telegraph = 0f,
            Width = _width,
            DrawTime = _drawTime,
            Active = _active,
            Linger = 0.3f,
            Color = FigmaArt.Blue,
            Core = new Color(0.75f, 0.9f, 1f, 1f),
            Order = 30,
        };
        PathStroke s = PathStroke.Spawn(owner, data, pts, style);
        if (s == null) yield break;

        // アンカーポイント（白い四角）とハンドル（短い線と丸）
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 p = pts[i];
            RuntimeSprites.Show(s.transform, FigmaArt.Anchor, p, 33, 1f, "Anchor");
            Vector2 a = i > 0 ? pts[i - 1] : p;
            Vector2 b = i < pts.Count - 1 ? pts[i + 1] : p;
            Vector2 tan = (b - a).sqrMagnitude > 0.001f ? (b - a).normalized : Vector2.right;
            if (i == 0 || i == pts.Count - 1) continue;
            Vector2 h0 = p - tan * 0.7f, h1 = p + tan * 0.7f;
            PathStroke.Bar(s.transform, h0, h1, KitDress.Px, FigmaArt.Blue, 32, "HandleLine");
            RuntimeSprites.Show(s.transform, FigmaArt.HandleDot, h0, 33, 1f, "HandleDot");
            RuntimeSprites.Show(s.transform, FigmaArt.HandleDot, h1, 33, 1f, "HandleDot");
        }
        yield return new WaitForSeconds(_drawTime);
    }
}
