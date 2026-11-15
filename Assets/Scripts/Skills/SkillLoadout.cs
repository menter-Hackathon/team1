using System;
using UnityEngine;

/// <summary>スキル枠が変わった理由（UI で演出を変えたいとき用）</summary>
public enum SkillSlotChangeReason
{
    /// <summary>空いている枠に入った</summary>
    Acquired,
    /// <summary>満杯のときに、入れ替え待ちのスキルと入れ替えた</summary>
    Replaced,
    /// <summary>捨てた</summary>
    Discarded,
    /// <summary>ランの終わりなどで、まとめて空にした</summary>
    Cleared,
    /// <summary>別の所持スキルにつなぎ替えた（シーン読み込みの後など）。UI は表示を作り直す</summary>
    Loaded,
}

/// <summary>スキル1枠の変化</summary>
public readonly struct SkillSlotChange
{
    /// <summary>枠の番号（0始まり。画面に出すときは +1）</summary>
    public readonly int Index;
    /// <summary>変わる前のスキル（空なら null）</summary>
    public readonly SkillDefinition Previous;
    /// <summary>変わった後のスキル（空なら null）</summary>
    public readonly SkillDefinition Current;
    public readonly SkillSlotChangeReason Reason;

    public SkillSlotChange(int index, SkillDefinition previous, SkillDefinition current, SkillSlotChangeReason reason)
    {
        Index = index;
        Previous = previous;
        Current = current;
        Reason = reason;
    }
}

/// <summary>スキルを手に入れようとした結果</summary>
public enum SkillAcquireResult
{
    /// <summary>空いている枠に入った</summary>
    Added,
    /// <summary>満杯なので入れ替え待ちになった。ReplaceWithPending か DeclinePending で決める</summary>
    PendingReplace,
    /// <summary>同じスキルをもう持っている（重複を許さない設定のとき）</summary>
    AlreadyOwned,
    /// <summary>別のスキルの入れ替え待ちの最中</summary>
    Busy,
    /// <summary>スキルが空（null）</summary>
    Invalid,
}

/// <summary>
/// 所持スキル（最大3枠）。MonoBehaviour ではないただのデータなので、
/// シーンをまたいで引き継ぐラン状態（M5）にそのまま持たせられる。
/// ・枠の位置は詰めない。捨てた枠は空きになり、1／2／3 キーとスキルの対応は変わらない
/// ・新しいスキルは一番前の空き枠に入る。満杯なら「入れ替え待ち」になり、入れ替える枠を選ぶか、拾わないかを決める
/// </summary>
[Serializable]
public class SkillLoadout
{
    public const int DefaultCapacity = 3;

    [SerializeField] SkillDefinition[] _slots;
    [Tooltip("【提案】オンにすると、同じスキルを複数の枠に持てる")]
    [SerializeField] bool _allowDuplicates;

    SkillDefinition _pending; // 入れ替え待ちのスキル（保存しない）

    /// <summary>枠の中身が変わったとき</summary>
    public event Action<SkillSlotChange> SlotChanged;
    /// <summary>入れ替え待ちが始まった（引数＝新しいスキル）／終わった（引数＝null）とき</summary>
    public event Action<SkillDefinition> PendingChanged;

    public SkillLoadout() : this(DefaultCapacity) { }

    public SkillLoadout(int capacity)
    {
        _slots = new SkillDefinition[Mathf.Max(1, capacity)];
    }

    public int Capacity => Slots.Length;
    public bool AllowDuplicates { get => _allowDuplicates; set => _allowDuplicates = value; }
    /// <summary>入れ替え待ちのスキル。無ければ null</summary>
    public SkillDefinition Pending => _pending;
    public bool HasPending => _pending != null;
    public bool IsFull => FirstEmptyIndex() < 0;

    /// <summary>スキルが入っている枠の数</summary>
    public int Count
    {
        get
        {
            int n = 0;
            foreach (var s in Slots) if (s != null) n++;
            return n;
        }
    }

    // インスペクターで配列を空にされても壊れないようにする
    SkillDefinition[] Slots => _slots != null && _slots.Length > 0 ? _slots : (_slots = new SkillDefinition[DefaultCapacity]);

    public bool IsValidIndex(int index) => index >= 0 && index < Slots.Length;

    /// <summary>枠のスキル。空、または番号が範囲外なら null</summary>
    public SkillDefinition Get(int index) => IsValidIndex(index) ? Slots[index] : null;

    public int IndexOf(SkillDefinition skill)
    {
        if (skill == null) return -1;
        return Array.IndexOf(Slots, skill);
    }

    public bool Contains(SkillDefinition skill) => IndexOf(skill) >= 0;

    /// <summary>
    /// スキルを手に入れる。空き枠があれば入れ、満杯なら入れ替え待ちにする。
    /// </summary>
    public SkillAcquireResult TryAcquire(SkillDefinition skill)
    {
        if (skill == null) return SkillAcquireResult.Invalid;
        if (HasPending) return SkillAcquireResult.Busy;
        if (!_allowDuplicates && Contains(skill)) return SkillAcquireResult.AlreadyOwned;

        int empty = FirstEmptyIndex();
        if (empty >= 0)
        {
            SetSlot(empty, skill, SkillSlotChangeReason.Acquired);
            return SkillAcquireResult.Added;
        }

        _pending = skill;
        PendingChanged?.Invoke(skill);
        return SkillAcquireResult.PendingReplace;
    }

    /// <summary>入れ替え待ちのスキルを、指定した枠に入れる（元のスキルは消える）</summary>
    public bool ReplaceWithPending(int index)
    {
        if (!HasPending || !IsValidIndex(index)) return false;

        SkillDefinition skill = _pending;
        _pending = null; // 枠のイベントを受け取った側から見て、もう入れ替え待ちではない状態にしておく
        var reason = Slots[index] != null ? SkillSlotChangeReason.Replaced : SkillSlotChangeReason.Acquired;
        SetSlot(index, skill, reason);
        PendingChanged?.Invoke(null);
        return true;
    }

    /// <summary>入れ替え待ちのスキルを拾わずに終える</summary>
    public bool DeclinePending()
    {
        if (!HasPending) return false;
        _pending = null;
        PendingChanged?.Invoke(null);
        return true;
    }

    /// <summary>枠のスキルを捨てる。枠は空きになる（ほかの枠は詰めない）</summary>
    public bool Discard(int index)
    {
        if (Get(index) == null) return false;
        SetSlot(index, null, SkillSlotChangeReason.Discarded);
        return true;
    }

    /// <summary>全部の枠を空にし、入れ替え待ちも取り消す（新しいランの開始など）</summary>
    public void Clear()
    {
        DeclinePending();
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i] != null) SetSlot(i, null, SkillSlotChangeReason.Cleared);
        }
    }

    int FirstEmptyIndex() => Array.IndexOf(Slots, null);

    void SetSlot(int index, SkillDefinition skill, SkillSlotChangeReason reason)
    {
        SkillDefinition previous = Slots[index];
        Slots[index] = skill;
        SlotChanged?.Invoke(new SkillSlotChange(index, previous, skill, reason));
    }
}
