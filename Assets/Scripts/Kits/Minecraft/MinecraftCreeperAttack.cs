using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft の技3（第2形態）「緑のブロック」（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// 流れ：部屋の端に緑のブロックの生き物が 2体出る（煙。0.8秒）→ 毎秒 3.5マスでプレイヤーへ歩く →
///       2マス以内で止まって白く点滅（1.0秒。半径 2.5マスの赤い円。場所は固定）→ 爆発。
/// ・避け方：点滅の前に近接1回で倒せば、爆発しない。点滅したら離れる
/// ・雑魚ではない（倒した数に入れない・体当たりのダメージなし）。ボスの出した物（50％・100％で消える）
/// </summary>
[CreateAssetMenu(fileName = "MinecraftCreeper_", menuName = "Team1/Kits/Minecraft/Green Block")]
public class MinecraftCreeperAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 2;
    [SerializeField, Min(0.1f)] float _smoke = 0.8f;
    [SerializeField, Min(0.5f)] float _speed = 3.5f;
    [SerializeField, Min(0.5f)] float _near = 2f;
    [Tooltip("点滅（赤い円）の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _fuse = 1f;
    [SerializeField, Min(0.5f)] float _radius = 2.5f;
    [Tooltip("歩く時間の上限（ふさがれていたら、ここで点滅を始める）")]
    [SerializeField, Min(1f)] float _maxWalk = 6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        for (int i = 0; i < _count; i++)
        {
            float x = i % 2 == 0 ? arena.xMin + 1.2f : arena.xMax - 1.2f;
            GameObject root = owner.CreateSpawnedObject("MinecraftCreeper", new Vector2(x, arena.yMin));
            root.AddComponent<MinecraftCreeper>().Setup(owner, data, _smoke, _speed, _near, _fuse, _radius, _maxWalk);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_smoke + 1.5f);
    }
}
