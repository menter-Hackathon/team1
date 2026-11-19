using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル枠のボタン（1／2／3）が押されたら、そのスキルの攻撃データを実行する。クールタイムもここで数える。
/// ・クールタイムは攻撃データの「間隔」。使った瞬間から数える
/// ・クールタイムは能力ごとに覚える。捨てて拾い直しても、別の枠に入れ直しても、残り時間はそのまま
/// ・使えるかどうか（のけぞり中・入れ替え待ち中は使えない）は PlayerSkills が判断して SkillUseRequested を出す
/// ・召喚（AI アシスタント。2026-11-18）は、味方が消えてからクールタイムを数える。味方がいる間は使えない
/// ・チャット（Slack・Discord・LINE。2026-11-19）は、止まっている間は続けて押すとメッセージが増え（最大数まで、クールタイムなし）、
///   動くか最大数まで出したら、そこからクールタイムを数える
/// </summary>
[RequireComponent(typeof(PlayerSkills), typeof(AttackRunner))]
public class PlayerSkillExecutor : MonoBehaviour
{
    PlayerSkills _skills;
    AttackRunner _runner;
    readonly Dictionary<SkillDefinition, float> _readyAt = new Dictionary<SkillDefinition, float>();
    readonly Dictionary<SkillDefinition, float> _cooldownTotal = new Dictionary<SkillDefinition, float>(); // 今のクールタイムの長さ（ゲージの割合用）

    /// <summary>クールタイムの倍率（状態異常「クールタイムが長い」。ふだんは1。使った瞬間の倍率で決まる。2026-11-19）</summary>
    public float CooldownMultiplier { get; set; } = 1f;
    // 終わってからクールタイムを数える能力（攻撃データ → 能力）。攻撃が終わったら、そこからクールタイムを数え直す
    readonly Dictionary<AttackData, SkillDefinition> _waitingFinish = new Dictionary<AttackData, SkillDefinition>();

    /// <summary>スキルを使ったとき（枠の番号, スキル）。SE・エフェクト・UI 用</summary>
    public event Action<int, SkillDefinition> SkillUsed;
    /// <summary>クールタイム中で使えなかったとき（枠の番号, スキル, 残り秒数）。UI の「まだ使えない」表示用</summary>
    public event Action<int, SkillDefinition, float> SkillOnCooldown;

    void Awake()
    {
        _skills = GetComponent<PlayerSkills>();
        _runner = GetComponent<AttackRunner>();
    }

    void OnEnable()
    {
        _skills.SkillUseRequested += OnSkillUseRequested;
        _runner.Finished += OnAttackFinished;
    }

    void OnDisable()
    {
        _skills.SkillUseRequested -= OnSkillUseRequested;
        _runner.Finished -= OnAttackFinished;
    }

    void OnAttackFinished(AttackData attack)
    {
        if (attack == null || !_waitingFinish.TryGetValue(attack, out SkillDefinition skill)) return;
        _waitingFinish.Remove(attack);
        float cd = attack.Cooldown * CooldownMultiplier;
        _readyAt[skill] = Time.time + cd;
        _cooldownTotal[skill] = cd;
    }

    void OnSkillUseRequested(int index, SkillDefinition skill) => TryUse(index, skill);

    /// <summary>スキルを使う。使えたら true</summary>
    public bool TryUse(int index, SkillDefinition skill)
    {
        if (skill == null) return false;
        AttackData attack = skill.Attack;
        if (attack == null)
        {
            Debug.LogWarning($"{skill.DisplayName}: 攻撃データが設定されていないので使えません", skill);
            return false;
        }

        // チャット（Slack・Discord・LINE）：ためが続いている間（止まっていて最大数まで）は、クールタイムなしでメッセージを足す
        if (attack.Kind == AttackKind.Chat && ChatVolley.TryAppend(_runner, attack))
        {
            SkillUsed?.Invoke(index, skill);
            return true;
        }

        float remain = GetCooldownRemaining(skill);
        if (remain > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, remain);
            return false;
        }
        if (!_runner.Run(attack)) return false;

        if (attack.Kind == AttackKind.Summon)
        {
            // 味方が消えるまでは使えない。消えたこと（Finished）を受け取れなかったときのために、いられる最大の秒数の後には使えるようにしておく
            _readyAt[skill] = Time.time + attack.SummonLifetime + attack.Cooldown;
            _waitingFinish[attack] = skill;
        }
        else if (attack.Kind == AttackKind.Chat)
        {
            // ためが終わる（動く・最大数まで出す）まではクールタイムを数えない。終わったら Finished から数える。
            // 受け取れなかったときのために、待つ秒数×最大数の後には使えるようにしておく
            _readyAt[skill] = Time.time + attack.ChatFireDelay * attack.ChatMaxMessages + attack.Cooldown;
            _waitingFinish[attack] = skill;
        }
        else
        {
            float cd = attack.Cooldown * CooldownMultiplier;
            _readyAt[skill] = Time.time + cd;
            _cooldownTotal[skill] = cd;
        }
        SkillUsed?.Invoke(index, skill);
        return true;
    }

    /// <summary>クールタイムの残り秒数（使えるなら 0）</summary>
    public float GetCooldownRemaining(SkillDefinition skill)
    {
        if (skill == null || !_readyAt.TryGetValue(skill, out float t)) return 0f;
        // チャットのためが続いている間は、まだ押せるので「使える」扱い
        if (skill.Attack != null && skill.Attack.Kind == AttackKind.Chat && ChatVolley.IsOpenFor(_runner, skill.Attack)) return 0f;
        return Mathf.Max(0f, t - Time.time);
    }

    /// <summary>クールタイムの残りの割合（1＝使った直後、0＝使える）。UI のゲージ用</summary>
    public float GetCooldownRatio(SkillDefinition skill)
    {
        if (skill == null || skill.Attack == null || skill.Attack.Cooldown <= 0f) return 0f;
        float total = _cooldownTotal.TryGetValue(skill, out float t) && t > 0f ? t : skill.Attack.Cooldown;
        return Mathf.Clamp01(GetCooldownRemaining(skill) / total);
    }
}
