using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】LINE の能力「スタンプ」（2026-11-19。docs/app-kits-1-design.md 4章）。
/// 正面 3マス先に、大きいスタンプ（3×2マス）が上から「ドン」と押される。押して 0.25秒で判定（落ちてくるのは絵だけ）。
/// 下にいた雑魚は全部倒れる。ボスにはゲージ +10（攻撃データの共通の欄）。短くて素直な、主力の能力
/// </summary>
[CreateAssetMenu(fileName = "Line_StampSkill", menuName = "Team1/Kits/LINE Stamp Skill")]
public class LineStampSkill : CustomAttack
{
    [SerializeField] Vector2 _size = new Vector2(3f, 2f);
    [Tooltip("正面の何マス先か")]
    [SerializeField] float _forward = 3f;
    [Tooltip("押されるまで（絵が落ちてくる秒数）")]
    [SerializeField, Min(0.05f)] float _drop = 0.25f;
    [Tooltip("判定の秒数")]
    [SerializeField, Min(0.05f)] float _active = 0.2f;

    static int _index;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float x = owner.OriginPoint.x + owner.Facing * _forward;
        owner.TryFindGround(x, owner.OriginPoint.y + 0.5f, out float groundY);
        Vector2 center = new Vector2(x, groundY + _size.y * 0.5f);
        GameObject go = owner.CreateSpawnedObject("Line_StampSkill", center);
        var stamp = go.AddComponent<LineStampPress>();
        stamp.Setup(owner, data, _size, _drop, _active, _index++);
        owner.NotifyReleased(data);
        yield break;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _index = 0;
}
