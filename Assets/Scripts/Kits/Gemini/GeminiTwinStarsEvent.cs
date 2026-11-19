using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Gemini の場面③の出来事「ふたご星」（2026-11-19。docs/stage-gimmick-variety-b.md 3章）。数値はすべて【仮】。
/// ・部屋のまん中（床から 4.5）を中心に、青い星とピンクの星（直径 1）が向かい合って回る（半径 4.5・1周 5秒・はじめは時計回り）。
///   半径 4.5 なので、床と足場の両方を通る。6秒たつと 0.5秒止まって（白く点滅）逆回り。全部で 12秒
///   ステージの番号で星の数が 1〜3（3つのときは紫も入れて 120度ずつ）
/// ・予告：回る道すじを、うすい赤の点線の円で 1.0秒。星はその場所でうすく点滅
/// ・星のうしろに、うすい尾（0.4秒の残像）
/// ・4秒・8秒に、中心から小さい 4つ角のきらめきが、星と 90度ずれた向きへゆっくり流れる（KitMotion.Catch・速さ 2）。
///   取ると：1回目＝能力のクールタイムを全部戻す／2回目＝星が 3秒止まる
/// ・中心の近く（半径 3 より内）はずっと安全。星の間はいつも半周（3つなら 1/3周）あく
/// ・KitMotion.Orbit は「止まる・逆回り・3秒止まる」を 1本の道にできないので、星は時間をゆがめた自分の道すじ（GroupBMotion.Mover）で動かす
/// </summary>
public class GeminiTwinStarsEvent : KitTimedEvent
{
    const float CenterY = 4.5f, Radius = 4.5f, Period = 5f, Duration = 12f, FlipAt = 6f, Pause = 0.5f;
    const float StarSize = 1f, FirstWarn = 1f, FreezeSeconds = 3f;
    const float CatchSpeed = 2f, CatchLinger = 2f, CatchSize = 0.8f;
    static readonly float[] CatchTimes = { 4f, 8f };
    const float TrailEvery = 0.05f, TrailLife = 0.4f;
    const float StartDeg = 90f; // 1つ目の星は真上から

    readonly int _stage;
    readonly List<KitMotionMover> _stars = new List<KitMotionMover>();
    readonly List<Color> _colors = new List<Color>();
    Vector2 _center;
    float _freezeAt = float.NaN; // 星が止まり始めた道の秒数（2回目のきらめき）
    int _caught;
    bool _white;
    float _trailAt;

