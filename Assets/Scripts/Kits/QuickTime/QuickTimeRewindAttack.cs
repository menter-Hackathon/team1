using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player の技1「巻き戻し」（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// 流れ：大きい丸い弾を N発、扇に撃つ（赤い線 0.8秒。毎秒 6マス）→ 1.2秒進んだ所で止まり、`⏪` の印と赤い点滅（0.8秒）→
///       来た道を戻る（毎秒 9マス）。ボスのいた所に着いて消える。
/// ・避け方：行きの道をおぼえて、戻りの道にいない
/// ・第2形態：5発。戻りは、今のプレイヤーへ向きを変える弾が 1発まじる（その弾だけ、止まっている間に赤い線。向きは止まった時に固定）
/// </summary>
[CreateAssetMenu(fileName = "QuickTimeRewind_", menuName = "Team1/Kits/QuickTime/Rewind")]
public class QuickTimeRewindAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 3;
    [SerializeField, Range(0f, 120f)] float _spread = 50f;
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(1f)] float _outSpeed = 6f;
    [SerializeField, Min(0.2f)] float _outTime = 1.2f;
    [SerializeField, Min(0.8f)] float _hold = 0.8f;
    [SerializeField, Min(1f)] float _backSpeed = 9f;
    [Tooltip("第2形態：戻りでプレイヤーへ向きを変える弾を1発まぜる")]
    [SerializeField] bool _oneAims;
    [SerializeField, Min(0.2f)] float _size = 0.9f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 origin = owner.OriginPoint;
        Vector2 target = owner.TargetPoint ?? origin + new Vector2(owner.Facing, 0f);
        Vector2 baseDir = (target - origin).sqrMagnitude > 0.0001f ? (target - origin).normalized : new Vector2(owner.Facing, 0f);
        float len = _outSpeed * _outTime;
        var dirs = new List<Vector2>();
        for (int i = 0; i < _count; i++)
        {
            float a = _count > 1 ? -_spread * 0.5f + _spread * i / (_count - 1) : 0f;
            Vector2 d = Quaternion.Euler(0f, 0f, a) * baseDir;
            dirs.Add(d);
            KitDress.Line(owner, origin, origin + d * len, _telegraph);
        }
        yield return new WaitForSeconds(_telegraph);
        int aimer = _oneAims ? Random.Range(0, _count) : -1;
        for (int i = 0; i < dirs.Count; i++)
        {
            GameObject go = owner.CreateSpawnedObject("QuickTimeRewindBall", origin);
            go.AddComponent<QuickTimeRewindBall>().Setup(owner, data, origin, dirs[i], _outSpeed, _outTime, _hold, _backSpeed, i == aimer, _size);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_outTime + _hold + len / _backSpeed + 0.2f);
    }
}
