using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft の技1「TNT」（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// 流れ：ボスが TNT を山なりに N個投げる（プレイヤーの近く、2.5マス離して）→ 床で白く点滅（1.5秒。半径 2.5マスの赤い円）→ 爆発。
/// ・避け方：赤い円から出る。近接で押せる（1回で 3マスすべる）
/// ・第2形態：4つ。0.4秒ずつずらして順に爆発（攻撃データの欄で）
/// </summary>
[CreateAssetMenu(fileName = "MinecraftTnt_", menuName = "Team1/Kits/Minecraft/TNT")]
public class MinecraftTntAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 2;
    [SerializeField, Min(0.5f)] float _spacing = 2.5f;
    [SerializeField, Min(0.1f)] float _fly = 0.6f;
    [Tooltip("止まってから爆発までの秒数（赤い円の時間。短くしない）")]
    [SerializeField, Min(0.8f)] float _fuse = 1.5f;
    [Tooltip("1つずつ爆発をずらす秒数（第2形態 0.4）")]
    [SerializeField, Min(0f)] float _stagger;
    [SerializeField, Min(0.5f)] float _radius = 2.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 target = owner.TargetPoint ?? arena.center;
        float offset = Random.Range(-0.6f, 0.6f);
        for (int i = 0; i < _count; i++)
        {
            float x = target.x + offset + (i - (_count - 1) * 0.5f) * _spacing;
            x = Mathf.Clamp(x, arena.xMin + 0.8f, arena.xMax - 0.8f);
            MinecraftTnt.Spawn(owner, data, owner.OriginPoint, new Vector2(x, arena.yMin + 0.5f), _fly, _fuse + i * _stagger, _radius, true);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_fly + _fuse + (_count - 1) * _stagger + 0.2f);
    }
}
