using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル枠のボタン（1／2／3）が押されたら、そのスキルの攻撃データを実行する。クールタイムもここで数える。
/// ・クールタイムは攻撃データの「間隔」。使った瞬間から数える
/// ・クールタイムは能力ごとに覚える。捨てて拾い直しても、別の枠に入れ直しても、残り時間はそのまま
/// ・使えるかどうか（のけぞり中・入れ替え待ち中は使えない）は PlayerSkills が判断して SkillUseRequested を出す
/// ・2026-11-19（アプリのキット E2・E5）：クールタイムは毎フレーム「進み方の倍率」（GameBuffs.PlayerCooldown の逆数）で減らす
///   （途中で倍率が変わっても効く）。封印（Seal）・クールタイムを進める（AdvanceCooldowns）・全部回復（RefillCooldowns）を足した
/// </summary>
[RequireComponent(typeof(PlayerSkills), typeof(AttackRunner))]
public class PlayerSkillExecutor : MonoBehaviour
{
    PlayerSkills _skills;
    AttackRunner _runner;

    /// <summary>能力を実行する AttackRunner（HUD が、専用の技の状態を聞くとき用）</summary>
    public AttackRunner Runner => _runner;
    // 能力ごとの、クールタイムの残り（倍率をかける前の秒数。毎フレーム、進み方の倍率で減らす）
    readonly Dictionary<SkillDefinition, float> _remain = new Dictionary<SkillDefinition, float>();
    readonly List<SkillDefinition> _keys = new List<SkillDefinition>();
    // 封印（2026-11-19）：全部の枠と、枠ごとの「使えるようになる時刻」
    float _sealAllUntil;
    readonly float[] _sealSlotUntil = new float[8];

    /// <summary>スキルを使ったとき（枠の番号, スキル）。SE・エフェクト・UI 用</summary>
    public event Action<int, SkillDefinition> SkillUsed;
    /// <summary>クールタイム中で使えなかったとき（枠の番号, スキル, 残り秒数）。UI の「まだ使えない」表示用</summary>
    public event Action<int, SkillDefinition, float> SkillOnCooldown;

    void Awake()
    {
        _skills = GetComponent<PlayerSkills>();
        _runner = GetComponent<AttackRunner>();
    }

    void OnEnable() => _skills.SkillUseRequested += OnSkillUseRequested;
    void OnDisable() => _skills.SkillUseRequested -= OnSkillUseRequested;

    void OnSkillUseRequested(int index, SkillDefinition skill) => TryUse(index, skill);

    /// <summary>クールタイムの進み方の倍率（1.5 ＝ 1.5倍の速さで進む。GameBuffs.PlayerCooldown の逆数）</summary>
    static float Rate => 1f / Mathf.Max(0.1f, GameBuffs.PlayerCooldown);

    void Update()
    {
        if (_remain.Count == 0) return;
        float step = Time.deltaTime * Rate;
        if (step <= 0f) return;
        _keys.Clear();
        _keys.AddRange(_remain.Keys);
        foreach (var k in _keys)
        {
            float r = _remain[k] - step;
            if (r <= 0f || k == null)
            {
                _remain.Remove(k);
                if (k != null) GameAudio.Event("skill_ready");
            }
            else _remain[k] = r;
        }
    }

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

