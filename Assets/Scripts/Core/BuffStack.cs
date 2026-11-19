using System;
using System.Collections.Generic;

/// <summary>同じバフがもう一度かかったときの扱い</summary>
public enum BuffStackMode
{
    /// <summary>効果は重ねず、残り時間に「続く秒数」を足す（初期値）</summary>
    Extend = 0,
    /// <summary>効果を重ねる（+30% が2つで +60%）。それぞれの時間で切れる</summary>
    Stack = 1,
    /// <summary>効果は重ねず、残り時間を「続く秒数」に戻す</summary>
    Refresh = 2,
}

/// <summary>バフ1つ分の中身（攻撃力・移動速度の上がり方と、続く秒数）</summary>
[Serializable]
public struct BuffSpec
{
    [UnityEngine.Tooltip("攻撃力の上がり方（0.3 で +30%）。ボスのゲージ増加量と、雑魚へのダメージに掛ける")]
    public float AttackBonus;
    [UnityEngine.Tooltip("移動速度の上がり方（0.15 で +15%）。ボスは攻撃のあとの待ち時間が、この分だけ短くなる")]
    public float SpeedBonus;
    [UnityEngine.Tooltip("続く秒数")]
    public float Duration;
    [UnityEngine.Tooltip("同じバフが重なったときの扱い")]
    public BuffStackMode StackMode;

    public static BuffSpec Default => new BuffSpec { AttackBonus = 0.3f, SpeedBonus = 0.15f, Duration = 8f, StackMode = BuffStackMode.Extend };
}

/// <summary>
/// かかっているバフの一覧と、合わせた倍率。時間は呼ぶ側から渡す（テストできるように Unity の時間に触らない）。
/// 同じ id のバフは、BuffSpec.StackMode に従って延長・重ねる・やり直す。
/// </summary>
public class BuffStack
{
    struct Entry
    {
        public string Id;
        public BuffSpec Spec;
        public float EndTime;
    }

    readonly List<Entry> _entries = new List<Entry>();

    /// <summary>バフをかける</summary>
    public void Apply(string id, BuffSpec spec, float now)
    {
        Prune(now);
        if (spec.StackMode != BuffStackMode.Stack)
        {
            int i = _entries.FindIndex(e => e.Id == id);
            if (i >= 0)
            {
                Entry e = _entries[i];
                e.EndTime = spec.StackMode == BuffStackMode.Extend ? e.EndTime + spec.Duration : now + spec.Duration;
                e.Spec = spec;
                _entries[i] = e;
                return;
            }
        }
        _entries.Add(new Entry { Id = id, Spec = spec, EndTime = now + spec.Duration });
    }

    /// <summary>攻撃力の倍率（1＝そのまま）</summary>
    public float AttackMultiplier(float now)
    {
        float m = 1f;
        foreach (var e in _entries) if (e.EndTime > now) m += e.Spec.AttackBonus;
        return m;
    }

    /// <summary>移動速度の倍率（1＝そのまま）</summary>
    public float SpeedMultiplier(float now)
    {
        float m = 1f;
        foreach (var e in _entries) if (e.EndTime > now) m += e.Spec.SpeedBonus;
        return m;
    }

    /// <summary>この id のバフが、今かかっているか</summary>
    public bool Has(string id, float now) => _entries.Exists(e => e.Id == id && e.EndTime > now);

    /// <summary>この id のバフの残り秒数（かかっていなければ 0）</summary>
    public float Remaining(string id, float now)
    {
        float r = 0f;
        foreach (var e in _entries) if (e.Id == id && e.EndTime > now) r = Math.Max(r, e.EndTime - now);
        return r;
    }

    /// <summary>何かバフがかかっているか</summary>
    public bool Any(float now) => _entries.Exists(e => e.EndTime > now);

    public void Clear() => _entries.Clear();

    void Prune(float now) => _entries.RemoveAll(e => e.EndTime <= now);
}
