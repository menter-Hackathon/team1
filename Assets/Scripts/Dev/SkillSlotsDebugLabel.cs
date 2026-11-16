using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】スキル枠の状態を画面の左上に文字で出す確認用。UI 担当の本番表示ができたら消す。
/// 表示は最初の1回を除いて PlayerSkills のイベントだけで更新している。
/// 表示が変われば、UI 向けのイベントが届いているということ。届いたイベントはコンソールにも出す。
/// テストシーンに置いた SkillPickup の上には、スキル名を出す。
/// </summary>
public class SkillSlotsDebugLabel : MonoBehaviour
{
    [SerializeField] PlayerSkills _skills;
    [Tooltip("画面に残すイベントの行数")]
    [SerializeField, Min(1)] int _logLines = 8;

    string[] _slotNames;
    int _selected;
    string _pendingName;
    readonly List<string> _log = new List<string>();
    SkillPickup[] _pickups;
    GUIStyle _style;
    GUIStyle _smallStyle;
    GUIStyle _centerStyle;

    void Awake()
    {
        if (_skills == null) _skills = GetComponent<PlayerSkills>();
    }

    void OnEnable()
    {
        if (_skills == null) return;
        _skills.SlotChanged += OnSlotChanged;
        _skills.PendingChanged += OnPendingChanged;
        _skills.SelectedSlotChanged += OnSelectedSlotChanged;
        _skills.SkillUseRequested += OnSkillUseRequested;
        _skills.AcquireAttempted += OnAcquireAttempted;
    }

    void OnDisable()
    {
        if (_skills == null) return;
        _skills.SlotChanged -= OnSlotChanged;
        _skills.PendingChanged -= OnPendingChanged;
        _skills.SelectedSlotChanged -= OnSelectedSlotChanged;
        _skills.SkillUseRequested -= OnSkillUseRequested;
        _skills.AcquireAttempted -= OnAcquireAttempted;
    }

    void Start()
    {
        if (_skills == null) return;
        // 最初の状態だけは直接読む（購読する前の状態なので）
        _slotNames = new string[_skills.Capacity];
        for (int i = 0; i < _slotNames.Length; i++) _slotNames[i] = NameOf(_skills.GetSkill(i));
        _selected = _skills.SelectedSlot;
        _pendingName = _skills.Pending != null ? _skills.Pending.DisplayName : null;
        _pickups = FindObjectsOfType<SkillPickup>();
    }

    static string NameOf(SkillDefinition skill) => skill != null ? skill.DisplayName : "（空）";

    void AddLog(string line)
    {
        string text = $"[{Time.time:0.0}s] {line}";
        _log.Add(text);
        while (_log.Count > _logLines) _log.RemoveAt(0);
        Debug.Log($"[SkillSlots] {text}", this);
    }

    void OnSlotChanged(SkillSlotChange change)
    {
        if (_slotNames != null && change.Index < _slotNames.Length) _slotNames[change.Index] = NameOf(change.Current);
        AddLog($"SlotChanged 枠{change.Index + 1}：{NameOf(change.Previous)} → {NameOf(change.Current)}（{ReasonText(change.Reason)}）");
    }

    void OnPendingChanged(SkillDefinition pending)
    {
        _pendingName = pending != null ? pending.DisplayName : null;
        AddLog(pending != null ? $"PendingChanged 入れ替え待ち開始：{pending.DisplayName}" : "PendingChanged 入れ替え待ち終了");
    }

    void OnSelectedSlotChanged(int index)
    {
        _selected = index;
        AddLog($"SelectedSlotChanged 選択：枠{index + 1}");
    }

    void OnSkillUseRequested(int index, SkillDefinition skill)
    {
        AddLog($"SkillUseRequested 枠{index + 1}：{skill.DisplayName}（攻撃の中身は M3）");
    }

    void OnAcquireAttempted(SkillDefinition skill, SkillAcquireResult result)
    {
        AddLog($"AcquireAttempted {NameOf(skill)}：{ResultText(result)}");
    }

    static string ReasonText(SkillSlotChangeReason reason)
    {
        switch (reason)
        {
            case SkillSlotChangeReason.Acquired: return "獲得";
            case SkillSlotChangeReason.Replaced: return "入れ替え";
            case SkillSlotChangeReason.Discarded: return "捨てた";
            case SkillSlotChangeReason.Cleared: return "全消去";
            case SkillSlotChangeReason.Loaded: return "つなぎ替え";
            default: return reason.ToString();
        }
    }

    static string ResultText(SkillAcquireResult result)
    {
        switch (result)
        {
            case SkillAcquireResult.Added: return "空き枠に入った";
            case SkillAcquireResult.PendingReplace: return "満杯なので入れ替え待ち";
            case SkillAcquireResult.AlreadyOwned: return "もう持っているので拾えない";
            case SkillAcquireResult.Busy: return "入れ替え待ちの最中なので拾えない";
            case SkillAcquireResult.Invalid: return "スキルが空";
            default: return result.ToString();
        }
    }

    void OnGUI()
    {
        if (_skills == null || _slotNames == null) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
            _style.normal.textColor = Color.white;
            _smallStyle = new GUIStyle(_style) { fontSize = 13, fontStyle = FontStyle.Normal };
            _centerStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
        }

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("スキル枠（仮表示）");
        for (int i = 0; i < _slotNames.Length; i++)
        {
            string mark = i == _selected ? "▶" : "　";
            lines.AppendLine($"{mark} [{i + 1}] {_slotNames[i]}");
        }
        if (_pendingName != null)
        {
            lines.AppendLine($"<color=#ffd84d>入れ替え待ち：{_pendingName}</color>");
            lines.AppendLine("<color=#ffd84d>　1／2／3：入れ替える枠を選ぶ　X長押し：拾わない</color>");
        }
        float progress = _skills.DiscardHoldProgress;
        if (progress > 0f)
        {
            int filled = Mathf.RoundToInt(progress * 10f);
            string label = _pendingName != null ? "拾わない" : "捨てる";
            lines.AppendLine($"{label} {new string('■', filled)}{new string('□', 10 - filled)}");
        }

        var help = new System.Text.StringBuilder();
        help.AppendLine("1／2／3：使う　Q／E：枠を選ぶ　X長押し：捨てる　G：テスト用スキルを獲得");
        help.AppendLine("--- 届いたイベント ---");
        foreach (string line in _log) help.AppendLine(line);

        const float width = 560f;
        var main = new GUIContent(lines.ToString());
        var sub = new GUIContent(help.ToString());
        float mainHeight = _style.CalcHeight(main, width - 16f);
        float subHeight = _smallStyle.CalcHeight(sub, width - 16f);
        GUI.Box(new Rect(8f, 8f, width, mainHeight + subHeight + 12f), GUIContent.none);
        GUI.Label(new Rect(16f, 12f, width - 16f, mainHeight), main, _style);
        GUI.Label(new Rect(16f, 12f + mainHeight, width - 16f, subHeight), sub, _smallStyle);

        DrawPickupNames();
    }

    void DrawPickupNames()
    {
        Camera cam = Camera.main;
        if (cam == null || _pickups == null) return;
        foreach (SkillPickup pickup in _pickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled || pickup.Skill == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(pickup.transform.position + new Vector3(0f, 0.7f, 0f));
            if (sp.z <= 0f) continue;
            GUI.Label(new Rect(sp.x - 80f, Screen.height - sp.y - 10f, 160f, 20f), pickup.Skill.DisplayName, _centerStyle);
        }
    }
}
