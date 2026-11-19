using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>こわれた砂の小さいかけら（すぐ消える）</summary>
public class MinecraftCrumbs : MonoBehaviour
{
    readonly List<(Transform t, Vector2 v)> _bits = new List<(Transform, Vector2)>();
    float _born;

    public static void Spawn(Vector2 at, Color color)
    {
        var go = new GameObject("MinecraftCrumbs");
        go.transform.position = at;
        var c = go.AddComponent<MinecraftCrumbs>();
        c._born = Time.time;
        for (int i = 0; i < 8; i++)
        {
            var b = new GameObject("Bit");
            b.transform.SetParent(go.transform, false);
            b.transform.localPosition = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
            AttackRunner.AddVisual(b.transform, PlaceholderSprites.Square, color, Vector2.one * Random.Range(0.12f, 0.22f), 30, "Sq");
            c._bits.Add((b.transform, new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 5f))));
        }
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t > 0.5f) { Destroy(gameObject); return; }
        for (int i = 0; i < _bits.Count; i++)
        {
            var (tr, v) = _bits[i];
            if (tr == null) continue;
            v.y -= 18f * Time.deltaTime;
            _bits[i] = (tr, v);
            tr.localPosition += (Vector3)(v * Time.deltaTime);
        }
    }
}

/// <summary>近づくと爆発する緑のブロックの生き物（自分で進む）</summary>
public class MinecraftCreeper : CustomEffect
{
    const float Size = 0.9f;

    enum State { Smoke, Walk, Fuse, Done }

    Hitbox _blast;
    Kit3DVisual _visual;
    KitProp _prop;
    GameObject _ring;
    State _state;
    float _t0, _smoke, _speed, _near, _fuse, _radius, _maxWalk;
    readonly List<Transform> _puffs = new List<Transform>();

    internal void Setup(AttackRunner owner, AttackData data, float smoke, float speed, float near, float fuse, float radius, float maxWalk)
    {
        _smoke = smoke;
        _speed = speed;
        _near = near;
        _fuse = fuse;
        _radius = radius;
        _maxWalk = maxWalk;
        _t0 = Time.time;
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;
        col.offset = new Vector2(0f, Size);
        _blast = owner.AddHitbox(gameObject, data, col);
        _visual = MinecraftArt.CreeperVisual(transform, Size, gameObject);
        _visual.Busy = true;
        _visual.SetScale(0.01f);
        // 煙（灰色の四角がふくらむ）
        for (int i = 0; i < 6; i++)
        {
            var p = new GameObject("Puff");
            p.layer = gameObject.layer;
            p.transform.SetParent(transform, false);
            p.transform.localPosition = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 1.6f));
            AttackRunner.AddVisual(p.transform, PlaceholderSprites.Square, new Color(0.85f, 0.85f, 0.85f, 0.8f), Vector2.one * 0.4f, 21, "Sq");
            _puffs.Add(p.transform);
        }
        _state = State.Smoke;
    }

    protected override void Restore()
    {
        if (_prop != null) Destroy(_prop.gameObject);
    }

    void Update()
    {
        float t = Time.time - _t0;
        switch (_state)
        {
            case State.Smoke:
                float k = Mathf.Clamp01(t / _smoke);
                _visual.SetScale(k);
                foreach (var p in _puffs) if (p != null) p.localScale = Vector3.one * (1f + k * 1.5f);
                if (k >= 1f)
                {
                    foreach (var p in _puffs) if (p != null) Destroy(p.gameObject);
                    _puffs.Clear();
                    _state = State.Walk;
                    _t0 = Time.time;
                    _prop = KitProp.Spawn(BodyCenter, null, new Vector2(Size, Size * 2f), OnKilled, 1);
                    _prop.DestroyOnBreak = false;
                    if (_prop.Visual != null) _prop.Visual.enabled = false;
                }
                break;
            case State.Walk:
                Walk(t);
                break;
            case State.Fuse:
                _visual.SetShade(Mathf.FloorToInt(t / 0.1f) % 2 == 0 ? 1f : 2.4f);
                if (_ring != null) _ring.SetActive(t < _fuse - 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0);
                if (t >= _fuse) StartCoroutine(Explode());
                break;
        }
    }

    Vector2 BodyCenter => (Vector2)transform.position + new Vector2(0f, Size);

    void Walk(float t)
    {
        Transform pl = KitPlayer.Transform;
        if (pl == null || t >= _maxWalk) { StartFuse(); return; }
        float dx = pl.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= _near && Mathf.Abs(pl.position.y - transform.position.y) < 3f) { StartFuse(); return; }
        int dir = dx >= 0f ? 1 : -1;
        float step = _speed * Time.deltaTime;
        // 壁・置いた箱にぶつかったら、その場で待つ（時間の上限で点滅を始める）
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(Kit3D.SolidMask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.BoxCast(BodyCenter, new Vector2(Size * 0.9f, Size * 1.8f), 0f, new Vector2(dir, 0f), filter, hits, step + 0.02f);
        for (int i = 0; i < n; i++) if (hits[i].distance > 0f) return;
        transform.position += new Vector3(dir * step, 0f, 0f);
        if (_prop != null) _prop.transform.position = BodyCenter;
        // 歩くゆれ
        _visual.SetExtraRotation(Quaternion.AngleAxis(Mathf.Sin(Time.time * 14f) * 5f, Vector3.forward));
    }

    void StartFuse()
    {
        _state = State.Fuse;
        _t0 = Time.time;
        if (_prop != null) { Destroy(_prop.gameObject); _prop = null; } // 点滅したら、もう倒せない
        _visual.SetExtraRotation(Quaternion.identity);
        // 爆発する範囲の赤い円（場所は固定）
        _ring = new GameObject("Range");
        _ring.layer = gameObject.layer;
        _ring.transform.SetParent(transform, false);
        _ring.transform.localPosition = new Vector2(0f, Size);
        AttackRunner.AddVisual(_ring.transform, PlaceholderSprites.Circle, new Color(1f, 0.27f, 0.23f, 0.18f), Vector2.one * _radius * 2f, 16, "Fill");
        RuntimeSprites.Show(_ring.transform, MinecraftArt.Ring(Mathf.RoundToInt(_radius * 36f), 2), Vector2.zero, 17, 1f, "Edge").color = KitDress.Red;
    }

    void OnKilled(KitProp p)
    {
        if (_state != State.Walk) return;
        _state = State.Done;
        MinecraftCrumbs.Spawn(BodyCenter, MinecraftArt.Green);
        Destroy(gameObject);
    }

    IEnumerator Explode()
    {
        _state = State.Done;
        if (_ring != null) Destroy(_ring);
        MinecraftArt.Explosion(BodyCenter, _radius);
        GameAudio.Se("se_minecraft_boom", 0.85f, 1.15f);
        _visual.Show(false);
        _blast.Activate();
        yield return new WaitForSeconds(0.15f);
        _blast.Deactivate();
        Destroy(gameObject);
    }
}
