using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player の能力「トリム」（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// ・自分の左右 3.5マスに黄色いつまみが出て、0.2秒で自分の所まで閉じる。間の敵に当たり、雑魚は自分の前へ寄せられる
/// ・ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "QuickTimeTrimSkill_", menuName = "Team1/Kits/QuickTime/Trim Skill")]
public class QuickTimeTrimSkill : CustomAttack
{
    [SerializeField, Min(0.5f)] float _reach = 3.5f;
    [SerializeField, Min(0.05f)] float _close = 0.2f;
    [SerializeField, Min(0.5f)] float _height = 2.4f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 o = owner.OriginPoint;
        GameObject root = owner.CreateSpawnedObject("QuickTimeTrimSkill", o);
        root.AddComponent<QuickTimeTrimSkillEffect>().Setup(owner, data, _reach, _close, _height);
        owner.NotifyReleased(data);
        yield break;
    }
}
