using UnityEngine;

/// <summary>
/// 歩く雑魚ウイルス（仕様書 4.1）。床を一定の速さで往復し、壁や足場の端で折り返す。
/// 体当たりは体の Hitbox、被弾は Hurtbox → Health（HP）で受ける。
/// 攻撃を受けると少し押し戻されて、短い間止まる。倒れたら動かない（消すのは DisableOnDeath）。
/// </summary>
[RequireComponent(typeof(KinematicBody2D), typeof(Health))]
public class ZakoWalker : MonoBehaviour
{
    [Tooltip("歩く速さ（マス／秒）")]
    [SerializeField] float _speed = 2f;
    [Tooltip("最初に歩く向き（右 1／左 -1）")]
    [SerializeField] int _startDirection = -1;
    [Tooltip("足場の端で折り返す（オフなら端から落ちる）")]
    [SerializeField] bool _turnAtLedges = true;
    [Tooltip("足場の端を調べるときの床（床・すり抜け床）")]
    [SerializeField] LayerMask _groundMask;
    [SerializeField] float _gravity = 40f;
    [SerializeField] float _maxFallSpeed = 18f;

    [Header("被弾")]
    [Tooltip("攻撃を受けてから止まる秒数")]
    [SerializeField, Min(0f)] float _hitPause = 0.25f;
    [Tooltip("攻撃のノックバックをどれだけ受けるか（0で押し戻されない）")]
    [SerializeField, Min(0f)] float _knockbackScale = 0.5f;

    [Header("見た目")]
    [Tooltip("向きに合わせて左右反転させる見た目（無くてもよい）")]
    [SerializeField] Transform _visualRoot;

    KinematicBody2D _body;
    BoxCollider2D _box;
    Health _health;
    Vector2 _velocity;
    float _pauseUntil = float.NegativeInfinity;

    /// <summary>歩いている向き（右 1／左 -1）</summary>
    public int Direction { get; private set; }

    void Reset()
    {
        _groundMask = LayerMask.GetMask("Ground", "OneWayPlatform");
    }

    void Awake()
    {
        _body = GetComponent<KinematicBody2D>();
        _box = GetComponent<BoxCollider2D>();
        _health = GetComponent<Health>();
        Direction = _startDirection >= 0 ? 1 : -1;
    }

    void OnEnable() => _health.Damaged += OnDamaged;
    void OnDisable() => _health.Damaged -= OnDamaged;

    void OnDamaged(DamageInfo info)
    {
        _pauseUntil = Time.time + _hitPause;
        _velocity.x = info.Knockback.x * _knockbackScale;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool stopped = _health.IsDead || Time.time < _pauseUntil;

        if (stopped) _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, 20f * dt);
        else _velocity.x = Direction * _speed * GameBuffs.ZakoSpeed; // 2026-11-19：雑魚の速さの倍率（アプリのキット）

        // 足場の端に来たら、落ちる前に折り返す
        if (!stopped && _turnAtLedges && _body.IsGrounded && !HasGroundAhead()) Turn();

        _velocity.y = Mathf.Max(_velocity.y - _gravity * dt, -_maxFallSpeed);
        _body.Move(_velocity * dt);

        if (_body.IsGrounded && _velocity.y < 0f) _velocity.y = 0f;
        if (!stopped && ((Direction > 0 && _body.HitWallRight) || (Direction < 0 && _body.HitWallLeft))) Turn();
    }

    void Update()
    {
        if (_visualRoot == null) return;
        Vector3 s = _visualRoot.localScale;
        s.x = Mathf.Abs(s.x) * Direction;
        _visualRoot.localScale = s;
    }

    /// <summary>進む向きを決める（右 1／左 -1）。ボスから追い出されたウイルスが、プレイヤーから離れる向きに走り出すとき用</summary>
    public void SetDirection(int direction)
    {
        Direction = direction >= 0 ? 1 : -1;
        _velocity = Vector2.zero;
    }

    void Turn()
    {
        Direction = -Direction;
        _velocity.x = Direction * _speed * GameBuffs.ZakoSpeed;
    }

    /// <summary>進む方向の足元の少し先に床があるか</summary>
    bool HasGroundAhead()
    {
        Bounds b = _box.bounds;
        var probe = new Vector2(b.center.x + Direction * (b.extents.x + 0.05f), b.min.y + 0.05f);
        return Physics2D.Raycast(probe, Vector2.down, 0.3f, _groundMask).collider != null;
    }
}
