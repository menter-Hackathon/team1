using UnityEngine;

/// <summary>
/// 【仮】能力 ハンマーの1回分（2026-11-19）。XcodeHammerSkill が出し、あとは自分で進む。
/// ため（プレイヤーを止める。ハンマーを上にかまえる）→ ふり下ろす（正面の箱の判定）→ 消える。止めた操作は、消えるときに必ず戻す。
/// </summary>
public class XcodeHammerSwing : MonoBehaviour
{
    const int Order = 23;
    const float SwingTime = 0.08f;
    const float StayTime = 0.25f;

    AttackRunner _owner;
    AttackData _data;
    XcodeHammerSkill _cfg;
    PlayerController _pc;
    bool _locked;
    bool _prevControl;
    float _start;
    bool _swung;
    int _dir;
    SpriteRenderer _hammer;
    Hitbox _hit;
    Vector2 _center;

    public static XcodeHammerSwing Spawn(AttackRunner owner, AttackData data, XcodeHammerSkill cfg)
    {
        if (owner == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("XcodeHammerSwing", owner.transform.position);
        var s = go.AddComponent<XcodeHammerSwing>();
        s._owner = owner;
        s._data = data;
        s._cfg = cfg;
        s._start = Time.time;
        s._dir = owner.Facing >= 0 ? 1 : -1;
        s.Begin();
        return s;
    }

    void Begin()
    {
        _pc = _owner.GetComponent<PlayerController>();
        if (_pc != null && _cfg.Charge > 0f)
        {
            _prevControl = _pc.CanControl;
            _pc.CanControl = false;
            _pc.SetVelocity(new Vector2(0f, _pc.Velocity.y));
            _locked = true;
        }
        _hammer = RuntimeSprites.Show(transform, XcodeArt.SkillHammer(), Vector2.zero, Order, 1.1f, "Hammer");
        _hammer.flipX = _dir < 0;
        Follow(0f);
    }

    /// <summary>体の前の、判定の真ん中</summary>
    Vector2 HitCenter()
    {
        Vector2 feet = _owner.transform.position;
        float halfW = 0.35f;
        foreach (var col in _owner.GetComponents<Collider2D>())
        {
            if (col.isTrigger || !col.enabled) continue;
            feet = new Vector2(col.bounds.center.x, col.bounds.min.y);
            halfW = col.bounds.extents.x;
            break;
        }
        return new Vector2(feet.x + _dir * (halfW + _cfg.Size.x * 0.5f), feet.y + _cfg.Size.y * 0.5f);
    }

    // ためている間：ハンマーを体の後ろの上にかまえる（少しふるえる）
    void Follow(float k)
    {
        Vector2 body = RewardKit.BodyCenter(_owner.transform);
        float shake = Mathf.Sin(Time.time * 60f) * 0.04f * k;
        transform.position = body + new Vector2(-_dir * 0.3f + shake, 1.0f);
        _hammer.transform.localPosition = Vector2.zero;
        _hammer.transform.localRotation = Quaternion.Euler(0f, 0f, _dir > 0 ? 100f : -100f);
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < _cfg.Charge)
        {
            if (_owner == null) { Destroy(gameObject); return; }
            Follow(e / Mathf.Max(0.01f, _cfg.Charge));
            return;
        }
        if (!_swung) Swing();
        float s = e - _cfg.Charge;
        if (s < SwingTime)
        {
            float k = s / SwingTime;
            float ang = Mathf.Lerp(100f, -15f, k);
            _hammer.transform.localRotation = Quaternion.Euler(0f, 0f, _dir > 0 ? ang : -ang);
            return;
        }
        if (s >= _cfg.HitTime && _hit != null && _hit.IsActive) _hit.Deactivate();
        float f = (s - SwingTime) / StayTime;
        if (f >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        _hammer.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((f - 0.4f) / 0.6f));
    }

    void Swing()
    {
        _swung = true;
        Unlock();
        _center = HitCenter();
        // 判定は体の前の箱。ハンマーの絵はその上へ動かす
        transform.position = _center;
        _hit = XcodeArt.AddHit(_owner, _data, transform, Vector2.zero, _cfg.Size);
        _hit.Hit += OnHit;
        _hit.Activate();
        _hammer.transform.localPosition = new Vector2(-_dir * 0.4f, 0.2f);
        Vector2 ground = new Vector2(_center.x, _center.y - _cfg.Size.y * 0.5f + 0.2f);
        FxKit.Ring(ground, 2.6f, 0.18f, Color.white);
        FxKit.Ring(ground, 1.6f, 0.15f, XcodeArt.Blue, 0.05f);
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        // とどめ：ボスのゲージがいっぱいになった
        if (target is VirusGauge g && g.IsFull && g.GetComponent<BossController>() != null)
            KitToast.Show("Build Succeeded", null, XcodeArt.Green);
    }

    void Unlock()
    {
        if (!_locked) return;
        _locked = false;
        if (_pc != null && !_pc.CanControl && _prevControl) _pc.CanControl = true;
    }

    void OnDestroy()
    {
        Unlock();
        if (_hit != null) _hit.Hit -= OnHit;
    }
}
