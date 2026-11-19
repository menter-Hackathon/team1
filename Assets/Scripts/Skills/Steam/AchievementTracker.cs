using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>実績の条件の種類。アセットには数値で保存されるので、値は明示しておく（消すときは欠番にする）</summary>
public enum AchievementCondition
{
    /// <summary>「期間（秒）」以内に「回数」体を倒す</summary>
    KillsWithin = 0,
    /// <summary>「期間（秒）」のあいだダメージを受けない（ボスはゲージが増えない）</summary>
    NoDamageFor = 1,
    /// <summary>スキルを「回数」回使う（ボスは技を出した回数）</summary>
    SkillUses = 2,
    /// <summary>アプリボスに初めてダメージを与える</summary>
    FirstBossDamage = 3,
    /// <summary>相手に攻撃を「回数」回当てる（ボス：プレイヤーに3回当てる）</summary>
    HitsLanded = 4,
    /// <summary>戦闘が始まってから「期間（秒）」たつ</summary>
    TimeElapsed = 5,
}

/// <summary>実績を数えるきっかけ（AchievementRunner がゲームの出来事から作って渡す）</summary>
public enum AchievementEvent
{
    Kill,
    DamageTaken,
    SkillUsed,
    BossDamaged,
    HitLanded,
}

/// <summary>実績1つ分の中身（名前・条件・回数・期間）</summary>
[Serializable]
public struct AchievementSpec
{
    [Tooltip("通知に出す名前")]
    public string Name;
    public AchievementCondition Condition;
    [Tooltip("回数（倒す数・使う回数・当てる回数）")]
    [Min(1)] public int Count;
    [Tooltip("期間（秒）。「以内に倒す」「ダメージを受けない」「経過」で使う")]
    [Min(0f)] public float Window;

    public AchievementSpec(string name, AchievementCondition condition, int count = 1, float window = 0f)
    {
        Name = name;
        Condition = condition;
        Count = Mathf.Max(1, count);
        Window = window;
    }
}

/// <summary>
/// 戦闘中の実績を数えて、条件を満たしたら解除する（Steam スキル）。時間は呼ぶ側から渡す（テストできるように）。
/// ・同じ実績は、1回の戦闘（Begin から次の Begin まで）で1度だけ解除する
/// ・時間で決まる実績（ダメージを受けない・経過）は Tick で調べる
/// </summary>
public class AchievementTracker
{
    readonly AchievementSpec[] _specs;
    readonly bool[] _unlocked;
    readonly Queue<float> _kills = new Queue<float>();
    float _start;
    float _lastDamage;
    int _skillUses;
    int _hits;

    /// <summary>実績を解除したとき（実績の番号）</summary>
    public event Action<int> Unlocked;

    public AchievementTracker(IReadOnlyList<AchievementSpec> specs)
    {
        _specs = new AchievementSpec[specs?.Count ?? 0];
        for (int i = 0; i < _specs.Length; i++) _specs[i] = specs[i];
        _unlocked = new bool[_specs.Length];
    }

    public int Count => _specs.Length;
    public AchievementSpec Get(int index) => _specs[index];
    public bool IsUnlocked(int index) => _unlocked[index];

    /// <summary>戦闘を始める（数え直す。解除済みの印も消す）</summary>
    public void Begin(float now)
    {
        _start = now;
        _lastDamage = now;
        _skillUses = 0;
        _hits = 0;
        _kills.Clear();
        Array.Clear(_unlocked, 0, _unlocked.Length);
    }

    /// <summary>出来事を伝える</summary>
    public void Report(AchievementEvent e, float now)
    {
        switch (e)
        {
            case AchievementEvent.Kill: _kills.Enqueue(now); break;
            case AchievementEvent.DamageTaken: _lastDamage = now; break;
            case AchievementEvent.SkillUsed: _skillUses++; break;
            case AchievementEvent.HitLanded: _hits++; break;
        }

        for (int i = 0; i < _specs.Length; i++)
        {
            if (_unlocked[i]) continue;
            AchievementSpec s = _specs[i];
            bool done = s.Condition switch
            {
                AchievementCondition.KillsWithin => e == AchievementEvent.Kill && KillsInWindow(now, s.Window) >= s.Count,
                AchievementCondition.SkillUses => e == AchievementEvent.SkillUsed && _skillUses >= s.Count,
                AchievementCondition.FirstBossDamage => e == AchievementEvent.BossDamaged,
                AchievementCondition.HitsLanded => e == AchievementEvent.HitLanded && _hits >= s.Count,
                _ => false,
            };
            if (done) Unlock(i);
        }
    }

    /// <summary>時間で決まる実績を調べる（毎フレーム呼ぶ）</summary>
    public void Tick(float now)
    {
        for (int i = 0; i < _specs.Length; i++)
        {
            if (_unlocked[i]) continue;
            AchievementSpec s = _specs[i];
            bool done = s.Condition switch
            {
                AchievementCondition.NoDamageFor => now - _lastDamage >= s.Window,
                AchievementCondition.TimeElapsed => now - _start >= s.Window,
                _ => false,
            };
            if (done) Unlock(i);
        }
    }

    /// <summary>条件に関係なく解除する（デバッグ用）。もう解除していれば false</summary>
    public bool ForceUnlock(int index)
    {
        if (index < 0 || index >= _specs.Length || _unlocked[index]) return false;
        Unlock(index);
        return true;
    }

    int KillsInWindow(float now, float window)
    {
        while (_kills.Count > 0 && now - _kills.Peek() > window) _kills.Dequeue();
        return _kills.Count;
    }

    void Unlock(int index)
    {
        _unlocked[index] = true;
        Unlocked?.Invoke(index);
    }
}
