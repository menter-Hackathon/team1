using System;
using UnityEngine;

/// <summary>
/// 【仮】区画1つ（2026-11-19。docs/app-kits-1-design.md E6。ZoneStrike が作る）。担当3 の物。
/// ・打つ区画：delay 秒待つ → 赤い予告（PaceZoneMark）telegraph 秒 → 判定（AreaStrike）active 秒 → 消える
/// ・安全な区画：delay 秒待つ → 色を塗る（判定なし）seconds 秒 → 消える
/// ・CustomEffect なので、ボスの形態変化（50％）でも消える。予告と判定は子なので、いっしょに消える
/// </summary>
public class ZoneStrikeZone : CustomEffect
{
    const int SafeOrder = 15;

    /// <summary>判定が出た瞬間（絵を足したいとき）</summary>
    public event Action<ZoneStrikeZone> Fired;

    /// <summary>区画</summary>
    public Rect Zone { get; private set; }
    /// <summary>安全な区画か（判定なし）</summary>
    public bool IsSafe { get; private set; }
    /// <summary>予告を始める時刻</summary>
    public float TelegraphAt { get; private set; }
    /// <summary>判定が出る時刻（安全な区画は色を塗る時刻）</summary>
    public float FireAt { get; private set; }
    /// <summary>消える時刻</summary>
    public float EndAt { get; private set; }
    /// <summary>判定（安全な区画は null）</summary>
    public AreaStrike Strike { get; private set; }
    /// <summary>判定が出ているか</summary>
    public bool IsHitActive => Strike != null && Strike.IsHitActive;

    float _active;
    bool _markShown;
    bool _fired;
    SpriteRenderer _safeFill;
    SpriteRenderer _safeEdge;
    Color _safeColor;

    internal void SetupStrike(AttackRunner owner, AttackData sized, Rect zone, float delay, float telegraph, float active, Sprite fill, Color? fillColor)
    {
        Zone = zone;
        float now = Time.time;
        TelegraphAt = now + delay;
        FireAt = TelegraphAt + telegraph;
        EndAt = FireAt + active;
        _active = active;
        // 判定は今作り、予告の終わりまで待たせる（AreaStrike の予告の赤丸は使わない）
        Strike = AreaStrike.Spawn(owner, sized, zone.center, Vector2.up, delay + telegraph);
        if (Strike != null)
        {
            Strike.transform.SetParent(transform, true);
            if (fill != null) KitDress.Swap(Strike.gameObject, fill, fillColor);
            else if (fillColor.HasValue)
            {
                SpriteRenderer v = KitDress.FindVisual(Strike.transform);
                if (v != null) v.color = fillColor.Value;
            }
        }
        if (telegraph <= 0f) _markShown = true;
        Tick();
    }

    internal void SetupSafe(Rect zone, float delay, float seconds, Color color)
    {
        Zone = zone;
        IsSafe = true;
        TelegraphAt = Time.time + delay;
        FireAt = TelegraphAt;
        EndAt = FireAt + seconds;
        _markShown = true;
        _safeColor = color;
        transform.position = new Vector3(zone.center.x, zone.yMin, 0f);
        _safeFill = FxKit.Box(transform, new Vector2(0f, zone.height * 0.5f), zone.size, new Color(color.r, color.g, color.b, 0.22f), SafeOrder, "SafeFill");
        _safeEdge = FxKit.Box(transform, new Vector2(0f, zone.height - FxKit.Pixel), new Vector2(zone.width, FxKit.Pixel * 2f), new Color(color.r, color.g, color.b, 0.9f), SafeOrder + 1, "SafeEdge");
        _safeFill.enabled = _safeEdge.enabled = delay <= 0f;
        Tick();
    }

    void Update() => Tick();

    void Tick()
    {
        float now = Time.time;
        if (now >= EndAt + 0.05f)
        {
            Destroy(gameObject);
            return;
        }
        if (IsSafe)
        {
            bool on = now >= FireAt;
            if (_safeFill != null)
            {
                _safeFill.enabled = on;
                // ゆっくり明るさを変える（「ここが安全」）
                float a = 0.18f + 0.08f * Mathf.Sin(now * 6f);
                _safeFill.color = new Color(_safeColor.r, _safeColor.g, _safeColor.b, a);
            }
            if (_safeEdge != null) _safeEdge.enabled = on;
            return;
        }
        if (!_markShown && now >= TelegraphAt)
        {
            _markShown = true;
            PaceZoneMark mark = PaceZoneMark.Show(null, Zone.xMin, Zone.xMax, Zone.yMin, Zone.height, Mathf.Max(0.05f, FireAt - now), _active);
            if (mark != null) mark.transform.SetParent(transform, true);
        }
        if (!_fired && now >= FireAt)
        {
            _fired = true;
            GameAudio.Event("zone_fire");
            try { Fired?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>この区画をやめる（予告も判定も消す）</summary>
    public void Cancel() => Destroy(gameObject);
}
