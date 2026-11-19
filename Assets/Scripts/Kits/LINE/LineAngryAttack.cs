using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】LINE のボスの技「怒りのメッセージ」（2026-11-19。docs/app-kits-1-design.md 4章 P2）。
/// 赤い吹き出し（LineMessage）が 3つ（プレイヤーの足元、左の足場の上、右の足場の上）に出る → 1.5秒 赤く点滅（爆発の範囲に赤い輪）→ 直径 4 の爆発。
/// 避け方：離れる。または近接で叩いて消す（返信。1つ消せば、そこは安全）。
/// 同じ所に重なるとき（プレイヤーが足場の上にいる）は、床の上の別の所に出す
/// </summary>
[CreateAssetMenu(fileName = "Line_Angry", menuName = "Team1/Kits/LINE Angry")]
public class LineAngryAttack : CustomAttack
{
    [Tooltip("赤く点滅する秒数（予告）")]
    [SerializeField, Min(0.8f)] float _blink = 1.5f;
    [Tooltip("爆発の直径（マス）")]
    [SerializeField, Min(1f)] float _diameter = 4f;
    [Tooltip("出す間隔（秒。ポン・ポン・ポン）")]
    [SerializeField, Min(0f)] float _stagger = 0.12f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        var points = new List<Vector2>();
        if (TalkKitUtil.PlayerBody(owner.Target, out Bounds pb))
            points.Add(new Vector2(pb.center.x, TalkKitUtil.StandY(pb.center.x, pb.min.y + 0.3f)));
        List<Bounds> plats = TalkKitUtil.Platforms(arena);
        if (plats.Count > 0) Add(points, new Vector2(plats[0].center.x, plats[0].max.y));
        if (plats.Count > 1) Add(points, new Vector2(plats[plats.Count - 1].center.x, plats[plats.Count - 1].max.y));
        // 足りなければ、床の上（プレイヤーから 5〜7マス）
        float baseX = points.Count > 0 ? points[0].x : arena.center.x;
        float[] offsets = { 6f, -6f, 8f, -8f, 4.5f, -4.5f };
        foreach (float o in offsets)
        {
            if (points.Count >= 3) break;
            float x = baseX + o;
            if (x < arena.xMin + 1.2f || x > arena.xMax - 1.2f) continue;
            Add(points, new Vector2(x, TalkKitUtil.FloorY(x, arena.yMin + 2.4f)));
        }

        var list = new List<LineMessage>();
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0 && _stagger > 0f) yield return new WaitForSeconds(_stagger);
            LineMessage m = LineMessage.Spawn(owner, data, points[i], 0f, _blink, _diameter);
            if (m != null) list.Add(m);
        }
        owner.NotifyReleased(data);
        float timeout = Time.time + _blink + 1.5f;
        while (Time.time < timeout && list.Exists(m => m != null && !m.IsDone)) yield return null;
        yield return new WaitForSeconds(0.3f); // 爆発が終わるまで
    }

    static void Add(List<Vector2> points, Vector2 p)
    {
        foreach (var q in points) if ((q - p).sqrMagnitude < 2.5f * 2.5f) return;
        points.Add(p);
    }
}
