using System;
using UnityEngine;

/// <summary>
/// 【仮】「ここに立つ」場所（KitMotion.HoldZone が作る。2026-11-19）。担当0 の物。
/// ・床の上に光る帯（アプリの色。赤は使わない）。帯の上に、丸く並んだ 12個の点（進みの印）
/// ・プレイヤーの体のまん中が帯の中（足が床〜帯の高さ）にいる間、進みがたまる。出るとすぐ 0 に戻る
/// ・seconds 秒たまったら onDone（1回。白い輪と音）。life 秒たっても終わらなければ onFail（life 0 ならずっと）
/// ・warn があれば、その間は帯が点滅して出てくる（まだ数えない）
/// ・外から消された（片づけ・Cancel）ときは、どちらも呼ばない
/// </summary>
public class KitMotionHold : KitMotionItem
{
    const int Segments = 12;
    const float RingRadius = 0.42f;

    float _xMin, _xMax, _surface, _height, _seconds, _life;
    Action _onDone, _onFail;
    Color _color;
    SpriteRenderer _fill, _top, _left, _right;
    readonly SpriteRenderer[] _segs = new SpriteRenderer[Segments];

    /// <summary>今たまっている秒数</summary>
    public float Progress { get; private set; }
    /// <summary>進み（0〜1）</summary>
    public float Progress01 => _seconds > 0f ? Mathf.Clamp01(Progress / _seconds) : 0f;
    /// <summary>今プレイヤーが中にいるか</summary>
    public bool PlayerInside { get; private set; }
    /// <summary>終わったか（onDone を呼んだ）</summary>
    public bool Done { get; private set; }
    /// <summary>帯の範囲（世界）</summary>
    public Rect Zone => new Rect(_xMin, _surface, _xMax - _xMin, _height);

    internal void Setup(float x, float width, float seconds, Action onDone, float life, Action onFail, Color color, float height, float surface, float warn)
    {
        _xMin = x - width * 0.5f;
        _xMax = x + width * 0.5f;
        _surface = surface;
        _height = height;
        _seconds = seconds;
        _life = life;
        _onDone = onDone;
        _onFail = onFail;
        _color = color;
        Warn = warn;
        transform.position = new Vector3(x, surface, 0f);
        _fill = FxKit.Box(transform, new Vector2(0f, height * 0.5f), new Vector2(width, height), color, OrderZone, "Fill");
        _top = FxKit.Box(transform, new Vector2(0f, height - KitDress.Px), new Vector2(width, KitDress.Px * 2f), Color.white, OrderZone + 1, "Top");
        _left = FxKit.Box(transform, new Vector2(-width * 0.5f + KitDress.Px * 0.5f, height * 0.5f), new Vector2(KitDress.Px, height), Color.white, OrderZone + 1, "Left");
        _right = FxKit.Box(transform, new Vector2(width * 0.5f - KitDress.Px * 0.5f, height * 0.5f), new Vector2(KitDress.Px, height), Color.white, OrderZone + 1, "Right");
        Vector2 ringAt = new Vector2(0f, height + RingRadius + 0.25f);
        for (int i = 0; i < Segments; i++)
        {
            float a = (90f - 360f * i / Segments) * Mathf.Deg2Rad; // 上から時計回り
            _segs[i] = FxKit.Box(transform, ringAt + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius, Vector2.one * KitDress.Px * 3f, Color.white, OrderMark, "Seg");
        }
        Paint(0f, false);
        GameAudio.Event("popup_open");
    }

    protected override bool Step(float age)
    {
        if (age < Warn)
        {
            bool on = Warn - age < 0.3f ? Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 : true;
            Paint(on ? 0.5f + 0.3f * Mathf.Sin(age * 10f) : 0.15f, false);
            return true;
        }
        float t = age - Warn;
        PlayerInside = CheckInside();
        if (PlayerInside) Progress += Time.deltaTime;
        else Progress = 0f;
        Paint(1f, PlayerInside);
        if (Progress >= _seconds)
        {
            Done = true;
            Progress = _seconds;
            FxKit.Ring(new Vector2((_xMin + _xMax) * 0.5f, _surface + _height * 0.5f), Mathf.Max(_xMax - _xMin, _height) * 1.2f, 0.3f, Color.white);
            GameAudio.Event("skill_pickup");
            KitMotion.Safe(_onDone);
            return false;
        }
        if (_life > 0f && t >= _life)
        {
            KitMotion.Safe(_onFail);
            return false;
        }
        return true;
    }

    bool CheckInside()
    {
        if (!KitMotion.PlayerBounds(out Bounds b)) return false;
        float cx = b.center.x;
        return cx >= _xMin && cx <= _xMax && b.min.y >= _surface - 0.3f && b.min.y <= _surface + _height;
    }

    void Paint(float alpha, bool inside)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (inside ? 10f : 5f));
        float fillA = (inside ? 0.28f : 0.14f + 0.06f * pulse) * alpha;
        SetColor(_fill, new Color(_color.r, _color.g, _color.b, fillA));
        Color edge = Color.Lerp(_color, Color.white, inside ? 0.8f : 0.5f);
        edge.a = (inside ? 1f : 0.7f) * alpha;
        SetColor(_top, edge);
        Color side = edge;
        side.a *= 0.6f;
        SetColor(_left, side);
        SetColor(_right, side);
        int lit = Mathf.FloorToInt(Progress01 * Segments + 0.001f);
        for (int i = 0; i < Segments; i++)
        {
            Color c = i < lit ? Color.white : new Color(_color.r * 0.5f, _color.g * 0.5f, _color.b * 0.5f, 0.8f);
            c.a *= alpha;
            SetColor(_segs[i], c);
        }
    }

    static void SetColor(SpriteRenderer sr, Color c)
    {
        if (sr != null) sr.color = c;
    }
}
