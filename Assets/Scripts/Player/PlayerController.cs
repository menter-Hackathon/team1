using System;
using UnityEngine;

/// <summary>
/// プレイヤーの移動・ジャンプ。
/// ・ジャンプは「高さ」と「頂点までの時間」で指定する（重力とジャンプ初速はそこから計算）
/// ・ボタンを早く離すと低く跳ぶ（可変ジャンプ）
/// ・崖から落ちた直後でも少しの間ジャンプできる（コヨーテタイム）
/// ・着地の少し前に押したジャンプも受け付ける（先行入力）
/// </summary>
[RequireComponent(typeof(KinematicBody2D), typeof(PlayerInputReader))]
public class PlayerController : MonoBehaviour
{
    [Header("横移動")]
    [SerializeField] float _moveSpeed = 7f;
    [SerializeField] float _groundAcceleration = 80f;
    [SerializeField] float _groundDeceleration = 90f;
    [SerializeField] float _airAcceleration = 50f;
    [SerializeField] float _airDeceleration = 35f;

    [Header("ジャンプ")]
    [Tooltip("ボタンを押し続けたときの最高到達点の高さ（ユニット）")]
    [SerializeField] float _jumpHeight = 3.2f;
    [Tooltip("頂点に着くまでの秒数。小さいほどキビキビ跳ぶ")]
    [SerializeField] float _timeToApex = 0.38f;
    [Tooltip("落下中の重力倍率。大きいほど落ちるのが速い")]
    [SerializeField] float _fallGravityMultiplier = 1.7f;
    [Tooltip("上昇中にボタンを離したときに残す上向き速度の割合")]
    [SerializeField, Range(0f, 1f)] float _jumpCutMultiplier = 0.5f;
    [SerializeField] float _maxFallSpeed = 18f;
    [Tooltip("崖から落ちた後もジャンプできる猶予（秒）")]
    [SerializeField] float _coyoteTime = 0.1f;
    [Tooltip("着地前に押したジャンプを覚えておく時間（秒）")]
    [SerializeField] float _jumpBufferTime = 0.12f;

    [Header("被弾")]
    [Tooltip("のけぞり中の横方向の減速。小さいほど遠くまで吹き飛ぶ")]
    [SerializeField] float _knockbackDeceleration = 12f;

    [Header("見た目")]
    [Tooltip("左右反転させる見た目のルート（当たり判定は反転させない）")]
    [SerializeField] Transform _visualRoot;

    KinematicBody2D _body;
    PlayerInputReader _input;
    Vector2 _velocity;
    float _lastGroundedTime = float.NegativeInfinity;
    float _lastJumpPressedTime = float.NegativeInfinity;
    bool _isJumpRising; // ジャンプで上昇中（可変ジャンプの判定用）
    bool _wasGrounded;
    float _stunUntil = float.NegativeInfinity;

    public Vector2 Velocity => _velocity;
    public bool IsGrounded => _body.IsGrounded;
    /// <summary>右向きなら 1、左向きなら -1。攻撃の向きなどに使う</summary>
    public int FacingDirection { get; private set; } = 1;
    /// <summary>false の間は入力を受け付けない（被弾のけぞり・ステージ遷移中など）</summary>
    public bool CanControl { get; set; } = true;
    /// <summary>被弾でのけぞっている間 true（入力を受け付けない）</summary>
    public bool IsStunned => Time.time < _stunUntil;
    /// <summary>今プレイヤーが操作できるか（CanControl かつ のけぞり中でない）</summary>
    public bool IsControllable => CanControl && !IsStunned;

    /// <summary>ジャンプした瞬間（SE・エフェクト用）</summary>
    public event Action Jumped;
    /// <summary>着地した瞬間（SE・エフェクト用）</summary>
    public event Action Landed;

    float Gravity => 2f * _jumpHeight / (_timeToApex * _timeToApex);
    float JumpVelocity => Gravity * _timeToApex;

