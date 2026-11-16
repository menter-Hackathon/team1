using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】プレイヤーの近接。J キー（ゲームパッドは左のボタン）で前方に攻撃判定を出す。
/// 攻撃データ（AttackData、種類 Melee）を設定すると、判定時間・間隔・ダメージ・ゲージ増加・ノックバック・のけぞりをそこから読む。
/// 空なら、このコンポーネントの値と Hitbox の値をそのまま使う。
/// 本番の近接（ボタン・アニメーション・空中攻撃など）は未決定。決まったら作り直す。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerDebugAttack : MonoBehaviour
{
    [SerializeField] Hitbox _hitbox;
    [Tooltip("近接の攻撃データ（種類 Melee）。空ならこのコンポーネントの値を使う")]
    [SerializeField] AttackData _attack;
    [Tooltip("攻撃中だけ表示する絵（無くてもよい）")]
    [SerializeField] SpriteRenderer _effect;
    [SerializeField, Min(0.02f)] float _activeTime = 0.12f;
    [SerializeField, Min(0f)] float _cooldown = 0.3f;

    PlayerController _controller;
    InputAction _input;
    float _activeUntil;
    float _nextAttackTime;
    bool _attacking;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _input = new InputAction("DebugAttack", InputActionType.Button);
        _input.AddBinding("<Keyboard>/j");
        _input.AddBinding("<Gamepad>/buttonWest");
    }

    void Start()
    {
        if (_hitbox != null && _attack != null) _hitbox.Setup(_attack);
        if (_hitbox != null) _hitbox.Deactivate();
        if (_effect != null) _effect.enabled = false;
    }

    void OnEnable() => _input.Enable();
    void OnDisable() => _input.Disable();
    void OnDestroy() => _input.Dispose();

    /// <summary>使っている攻撃データ（無ければ null）</summary>
    public AttackData Attack => _attack;
    float ActiveTime => _attack != null ? _attack.ActiveTime : _activeTime;
    float CooldownTime => _attack != null ? _attack.Cooldown : _cooldown;

    void Update()
    {
        if (_hitbox == null) return;

        if (_input.WasPressedThisFrame() && _controller.IsControllable && Time.time >= _nextAttackTime)
        {
            _hitbox.Activate();
            _attacking = true;
            _activeUntil = Time.time + ActiveTime;
            _nextAttackTime = Time.time + CooldownTime;
            if (_effect != null) _effect.enabled = true;
        }

        if (_attacking && Time.time >= _activeUntil)
        {
            _attacking = false;
            _hitbox.Deactivate();
            if (_effect != null) _effect.enabled = false;
        }
    }
}
