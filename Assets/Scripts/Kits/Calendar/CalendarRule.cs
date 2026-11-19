using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのステージのギミック「予定の締め切り」（2026-11-19。docs/app-kits-1-design.md 9章）。StageRule。
/// ・画面の上に、細い時間の帯（1日の予定表）と、右へ進む赤い「今」の線。予定のブロックが 25秒に 1つ来る（OnGUI。CalendarStripGui）
/// ・予定の 8秒前：右上に通知（in 10 min）。足場の1つが、予定の色の枠で光る（会議室）
/// ・予定の時刻に、その足場の上にいる＝出席：✓ と小さい紙吹雪。ごほうび：クールタイムを全部回復（E5）
/// ・いない＝欠席：右上に Missed。罰はライフではなく、相手の強化を 8秒（雑魚の時間は雑魚の速さ ×1.2、ボス戦はボスの待ち時間 ×0.8。E2）
/// ・締め切りの線（ボスの技）が出ている間・③ 出来事（2026-11-19：ダブルブッキング）・④ ひと休み・ボスの登場・形態変化の間は、時間の帯が止まる
/// ・2026-11-19：出来事の「空き時間」に立ちきると、次の欠席の罰を 1回なかったことにする（Excuse）
/// ・足場が無い画面では、床の一か所（幅 3）を会議室にする
/// </summary>
public class CalendarRule : StageRule
{
    // 【仮】数値
    const float Cycle = 25f, Warn = 8f, PenaltySeconds = 8f;
    const float ZakoSpeed = 1.2f, BossWait = 0.8f;
    const float JumpAllowance = 2.5f; // 会議室の上で跳んでいても出席
    const int FrameOrder = 15;

    float _clock;
    bool _warned;
    bool _excused; // 2026-11-19：次の欠席の罰を 1回なかったことにする（出来事の空き時間）
    int _color;
    bool _hasRoom;
    Bounds _room;
    GameObject _roomDecor;
    SpriteRenderer _roomFrame;
    CalendarStripGui _strip;
    Rect _scannedArea;
    readonly List<Bounds> _platforms = new List<Bounds>();

    public override void Begin(StageRuleContext ctx)
    {
        _clock = 0f;
        _color = Random.Range(0, CalendarArt.EventColors.Length);
        Scan();
        var go = ctx.TrackDecor(new GameObject("Calendar_Strip"));
        _strip = go.AddComponent<CalendarStripGui>();
        _strip.EventColor = CalendarArt.EventColor(_color);
    }

    public override void OnBossAppeared(BossController boss)
    {
        Scan();
        ClearRoom();
        _clock = Mathf.Min(_clock, Cycle - Warn - 2f);
        _warned = false;
    }

    public override void Tick(float dt)
    {
        Rect area = Ctx.PlayArea;
        if (Mathf.Abs(area.center.x - _scannedArea.center.x) > 2f || Mathf.Abs(area.center.y - _scannedArea.center.y) > 2f)
        {
            Scan();
            if (_warned) PickRoom(); // 画面が変わったら、会議室を選び直す
        }

        bool paused = CalendarDeadlineEffect.AnyActive || (!Ctx.InBossFight && Ctx.Phase == PacePhase.Event) || Ctx.Phase == PacePhase.Rest || Ctx.BossTransitioning;
        if (!paused) _clock += dt;
        if (_strip != null)
        {
            _strip.Progress = Mathf.Clamp01(_clock / Cycle);
            _strip.Paused = paused;
            _strip.Soon = _warned;
            _strip.EventColor = CalendarArt.EventColor(_color);
        }

        if (!_warned && _clock >= Cycle - Warn)
        {
            _warned = true;
            PickRoom();
            KitToast.Show("in 10 min", null, CalendarArt.EventColor(_color));
            GameAudio.Event("gm_calendar_soon");
        }
        UpdateRoomDecor();

        if (_clock >= Cycle)
        {
            Resolve();
            _clock = 0f;
            _warned = false;
            _color++;
            ClearRoom();
        }
    }

    public override void End()
    {
        StatMods.ClearPrefix(Ctx.Key(""));
        ClearRoom();
        if (_strip != null) Object.Destroy(_strip.gameObject);
        _strip = null;
    }