        float remain = GetCooldownRemaining(skill);
        if (remain > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, remain);
            GameAudio.Event("skill_denied");
            return false;
        }
        float sealed_ = SealRemaining(index);
        if (sealed_ > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, sealed_); // 封印中（2026-11-19）
            GameAudio.Event("skill_denied");
            return false;
        }
        DressFor(skill);
        if (!_runner.Run(attack))
        {
            GameAudio.Event("skill_denied"); // 相手がいない・使えない
            return false;
        }

        // 2回押しの能力（zip圧縮）は、1回目のあとはクールタイムを始めない（2026-11-18）
        bool defer = attack.Kind == AttackKind.Custom && attack.Custom != null && attack.Custom.DefersCooldown(_runner);
        if (!defer && attack.Cooldown > 0f) _remain[skill] = attack.Cooldown; // 2026-11-19：倍率は Update で（進み方）
        SkillUsed?.Invoke(index, skill);
        return true;
    }

    /// <summary>
    /// 能力の見た目（スキン・アプリの色・アプリのアイコン）を入れる（2026-11-18：スキルの見た目 1-1）。
    /// 能力は同時に使えるので、攻撃データごとに覚えさせる（SkinDresser.Remember）。AttackRunner.Skin は最後に使った能力
    /// </summary>
    void DressFor(SkillDefinition skill)
    {
        SkinId skin = SkinTables.ForSkill(skill);
        Sprite icon = null;
        GameSession session = GameSession.Instance;
        if (session != null && session.Run != null) icon = session.Run.SourceIcon(skill);
        if (icon == null) icon = skill.Icon;
        SkinDresser.Remember(skill.Attack, skin, skill.Theme, icon);
        _runner.Skin = skin;
        _runner.SkinTheme = skill.Theme;
        _runner.SkinIcon = icon;
    }

    /// <summary>クールタイムの残り秒数（今の進み方の倍率での秒数。使えるなら 0）</summary>
    public float GetCooldownRemaining(SkillDefinition skill)
    {
        if (skill == null || !_remain.TryGetValue(skill, out float r)) return 0f;
        return Mathf.Max(0f, r / Rate);
    }

    /// <summary>クールタイムの残りの割合（1＝使った直後、0＝使える）。UI のゲージ用</summary>
    public float GetCooldownRatio(SkillDefinition skill)
    {
        if (skill == null || skill.Attack == null || skill.Attack.Cooldown <= 0f) return 0f;
        if (!_remain.TryGetValue(skill, out float r)) return 0f;
        return Mathf.Clamp01(r / skill.Attack.Cooldown);
    }

    // ───────── 2026-11-19：アプリのキットの入口（E5。KitPlayer から呼ぶ） ─────────

    /// <summary>全部の能力のクールタイムを seconds 秒進める（倍率をかける前の秒数）</summary>
    public void AdvanceCooldowns(float seconds)
    {
        if (seconds <= 0f || _remain.Count == 0) return;
        _keys.Clear();
        _keys.AddRange(_remain.Keys);
        foreach (var k in _keys)
        {
            float r = _remain[k] - seconds;
            if (r <= 0f) _remain.Remove(k);
            else _remain[k] = r;
        }
    }

    /// <summary>全部の能力のクールタイムを回復する（すぐ使える）</summary>
    public void RefillCooldowns() => _remain.Clear();

    /// <summary>能力の枠を seconds 秒使えなくする。slot が負なら全部の枠。重ねたら長い方</summary>
    public void Seal(float seconds, int slot = -1)
    {
        float until = Time.time + Mathf.Max(0f, seconds);
        if (slot < 0) _sealAllUntil = Mathf.Max(_sealAllUntil, until);
        else if (slot < _sealSlotUntil.Length) _sealSlotUntil[slot] = Mathf.Max(_sealSlotUntil[slot], until);
    }

    /// <summary>封印を全部とく</summary>
    public void Unseal()
    {
        _sealAllUntil = 0f;
        for (int i = 0; i < _sealSlotUntil.Length; i++) _sealSlotUntil[i] = 0f;
    }

    /// <summary>その枠が封印されているか</summary>
    public bool IsSealed(int slot) => SealRemaining(slot) > 0f;

    /// <summary>その枠の封印の残り秒数（無ければ 0）</summary>
    public float SealRemaining(int slot)
    {
        float until = _sealAllUntil;
        if (slot >= 0 && slot < _sealSlotUntil.Length) until = Mathf.Max(until, _sealSlotUntil[slot]);
        return Mathf.Max(0f, until - Time.time);
    }
}
