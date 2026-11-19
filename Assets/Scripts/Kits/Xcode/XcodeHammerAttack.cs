using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Build（ハンマー。Xcode のボスの技1。2026-11-19。docs/app-kits-2-design.md 3-2）。
/// プレイヤーの真上に大きいハンマー、床に幅 3マスの赤い影（0.9秒。影は出た時の位置で固定）→ ふり下ろす（影の真上の柱が当たる）→
/// 床を左右へ 3マスずつ、低い衝撃（高さ 0.8マス。毎秒 8マス。弾の数値は _shock）。
/// 第2形態：3回続ける（1回ごとに、その時のプレイヤーの真上を狙う）。避け方：影から出る。衝撃はジャンプ。
/// </summary>
[CreateAssetMenu(fileName = "Xcode_Hammer", menuName = "Team1/Kits/Xcode/Hammer")]
public class XcodeHammerAttack : CustomAttack
{
    [Tooltip("床を走る衝撃（弾の攻撃データ。名前に枠の語を入れない）")]
    [SerializeField] AttackData _shock;
    [Tooltip("【仮】何回ふり下ろすか")]
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("【仮】1回ごとの間（秒。次の影が出るまで）")]
    [SerializeField, Min(0.3f)] float _interval = 1.5f;
    [Tooltip("【仮】予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [Tooltip("【仮】影の幅（マス）")]
    [SerializeField, Min(1f)] float _width = 3f;

    public AttackData Shock => _shock;
    public float Telegraph => _telegraph;
    public float Width => _width;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            for (int i = 0; i < _count; i++) XcodeHammerDrop.Spawn(owner, data, this, i * _interval);
            length = (_count - 1) * _interval + _telegraph + 0.6f;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
