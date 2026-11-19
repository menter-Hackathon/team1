using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// キーボードのキー設定（2026-11-20：一時停止のメニューの「キー設定」）。操作ごとに「メインのキー」を1つ選べる。
/// ・初期設定は PlayerInputReader の今までのキー（WASD・Space・J・K・1/2/3・Q・E・X）。変えたキーだけ PlayerPrefs に残す（"keys.<id>"）
/// ・ほかの操作で使っているキーを選んだら、2つの操作のキーを入れ替える（同じキーが2つの操作にならない）
/// ・サブのキー（矢印・F・左 Shift）とコントローラーは変えない。サブのキーが、どれかの操作のメインのキーと同じになったら、そのサブのキーは使わない
/// ・Esc（一時停止）は選べない
/// PlayerInputReader が有効になるたびに Apply で反映する（一時停止のメニューを閉じると反映される）
/// </summary>
public static class KeyBindings
{
    /// <summary>設定できる操作</summary>
    public class Entry
    {
        public string Id;       // 保存の名前
        public string Label;    // メニューに出す名前
        public string Action;   // PlayerInputReader の InputAction の名前
        public string Part;     // 移動の上下左右（2DVector の部品の名前）。それ以外は null
        public string Default;  // 初期設定のキー（"<Keyboard>/…"）
    }

    public static readonly Entry[] Entries =
    {
        new Entry { Id = "move_up",      Label = "移動（上）",         Action = "Move", Part = "up",    Default = "<Keyboard>/w" },
        new Entry { Id = "move_down",    Label = "移動（下）",         Action = "Move", Part = "down",  Default = "<Keyboard>/s" },
        new Entry { Id = "move_left",    Label = "移動（左）",         Action = "Move", Part = "left",  Default = "<Keyboard>/a" },
        new Entry { Id = "move_right",   Label = "移動（右）",         Action = "Move", Part = "right", Default = "<Keyboard>/d" },
        new Entry { Id = "jump",         Label = "ジャンプ",           Action = "Jump",            Default = "<Keyboard>/space" },
        new Entry { Id = "attack",       Label = "攻撃",               Action = "Attack",          Default = "<Keyboard>/j" },
        new Entry { Id = "dash",         Label = "ダッシュ",           Action = "Dash",            Default = "<Keyboard>/k" },
        new Entry { Id = "skill1",       Label = "スキル 1",           Action = "Skill1",          Default = "<Keyboard>/1" },
        new Entry { Id = "skill2",       Label = "スキル 2",           Action = "Skill2",          Default = "<Keyboard>/2" },
        new Entry { Id = "skill3",       Label = "スキル 3",           Action = "Skill3",          Default = "<Keyboard>/3" },
        new Entry { Id = "skill_prev",   Label = "スキルの選択（前）", Action = "SkillSelectPrev", Default = "<Keyboard>/q" },
        new Entry { Id = "skill_next",   Label = "スキルの選択（次）", Action = "SkillSelectNext", Default = "<Keyboard>/e" },
        new Entry { Id = "skill_discard",Label = "スキルを捨てる（長押し）", Action = "SkillDiscard", Default = "<Keyboard>/x" },
    };

    const string Prefix = "keys.";
    static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

    /// <summary>キー設定が変わった</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cache.Clear();
        Changed = null;
    }

    /// <summary>今のキー（"<Keyboard>/…"）</summary>
    public static string Get(Entry e)
    {
        if (!_cache.TryGetValue(e.Id, out string path))
        {
            path = PlayerPrefs.GetString(Prefix + e.Id, e.Default);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>/", StringComparison.Ordinal)) path = e.Default;
            _cache[e.Id] = path;
        }
        return path;
    }

    /// <summary>選べないキー（Esc は一時停止）</summary>
    public static bool IsReserved(string path) => string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase);

    /// <summary>キーを変える。ほかの操作で使っていれば、その操作には今のキーを渡す（入れ替え）</summary>
    public static void Set(Entry e, string path)
    {
        if (e == null || string.IsNullOrEmpty(path) || IsReserved(path)) return;
        string old = Get(e);
        if (Same(old, path)) return;
        foreach (Entry other in Entries)
        {
            if (other != e && Same(Get(other), path)) Store(other, old);
        }
        Store(e, path);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>全部を初期設定に戻す</summary>
    public static void ResetAll()
    {
        foreach (Entry e in Entries)
        {
            PlayerPrefs.DeleteKey(Prefix + e.Id);
            _cache[e.Id] = e.Default;
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>メニューに出すキーの名前（「Space」「J」など）</summary>
    public static string DisplayName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";
        try { return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice); }
        catch { return path; }
    }

    static void Store(Entry e, string path)
    {
        _cache[e.Id] = path;
        if (Same(path, e.Default)) PlayerPrefs.DeleteKey(Prefix + e.Id);
        else PlayerPrefs.SetString(Prefix + e.Id, path);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PlayerInputReader の操作に、今のキー設定を反映する（有効にする前に呼ぶ）。
    /// メインのキー＝その操作の最初のキーボードの割り当て（移動は1つ目の上下左右の組の、その向き）
    /// </summary>
    public static void Apply(IList<InputAction> actions)
    {
        if (actions == null) return;
        var primaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Entry e in Entries) primaries.Add(Get(e));

        foreach (InputAction action in actions)
        {
            if (action == null) continue;
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            var mainIndexes = new HashSet<int>();
            foreach (Entry e in Entries)
            {
                if (e.Action != action.name) continue;
                int index = FindMainBinding(action, e.Part);
                if (index < 0) continue;
                mainIndexes.Add(index);
                string path = Get(e);
                if (Same(path, action.bindings[index].path)) action.RemoveBindingOverride(index);
                else action.ApplyBindingOverride(index, path);
            }
            // サブのキー：どれかの操作のメインのキーと同じなら使わない（空の割り当てにする）
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite || mainIndexes.Contains(i) || !IsKeyboard(b.path)) continue;
                if (primaries.Contains(b.path)) action.ApplyBindingOverride(i, "");
                else action.RemoveBindingOverride(i);
            }
            if (wasEnabled) action.Enable();
        }
    }

    static bool IsKeyboard(string path) => path != null && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);

    static int FindMainBinding(InputAction action, string part)
    {
        var bindings = action.bindings;
        if (string.IsNullOrEmpty(part))
        {
            for (int i = 0; i < bindings.Count; i++)
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite && IsKeyboard(bindings[i].path)) return i;
            return -1;
        }
        // 1つ目の 2DVector の組のうち、キーボードの、その向きの部品
        for (int i = 0; i < bindings.Count; i++)
        {
            if (!bindings[i].isComposite) continue;
            for (int k = i + 1; k < bindings.Count && bindings[k].isPartOfComposite; k++)
                if (string.Equals(bindings[k].name, part, StringComparison.OrdinalIgnoreCase) && IsKeyboard(bindings[k].path)) return k;
            return -1;
        }
        return -1;
    }
}
