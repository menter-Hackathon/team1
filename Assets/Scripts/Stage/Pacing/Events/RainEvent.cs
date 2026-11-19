using System.Collections.Generic;
using UnityEngine;

// 【仮】出来事「雨」（2026-11-19。docs/stage-pacing-design.md 3-2）。担当2 の物。
// 画面の上から物が次々に落ちる。出た瞬間から、落ちる床に影（予告 0.8秒）。プレイヤーの近く±3マス。同時に3個まで。
// 部品は FallingObject.Spawn（NotificationDropper と同じ呼び方）。絵は出来事用の AttackRunner のスキンが付ける
// （攻撃データの名前に ErrorWindowFall → 落下物の枠）。穴の上には落とさない。
// 強さ：1＝8秒・少しゆっくり、2＝10秒、3＝12秒・少し速い（重ねる雨は Seconds で短くする）。最後の物が着地するまで待ってから終わる。

/// <summary>出来事「雨」（Dev のビルドエラー・Chat の通知の嵐・AI の生成・Finder のコピー・Generic）</summary>
public class RainEvent : PaceEvent
{
    /// <summary>同時に落ちている物の数の上限</summary>
    const int MaxLive = 3;
    /// <summary>プレイヤーからずらす量の最大（マス）</summary>
    const float Spread = 3f;

    readonly AttackData _data;
    readonly float _interval;
    readonly List<FallingObject> _live = new List<FallingObject>();
    float _clock;
    float _next;
    float _duration;
    float _step;
    int _count;

    /// <param name="data">落とす物の攻撃データ（種類 Spawn。名前に ErrorWindowFall）</param>
    /// <param name="interval">落とす間隔（秒。強さ2のとき）</param>
    public RainEvent(AttackData data, float interval)
    {
        _data = data;
        _interval = Mathf.Max(0.3f, interval);
    }

    public override float MaxSeconds => 16f;

    /// <summary>続ける秒数を決めて入れる（0 なら強さで決める。重ねる雨は短くする）</summary>
    public float Seconds { get; set; }

    protected override void OnBegin()
    {
        _duration = Seconds > 0f ? Seconds : Strength <= 1 ? 8f : Strength == 2 ? 10f : 12f;
        _step = _interval * (Strength <= 1 ? 1.15f : Strength == 2 ? 1f : 0.85f);
        _next = 0f;
    }

    protected override bool OnTick(float dt)
    {
        _live.RemoveAll(f => f == null || f.HasLanded);
        if (_data == null) return false;
        // ダメージのすぐあとは、時計を止める（新しい影を出さない。落ちている物はそのまま）
        if (!Quiet) _clock += dt;
        // 終わりの少し前からは出さない（終わるときに、落ちている途中の物を消さないため）
        float stopAt = _duration - (_data.TelegraphTime + 0.9f);
        if (!Quiet && _clock >= _next && _clock < stopAt && _live.Count < MaxLive)
        {
            _next = _clock + (TrySpawn() ? _step : 0.2f);
        }
        return _clock < _duration || _live.Count > 0;
    }

    bool TrySpawn()
    {
        Rect a = Area;
        Vector2 player = PlayerPos;
        Vector2 size = _data.ObjectSize;
        float half = size.x * 0.5f;
        float minX = a.xMin + half + 0.3f, maxX = a.xMax - half - 0.3f;
        if (minX >= maxX) return false;
        float hangY = a.yMax - size.y * 0.5f - 1.1f; // 上の表示（HUD）に重ねない
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float x = Mathf.Clamp(player.x + Random.Range(-Spread, Spread), minX, maxX);
            if (Overlaps(x, half)) continue;
            if (!Runner.TryFindGround(x, hangY, out float groundY)) continue; // 穴の上には落とさない
            if (groundY > hangY - size.y) continue;                            // 上の方の足場のすぐ上
            float now = Time.time;
            FallingObject f = FallingObject.Spawn(Runner, _data, new Vector2(x, hangY), now, now + _data.TelegraphTime, true, false, _count++ % MaxLive); // 絵の番号は 0〜2 で回す
            if (f != null) _live.Add(f);
            return f != null;
        }
        return false;
    }

    /// <summary>まだ落ちていない物と、横に重なるか（同じ所に重ねて落とさない）</summary>
    bool Overlaps(float x, float half)
    {
        foreach (var f in _live)
        {
            if (f == null) continue;
            float other = f.Data != null ? f.Data.ObjectSize.x * 0.5f : half;
            if (Mathf.Abs(f.transform.position.x - x) < (half + other) * 0.85f) return true;
        }
        return false;
    }
}