    public GeminiTwinStarsEvent(int stage) : base("Gemini")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        At(0f, Spawn);
        foreach (float t in CatchTimes) At(t, Sparkle);
        EndAt(FirstWarn + Duration + Pause + 0.3f);
    }

    void Spawn()
    {
        _center = new Vector2(View.center.x, Floor + CenterY);
        int count = Mathf.Clamp(GroupBMotion.Scaled(2, _stage), 1, 3);
        Color[] palette = count == 3 ? new[] { GeminiArt.Blue, GeminiArt.Purple, GeminiArt.Pink } : new[] { GeminiArt.Blue, GeminiArt.Pink };
        Sprite star = StarSprite;
        for (int i = 0; i < count; i++)
        {
            float start = StartDeg + 360f * i / count;
            KitPath path = new KitPath(t => _center + Dir(start - 360f / Period * Warp(t)) * Radius, Duration + Pause);
            Color c = palette[i % palette.Length];
            KitMotionMover m = GroupBMotion.Mover(Owner, path, Vector2.one * StarSize, star, c, FirstWarn, true, 1, "Gemini_TwinStar", 0.8f, true);
            if (m == null) continue;
            _stars.Add(m);
            _colors.Add(c);
        }
        // 予告：回る道すじ（うすい赤の点線の円）
        KitMotionMark ring = KitMotionMark.Create(null, KitMotionArt.Ring(64, true), _center, Vector2.one * Radius * 2f, 0f,
            new Color(KitMotion.Red.r, KitMotion.Red.g, KitMotion.Red.b, 0.5f), KitMotionItem.OrderWarn, FirstWarn, "Gemini_OrbitPath");
        if (ring != null) Track(ring.gameObject);
    }

    static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

    /// <summary>道の秒数 → 回った秒数（止まる 3秒をぬく → 6秒で 0.5秒止まる → 逆回り）</summary>
    float Warp(float t)
    {
        if (!float.IsNaN(_freezeAt) && t > _freezeAt) t = t < _freezeAt + FreezeSeconds ? _freezeAt : t - FreezeSeconds;
        if (t < FlipAt) return t;
        if (t < FlipAt + Pause) return FlipAt;
        return FlipAt - (t - FlipAt - Pause);
    }

    /// <summary>道の秒数（止まっている分をぬいた、Warp に入れる前の秒数のうち、止まる 3秒の分だけぬいた物）</summary>
    float Unfrozen(float t)
    {
        if (!float.IsNaN(_freezeAt) && t > _freezeAt) return t < _freezeAt + FreezeSeconds ? _freezeAt : t - FreezeSeconds;
        return t;
    }

    /// <summary>中心から、星と 90度ずれた向きへ、きらめき（良い物）</summary>
    void Sparkle()
    {
        Vector2 dir = Vector2.right;
        KitMotionMover lead = _stars.Find(s => s != null && !s.IsDone);
        if (lead != null && lead.Body != null)
        {
            Vector2 v = (Vector2)lead.Body.position - _center;
            if (v.sqrMagnitude > 0.01f) dir = new Vector2(-v.y, v.x).normalized;
        }
        KitPath path = KitPath.Line(_center, _center + dir * (Radius + 1.5f), CatchSpeed).Then(CatchLinger);
        KitMotion.Catch(Owner, path, OnSparkle, GeminiArt.Star4(9), Color.white, CatchSize);
    }

    void OnSparkle()
    {
        _caught++;
        GameAudio.Event(_caught == 1 ? "gm_gemini_pick_blue" : "gm_gemini_pick_pink");
        if (_caught == 1)
        {
            KitPlayer.RefillCooldowns();
            return;
        }
        // 2回目：星が 3秒止まる
        KitMotionMover lead = _stars.Find(s => s != null && !s.IsDone);
        if (lead != null && float.IsNaN(_freezeAt)) _freezeAt = lead.MoveTime;
        foreach (KitMotionMover s in _stars) if (s != null && !s.IsDone && s.Body != null) FxKit.Ring(s.Body.position, 1.6f, 0.25f, Color.white);
    }

    protected override void OnFrame(float clock)
    {
        _stars.RemoveAll(s => s == null || s.IsDone);
        if (_stars.Count == 0) return;
        // 逆回りの前の 0.5秒（止まっている間）は、白く点滅
        KitMotionMover lead = _stars[0];
        float t = lead.Warning ? -1f : Unfrozen(lead.MoveTime);
        bool flip = t >= FlipAt && t < FlipAt + Pause;
        bool white = flip && Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        if (white != _white)
        {
            _white = white;
            for (int i = 0; i < _stars.Count && i < _colors.Count; i++) if (_stars[i] != null) _stars[i].SetColor(white ? Color.white : _colors[i]);
        }
        // うすい尾
        if (Time.time < _trailAt) return;
        _trailAt = Time.time + TrailEvery;
        Sprite star = StarSprite;
        for (int i = 0; i < _stars.Count && i < _colors.Count; i++)
        {
            KitMotionMover s = _stars[i];
            if (s == null || s.Warning || s.Body == null) continue;
            Color c = _colors[i];
            c.a = 0.35f;
            FxAnim.Sprite(star, s.Body.position, KitMotionItem.OrderBody - 1, TrailLife, c, StarSize * 18f / 13f * 0.85f, "Gemini_Trail").FadeFrom(0f);
        }
    }

    /// <summary>白い 4つ角の星（13ドット。色は SpriteRenderer で付ける。形は GeminiArt.Star4 と同じ）</summary>
    static Sprite StarSprite => KitArt.Paint("gemini.event.star", 13, 13, c =>
    {
        const int n = 13;
        float m = (n - 1) * 0.5f;
        var white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float ax = Mathf.Abs(x - m) / m, ay = Mathf.Abs(y - m) / m;
                if (Mathf.Sqrt(ax) + Mathf.Sqrt(ay) <= 1.02f) c.Px(x, y, white);
            }
    });
}
