using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro の場面③の出来事「書き出し」に足す「波形」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Premiere Pro）。
/// PremiereKit.MakeEvent が、書き出し（PremiereRenderEvent）と同時に進める（ComboEvent。通知は Rendering... の 1回）。
/// ・書き出しの進みが 30％（3.6秒）と 70％（8.4秒）の所で、左の壁から右へ、音声トラックの波形の山が床を走る（KitMotion.FloorWave。
///   高さ 0.9・毎秒 6マス・予告 0.8。ジャンプで越える）。強さ 3 は 70％の方を左右から 1つずつ（まん中で出会うので、足場へ上がる）
/// ・進みが 50％（6秒）の時に 1回、床のまん中の幅 6マスが 3秒、左へ流れる（KitMotion.Conveyor。毎秒 2マス。予告 0.8：緑の矢印）
/// ・タイムラインのマーカー（PremiereTimelineStage）との公平さ：流れている間はその幅にマーカーを打たない（PlaceBlock）、
///   波が通る前後（0.8秒前〜0.5秒後）はその x のマーカーを光らせない（DipHold。着くのを遅らせる）
/// 時刻は表の時計（ダメージのあと 1秒止まる）なので、書き出しのバーより少し遅れることがある
/// </summary>
public class PremiereWaveformEvent : KitTimedEvent
{
    const float RenderSeconds = 12f; // PremiereRenderEvent の長さ
    const float WaveSpeed = 6f, WaveHeight = 0.9f, WaveWidth = 0.7f, Warn = 0.8f;
    const float BeltWidth = 6f, BeltSpeed = -2f, BeltSeconds = 3f;
    const float HoldBefore = 0.8f, HoldAfter = 0.5f;
    static readonly Color A1 = RuntimeSprites.Hex("6A9C86");

    struct Wave
    {
        public float T0;     // 動き出す時刻（Time.time）
        public float X0;     // 動き出す x
        public float Dir;    // +1 右へ、-1 左へ
        public float EndX;   // 止まる x
    }

    readonly List<Wave> _waves = new List<Wave>();
    float _beltFrom = -1f, _beltTo = -1f, _beltX;

    public PremiereWaveformEvent() : base(null) { }

    protected override void Build()
    {
        At(RenderSeconds * 0.3f, () => Run(KitSide.Left));
        At(RenderSeconds * 0.5f, Belt);
        At(RenderSeconds * 0.7f, () => Run(Level >= 3 ? KitSide.Both : KitSide.Left));
        EndAt(RenderSeconds * 0.7f + Warn + (View.width + 2f) / WaveSpeed + 0.3f);
        PremiereTimelineStage tl = PremiereTimelineStage.Current;
        if (tl != null)
        {
            tl.DipHold = Hold;
            tl.PlaceBlock = Blocked;
        }
    }

    protected override void OnEnd(bool byBoss)
    {
        PremiereTimelineStage tl = PremiereTimelineStage.Current;
        if (tl != null)
        {
            if (tl.DipHold == (System.Func<float, bool>)Hold) tl.DipHold = null;
            if (tl.PlaceBlock == (System.Func<float, bool>)Blocked) tl.PlaceBlock = null;
        }
        base.OnEnd(byBoss);
    }

    void Run(KitSide side)
    {
        Rect a = View;
        float floor = Floor;
        bool meet = side == KitSide.Both;
        KitMotionGroup g = KitMotion.FloorWave(Owner, side, WaveSpeed, WaveHeight, Warn, WaveSprite, null, 1, WaveWidth, meet, floor);
        float w = g != null ? KitMotion.WarnFor(g.Owner, Warn, true) : Warn;
        float t0 = Time.time + w;
        if (side != KitSide.Right) _waves.Add(new Wave { T0 = t0, X0 = a.xMin - WaveWidth, Dir = 1f, EndX = meet ? a.center.x : a.xMax + WaveWidth });
        if (side != KitSide.Left) _waves.Add(new Wave { T0 = t0, X0 = a.xMax + WaveWidth, Dir = -1f, EndX = meet ? a.center.x : a.xMin - WaveWidth });
    }

    void Belt()
    {
        Rect a = View;
        _beltX = a.center.x;
        _beltFrom = Time.time;
        _beltTo = Time.time + Warn + BeltSeconds;
        KitMotion.Conveyor(Owner, _beltX, BeltWidth, BeltSpeed, BeltSeconds, Warn, A1, Floor);
    }

    /// <summary>波が x を通る前後か（マーカーを光らせない）</summary>
    bool Hold(float x)
    {
        float now = Time.time;
        foreach (Wave w in _waves)
        {
            if (w.Dir > 0f ? x < w.X0 || x > w.EndX : x > w.X0 || x < w.EndX) continue;
            float pass = w.T0 + Mathf.Abs(x - w.X0) / WaveSpeed;
            if (now >= pass - HoldBefore && now <= pass + HoldAfter) return true;
        }
        return false;
    }

    /// <summary>流れる床の間、その幅か（マーカーを打たない）</summary>
    bool Blocked(float x)
    {
        float now = Time.time;
        return _beltFrom >= 0f && now >= _beltFrom && now <= _beltTo && Mathf.Abs(x - _beltX) < BeltWidth * 0.5f + 0.75f;
    }

    /// <summary>音声トラックの波形の山（13×16。A1 の緑の縦棒・上の端が白）</summary>
    static Sprite WaveSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(13, 16);
            int[] heights = { 5, 9, 13, 16, 12, 8, 4 };
            for (int i = 0; i < heights.Length; i++)
            {
                int x = i * 2, h = heights[i];
                GimmickA.Fill(g, x, 16 - h, 1, h, 'G');
                g[16 - h, x] = 'W';
                if (x + 1 < 13 && i < heights.Length - 1)
                {
                    int h2 = (h + heights[i + 1]) / 2 - 2;
                    if (h2 > 0) { GimmickA.Fill(g, x + 1, 16 - h2, 1, h2, 'D'); }
                }
            }
            var pal = new Dictionary<char, Color32> { { 'G', (Color32)A1 }, { 'W', new Color32(255, 255, 255, 255) }, { 'D', RuntimeSprites.Hex("4E7A67") } };
            return RuntimeSprites.FromAscii("PremiereWaveform", GimmickA.Rows(g), pal);
        }
    }
}