    // ───────── 会議室 ─────────

    void Scan()
    {
        _scannedArea = Ctx.PlayArea;
        _platforms.Clear();
        _platforms.AddRange(CallKitUtil.FindPlatforms(_scannedArea));
    }

    void PickRoom()
    {
        ClearRoom();
        Rect area = Ctx.PlayArea;
        CallKitUtil.TryPlayerBounds(out Bounds p);
        if (_platforms.Count > 0)
        {
            // プレイヤーから遠い方を少し選びやすく（動いて間に合う）
            var list = new List<Bounds>(_platforms);
            // 2026-11-19：アプリの UI の足場のうち、消えている・会議までに消える・落ちる・動く足場は選ばない（全部だめなら今までどおり）
            List<Bounds> steady = list.FindAll(b => AppStagePlatforms.IsSolidFor(b, Warn + 1f));
            if (steady.Count > 0) list = steady;
            list.Sort((a, b) => Mathf.Abs(b.center.x - p.center.x).CompareTo(Mathf.Abs(a.center.x - p.center.x)));
            _room = list[Random.value < 0.65f || list.Count == 1 ? 0 : Random.Range(1, list.Count)];
        }
        else
        {
            // 足場が無い：床の一か所（プレイヤーから 4〜7マス）
            float side = p.center.x <= area.center.x ? 1f : -1f;
            float x = Mathf.Clamp(p.center.x + side * Random.Range(4f, 7f), area.xMin + 2f, area.xMax - 2f);
            float floor = CallKitUtil.FloorY(area, x);
            _room = new Bounds(new Vector3(x, floor - 0.1f, 0f), new Vector3(3f, 0.2f, 0f));
        }
        _hasRoom = true;

        _roomDecor = Ctx.TrackDecor(new GameObject("Calendar_MeetingRoom"));
        _roomDecor.transform.position = new Vector3(_room.center.x, _room.max.y, 0f);
        Vector2 size = new Vector2(_room.size.x + 0.3f, 2.2f);
        int wp = Mathf.Clamp(Mathf.RoundToInt(size.x * CallArt.Ppu), 4, 200);
        int hp = Mathf.Clamp(Mathf.RoundToInt(size.y * CallArt.Ppu), 4, 80);
        _roomFrame = CalendarArt.Put(_roomDecor.transform, CallArt.Frame(wp, hp, 2), new Vector2(0f, size.y * 0.5f), size, CalendarArt.EventColor(_color), FrameOrder, "Frame");
        CalendarArt.Put(_roomDecor.transform, CalendarArt.Block(_color), new Vector2(0f, size.y + 0.45f), new Vector2(1.5f, 0.9f), Color.white, FrameOrder + 1, "Block");
    }

    void ClearRoom()
    {
        _hasRoom = false;
        if (_roomDecor != null) Object.Destroy(_roomDecor);
        _roomDecor = null;
        _roomFrame = null;
    }

