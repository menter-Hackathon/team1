using System;
using UnityEngine;

/// <summary>
/// プレイヤーの近接（仕様書 3.3）。J／F キー（ゲームパッドは左のボタン）で前方を1回振る。空中でも振れる。F は【仮】
/// ・雑魚には HP を減らし、ボスにはウイルス解除ゲージを増やす（どちらも Hitbox が相手に渡す）
/// ・攻撃データ（AttackData、種類 Melee）を設定すると、判定時間・間隔・ダメージ・ゲージ増加・ノックバック・のけぞりをそこから読む。
///   空なら、このコンポーネントの値と Hitbox の値をそのまま使う
/// 攻撃判定（Hitbox）は見た目（Visual）の子に置くので、向きに合わせて左右が反転する。
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PlayerInputReader))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [SerializeField] Hitbox _hitbox;
    [Tooltip("近接の攻撃データ（種類 Melee）。空ならこのコンポーネントの値を使う")]
    [SerializeField] AttackData _attack;
    [Tooltip("攻撃中だけ表示する絵（仮。アニメーションができたら外してよい）")]
    [SerializeField] SpriteRenderer _effect;
    [SerializeField, Min(0.02f)] float _activeTime = 0.12f;
    [SerializeField, Min(0f)] float _cooldown = 0.3f;

    PlayerController _controller;
    PlayerInputReader _input;
    float _activeUntil;
    float _nextAttackTime;
    bool _attacking;

    /// <summary>使っている攻撃データ（無ければ null）</summary>
    public AttackData Attack => _attack;
    /// <summary>攻撃判定が出ている間 true</summary>
    public bool IsAttacking => _attacking;

    /// <summary>振った瞬間（アニメーション・SE 用）</summary>
    public event Action Swung;

    float ActiveTime => _attack != null ? _attack.ActiveTime : _activeTime;
    float CooldownTime => _attack != null ? _attack.Cooldown : _cooldown;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _input = GetComponent<PlayerInputReader>();
    }

    void Start()
    {
        if (_hitbox != null && _attack != null) _hitbox.Setup(_attack);
        if (_hitbox != null) _hitbox.Deactivate();
        if (_effect != null) _effect.enabled = false;
    }

    void OnEnable() => _input.AttackPressed += OnAttackPressed;
    void OnDisable() => _input.AttackPressed -= OnAttackPressed;

    void OnAttackPressed() => TrySwing();

    float _lockedUntil = float.NegativeInfinity;
    /// <summary>近接を封じられているか（2026-11-18：Terminal の chmod 000 player）</summary>
    public bool IsLocked => Time.time < _lockedUntil;
    /// <summary>近接を seconds 秒封じる（すでに長く封じられていれば短くしない）</summary>
    public void Lock(float seconds) => _lockedUntil = Mathf.Max(_lockedUntil, Time.time + seconds);
    /// <summary>近接の封印を解く</summary>
    public void Unlock() => _lockedUntil = float.NegativeInfinity;
    /// <summary>封じられている間に近接を押したとき（南京錠をゆらす演出用）</summary>
    public event System.Action SwingBlocked;

    /// <summary>振る。間隔中・操作できないとき・封じられているときは false</summary>
    public bool TrySwing()
    {
        if (IsLocked)
        {
            SwingBlocked?.Invoke();
            GameAudio.Event("player_swing_blocked");
            return false;
        }
        if (_hitbox == null || !_controller.IsControllable || Time.time < _nextAttackTime) return false;

        _hitbox.Activate();
        _attacking = true;
        _activeUntil = Time.time + ActiveTime;
        _nextAttackTime = Time.time + CooldownTime;
        if (_effect != null) _effect.enabled = true;
        Swung?.Invoke();
        GameAudio.Event("player_swing");
        return true;
    }

    void Update()
    {
        if (_attacking && (Time.time >= _activeUntil || !_controller.CanControl))
        {
            _attacking = false;
            _hitbox.Deactivate();
            if (_effect != null) _effect.enabled = false;
        }
    }
}
