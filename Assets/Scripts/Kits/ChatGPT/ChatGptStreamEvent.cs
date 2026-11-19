using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ChatGPT の場面③の出来事「ストリーミング」（2026-11-19。docs/stage-gimmick-variety-b.md 1章）。数値はすべて【仮】。
/// ・答えの文字の行（横 3×縦 0.5 の灰色の棒。先に緑の丸）が、左の端から右へ波打って横切る（速さ 7・振れ幅 ±0.5）
/// ・高さは 3つの段を順に：床（0.7）→ 下の足場の上（3.6）→ 上の足場の上（5.6）→ くり返し。1回に 2本（0.5秒おき）、2.2秒おきに 5回
///   （同じ段が続かない。ほかの 2つの段は安全）。ステージの番号で 1回の本数が 1〜3
/// ・予告：左の端に、その高さの矢印と赤い線（波のぶんの帯）＋点滅するカーソル「▌」（緑）。1回目は 1.0秒、あとは 0.8秒
/// ・3秒たつと、入力欄の右（stop ボタンのさらに右）に「■ Stop」の的（5秒）。叩くと、画面の行が消えて、出来事が早く終わる（叩かなくても罰はない）
/// ・行が通ったあとに、うすい残像（0.3秒）
/// </summary>
public class ChatGptStreamEvent : KitTimedEvent
{
    /// <summary>行の高さ（床から）：床・下の足場の上・上の足場の上</summary>
    static readonly float[] Lanes = { 0.7f, 3.6f, 5.6f };
    static readonly Vector2 RowSize = new Vector2(3f, 0.5f);
    const int Waves = 5;
    const float Step = 2.2f, Gap = 0.5f, Speed = 7f, Wave = 0.5f, WaveLength = 4f;
    const float FirstWarn = 1f, Warn = 0.8f;
    const float StopAt = 3f, StopLife = 5f, StopSize = 1f;
    /// <summary>「■ Stop」の的の場所（部屋のまん中・床から）。入力欄（x −4〜4）と stop ボタン（x 4.4〜5.9）の右</summary>
    static readonly Vector2 StopSpot = new Vector2(6.6f, 1.3f);
    const float TrailEvery = 0.07f, TrailLife = 0.3f;

    readonly int _stage;
    readonly List<KitMotionMover> _rows = new List<KitMotionMover>();
    float _trailAt;

    public ChatGptStreamEvent(int stage) : base("Generating...")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        int perWave = GroupBMotion.Scaled(2, _stage);
        for (int i = 0; i < Waves; i++)
        {
            int wave = i;
            At(i * Step, () => Stream(wave, perWave));
        }
        At(StopAt, ShowStop);
        float cross = (View.width + 2f * (RowSize.x * 0.5f + 0.3f)) / Speed;
        EndAt((Waves - 1) * Step + Warn + (perWave - 1) * Gap + cross + 0.2f);
    }

    /// <summary>1回分：その段に、左から perWave 本</summary>
    void Stream(int wave, int perWave)
    {
        float y = Floor + Lanes[wave % Lanes.Length];
        float warn = wave == 0 ? FirstWarn : Warn;
        Sprite row = RowSprite;
        for (int k = 0; k < perWave; k++)
        {
            KitMotionMover m = GroupBMotion.SweepRect(Owner, KitSide.Left, y, Speed, RowSize, row, Color.white, warn + k * Gap, Wave, WaveLength,
                objectName: "ChatGPT_StreamRow");
            if (m == null) continue;
            _rows.Add(m);
        }
        // 左の端に、点滅するカーソル「▌」（アプリの色の印。最後の行が出るまで。行は動くので、この出来事の物として置く）
        KitMotionMark cursor = KitMotionMark.Create(null, PlaceholderSprites.Square, new Vector2(View.xMin + 1.2f, y), new Vector2(0.16f, 0.5f), 0f,
            ChatGptArt.Green, KitMotionItem.OrderMark, warn + (perWave - 1) * Gap, "ChatGPT_Cursor");
        if (cursor != null) Track(cursor.gameObject);
        GameAudio.Event("gm_chatgpt_long_answer");
    }

    void ShowStop()
    {
        Vector2 at = new Vector2(View.center.x + StopSpot.x, Floor + StopSpot.y);
        at.x = Mathf.Min(at.x, View.xMax - 0.8f);
        KitMotion.HitTarget(Owner, at, StopLife, StopStream, null, ChatGptArt.StopButton, Color.white, StopSize, 1, null, 0.4f);
    }

    /// <summary>「■ Stop」：画面の行が消えて、出来事が早く終わる</summary>
    void StopStream()
    {
        GameAudio.Event("gm_chatgpt_stop");
        foreach (KitMotionMover m in _rows)
        {
            if (m == null || m.IsDone) continue;
            if (!m.Warning && m.Body != null) FxKit.Ring(m.Body.position, 1.2f, 0.2f, Color.white);
            m.Cancel();
        }
        _rows.Clear();
        Stop();
    }

    protected override void OnFrame(float clock)
    {
        if (Time.time < _trailAt) return;
        _trailAt = Time.time + TrailEvery;
        _rows.RemoveAll(m => m == null || m.IsDone);
        Sprite row = RowSprite;
        foreach (KitMotionMover m in _rows)
        {
            if (m.Warning || m.Body == null) continue;
            // 絵は元の大きさ（54×9 ドット）でちょうど 3×0.5 マス
            FxAnim.Sprite(row, m.Body.position, KitMotionItem.OrderBody - 1, TrailLife, new Color(1f, 1f, 1f, 0.3f), 1f, "ChatGPT_Trail").FadeFrom(0f);
        }
    }

    /// <summary>答えの行（54×9 ドット＝3×0.5 マス）：灰色の棒に、うすい字の線。右の先に緑の丸（書いている途中の印）</summary>
    static Sprite RowSprite => KitArt.Paint("chatgpt.event.row", 54, 9, c =>
    {
        Color32 gray = ChatGptArt.Gray, light = ChatGptArt.LightGray, green = ChatGptArt.Green, dark = ChatGptArt.Panel;
        c.RoundFill(0, 0, 46, 9, dark);
        c.Frame(0, 0, 46, 9, gray);
        c.Fill(3, 3, 18, 2, light);
        c.Fill(23, 3, 8, 2, light);
        c.Fill(33, 3, 10, 2, gray);
        c.Disc(50, 4, 3, green);
    });
}
