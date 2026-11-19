using UnityEngine;

/// <summary>
/// 【仮】Simulator の iPhone 1台（2026-11-19）。XcodeSimulatorAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 影（予告）→ 落ちる（落ちている間だけ当たる）→ 立つ（KitProp。近接1回でこわれる）→ 画面が点いて赤い線 → 撃つ → 画面が消えて引っ込む。
/// こわされたら、そこで終わる（撃っていなければ撃たない）。
/// </summary>
public class XcodeSimulatorPhone : CustomEffect
{
    const int Order = 15;
    const float FallTime = 0.2f;
    const float FallHeight = 9f;
    const float LightDelay = 0.25f;
    const float ShotGap = 0.25f;
    const float OffTime = 0.3f;
    const float RetractTime = 0.3f;

    AttackRunner _owner;
    AttackData _data;
    XcodeSimulatorAttack _cfg;
    float _start;
    float _x;
    float _floor;
    bool _built;
    SpriteRenderer _shadow;
    SpriteRenderer _falling;
    Hitbox _fallHit;
    KitProp _prop;
    bool _landed;
    bool _lit;
    bool _broken;
    int _fired;
    Vector2 _aimDir;

    /// <summary>1台の長さ（秒）</summary>
    public static float LengthOf(XcodeSimulatorAttack cfg)
        => cfg.Telegraph + FallTime + LightDelay + cfg.AimTime + (cfg.Shots - 1) * ShotGap + OffTime + RetractTime;

    public static XcodeSimulatorPhone Spawn(AttackRunner owner, AttackData data, XcodeSimulatorAttack cfg, float x, float delay)
    {
        if (owner == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("XcodeSimulator", new Vector2(x, arena.yMin));
        var p = go.AddComponent<XcodeSimulatorPhone>();
        p._owner = owner;
        p._data = data;
        p._cfg = cfg;
        p._x = x;
        p._floor = arena.yMin;
        p._start = Time.time + Mathf.Max(0f, delay);
        return p;
    }

    void Build()
    {
        _built = true;
        _shadow = XcodeArt.Shadow(transform, new Vector2(0f, 0.12f), _cfg.Size.x, 16);
    }

    void Update()
    {
        if (_broken) return;
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        float tele = _cfg.Telegraph;
        Vector2 size = _cfg.Size;
        if (e < tele)
        {
            if (_shadow != null) _shadow.enabled = XcodeArt.Blink(tele - e);
            return;
        }
        float f = e - tele;
        if (f < FallTime)
        {
            if (_falling == null)
            {
                _falling = RuntimeSprites.Show(transform, XcodeArt.Phone(false), Vector2.zero, Order, 1f, "Falling");
                _fallHit = XcodeArt.AddHit(_owner, _data, transform, Vector2.zero, size * 0.9f);
                _fallHit.Activate();
            }
            float y = Mathf.Lerp(FallHeight, 0f, f / FallTime) + size.y * 0.5f;
            _falling.transform.localPosition = new Vector2(0f, y);
            _fallHit.transform.localPosition = new Vector2(0f, y);
            return;
        }
        if (!_landed) Land();
        if (_prop == null) { Destroy(gameObject); return; } // 外から消された
        if (_prop.IsBroken) return;
        float l = f - FallTime;
        if (l < LightDelay) return;
        if (!_lit)
        {
            _lit = true;
            _prop.SetSprite(XcodeArt.Phone(true));
            Vector2 screen = ScreenPoint();
            Vector2 target = XcodeArt.TargetOf(_owner);
            _aimDir = (target - screen).sqrMagnitude > 0.0001f ? (target - screen).normalized : Vector2.left;
            KitDress.Line(_owner, screen, screen + _aimDir * 30f, _cfg.AimTime);
        }
        float a = l - LightDelay - _cfg.AimTime;
        while (_fired < _cfg.Shots && a >= _fired * ShotGap)
        {
            Fire();
            _fired++;
        }
        float end = (_cfg.Shots - 1) * ShotGap;
        if (a < end + OffTime) return;
        if (a < end + OffTime + RetractTime)
        {
            _prop.SetSprite(XcodeArt.Phone(false));
            float k = (a - end - OffTime) / RetractTime;
            _prop.transform.position = new Vector2(_x, _floor + size.y * 0.5f - k * size.y);
            return;
        }
        Destroy(gameObject);
    }

    void Land()
    {
        _landed = true;
        if (_falling != null) Destroy(_falling.gameObject);
        if (_fallHit != null) Destroy(_fallHit.gameObject);
        if (_shadow != null) _shadow.enabled = false;
        Vector2 size = _cfg.Size;
        _prop = KitProp.Spawn(_owner, new Vector2(_x, _floor + size.y * 0.5f), XcodeArt.Phone(false), size, OnBroken, 1, 0f, Order);
        FxKit.Ring(new Vector2(_x, _floor + 0.2f), 2.2f, 0.15f, Color.white);
    }

    Vector2 ScreenPoint() => _prop != null ? (Vector2)_prop.transform.position + new Vector2(0f, 0.4f) : new Vector2(_x, _floor + 2f);

    void Fire()
    {
        AttackData b = _cfg.Bullet;
        if (b == null || _owner == null) return;
        Projectile p = Projectile.Spawn(_owner, b, ScreenPoint(), _aimDir, _fired);
        if (p != null && KitDress.Swap(p.gameObject, XcodeArt.Notification()) && _aimDir.x < 0f)
        {
            SpriteRenderer sr = KitDress.FindVisual(p.transform);
            if (sr != null) sr.flipY = true;
        }
        _owner.NotifyReleased(b);
    }

    void OnBroken(KitProp prop)
    {
        _broken = true;
        Vector2 c = prop != null ? (Vector2)prop.transform.position : new Vector2(_x, _floor + 2f);
        FxKit.Ring(c, 2.4f, 0.2f, XcodeArt.Screen);
        FxKit.Text("Quit", c + Vector2.up * 2.4f, 0.45f, Color.white, 30, 0.6f, XcodeArt.Red).Rise(0.4f);
        Destroy(gameObject, 0.05f);
    }

    protected override void Restore()
    {
        if (_prop != null && !_prop.IsBroken) Destroy(_prop.gameObject);
        _prop = null;
    }
}
