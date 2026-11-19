using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】コンパス（Safari のボスの技1。2026-11-19。docs/app-kits-2-design.md 4-4）。
/// ボスの上でコンパスの針が回る（0.5秒。水色の線がプレイヤーを追う＝まだ安全）→ 止まって赤い線（0.8秒。固定）→
/// 針が線の上を飛ぶ（毎秒 18マス。壁まで。弾の数値は _bullet）。3回。
/// 第2形態：5回。針の白い側も、反対向きに飛ぶ。避け方：赤い線から出る。
/// </summary>
[CreateAssetMenu(fileName = "Safari_Compass", menuName = "Team1/Kits/Safari/Compass")]
public class SafariCompassAttack : CustomAttack
{
    [Tooltip("飛ぶ針（弾の攻撃データ。名前に枠の語を入れない）")]
    [SerializeField] AttackData _bullet;
    [Tooltip("【仮】何回か")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("【仮】針が回る（秒。水色の線が追う）")]
    [SerializeField, Min(0f)] float _spin = 0.5f;
    [Tooltip("【仮】赤い線の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】オン：白い側も反対向きに飛ぶ（第2形態）")]
    [SerializeField] bool _both;

    public AttackData Bullet => _bullet;
    public int Count => _count;
    public float Spin => _spin;
    public float Telegraph => _telegraph;
    public bool Both => _both;

    public override bool CanRun(AttackRunner owner, AttackData data) => _bullet != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            if (SafariCompassSeq.Spawn(owner, this) != null) length = _count * (_spin + _telegraph) + 0.15f;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
