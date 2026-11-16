using UnityEngine;

/// <summary>
/// 「ここに当たったらダメージを受ける」範囲の目印。
/// この GameObject に付いたコライダー（トリガー）が被弾範囲になり、親をたどって見つけた IDamageable がダメージを受ける。
/// ・Hurtbox が付いていないコライダー（移動用の当たり判定・攻撃判定など）には攻撃が当たらない
/// ・形は見た目に合わせる（プレイヤーなら体のスプライトと同じ大きさ）
/// ・レイヤーは持ち主と同じにする（プレイヤーなら Player、敵なら Enemy）
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hurtbox : MonoBehaviour
{
    public Collider2D Collider { get; private set; }
    public IDamageable Owner { get; private set; }

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        Collider = GetComponent<Collider2D>();
        Collider.isTrigger = true; // 移動や地形の判定に影響しないようにトリガーにする
        Owner = GetComponentInParent<IDamageable>();
        if (Owner == null)
        {
            Debug.LogWarning($"{name}: 親に Health（IDamageable）が見つからないので、この Hurtbox は機能しません", this);
        }
    }
}
