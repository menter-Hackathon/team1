using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Microsoft Teams のボスの技「画面共有」（2026-11-19。docs/app-kits-1-design.md 17章 P1-1）。
/// プレイヤーを中心に、赤い点線の枠（横 8×縦 6）→ 0.9秒で実線になる（予告）→ 枠の中が判定 0.4秒。枠の上に小さい札（共有中の印）。
/// 第2形態は 2回続く（2回目は、1回目が終わったときのプレイヤーを中心に）。避け方：枠の外へ 4マス
/// </summary>
[CreateAssetMenu(fileName = "Teams_Share", menuName = "Team1/Kits/Teams Share")]
public class TeamsShareAttack : CustomAttack
{
    [SerializeField] Vector2 _size = new Vector2(8f, 6f);
    [Tooltip("点線の秒数（予告。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(0.05f)] float _active = 0.4f;
    [Tooltip("続けて出す回数（第2形態は 2）")]
    [SerializeField, Min(1)] int _times = 1;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        for (int i = 0; i < _times; i++)
        {
            Vector2 c = owner.TargetPoint ?? arena.center;
            // 枠は部屋の中に収める（壁ぎわにいると、逃げる向きは 1つ）
            c.x = Mathf.Clamp(c.x, arena.xMin + _size.x * 0.5f, arena.xMax - _size.x * 0.5f);
            c.y = Mathf.Clamp(c.y, arena.yMin + _size.y * 0.5f - 1f, arena.yMax - _size.y * 0.5f);
            TeamsShareFrame.Spawn(owner, data, c, _size, _telegraph, _active);
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(_telegraph + _active);
        }
        yield return new WaitForSeconds(0.2f);
    }
}
