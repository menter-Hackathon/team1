using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 再生ヘッドレーザー（Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-2）。数値は【仮】。
/// 流れ：画面の上にタイムラインの帯が出る → マーカー（炎・雷のアイコン）を置き、起きる範囲をうすく見せる →
/// 青い再生ヘッドが左端から右端へ走る → 触れたマーカーが白く光り、0.25秒後に爆発（円）・落雷（柱）→ 端に着いたら帯ごと消える。
/// 第2形態は往復（帰りも、同じマーカーがもう一度発動する）。
/// 再生ヘッドの線そのものには当たり判定がない（0章の表）。当たるのは爆発・落雷だけ。
/// ここではマーカーの場所を決めるだけで、出したあとは PremierePlayheadEffect が自分で進む（技はそれが終わるまで待つ）
/// </summary>
public class PremierePlayhead : CustomAttack
{
    [Header("マーカー（3-2）")]
    [Tooltip("マーカーの数（第1形態 3、第2形態 5）")]
    [SerializeField, Min(1)] int _markerCount = 3;
    [Tooltip("そのうち炎の数（残りは雷。第1形態 2、第2形態 3）")]
    [SerializeField, Min(0)] int _fireCount = 2;
    [Tooltip("1つ目は、プレイヤーの x からこの距離以内に置く（マス）")]
    [SerializeField, Min(0f)] float _firstMarkerRange = 1f;
    [Tooltip("マーカーどうしを、これ以上離す（マス）")]
    [SerializeField, Min(0f)] float _minSpacing = 3f;
    [Tooltip("範囲を全部足した幅の上限（マス）。すき間が必ず残るように。超える分のマーカーは置かない")]
    [SerializeField, Min(0f)] float _maxCoverage = 12f;
    [Tooltip("雷のマーカーの高さ（部屋の床の上、マス）")]
    [SerializeField] float _thunderHeight = 1f;
    [Tooltip("炎のマーカーの高さ（真下の床・足場の上、マス）")]
    [SerializeField] float _fireHeight = 1.5f;
    [Tooltip("部屋の左右の端から、マーカーを置かない幅（マス）")]
    [SerializeField, Min(0f)] float _sideMargin = 1f;

    [Header("時間・再生ヘッド")]
    [Tooltip("タイムラインの帯が出るまで（秒）")]
    [SerializeField, Min(0f)] float _stripTime = 0.3f;
    [Tooltip("マーカーを置き終えるまで（秒）。この間に1つずつ置く")]
    [SerializeField, Min(0f)] float _placeTime = 0.8f;
    [Tooltip("再生ヘッドの速さ（マス/秒）")]
    [SerializeField, Min(0.1f)] float _speed = 6f;
    [Tooltip("オン：左 → 右、右 → 左の往復（第2形態）")]
    [SerializeField] bool _roundTrip;
    [Tooltip("マーカーに触れてから発動するまで（秒）。この間マーカーが白く光る")]
    [SerializeField, Min(0f)] float _triggerDelay = 0.25f;
    [Tooltip("【仮】タイムラインの帯の上の端：画面の上の端からの距離（マス）。画面の上の表示（HUD）に隠れないように下げてある")]
    [SerializeField, Min(0f)] float _topInset = PremiereArt.HudInset;

    [Header("発動（攻撃データ。Area・予告 0）")]
    [Tooltip("炎：爆発。円、直径 3（判定 0.3秒）。出す場所 TargetPosition・床に置かない")]
    [SerializeField] AttackData _explosion;
    [Tooltip("雷：落雷。幅 1.5、床から天井まで（判定 0.3秒）。出す場所 TargetPosition・床の上に置く")]
    [SerializeField] AttackData _bolt;

    public float StripTime => _stripTime;
    public float PlaceTime => _placeTime;
    public float Speed => _speed;
    public bool RoundTrip => _roundTrip;
    public float TriggerDelay => _triggerDelay;
    public float TopInset => _topInset;
    public AttackData Explosion => _explosion;
    public AttackData Bolt => _bolt;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        List<PremierePlayheadEffect.Marker> markers = PickMarkers(owner, arena);
        PremierePlayheadEffect effect = PremierePlayheadEffect.Spawn(owner, this, markers);
        owner.NotifyReleased(data);
        // 再生ヘッドが走り終えるまで、ボスは次の技を始めない
        while (effect != null && !effect.Finished) yield return null;
    }

    /// <summary>
    /// マーカーの場所を決める。1つ目はプレイヤーの近く、残りはたがいに離す（離せる場所が無ければ、いちばん離れる所）。
    /// 範囲の幅の合計が上限を超えるなら、超える分は置かない
    /// </summary>
    List<PremierePlayheadEffect.Marker> PickMarkers(AttackRunner owner, Rect arena)
    {
        // 前から順に置くと、最後のほうで離せる場所が無くなることがある。全部離せる並びが出るまで、何回か置き直す
        List<PremierePlayheadEffect.Marker> result = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            result = TryPickMarkers(owner, arena, out bool spaced);
            if (spaced) break;
        }
        return result;
    }

    /// <summary>マーカーを1回置いてみる。spaced：全部のマーカーが、たがいに決まった距離以上離れたか</summary>
    List<PremierePlayheadEffect.Marker> TryPickMarkers(AttackRunner owner, Rect arena, out bool spaced)
    {
        spaced = true;
        // 炎と雷を混ぜた順番
        var kinds = new List<PremiereIcon>();
        int n = Mathf.Max(1, _markerCount);
        for (int i = 0; i < n; i++) kinds.Add(i < _fireCount ? PremiereIcon.Fire : PremiereIcon.Thunder);
        for (int i = kinds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PremiereIcon t = kinds[i];
            kinds[i] = kinds[j];
            kinds[j] = t;
        }

        float fireWidth = _explosion != null ? _explosion.AreaSize.x : 3f;
        float boltWidth = _bolt != null ? _bolt.AreaSize.x : 1.5f;
        float minX = arena.xMin + _sideMargin, maxX = arena.xMax - _sideMargin;
        if (maxX < minX) { minX = maxX = arena.center.x; }
        float px = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, minX, maxX);

        var result = new List<PremierePlayheadEffect.Marker>();
        var xs = new List<float>();
        float coverage = 0f;
        foreach (PremiereIcon kind in kinds)
        {
            float width = kind == PremiereIcon.Fire ? fireWidth : boltWidth;
            if (coverage + width > _maxCoverage + 0.001f) continue;

            float x;
            if (xs.Count == 0)
            {
                x = Mathf.Clamp(px + Random.Range(-_firstMarkerRange, _firstMarkerRange), minX, maxX);
            }
            else
            {
                x = Random.Range(minX, maxX);
                float bestGap = -1f;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    float cand = Random.Range(minX, maxX);
                    float gap = float.PositiveInfinity;
                    foreach (float o in xs) gap = Mathf.Min(gap, Mathf.Abs(cand - o));
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        x = cand;
                    }
                    if (gap >= _minSpacing) break;
                }
                if (bestGap < _minSpacing) spaced = false;
            }
            xs.Add(x);
            coverage += width;

            float y;
            if (kind == PremiereIcon.Fire)
            {
                // 真下の床・足場の上（上から探すので、足場があれば足場の上）
                owner.TryFindGround(x, arena.yMax - 0.05f, out float ground);
                y = ground + _fireHeight;
            }
            else
            {
                y = arena.yMin + _thunderHeight;
            }
            result.Add(new PremierePlayheadEffect.Marker { Kind = kind, Position = new Vector2(x, y) });
        }
        return result;
    }
}
