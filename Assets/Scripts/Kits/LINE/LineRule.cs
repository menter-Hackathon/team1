using UnityEngine;

/// <summary>
/// 【仮】LINE のステージのギミック「既読スルー」（2026-11-19。docs/app-kits-1-design.md 4章）。
/// ・12秒おき（ボス戦は 16秒おき）に、緑の吹き出し（メッセージ。LineMessage）が、プレイヤーから 4〜7マスの床の上に「ポン」と出る
/// ・5秒以内に近接で叩く＝返信：✓ が出て消える。ごほうび：全部の能力のクールタイムが 1秒進む（KitPlayer）
/// ・5秒たつと、吹き出しの左下に小さい字「既読」【要相談 1：日本語のまま】→ 1.5秒 赤く点滅（予告）→ 直径 4 の爆発
/// ・逃げてもよい（離れれば当たらない）。同時に場にあるのは 1つまで。③ 出来事の間は出さない
/// </summary>
public class LineRule : StageRule
{
    const float ZakoInterval = 12f;
    const float BossInterval = 16f;
    const float ReadDelay = 5f;
    const float Blink = 1.5f;
    const float Diameter = 4f;
    const float MinDistance = 4f, MaxDistance = 7f;
    const float Reward = 1f;

    float _timer;
    LineMessage _current;
    AttackData _boomData;

    public override void Begin(StageRuleContext ctx)
    {
        _boomData = TalkKitUtil.RuntimeData("Attack_LINE_ReadIgnored",
            "{\"_kind\":1,\"_damage\":1,\"_knockback\":{\"x\":6,\"y\":5},\"_hitStun\":0.25}");
        _timer = ZakoInterval * 0.5f;
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) { _timer = 0f; _current = null; }
    }

    public override void OnBossAppeared(BossController boss) => _timer = 0f;

    public override void OnBossPhase(int phase) => _current = null;

    public override void Tick(float dt)
    {
        if (_current != null && (_current.IsDone || _current == null)) _current = null;
        if (_current != null) return;
        if (!Ctx.InBossFight && (Ctx.Phase == PacePhase.Event || Ctx.Phase == PacePhase.Rest)) return;
        _timer += dt;
        float interval = Ctx.InBossFight ? BossInterval : ZakoInterval;
        if (_timer < interval || !Ctx.CanPlace) return;
        if (Spawn()) _timer = 0f;
    }

    bool Spawn()
    {
        AttackRunner runner = Ctx.Hazard;
        if (runner == null || Ctx.Player == null || _boomData == null) return false;
        Rect area = Ctx.PlayArea;
        float px = Ctx.Player.position.x;
        float margin = LineMessage.Size.x * 0.5f + 0.5f;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = px + side * Random.Range(MinDistance, MaxDistance);
            if (x < area.xMin + margin || x > area.xMax - margin) continue;
            float y = TalkKitUtil.FloorY(x, area.yMin + 2.4f);
            LineMessage m = LineMessage.Spawn(runner, _boomData, new Vector2(x, y), ReadDelay, Blink, Diameter, OnDone);
            if (m == null) return false;
            Ctx.TrackPlaced(m.gameObject);
            _current = m;
            GameAudio.Event("gm_line_message");
            return true;
        }
        return false;
    }

    void OnDone(LineMessage m, bool replied)
    {
        if (m == _current) _current = null;
        if (replied)
        {
            KitPlayer.AdvanceCooldowns(Reward);
            GameAudio.Event("gm_line_reply");
        }
        else GameAudio.Event("gm_line_boom"); // 返信されなかった＝爆発した
    }

    public override void End() => _current = null;
}
