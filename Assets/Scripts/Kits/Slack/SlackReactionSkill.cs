using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Slack の能力「リアクション」（2026-11-19。docs/app-kits-1-design.md 2章）。
/// 親指の絵文字を正面に投げる（毎秒 12マス・届く距離 8マス）。当たるとゲージ +6（弾の攻撃データ）、雑魚は倒れる。
/// 当たった相手の上に「親指＋数字」。そこから 5秒、自分の全部の攻撃のゲージが ×1.5（StatMods。重ねがけしない）。
/// もう一度当てると、時間だけ 5秒に戻り、数字が増える（飾り）。外したら倍率は付かない
/// </summary>
[CreateAssetMenu(fileName = "Slack_ReactionSkill", menuName = "Team1/Kits/Slack Reaction Skill")]
public class SlackReactionSkill : CustomAttack
{
    /// <summary>倍率の鍵</summary>
    public const string BuffKey = "slack.reaction";

    [Tooltip("投げる親指の攻撃データ（種類＝弾。速さ・寿命・ゲージ・ダメージ）")]
    [SerializeField] AttackData _shot;
    [SerializeField, Min(0.1f)] float _buffSeconds = 5f;
    [SerializeField, Min(1f)] float _buffMultiplier = 1.5f;

    static int _count;

    public override bool CanRun(AttackRunner owner, AttackData data) => _shot != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_shot == null) yield break;
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * _shot.SpawnOffset.x, _shot.SpawnOffset.y);
        Projectile p = Projectile.Spawn(owner, _shot, origin, new Vector2(owner.Facing, 0f));
        if (p == null) yield break;
        KitDress.Swap(p.gameObject, TalkArt.Thumb);
        // 弾は進む向きに回る（左向きは 180度）。親指が逆さにならないように、上下を反転する
        if (owner.Facing < 0)
        {
            SpriteRenderer sr = KitDress.FindVisual(p.transform);
            if (sr != null) sr.flipY = true;
        }
        Hitbox hb = p.GetComponent<Hitbox>();
        if (hb != null) hb.Hit += (target, info) => OnHit(info);
        owner.NotifyReleased(data);
    }

    void OnHit(DamageInfo info)
    {
        bool active = StatMods.Player.GaugeGain.Has(BuffKey);
        _count = active ? _count + 1 : 1;
        StatMods.Player.GaugeGain.SetFor(BuffKey, _buffMultiplier, _buffSeconds, SlackArt.Logo);
        ShowReaction(info.HitPoint + Vector2.up * 0.9f, _count);
    }

    /// <summary>当たった相手の上に「親指＋数字」（リアクションのピル）。浮いて消える</summary>
    static void ShowReaction(Vector2 at, int count)
    {
        FxAnim a = FxAnim.Create("Slack_Reaction", at, 0.9f);
        var pill = new TalkCanvas(40, 16);
        pill.Round(0, 0, 40, 16, 7, SlackArt.PillFill, SlackArt.PillEdge);
        TalkArt.ShowNative(a.transform, pill.ToSprite("Slack_ReactionPill40"), Vector2.zero, 45, 1f, "Pill");
        TalkArt.ShowNative(a.transform, TalkArt.Thumb, new Vector2(-0.45f, 0.02f), 46, 1f, "Thumb");
        TalkArt.Text(a.transform, count > 99 ? "99+" : count.ToString(), new Vector2(0.45f, 0f), 0.4f, (Color)SlackArt.PillEdge, 47);
        a.Rise(0.6f).Pop().FadeFrom(0.65f).Refresh();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _count = 0;
}
