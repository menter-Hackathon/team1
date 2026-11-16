using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻撃判定。有効な間、範囲に重なった相手の Hurtbox にダメージを与える。
/// 近接攻撃・弾・設置物・敵の体当たり（接触ダメージ）・トゲなど、すべての攻撃で共通して使う。
///
/// 当たり判定は物理のトリガー通知ではなく、毎物理フレーム自分で範囲を調べる方式。
/// （Kinematic どうしでも確実に判定でき、KinematicBody2D と同じ考え方）
/// </summary>
public class Hitbox : MonoBehaviour
{
    [SerializeField] Team _team = Team.Enemy;
    [Tooltip("攻撃範囲のコライダー。空ならこの GameObject のコライダーを使う")]
    [SerializeField] Collider2D _area;
    [Tooltip("当てる相手のレイヤー（プレイヤーの攻撃なら Enemy、敵の攻撃なら Player）")]
    [SerializeField] LayerMask _targetLayers;

    [Header("攻撃の中身（M3 で攻撃データから設定する予定）")]
    [SerializeField, Min(0)] int _damage = 1;
    [Tooltip("吹き飛ばす速度。x は相手が攻撃から離れる向きに自動で反転する")]
    [SerializeField] Vector2 _knockback = new Vector2(6f, 5f);
    [Tooltip("相手がのけぞって操作できない秒数")]
    [SerializeField, Min(0f)] float _hitStun = 0.25f;

    [Header("当たり方")]
    [Tooltip("オン：1回の攻撃（Activate してから）で同じ相手には1回だけ当たる。\nオフ：重なっている間ずっと当たり続ける（接触ダメージ・トゲ用。連続ヒットは相手の無敵時間で防ぐ）")]
    [SerializeField] bool _hitOncePerActivation = true;
    [Tooltip("ゲーム開始時から攻撃判定を出しておくか（接触ダメージ・トゲはオン、振る攻撃はオフ）")]
    [SerializeField] bool _activeOnStart = true;

    readonly Collider2D[] _results = new Collider2D[16];
    readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
    readonly HashSet<IDamageable> _hitThisStep = new HashSet<IDamageable>();
    ContactFilter2D _filter;
    Health _ownerHealth; // 持ち主（敵の体当たりなど）。倒れたら攻撃しない

    public bool IsActive { get; private set; }
    public Team Team => _team;

    /// <summary>相手にダメージが入ったとき（弾を消す・ヒットSEなどに使う）</summary>
    public event Action<IDamageable, DamageInfo> Hit;

    void Reset()
    {
        _area = GetComponent<Collider2D>();
        if (_area != null) _area.isTrigger = true;
    }

    void Awake()
    {
        if (_area == null) _area = GetComponent<Collider2D>();
        _ownerHealth = GetComponentInParent<Health>();
        IsActive = _activeOnStart;
    }

    /// <summary>攻撃判定を出す。「当てた相手」の記録もリセットされる</summary>
    public void Activate()
    {
        _alreadyHit.Clear();
        IsActive = true;
    }

    /// <summary>攻撃判定を消す</summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    void FixedUpdate()
    {
        if (!IsActive || _area == null || !_area.enabled) return;
        if (_ownerHealth != null && _ownerHealth.IsDead) return; // 倒された敵の体に触れてもダメージを受けない

        _filter.useTriggers = true; // Hurtbox はトリガーなので含める
        _filter.SetLayerMask(_targetLayers);
        int count = _area.OverlapCollider(_filter, _results);

        _hitThisStep.Clear();
        for (int i = 0; i < count; i++)
        {
            // Hurtbox が付いていないコライダー（相手の移動用の判定や攻撃判定）は無視する
            if (!_results[i].TryGetComponent(out Hurtbox hurtbox)) continue;

            IDamageable target = hurtbox.Owner;
            if (target == null || target.Team == _team) continue;
            if (!_hitThisStep.Add(target)) continue; // 同じ相手の別の Hurtbox に重なっていても1回だけ
            if (_hitOncePerActivation && _alreadyHit.Contains(target)) continue;

            DamageInfo info = BuildInfo(hurtbox);
            if (target.TryTakeDamage(info))
            {
                if (_hitOncePerActivation) _alreadyHit.Add(target);
                Hit?.Invoke(target, info);
            }
        }
    }

    DamageInfo BuildInfo(Hurtbox hurtbox)
    {
        Vector2 from = _area.bounds.center;
        Vector2 to = hurtbox.Collider.bounds.center;
        float dir = to.x >= from.x ? 1f : -1f; // 攻撃から離れる向きに飛ばす

        Rigidbody2D rb = _area.attachedRigidbody;
        return new DamageInfo
        {
            Amount = _damage,
            SourceTeam = _team,
            Knockback = new Vector2(_knockback.x * dir, _knockback.y),
            HitStun = _hitStun,
            HitPoint = hurtbox.Collider.ClosestPoint(from),
            Source = rb != null ? rb.gameObject : gameObject,
        };
    }
}
