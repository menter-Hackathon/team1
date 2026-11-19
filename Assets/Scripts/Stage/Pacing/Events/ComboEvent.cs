using System;
using UnityEngine;

// 【仮】出来事を重ねる（2026-11-19。docs/stage-pacing-design.md 3-3「3ステージ目は、表の型に群れを1回重ねる」）。担当2 の物。
// 始まり（赤いふち・通知）と終わり（白い光）は、ここで1回だけ出す。中の出来事は Announce＝false。
// 雑魚を出す出来事（群れ）が1つでも入っていれば SpawnsOwnZako＝true。渡された数（zakoBudget）は、その群れに全部渡す。
// 中の出来事は、それぞれの終わり方で終わる。全部終わったら、この出来事も終わる。

/// <summary>2つの出来事を同時に進める（表の型＋重ねる群れ／雨）</summary>
public class ComboEvent : StageEvent
{
    readonly PaceEvent[] _parts;
    readonly string _toast;
    StagePacer _pacer;
    bool _begun, _ended;

    public ComboEvent(string toast, params PaceEvent[] parts)
    {
        _toast = toast;
        _parts = parts ?? Array.Empty<PaceEvent>();
        foreach (var p in _parts) if (p != null) p.Announce = false;
    }

    public override bool SpawnsOwnZako
    {
        get
        {
            foreach (var p in _parts) if (p != null && p.SpawnsOwnZako) return true;
            return false;
        }
    }

    public override void Begin(StagePacer pacer, AttackRunner hazardRunner, int strength, int zakoBudget)
    {
        if (_begun) return;
        _begun = true;
        _pacer = pacer;
        if (pacer != null) pacer.PhaseChanged += OnPhaseChanged;
        EventShow.Intro(pacer, _toast);
        bool budgetGiven = false;
        foreach (var p in _parts)
        {
            if (p == null) continue;
            int budget = 0;
            if (p.SpawnsOwnZako && !budgetGiven)
            {
                budget = zakoBudget;
                budgetGiven = true;
            }
            p.Begin(pacer, hazardRunner, strength, budget);
        }
    }

    public override bool Tick(float dt)
    {
        if (_ended) return false;
        bool any = false;
        foreach (var p in _parts)
        {
            if (p == null || p.IsEnded) continue;
            if (p.Tick(dt)) any = true;
            else p.End();
        }
        return any;
    }

    public override void End() => Finish(false);

    void OnPhaseChanged(PacePhase phase)
    {
        if (phase == PacePhase.Done) Finish(true);
    }

    void Finish(bool byBoss)
    {
        if (_ended) return;
        _ended = true;
        if (_pacer != null) _pacer.PhaseChanged -= OnPhaseChanged;
        foreach (var p in _parts) if (p != null) p.End();
        if (_begun && !byBoss) EventShow.Outro();
    }
}
