using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル枠のボタン（1／2／3）が押されたら、そのスキルの攻撃データを実行する。クールタイムもここで数える。
/// ・クールタイムは攻撃データの「間隔」。使った瞬間から数える
/// ・クールタイムは能力ごとに覚える。捨てて拾い直しても、別の枠に入れ直しても、残り時間はそのまま
/// ・使えるかどうか（のけぞり中・入れ替え待ち中は使えない）は PlayerSkills が判断して SkillUseRequested を出す
/// ・「順番に切り替わる攻撃」があるスキル（Office）は、使うたびに次の攻撃へ進む。次の攻撃は GetNextAttack と
///   NextAttackChanged で分かる（スロットのアイコンを変える用）。順番も能力ごとに覚える
/// ・攻撃データが無く「装備中ずっと効く効果」だけのスキル（Steam）は、ボタンを押しても何もしない
/// </summary>
[RequireComponent(typeof(PlayerSkills), typeof(AttackRunner))]
public class PlayerSkillExecutor : MonoBehaviour
{
    PlayerSkills _skills;
    AttackRunner _runner;

    /// <summary>能力を実行する AttackRunner（HUD が、専用の技の状態を聞くとき用）</summary>
    public AttackRunner Runner => _runner;
    readonly Dictionary<SkillDefinition, float> _readyAt = new Dictionary<SkillDefinition, float>();
    readonly Dictionary<SkillDefinition, float> _cooldownOf = new Dictionary<SkillDefinition, float>();
    readonly Dictionary<SkillDefinition, int> _turn = new Dictionary<SkillDefinition, int>();

    /// <summary>スキルを使ったとき（枠の番号, スキル）。SE・エフェクト・UI 用</summary>
    public event Action<int, SkillDefinition> SkillUsed;
    /// <summary>クールタイム中で使えなかったとき（枠の番号, スキル, 残り秒数）。UI の「まだ使えない」表示用</summary>
    public event Action<int, SkillDefinition, float> SkillOnCooldown;
    /// <summary>順番に切り替わるスキルの、次の攻撃が変わったとき（スキル, 次の攻撃データ）。スロットのアイコン用</summary>
    public event Action<SkillDefinition, AttackData> NextAttackChanged;

    void Awake()
    {
        _skills = GetComponent<PlayerSkills>();
        _runner = GetComponent<AttackRunner>();
    }

    void OnEnable() => _skills.SkillUseRequested += OnSkillUseRequested;
    void OnDisable() => _skills.SkillUseRequested -= OnSkillUseRequested;

    void OnSkillUseRequested(int index, SkillDefinition skill) => TryUse(index, skill);

    /// <summary>次に使ったときに出る攻撃データ（順番に切り替わらないスキルは、いつも攻撃データ）</summary>
    public AttackData GetNextAttack(SkillDefinition skill)
    {
        if (skill == null) return null;
        _turn.TryGetValue(skill, out int turn);
        return skill.GetAttack(turn);
    }

    /// <summary>スキルを使う。使えたら true</summary>
    public bool TryUse(int index, SkillDefinition skill)
    {
        if (skill == null) return false;
        AttackData attack = GetNextAttack(skill);
        if (attack == null)
        {
            // 常に効く効果だけのスキル（Steam）は、押しても何もしない
            if (skill.Passive == null) Debug.LogWarning($"{skill.DisplayName}: 攻撃データが設定されていないので使えません", skill);
            return false;
        }

        float remain = GetCooldownRemaining(skill);
        if (remain > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, remain);
            return false;
        }
        if (!_runner.Run(attack)) return false;

        // 2回押しの能力（zip圧縮）は、1回目のあとはクールタイムを始めない（2026-11-18）
        bool defer = attack.Kind == AttackKind.Custom && attack.Custom != null && attack.Custom.DefersCooldown(_runner);
        if (!defer) _readyAt[skill] = Time.time + attack.Cooldown;
        SkillUsed?.Invoke(index, skill);
        return true;
    }

    /// <summary>クールタイムの残り秒数（使えるなら 0）</summary>
    public float GetCooldownRemaining(SkillDefinition skill)
    {
        if (skill == null || !_readyAt.TryGetValue(skill, out float t)) return 0f;
        return Mathf.Max(0f, t - Time.time);
    }

    /// <summary>クールタイムの残りの割合（1＝使った直後、0＝使える）。UI のゲージ用</summary>
    public float GetCooldownRatio(SkillDefinition skill)
    {
        if (skill == null) return 0f;
        float total = _cooldownOf.TryGetValue(skill, out float c) ? c : skill.Attack != null ? skill.Attack.Cooldown : 0f;
        if (total <= 0f) return 0f;
        return Mathf.Clamp01(GetCooldownRemaining(skill) / total);
    }
}
