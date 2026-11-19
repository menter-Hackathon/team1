using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft の能力「TNT」（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// ・正面 1.5マスに TNT を置く。1.2秒で爆発（半径 2.5マス）。自分には当たらない。近接で押せる（3マス）。ボスの砂のブロックもこわす
/// ・今のボム（ゲームのカテゴリ）は「投げてすぐ」。こちらは「置いて待つ・大きい」。ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "MinecraftTntSkill_", menuName = "Team1/Kits/Minecraft/TNT Skill")]
public class MinecraftTntSkill : CustomAttack
{
    [SerializeField, Min(0.5f)] float _forward = 1.5f;
    [SerializeField, Min(0.3f)] float _fuse = 1.2f;
    [SerializeField, Min(0.5f)] float _radius = 2.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 o = owner.OriginPoint;
        float x = o.x + owner.Facing * _forward;
        MinecraftTnt.Spawn(owner, data, new Vector2(x, o.y), new Vector2(x, o.y), 0f, _fuse, _radius, false);
        owner.NotifyReleased(data);
        yield break;
    }
}
