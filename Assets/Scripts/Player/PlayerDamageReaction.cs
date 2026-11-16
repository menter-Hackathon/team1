using UnityEngine;

/// <summary>
/// プレイヤーが被弾したときの動き。
/// ・被弾：吹き飛んで、少しの間操作できなくなる（点滅は DamageBlink）
/// ・ライフ0：操作できなくする。ゲームオーバー画面やリトライは M5 で Health.Died を受け取って作る
/// </summary>
[RequireComponent(typeof(Health), typeof(PlayerController))]
public class PlayerDamageReaction : MonoBehaviour
{
    Health _health;
    PlayerController _controller;

    void Awake()
    {
        _health = GetComponent<Health>();
        _controller = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
    }

    void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    void OnDamaged(DamageInfo info)
    {
        _controller.ApplyKnockback(info.Knockback, info.HitStun);
    }

    void OnDied()
    {
        _controller.CanControl = false;
    }
}
