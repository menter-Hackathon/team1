using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力「フライイン」（PowerPoint。2026-11-19。docs/app-kits-1-design.md 11章）。数値はすべて【仮】。
/// 押すと、自分の後ろ（壁、遠ければ 10マス後ろ）から、大きい矢印が自分の高さで飛んできて、前の壁まで横切る（毎秒 20マス。
/// 押してから自分の所を通るまで 最大 0.5秒）。通り道の敵すべてに1回ずつ当たる（雑魚は倒れる。ボスにゲージ +12）。
/// 画面の横の全部に届く。高さは自分で合わせる（足場の上で使えば、浮くボスに届く）。
/// 速さ・大きさ・ゲージ・ダメージは攻撃データ（地形に当たったら消える＝前の壁で止まる。相手に当たっても消えない）
/// </summary>
[CreateAssetMenu(fileName = "PowerPoint_FlyInSkill", menuName = "Team1/Kits/PowerPoint Fly In Skill")]
public class PowerPointFlyInSkill : CustomAttack
{
    [Tooltip("後ろから出す距離の上限（マス）。毎秒 20マスなら 10マスで 0.5秒")]
    [SerializeField, Min(1f)] float _maxBehind = 10f;
    [Tooltip("足元から、矢印の真ん中までの高さ（マス）")]
    [SerializeField] float _height = 0.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect area = OfficeKitTools.PlayerArea(owner);
        Vector2 feet = owner.transform.position;
        int dir = owner.Facing >= 0 ? 1 : -1;
        float half = data.HitSize.x * 0.5f;
        float backX = dir > 0
            ? Mathf.Max(area.xMin + half + 0.1f, feet.x - _maxBehind)
            : Mathf.Min(area.xMax - half - 0.1f, feet.x + _maxBehind);
        Vector2 from = new Vector2(backX, feet.y + _height);
        // 後ろの壁に、白い矢印の印（どこから来るか）
        FxAnim.Sprite(PowerPointArt.SkillArrow, from, 23, 0.15f, new Color(1f, 1f, 1f, 0.5f), 1f, "PPT_SkillMark").Pop();
        Projectile p = Projectile.Spawn(owner, data, from, new Vector2(dir, 0f));
        if (p != null)
        {
            KitDress.Swap(p.gameObject, PowerPointArt.SkillArrow);
            PowerPointTrail.Attach(p.gameObject, PowerPointArt.SkillArrow, data.HitSize, 22);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(0.15f);
    }
}
