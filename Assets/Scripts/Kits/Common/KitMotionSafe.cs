using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】「ここだけ安全」の印（KitMotion.SafeSpot が作る。2026-11-19）。担当0 の物。
/// ・床の x・width に、アプリの色（赤は使わない）の帯と白いふち（画面の上まで）。点滅して目立たせる
/// ・then が無ければ：印のほかの床ぜんぶから Erupt（印の終わりにちょうどせり上がるよう、Erupt の予告を先に始める）
/// ・then があれば：warn 秒後に then(安全な範囲) を呼ぶ
/// ・Erupt はこの物の子（一緒に消える）。外から消された（片づけ・Cancel）ときは then を呼ばない
/// </summary>
public class KitMotionSafe : KitMotionItem
{
    Rect _safe;
    Action<Rect> _then;
    float _stay, _height, _eruptAt, _eruptWarn;
    int _damage;
    Sprite _sprite;
    Color? _hazardColor;
    Color _color;
    bool _launched, _fired;
    SpriteRenderer _fill, _left, _right, _floor;
    readonly List<KitMotionItem> _children = new List<KitMotionItem>();

    /// <summary>安全な範囲（世界。床から画面の上まで）</summary>
    public Rect SafeRect => _safe;
    /// <summary>危ない物を出したか（then を呼んだか・Erupt が上がったか）</summary>
    public bool Fired => _fired;

    internal void Setup(float x, float width, float warn, Action<Rect> then, Color color, int damage, float height, float stay, Sprite sprite, Color? hazardColor, float surface)
    {
        Rect a = KitMotion.Area;
        _safe = new Rect(x - width * 0.5f, surface, width, Mathf.Max(1f, a.yMax - surface));
        Warn = warn;
        _then = then;
        _color = color;
        _damage = damage;
        _height = height;
        _stay = stay;
        _sprite = sprite;
        _hazardColor = hazardColor;
        _eruptWarn = Mathf.Clamp(warn * 0.5f, KitMotion.WarnFor(Owner, 0f, true), warn);
        _eruptAt = Mathf.Max(0f, warn - _eruptWarn);
        transform.position = new Vector3(x, surface, 0f);
        float h = _safe.height;
        _fill = FxKit.Box(transform, new Vector2(0f, h * 0.5f), new Vector2(width, h), color, OrderZone, "Fill");
        _left = FxKit.Box(transform, new Vector2(-width * 0.5f + KitDress.Px, h * 0.5f), new Vector2(KitDress.Px * 2f, h), Color.white, OrderZone + 1, "Left");
        _right = FxKit.Box(transform, new Vector2(width * 0.5f - KitDress.Px, h * 0.5f), new Vector2(KitDress.Px * 2f, h), Color.white, OrderZone + 1, "Right");
        _floor = FxKit.Box(transform, new Vector2(0f, KitDress.Px * 1.5f), new Vector2(width, KitDress.Px * 3f), Color.Lerp(color, Color.white, 0.5f), OrderZone + 1, "Floor");
        Paint(0f);
    }

    protected override bool Step(float age)
    {
        Paint(age);
        if (_then == null && !_launched && age >= _eruptAt)
        {
            _launched = true;
            Rect a = KitMotion.Area;
            float hold = Mathf.Max(0.5f, _stay * 0.5f);
            if (_safe.xMin - a.xMin > 0.3f) Launch(a.xMin, _safe.xMin, hold);
            if (a.xMax - _safe.xMax > 0.3f) Launch(_safe.xMax, a.xMax, hold);
        }
        if (!_fired && age >= Warn)
        {
            _fired = true;
            if (_then != null)
            {
                Rect safe = _safe;
                KitMotion.Safe(() => _then(safe));
            }
        }
        _children.RemoveAll(c => c == null || c.IsDone);
        return age < Warn + _stay || _children.Count > 0;
    }

    void Launch(float xMin, float xMax, float hold)
    {
        KitMotionErupt e = KitMotion.MakeErupt(Owner, this, (xMin + xMax) * 0.5f, xMax - xMin, _height, _eruptWarn, 0.15f, hold, _sprite, _hazardColor, _damage, _safe.yMin);
        if (e != null) _children.Add(e);
    }

    void Paint(float age)
    {
        bool fast = age < Warn && Warn - age < 0.3f;
        float pulse = fast ? (Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? 1f : 0.4f) : 0.6f + 0.4f * Mathf.Sin(age * 8f);
        float fade = age > Warn + _stay - 0.3f ? Mathf.Clamp01((Warn + _stay - age) / 0.3f) : 1f;
        if (_fill != null) _fill.color = new Color(_color.r, _color.g, _color.b, (0.16f + 0.1f * pulse) * fade);
        Color edge = new Color(1f, 1f, 1f, (0.55f + 0.45f * pulse) * fade);
        if (_left != null) _left.color = edge;
        if (_right != null) _right.color = edge;
        if (_floor != null)
        {
            Color f = Color.Lerp(_color, Color.white, 0.5f);
            f.a = fade;
            _floor.color = f;
        }
    }
}
