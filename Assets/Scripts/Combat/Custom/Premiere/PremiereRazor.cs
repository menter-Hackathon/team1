using System.Collections;
using UnityEngine;

/// <summary>
/// レーザーツール（Razor＝カミソリ。Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-3）。数値は【仮】。場に残る技。
/// 流れ：画面の上にカミソリの刃が2つ現れ、真下に白い点線（切る線）、2本の間（切られる区間）にうすい紫の斜線（0.9秒）→
/// 刃が上から下へ落ちて2本の線に判定（0.15秒）→ 区間が黒くなって左右から閉じる。区間の中の足場が消え、ボスがプレイヤーのほうへ寄る（0.3秒）→
/// 消えた足場は 4秒後に戻る（最後の 1秒は点滅）。
/// 本当には地形を詰めない（0章の表）。ここでは区間の位置を決めるだけで、出したあとは PremiereRazorEffect が自分で進む
/// </summary>
public class PremiereRazor : CustomAttack
{
    [Header("区間（3-3）")]
    [Tooltip("区間の幅（マス）。ボスとプレイヤーの x の真ん中を中心にして、部屋の中に収める")]
    [SerializeField, Min(0.5f)] float _sectionWidth = 4f;
    [Tooltip("予告を出す瞬間、切る線をプレイヤーの x からこれ以上離す（マス。離れるまで区間をずらす）")]
    [SerializeField, Min(0f)] float _lineClearance = 1f;

    [Header("時間")]
    [SerializeField, Min(0f)] float _telegraphTime = 0.9f;
    [SerializeField, Min(0f)] float _cutTime = 0.15f;
    [SerializeField, Min(0f)] float _deleteTime = 0.3f;
    [Tooltip("足場が消えている時間（秒。最後の「点滅して戻る」もふくむ）")]
    [SerializeField, Min(0f)] float _removeTime = 4f;
    [Tooltip("戻る前に点滅する時間（秒）")]
    [SerializeField, Min(0f)] float _blinkTime = 1f;

    [Header("ボスの寄り")]
    [Tooltip("ボスがプレイヤーのほうへ寄る距離（マス）")]
    [SerializeField, Min(0f)] float _approach = 4f;
    [Tooltip("ボスはプレイヤーの x からこれより近くには寄らない（マス）")]
    [SerializeField, Min(0f)] float _minBossDistance = 2.5f;

    [Header("見た目")]
    [Tooltip("【仮】刃を出す高さ：画面の上の端から、刃の上の端までの距離（マス）。画面の上の表示（HUD）に隠れないように下げてある")]
    [SerializeField, Min(0f)] float _topInset = PremiereArt.HudInset;

    [Header("判定（攻撃データ）")]
    [Tooltip("切る線の判定（Area・箱 幅 0.6・高さ 12・床の上に置く・判定 0.15秒・予告 0）")]
    [SerializeField] AttackData _line;

    public float SectionWidth => _sectionWidth;
    public float TelegraphTime => _telegraphTime;
    public float CutTime => _cutTime;
    public float DeleteTime => _deleteTime;
    public float RemoveTime => _removeTime;
    public float BlinkTime => _blinkTime;
    public float Approach => _approach;
    public float MinBossDistance => _minBossDistance;
    public float TopInset => _topInset;
    public AttackData Line => _line;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float center = PickCenter(owner, arena);
        PremiereRazorEffect effect = PremiereRazorEffect.Spawn(owner, this, center);
        owner.NotifyReleased(data);
        // 切って、ボスが寄り終えるまで待つ（足場が戻るのは待たない。場に残る）
        while (effect != null && !effect.ActionDone) yield return null;
    }

    /// <summary>区間の真ん中の x。ボスとプレイヤーの真ん中から始め、部屋に収まり、線がプレイヤーから離れる所までずらす</summary>
    float PickCenter(AttackRunner owner, Rect arena)
    {
        float half = _sectionWidth * 0.5f;
        float lo = arena.xMin + half, hi = arena.xMax - half;
        if (hi < lo) return arena.center.x;
        float bx = owner.transform.position.x;
        float px = (owner.TargetPoint ?? owner.OriginPoint).x;
        float c0 = Mathf.Clamp((bx + px) * 0.5f, lo, hi);
        const float step = 0.1f;
        int maxSteps = Mathf.CeilToInt(arena.width / step) + 1;
        for (int s = 0; s <= maxSteps * 2; s++)
        {
            // 0, +0.1, -0.1, +0.2, -0.2 … の順に試す
            float offset = ((s + 1) / 2) * step * (s % 2 == 1 ? 1f : -1f);
            float c = c0 + offset;
            if (c < lo - 0.0001f || c > hi + 0.0001f) continue;
            if (Mathf.Abs(c - half - px) >= _lineClearance && Mathf.Abs(c + half - px) >= _lineClearance) return c;
        }
        return c0;
    }
}
