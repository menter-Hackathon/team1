using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力 ハンマー（Xcode を倒すと手に入る。2026-11-19。docs/app-kits-2-design.md 3-2）。1回押し。
/// ため 0.35秒（その間は動けない）→ 正面 3×3マスに大きいハンマーをふり下ろす（判定 0.15秒。ダメージ・ゲージは攻撃データの共通の欄）。
/// とどめに使うと、右上の通知が Build Succeeded。
/// </summary>
[CreateAssetMenu(fileName = "Xcode_HammerSkill", menuName = "Team1/Kits/Xcode/Hammer Skill")]
public class XcodeHammerSkill : CustomAttack
{
    [Tooltip("【仮】ため（秒。動けない）")]
    [SerializeField, Min(0f)] float _charge = 0.35f;
    [Tooltip("【仮】判定の大きさ（マス。体の前の端から横、足元から縦）")]
    [SerializeField] Vector2 _size = new Vector2(3f, 3f);
    [Tooltip("【仮】判定の時間（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;

    public float Charge => _charge;
    public Vector2 Size => _size;
    public float HitTime => _hitTime;

    public override bool CanRun(AttackRunner owner, AttackData data) => owner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        try { XcodeHammerSwing.Spawn(owner, data, this); }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        yield break;
    }
}
