using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Slack のステージのギミック「未読がたまる」（2026-11-19。docs/app-kits-1-design.md 2章）。
/// ・10秒おき（ボス戦は 15秒おき）に、通知の板が1枚、プレイヤーから 3〜6マス離れた床に落ちる（影の予告 0.8秒。落ちてくる間は当たる）
/// ・落ちた板は「未読」として床に残る（KitProp。触っても痛くない）。近接か能力で叩くと「既読」（✓✓ が出て消える）
/// ・HUD のライフの下に Slack のアイコンと赤いメーター（未読の数 ÷ 3。3 で点滅）
/// ・未読が 3 になると
///   雑魚の時間：@channel。床の全部に予告 2.5秒 → 低い波（Attack_Pace_RedWave_Reboot）。足場の上は安全。未読は全部消える
///   ボス戦：ボスの待ち時間 ×0.7（ボスの右上に赤い 99+ のバッジが震える）。未読を 2 以下にすると戻る。危ない物は出さない
/// ・未読の数は、毎フレーム場に残っている板から数え直す（ボスの登場・50％で板が消されても、倍率が残らない）
/// </summary>
public class SlackRule : StageRule
{
    const float ZakoInterval = 10f;
    const float BossInterval = 15f;
    const int Limit = 3;
    const float MinDistance = 3f, MaxDistance = 6f;
    static readonly Vector2 BoardSize = new Vector2(2f, 1.3f);

    readonly List<FallingObject> _drops = new List<FallingObject>();
    readonly List<KitProp> _unread = new List<KitProp>();
    float _timer;
    AttackData _dropData;
    GameObject _bossBadge;
    bool _boosted;

    string WaitKey => Ctx.Key("unread");
    string MeterKey => Ctx.Key("unread.meter");

    public override void Begin(StageRuleContext ctx)
    {
        _dropData = TalkKitUtil.RuntimeData("Attack_Slack_UnreadDrop",
            "{\"_kind\":2,\"_damage\":1,\"_knockback\":{\"x\":6,\"y\":5},\"_hitStun\":0.25,\"_objectSize\":{\"x\":2,\"y\":1.3}," +
            "\"_fallSpeed\":12,\"_landRemainTime\":0.6,\"_telegraphTime\":0.8,\"_spawnCount\":1}");
        _timer = ZakoInterval * 0.5f; // 最初の1枚は少し早く
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _timer = 0f;
    }

    public override void OnBossAppeared(BossController boss) => _timer = 0f;

    public override void Tick(float dt)
    {
        _drops.RemoveAll(d => d == null);
        _unread.RemoveAll(p => p == null || p.IsBroken);

        // 落ちた板を、未読の置物に変える
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            FallingObject f = _drops[i];
            if (!f.HasLanded) continue;
            Vector2 at = f.transform.position;
            Object.Destroy(f.gameObject);
            _drops.RemoveAt(i);
            PlaceUnread(at);
        }

        int unread = _unread.Count;
        KitMeter.Set(MeterKey, unread / (float)Limit, (Color)SlackArt.Badge, SlackArt.Logo, unread >= Limit);

        if (Ctx.InBossFight) UpdateBoss(unread >= Limit);
        else if (unread >= Limit && Ctx.CanSpawnHazard) AtChannel();

