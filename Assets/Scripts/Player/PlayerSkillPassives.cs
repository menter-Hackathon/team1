using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 枠に入っているスキルの「装備中ずっと効く効果」（SkillPassive）を、プレイヤーに付けたり外したりする。
/// ・枠に入った（獲得・入れ替え・つなぎ替え）ら Attach、枠から外れたら Dispose
/// ・同じスキルが2つの枠にあっても、効果は1つだけ
/// プレイヤーに PlayerSkills と一緒に付ける。
/// </summary>
[RequireComponent(typeof(PlayerSkills))]
public class PlayerSkillPassives : MonoBehaviour
{
    PlayerSkills _skills;
    readonly Dictionary<SkillDefinition, IDisposable> _attached = new Dictionary<SkillDefinition, IDisposable>();

    /// <summary>今効いている効果のスキル（テスト・デバッグ用）</summary>
    public IEnumerable<SkillDefinition> ActiveSkills => _attached.Keys;

    void Awake() => _skills = GetComponent<PlayerSkills>();

    void OnEnable()
    {
        _skills.SlotChanged += OnSlotChanged;
        Sync();
    }

    void OnDisable()
    {
        _skills.SlotChanged -= OnSlotChanged;
        foreach (var d in _attached.Values) d?.Dispose();
        _attached.Clear();
    }

    void OnSlotChanged(SkillSlotChange change) => Sync();

    /// <summary>枠の中身に合わせて、効果を付け外しする</summary>
    public void Sync()
    {
        if (_skills == null || _skills.Loadout == null) return;
        var equipped = new HashSet<SkillDefinition>();
        for (int i = 0; i < _skills.Capacity; i++)
        {
            SkillDefinition s = _skills.GetSkill(i);
            if (s != null && s.Passive != null) equipped.Add(s);
        }

        var remove = new List<SkillDefinition>();
        foreach (var kv in _attached) if (!equipped.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (var s in remove)
        {
            _attached[s]?.Dispose();
            _attached.Remove(s);
        }
        foreach (var s in equipped)
        {
            if (!_attached.ContainsKey(s)) _attached[s] = s.Passive.Attach(gameObject);
        }
    }
}
