using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player の技2「トリム」（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// 流れ：下に再生バー。黄色いつまみ 2つが、残す範囲（幅 6マス）を囲む。外側に赤いしま（1.2秒）→ 外側が切られる（床から天井まで。判定 0.2秒）。
/// ・残す範囲は、プレイヤーから届く所（範囲の真ん中がプレイヤーから 4マス以内。7マス＝1.0秒＜予告 1.2秒）
/// ・第2形態：2回続ける。2回目の範囲は幅 4マスで、1回目から 5マス以上離れる（予告 1.4秒。真ん中がその時のプレイヤーから 6マス以内）
/// </summary>
[CreateAssetMenu(fileName = "QuickTimeTrim_", menuName = "Team1/Kits/QuickTime/Trim")]
public class QuickTimeTrimAttack : CustomAttack
{
    [SerializeField, Min(1f)] float _width = 6f;
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [Tooltip("第2形態：2回目（幅・予告。幅 0 なら出さない）")]
    [SerializeField, Min(0f)] float _width2;
    [SerializeField, Min(0.8f)] float _telegraph2 = 1.4f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float c1 = PickCenter(owner, arena, _width, float.NaN);
        Spawn(owner, data, arena, c1, _width, _telegraph);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.25f);
        if (_width2 > 0f)
        {
            float c2 = PickCenter(owner, arena, _width2, c1);
            Spawn(owner, data, arena, c2, _width2, _telegraph2);
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(_telegraph2 + 0.25f);
        }
    }

    static void Spawn(AttackRunner owner, AttackData data, Rect arena, float center, float width, float telegraph)
    {
        GameObject root = owner.CreateSpawnedObject("QuickTimeTrim", new Vector2(center, arena.center.y));
        root.AddComponent<QuickTimeTrimEffect>().Setup(owner, data, arena, center, width, telegraph);
    }

    // 範囲の真ん中：プレイヤーから 4マス以内（avoid があれば、そこから 5マス以上・プレイヤーから 6マス以内）
    static float PickCenter(AttackRunner owner, Rect arena, float width, float avoid)
    {
        Vector2 target = owner.TargetPoint ?? arena.center;
        float min = arena.xMin + width * 0.5f, max = arena.xMax - width * 0.5f;
        float best = Mathf.Clamp(target.x, min, max);
        float bestScore = float.NegativeInfinity;
        // 2回目は ±6マスまで（1回目の範囲の中にいるプレイヤーから、5マス以上離すため。6マス＝0.86秒＜予告 1.4秒）
        float reach = float.IsNaN(avoid) ? 4f : 6f;
        for (int i = 0; i < 40; i++)
        {
            float c = Mathf.Clamp(target.x + Random.Range(-reach, reach), min, max);
            float score = 0f;
            if (!float.IsNaN(avoid)) score -= Mathf.Max(0f, 5f - Mathf.Abs(c - avoid)) * 10f;
            score -= Mathf.Max(0f, Mathf.Abs(c - target.x) - reach) * 5f;
            if (score > bestScore) { bestScore = score; best = c; }
            if (score >= 0f) break;
        }
        return best;
    }
}