        // 板を落とす（③ 出来事の間は落とさない。出せない間は時計を止めずに待つ）
        if (!Ctx.InBossFight && Ctx.Phase == PacePhase.Event) return;
        _timer += dt;
        float interval = Ctx.InBossFight ? BossInterval : ZakoInterval;
        if (_timer < interval) return;
        if (!Ctx.CanSpawnHazard || !Ctx.CanPlace || _drops.Count + _unread.Count >= StageRuleHost.MaxPlaced) return;
        if (Drop()) _timer = 0f;
    }

    // ───────── 板を落とす ─────────

    bool Drop()
    {
        AttackRunner runner = Ctx.Hazard;
        if (runner == null || Ctx.Player == null || _dropData == null) return false;
        Rect area = Ctx.PlayArea;
        float px = Ctx.Player.position.x;
        float margin = BoardSize.x * 0.5f + 0.4f;
        float x = px;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float cand = px + side * Random.Range(MinDistance, MaxDistance);
            if (cand < area.xMin + margin || cand > area.xMax - margin) cand = px - side * Random.Range(MinDistance, MaxDistance);
            if (cand < area.xMin + margin || cand > area.xMax - margin) continue;
            x = cand;
            break;
        }
        if (Mathf.Abs(x - px) < MinDistance - 0.01f) return false;
        float now = Time.time;
        float fallAt = now + Mathf.Max(0.8f, _dropData.TelegraphTime);
        Vector2 hang = new Vector2(x, area.yMax + BoardSize.y * 0.5f);
        FallingObject f = FallingObject.Spawn(runner, _dropData, hang, fallAt, fallAt, true, false, 0);
        if (f == null) return false;
        KitDress.Swap(f.gameObject, SlackArt.Board);
        // 影の上に赤い「！」（落ちてくる間は当たる）
        Transform shadow = f.transform.Find("Shadow");
        if (shadow != null) TalkArt.ShowNative(shadow, TalkArt.Bang, new Vector2(0f, 0.55f), 18, 1.5f, "Bang").gameObject.AddComponent<SlackBlink>();
        _drops.Add(f);
        return true;
    }

    void PlaceUnread(Vector2 at)
    {
        KitProp prop = KitProp.Spawn(at, SlackArt.Board, BoardSize, OnRead);
        Ctx.TrackPlaced(prop.gameObject);
        _unread.Add(prop);
        GameAudio.Event("gm_slack_unread_add", 1f + 0.12f * (_unread.Count - 1));
    }

    /// <summary>叩かれた＝既読。✓✓ が出て、灰色になって消える</summary>
    void OnRead(KitProp prop)
    {
        if (prop == null) return;
        Vector2 at = prop.transform.position;
        GameAudio.Event("gm_slack_read");
        FxAnim a = FxAnim.Create("Slack_Read", at, 0.5f);
        TalkArt.Show(a.transform, SlackArt.BoardRead, Vector2.zero, BoardSize, Color.white, 16, "Board");
        TalkArt.ShowNative(a.transform, TalkArt.Check, new Vector2(-0.12f, 0.95f), 46, 2f, "CheckA").color = (Color)SlackArt.Green;
        TalkArt.ShowNative(a.transform, TalkArt.Check, new Vector2(0.22f, 0.95f), 46, 2f, "CheckB").color = (Color)SlackArt.Green;
        a.FadeFrom(0.3f).Refresh();
    }

    // ───────── 未読が 3 ─────────

    /// <summary>雑魚の時間：@channel。床の全部に長い予告 → 低い波。未読は全部消える</summary>
    void AtChannel()
    {
        AttackRunner runner = Ctx.Hazard;
        PaceAssets assets = PaceAssets.Load();
        AttackData wave = assets != null ? (assets.FloorReboot != null ? assets.FloorReboot : assets.Floor) : null;
        foreach (var p in _unread) if (p != null) Object.Destroy(p.gameObject);
        _unread.Clear();
        KitToast.Show("@channel", SlackArt.Logo, (Color)SlackArt.Purple);
        if (runner == null || wave == null) return;
        GameAudio.Event("gm_slack_channel");

        Rect area = Ctx.PlayArea;
        float floorY = TalkKitUtil.FloorY(area.center.x, area.yMin + 2.4f);
        float xMin = area.xMin, xMax = area.xMax;
        // 足場が無い画面は、逃げ場が無いので、プレイヤーのいる側の半分だけ（SweepEvent と同じ考え）
        if (TalkKitUtil.Platforms(area).Count == 0 && Ctx.Player != null)
        {
            if (Ctx.Player.position.x >= area.center.x) xMin = area.center.x;
            else xMax = area.center.x;
        }
        float unit = Mathf.Max(0.5f, wave.AreaSize.x);
        int n = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) / unit - 0.01f));
        for (int i = 0; i < n; i++)
        {
            float cx = n == 1 ? (xMin + xMax) * 0.5f : Mathf.Lerp(xMin + unit * 0.5f, xMax - unit * 0.5f, i / (float)(n - 1));
            AreaStrike.Spawn(runner, wave, new Vector2(cx, floorY + 0.05f), Vector2.up, 0f, i);
        }
        PaceZoneMark.Show(runner, xMin, xMax, floorY, wave.AreaSize.y, wave.TelegraphTime, wave.HitWindow);
    }

    /// <summary>ボス戦：未読が 3 以上の間だけ、ボスの待ち時間 ×0.7（バッジ 99+）</summary>
    void UpdateBoss(bool over)
    {
        if (over == _boosted)
        {
            if (over && _bossBadge == null) ShowBadge();
            return;
        }
        _boosted = over;
        if (over)
        {
            StatMods.Boss.Wait.Set(WaitKey, 0.7f, SlackArt.Logo);
            GameAudio.Event("gm_slack_boss_boost");
            ShowBadge();
        }
        else
        {
            StatMods.Boss.Wait.Clear(WaitKey);
            GameAudio.Event("gm_slack_boss_calm");
            if (_bossBadge != null) Object.Destroy(_bossBadge);
            _bossBadge = null;
        }
    }

    void ShowBadge()
    {
        if (Ctx.Boss == null) return;
        _bossBadge = Ctx.TrackDecor(SlackBossBadge.Create(Ctx.Boss).gameObject);
    }

    public override void End()
    {
        StatMods.Boss.Wait.Clear(WaitKey);
        KitMeter.Clear(MeterKey);
        _boosted = false;
        if (_bossBadge != null) Object.Destroy(_bossBadge);
        _bossBadge = null;
        _drops.Clear();
        _unread.Clear();
    }
}
