using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】999+ warnings（Xcode のボスの第2形態の技。2026-11-19。docs/app-kits-2-design.md 3-2）。
/// 床いっぱいに影（1.0秒）。影の無いすき間が 2か所（幅 2.5マス）→ 黄色い三角が一度に降る（足場もすり抜けて床まで）。3回。
/// すき間は毎回変わる。毎回、1つはプレイヤーから 4.5マス以内。足場の上にも影（足場の上も当たる）。ボスの頭の上に 999+ のバッジ。
/// </summary>
[CreateAssetMenu(fileName = "Xcode_Warnings", menuName = "Team1/Kits/Xcode/Warnings")]
public class XcodeWarningsAttack : CustomAttack
{
    [Tooltip("【仮】何回降らせるか")]
    [SerializeField, Min(1)] int _waves = 3;
    [Tooltip("【仮】1回ごとの間（秒）")]
    [SerializeField, Min(1f)] float _interval = 1.9f;
    [Tooltip("【仮】影の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.0f;
    [Tooltip("【仮】すき間の幅（マス）")]
    [SerializeField, Min(1f)] float _gapWidth = 2.5f;
    [Tooltip("【仮】1つ目のすき間を、プレイヤーからこの距離（マス）以内に")]
    [SerializeField, Min(1f)] float _near = 4.5f;
    [Tooltip("【仮】三角の列の幅（マス）")]
    [SerializeField, Min(0.5f)] float _column = 1f;
    [Tooltip("【仮】落ちる速さ（マス/秒）")]
    [SerializeField, Min(1f)] float _fallSpeed = 18f;

    public float Telegraph => _telegraph;
    public float GapWidth => _gapWidth;
    public float Near => _near;
    public float Column => _column;
    public float FallSpeed => _fallSpeed;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            float fall = BattleArena.GetBounds().height / _fallSpeed;
            length = (_waves - 1) * _interval + _telegraph + fall + 0.1f;
            for (int i = 0; i < _waves; i++) XcodeWarningRain.Spawn(owner, data, this, i * _interval);
            XcodeWarningBadge.Spawn(owner, length);
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
