using System.Collections;
using UnityEngine;

/// <summary>
/// 物理で動く物（Unity スキルの Cube・Sphere・Capsule）。AttackRunner が AttackData（種類＝PhysicsObject）から作る。
/// 流れ：（予告の点滅：ボス版だけ）→ 出る絵（Unity_Spawn）の「本体を出すコマ」で本体が現れる → 物理で落ちる・転がる・倒れる → 寿命で消える。
/// ・本体は Rigidbody2D（Dynamic）と Box／Circle／Capsule のコライダー。レイヤーは地形（Ground）なので、プレイヤーも雑魚も上に乗れる
/// ・相手に当たるのは、一定以上の速さで落ちているときだけ（床に置いてあるだけの物は当たらない）。1回落ちるごとに同じ相手には1回
/// ・相手に当たったとき、または一定以上の速さでぶつかって止まったとき、着地の砂煙（Unity_Impact）を出す
/// ・同時に置いておける数を超えたら、AttackRunner が古い物から Despawn する
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PhysicsPrimitive : MonoBehaviour
{
    /// <summary>落ちるのが止まってから、当たる判定を残す秒数（ぶつかった瞬間に速さが 0 になっても当たるように）</summary>
    const float HitGrace = 0.1f;
    /// <summary>砂煙を続けて出さない秒数</summary>
    const float ImpactCooldown = 0.25f;

    AttackRunner _owner;
    AttackData _data;
    Rigidbody2D _rb;
    Collider2D _body;
    Hitbox _hitbox;
    SpriteRenderer _visual;
    float _lastFastFall = float.NegativeInfinity;
    float _lastImpact = float.NegativeInfinity;

    public AttackData Data => _data;
    /// <summary>何番目の形か（0＝Cube、1＝Sphere、2＝Capsule）</summary>
    public int ShapeIndex { get; private set; }
    /// <summary>本体が現れたか</summary>
    public bool IsReleased { get; private set; }
    /// <summary>消えた（消えている途中を含む）</summary>
    public bool IsGone { get; private set; }
    /// <summary>今、相手に当たる状態か（テスト用）</summary>
    public bool IsDangerous => _hitbox != null && _hitbox.IsActive;
    public Rigidbody2D Body => _rb;

    /// <summary>1つ作る。position は本体の中心</summary>
    public static PhysicsPrimitive Spawn(AttackRunner owner, AttackData data, Vector2 position, int shapeIndex)
    {
        PrimitiveEntry entry = data.Primitives[Mathf.Clamp(shapeIndex, 0, data.Primitives.Length - 1)];
        Vector2 size = entry.Size.sqrMagnitude > 0f ? entry.Size : Vector2.one;
        GameObject go = owner.CreateSpawnedObject($"Primitive_{data.name}_{entry.Shape}", position);
        int terrainLayer = FirstLayer(owner.SolidMask);
        int ownerLayer = go.layer;
        if (terrainLayer >= 0) go.layer = terrainLayer; // 足場になる（KinematicBody2D は地形のレイヤーに乗る）

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.simulated = false; // 本体が現れるまで動かない

        var material = new PhysicsMaterial2D("Primitive") { friction = 0.6f, bounciness = 0.05f };
        Collider2D body = AddShape(go, entry.Shape, size, false);
        body.sharedMaterial = material;

        // 当たる判定：本体より少し大きい形（ぶつかって止まる前に、相手の被弾範囲に重なるように）
        var hitGo = new GameObject("Hit");
        hitGo.layer = ownerLayer;
        hitGo.transform.SetParent(go.transform, false);
        Collider2D hitCol = AddShape(hitGo, entry.Shape, size * 1.15f, true);
        Hitbox hitbox = owner.AddHitbox(hitGo, data, hitCol);

        var vgo = new GameObject("Visual");
        vgo.layer = go.layer;
        vgo.transform.SetParent(go.transform, false);
        var sr = vgo.AddComponent<SpriteRenderer>();
        sr.sprite = entry.Sprite;
        sr.color = data.Color;
        sr.sortingOrder = 16;
        sr.flipX = owner.Facing < 0;
        sr.enabled = false;

        var p = go.AddComponent<PhysicsPrimitive>();
        p._owner = owner;
        p._data = data;
        p._rb = rb;
        p._body = body;
        p._hitbox = hitbox;
        p._visual = sr;
        p.ShapeIndex = shapeIndex;
        hitbox.Hit += p.OnHit;
        p.StartCoroutine(p.Run());
        return p;
    }

    static Collider2D AddShape(GameObject go, PrimitiveShape shape, Vector2 size, bool trigger)
    {
        Collider2D col;
        switch (shape)
        {
            case PrimitiveShape.Circle:
                var c = go.AddComponent<CircleCollider2D>();
                c.radius = size.x * 0.5f;
                col = c;
                break;
            case PrimitiveShape.Capsule:
                var cap = go.AddComponent<CapsuleCollider2D>();
                cap.size = size;
                cap.direction = size.y >= size.x ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;
                col = cap;
                break;
            default:
                var b = go.AddComponent<BoxCollider2D>();
                b.size = size;
                col = b;
                break;
        }
        col.isTrigger = trigger;
        return col;
    }

    static int FirstLayer(LayerMask mask)
    {
        int v = mask.value;
        for (int i = 0; i < 32; i++) if ((v & (1 << i)) != 0) return i;
        return -1;
    }

    IEnumerator Run()
    {
        // 予告（ボス版：その位置に Unity_SpawnWarning_Virus を点滅させる）
        Sprite[] warning = _data.WarningFrames;
        if (warning != null && warning.Length > 0 && warning[0] != null && _data.TelegraphTime > 0f)
        {
            var wgo = new GameObject("Warning");
            wgo.layer = gameObject.layer;
            wgo.transform.SetParent(transform, false);
            var wsr = wgo.AddComponent<SpriteRenderer>();
            wsr.sortingOrder = 17;
            wgo.AddComponent<SpriteFrameAnimation>().Setup(wsr, warning, _data.WarningFrameRate, true, false);
            yield return new WaitForSeconds(_data.TelegraphTime);
            Destroy(wgo);
        }

        // 出る絵（Unity_Spawn）。「本体を出すコマ」で本体が現れる
        Sprite[] spawn = _data.SpawnFrames;
        if (spawn != null && spawn.Length > 0 && spawn[0] != null)
        {
            SpriteRenderer fx = Vfx.Play(spawn, transform.position, _data.SpawnFrameRate, 21, null, _visual.flipX);
            if (fx != null) _owner.Register(fx.gameObject);
            float wait = _data.SpawnReleaseDelay;
            if (wait > 0f) yield return new WaitForSeconds(wait);
        }

        IsReleased = true;
        _visual.enabled = true;
        _rb.simulated = true;
        // Capsule は少し傾けて倒れやすくする（形ごとの個性。Sphere は円なので放っておいても転がる）
        if (_body is CapsuleCollider2D) _rb.AddTorque(Random.Range(-1f, 1f) * 2f, ForceMode2D.Impulse);

        yield return new WaitForSeconds(_data.ObjectLifetime);
        Despawn();
    }

    void FixedUpdate()
    {
        if (!IsReleased || IsGone) return;
        float now = Time.time;
        if (-_rb.velocity.y >= _data.DamageFallSpeed) _lastFastFall = now;
        bool dangerous = now - _lastFastFall <= HitGrace;
        if (dangerous && !_hitbox.IsActive) _hitbox.Activate(); // 落ち始めるたびに「当てた相手」を数え直す
        else if (!dangerous && _hitbox.IsActive) _hitbox.Deactivate();
    }

    void OnHit(IDamageable target, DamageInfo info) => PlayImpact();

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsReleased || IsGone) return;
        if (collision.relativeVelocity.magnitude < _data.ImpactSpeed) return;
        // 下に何かがあってぶつかったとき（着地）だけ。横の壁にぶつかったときは出さない
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                PlayImpact();
                return;
            }
        }
    }

    void PlayImpact()
    {
        if (Time.time - _lastImpact < ImpactCooldown) return;
        _lastImpact = Time.time;
        Bounds b = _body.bounds;
        SpriteRenderer fx = Vfx.Play(_data.ImpactFrames, new Vector2(b.center.x, b.min.y), _data.ImpactFrameRate, 22);
        if (fx != null && _owner != null) _owner.Register(fx.gameObject);
    }

    /// <summary>消す（寿命・上限を超えたとき）。小さい煙を出してすぐ消える</summary>
    public void Despawn()
    {
        if (IsGone) return;
        IsGone = true;
        if (_hitbox != null) _hitbox.Deactivate();
        if (IsReleased) Vfx.SmokeSmall(_body.bounds.center);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        IsGone = true;
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }
}
