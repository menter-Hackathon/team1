using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのボスの技「毎週の予定」（2026-11-19。docs/app-kits-1-design.md 9章 P2）。Custom 小。
/// プレイヤーの足元に柱（2×5）の予告 0.8秒 → 判定 0.4秒。interval 秒後、同じ場所と今の足元に 2本。
/// さらに interval 秒後、前の 2か所と今の足元に 3本（くり返す予定）。毎回、予告は 0.8秒（赤い丸）。
/// くり返す場所には、回る矢印の印。避け方：前に出た場所には戻らない。新しい場所へ 1.5マスずつ動く。
/// 柱の数値（大きさ・予告・判定の時間）はこの技の攻撃データの「指定位置の判定」の欄（AreaStrike で出す）
/// </summary>
[CreateAssetMenu(fileName = "Calendar_Weekly", menuName = "Team1/Kits/Calendar Weekly")]
public class CalendarWeeklyAttack : CustomAttack
{
    [Tooltip("回数（1回ごとに柱が1本増える）")]
    [SerializeField, Min(1)] int _waves = 3;
    [Tooltip("次の回までの間（秒）")]
    [SerializeField, Min(0.3f)] float _interval = 0.9f;
    [Tooltip("新しい柱を、前の柱からこれより近くには置かない（マス。近ければ同じ場所とみなす）")]
    [SerializeField, Min(0f)] float _sameSpot = 0.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float half = data.AreaSize.x * 0.5f;
        var spots = new List<Vector2>();
        for (int w = 0; w < _waves; w++)
        {
            if (w > 0) yield return new WaitForSeconds(_interval);
            Vector2 feet = FeetOf(owner);
            feet.x = Mathf.Clamp(feet.x, arena.xMin + half, arena.xMax - half);
            // 前の場所（くり返し）
            for (int i = 0; i < spots.Count; i++)
            {
                AreaStrike.Spawn(owner, data, spots[i], Vector2.up, 0f, i);
                ShowRepeat(owner, spots[i], data);
            }
            // 今の足元（前の場所と重なれば、くり返しだけ）
            bool same = false;
            foreach (var s in spots) if (Mathf.Abs(s.x - feet.x) < _sameSpot && Mathf.Abs(s.y - feet.y) < 1f) same = true;
            if (!same)
            {
                AreaStrike.Spawn(owner, data, feet, Vector2.up, 0f, spots.Count);
                spots.Add(feet);
            }
            owner.NotifyReleased(data);
        }
        yield return new WaitForSeconds(data.TelegraphTime + data.HitWindow);
    }

    /// <summary>プレイヤーの足元（体の下の端の少し上）。床に付けるのは AreaStrike（真下の床を探す）</summary>
    static Vector2 FeetOf(AttackRunner owner)
    {
        Transform t = owner.Target;
        if (t != null && CallKitUtil.TryBodyBounds(t, out Bounds b)) return new Vector2(b.center.x, b.min.y + 0.1f);
        return owner.TargetPoint ?? owner.OriginPoint;
    }

    /// <summary>くり返す場所の印（回る矢印。予告の間だけ、柱の上の方に出る）</summary>
    static void ShowRepeat(AttackRunner owner, Vector2 spot, AttackData data)
    {
        owner.TryFindGround(spot.x, spot.y + 0.5f, out float ground);
        FxAnim a = FxAnim.Sprite(CalendarArt.Repeat, new Vector2(spot.x, ground + 1.4f), 40, Mathf.Max(0.3f, data.TelegraphTime), Color.white, 1.2f, "Calendar_Repeat")
            .Spin(-360f).FadeFrom(0.8f);
        if (a != null) owner.Register(a.gameObject);
    }
}
