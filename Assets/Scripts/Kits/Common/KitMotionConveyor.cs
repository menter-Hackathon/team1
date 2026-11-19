using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】動く床（KitMotion.Conveyor が作る。2026-11-19）。担当0 の物。
/// ・床の一部（xMin〜xMax・高さ SurfaceY）の上に、山かっこの矢印の列。予告の間は点滅しながらゆっくり流れ、動く間は speed で流れる
/// ・動く間、その上に立っているプレイヤー・雑魚（KinematicBody2D）を Carry で横に運ぶ（AppPlatform と同じやり方。
///   乗っている物の Move より前に呼ぶため、早く動く）。当たり判定は無い
/// </summary>
[DefaultExecutionOrder(-200)]
public class KitMotionConveyor : KitMotionItem
{
    const float Spacing = 0.9f;
    static readonly Collider2D[] Riders = new Collider2D[8];
    static readonly HashSet<KinematicBody2D> Carried = new HashSet<KinematicBody2D>();

    float _xMin, _xMax, _duration, _flow;
    Color _color;
    readonly List<SpriteRenderer> _arrows = new List<SpriteRenderer>();
    SpriteRenderer _strip;

    /// <summary>流れる速さ（毎秒のマス。＋で右、－で左）。途中で変えてよい（逆にすると矢印も向きが変わる）</summary>
    public float Speed { get; set; }
    /// <summary>床の高さ</summary>
    public float SurfaceY { get; private set; }
    /// <summary>今、運んでいるか（予告のあと、duration 秒）</summary>
    public bool Running => !IsDone && Age >= Warn && Age < Warn + _duration;

    internal void Setup(float x, float width, float speed, float duration, float warn, Color color, float surface)
    {
        _xMin = x - width * 0.5f;
        _xMax = x + width * 0.5f;
        Speed = speed;
        _duration = duration;
        Warn = warn;
        SurfaceY = surface;
        _color = color;
        transform.position = new Vector3(x, surface, 0f);
        _strip = FxKit.Box(transform, new Vector2(0f, KitDress.Px), new Vector2(width, KitDress.Px * 2f), new Color(color.r, color.g, color.b, 0.6f), OrderZone, "Strip");
        int n = Mathf.Max(1, Mathf.FloorToInt(width / Spacing));
        for (int i = 0; i < n; i++)
        {
            SpriteRenderer sr = AttackRunner.AddVisual(transform, KitMotionArt.Chevron, color, new Vector2(5f, 7f) * KitDress.Px * 1.4f, OrderZone + 1, "Arrow");
            _arrows.Add(sr);
        }
        Place();
    }

    protected override bool Step(float age)
    {
        if (age >= Warn + _duration) return false;
        bool warning = age < Warn;
        _flow += Speed * (warning ? 0.3f : 1f) * Time.deltaTime;
        Place();
        float a;
        if (warning) a = Warn - age < 0.3f ? (Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? 1f : 0.3f) : 0.55f + 0.35f * Mathf.Sin(age * 10f);
        else a = 0.9f;
        float left = Warn + _duration - age; // 最後の 0.5秒はうすくなる
        if (!warning && left < 0.5f) a *= left / 0.5f;
        foreach (var sr in _arrows)
        {
            if (sr == null) continue;
            Color c = _color;
            c.a *= a * EdgeFade(sr.transform.position.x);
            sr.color = c;
            sr.flipX = Speed < 0f;
        }
        if (_strip != null) _strip.color = new Color(_color.r, _color.g, _color.b, 0.6f * a);
        return true;
    }

    // 矢印を、流れた分だけずらして並べる（はしまで行ったら反対のはしへ）
    void Place()
    {
        int n = _arrows.Count;
        if (n == 0) return;
        float width = _xMax - _xMin;
        float span = width / n;
        float cx = (_xMin + _xMax) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            if (_arrows[i] == null) continue;
            float s = Mathf.Repeat(i * span + span * 0.5f + _flow, width);
            _arrows[i].transform.position = new Vector3(_xMin + s, SurfaceY + 0.28f, 0f);
        }
        if (_strip != null) _strip.transform.position = new Vector3(cx, SurfaceY + KitDress.Px, 0f);
    }

    float EdgeFade(float x)
    {
        float d = Mathf.Min(x - _xMin, _xMax - x);
        return Mathf.Clamp01(d / 0.4f);
    }

    void FixedUpdate()
    {
        if (!Running || Mathf.Abs(Speed) < 0.001f) return;
        Vector2 delta = new Vector2(Speed * Time.fixedDeltaTime, 0f);
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Player", "Enemy"));
        int n = Physics2D.OverlapBox(new Vector2((_xMin + _xMax) * 0.5f, SurfaceY + 0.15f), new Vector2(_xMax - _xMin, 0.3f), 0f, filter, Riders);
        Carried.Clear();
        for (int i = 0; i < n; i++)
        {
            Collider2D c = Riders[i];
            Rigidbody2D rb = c != null ? c.attachedRigidbody : null;
            if (rb == null || !rb.TryGetComponent(out KinematicBody2D kb) || !rb.TryGetComponent(out BoxCollider2D box) || !Carried.Add(kb)) continue;
            Bounds feet = box.bounds;
            if (!kb.IsGrounded || Mathf.Abs(feet.min.y - SurfaceY) > 0.1f) continue; // この床の上に立っている物だけ
            if (feet.center.x < _xMin || feet.center.x > _xMax) continue;
            kb.Carry(delta);
        }
        Carried.Clear();
    }
}
