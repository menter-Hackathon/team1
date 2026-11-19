using System.Collections;
using UnityEngine;

/// <summary>
/// レーザーツール（Razor＝カミソリ。Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-3）。数値は【仮】。場に残る技。
/// 2026-11-19（docs/app-feedback-v2-design.md 5-3）：区間（幅4マス）のたての列を、まるごと消して、閉じる。
/// 流れ：画面の上にカミソリの刃が2つ、真下に赤い点線（切る線）、2本の間に赤むらさきの斜線（0.9秒）→
/// 刃が上から下へ落ちる。判定は区間の中の全部（幅4×床から天井、0.15秒）→
/// 区間の列が、背景・床の絵・足場・タイムラインの飾りごと黒く抜ける（中は暗い市松。0.25秒）→
/// 黒い列が左右からまん中へ閉じる（0.4秒。画面が横に1回ゆれる。ボスがプレイヤーのほうへ寄る）→ つなぎ目に白い線（0.5秒）。
/// 区間の中の足場は 4秒消えたまま（点線の枠だけ）。床の当たり判定は消さない。絵の上に黒い列をかぶせるだけ（左右の絵は動かさない）。
/// ここでは区間の位置を決めるだけで、出したあとは PremiereRazorEffect が自分で進む
/// </summary>
public class PremiereRazor : CustomAttack
{
    [Header("区間（5-3）")]
    [Tooltip("区間の幅（マス）。ボスとプレイヤーの x の真ん中を中心にして、部屋の中に収める")]
    [SerializeField, Min(0.5f)] float _sectionWidth = 4f;
    [Tooltip("2026-11-19：予告を出す瞬間、プレイヤーから「出られる側の区間の端」までをこれ以下にする（マス。壁でふさがった側は数えない。越えるならずらす）")]
    [SerializeField, Min(0.5f)] float _maxExitDistance = 2.5f;

    [Header("時間")]
    [SerializeField, Min(0f)] float _telegraphTime = 0.9f;
    [SerializeField, Min(0f)] float _cutTime = 0.15f;
    [Tooltip("2026-11-19：黒く抜けている時間（秒）")]
    [SerializeField, Min(0f)] float _blackTime = 0.25f;
    [Tooltip("2026-11-19：黒い列が左右から閉じる時間（秒）")]
    [SerializeField, Min(0f)] float _closeTime = 0.4f;
    [Tooltip("2026-11-19：閉じた所の白い線が残る時間（秒）")]
    [SerializeField, Min(0f)] float _seamTime = 0.5f;
    [Tooltip("足場が消えている時間（秒。黒く抜けた瞬間から。最後の「点滅して戻る」もふくむ）")]
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
    [Tooltip("切る判定のダメージ・ノックバック（Area の攻撃データ）。2026-11-19：判定の形は区間の全部（技の中で作る）")]
    [SerializeField] AttackData _line;

    public float SectionWidth => _sectionWidth;
    public float TelegraphTime => _telegraphTime;
    public float CutTime => _cutTime;
    public float BlackTime => _blackTime;
    public float CloseTime => _closeTime;
    public float SeamTime => _seamTime;
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
        PremiereRazorEffect effect = PremiereRazorEffect.Spawn(owner, this, center, _line != null ? _line : data);
        owner.NotifyReleased(data);
        // 切って、閉じて、ボスが寄り終えるまで待つ（足場が戻るのは待たない。場に残る）
        while (effect != null && !effect.ActionDone) yield return null;
    }

    /// <summary>
    /// 区間の真ん中の x。ボスとプレイヤーの真ん中から始め、部屋に収め、
    /// プレイヤーが区間の中にいるなら「出られる側の端」まで _maxExitDistance 以下になる所までずらす
    /// </summary>
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
            if (CanEscape(c, half, px, arena)) return c;
        }
        return c0;
    }

    bool CanEscape(float c, float half, float px, Rect arena)
    {
        float left = c - half, right = c + half;
        if (px <= left || px >= right) return true; // 区間の外
        bool leftOpen = left > arena.xMin + 0.05f, rightOpen = right < arena.xMax - 0.05f;
        float best = float.PositiveInfinity;
        if (leftOpen) best = Mathf.Min(best, px - left);
        if (rightOpen) best = Mathf.Min(best, right - px);
        return best <= _maxExitDistance;
    }
}
