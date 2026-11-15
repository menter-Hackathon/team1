using System;
using UnityEngine;

/// <summary>
/// ウイルス解除ゲージ（仕様書 4.3、docs 3章）。アプリボスは HP を持たず、攻撃を当てるとこのゲージが増える。
/// ・プレイヤーの攻撃の「ゲージ増加」（DamageInfo.GaugeGain）だけ増える。ダメージ量は見ない
/// ・最大値まで増えたら Filled（正常化）。それ以上は増えない
/// ・Invincible の間は増えない（形態変化の演出中など）
/// 被弾の窓口（IDamageable）なので、ボスの Hurtbox はこのゲージに攻撃を届ける。
/// </summary>
public class VirusGauge : MonoBehaviour, IDamageable
{
    [SerializeField] Team _team = Team.Enemy;
    [Tooltip("最大値（docs 3章：100）。使用時間で 80〜140 に変える案がある【提案】")]
    [SerializeField, Min(1)] int _max = 100;

    public Team Team => _team;
    public int Current { get; private set; }
    public int Max => _max;
    /// <summary>今の割合（0〜1）</summary>
    public float Ratio => (float)Current / _max;
    public bool IsFull => Current >= _max;
    /// <summary>true の間は攻撃が当たらない（ゲージが増えない）</summary>
    public bool Invincible { get; set; }

    /// <summary>値が変わったとき（現在値, 最大値）</summary>
    public event Action<int, int> Changed;
    /// <summary>攻撃が当たってゲージが増えたとき（点滅・SE用）</summary>
    public event Action<DamageInfo> Gained;
    /// <summary>最大値になったとき（1回だけ）</summary>
    public event Action Filled;

    public bool TryTakeDamage(in DamageInfo info)
    {
        if (!isActiveAndEnabled || IsFull || Invincible) return false;
        if (info.SourceTeam == _team || info.GaugeGain <= 0) return false;
        Add(info.GaugeGain);
        Gained?.Invoke(info);
        return true;
    }

    /// <summary>ゲージを直接増やす（デバッグ用。無敵中でも増える）</summary>
    public void Add(int amount)
    {
        if (IsFull || amount <= 0) return;
        Current = Mathf.Min(_max, Current + amount);
        Changed?.Invoke(Current, _max);
        if (IsFull) Filled?.Invoke();
    }

    /// <summary>最大値を変える（使用時間による強さの調整用）。今の値は 0 に戻す</summary>
    public void SetMax(int max)
    {
        _max = Mathf.Max(1, max);
        ResetGauge();
    }

    /// <summary>0 に戻す（やり直し用）</summary>
    public void ResetGauge()
    {
        Current = 0;
        Invincible = false;
        Changed?.Invoke(Current, _max);
    }
}
