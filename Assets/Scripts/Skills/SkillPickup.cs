using System;
using UnityEngine;

/// <summary>
/// プレイヤーが触れるとスキルを手に入れるアイテム。今はボスがいないので、デバッグ用にテストシーンに置いている。
/// （ボスの撃破報酬としてこのアイテムを落とす形にしてもよい）
/// ・空き枠があれば、そこに入って消える
/// ・満杯なら「入れ替え待ち」になって消える。入れ替える枠を選ぶか、拾わないかは PlayerSkills の操作で決める
/// ・同じスキルを持っている／別の入れ替え待ちの最中なら拾えず、その場に残る（一度離れて触れ直すと、もう一度試す）
/// 当たり判定は Hitbox と同じく、物理のトリガー通知ではなく毎物理フレーム自分で重なりを調べる。
/// </summary>
public class SkillPickup : MonoBehaviour
{
    [SerializeField] SkillDefinition _skill;
    [Tooltip("拾える範囲のコライダー（トリガー）。空ならこの GameObject のコライダーを使う")]
    [SerializeField] Collider2D _area;
    [Tooltip("拾う相手のレイヤー（Player）")]
    [SerializeField] LayerMask _targetLayers;

    readonly Collider2D[] _results = new Collider2D[4];
    ContactFilter2D _filter;
    PlayerSkills _touching; // 前の物理フレームで触れていたプレイヤー

    public SkillDefinition Skill => _skill;

    /// <summary>拾われたとき（SE・エフェクト用）。結果は Added か PendingReplace</summary>
    public event Action<PlayerSkills, SkillAcquireResult> PickedUp;

    void Reset()
    {
        _area = GetComponent<Collider2D>();
        if (_area != null) _area.isTrigger = true;
        _targetLayers = LayerMask.GetMask("Player");
    }

    void Awake()
    {
        if (_area == null) _area = GetComponent<Collider2D>();
    }

    void FixedUpdate()
    {
        if (_skill == null || _area == null || !_area.enabled) return;

        _filter.useTriggers = false; // プレイヤーの体（移動用の当たり判定）だけを見る。Hurtbox などのトリガーは数えない
        _filter.SetLayerMask(_targetLayers);
        int count = _area.OverlapCollider(_filter, _results);

        PlayerSkills found = null;
        for (int i = 0; i < count && found == null; i++)
        {
            Rigidbody2D rb = _results[i].attachedRigidbody;
            found = rb != null ? rb.GetComponent<PlayerSkills>() : _results[i].GetComponentInParent<PlayerSkills>();
        }

        // 触れた瞬間だけ試す（拾えなかったときに、毎フレーム試し続けないように）
        bool entered = found != null && found != _touching;
        _touching = found;
        if (!entered) return;

        SkillAcquireResult result = found.TryAcquire(_skill);
        if (result == SkillAcquireResult.Added || result == SkillAcquireResult.PendingReplace)
        {
            PickedUp?.Invoke(found, result);
            gameObject.SetActive(false);
        }
    }
}
