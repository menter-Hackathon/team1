using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】プライベートブラウズ（Safari のボスの第2形態の技。2026-11-19。docs/app-kits-2-design.md 4-4）。場に残る技。
/// 画面が暗くなる（3.5秒）。ボスは目だけ見える（体はうすくする）。その間に、ねらう弾を 2回（予告の赤い線 0.8秒・弾は明るく見える）。
/// 暗くする板はプレイヤー・雑魚・弾より後ろ（並びの順 9）。避け方：線を見る。
/// </summary>
[CreateAssetMenu(fileName = "Safari_Private", menuName = "Team1/Kits/Safari/Private")]
public class SafariPrivateAttack : CustomAttack
{
    [Tooltip("ねらう弾（弾の攻撃データ。名前に枠の語を入れない）")]
    [SerializeField] AttackData _bullet;
    [Tooltip("【仮】暗い時間（秒）")]
    [SerializeField, Min(1f)] float _duration = 3.5f;
    [Tooltip("【仮】弾の回数")]
    [SerializeField, Min(1)] int _shots = 2;
    [Tooltip("【仮】最初の予告が出るまで（秒）")]
    [SerializeField, Min(0f)] float _firstDelay = 0.5f;
    [Tooltip("【仮】予告の線（秒）")]
    [SerializeField, Min(0.8f)] float _lineTime = 0.8f;
    [Tooltip("【仮】1回ごとの間（秒）")]
    [SerializeField, Min(0.5f)] float _interval = 1.4f;

    public AttackData Bullet => _bullet;
    public float Duration => _duration;
    public int Shots => _shots;
    public float FirstDelay => _firstDelay;
    public float LineTime => _lineTime;
    public float Interval => _interval;

    public override bool CanRun(AttackRunner owner, AttackData data) => _bullet != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            if (SafariPrivateDark.Spawn(owner, this) != null) length = _duration;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
