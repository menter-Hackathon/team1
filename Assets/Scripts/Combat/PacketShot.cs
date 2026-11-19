using UnityEngine;

/// <summary>
/// 棒状のパケット1発（2026-11-19。ブラウザの能力）。PacketVolley が作る。
/// ・最初は撃った向きへ飛び、ロックオンした敵へ曲がっていく（敵が生きている間は、地形を通り抜ける）
/// ・ロックオンした敵がいなくなったら（倒し切った・ロックオンする敵がいなかった）、追尾をやめて重力で落ち、
///   床・足場・壁に当たったら火花を出して消える（2026-11-19）
/// ・相手に当たったら消える（ロックオンした敵以外にも当たる）。寿命が来ても消える
/// ・進む向きに回転する。後ろに短い残像が続く
/// ・「壊せる」パケット（ブラウザのボス）は、攻撃を1回当てると壊れる
/// </summary>
public class PacketShot : MonoBehaviour, IDamageable
{
    AttackRunner _owner;
    AttackData _data;
    PacketVolley _volley;
    SpriteRenderer _sr;
    Hitbox _hitbox;
    Vector2 _velocity;
    float _dieAt;
    float _nextGhost;
    bool _homing;
    bool _dead;
    bool _falling;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];
    /// <summary>追尾をやめたあとに落ちる重力（マス/秒²）</summary>
    const float FallGravity = 22f;

    public static PacketShot Spawn(AttackRunner owner, AttackData data, PacketVolley volley, Vector2 position, Vector2 direction, bool homing)
    {
        GameObject go = owner.CreateSpawnedObject($"Packet_{data.name}", position);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var p = go.AddComponent<PacketShot>();
        p._owner = owner;
        p._data = data;
        p._volley = volley;
        p._homing = homing;
        p._velocity = direction.normalized * data.Speed;
        p._dieAt = Time.time + data.Lifetime;
        p._sr = AttackRunner.AddVisual(go.transform, data.Sprite, data.Color, data.HitSize, 21, "Art");

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = data.HitSize;
        p._hitbox = owner.AddHitbox(go, data, col);
        p._hitbox.Hit += p.OnHit;
        p._hitbox.Activate();
        if (data.PacketDestructible) go.AddComponent<Hurtbox>(); // 攻撃で壊せる（被弾の窓口はこのパケット）
        p.Face();
        return p;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    public Team Team => _owner != null ? _owner.Team : Team.Enemy;

    /// <summary>壊せるパケット：攻撃を1回当てると壊れる</summary>
    public bool TryTakeDamage(in DamageInfo info)
    {
        if (_dead || _data == null || !_data.PacketDestructible || info.SourceTeam == Team || info.Amount <= 0) return false;
        Vfx.HitSpark(transform.position);
        Die();
        return true;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        Vfx.HitSpark(info.HitPoint);
        if (_data.DestroyOnHit) Die();
    }

    void Update()
    {
        if (_dead) return;
        if (Time.time >= _dieAt)
        {
            Die();
            return;
        }
        // ロックオンした敵がまだいれば、そこへ曲がっていく（地形は通り抜ける）
        bool targetAlive = _homing && _volley != null && _volley.Target != null && _volley.Target.enabled && _volley.Target.gameObject.activeInHierarchy;
        if (targetAlive)
        {
            Vector2 want = _volley.TargetPoint - (Vector2)transform.position;
            float maxTurn = _data.PacketTurnRate * Time.deltaTime;
            float angle = Vector2.SignedAngle(_velocity, want);
            _velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -maxTurn, maxTurn)) * _velocity;
        }
        else
        {
            // 敵がいない：追尾をやめて重力で落ちる。床・足場・壁に当たったら消える
            if (!_falling)
            {
                _falling = true;
                _dieAt = Mathf.Max(_dieAt, Time.time + 3f); // 空中で消えず、地面まで届くように寿命を延ばす
            }
            _velocity.y -= FallGravity * Time.deltaTime;
            Vector2 delta = _velocity * Time.deltaTime;
            float dist = delta.magnitude;
            if (dist > 0f && _owner != null)
            {
                var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
                filter.SetLayerMask(_owner.GroundMask | _owner.SolidMask);
                int n = Physics2D.Raycast(transform.position, delta / dist, filter, _hits, dist);
                for (int i = 0; i < n; i++)
                {
                    if (_hits[i].distance <= 0f) continue; // 始点が地形の中（足場の中を通っている途中）
                    // すり抜け床は、上から落ちてきたときだけ当たる
                    bool oneWay = (_owner.SolidMask.value & (1 << _hits[i].collider.gameObject.layer)) == 0;
                    if (oneWay && (_velocity.y > 0f || _hits[i].normal.y < 0.5f)) continue;
                    transform.position = _hits[i].point;
                    Vfx.HitSpark(_hits[i].point);
                    Die();
                    return;
                }
            }
        }
        transform.position += (Vector3)(_velocity * Time.deltaTime);
        Face();

        if (Time.time >= _nextGhost)
        {
            Ghost();
            _nextGhost = Time.time + 0.03f;
        }
    }

    void Face() => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg);

    void Ghost()
    {
        if (_sr == null || _sr.sprite == null) return;
        var go = new GameObject("PacketGhost");
        go.transform.SetPositionAndRotation(_sr.transform.position, _sr.transform.rotation);
        go.transform.localScale = _sr.transform.lossyScale;
        var g = go.AddComponent<SpriteRenderer>();
        g.sprite = _sr.sprite;
        g.sortingOrder = _sr.sortingOrder - 1;
        Color c = _sr.color;
        c.a *= 0.35f;
        g.color = c;
        go.AddComponent<FadeAndDestroy>().Setup(0.12f);
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        if (_hitbox != null) _hitbox.Deactivate();
        Destroy(gameObject);
    }
}
