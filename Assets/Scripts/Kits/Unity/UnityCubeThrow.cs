using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity の能力「キューブ投げ」（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// ・3D の立方体（1マス）を山なりに投げる。当たるか床で1回はずんで、もう1回当たれる（ゲージは攻撃データの値 ×最大2回）
/// ・止まったら 3秒残る（乗れる。敵の弾を止める）。同時に2個まで（3個目を投げると、いちばん古い物が消える）
/// ・投げた瞬間に、左上に「+ Cube」の小さい行（Hierarchy の形）が 0.6秒
/// ・ステージの決まりに頼らない（どのステージ・Terminal でも動く）
/// </summary>
[CreateAssetMenu(fileName = "UnityCubeThrow_", menuName = "Team1/Kits/Unity/Cube Throw")]
public class UnityCubeThrow : CustomAttack
{
    [SerializeField] Vector2 _velocity = new Vector2(7.5f, 8.5f);
    [SerializeField, Min(1f)] float _gravity = 26f;
    [SerializeField, Min(0.3f)] float _size = 1f;
    [Tooltip("止まってから残る秒数")]
    [SerializeField, Min(0f)] float _remain = 3f;
    [Tooltip("同時に置ける数")]
    [SerializeField, Min(1)] int _max = 2;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        int face = owner.Facing;
        Vector2 at = owner.OriginPoint + new Vector2(face * 0.55f, 0.6f);
        UnityThrownCube.Spawn(owner, data, at, new Vector2(face * _velocity.x, _velocity.y), _gravity, _size, _remain, _max);
        owner.NotifyReleased(data);
        UnityHud.Ping("+ Cube", 0.6f);
        yield break;
    }
}

/// <summary>投げた立方体（自分で進む）</summary>
public class UnityThrownCube : MonoBehaviour
{
    static readonly List<UnityThrownCube> Live = new List<UnityThrownCube>();

    Rigidbody2D _rb;
    Hitbox _hitbox;
    Kit3DVisual _visual;
    Vector2 _pos, _v;
    float _g, _half, _remain;
    int _hitsLeft = 2;
    int _bounces;
    bool _settled;
    float _reactivateAt = -1f;
    float _expireAt = float.PositiveInfinity;
    float _blinkStart = -1f;
    readonly List<GameObject> _rest = new List<GameObject>();

    public static UnityThrownCube Spawn(AttackRunner owner, AttackData data, Vector2 at, Vector2 velocity, float gravity, float size, float remain, int max)
    {
        Live.RemoveAll(c => c == null);
        while (Live.Count >= Mathf.Max(1, max))
        {
            UnityThrownCube old = Live[0];
            Live.RemoveAt(0);
            if (old != null) Destroy(old.gameObject);
        }
        GameObject go = owner.CreateSpawnedObject("UnityThrownCube", at);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one * size;
        var c = go.AddComponent<UnityThrownCube>();
        c._rb = rb;
        c._pos = at;
        c._v = velocity;
        c._g = gravity;
        c._half = size * 0.5f;
        c._remain = remain;
        c._hitbox = owner.AddHitbox(go, data, col);
        c._hitbox.Hit += c.OnHit;
        c._hitbox.Activate();
        c._visual = Kit3D.AddVisual(go.transform, Kit3DShape.Cube, Vector3.one * size, UnityArt.Object, go);
        c._visual.Flash(0.06f);
        Live.Add(c);
        return c;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
        Live.Remove(this);
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        if (_settled) return;
        _hitsLeft--;
        _hitbox.Deactivate();
        // 相手からはね返る
        _v.x = -_v.x * 0.35f;
        _v.y = Mathf.Max(_v.y, 5f);
        if (_hitsLeft > 0) _reactivateAt = Time.time + 0.2f;
    }

    void FixedUpdate()
    {
        if (_settled) return;
        float dt = Time.fixedDeltaTime;
        if (_reactivateAt > 0f && Time.time >= _reactivateAt)
        {
            _reactivateAt = -1f;
            if (_hitsLeft > 0) _hitbox.Activate();
        }
        _v.y -= _g * dt;
        Vector2 d = _v * dt;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        var hits = new RaycastHit2D[4];
        // 横：壁で弱くはね返る
        if (Mathf.Abs(d.x) > 0f)
        {
            filter.SetLayerMask(Kit3D.SolidMask);
            int n = Physics2D.BoxCast(_pos, Vector2.one * (_half * 1.8f), 0f, new Vector2(Mathf.Sign(d.x), 0f), filter, hits, Mathf.Abs(d.x) + 0.02f);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance <= 0f || Mathf.Abs(hits[i].normal.x) < 0.5f) continue;
                _v.x = -_v.x * 0.4f;
                d.x = 0f;
                break;
            }
        }
        // 下：床・足場で1回はずみ、2回目で止まる
        if (d.y < 0f)
        {
            filter.SetLayerMask(Kit3D.GroundMask);
            int n = Physics2D.BoxCast(_pos, new Vector2(_half * 1.8f, 0.05f), 0f, Vector2.down, filter, hits, -d.y + _half);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance <= 0f || hits[i].normal.y < 0.5f) continue;
                float floorY = hits[i].point.y;
                _pos.y = floorY + _half;
                if (_bounces == 0)
                {
                    _bounces++;
                    _v.y = 6f;
                    _v.x *= 0.6f;
                    if (_hitsLeft > 0 && !_hitbox.IsActive) _hitbox.Activate(); // はずんだら、もう1回当たれる
                    _rb.MovePosition(_pos);
                    return;
                }
                Settle();
                return;
            }
        }
        _pos += d;
        _rb.MovePosition(_pos);
        _visual.Spin(-_v.x * dt / Mathf.Max(0.1f, _half) * Mathf.Rad2Deg);
        if (_pos.y < BattleArena.GetBounds().yMin - 15f) Destroy(gameObject);
    }

    // 止まる：乗れる（すり抜け床）・敵の弾を止める（BulletBlocker）箱になる
    void Settle()
    {
        _settled = true;
        _hitbox.Deactivate();
        _rb.MovePosition(_pos);
        _visual.SetExtraRotation(Quaternion.identity);
        _visual.SetShade(0.85f);
        _rest.Add(Collider("Top", "OneWayPlatform"));
        _rest.Add(Collider("Block", "BulletBlocker"));
        _expireAt = Time.time + _remain;
    }

    GameObject Collider(string name, string layer)
    {
        var go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(layer);
        go.transform.SetParent(transform, false);
        var b = go.AddComponent<BoxCollider2D>();
        b.size = Vector2.one * (_half * 2f - 0.04f);
        return go;
    }

    void Update()
    {
        if (!_settled) return;
        float now = Time.time;
        if (now < _expireAt) return;
        if (_blinkStart < 0f)
        {
            _blinkStart = now;
            foreach (var g in _rest) if (g != null) Destroy(g);
        }
        float t = now - _blinkStart;
        if (t < 0.5f) { _visual.Show(Mathf.FloorToInt(t / 0.08f) % 2 == 0); return; }
        _visual.Show(true);
        float k = 1f - Mathf.Clamp01((t - 0.5f) / 0.2f);
        _visual.SetScale(k);
        if (k <= 0f) Destroy(gameObject);
    }
}
