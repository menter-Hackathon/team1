using UnityEngine;

/// <summary>
/// 弾（部品 A）。AttackRunner が AttackData から作る。
/// ・決まった速さで飛び、重力があれば落ちていく。進む向きに回転する
/// ・寿命が来たら消える。地形（床・壁）に当たったら消える／追撃を出す。相手に当たったら消える（設定による）
/// ・すり抜け床（フォルダ）は通り抜ける
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Vector2 _velocity;
    float _dieAt;
    float _castRadius;
    bool _dead;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

    public AttackData Data => _data;
    public Vector2 Velocity => _velocity;
    public AttackRunner Owner => _owner;

    /// <summary>弾を1発作る</summary>
    public static Projectile Spawn(AttackRunner owner, AttackData data, Vector2 position, Vector2 direction)
    {
        GameObject go = owner.CreateSpawnedObject($"Projectile_{data.name}", position);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = data.HitSize;

        AttackRunner.AddVisual(go.transform, data.Sprite, data.Color, data.HitSize);

        var p = go.AddComponent<Projectile>();
        p._owner = owner;
        p._data = data;
        p._rb = rb;
        p._velocity = direction.normalized * data.Speed;
        p._dieAt = Time.time + data.Lifetime;
        p._castRadius = Mathf.Max(0.02f, Mathf.Min(data.HitSize.x, data.HitSize.y) * 0.5f);
        p._hitbox = owner.AddHitbox(go, data, col);
        p._hitbox.Hit += p.OnHit;
        p._hitbox.Activate();
        p.FaceVelocity();
        return p;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        if (_data.DestroyOnHit) Die();
    }

    void FixedUpdate()
    {
        if (_dead) return;
        if (Time.time >= _dieAt)
        {
            Die();
            return;
        }

        float dt = Time.fixedDeltaTime;
        _velocity.y -= _data.Gravity * dt;
        Vector2 pos = _rb.position;
        Vector2 delta = _velocity * dt;

        // 進む先に地形があるか調べる（すり抜け床は通る）
        float dist = delta.magnitude;
        if (dist > 0f && TryHitTerrain(pos, delta / dist, dist, out RaycastHit2D hit))
        {
            if (_data.OnTerrainHit != null) _owner.SpawnFollowUp(_data.OnTerrainHit, hit.point, hit.normal);
            if (_data.DestroyOnTerrain)
            {
                _rb.position = hit.centroid;
                Die();
                return;
            }
        }

        _rb.MovePosition(pos + delta);
        FaceVelocity();
    }

    bool TryHitTerrain(Vector2 pos, Vector2 dir, float distance, out RaycastHit2D result)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.SolidMask);
        int count = Physics2D.CircleCast(pos, _castRadius, dir, filter, _hits, distance);
        result = count > 0 ? _hits[0] : default;
        return count > 0;
    }

    void FaceVelocity()
    {
        if (_velocity.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        if (_hitbox != null) _hitbox.Deactivate();
        Destroy(gameObject);
    }
}
