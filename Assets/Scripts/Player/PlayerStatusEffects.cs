using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>プレイヤーの状態異常の種類（2026-11-19。Photoshop のボスのフィルター）。値は画面の印の番号（GameArt.StatusIcons）と同じ</summary>
public enum StatusEffect
{
    /// <summary>移動が速くなる</summary>
    Fast = 0,
    /// <summary>移動が遅くなる</summary>
    Slow = 1,
    /// <summary>能力のクールタイムが長くなる</summary>
    LongCooldown = 2,
    /// <summary>ジャンプが高くなる</summary>
    HighJump = 3,
}

/// <summary>
/// プレイヤーの状態異常（2026-11-19。Photoshop のボスのフィルターがかける。数値は【仮】）。プレイヤーに付ける（無ければ、かける側が付ける）。
/// ・それぞれ決まった時間だけ続く。続いている間、毎フレーム PlayerController・PlayerSkillExecutor の倍率に入れる
///   速い：移動×1.5、遅い：移動×0.6、クールタイムが長い：クールタイム×2（使った瞬間の倍率で決まる）、ジャンプが高い：高さ×1.45
/// ・画面の左上に、印と残り時間を出す（PixelHud）。クールタイムが長い間は、能力の枠にも印
/// </summary>
public class PlayerStatusEffects : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float _fastSpeed = 1.5f;
    [SerializeField, Min(0.1f)] float _slowSpeed = 0.6f;
    [SerializeField, Min(1f)] float _cooldownScale = 2f;
    [SerializeField, Min(1f)] float _jumpScale = 1.45f;

    readonly Dictionary<StatusEffect, (float start, float end)> _active = new Dictionary<StatusEffect, (float, float)>();
    readonly List<StatusEffect> _expired = new List<StatusEffect>();
    PlayerController _controller;
    PlayerSkillExecutor _executor;

    /// <summary>状態異常がかかった・解けたとき</summary>
    public event Action Changed;

    /// <summary>かかっている状態異常（かかった順）</summary>
    public IEnumerable<StatusEffect> Active => _active.Keys;
    public bool HasAny => _active.Count > 0;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _executor = GetComponent<PlayerSkillExecutor>();
    }

    /// <summary>状態異常をかける（同じ物がかかっていれば、時間を延ばす）。速いと遅いは、あとからかけた方だけにする</summary>
    public void Apply(StatusEffect effect, float duration)
    {
        if (effect == StatusEffect.Fast) _active.Remove(StatusEffect.Slow);
        if (effect == StatusEffect.Slow) _active.Remove(StatusEffect.Fast);
        _active[effect] = (Time.time, Time.time + Mathf.Max(0.1f, duration));
        ApplyMultipliers();
        Changed?.Invoke();
    }

    /// <summary>状態異常を解く</summary>
    public void Remove(StatusEffect effect)
    {
        if (!_active.Remove(effect)) return;
        ApplyMultipliers();
        Changed?.Invoke();
    }

    public void ClearAll()
    {
        if (_active.Count == 0) return;
        _active.Clear();
        ApplyMultipliers();
        Changed?.Invoke();
    }

    public bool Has(StatusEffect effect) => _active.ContainsKey(effect);

    /// <summary>残りの秒数（かかっていなければ 0）</summary>
    public float Remaining(StatusEffect effect) => _active.TryGetValue(effect, out var t) ? Mathf.Max(0f, t.end - Time.time) : 0f;

    /// <summary>残りの割合（1＝かかった直後、0＝解ける）</summary>
    public float RemainingRatio(StatusEffect effect) =>
        _active.TryGetValue(effect, out var t) ? Mathf.Clamp01((t.end - Time.time) / Mathf.Max(0.01f, t.end - t.start)) : 0f;

    void Update()
    {
        if (_active.Count == 0) return;
        _expired.Clear();
        foreach (var kv in _active)
        {
            if (Time.time >= kv.Value.end) _expired.Add(kv.Key);
        }
        if (_expired.Count == 0) return;
        foreach (var e in _expired) _active.Remove(e);
        ApplyMultipliers();
        Changed?.Invoke();
    }

    void OnDisable()
    {
        // シーンが終わるときなど：倍率を元に戻しておく
        _active.Clear();
        ApplyMultipliers();
    }

    void ApplyMultipliers()
    {
        if (_controller != null)
        {
            _controller.SpeedMultiplier = Has(StatusEffect.Fast) ? _fastSpeed : Has(StatusEffect.Slow) ? _slowSpeed : 1f;
            _controller.JumpMultiplier = Has(StatusEffect.HighJump) ? _jumpScale : 1f;
        }
        if (_executor != null) _executor.CooldownMultiplier = Has(StatusEffect.LongCooldown) ? _cooldownScale : 1f;
    }

    /// <summary>画面に出す名前</summary>
    public static string Label(StatusEffect e) => e switch
    {
        StatusEffect.Fast => "移動が速くなった！",
        StatusEffect.Slow => "移動が遅くなった！",
        StatusEffect.LongCooldown => "能力のクールタイムが長くなった！",
        StatusEffect.HighJump => "ジャンプが高くなった！",
        _ => "",
    };
}
