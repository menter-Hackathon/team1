using System.Collections;
using UnityEngine;

/// <summary>
/// リップル削除（Premiere Pro の能力。2026-11-19。docs/app-feedback-v2-design.md 5-5）。数値はすべて【仮】。1回押し。
/// レーザーツールで切って、リップル削除。切った区間が消え、その先の物が手前に詰まる。
/// ・押した瞬間：正面の区間（体の前の端から 横 4.5×高さ 3.5。地上は足元から、空中は体の下 1マスから）に、白い点線の枠と紫の斜線（0.2秒）。
///   上から小さいカミソリの刃が落ちる
/// ・削除：区間が黒く抜ける（0.1秒）。中の雑魚にダメージ・ボスにゲージ（攻撃データの欄。雑魚 3・ゲージ +12）。中の敵の弾は消える
/// ・詰め：黒い区間がプレイヤーの側へ閉じる（0.15秒）。区間の先 6マス以内の雑魚が、区間の幅だけ手前へすべってくる（壁・穴は越えない）
/// ・動かさない物：ボス・ウイルス本体・落下物・敵の弾・プレイヤー
/// 出したあとは PremiereRippleEffect が自分で進む（能力は待たない）
/// </summary>
public class PremiereRippleDelete : CustomAttack
{
    [Header("区間（5-5）")]
    [Tooltip("区間の大きさ（マス）。体の前の端から横、足元から縦")]
    [SerializeField] Vector2 _size = new Vector2(4.5f, 3.5f);
    [Tooltip("空中で押したとき、区間の下の端を体の下の何マスにするか")]
    [SerializeField, Min(0f)] float _airDrop = 1f;

    [Header("引き寄せ")]
    [Tooltip("区間の先、この距離（マス）以内の雑魚を引き寄せる")]
    [SerializeField, Min(0f)] float _pullRange = 6f;
    [Tooltip("引き寄せる距離（マス。区間の幅と同じ）")]
    [SerializeField, Min(0f)] float _pullDistance = 4.5f;

    [Header("時間（秒）")]
    [SerializeField, Min(0f)] float _markTime = 0.2f;
    [SerializeField, Min(0f)] float _deleteTime = 0.1f;
    [SerializeField, Min(0f)] float _closeTime = 0.15f;

    public Vector2 Size => _size;
    public float PullRange => _pullRange;
    public float PullDistance => _pullDistance;
    public float MarkTime => _markTime;
    public float DeleteTime => _deleteTime;
    public float CloseTime => _closeTime;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Bounds body = BodyOf(owner);
        int f = owner.Facing >= 0 ? 1 : -1;
        float front = f > 0 ? body.max.x : body.min.x;
        bool grounded = true;
        if (owner.TryGetComponent(out KinematicBody2D kb)) grounded = kb.IsGrounded;
        float bottom = grounded ? body.min.y : body.min.y - _airDrop;
        var rect = new Rect(f > 0 ? front : front - _size.x, bottom, _size.x, _size.y);
        PremiereRippleEffect.Spawn(owner, data, this, rect, f);
        owner.NotifyReleased(data);
        yield break;
    }

    static Bounds BodyOf(AttackRunner owner)
    {
        foreach (var col in owner.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds;
        }
        return new Bounds(owner.transform.position + new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0f));
    }
}
