using System;
using UnityEngine;

/// <summary>
/// 体力。プレイヤー（ライフ制）と敵（HP制）の共通部品。
/// ・プレイヤー：最大値3、「1回の被弾で1減らす」をオン → 3回当たるとアウト
/// ・敵：最大値をHPにして、攻撃のダメージ量の分だけ減らす
/// 被弾したときの動き（ノックバック・点滅など）は別のコンポーネントがイベントを受け取って行う。
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] Team _team = Team.Enemy;
    [Tooltip("最大値。プレイヤーはライフの数、敵はHP")]
    [SerializeField, Min(1)] int _max = 3;
    [Tooltip("オンにすると攻撃のダメージ量に関係なく、1回の被弾で1だけ減る（プレイヤーのライフ制用）")]
    [SerializeField] bool _oneDamagePerHit;
    [Tooltip("被弾した後の無敵時間（秒）。敵は0で、1回の攻撃が何度も当たらないように攻撃側で防ぐ")]
    [SerializeField, Min(0f)] float _invincibleTimeAfterHit;

    float _invincibleUntil = float.NegativeInfinity;

    public Team Team => _team;
    public int Current { get; private set; }
    public int Max => _max;
    public bool IsDead { get; private set; }
    public bool IsInvincible => Time.time < _invincibleUntil;

    /// <summary>値が変わったとき（現在値, 最大値）。HP表示・ハート表示、ボスの形態変化の判定などに使う</summary>
    public event Action<int, int> Changed;
    /// <summary>ダメージを受けたとき（ノックバック・点滅・SE用）</summary>
    public event Action<DamageInfo> Damaged;
    /// <summary>0になったとき（1回だけ呼ばれる）</summary>
    public event Action Died;

    void Awake()
    {
        Current = _max;
    }

    public bool TryTakeDamage(in DamageInfo info)
    {
        if (!isActiveAndEnabled || IsDead || IsInvincible) return false;
        if (info.SourceTeam == _team) return false;

        int amount = _oneDamagePerHit ? 1 : info.Amount;
        if (amount <= 0) return false;

        Current = Mathf.Max(0, Current - amount);
        if (Current > 0) SetInvincible(_invincibleTimeAfterHit);

        Changed?.Invoke(Current, _max);
        Damaged?.Invoke(info);

        if (Current == 0)
        {
            IsDead = true;
            Died?.Invoke();
        }
        return true;
    }

    /// <summary>回復する（倒れた後は回復しない）</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        int before = Current;
        Current = Mathf.Min(_max, Current + amount);
        if (Current != before) Changed?.Invoke(Current, _max);
    }

    /// <summary>指定した秒数だけ無敵にする（回避スキルなど）。すでに長い無敵中なら短くはしない</summary>
    public void SetInvincible(float seconds)
    {
        _invincibleUntil = Mathf.Max(_invincibleUntil, Time.time + seconds);
    }

    /// <summary>最大値まで戻して生き返らせる（リトライなど）</summary>
    public void ResetHealth()
    {
        IsDead = false;
        Current = _max;
        _invincibleUntil = float.NegativeInfinity;
        Changed?.Invoke(Current, _max);
    }
}
