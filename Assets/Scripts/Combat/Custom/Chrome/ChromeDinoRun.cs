using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Chrome 専用の能力「恐竜ラン」（2026-11-19。docs/app-feedback-v2-design.md 2-4）。1回押し。
/// ・押すと、足元の床に恐竜が現れ、向いている方へ床の上を走る（毎秒 11マス、12マスまで）。空中で押したら、真下の床に現れる
/// ・触れた雑魚をはね飛ばす（貫通。1体に1回、ダメージ 3）。ボスには1回だけ（ゲージ +12）。敵の弾は消さない
/// ・前方 3マス以内に、床から 1.5マスより高い相手がいたら、1回だけ跳ぶ（高さ 3.5マス）
/// ・壁に当たると、目が × になって 0.3秒止まって消える。12マス走り切ったら、うすくなって消える。穴の上は、そのまま走り抜ける（割り切り）
/// ダメージ・ゲージ・クールタイムは攻撃データの共通の欄（ダメージ 3・ゲージ 12・クールタイム 3.5）。判定は 1.2×1.3（恐竜の大きさ）
/// </summary>
[CreateAssetMenu(fileName = "Chrome_DinoRun", menuName = "Team1/Custom Attack/Chrome Dino Run")]
public class ChromeDinoRun : CustomAttack
{
    [Tooltip("【仮】走る速さ（マス/秒）")]
    [SerializeField, Min(0.1f)] float _speed = 11f;
    [Tooltip("【仮】走る距離（マス）")]
    [SerializeField, Min(0.1f)] float _distance = 12f;
    [Tooltip("【仮】恐竜の大きさ＝判定（マス）")]
    [SerializeField] Vector2 _size = new Vector2(1.2f, 1.3f);
    [Tooltip("【仮】跳ぶ相手を探す、前方の距離（マス）")]
    [SerializeField, Min(0f)] float _jumpLook = 3f;
    [Tooltip("【仮】この高さ（床から、マス）より上にいる相手なら跳ぶ")]
    [SerializeField, Min(0f)] float _jumpTrigger = 1.5f;
    [Tooltip("【仮】跳ぶ高さ（マス）")]
    [SerializeField, Min(0f)] float _jumpHeight = 3.5f;
    [Tooltip("【仮】跳んでいる時間（秒）")]
    [SerializeField, Min(0.1f)] float _jumpTime = 0.6f;

    public float Speed => _speed;
    public float Distance => _distance;
    public Vector2 Size => _size;
    public float JumpLook => _jumpLook;
    public float JumpTrigger => _jumpTrigger;
    public float JumpHeight => _jumpHeight;
    public float JumpTime => _jumpTime;

    public override bool CanRun(AttackRunner owner, AttackData data) => owner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 feet = Feet(owner);
        // 空中なら、真下の床に現れる
        float groundY = feet.y;
        if (owner.TryFindGround(feet.x, feet.y + 0.3f, out float gy)) groundY = gy;
        int dir = owner.Facing >= 0 ? 1 : -1;
        ChromeDinoEffect.Spawn(owner, data, this, new Vector2(feet.x, groundY), dir);
        owner.NotifyReleased(data);
        yield break;
    }

    /// <summary>HUD の枠の絵：恐竜の横顔</summary>
    public override Sprite DisplayIcon(AttackRunner owner) => ChromeArt.DinoIcon();

    /// <summary>持ち主の足元（移動用の当たり判定の下の端。無ければ位置）</summary>
    static Vector2 Feet(AttackRunner owner)
    {
        foreach (var col in owner.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return new Vector2(col.bounds.center.x, col.bounds.min.y);
        }
        return owner.transform.position;
    }
}
