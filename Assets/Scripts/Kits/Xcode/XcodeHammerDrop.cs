using UnityEngine;

/// <summary>
/// 【仮】Build のハンマー1回分（2026-11-19）。XcodeHammerAttack が出し、あとは自分で進む。
/// 待つ → 影とハンマー（予告）→ ふり下ろす（柱の判定 0.15秒）→ 左右へ衝撃（弾）→ うすくなって消える。CustomEffect（形態変化で消える）。
/// </summary>
public class XcodeHammerDrop : CustomEffect
{
    const int Order = 22;
    const float Scale = 1.25f;
    const float RaiseY = 6.5f;
    const float SlamTime = 0.08f;
    const float HitTime = 0.15f;
    const float StayTime = 0.4f;

    AttackRunner _owner;
    AttackData _data;
    XcodeHammerAttack _cfg;
    float _start;
    bool _built;
    bool _slammed;
    bool _shocked;
    float _floor;
    SpriteRenderer _hammer;
    SpriteRenderer _shadow;
    Hitbox _hit;
    float _halfH;

    public static XcodeHammerDrop Spawn(AttackRunner owner, AttackData data, XcodeHammerAttack cfg, float delay)
    {
        if (owner == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("XcodeHammer", owner.OriginPoint);
        var d = go.AddComponent<XcodeHammerDrop>();
        d._owner = owner;
        d._data = data;
        d._cfg = cfg;
        d._start = Time.time + Mathf.Max(0f, delay);
        return d;
    }

    void Build()
    {
        _built = true;
        Rect arena = BattleArena.GetBounds();
        float half = _cfg.Width * 0.5f;
        float x = Mathf.Clamp(XcodeArt.TargetOf(_owner).x, arena.xMin + half, arena.xMax - half);
        _floor = arena.yMin;
        transform.position = new Vector2(x, _floor);
        _shadow = XcodeArt.Shadow(transform, new Vector2(0f, 0.12f), _cfg.Width, 16);
        _hammer = RuntimeSprites.Show(transform, XcodeArt.Hammer(), Vector2.zero, Order, Scale, "Hammer");
        _halfH = _hammer.sprite.bounds.size.y * Scale * 0.5f;
        _hammer.transform.localPosition = new Vector2(0f, RaiseY + _halfH);
        _hit = XcodeArt.AddHit(_owner, _data, transform, new Vector2(0f, (RaiseY + 2f) * 0.5f), new Vector2(_cfg.Width, RaiseY + 2f));
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        float tele = _cfg.Telegraph;
        if (e < tele)
        {
            // 少し持ち上がる（ふりかぶる）
            float k = e / tele;
            _hammer.transform.localPosition = new Vector2(0f, RaiseY + _halfH + 0.5f * Mathf.Sin(k * Mathf.PI * 0.5f));
            _shadow.enabled = XcodeArt.Blink(tele - e);
            return;
        }
        float s = e - tele;
        if (!_slammed)
        {
            _slammed = true;
            _hit.Activate();
            _shadow.enabled = true;
        }
        if (s < SlamTime)
        {
            float k = s / SlamTime;
            _hammer.transform.localPosition = new Vector2(0f, Mathf.Lerp(RaiseY + 0.5f, 0f, k * k) + _halfH);
            return;
        }
        _hammer.transform.localPosition = new Vector2(0f, _halfH);
        if (!_shocked)
        {
            _shocked = true;
            Shockwaves();
        }
        if (s >= SlamTime + HitTime && _hit != null && _hit.IsActive) _hit.Deactivate();
        float f = (s - SlamTime) / StayTime;
        if (f >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Color c = Color.white;
        c.a = 1f - Mathf.Clamp01((f - 0.5f) * 2f);
        _hammer.color = c;
        _shadow.color = XcodeArt.A(XcodeArt.Red, 0.6f * c.a);
    }

    void Shockwaves()
    {
        Vector2 p = transform.position;
        FxKit.Ring(p + Vector2.up * 0.3f, 2.4f, 0.2f, Color.white);
        AttackData shock = _cfg.Shock;
        if (shock == null || _owner == null) return;
        float half = _cfg.Width * 0.5f;
        for (int dir = -1; dir <= 1; dir += 2)
        {
            Vector2 from = new Vector2(p.x + dir * half * 0.6f, _floor + shock.HitSize.y * 0.5f);
            Projectile proj = Projectile.Spawn(_owner, shock, from, new Vector2(dir, 0f), dir < 0 ? 0 : 1);
            if (proj != null && KitDress.Swap(proj.gameObject, XcodeArt.Shock()) && dir < 0)
            {
                SpriteRenderer sr = KitDress.FindVisual(proj.transform);
                if (sr != null) sr.flipY = true; // 左向きは弾が 180°回るので、上下を戻す
            }
        }
        _owner.NotifyReleased(shock);
    }
}