    void Awake()
    {
        _body = GetComponent<KinematicBody2D>();
        _input = GetComponent<PlayerInputReader>();
    }

    void OnEnable() => _input.JumpPressed += OnJumpPressed;
    void OnDisable() => _input.JumpPressed -= OnJumpPressed;

    void OnJumpPressed()
    {
        if (CanControl) _lastJumpPressedTime = Time.time;
    }

    void Update()
    {
        float x = IsControllable ? _input.Move.x : 0f;
        if (x > 0f) FacingDirection = 1;
        else if (x < 0f) FacingDirection = -1;

        if (_visualRoot != null)
        {
            Vector3 s = _visualRoot.localScale;
            s.x = Mathf.Abs(s.x) * FacingDirection;
            _visualRoot.localScale = s;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float now = Time.time;
        bool grounded = _body.IsGrounded; // 前回の Move の結果
        if (grounded) _lastGroundedTime = now;
        bool stunned = IsStunned;
        bool controllable = CanControl && !stunned;

        // --- 横移動 ---
        float inputX = controllable ? _input.Move.x : 0f;
        float targetX = inputX * _moveSpeed;
        bool hasInput = Mathf.Abs(targetX) > 0.01f;
        float accel;
        if (stunned) accel = _knockbackDeceleration; // のけぞり中は弱い減速で吹き飛ばす
        else if (grounded) accel = hasInput ? _groundAcceleration : _groundDeceleration;
        else accel = hasInput ? _airAcceleration : _airDeceleration;
        _velocity.x = Mathf.MoveTowards(_velocity.x, targetX, accel * dt);

        // --- ジャンプ（先行入力 × コヨーテタイム） ---
        bool jumpBuffered = now - _lastJumpPressedTime <= _jumpBufferTime;
        bool canJump = now - _lastGroundedTime <= _coyoteTime;
        if (controllable && jumpBuffered && canJump)
        {
            _velocity.y = JumpVelocity;
            _isJumpRising = true;
            // 同じ猶予時間内に二段ジャンプしないよう、両方を消費する
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
        }

        // --- 可変ジャンプ：上昇中にボタンを離したら減速 ---
        if (_isJumpRising)
        {
            if (_velocity.y <= 0f)
            {
                _isJumpRising = false;
            }
            else if (!(controllable && _input.JumpHeld))
            {
                _velocity.y *= _jumpCutMultiplier;
                _isJumpRising = false;
            }
        }

        // --- 重力 ---
        float g = Gravity * (_velocity.y < 0f ? _fallGravityMultiplier : 1f);
        _velocity.y = Mathf.Max(_velocity.y - g * dt, -_maxFallSpeed);

        // --- 移動と衝突 ---
        _body.Move(_velocity * dt);

        if (_body.IsGrounded && _velocity.y < 0f) _velocity.y = 0f;
        if (_body.HitCeiling && _velocity.y > 0f)
        {
            _velocity.y = 0f;
            _isJumpRising = false;
        }
        if ((_body.HitWallLeft && _velocity.x < 0f) || (_body.HitWallRight && _velocity.x > 0f))
        {
            _velocity.x = 0f;
        }

        if (_body.IsGrounded && !_wasGrounded) Landed?.Invoke();
        _wasGrounded = _body.IsGrounded;
    }

    /// <summary>速度を直接上書きする（スキルの移動など）</summary>
    public void SetVelocity(Vector2 velocity)
    {
        _velocity = velocity;
        _isJumpRising = false;
    }

    /// <summary>吹き飛ばして、stunTime 秒のあいだ操作できなくする（被弾時）</summary>
    public void ApplyKnockback(Vector2 velocity, float stunTime)
    {
        SetVelocity(velocity);
        _stunUntil = Mathf.Max(_stunUntil, Time.time + stunTime);
        _lastJumpPressedTime = float.NegativeInfinity; // 被弾前に押したジャンプを持ち越さない
    }
}
