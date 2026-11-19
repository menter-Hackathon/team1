using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】999+ warnings の1回分（2026-11-19）。XcodeWarningsAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 影（床と足場の上。すき間 2つ）→ 黄色い三角が一度に降る（足場もすり抜ける）→ 床で消える。
/// </summary>
public class XcodeWarningRain : CustomEffect
{
    const int Order = 21;

    AttackRunner _owner;
    AttackData _data;
    XcodeWarningsAttack _cfg;
    float _start;
    bool _built;
    bool _falling;
    Rect _arena;
    readonly List<SpriteRenderer> _shadows = new List<SpriteRenderer>();
    readonly List<Transform> _drops = new List<Transform>();

    public static XcodeWarningRain Spawn(AttackRunner owner, AttackData data, XcodeWarningsAttack cfg, float delay)
    {
        if (owner == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("XcodeWarnings", arena.center);
        var r = go.AddComponent<XcodeWarningRain>();
        r._owner = owner;
        r._data = data;
        r._cfg = cfg;
        r._arena = arena;
        r._start = Time.time + Mathf.Max(0f, delay);
        return r;
    }

    Vector2 L(float x, float y) => new Vector2(x - _arena.center.x, y - _arena.center.y);

    void Build()
    {
        _built = true;
        float half = _cfg.GapWidth * 0.5f;
        float px = XcodeArt.TargetOf(_owner).x;
        float lo = _arena.xMin + half, hi = _arena.xMax - half;
        float g1 = Mathf.Clamp(px + Random.Range(-(_cfg.Near - half), _cfg.Near - half), lo, hi);
        float g2 = g1;
        for (int t = 0; t < 30; t++)
        {
            float c = Random.Range(lo, hi);
            if (Mathf.Abs(c - g1) >= _cfg.GapWidth + 3f) { g2 = c; break; }
        }
        int n = Mathf.Max(1, Mathf.FloorToInt(_arena.width / _cfg.Column));
        float col = _arena.width / n;
        for (int i = 0; i < n; i++)
        {
            float x0 = _arena.xMin + i * col, x1 = x0 + col;
            if (Overlaps(x0, x1, g1, half) || Overlaps(x0, x1, g2, half)) continue;
            float cx = (x0 + x1) * 0.5f;
            _shadows.Add(XcodeArt.Shadow(transform, L(cx, _arena.yMin + 0.12f), col * 0.9f, 16));
            // 足場の上にも影
            if (_owner.TryFindGround(cx, _arena.yMax - 0.2f, out float gy) && gy > _arena.yMin + 0.5f)
                _shadows.Add(XcodeArt.Shadow(transform, L(cx, gy + 0.12f), col * 0.9f, 16));
            var drop = new GameObject("Warning").transform;
            drop.gameObject.layer = gameObject.layer;
            drop.SetParent(transform, false);
            drop.localPosition = L(cx, _arena.yMax + 0.6f);
            SpriteRenderer sr = RuntimeSprites.Show(drop, XcodeArt.Warning(), Vector2.zero, Order, Mathf.Min(1.1f, col * 18f / 16f), "Tri");
            sr.enabled = false;
            XcodeArt.AddHit(_owner, _data, drop, Vector2.zero, new Vector2(col * 0.8f, 0.7f));
            _drops.Add(drop);
        }
    }

    static bool Overlaps(float x0, float x1, float gap, float half) => x1 > gap - half && x0 < gap + half;

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        float tele = _cfg.Telegraph;
        if (e < tele)
        {
            bool on = XcodeArt.Blink(tele - e);
            foreach (var s in _shadows) if (s != null) s.enabled = on;
            return;
        }
        if (!_falling)
        {
            _falling = true;
            foreach (var s in _shadows) if (s != null) s.enabled = false;
            foreach (var d in _drops)
            {
                if (d == null) continue;
                foreach (var sr in d.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = true;
                var h = d.GetComponentInChildren<Hitbox>();
                if (h != null) h.Activate();
            }
        }
        float dy = _cfg.FallSpeed * Time.deltaTime;
        int alive = 0;
        for (int i = 0; i < _drops.Count; i++)
        {
            Transform d = _drops[i];
            if (d == null) continue;
            Vector3 p = d.localPosition;
            p.y -= dy;
            if (p.y + _arena.center.y <= _arena.yMin + 0.35f)
            {
                if (i % 4 == 0) FxKit.Ring((Vector2)d.position, 0.9f, 0.12f, XcodeArt.Yellow);
                Destroy(d.gameObject);
                _drops[i] = null;
                continue;
            }
            d.localPosition = p;
            alive++;
        }
        if (alive == 0) Destroy(gameObject);
    }
}

/// <summary>【仮】ボスの頭の上の 999+ のバッジ（2026-11-19）。時間が来たら消える。CustomEffect</summary>
public class XcodeWarningBadge : CustomEffect
{
    AttackRunner _owner;
    float _until;
    Transform _badge;

    public static XcodeWarningBadge Spawn(AttackRunner owner, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("XcodeWarningBadge", XcodeArt.HeadOf(owner));
        var b = go.AddComponent<XcodeWarningBadge>();
        b._owner = owner;
        b._until = Time.time + seconds;
        b._badge = go.transform;
        XcodeArt.Box(go.transform, Vector2.zero, new Vector2(2.0f, 0.75f), MacUi.Gray5, 40, "Bg");
        RuntimeSprites.Show(go.transform, XcodeArt.Warning(), new Vector2(-0.62f, 0f), 41, 0.8f, "Tri");
        XcodeArt.Text(go.transform, "999+", new Vector2(0.25f, 0f), 0.4f, XcodeArt.Yellow, 41);
        return b;
    }

    void Update()
    {
        if (Time.time >= _until) { Destroy(gameObject); return; }
        if (_owner != null) transform.position = XcodeArt.HeadOf(_owner);
    }
}
