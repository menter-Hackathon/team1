using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Accept / Reject（Cursor のボスの技2。2026-11-19。docs/app-kits-2-design.md 3-3）。部屋の半分が Reject される。
/// 部屋が左右に分かれ、片方が緑（+）、片方が赤（-）になる（1.8秒。ボスの頭の上に Accept と Reject のボタン）→ 赤い所が消える（床から天井まで。判定 0.2秒）。
/// 第1形態：2つに分けて1回。プレイヤーのいる側が赤。第2形態：3つに分けて 2つが赤（緑は、プレイヤーのいる区画か、となり）。続けて2回。
/// </summary>
[CreateAssetMenu(fileName = "Cursor_AcceptReject", menuName = "Team1/Kits/Cursor/Accept Reject")]
public class CursorAcceptRejectAttack : CustomAttack
{
    [Tooltip("【仮】区画の数（2 か 3）")]
    [SerializeField, Range(2, 3)] int _sections = 2;
    [Tooltip("【仮】何回か")]
    [SerializeField, Min(1)] int _repeat = 1;
    [Tooltip("【仮】予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.8f;
    [Tooltip("【仮】判定（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.2f;
    [Tooltip("【仮】次の回までの間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.3f;

    public int Sections => _sections;
    public float Telegraph => _telegraph;
    public float HitTime => _hitTime;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            float step = _telegraph + _hitTime + _gap;
            for (int i = 0; i < _repeat; i++) CursorDiffZones.Spawn(owner, data, this, i * step, i == 0);
            length = _repeat * step - _gap + 0.1f;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
