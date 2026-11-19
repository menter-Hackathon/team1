using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player の技3（第2形態）「画面収録」（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// 流れ：上に赤い丸と `REC`。2秒の間、プレイヤーの通った道に赤い点線が残る → 録画が止まり、点線が太く赤く光る（0.8秒）→
///       赤い影が、同じ道を 1.5倍の速さでなぞる。影に触れると当たる。
/// ・避け方：自分の通った道から離れる。止まっていた場合（点線が点）は、その場から動けば当たらない
/// </summary>
[CreateAssetMenu(fileName = "QuickTimeRecord_", menuName = "Team1/Kits/QuickTime/Screen Recording")]
public class QuickTimeRecordAttack : CustomAttack
{
    [SerializeField, Min(0.5f)] float _record = 2f;
    [SerializeField, Min(0.8f)] float _glow = 0.8f;
    [SerializeField, Min(1f)] float _speedUp = 1.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject root = owner.CreateSpawnedObject("QuickTimeRecord", arena.center);
        var e = root.AddComponent<QuickTimeRecordEffect>();
        e.Setup(owner, data, arena, _record, _glow, _speedUp);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_record + _glow + _record / _speedUp + 0.2f);
    }
}
