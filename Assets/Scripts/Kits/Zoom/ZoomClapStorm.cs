using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Zoom のボスの技「リアクションの嵐」（2026-11-19。docs/app-kits-1-design.md 18章 P2）。
/// ボスの上に拍手の手が count 個並ぶ（0.8秒。落ちる所に影と赤い「！」）→ 1つ目はプレイヤーの真上、残りは部屋に散らばって落ちる（毎秒 9マス）。
/// 避け方：影の無い所に立つ。落ちる物の数値（大きさ・速さ）はこの技の攻撃データの「落下物」の欄。絵は拍手の手（自作のドット絵）
/// </summary>
[CreateAssetMenu(fileName = "Zoom_ClapStorm", menuName = "Team1/Kits/Zoom Clap Storm")]
public class ZoomClapStorm : CustomAttack
{
    [Tooltip("落とす数")]
    [SerializeField, Min(1)] int _count = 5;
    [Tooltip("予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("2つ目からは、プレイヤーとほかの手からこれだけ離す（マス）")]
    [SerializeField, Min(0f)] float _spread = 2.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float half = data.ObjectSize.x * 0.5f;
        float px = Mathf.Clamp((owner.TargetPoint ?? arena.center).x, arena.xMin + half, arena.xMax - half);
        var xs = new List<float> { px };
        for (int i = 1; i < _count; i++) xs.Add(PickX(arena, half, xs));

        // ボスの上に並ぶ（飾り。落ちる物とは別）
        var w = owner.GetComponent<IWarpable>();
        Bounds body = w != null ? w.BodyBounds : new Bounds(owner.transform.position, Vector3.one * 2f);
        for (int i = 0; i < _count; i++)
        {
            Vector2 at = new Vector2(body.center.x + (i - (_count - 1) * 0.5f) * 0.9f, body.max.y + 0.8f);
            FxAnim a = FxAnim.Sprite(ZoomKitArt.Clap, at, 42, _telegraph, Color.white, 1f, "Zoom_ClapUp").Pop().Delay(i * 0.06f).FadeFrom(0.85f);
            owner.Register(a.gameObject);
        }

        float hangY = arena.yMax + data.ObjectSize.y * 0.5f;
        float fallAt = Time.time + _telegraph;
        var objs = new List<FallingObject>();
        for (int i = 0; i < xs.Count; i++)
        {
            FallingObject f = FallingObject.Spawn(owner, data, new Vector2(xs[i], hangY), fallAt, fallAt + i * 0.05f, true, false, i);
            if (f == null) continue;
            KitDress.Swap(f.gameObject, ZoomKitArt.Clap);
            CallKitUtil.AddExclaim(f);
            objs.Add(f);
        }
        owner.NotifyReleased(data);
        float timeout = Time.time + 6f;
        while (Time.time < timeout && objs.Exists(o => o != null && !o.HasLanded)) yield return null;
    }

    float PickX(Rect arena, float half, List<float> taken)
    {
        float best = Random.Range(arena.xMin + half, arena.xMax - half);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float x = Random.Range(arena.xMin + half, arena.xMax - half);
            bool ok = true;
            foreach (float t in taken) if (Mathf.Abs(t - x) < _spread) { ok = false; break; }
            if (ok) return x;
            best = x;
        }
        return best;
    }
}
