using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System からプレイヤーの入力を読み取る。
/// キー割り当てはコード内で定義している（.inputactions アセット不要で、マージ時に衝突しにくい）。
/// 通常攻撃のボタンは、仕様が決まったらここに追加する。
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("スティックの小さな傾きを無視するしきい値")]
    [SerializeField, Range(0f, 0.9f)] float _moveDeadZone = 0.2f;

    InputAction _move;
    InputAction _jump;
    InputAction _skillDiscard;
    readonly List<InputAction> _actions = new List<InputAction>();

    /// <summary>移動入力（-1〜1）。キーボードは上下左右、ゲームパッドは左スティック／十字キー</summary>
    public Vector2 Move { get; private set; }
    /// <summary>ジャンプボタンを押し続けているか（可変ジャンプ用）</summary>
    public bool JumpHeld => _jump != null && _jump.IsPressed();
    /// <summary>ジャンプボタンが押された瞬間に呼ばれる</summary>
    public event Action JumpPressed;

    /// <summary>スキル枠のボタン（1／2／3）が押された瞬間。引数は枠の番号（0始まり）</summary>
    public event Action<int> SkillSlotPressed;
    /// <summary>選択中のスキル枠を切り替えるボタン（Q／E）が押された瞬間。-1 で前、+1 で次</summary>
    public event Action<int> SkillSelectStepped;
    /// <summary>スキルを捨てるボタン（X）を押し続けているか。長押しの判定は PlayerSkills で行う</summary>
    public bool SkillDiscardHeld => _skillDiscard != null && _skillDiscard.IsPressed();

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
        _actions.Add(_move);

        _jump = new InputAction("Jump", InputActionType.Button);
        _jump.AddBinding("<Keyboard>/space");
        _jump.AddBinding("<Gamepad>/buttonSouth");
        _jump.performed += OnJumpPerformed;
        _actions.Add(_jump);

        // --- スキル ---
        // 枠を使う：1／2／3（ゲームパッドは Y／B／RB）
        string[] slotKeys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3" };
        string[] slotPads = { "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast", "<Gamepad>/rightShoulder" };
        for (int i = 0; i < slotKeys.Length; i++)
        {
            int index = i; // ラムダに渡すためにコピーする
            var slot = new InputAction($"Skill{i + 1}", InputActionType.Button);
            slot.AddBinding(slotKeys[i]);
            slot.AddBinding(slotPads[i]);
            slot.performed += _ => SkillSlotPressed?.Invoke(index);
            _actions.Add(slot);
        }

        // 選択中の枠を切り替える：Q で前、E で次（ゲームパッドは LB で次。3の次は1に戻る）
        var selectPrev = new InputAction("SkillSelectPrev", InputActionType.Button);
        selectPrev.AddBinding("<Keyboard>/q");
        selectPrev.performed += _ => SkillSelectStepped?.Invoke(-1);
        _actions.Add(selectPrev);

        var selectNext = new InputAction("SkillSelectNext", InputActionType.Button);
        selectNext.AddBinding("<Keyboard>/e");
        selectNext.AddBinding("<Gamepad>/leftShoulder");
        selectNext.performed += _ => SkillSelectStepped?.Invoke(+1);
        _actions.Add(selectNext);

        // 捨てる：X 長押し（ゲームパッドは LT 長押し）
        _skillDiscard = new InputAction("SkillDiscard", InputActionType.Button);
        _skillDiscard.AddBinding("<Keyboard>/x");
        _skillDiscard.AddBinding("<Gamepad>/leftTrigger");
        _actions.Add(_skillDiscard);
    }

    void OnEnable()
    {
        foreach (var action in _actions) action.Enable();
    }

    void OnDisable()
    {
        foreach (var action in _actions) action.Disable();
        Move = Vector2.zero;
    }

    void OnDestroy()
    {
        _jump.performed -= OnJumpPerformed;
        foreach (var action in _actions) action.Dispose();
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
