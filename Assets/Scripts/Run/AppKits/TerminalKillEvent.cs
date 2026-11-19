using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Terminal の場面③の出来事に足す「kill -9」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Terminal）。
/// TerminalKit が Make で登録する：kill -9（この出来事）と sudo reboot（今の床の全部。SweepEvent）を同時に進める（ComboEvent。通知は reboot）。
/// ・出来事の 2秒目と 5.6秒目【仮】に 1回ずつ：コマンドの行が kill -9 * に変わる → 床の 5区画（TerminalKit の rm と同じ分け方）のうち
///   1区画（強さ 1 は 2区画）に、明るい緑の枠と [PID] の札＝安全（KitMotion.SafeSpot）→ 予告のあと、ほかの区画の床から、
///   KILLED の字が縦に並ぶ緑の柱がせり上がる（KitMotion.Erupt。せり上がり 0.2・止まる 0.5・高さ 2）
/// ・安全の予告 1.6（強さ 3 は 1.3）。柱の赤い予告は、その後半（0.8／0.65）。安全の区画は、プレイヤーから 2区画以内に必ず 1つ
/// ・落ちない足場（LS・CAT）の真下の区画は、柱を低く（1.5）して、足場の上を必ず安全にする（乗ると落ちる足場だけが逃げ場にならない）
/// ・sudo reboot の本体（床の全部・予告 2.5秒）は、後半に 1回（kill -9 の柱が引っこんでから始まる。判定どうしは 3秒以上あく）
/// 変えた所：2回目の kill -9 は 6秒目 → 5.6秒目、sudo reboot は 2回 → 1回（12秒の中に収めるため）
/// rm（床が 1区画消える）は、今のまま ③ の間と、そのあと 2秒は出ない（TerminalKit）
/// </summary>
public class TerminalKillEvent : KitTimedEvent
{
    const int Sections = 5;
    const float FirstKill = 1.2f, SecondKill = 4.8f;   // 表の時計（出来事の 2秒目・5.6秒目）
    const float RebootAt = 7.5f;                        // sudo reboot の予告を始める時刻（表の時計）
    const float Rise = 0.2f, Hold = 0.5f, Height = 2f, LowHeight = 1.5f, Stay = 0.9f;

    public TerminalKillEvent() : base(null) { }

    /// <summary>kill -9 ＋ sudo reboot（1回）。攻撃データ・AttackRunner のプレハブが無ければ null（スキンの出来事のまま）</summary>
    public static StageEvent Make()
    {
        PaceAssets a = PaceAssets.Load();
        AttackData data = a != null ? (a.FloorReboot != null ? a.FloorReboot : a.Floor) : null;
        if (data == null || PaceAssets.RunnerPrefab() == null) return null;
        var reboot = new SweepEvent(SweepEvent.Pattern.WholeFloor, data, 2f, 1, 1, 1) { StartDelay = PaceEvent.LeadTime + RebootAt };
        return new ComboEvent("reboot", new TerminalKillEvent(), reboot);
    }

    protected override void Build()
    {
        At(FirstKill, Kill);
        At(SecondKill, Kill);
        At(RebootAt - 0.1f, () => TerminalKit.Command("sudo reboot"));
        EndAt(RebootAt + 0.2f);
    }

    void Kill()
    {
        TerminalKit.Command("kill -9 *");
        Rect a = View;
        float floor = Floor;
        float w = a.width / Sections;
        int player = Mathf.Clamp(Mathf.FloorToInt((PlayerPos.x - a.xMin) / w), 0, Sections - 1);
        // 安全の区画：プレイヤーから 2区画以内に 1つ（強さ 1 は、もう 1つどこでも）
        var safe = new List<int> { Mathf.Clamp(player + Random.Range(-2, 3), 0, Sections - 1) };
        if (Level <= 1)
        {
            int other = Random.Range(0, Sections - 1);
            if (other >= safe[0]) other++;
            safe.Add(other);
        }
        float warn = Level >= 3 ? 1.3f : 1.6f;
        float eruptWarn = Mathf.Max(KitMotion.MinWarn, warn * 0.5f);
        foreach (int s in safe)
        {
            float cx = a.xMin + w * (s + 0.5f);
            KitMotionSafe spot = KitMotion.SafeSpot(Owner, cx, w, warn, _ => { }, TerminalArt.Green, 1, Height, Stay, null, null, floor);
            if (spot != null)
                KitMotionMark.Create(spot.transform, PidSprite, new Vector2(cx, floor + 2.6f), PidSprite.bounds.size * 2f, 0f, Color.white,
                    KitMotionItem.OrderMark, warn + Stay, "Pid");
        }
        List<Bounds> steady = GimmickA.SteadyPlatforms(a);
        At(Clock + warn - eruptWarn, () =>
        {
            for (int i = 0; i < Sections; i++)
            {
                if (safe.Contains(i)) continue;
                float cx = a.xMin + w * (i + 0.5f);
                float h = UnderSteady(steady, cx, w, floor) ? LowHeight : Height;
                KitMotionErupt e = KitMotion.Erupt(Owner, cx, w, h, eruptWarn, Rise, Hold, null, TerminalArt.DarkGreen, 1, floor);
                Letter(e, w, h);
            }
            GameAudio.Event("gm_terminal_rm_floor");
        });
    }

    /// <summary>落ちない足場（下の段）の真下の区画か</summary>
    static bool UnderSteady(List<Bounds> steady, float cx, float w, float floor)
    {
        foreach (Bounds b in steady)
            if (b.max.x > cx - w * 0.5f && b.min.x < cx + w * 0.5f && b.max.y - floor < 4.5f) return true;
        return false;
    }

    /// <summary>柱に KILLED の字の列を並べる（床の下はかくれる：Erupt のマスクに合わせる）</summary>
    static void Letter(KitMotionErupt e, float w, float h)
    {
        if (e == null || e.Body == null) return;
        Sprite col = KilledColumn;
        Vector2 native = col.bounds.size;
        float scale = Mathf.Min(1f, h * 0.9f / Mathf.Max(0.01f, native.y));
        int n = Mathf.Max(1, Mathf.FloorToInt(w / 0.75f));
        for (int k = 0; k < n; k++)
        {
            float x = -w * 0.5f + w * (k + 0.5f) / n;
            SpriteRenderer sr = RuntimeSprites.Show(e.Body, col, new Vector2(x, 0f), KitMotionItem.OrderBody + 1, scale, "Killed");
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
        if (e.Visual != null) e.Visual.color = TerminalArt.DarkGreen;
    }

    /// <summary>KILLED の字が縦に並ぶ（5×35。緑）</summary>
    static Sprite KilledColumn
    {
        get
        {
            char[,] g = GimmickA.Grid(5, 35);
            GimmickA.Stamp(g, "KILLED", 1, 0, 'G', true);
            var pal = new Dictionary<char, Color32> { { 'G', (Color32)TerminalArt.Green } };
            return RuntimeSprites.FromAscii("TerminalKilledColumn", GimmickA.Rows(g), pal);
        }
    }

    /// <summary>[PID] の札（19×7。黒い地に緑の字）</summary>
    static Sprite PidSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(19, 7);
            GimmickA.Fill(g, 0, 0, 19, 7, 'K');
            GimmickA.Stamp(g, "[PID]", 1, 1, 'G');
            var pal = new Dictionary<char, Color32> { { 'K', (Color32)TerminalArt.Black }, { 'G', (Color32)TerminalArt.Green } };
            return RuntimeSprites.FromAscii("TerminalPidTag", GimmickA.Rows(g), pal);
        }
    }
}
