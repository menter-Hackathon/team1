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
    /// <summary>どれかのゲージが最大値になったとき（とどめの隅の通知・Terminal の [Process completed]。2026-11-18：スキルの見た目 1-5）</summary>
    public static event Action<VirusGauge> AnyFilled;
    /// <summary>どれかのボスのゲージが、攻撃で増えたとき（ゲージ, 攻撃の中身）。実績（Steam）の「ボスに初めてダメージ」用</summary>
    public static event Action<VirusGauge, DamageInfo> AnyGained;

    public bool TryTakeDamage(in DamageInfo info)
    {
        if (!isActiveAndEnabled || IsFull || Invincible) return false;
        if (info.SourceTeam == _team || info.GaugeGain <= 0) return false;
        // 受けるゲージの倍率（2026-11-19：アプリのキット。StatMods.Boss.GaugeTaken → GameBuffs）。近接なら近接の倍率も掛ける
        float k = GameBuffs.BossGaugeTaken * (IsMelee(info) ? GameBuffs.PlayerMeleeGauge : 1f);
        Add(Mathf.Max(1, Mathf.RoundToInt(info.GaugeGain * k)));
        Gained?.Invoke(info);
        if (!IsFull) GameAudio.Event("boss_hit"); // 満タンにした一撃は boss_purged の音
        return true;
    }

    /// <summary>プレイヤーの近接の当たりか（近接の判定は攻撃データを持たず、当てた物がプレイヤー。2026-11-19）</summary>
    static bool IsMelee(in DamageInfo info)
    {
        if (info.SourceTeam != Team.Player || info.Data != null || info.Source == null) return false;
        return info.Source.GetComponentInParent<PlayerMeleeAttack>() != null;
    }

    /// <summary>ゲージを直接増やす（デバッグ用。無敵中でも増える）</summary>
    public void Add(int amount)
    {
        if (IsFull || amount <= 0) return;
        Current = Mathf.Min(_max, Current + amount);
        Changed?.Invoke(Current, _max);
        if (IsFull)
        {
            Filled?.Invoke();
            AnyFilled?.Invoke(this);
        }
    }

    /// <summary>
    /// 【仮】ゲージを戻す（2026-11-19：アプリのキット E5。Zoom の 40分で切れる）。0 より下にしない。満タンなら戻さない。
    /// 50％を下回っても、ボスは第2形態のまま（BossController は形態を戻さない）。戻した量を返す
    /// </summary>
    public int Remove(int amount)
    {
        if (IsFull || amount <= 0 || Current <= 0) return 0;
        int before = Current;
        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, _max);
        return before - Current;
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
