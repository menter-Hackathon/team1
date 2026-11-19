using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 再生ヘッド（Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-2）。数値は【仮】。
/// 2026-11-19（docs/app-feedback-v2-design.md 5-2）：マーカーで起きる物を、実際の機能「ディップ トゥ ホワイト」（たての白い柱）にした。
/// 炎・雷のアイコン、円の爆発は、もう出さない。
/// 流れ：画面の上にタイムラインの帯が出る → ルーラーに赤いマーカー（下向きの五角形）を打ち、真下へ赤い点線と、柱になる幅のうすい赤の帯 →
/// 青い再生ヘッドが左端から右端へ走る → マーカーに着くと白く光り、少しあとに白い柱（幅 1.5、床から天井、0.3秒）→ 端に着いたら帯ごと消える。
/// 第2形態は往復（帰りも、同じマーカーがもう一度起きる）。マーカーは1つ足す（円が柱になって、やさしくなった分）。
/// 再生ヘッドの線そのものには当たり判定がない。ここではマーカーの場所を決めるだけで、出したあとは PremierePlayheadEffect が自分で進む
/// </summary>
public class PremierePlayhead : CustomAttack
{
    [Header("マーカー（5-2）")]
    [Tooltip("マーカーの数（第1形態 3、第2形態 5）")]
    [SerializeField, Min(1)] int _markerCount = 3;
    [Tooltip("2026-11-19：足すマーカーの数（第2形態 1。ビルダーが毎回入れる）")]
    [SerializeField, Min(0)] int _extraMarkers;
    [Tooltip("1つ目は、プレイヤーの x からこの距離以内に置く（マス）")]
    [SerializeField, Min(0f)] float _firstMarkerRange = 1f;
    [Tooltip("マーカーどうしを、これ以上離す（マス）")]
    [SerializeField, Min(0f)] float _minSpacing = 3f;
    [Tooltip("柱の幅を全部足した幅の上限（マス）。すき間が必ず残るように。超える分のマーカーは置かない")]
    [SerializeField, Min(0f)] float _maxCoverage = 12f;
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
    [Tooltip("マーカーに着いてから柱が出るまで（秒）。この間マーカーが白く光る")]
    [SerializeField, Min(0f)] float _triggerDelay = 0.25f;
    [Tooltip("【仮】タイムラインの帯の上の端：画面の上の端からの距離（マス）。画面の上の表示（HUD）に隠れないように下げてある")]
    [SerializeField, Min(0f)] float _topInset = PremiereArt.HudInset;

    [Header("起きる物（攻撃データ。Area・予告 0）")]
    [Tooltip("2026-11-19：ディップ トゥ ホワイトの柱。幅 1.5、床から天井まで（判定 0.3秒）。出す場所 TargetPosition・床の上に置く")]
    [SerializeField] AttackData _dip;
    [Tooltip("前の落雷（柱）。_dip が空のときだけ、代わりに使う（判定の形が同じなので）")]
    [SerializeField] AttackData _bolt;

    public float StripTime => _stripTime;
    public float PlaceTime => _placeTime;
    public float Speed => _speed;
    public bool RoundTrip => _roundTrip;
    public float TriggerDelay => _triggerDelay;
    public float TopInset => _topInset;
    /// <summary>柱の攻撃データ（ディップ。無ければ前の落雷）</summary>
    public AttackData Dip => _dip != null ? _dip : _bolt;
    public float DipWidth => Dip != null ? Dip.AreaSize.x : 1.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        List<Vector2> markers = PickMarkers(owner, arena);
        PremierePlayheadEffect effect = PremierePlayheadEffect.Spawn(owner, this, markers);
        owner.NotifyReleased(data);
        // 再生ヘッドが走り終えるまで、ボスは次の技を始めない
        while (effect != null && !effect.Finished) yield return null;
    }

    /// <summary>
    /// マーカーの場所を決める。1つ目はプレイヤーの近く、残りはたがいに離す（離せる場所が無ければ、いちばん離れる所）。
    /// 柱の幅の合計が上限を超えるなら、超える分は置かない
    /// </summary>
    List<Vector2> PickMarkers(AttackRunner owner, Rect arena)
    {
        List<Vector2> result = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            result = TryPickMarkers(owner, arena, out bool spaced);
            if (spaced) break;
        }
        return result;
    }

    List<Vector2> TryPickMarkers(AttackRunner owner, Rect arena, out bool spaced)
    {
        spaced = true;
        int n = Mathf.Max(1, _markerCount + _extraMarkers);
        float width = DipWidth;
        float minX = arena.xMin + _sideMargin, maxX = arena.xMax - _sideMargin;
        if (maxX < minX) { minX = maxX = arena.center.x; }
        float px = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, minX, maxX);

        var result = new List<Vector2>();
        float coverage = 0f;
        for (int k = 0; k < n; k++)
        {
            if (coverage + width > _maxCoverage + 0.001f) break;
            float x;
            if (result.Count == 0)
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
                    foreach (Vector2 o in result) gap = Mathf.Min(gap, Mathf.Abs(cand - o.x));
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        x = cand;
                    }
                    if (gap >= _minSpacing) break;
                }
                if (bestGap < _minSpacing) spaced = false;
            }
            coverage += width;
            result.Add(new Vector2(x, arena.yMin + 1f));
        }
        return result;
    }
}
