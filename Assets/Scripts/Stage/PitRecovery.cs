using UnityEngine;

/// <summary>
/// 穴に落ちたとき（2026-11-17：横スクロールのステージ）。プレイヤーに付ける。
/// ・プレイヤーがこの高さより下に落ちたら、ライフを1減らして、直前に立っていた床の上に戻す
/// ・戻す場所は「真下が床（Ground）の所」だけを覚える（消える足場の上に戻して、また落ちないように）
/// ・無敵の間に落ちたときは、ライフは減らずに戻るだけ
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(Health))]
public class PitRecovery : MonoBehaviour
{
    [Tooltip("この高さより下に落ちたら、穴に落ちたとする")]
    [SerializeField] float _killY = -3f;
    [Tooltip("戻してよい床（Ground）のレイヤー")]
    [SerializeField] LayerMask _safeGround;

    PlayerController _controller;
    Health _health;
    KinematicBody2D _body;
    Vector2 _lastSafe;
    bool _hasSafe;

    /// <summary>穴に落ちたとき（SE・エフェクト用）</summary>
    public event System.Action Fell;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _health = GetComponent<Health>();
        _body = GetComponent<KinematicBody2D>();
    }

    void FixedUpdate()
    {
        if (_health.IsDead) return;
        Vector2 pos = transform.position;
        if (_controller.IsGrounded && OnSafeGround(pos))
        {
            _lastSafe = pos;
            _hasSafe = true;
        }
        if (pos.y > _killY || !_hasSafe) return;

        var info = new DamageInfo { Amount = 1, SourceTeam = Team.Enemy, Knockback = Vector2.zero, HitStun = 0f, HitPoint = pos };
        _health.TryTakeDamage(in info);
        if (_health.IsDead) return; // ライフ0：やり直し（StageDirector）
        Vector2 back = _lastSafe + new Vector2(0f, 0.1f);
        if (_body != null) _body.Warp(back);
        else transform.position = back;
        _controller.SetVelocity(Vector2.zero);
        Vfx.SmokeSmall(back + new Vector2(0f, 0.5f));
        Fell?.Invoke();
        GameAudio.Event("player_fall");
    }

    bool OnSafeGround(Vector2 feet)
    {
        // 足元の左右の端の真下が、どちらも床（すり抜け床・消える足場ではない）か
        return Physics2D.Raycast(feet + new Vector2(-0.3f, 0.1f), Vector2.down, 0.35f, _safeGround).collider != null
            && Physics2D.Raycast(feet + new Vector2(0.3f, 0.1f), Vector2.down, 0.35f, _safeGround).collider != null;
    }
}
