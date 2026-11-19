using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Slack のステージのギミック「未読がたまる」（2026-11-19。docs/app-kits-1-design.md 2章）。
/// ・10秒おき（ボス戦は 15秒おき）に、通知の板が1枚、プレイヤーから 3〜6マス離れた床に出る
///   2026-11-19（docs/stage-gimmick-variety-a.md Slack）：上から落とすのをやめて、その場に出る（KitMotion.Pop。赤い点線の枠と「！」0.8秒・
///   ボス戦 1.0秒 → 奥から大きくなって出る。出る 0.15秒だけ当たる）。雑魚の時間は 3回に 1回、右の壁から床の上をすべって来る
///   （GimmickA.Slide。毎秒 5マス・ジャンプで越える。止まった所で未読になる）。ボス戦は出るだけ
/// ・出た板は「未読」として床に残る（KitProp。触っても痛くない）。近接か能力で叩くと「既読」（✓✓ が出て消える）
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

    const float PopWarn = 0.8f, BossPopWarn = 1.0f, PopHit = 0.15f, PopKeep = 0.25f; // 【仮】
    const float SlideSpeed = 5f;
    const int SlideEvery = 3;

    readonly List<KitMotionItem> _drops = new List<KitMotionItem>(); // 出てくる途中の板（Pop・すべる板）
    readonly List<KitProp> _unread = new List<KitProp>();
    float _timer;
    int _dropCount;
    GameObject _bossBadge;
    bool _boosted;

    string WaitKey => Ctx.Key("unread");
    string MeterKey => Ctx.Key("unread.meter");

    public override void Begin(StageRuleContext ctx)
    {
        _timer = ZakoInterval * 0.5f; // 最初の1枚は少し早く
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _timer = 0f;
    }

    public override void OnBossAppeared(BossController boss) => _timer = 0f;

    public override void Tick(float dt)
    {
        _drops.RemoveAll(d => d == null || d.IsDone);
        _unread.RemoveAll(p => p == null || p.IsBroken);

        // 出た板（Pop）を、未読の置物に変える（すべる板は、着いたとき Finished で変える）
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            if (!(_drops[i] is KitMotionPop pop) || !pop.IsOut || pop.Age < pop.Warn + PopKeep) continue;
            Vector2 at = pop.transform.position;
            pop.Cancel();
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
        if (Ctx.Player == null) return false;
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
        float floorY = TalkKitUtil.FloorY(x, area.yMin + 2.4f);
        Vector2 at = new Vector2(x, floorY + BoardSize.y * 0.5f);
        _dropCount++;
        KitOwner owner = Ctx;
        if (!Ctx.InBossFight && _dropCount % SlideEvery == 0)
        {
            // 3回に 1回：右の壁から床の上をすべって来る（本物の通知が右から出るのに合わせる）。止まった所で未読
            KitMotionMover m = GimmickA.Slide(owner, KitSide.Right, at.y, x, BoardSize, SlideSpeed, PopWarn, SlackArt.Board, null, 1);
            if (m == null) return false;
            m.Finished += it => { if (it != null) PlaceUnread(it.transform.position); };
            _drops.Add(m);
            return true;
        }
        float warn = Ctx.InBossFight ? BossPopWarn : PopWarn;
        KitMotionPop pop = KitMotion.Pop(owner, at, warn, 1f, 0, SlackArt.Board, null, 1, 1.2f, 6f, false, PopHit, BoardSize);
        if (pop == null) return false;
        // 枠の上に赤い「！」（予告の間だけ）
        Vector2 bang = TalkArt.Bang != null ? (Vector2)TalkArt.Bang.bounds.size * 1.5f : new Vector2(0.3f, 0.6f);
        KitMotionMark.Create(pop.transform, TalkArt.Bang, at + Vector2.up * (BoardSize.y * 0.5f + 0.45f), bang, 0f, Color.white, KitMotionItem.OrderMark, pop.Warn, "Bang");
        _drops.Add(pop);
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

    /// <summary>【仮】2026-11-19：未読の板を1枚、既読にする（出来事「スレッドが伸びる」の ✓ のごほうび）。未読が無い・Slack のステージでなければ false</summary>
    public static bool ReadOne()
    {
        if (!(StageRules.Current is SlackRule rule)) return false;
        rule._unread.RemoveAll(p => p == null || p.IsBroken);
        if (rule._unread.Count == 0) return false;
        rule._unread[0].Break(); // Broken → OnRead（✓✓ が出て消える）
        return true;
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
