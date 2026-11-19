using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】LINE のボスの技「スタンプ連打」（2026-11-19。docs/app-kits-1-design.md 4章 P1-1）。
/// 四角いスタンプ（3×2マス）が、プレイヤーの位置に 0.35秒おきに N個落ちる（毎回狙い直す。影の予告 0.8秒、毎秒 10マス）。
/// 着地で小さいハートが飛ぶ（飾り）。避け方：止まらずに歩く。
/// 今の部品（落下物の種類）は「くり返し」を使わないので、ここで 1個ずつ出す
/// </summary>
[CreateAssetMenu(fileName = "Line_StampBarrage", menuName = "Team1/Kits/LINE Stamp Barrage")]
public class LineStampBarrageAttack : CustomAttack
{
    [Tooltip("落とすスタンプの攻撃データ（種類＝落下物。大きさ・速さ・予告・ダメージ）。名前に枠の語を入れない")]
    [SerializeField] AttackData _drop;
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("落とす間隔（秒）")]
    [SerializeField, Min(0.05f)] float _interval = 0.35f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _drop != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_drop == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float half = _drop.ObjectSize.x * 0.5f;
        float hangY = arena.yMax + _drop.ObjectSize.y * 0.5f;
        var drops = new List<FallingObject>();
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            float x = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, arena.xMin + half, arena.xMax - half);
            float fallAt = Time.time + Mathf.Max(0.8f, _drop.TelegraphTime);
            FallingObject f = FallingObject.Spawn(owner, _drop, new Vector2(x, hangY), fallAt, fallAt, true, false, i);
            if (f == null) continue;
            KitDress.Swap(f.gameObject, LineArt.Stamp(i));
            f.gameObject.AddComponent<LineStampLanding>();
            drops.Add(f);
            owner.NotifyReleased(data);
        }
        float timeout = Time.time + 5f;
        while (Time.time < timeout && drops.Exists(d => d != null && !d.HasLanded)) yield return null;
    }
}
