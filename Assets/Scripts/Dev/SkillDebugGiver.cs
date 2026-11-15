using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】ボスがまだいないので、G キーでテスト用のスキルをリストの順番に渡す（最後まで行ったら最初に戻る）。
/// ボスの撃破報酬ができたら、このコンポーネントは消す。
/// </summary>
[RequireComponent(typeof(PlayerSkills))]
public class SkillDebugGiver : MonoBehaviour
{
    [SerializeField] SkillDefinition[] _skills;

    PlayerSkills _playerSkills;
    InputAction _give;
    int _next;

    void Awake()
    {
        _playerSkills = GetComponent<PlayerSkills>();
        _give = new InputAction("DebugGiveSkill", InputActionType.Button);
        _give.AddBinding("<Keyboard>/g");
    }

    void OnEnable() => _give.Enable();
    void OnDisable() => _give.Disable();
    void OnDestroy() => _give.Dispose();

    void Update()
    {
        if (!_give.WasPressedThisFrame() || _skills == null || _skills.Length == 0) return;

        SkillDefinition skill = _skills[_next];
        _next = (_next + 1) % _skills.Length;
        _playerSkills.TryAcquire(skill);
    }
}
