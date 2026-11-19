using UnityEngine;

/// <summary>
/// 【仮】ChatGPT のステージのギミック「考え中…」（2026-11-19。docs/app-kits-1-design.md 5章）。担当3 の物。
/// ・ボス戦：18秒おきに、ボスが「考え中…」になる（頭の上の点 3つが順に光る。3秒。動かない・攻撃しない＝ひるみ）
///   - 3秒の間にゲージを 10 以上増やすと Stop generating：■ が出て、ボスが 1.5秒ひるむ。大技は出ない
///   - 増やせなかったら「長い答え」：長文の柱が左から右へ順に 5本（幅 3・すき間 1・間 0.45秒。予告は 5本とも先に 1.0秒）。
///     通り過ぎた所・すき間にいれば当たらない。足場の上も当たる。その間、ボスはひるんだまま（ほかの技を出さない）
///   - 考え始めるのは、出してよいとき（ctx.CanSpawnHazard：場に残る技の間・技の途中・形態変化のあとは考えない）
/// ・雑魚の時間：無し（雑魚の出る場所を先に知る入口が無いため。仕様書の「難しければ、雑魚の時間は無し」）
/// ・数値は【仮】。柱の間隔と判定の秒数は、すき間を通れるように仕様書（0.35秒）より少しゆっくりにした
/// </summary>
public class ChatGptRule : StageRule
{
    const float Interval = 18f;
    const float ThinkTime = 3f;
    const int NeedGauge = 10;
    const float StopStun = 1.5f;
    const int Pillars = 5;
    const float PillarWidth = 3f;
    const float PillarGap = 1f;
    const float AnswerLead = 1f;
    const float AnswerStep = 0.45f;
    const float AnswerActive = 0.25f;
    const int FxOrder = 35;

    enum Mode { Idle, Thinking, Stopped, Answering }

    Mode _mode;
    float _timer;
    float _t;
    int _gaugeAt;
    bool _stunnedByUs;
    BossController _boss;
    ChatGptThinkingDots _dots;
    AttackData _data;

    float AnswerTime => AnswerLead + (Pillars - 1) * AnswerStep + AnswerActive;

    public override void Begin(StageRuleContext ctx)
    {
        _data = ZoneStrike.MakeData("Attack_Stage_ChatGPT_LongAnswer", 1, 0, new Vector2(6f, 5f), 0.25f, Color.white);
    }

    public override void OnBossAppeared(BossController boss)
    {
        _boss = boss;
        _timer = 0f;
        _mode = Mode.Idle;
        if (_boss != null) _boss.TransitionStarted += OnTransition;
    }

    void OnTransition() => ResetState();

    public override void OnBossPhase(int phase)
    {
        ResetState();
        // 形態変化の前にかけたひるみが、第2形態の始めに残らないように
        if (_stunnedByUs && _boss != null && KitBoss.IsStunned(_boss)) KitBoss.Stun(_boss, 0.01f);
        _stunnedByUs = false;
        _timer = 0f;
    }

    public override void Tick(float dt)
    {
        if (_boss == null || !Ctx.InBossFight || _boss.Gauge == null) return;
        switch (_mode)
        {
            case Mode.Idle:
                _timer += dt;
                if (_timer >= Interval && Ctx.CanSpawnHazard && KitBoss.IsFighting(_boss) && !KitBoss.IsStunned(_boss)) StartThinking();
                break;
            case Mode.Thinking:
                _t += dt;
                int gained = _boss.Gauge.Current - _gaugeAt;
                if (_dots != null) _dots.SetProgress((float)gained / NeedGauge);
                if (gained >= NeedGauge) StopGenerating();
                else if (_t >= ThinkTime) LongAnswer();
                break;
            case Mode.Stopped:
                _t += dt;
                if (_t >= StopStun) ToIdle();
                break;
            case Mode.Answering:
                _t += dt;
                if (_t >= AnswerTime) ToIdle();
                break;
        }
    }

    // ───────── 考え中 → 止める／長い答え ─────────

    void StartThinking()
    {
        _mode = Mode.Thinking;
        GameAudio.Event("gm_chatgpt_think_start");
        _t = 0f;
        _gaugeAt = _boss.Gauge.Current;
        KitBoss.Stun(_boss, ThinkTime + 0.3f);
        _stunnedByUs = true;
        Bounds body = _boss.BodyBounds;
        _dots = ChatGptThinkingDots.At(new Vector2(body.center.x, body.max.y + 0.8f), 0f, 1.8f, 0.6f);
        _dots.SetProgress(0f);
        Ctx.TrackHazard(_dots.gameObject);
    }

    void StopGenerating()
    {
        _mode = Mode.Stopped;
        GameAudio.Event("gm_chatgpt_stop");
        _t = 0f;
        Vector2 at = _dots != null ? (Vector2)_dots.transform.position : (Vector2)_boss.BodyBounds.center + Vector2.up * 2f;
        ClearDots();
        KitBoss.Stun(_boss, StopStun);
        _stunnedByUs = true;
        // ■ がポンと出る。UI をまねた短い英語だけ
        FxAnim.Sprite(ChatGptArt.StopButton, at, FxOrder, 1.2f, Color.white, 2f, "ChatGPTStop").Pop().FadeFrom(0.75f);
        FxKit.Text("Stop generating", at + Vector2.down * 0.95f, 0.42f, Color.white, FxOrder, 1.2f, ChatGptArt.Dark);
        FxKit.Ring(at, 2.4f, 0.3f, ChatGptArt.Green);
    }

    void LongAnswer()
    {
        _mode = Mode.Answering;
        GameAudio.Event("gm_chatgpt_long_answer");
        _t = 0f;
        ClearDots();
        KitBoss.Stun(_boss, AnswerTime + 0.3f);
        _stunnedByUs = true;
        AttackRunner owner = Ctx.Hazard;
        if (owner == null || _data == null) return;
        Rect arena = Ctx.Arena;
        float total = Pillars * PillarWidth + (Pillars - 1) * PillarGap;
        Rect[] cols = ZoneStrike.Row(arena.center.x - total * 0.5f, PillarWidth, PillarGap, Pillars, arena.yMin, arena.height);
        ZoneStrike.StrikeSweep(owner, _data, cols, 0f, AnswerStep, AnswerLead, AnswerActive, ChatGptArt.AnswerColumn, new Color(1f, 1f, 1f, 0.92f));
    }

    void ToIdle()
    {
        _mode = Mode.Idle;
        _timer = 0f;
        _t = 0f;
        _stunnedByUs = false;
    }

    void ClearDots()
    {
        if (_dots != null) Object.Destroy(_dots.gameObject);
        _dots = null;
    }

    void ResetState()
    {
        ClearDots();
        _mode = Mode.Idle;
        _t = 0f;
    }

    public override void End()
    {
        ResetState();
        if (_boss != null) _boss.TransitionStarted -= OnTransition;
        _boss = null;
    }
}
