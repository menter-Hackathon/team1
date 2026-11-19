using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Space の切り替え（Arc のボスの技1。2026-11-19。docs/app-kits-2-design.md 4-5）。
/// ふちに赤い矢印と帯の高さ（0.9秒）→ グラデーションの帯（幅 1.5マス）が毎秒 11マスで部屋を横切る。
/// 低い帯（床〜2マス）はジャンプで越える。高い帯（1.6マスより上、天井まで）は床にいれば当たらない（足場の上は当たる）。
/// 第1形態：低い帯を 2回（右から、左から）。第2形態：低・高・低（0.9秒おき。同じ向き）
/// </summary>
[CreateAssetMenu(fileName = "Arc_Space", menuName = "Team1/Kits/Arc Space")]
public class ArcSpaceAttack : CustomAttack
{
    [Tooltip("オン：第2形態（低・高・低）。オフ：低い帯を右から・左から")]
    [SerializeField] bool _phase2;
    [Tooltip("帯と帯の間（秒）")]
    [SerializeField, Min(0.3f)] float _interval = 1.1f;
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(1f)] float _speed = 11f;
    [Tooltip("帯の幅（マス）")]
    [SerializeField, Min(0.3f)] float _width = 1.5f;
    [Tooltip("低い帯の上の端（床から）・高い帯の下の端（床から）")]
    [SerializeField] float _lowTop = 2f;
    [SerializeField] float _highBottom = 1.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        // 第2形態は、ボスのいる側から（同じ向き）
        int fromBoss = owner.OriginPoint.x <= arena.center.x ? 1 : -1;
        int n = _phase2 ? 3 : 2;
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            bool low = !_phase2 || i != 1;
            int dir = _phase2 ? fromBoss : (i == 0 ? -1 : 1); // -1：右から左へ
            float delay = i * _interval;
            ArcBandEffect.Spawn(owner, data, arena, low, dir, delay, _telegraph, _speed, _width, _lowTop, _highBottom);
            last = delay;
        }
        // 最後の帯が出るまで待つ（横切る間は、ボスは次へ進んでよい）
        yield return new WaitForSeconds(last + _telegraph + 0.4f);
    }
}
