using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】テストシーン用：C キーで、ボスの見た目を別のアプリのものに切り替える（2026-11-19：AI チャットのボス）。
/// アイコンは Mac に入っているアプリから読む（入っていなければ、名前から決まる色の仮のアイコン）。切り替えたボスは最初から
/// </summary>
public class BossAppLookCycler : MonoBehaviour
{
    [SerializeField] BossController _boss;
    [Tooltip("切り替えるアプリの名前（スクリーンタイムでの名前）")]
    [SerializeField] string[] _names = { "ChatGPT", "Claude", "Perplexity", "Codex" };
    [Tooltip("アプリのバンドルID（名前と同じ順。分からなければ空）")]
    [SerializeField] string[] _bundleIds = { "com.openai.chat", "com.anthropic.claudefordesktop", "ai.perplexity.mac", "com.openai.codex" };

    InputAction _next;
    int _index;

    public string CurrentName => _names != null && _names.Length > 0 ? _names[_index] : "";

    void Awake()
    {
        _next = new InputAction("DebugNextAppLook", InputActionType.Button, "<Keyboard>/c");
    }

    void OnEnable() => _next.Enable();
    void OnDisable() => _next.Disable();
    void OnDestroy() => _next.Dispose();

    void Start() => Apply();

    void Update()
    {
        if (!_next.WasPressedThisFrame() || _names == null || _names.Length == 0) return;
        _index = (_index + 1) % _names.Length;
        Apply();
        if (_boss != null && _boss.isActiveAndEnabled) _boss.ResetBoss();
    }

    void Apply()
    {
        if (_boss == null || _names == null || _names.Length == 0) return;
        string bundle = _bundleIds != null && _index < _bundleIds.Length ? _bundleIds[_index] : "";
        AppVisual v = AppVisuals.Get(new AppUsageRow { Name = _names[_index], BundleId = bundle, Seconds = 1 });
        foreach (var look in _boss.GetComponentsInChildren<IBossAppLook>(true)) look.ApplyAppLook(_names[_index], v, v != null ? v.Theme : null);
    }

    void OnGUI()
    {
        if (_names == null || _names.Length == 0) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        GUI.Label(new Rect(10f, Screen.height - 46f, Screen.width - 20f, 22f), $"【仮】C：ボスのアプリを切り替え（今：{CurrentName}）", style);
    }
}
