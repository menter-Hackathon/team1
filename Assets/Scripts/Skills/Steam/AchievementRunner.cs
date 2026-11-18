using System;
using UnityEngine;

/// <summary>
/// Steam スキルの本体（AchievementPassive.Attach が持ち主に付ける）。戦闘中の出来事を AchievementTracker に伝え、
/// 実績を解除したら：① 画面の隅に通知 ② 体の中心に Steam_BuffBurst ③ バフ（StatModifiers）④ バフの間、足元に Steam_BuffAura。
/// 持ち主の陣営で数え方を合わせる（プレイヤーなら「敵を倒す・ボスにダメージ」、ボスなら「プレイヤーに当てる」）。
/// 外すとき（枠から外した・ボスを正常化した）は Dispose。かかっているバフは時間まで残る。
/// </summary>
public class AchievementRunner : MonoBehaviour, IDisposable
{
    AchievementPassive _passive;
    AchievementTracker _tracker;
    Team _team = Team.Player;
    Health _health;
    VirusGauge _gauge;
    PlayerSkillExecutor _executor;
    BossController _boss;
    StatModifiers _stats;
    SpriteRenderer _aura;
    bool _subscribed;

    /// <summary>どれかの持ち主が実績を解除したとき（持ち主, 実績）。デバッグ表示・SE 用</summary>
    public static event Action<AchievementRunner, AchievementDefinition> AnyUnlocked;

    public AchievementPassive Passive => _passive;
    public AchievementTracker Tracker => _tracker;
    public Team Team => _team;

    public void Setup(AchievementPassive passive)
    {
        _passive = passive;
        var specs = new AchievementSpec[passive.Achievements.Length];
        for (int i = 0; i < specs.Length; i++) specs[i] = passive.Achievements[i] != null ? passive.Achievements[i].Spec : default;
        _tracker = new AchievementTracker(specs);
        _tracker.Unlocked += OnUnlocked;

        _health = GetComponent<Health>();
        _gauge = GetComponent<VirusGauge>();
        _executor = GetComponent<PlayerSkillExecutor>();
        _boss = GetComponent<BossController>();
        if (_health != null) _team = _health.Team;
        else if (_gauge != null) _team = _gauge.Team;
        _stats = StatModifiers.GetOrAdd(gameObject);

        Subscribe();
        _tracker.Begin(Time.time);
    }

    /// <summary>数え直す（新しい戦闘・やり直し）</summary>
    public void Restart() => _tracker?.Begin(Time.time);

    /// <summary>条件に関係なく解除する（デバッグ用）</summary>
    public bool ForceUnlock(int index) => _tracker != null && _tracker.ForceUnlock(index);

    void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        Health.AnyDied += OnAnyDied;
        Health.AnyDamaged += OnAnyDamaged;
        VirusGauge.AnyGained += OnAnyGained;
        if (_executor != null) _executor.SkillUsed += OnSkillUsed;
        if (_boss != null) _boss.MoveStarted += OnBossMove;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        Health.AnyDied -= OnAnyDied;
        Health.AnyDamaged -= OnAnyDamaged;
        VirusGauge.AnyGained -= OnAnyGained;
        if (_executor != null) _executor.SkillUsed -= OnSkillUsed;
        if (_boss != null) _boss.MoveStarted -= OnBossMove;
    }

    void OnAnyDied(Health victim)
    {
        if (victim != null && victim.Team != _team) _tracker.Report(AchievementEvent.Kill, Time.time);
    }

    void OnAnyDamaged(Health victim, DamageInfo info)
    {
        if (victim == _health) _tracker.Report(AchievementEvent.DamageTaken, Time.time);
        else if (victim != null && victim.Team != _team && info.SourceTeam == _team) _tracker.Report(AchievementEvent.HitLanded, Time.time);
    }

    void OnAnyGained(VirusGauge gauge, DamageInfo info)
    {
        if (gauge == _gauge)
        {
            _tracker.Report(AchievementEvent.DamageTaken, Time.time); // ボス：ゲージが増えた＝ダメージを受けた
            return;
        }
        if (gauge != null && gauge.Team != _team && info.SourceTeam == _team)
        {
            _tracker.Report(AchievementEvent.BossDamaged, Time.time);
            _tracker.Report(AchievementEvent.HitLanded, Time.time);
        }
    }

    void OnSkillUsed(int index, SkillDefinition skill) => _tracker.Report(AchievementEvent.SkillUsed, Time.time);
    void OnBossMove(AttackData move) => _tracker.Report(AchievementEvent.SkillUsed, Time.time);

    void Update()
    {
        if (_tracker == null) return;
        _tracker.Tick(Time.time);
        UpdateAura();
    }

    void OnUnlocked(int index)
    {
        AchievementDefinition def = _passive.Achievements[index];
        string title = def != null ? def.DisplayName : _tracker.Get(index).Name;

        AchievementToast.Show(_passive, title);
        Bounds body = BodyBounds(gameObject);
        Vfx.Play(_passive.BurstFrames, body.center, _passive.BurstFrameRate, 40);

        _stats.Apply(_passive.BuffId, _passive.Buff);
        if (_passive.Targets == BuffTargets.SelfAndAllies)
        {
            // 同じ陣営で、ステージに出ている物（ボス版：雑魚ウイルス）にも同じバフ
            foreach (var h in FindObjectsOfType<Health>())
            {
                if (h == null || h.gameObject == gameObject || h.IsDead || h.Team != _team) continue;
                StatModifiers.GetOrAdd(h.gameObject).Apply(_passive.BuffId, _passive.Buff);
            }
        }
        AnyUnlocked?.Invoke(this, def);
    }

    void UpdateAura()
    {
        bool on = _stats != null && _stats.Has(_passive.BuffId);
        Sprite[] frames = _passive.AuraFrames;
        if (!on || frames == null || frames.Length == 0 || frames[0] == null)
        {
            if (_aura != null) _aura.gameObject.SetActive(false);
            return;
        }
        if (_aura == null)
        {
            var go = new GameObject("BuffAura");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _aura = go.AddComponent<SpriteRenderer>();
            _aura.sortingOrder = 14; // プレイヤーの絵（15）のすぐ後ろ
            go.AddComponent<SpriteFrameAnimation>().Setup(_aura, frames, _passive.AuraFrameRate, true, false);
        }
        if (!_aura.gameObject.activeSelf) _aura.gameObject.SetActive(true);
        Bounds b = BodyBounds(gameObject);
        _aura.transform.position = new Vector3(b.center.x, b.min.y, transform.position.z); // 足元（素材の原点は下の中央）
    }

    /// <summary>体の範囲（ボスは被弾範囲、プレイヤー・雑魚は移動用の当たり判定）</summary>
    static Bounds BodyBounds(GameObject go)
    {
        if (go.TryGetComponent(out IWarpable w)) return w.BodyBounds;
        foreach (var col in go.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds;
        }
        return new Bounds(go.transform.position, Vector3.one);
    }

    public void Dispose()
    {
        if (this == null) return;
        Destroy(this);
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (_tracker != null) _tracker.Unlocked -= OnUnlocked;
        if (_aura != null) Destroy(_aura.gameObject);
    }
}
