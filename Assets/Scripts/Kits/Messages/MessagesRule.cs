using UnityEngine;

/// <summary>
/// 【仮】メッセージのステージのギミック「風船」（2026-11-19。docs/app-kits-1-design.md 16章）。
/// ・20秒おきに、画面のエフェクト「風船」：床から風船が 5つ、ゆっくり上がる（毎秒 2マス。KitProp。痛くない）
/// ・近接か能力で割ると、1つにつきクールタイムが 1秒進む。5つ全部割ると、紙吹雪と、8秒 与えるゲージ ×1.3（StatMods）
/// ・高い所の風船は、足場からジャンプして割る。ボス戦でも同じ。危ない物は出さない（ごほうびだけ）
/// </summary>
public class MessagesRule : StageRule
{
    const float Interval = 20f;
    const int Count = 5;
    const float RiseSpeed = 2f;
    const float CooldownReward = 1f;
    const float BuffSeconds = 8f;
    const float BuffMultiplier = 1.3f;

    float _timer;
    MessagesBalloonGroup _group;

    string BuffKey => Ctx.Key("balloons");

    public override void Begin(StageRuleContext ctx) => _timer = Interval * 0.6f;

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _timer = Interval * 0.6f;
    }

    public override void Tick(float dt)
    {
        if (_group != null) return;
        _timer += dt;
        if (_timer < Interval || !Ctx.CanPlace) return;
        _timer = 0f;
        _group = MessagesBalloonGroup.Spawn(Ctx.PlayArea, Count, RiseSpeed, OnPop, OnAll);
        Ctx.TrackPlaced(_group.gameObject);
        GameAudio.Event("gm_messages_balloons");
        KitToast.Show("Sent with Balloons", MessagesArt.Icon, (Color)MessagesArt.Blue);
    }

    void OnPop()
    {
        KitPlayer.AdvanceCooldowns(CooldownReward);
        // 割るほど高く
        GameAudio.Event("gm_messages_pop", 1f + 0.08f * (_group != null ? _group.Popped : 0));
    }

    void OnAll()
    {
        StatMods.Player.GaugeGain.SetFor(BuffKey, BuffMultiplier, BuffSeconds, MessagesArt.Icon);
        GameAudio.Event("gm_messages_all");
        Confetti(Ctx.PlayArea);
    }

    /// <summary>紙吹雪（画面の上から色の四角が舞い落ちる。飾り）</summary>
    static void Confetti(Rect area)
    {
        for (int i = 0; i < 28; i++)
        {
            Vector2 p = new Vector2(Random.Range(area.xMin + 0.5f, area.xMax - 0.5f), area.yMax - Random.Range(0f, 1.5f));
            Color c = (Color)MessagesArt.FireworkColors[i % MessagesArt.FireworkColors.Length];
            FxAnim.Sprite(MessagesArt.Confetti, p, 35, Random.Range(1.2f, 1.8f), c, 2f, "Confetti")
                .Velocity(new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 0.5f))).Gravity(2.5f).Spin(Random.Range(-360f, 360f)).FadeFrom(0.7f);
        }
    }

    public override void End()
    {
        StatMods.Player.GaugeGain.Clear(BuffKey);
        _group = null;
    }
}
