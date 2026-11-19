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

    [Header("攻撃の中身（AttackData から使うときは Setup で上書きされる）")]
    [SerializeField, Min(0)] int _damage = 1;
    [Tooltip("ボスのウイルス解除ゲージを増やす量（プレイヤーの攻撃用）")]
    [SerializeField, Min(0)] int _gaugeGain;
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
    Transform _statSource; // バフ（StatModifiers）を読む持ち主。空ならこの物の親をたどる

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

    /// <summary>
    /// 攻撃の中身（ダメージ・ゲージ増加・ノックバック・のけぞり）を AttackData から設定する。
    /// 判定のオン・オフは変えない
    /// </summary>
    public void Setup(AttackData data)
    {
        _damage = data.Damage;
        _gaugeGain = data.GaugeGain;
        _knockback = data.Knockback;
        _hitStun = data.HitStun;
    }

    /// <summary>陣営・当てる相手・当たり方を設定する（弾・判定を実行時に作るとき用）</summary>
    public void SetOwner(Team team, LayerMask targetLayers, bool hitOncePerActivation = true)
    {
        _team = team;
        _targetLayers = targetLayers;
        _hitOncePerActivation = hitOncePerActivation;
    }

    /// <summary>バフ（攻撃力の倍率）を読む持ち主を決める（AttackRunner が出した弾・判定用）</summary>
    public void SetStatSource(Transform source) => _statSource = source;

    /// <summary>攻撃範囲のコライダーを差し替える</summary>
    public void SetArea(Collider2D area) => _area = area;

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
        // 持ち主のバフ（Steam の実績解除）：ダメージ量とゲージ増加量に掛ける
        StatModifiers stats = (_statSource != null ? _statSource : transform).GetComponentInParent<StatModifiers>();
        float mult = stats != null ? stats.AttackMultiplier : 1f;
        return new DamageInfo
        {
            Amount = StatModifiers.Scale(_damage, mult),
            GaugeGain = StatModifiers.Scale(_gaugeGain, mult),
            SourceTeam = _team,
            Knockback = new Vector2(_knockback.x * dir, _knockback.y),
            HitStun = _hitStun,
            HitPoint = hurtbox.Collider.ClosestPoint(from),
            Source = rb != null ? rb.gameObject : gameObject,
        };
    }
}
