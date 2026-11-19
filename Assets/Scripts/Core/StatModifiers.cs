using System;
using UnityEngine;

/// <summary>
/// キャラクター（プレイヤー・ボス・雑魚）にかかっているバフ。Steam の実績解除などでかかる。
/// ・攻撃力の倍率：Hitbox がダメージ量とゲージ増加量に掛ける（持ち主の子の判定・持ち主が出した弾や判定）
/// ・移動速度の倍率：PlayerController・ZakoWalker が速さに掛ける。ボスは攻撃のあとの待ち時間を割る
/// 付いていなければ倍率は 1。実行中に必要になったら GetOrAdd で付ける。
/// </summary>
public class StatModifiers : MonoBehaviour
{
    readonly BuffStack _buffs = new BuffStack();

    /// <summary>バフがかかったとき（id, 中身）</summary>
    public event Action<string, BuffSpec> BuffApplied;

    public float AttackMultiplier => _buffs.AttackMultiplier(Time.time);
    public float SpeedMultiplier => _buffs.SpeedMultiplier(Time.time);

    public void Apply(string id, BuffSpec spec)
    {
        _buffs.Apply(id, spec, Time.time);
        BuffApplied?.Invoke(id, spec);
    }

    public bool Has(string id) => _buffs.Has(id, Time.time);
    public float Remaining(string id) => _buffs.Remaining(id, Time.time);
    public void ClearAll() => _buffs.Clear();

    /// <summary>付いていれば返し、無ければ付ける</summary>
    public static StatModifiers GetOrAdd(GameObject go)
    {
        if (go == null) return null;
        return go.TryGetComponent(out StatModifiers m) ? m : go.AddComponent<StatModifiers>();
    }

    /// <summary>この物の移動速度の倍率（StatModifiers が無ければ 1）</summary>
    public static float SpeedOf(Component c) => c != null && c.TryGetComponent(out StatModifiers m) ? m.SpeedMultiplier : 1f;

    /// <summary>攻撃力の倍率を、整数の値（ダメージ量・ゲージ増加量）に掛ける。0 は 0 のまま。1 以上の値は小数を切り上げる</summary>
    public static int Scale(int value, float multiplier)
    {
        if (value <= 0 || Mathf.Approximately(multiplier, 1f)) return value;
        return Mathf.Max(0, Mathf.CeilToInt(value * multiplier - 0.0001f));
    }
}
