using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】ダメージ処理の動作確認用の攻撃。J キー（ゲームパッドは左のボタン）で前方に攻撃判定を出す。
/// M3 で本番の攻撃処理ができたら、このコンポーネントは消す。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerDebugAttack : MonoBehaviour
{
    [SerializeField] Hitbox _hitbox;
    [Tooltip("攻撃中だけ表示する絵（無くてもよい）")]
    [SerializeField] SpriteRenderer _effect;
    [SerializeField, Min(0.02f)] float _activeTime = 0.12f;
    [SerializeField, Min(0f)] float _cooldown = 0.3f;

    PlayerController _controller;
    InputAction _attack;
    float _activeUntil;
    float _nextAttackTime;
    bool _attacking;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _attack = new InputAction("DebugAttack", InputActionType.Button);
        _attack.AddBinding("<Keyboard>/j");
        _attack.AddBinding("<Gamepad>/buttonWest");
    }

    void Start()
    {
        if (_hitbox != null) _hitbox.Deactivate();
        if (_effect != null) _effect.enabled = false;
    }

    void OnEnable() => _attack.Enable();
    void OnDisable() => _attack.Disable();
    void OnDestroy() => _attack.Dispose();

    void Update()
    {
        if (_hitbox == null) return;

        if (_attack.WasPressedThisFrame() && _controller.IsControllable && Time.time >= _nextAttackTime)
        {
            _hitbox.Activate();
            _attacking = true;
            _activeUntil = Time.time + _activeTime;
            _nextAttackTime = Time.time + _cooldown;
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
