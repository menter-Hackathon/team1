using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System からプレイヤーの入力を読み取る。
/// キー割り当てはコード内で定義している（.inputactions アセット不要で、マージ時に衝突しにくい）。
/// 攻撃・スキルのボタンは、仕様が決まったらここに追加する。
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("スティックの小さな傾きを無視するしきい値")]
    [SerializeField, Range(0f, 0.9f)] float _moveDeadZone = 0.2f;

    InputAction _move;
    InputAction _jump;

    /// <summary>移動入力（-1〜1）。キーボードは上下左右、ゲームパッドは左スティック／十字キー</summary>
    public Vector2 Move { get; private set; }
    /// <summary>ジャンプボタンを押し続けているか（可変ジャンプ用）</summary>
    public bool JumpHeld => _jump != null && _jump.IsPressed();
    /// <summary>ジャンプボタンが押された瞬間に呼ばれる</summary>
    public event Action JumpPressed;

    void Awake()
    {
        _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        _move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        _move.AddBinding("<Gamepad>/leftStick");
        _move.AddBinding("<Gamepad>/dpad");

        _jump = new InputAction("Jump", InputActionType.Button);
        _jump.AddBinding("<Keyboard>/space");
        _jump.AddBinding("<Gamepad>/buttonSouth");
        _jump.performed += OnJumpPerformed;
    }

    void OnEnable()
    {
        _move.Enable();
        _jump.Enable();
    }

    void OnDisable()
    {
        _move.Disable();
        _jump.Disable();
        Move = Vector2.zero;
    }

    void OnDestroy()
    {
        _jump.performed -= OnJumpPerformed;
        _move.Dispose();
        _jump.Dispose();
    }

    void Update()
    {
        Vector2 v = _move.ReadValue<Vector2>();
        if (Mathf.Abs(v.x) < _moveDeadZone) v.x = 0f;
        if (Mathf.Abs(v.y) < _moveDeadZone) v.y = 0f;
        Move = v;
    }

    void OnJumpPerformed(InputAction.CallbackContext _)
    {
        JumpPressed?.Invoke();
    }
}
