using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックのステージの出来事「ビートの波」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 8章）。
/// ・時計は MusicRule のビート（0.6秒＝1ビート。曲の再生位置に合わせた MusicRule.Phase01 の折り返しを数える）。24ビート＋1（約 15秒）
/// ・0〜4ビート目：前ぶれ。床の左右の端にピンクの矢印（2.4秒）と、端の棒がはねる
/// ・FloorWave（主）：音の波。4ビートごとに1つ、ビートの瞬間に端を出る（4・8・12・16・20ビート目）。左・右・左…の順。4／5／5回。
///   強さ 3 は最後の1回が左右同時（まん中でぶつかって消える）。速さ 8.3（端からまん中まで、ちょうど 2ビート）・高さ 0.8。
///   予告：出る 2ビート前から（1.2秒）、出る側の端の棒がビートごとに高くなる＋赤い矢印。絵はイコライザーの棒 3本（先頭だけ赤寄りのピンク）
///   ダメージのすぐあと（新しい予告を出さない 1秒）にかかった波は、出さずに飛ばす（リズムをずらさない）
/// ・Catch：音符。6ビート目から 2ビートごとに 1つ、上の左右の角から、ゆるい弧で反対側の低い足場の高さへ（3秒）。合わせて 6個。
///   取る：その後 3秒、Perfect の窓が 0.12 → 0.2秒に広がる（MusicRule.WidenWindow）。ピンクの輪
/// </summary>
public class MusicBeatWaveEvent : KitTimedEvent
{
    const float WaveSpeed = 8.3f, WaveHeight = 0.8f, WaveWidth = 1.2f;
    const int FirstWaveBeat = 4, WaveEveryBeats = 4, WarnBeats = 2, EndBeat = 25;
    const int FirstNoteBeat = 6, NoteEveryBeats = 2, Notes = 6;
    const float NoteTime = 3f, NoteLinger = 0.8f, NoteHeight = 3.5f, NoteSize = 0.8f, WideSeconds = 3f;

    readonly List<KitMotionGroup> _waves = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    float _lastPh = -1f;
    int _beat;
    int _noteCount;

    public MusicBeatWaveEvent() : base("Beat Wave") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(MusicKit.Label, stage => new MusicBeatWaveEvent());

    int WaveCount => Level >= 2 ? 5 : 4;

    protected override void Build()
    {
        EndAt(EndBeat * MusicRule.Beat + 2f); // 念のための上限（ふつうはビートを数えて Stop で終わる）
    }

    protected override void OnFrame(float clock)
    {
        float ph = StageRules.Current is MusicRule ? MusicRule.Phase01 : Mathf.Repeat(Time.time, MusicRule.Beat) / MusicRule.Beat;
        bool tick = _lastPh >= 0f && ph + 0.5f < _lastPh; // 0 へ折り返した＝ビートの瞬間
        _lastPh = ph;
        if (tick) OnBeat(_beat++);
        DressWaves();
    }

    void OnBeat(int b)
    {
        if (b >= EndBeat) { Stop(); return; }
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        if (b == 0)
        {
            // 前ぶれ：床の左右の端にピンクの矢印（4ビート）
            Track(KitMotionMark.Create(null, KitMotionArt.Arrow, new Vector2(a.xMin + 0.55f, floor + 1.15f), Vector2.one * 0.75f, 0f, (Color)MusicArt.Pink, KitMotionItem.OrderMark, MusicRule.Beat * 4f, "MusicArrowL").gameObject);
            Track(KitMotionMark.Create(null, KitMotionArt.Arrow, new Vector2(a.xMax - 0.55f, floor + 1.15f), Vector2.one * 0.75f, 180f, (Color)MusicArt.Pink, KitMotionItem.OrderMark, MusicRule.Beat * 4f, "MusicArrowR").gameObject);
        }
        if (b < FirstWaveBeat) { EdgeBars(KitSide.Both, 1 + b % 2); }

        // 波：出る 2ビート前に予告を始める（予告 1.2秒＝ちょうど 2ビート後に端を出る）
        for (int k = 0; k < WaveCount; k++)
        {
            int launch = FirstWaveBeat + WaveEveryBeats * k;
            KitSide side = Level >= 3 && k == WaveCount - 1 ? KitSide.Both : k % 2 == 0 ? KitSide.Left : KitSide.Right;
            if (b == launch - WarnBeats)
            {
                if (Quiet) continue; // ダメージのすぐあと：この波は出さない
                KitMotionGroup g = KitMotion.FloorWave(Owner, side, WaveSpeed, WaveHeight, MusicRule.Beat * WarnBeats, WaveSprite, null, 1, WaveWidth, side == KitSide.Both);
                if (g != null) _waves.Add(g);
                EdgeBars(side, 1);
            }
            else if (b == launch - WarnBeats + 1) EdgeBars(side, 2);
        }

        // 音符
        if (b >= FirstNoteBeat && (b - FirstNoteBeat) % NoteEveryBeats == 0 && _noteCount < Notes) Note(_noteCount++);
    }

