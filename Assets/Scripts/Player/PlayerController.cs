using System;
using UnityEngine;

/// <summary>
/// プレイヤーの移動・ジャンプ。
/// ・ジャンプは「高さ」と「頂点までの時間」で指定する（重力とジャンプ初速はそこから計算）
/// ・ボタンを早く離すと低く跳ぶ（可変ジャンプ）
/// ・崖から落ちた直後でも少しの間ジャンプできる（コヨーテタイム）
/// ・着地の少し前に押したジャンプも受け付ける（先行入力）
/// ・ダッシュ：向いている方向へ短く高速移動（仕様書 3.2：無敵なし）。ダッシュ中は重力なし。空中では着地までに1回。
///   ジャンプするとダッシュは終わる
/// ・近接の攻撃判定が出ている間（PlayerMeleeAttack.IsAttacking）は、向きを変えない（判定が反対側へ移らないように）。
///   移動の入力はそのまま効くので、後ずさりになる。ダッシュも入力の方向へ進むが、向きはそのまま
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

    [Header("ダッシュ")]
    [Tooltip("ダッシュ中の速さ（マス／秒）")]
    [SerializeField] float _dashSpeed = 16f;
    [Tooltip("ダッシュが続く秒数。距離＝速さ×秒数（16×0.15＝2.4マス）")]
    [SerializeField, Min(0.01f)] float _dashDuration = 0.15f;
    [Tooltip("ダッシュを始めてから、次のダッシュができるまでの秒数")]
    [SerializeField, Min(0f)] float _dashCooldown = 0.4f;
    [Tooltip("空中でできるダッシュの回数（着地で回復）")]
    [SerializeField, Min(0)] int _airDashCount = 1;

    [Header("被弾")]
    [Tooltip("のけぞり中の横方向の減速。小さいほど遠くまで吹き飛ぶ")]
    [SerializeField] float _knockbackDeceleration = 12f;

    [Header("見た目")]
    [Tooltip("左右反転させる見た目のルート（当たり判定は反転させない）")]
    [SerializeField] Transform _visualRoot;

    KinematicBody2D _body;
    PlayerInputReader _input;
    PlayerMeleeAttack _melee; // 無くてもよい。振っている間は向きを固定する
    Vector2 _velocity;
    float _lastGroundedTime = float.NegativeInfinity;
    float _lastJumpPressedTime = float.NegativeInfinity;
    bool _isJumpRising; // ジャンプで上昇中（可変ジャンプの判定用）
    bool _wasGrounded;
    float _stunUntil = float.NegativeInfinity;
    bool _dashRequested;
    float _dashUntil = float.NegativeInfinity;
    float _nextDashTime = float.NegativeInfinity;
    int _airDashesUsed;
    int _dashDirection = 1;

    public Vector2 Velocity => _velocity;
    float _moveBoostUntil;
    float _moveBoostMultiplier = 1f;

    public void GrantMoveSpeedBoost(float multiplier, float seconds)
    {
        _moveBoostMultiplier = Mathf.Max(1f, multiplier);
        _moveBoostUntil = Time.time + Mathf.Max(0f, seconds);
    }

    public void ClearMoveSpeedBoost() => _moveBoostUntil = 0f;
    public bool IsGrounded => _body.IsGrounded;
    /// <summary>右向きなら 1、左向きなら -1。攻撃の向きなどに使う</summary>
    public int FacingDirection { get; private set; } = 1;
    /// <summary>false の間は入力を受け付けない（被弾のけぞり・ステージ遷移中など）</summary>
    public bool CanControl { get; set; } = true;
    /// <summary>被弾でのけぞっている間 true（入力を受け付けない）</summary>
    public bool IsStunned => Time.time < _stunUntil;
    /// <summary>今プレイヤーが操作できるか（CanControl かつ のけぞり中でない）</summary>
    public bool IsControllable => CanControl && !IsStunned;
    /// <summary>ダッシュ中か</summary>
    public bool IsDashing => Time.time < _dashUntil;
    /// <summary>向きを変えられない間 true（近接の攻撃判定が出ている間）</summary>
    public bool IsFacingLocked => _melee != null && _melee.IsAttacking;

    /// <summary>
    /// 【仮】横の速さの倍率（2026-11-18：Premiere Pro のスピードデュレーションの「スローの場」）。ジャンプ・落ちる速さ・ダッシュは変えない。
    /// 変えた技が、終わるとき（場から出たとき・消えたとき）に 1 に戻す。
    /// 2026-11-19：main の GrantMoveSpeedBoost（時間つきの加速。技のデモ）と1つにまとめた。読むと両方を掛けた値、書くとスローの場の分だけ変わる
    /// </summary>
    public float MoveSpeedMultiplier
    {
        get => _moveSpeedMultiplier * (Time.time < _moveBoostUntil ? _moveBoostMultiplier : 1f);
        set => _moveSpeedMultiplier = value;
    }
    float _moveSpeedMultiplier = 1f;

    float _jumpLockedUntil = float.NegativeInfinity;
    /// <summary>ジャンプを封じられているか（2026-11-18：Terminal の chmod 000 player）</summary>
    public bool IsJumpLocked => Time.time < _jumpLockedUntil;
    /// <summary>ジャンプを seconds 秒封じる（すでに長く封じられていれば短くしない）</summary>
    public void LockJump(float seconds) => _jumpLockedUntil = Mathf.Max(_jumpLockedUntil, Time.time + seconds);
    /// <summary>ジャンプの封印を解く</summary>
    public void UnlockJump() => _jumpLockedUntil = float.NegativeInfinity;
    /// <summary>封じられている間にジャンプを押したとき（南京錠をゆらす演出用）</summary>
    public event Action JumpBlocked;

    /// <summary>ジャンプした瞬間（SE・エフェクト用）</summary>
    public event Action Jumped;
    /// <summary>着地した瞬間（SE・エフェクト用）</summary>
    public event Action Landed;
    /// <summary>ダッシュを始めた瞬間（SE・エフェクト・アニメーション用）</summary>
    public event Action Dashed;
    float _airTopY; // 空中で一番高かった位置（強い着地の音）

    float Gravity => 2f * _jumpHeight / (_timeToApex * _timeToApex);
    float JumpVelocity => Gravity * _timeToApex;

    void Awake()
    {
        _body = GetComponent<KinematicBody2D>();
        _input = GetComponent<PlayerInputReader>();
        _melee = GetComponent<PlayerMeleeAttack>();
    }

    void OnEnable()
    {
        _input.JumpPressed += OnJumpPressed;
        _input.DashPressed += OnDashPressed;
    }

    void OnDisable()
    {
        _input.JumpPressed -= OnJumpPressed;
        _input.DashPressed -= OnDashPressed;
    }

    void OnJumpPressed()
    {
        if (IsJumpLocked)
        {
            JumpBlocked?.Invoke();
            return;
        }
        if (CanControl) _lastJumpPressedTime = Time.time;
    }

    void OnDashPressed()
    {
        if (IsControllable) _dashRequested = true; // 次の FixedUpdate で始める
    }

    void Update()
    {
        float x = IsControllable && !IsFacingLocked ? _input.Move.x : 0f;
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
        if (grounded)
        {
            _lastGroundedTime = now;
            _airDashesUsed = 0;
        }
        bool stunned = IsStunned;
        bool controllable = CanControl && !stunned;

        // --- ダッシュの開始 ---
        if (_dashRequested)
        {
            _dashRequested = false;
            if (controllable) TryStartDash(grounded, now);
        }
        if (!controllable) _dashUntil = float.NegativeInfinity; // のけぞり・操作不可になったらダッシュは終わり
        bool dashing = now < _dashUntil;

        // --- 横移動 ---
        float inputX = controllable ? _input.Move.x : 0f;
        float targetX = inputX * _moveSpeed * Mathf.Max(0.05f, MoveSpeedMultiplier * GameBuffs.PlayerSpeed);
        bool hasInput = Mathf.Abs(targetX) > 0.01f;
        float accel;
        if (stunned) accel = _knockbackDeceleration; // のけぞり中は弱い減速で吹き飛ばす
        else if (grounded) accel = hasInput ? _groundAcceleration : _groundDeceleration;
        else accel = hasInput ? _airAcceleration : _airDeceleration;
        if (dashing) _velocity.x = _dashDirection * _dashSpeed;
        else _velocity.x = Mathf.MoveTowards(_velocity.x, targetX, accel * dt);

        // --- ジャンプ（先行入力 × コヨーテタイム） ---
        bool jumpBuffered = now - _lastJumpPressedTime <= _jumpBufferTime;
        bool canJump = now - _lastGroundedTime <= _coyoteTime;
        if (controllable && jumpBuffered && canJump && !IsJumpLocked)
        {
            _velocity.y = JumpVelocity;
            _isJumpRising = true;
            if (dashing) EndDash(); // ジャンプでダッシュは終わる
            dashing = false;
            // 同じ猶予時間内に二段ジャンプしないよう、両方を消費する
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
            GameAudio.Event("player_jump");
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

        // --- 重力（ダッシュ中はまっすぐ飛ぶ） ---
        if (dashing)
        {
            _velocity.y = 0f;
        }
        else
        {
            float g = Gravity * (_velocity.y < 0f ? _fallGravityMultiplier : 1f);
            _velocity.y = Mathf.Max(_velocity.y - g * dt, -_maxFallSpeed);
        }

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
            if (dashing) _dashUntil = float.NegativeInfinity; // 壁にぶつかったらダッシュは終わり
        }
        else if (dashing && Time.time + dt >= _dashUntil)
        {
            EndDash(); // 次のフレームで終わるので、ふつうの速さに戻しておく
        }

        if (_body.IsGrounded && !_wasGrounded)
        {
            Landed?.Invoke();
            if (_airTopY - transform.position.y > _jumpHeight + 1f) GameAudio.Event("player_land"); // 強い着地（ジャンプより高い所から）だけ
        }
        _airTopY = _body.IsGrounded ? transform.position.y : Mathf.Max(_airTopY, transform.position.y);
        _wasGrounded = _body.IsGrounded;
    }

    void TryStartDash(bool grounded, float now)
    {
        if (now < _nextDashTime) return;
        if (!grounded)
        {
            if (_airDashesUsed >= _airDashCount) return;
            _airDashesUsed++;
        }
        float x = _input.Move.x;
        _dashDirection = x > 0f ? 1 : x < 0f ? -1 : FacingDirection;
        if (!IsFacingLocked) FacingDirection = _dashDirection; // 振っている間は、後ろへダッシュしても前を向いたまま
        _dashUntil = now + _dashDuration;
        _nextDashTime = now + _dashCooldown;
        _isJumpRising = false;
        _velocity = new Vector2(_dashDirection * _dashSpeed, 0f);
        Dashed?.Invoke();
        GameAudio.Event("player_dash");
    }

    /// <summary>ダッシュを終わらせ、横の速さをふつうの移動速度に戻す（急に止まらないように）</summary>
    void EndDash()
    {
        _dashUntil = float.NegativeInfinity;
        _velocity.x = _dashDirection * _moveSpeed;
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
        _dashUntil = float.NegativeInfinity;
        _stunUntil = Mathf.Max(_stunUntil, Time.time + stunTime);
        _lastJumpPressedTime = float.NegativeInfinity; // 被弾前に押したジャンプを持ち越さない
    }
}
