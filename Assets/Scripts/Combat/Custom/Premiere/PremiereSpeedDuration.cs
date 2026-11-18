using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スピードデュレーション（Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-4）。数値は【仮】。場に残る技。ダメージなし。
/// 画面の上のほうに「速度・デュレーション」のウィンドウが出て、数字が 100 % から回る（0.8秒）。出し方は交互：
/// ・1回目：スローの場（50 %）。プレイヤーの今いる場所に半径3マスの円。中にいる間、プレイヤーの横の速さが 0.5倍（4秒）
/// ・2回目：ボスの倍速（300 %）。ボスの移動 3倍、攻撃のあとの待ち時間 0.5倍（5秒）。予告・弾の速さ・判定は変えない
/// ゲーム全体の時間（Time.timeScale）は使わない。出したあとは PremiereSpeedEffect が自分で進み、終わる・消されるときに倍率を 1 に戻す
/// </summary>
public class PremiereSpeedDuration : CustomAttack
{
    [Header("ウィンドウ（3-4）")]
    [Tooltip("ウィンドウが出て、数字が回り終えるまで（秒）。ここで効果が始まる")]
    [SerializeField, Min(0f)] float _windowTime = 0.8f;
    [Tooltip("ウィンドウが閉じるまで（秒）")]
    [SerializeField, Min(0f)] float _closeTime = 0.4f;

    [Header("スローの場")]
    [SerializeField, Min(0.1f)] float _slowRadius = 3f;
    [SerializeField, Min(0f)] float _slowDuration = 4f;
    [Tooltip("中にいる間の、プレイヤーの横の速さの倍率")]
    [SerializeField, Range(0.05f, 1f)] float _slowMultiplier = 0.5f;

    [Header("ボスの倍速")]
    [SerializeField, Min(0f)] float _boostDuration = 5f;
    [Tooltip("ボスの移動の速さの倍率")]
    [SerializeField, Min(0.05f)] float _boostMoveMultiplier = 3f;
    [Tooltip("ボスの攻撃のあとの待ち時間の倍率")]
    [SerializeField, Min(0.05f)] float _boostWaitMultiplier = 0.5f;

    public float WindowTime => _windowTime;
    public float CloseTime => _closeTime;
    public float SlowRadius => _slowRadius;
    public float SlowDuration => _slowDuration;
    public float SlowMultiplier => _slowMultiplier;
    public float BoostDuration => _boostDuration;
    public float BoostMoveMultiplier => _boostMoveMultiplier;
    public float BoostWaitMultiplier => _boostWaitMultiplier;

    // 出した回数（ボスごと）。偶数回目はスロー、奇数回目は倍速。アセットには保存しない
    readonly Dictionary<AttackRunner, int> _uses = new Dictionary<AttackRunner, int>();

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (!_uses.TryGetValue(owner, out int count)) PruneDeadOwners();
        _uses[owner] = count + 1;
        bool slow = count % 2 == 0;
        PremiereSpeedEffect.Spawn(owner, this, slow);
        owner.NotifyReleased(data);
        // ウィンドウが閉じるまで待つ（効果は場に残る）
        float end = Time.time + _windowTime + _closeTime;
        while (Time.time < end) yield return null;
    }

    /// <summary>消えたボス（前のシーン）の分を捨てる</summary>
    void PruneDeadOwners()
    {
        List<AttackRunner> dead = null;
        foreach (var key in _uses.Keys)
        {
            if (key == null) (dead ?? (dead = new List<AttackRunner>())).Add(key);
        }
        if (dead == null) return;
        foreach (var key in dead) _uses.Remove(key);
    }
}
