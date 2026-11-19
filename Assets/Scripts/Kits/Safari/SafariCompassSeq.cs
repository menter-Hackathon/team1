using UnityEngine;

/// <summary>
/// 【仮】コンパスの進行（2026-11-19）。SafariCompassAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// ボスの頭の上に文字盤。1回ごとに：針が回る（水色の線が追う）→ 止まって赤い線 → 針を撃つ。
/// </summary>
public class SafariCompassSeq : CustomEffect
{
    const int Order = 24;

    AttackRunner _owner;
    SafariCompassAttack _cfg;
    float _start;
    Transform _dial;
    SpriteRenderer _needle;
    SpriteRenderer _track;
    int _cycle = -1;
    bool _locked;
    Vector2 _dir = Vector2.left;
    Vector2 _from;

    public static SafariCompassSeq Spawn(AttackRunner owner, SafariCompassAttack cfg)
    {
        if (owner == null || cfg == null || cfg.Bullet == null) return null;
        GameObject go = owner.CreateSpawnedObject("SafariCompass", SafariArt.HeadOf(owner));
        var s = go.AddComponent<SafariCompassSeq>();
        s._owner = owner;
        s._cfg = cfg;
        s._start = Time.time;
        s.Build();
        return s;
    }

    void Build()
    {
        _dial = transform;
        RuntimeSprites.Show(_dial, SafariArt.Dial(), Vector2.zero, Order, 1.2f, "Dial");
        _needle = RuntimeSprites.Show(_dial, SafariArt.Needle(), Vector2.zero, Order + 1, 1.0f, "Needle");
        _track = SafariArt.Box(_dial, Vector2.zero, Vector2.one, SafariArt.A(SafariArt.Cyan, 0.7f), Order - 1, "Track");
        _track.enabled = false;
    }

    Vector2 Origin => transform.position;

    Vector2 TargetDir()
    {
        if (_owner != null && _owner.TargetPoint is Vector2 t && (t - Origin).sqrMagnitude > 0.0001f) return (t - Origin).normalized;
        return _dir;
    }

    void Update()
    {
        if (_owner != null) transform.position = SafariArt.HeadOf(_owner);
        float cycleLen = _cfg.Spin + _cfg.Telegraph;
        float e = Time.time - _start;
        int c = Mathf.FloorToInt(e / cycleLen);
        if (c != _cycle)
        {
            if (_cycle >= 0) Fire(); // 前の回の予告が終わった
            if (c >= _cfg.Count)
            {
                Destroy(gameObject);
                return;
            }
            _cycle = c;
            _locked = false;
        }
        float t = e - c * cycleLen;
        if (t < _cfg.Spin)
        {
            // 針が回る。水色の線がプレイヤーを追う（まだ安全）
            _needle.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 900f);
            Vector2 d = TargetDir();
            _track.enabled = true;
            SafariArt.SetLine(_track, Origin, Origin + d * 30f, 1f);
            return;
        }
        if (!_locked)
        {
            _locked = true;
            _track.enabled = false;
            _dir = TargetDir();
            _from = Origin;
            _needle.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
            KitDress.Line(_owner, _from, _from + _dir * 30f, _cfg.Telegraph);
            if (_cfg.Both) KitDress.Line(_owner, _from, _from - _dir * 30f, _cfg.Telegraph);
        }
    }

    void Fire()
    {
        if (_owner == null || !_locked) return;
        AttackData b = _cfg.Bullet;
        Projectile p = Projectile.Spawn(_owner, b, _from, _dir, _cycle * 2);
        SafariArt.Dress(p, SafariArt.RedNeedle(), _dir);
        if (_cfg.Both)
        {
            Projectile q = Projectile.Spawn(_owner, b, _from, -_dir, _cycle * 2 + 1);
            SafariArt.Dress(q, SafariArt.WhiteNeedle(), -_dir);
        }
        _owner.NotifyReleased(b);
    }
}
