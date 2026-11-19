using UnityEngine;

/// <summary>
/// 弾（部品 A）。AttackRunner が AttackData から作る。
/// ・決まった速さで飛び、重力があれば落ちていく。進む向きに回転する
/// ・寿命が来たら消える。地形（床・壁）に当たったら消える／追撃を出す。相手に当たったら消える（設定による）
/// ・すり抜け床（フォルダ）は通り抜ける
/// ・【仮】敵の弾は、盾ウィンドウ（AttackRunner の「弾だけを止める物」）に当たると消える。追撃は出さない
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
    bool _landed;     // 着地をスキンに知らせたか（1回だけ）
    int _index;       // 何発目か（スキンの絵の番号）
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

    public AttackData Data => _data;
    public Vector2 Velocity => _velocity;
    public AttackRunner Owner => _owner;
    /// <summary>地形（床・壁）に当たったとき（弾, 当たった位置）。見た目用（2026-11-18：文字列の雨の字がはねる）</summary>
    public event System.Action<Projectile, Vector2> HitTerrain;

    /// <summary>弾を1発作る。index：何発目か（連射・扇の番号。スキンの絵に使う。2026-11-18）</summary>
    public static Projectile Spawn(AttackRunner owner, AttackData data, Vector2 position, Vector2 direction, int index = 0)
    {
        GameObject go = owner.CreateSpawnedObject($"Projectile_{data.name}", position);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = data.HitSize;

        SpriteRenderer visual = AttackRunner.AddVisual(go.transform, data.Sprite, data.Color, data.HitSize);

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
        p._index = index;
        // スキンの絵（撃った瞬間＝本体）。付けたら今の絵をかくす（2026-11-18：スキルの見た目）
        SkinDresser.Dress(owner, data, go, DressPhase.Active, visual, data.HitSize, index, direction.normalized,
            Mathf.Max(1, data.ProjectileCount) * Mathf.Max(1, data.RepeatCount), data.Lifetime);
        return p;
    }

    /// <summary>スキンに場面を知らせる（着地・消える）</summary>
    void NotifySkin(DressPhase phase)
    {
        if (_data == null) return;
        Vector2 dir = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.zero;
        SkinDresser.Dress(_owner, _data, gameObject, phase, null, _data.HitSize, _index, dir);
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        if (_dead) return;
        // 相手に当たったときも追撃を出す（ボムの爆発）
        if (_data.FollowUpOnHit && _data.OnTerrainHit != null) _owner.SpawnFollowUp(_data.OnTerrainHit, _rb.position, Vector2.up);
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
        if (dist > 0f)
        {
            Vector2 dir = delta / dist;
            bool hitTerrain = TryHitTerrain(pos, dir, dist, out RaycastHit2D hit);

            // 【仮】弾だけを止める物（盾ウィンドウ）が地形より手前にあれば、そこで消える。
            // 地形ではないので、追撃（赤い波線など）は出さない。「地形に当たったら消えるか」の設定にも関係なく消える
            if (TryHitBlocker(pos, dir, hitTerrain ? hit.distance : dist, out RaycastHit2D block))
            {
                _rb.position = block.centroid;
                Die();
                return;
            }

            if (hitTerrain)
            {
                if (!_landed && HitTerrain != null)
                {
                    try { HitTerrain(this, hit.point); }
                    catch (System.Exception e) { Debug.LogWarning($"弾の HitTerrain で例外：{e}"); } // 見た目の例外で、弾の動きを止めない
                }
                if (_data.OnTerrainHit != null) _owner.SpawnFollowUp(_data.OnTerrainHit, hit.point, hit.normal);
                if (_data.DestroyOnTerrain)
                {
                    _rb.position = hit.centroid;
                    transform.position = hit.centroid; // スキンの着地の絵を、当たった所に出す
                    if (!_landed)
                    {
                        _landed = true;
                        NotifySkin(DressPhase.Landed);
                    }
                    Die();
                    return;
                }
                if (!_landed)
                {
                    _landed = true;
                    NotifySkin(DressPhase.Landed);
                }
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

    /// <summary>
    /// 【仮】弾だけを止める物に当たるか調べる。持ち主の AttackRunner に止める物のレイヤーが無ければ（プレイヤーの弾）、何もしない。
    /// 盾ウィンドウなら、当たった数を数えてもらう（割れている盾は通り抜ける）
    /// </summary>
    bool TryHitBlocker(Vector2 pos, Vector2 dir, float distance, out RaycastHit2D result)
    {
        result = default;
        int mask = _owner.ProjectileBlockerMask.value;
        if (mask == 0) return false;

        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(mask);
        int count = Physics2D.CircleCast(pos, _castRadius, dir, filter, _hits, distance);
        for (int i = 0; i < count; i++)
        {
            var shield = _hits[i].collider.GetComponentInParent<ShieldWindow>();
            if (shield != null && !shield.TryBlock(this)) continue;
            result = _hits[i];
            return true;
        }
        return false;
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
        NotifySkin(DressPhase.Vanish); // スキンの消える絵（例外はスキンの入口で止める）
        Destroy(gameObject);
    }
}
