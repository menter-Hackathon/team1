using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】テストシーン用：始めのライフを決める。H キーで、もう一度その値に戻す（回復を確かめるため。2026-11-19：パケットの能力）
/// </summary>
[RequireComponent(typeof(Health))]
public class DebugStartLife : MonoBehaviour
{
    [Tooltip("始めのライフ")]
    [SerializeField, Min(1)] int _life = 1;

    Health _health;
    InputAction _reset;

    void Awake()
    {
        _health = GetComponent<Health>();
        _reset = new InputAction("DebugResetLife", InputActionType.Button, "<Keyboard>/h");
    }

    void OnEnable() => _reset.Enable();
    void OnDisable() => _reset.Disable();
    void OnDestroy() => _reset.Dispose();

    void Start() => _health.SetCurrent(_life);

    void Update()
    {
        if (_reset.WasPressedThisFrame()) _health.SetCurrent(_life);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        GUI.Label(new Rect(10f, Screen.height - 24f, Screen.width - 20f, 22f), $"【仮】H：ライフを {_life} に戻す（回復を確かめる）", style);
    }
}
