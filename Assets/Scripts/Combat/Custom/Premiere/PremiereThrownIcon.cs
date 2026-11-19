using UnityEngine;

/// <summary>
/// 投げたエフェクトアイコン（能力「エフェクトアイコン投げ」の弾。2026-11-18。docs/app-boss-skills-design.md 3-5）。
/// 飛び方は今ある弾（Projectile）と同じ：決まった速さで投げ、重力で落ちる。すり抜け床（フォルダ）は通り抜ける。
/// 床・壁（AttackRunner の「弾が当たって止まる地形」）か相手に当たったら、アイコンの効果（PremiereFx.IconEffect）を出して消える。
/// 直撃にも判定がある（ダメージ・ゲージは能力の攻撃データの共通の欄）。寿命が来たら、効果を出さずに消える
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PremiereThrownIcon : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _effect;
    PremiereIcon _kind;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Transform _visual;
    Vector2 _velocity;
    float _gravity;
    float _dieAt;
    float _castRadius;
    float _spin;
    bool _dead;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

    public PremiereIcon Kind => _kind;

    /// <summary>
    /// 1つ投げる。direct は直撃の攻撃データ（ダメージ・ゲージ・ノックバック）、effect は当たった所に出す効果の攻撃データ
    /// </summary>
    public static PremiereThrownIcon Spawn(AttackRunner owner, AttackData direct, PremiereIcon kind, AttackData effect,
        Vector2 position, Vector2 velocity, float gravity, float hitSize, float visualSize, float lifetime, float spin)
    {
        GameObject go = owner.CreateSpawnedObject($"PremiereThrownIcon_{kind}", position);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one * hitSize;

        var icon = go.AddComponent<PremiereThrownIcon>();
        icon._owner = owner;
        icon._effect = effect;
        icon._kind = kind;
        icon._rb = rb;
        icon._velocity = velocity;
        icon._gravity = gravity;
        icon._dieAt = Time.time + lifetime;
        icon._castRadius = Mathf.Max(0.02f, hitSize * 0.5f);
        icon._spin = spin;
        // 絵は回すので、判定（親）とは別の子にする
        SpriteRenderer visual = RuntimeSprites.Show(go.transform, PremiereArt.Icon(kind), Vector2.zero, 21, visualSize, "Icon");
        icon._visual = visual.transform;
        PremiereSkinTrail.Attach(go, visual); // 後ろに同じアイコンのうすい残像 2枚（12-1。2026-11-18）
        icon._hitbox = owner.AddHitbox(go, direct, col);
        icon._hitbox.SetErrorKind("IconThrow_" + kind); // 当たった絵はアイコンごと（PremiereSkin）
        icon._hitbox.Hit += icon.OnHit;
        icon._hitbox.Activate();
        return icon;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        // 相手に当たった：その場に効果（面の向きは無いので、炎・もやは当たった所が真ん中）
        Burst(_rb.position, Vector2.zero);
    }

    void Update()
    {
        if (_visual != null && !_dead) _visual.Rotate(0f, 0f, _spin * Time.deltaTime);
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
        _velocity.y -= _gravity * dt;
        Vector2 pos = _rb.position;
        Vector2 delta = _velocity * dt;
        float dist = delta.magnitude;
        if (dist > 0f && _owner != null)
        {
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(_owner.SolidMask);
            int count = Physics2D.CircleCast(pos, _castRadius, delta / dist, filter, _hits, dist);
            if (count > 0)
            {
                _rb.position = _hits[0].centroid;
                Burst(_hits[0].point, _hits[0].normal);
                return;
            }
        }
        _rb.MovePosition(pos + delta);
    }

    /// <summary>効果を出して消える（1回だけ）</summary>
    void Burst(Vector2 point, Vector2 normal)
    {
        if (_dead) return;
        if (_owner != null && _effect != null)
        {
            AreaStrike strike = PremiereFx.IconEffect(_owner, _effect, _kind, point, normal);
            // 効果の判定にも、投げたアイコンと同じスキン（Premiere）を入れる（当たった絵がアイコンごとになるように。2026-11-18）。
            // 効果の攻撃データは能力として覚えていないので、ほかの能力を使ったあとだと、別のスキンになってしまうため
            if (strike != null && strike.TryGetComponent(out Hitbox hb))
            {
                SkinId skin = _hitbox != null && _hitbox.Skin != SkinId.None ? _hitbox.Skin : SkinId.Premiere;
                hb.SetSkin(skin, "PremierePro_" + _kind, _effect, _hitbox != null ? _hitbox.SkinTheme : null, _hitbox != null ? _hitbox.SkinIcon : null);
            }
        }
        Die();
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        if (_hitbox != null) _hitbox.Deactivate();
        Destroy(gameObject);
    }
}