    void UpdateRoomDecor()
    {
        if (_roomFrame == null) return;
        float left = Cycle - _clock;
        float speed = left < 2f ? 14f : 5f;
        _roomFrame.color = CallKitUtil.Alpha(CalendarArt.EventColor(_color), 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * speed)));
    }

    bool InRoom()
    {
        if (!_hasRoom || !CallKitUtil.TryPlayerBounds(out Bounds p)) return false;
        float x = p.center.x;
        if (x < _room.min.x - 0.15f || x > _room.max.x + 0.15f) return false;
        float feet = p.min.y;
        return feet >= _room.max.y - 0.3f && feet <= _room.max.y + JumpAllowance;
    }

    /// <summary>
    /// 【仮】2026-11-19：次の予定の「欠席」の罰を 1回なかったことにする（出来事「ダブルブッキング」の空き時間のごほうび）。
    /// カレンダーのステージでない・もう取ってあれば false（呼ぶ側がほかのごほうびにする）
    /// </summary>
    public static bool Excuse()
    {
        if (!(StageRules.Current is CalendarRule rule) || rule._excused) return false;
        rule._excused = true;
        if (CallKitUtil.TryPlayerBounds(out Bounds p))
            FxAnim.Sprite(CalendarArt.Check, new Vector2(p.center.x, p.max.y + 0.6f), 60, 0.7f, Color.white, 2f, "Calendar_Excuse").Pop().Rise(0.5f);
        return true;
    }

    // ───────── 予定の時刻 ─────────

    void Resolve()
    {
        if (!_hasRoom) return;
        Color c = CalendarArt.EventColor(_color);
        if (InRoom())
        {
            KitPlayer.RefillCooldowns();
            GameAudio.Event("gm_calendar_attend");
            if (CallKitUtil.TryPlayerBounds(out Bounds p))
            {
                Vector2 head = new Vector2(p.center.x, p.max.y + 0.6f);
                FxAnim.Sprite(CalendarArt.Check, head, 60, 0.7f, Color.white, 2f, "Calendar_Attend").Pop().Rise(0.5f);
                FxKit.Fragments(head, Vector2.up, 10, null, new[] { c, CalendarArt.Red, Color.white }, 1f);
            }
            return;
        }
        if (_excused)
        {
            // 出来事の空き時間で、予定をずらしておいた：罰なし
            _excused = false;
            KitToast.Show("Rescheduled", null, c);
            GameAudio.Event("gm_calendar_attend");
            return;
        }
        KitToast.Show("Missed", null, CalendarArt.Red);
        GameAudio.Event("gm_calendar_missed");
        if (Ctx.InBossFight) StatMods.Boss.Wait.SetFor(Ctx.Key("missed"), BossWait, PenaltySeconds, Ctx.Icon);
        else StatMods.Zako.Speed.SetFor(Ctx.Key("missed"), ZakoSpeed, PenaltySeconds, Ctx.Icon);
    }
}

/// <summary>
/// 画面の上の、細い時間の帯（OnGUI。文字なし）：暗い帯に時刻の目盛り、右に予定のブロック、左から右へ進む赤い「今」の線。
/// 予定の 8秒前からブロックが点滅。止まっている間は、今の線が灰色
/// </summary>
public class CalendarStripGui : MonoBehaviour
{
    public float Progress;
    public bool Paused;
    public bool Soon;
    public Color EventColor = CalendarArt.Blue;

    const float W = 200f, H = 16f, Top = 62f;
    const float NowFrom = 0.03f, EventAt = 0.86f;

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 11;
        PixelGui.Begin(false);
        float x = Mathf.Round((PixelGui.Width - W) * 0.5f);
        var box = new Rect(x, Top, W, H);
        PixelGui.Fill(box, MacUi.Gray3);
        var inner = new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f);
        PixelGui.Fill(inner, CallKitUtil.Alpha(MacUi.Gray6, 0.92f));
        // 時刻の目盛り
        for (int i = 1; i < 10; i++)
        {
            float tx = Mathf.Round(inner.x + inner.width * i / 10f);
            PixelGui.Fill(new Rect(tx, inner.y + inner.height - 4f, 1f, 4f), MacUi.Gray2);
        }
        // 予定のブロック
        bool blink = Soon && Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0;
        var ev = new Rect(Mathf.Round(inner.x + inner.width * EventAt), inner.y + 2f, Mathf.Round(inner.width * 0.1f), inner.height - 4f);
        PixelGui.Fill(ev, blink ? Color.Lerp(EventColor, Color.white, 0.5f) : EventColor);
        PixelGui.Fill(new Rect(ev.x, ev.y, 2f, ev.height), Color.Lerp(EventColor, Color.black, 0.35f));
        PixelGui.Fill(new Rect(ev.x + 4f, ev.y + 3f, Mathf.Max(2f, ev.width - 8f), 2f), Color.white);
        // 今の線（左の端から予定のブロックまで）
        float nx = Mathf.Round(inner.x + inner.width * Mathf.Lerp(NowFrom, EventAt, Mathf.Clamp01(Progress)));
        Color now = Paused ? MacUi.Gray1 : CalendarArt.Red;
        PixelGui.Fill(new Rect(nx, box.y - 2f, 2f, box.height + 4f), now);
        PixelGui.Fill(new Rect(nx - 2f, box.y - 4f, 6f, 4f), now);
        PixelGui.End();
    }
}
