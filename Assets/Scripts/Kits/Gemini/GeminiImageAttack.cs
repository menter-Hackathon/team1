using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】画像生成（2026-11-19。Gemini の第2形態の技。docs/app-kits-1-design.md 7章 P2）。担当3 の物。
/// 大きい絵（額ぶち。4×3マス）が、そのときのプレイヤーの位置に 2枚（0.6秒おき。影の予告 0.8秒）→ 着地後 3秒残って足場になる。
/// ・絵は中の攻撃データ（落下物）を FallingObject で出し、額ぶちの絵に差し替える
///   （AttackRunner の落下物は、くり返しで狙い直さないので、ここで 1枚ずつ狙う）
/// </summary>
[CreateAssetMenu(fileName = "Gemini_Image", menuName = "Team1/Kits/Gemini Image")]
public class GeminiImageAttack : CustomAttack
{
    [Tooltip("落とす絵（落下物。大きさ・落ちる速さ・予告・残る秒数・足場）")]
    [SerializeField] AttackData _frame;
    [Tooltip("【仮】枚数")]
    [SerializeField, Min(1)] int _count = 2;
    [Tooltip("【仮】間隔（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _frame != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_frame == null) yield break;
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _frame.ObjectSize;
        float half = size.x * 0.5f;
        var objects = new List<FallingObject>();
        // ボスの上に星（「生成中」）
        Bounds body = GeminiArt.BodyOf(owner);
        FxAnim.Sprite(GeminiArt.Star4(13), new Vector2(body.center.x, body.max.y + 0.8f), 33, _count * _interval + 0.4f, Color.white, 1.4f, "GeminiGenerate").Spin(300f).Pop().FadeFrom(0.8f);
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            float x = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, arena.xMin + half, arena.xMax - half);
            float fallAt = Time.time + _frame.TelegraphTime;
            FallingObject f = FallingObject.Spawn(owner, _frame, new Vector2(x, arena.yMax + size.y * 0.5f), fallAt, fallAt, true, false, i);
            if (f != null)
            {
                KitDress.Swap(f.gameObject, GeminiArt.ImageFrame);
                objects.Add(f);
            }
            owner.NotifyReleased(data);
        }
        float timeout = Time.time + 10f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }
}