    /// <summary>端の棒がはねる（level が高いほど高い。0.5ビート。飾り）</summary>
    void EdgeBars(KitSide side, int level)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        foreach (bool left in new[] { true, false })
        {
            if (left && side == KitSide.Right || !left && side == KitSide.Left) continue;
            for (int i = 0; i < 3; i++)
            {
                float h = 0.5f + 0.45f * level + 0.25f * (left ? i : 2 - i);
                float x = left ? a.xMin + 0.3f + i * 0.32f : a.xMax - 0.3f - i * 0.32f;
                Track(KitMotionMark.Create(null, PlaceholderSprites.Square, new Vector2(x, floor + h * 0.5f), new Vector2(0.24f, h), 0f,
                    MusicArt.BarColor(i / 2f), KitMotionItem.OrderWarn, MusicRule.Beat * 0.6f, "MusicEdgeBar").gameObject);
            }
        }
    }

    void Note(int i)
    {
        Rect a = KitMotion.Area;
        bool fromLeft = i % 2 == 0;
        Vector2 from = KitMotion.Corner(fromLeft ? KitCorner.TopLeft : KitCorner.TopRight);
        var to = new Vector2(a.center.x + (fromLeft ? 3.5f : -3.5f), KitMotion.FloorY() + NoteHeight);
        KitPath path = KitPath.Arc(from, to, 1f, NoteTime).Then(NoteLinger);
        KitMotion.Catch(Owner, path, OnNote, MusicArt.Note(i), null, NoteSize);
    }

    void OnNote()
    {
        if (StageRules.Current is MusicRule) MusicRule.WidenWindow(WideSeconds);
        else KitPlayer.AdvanceCooldowns(1f);
        FxKit.Ring(KitMotion.PlayerPos, 3f, 0.25f, (Color)MusicArt.Pink);
        GameAudio.Event("gm_music_perfect");
    }

    /// <summary>右から来る波は、絵を左右反転（先頭の棒が進む側）</summary>
    void DressWaves()
    {
        float cx = KitMotion.Area.center.x;
        _waves.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _waves)
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || !_dressed.Add(m)) continue;
                if (m.Visual != null) m.Visual.flipX = m.transform.position.x > cx;
            }
        _dressed.RemoveWhere(x => x == null);
    }

    static readonly Dictionary<char, Color32> WavePal = new Dictionary<char, Color32>
    {
        { 'P', MusicArt.Pink },
        { 'R', MusicArt.Red },
    };

    /// <summary>イコライザーの棒 3本（11×9。右へ進む向き：先頭＝右の棒がいちばん高く、赤寄りのピンク）</summary>
    static Sprite WaveSprite => RuntimeSprites.FromAscii("MusicEvent_Wave", new[]
    {
        "........RRR",
        "........RRR",
        "....PPP.RRR",
        "....PPP.RRR",
        "PPP.PPP.RRR",
        "PPP.PPP.RRR",
        "PPP.PPP.RRR",
        "PPP.PPP.RRR",
        "PPP.PPP.RRR",
    }, WavePal);
}
