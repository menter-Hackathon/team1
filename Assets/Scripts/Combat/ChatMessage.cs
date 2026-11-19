using UnityEngine;

/// <summary>
/// チャットのメッセージ1つ（ChatVolley が作る）。ためている間は持ち主に付いてきて、合図で横へ飛んでいく。
/// ・出たときは、ぽんと大きくなって出てくる
/// ・飛んでいくとき：出た場所に残像が一瞬残り、飛び始めにも短い残像が続く
/// ・飛ぶ前（ためている間）も判定がある（2026-11-19）
/// ・相手に当たったら（飛ぶ前でも）、そのアプリのエフェクトを出して消える。飛んでいる間は地形に当たっても消える。寿命が来ても消える
/// </summary>
public class ChatMessage : MonoBehaviour
{
    const float PopTime = 0.12f;

    AttackRunner _owner;
    AttackData _data;
    SpriteRenderer _sr;
    BoxCollider2D _col;
    Hitbox _hitbox;
    Vector2 _velocity;
    float _bornAt;
    float _launchedAt = -1f;
    float _dieAt;
    float _nextGhost;
    bool _dead;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

    /// <summary>メッセージの高さ（マス）</summary>
    public float Height => _sr != null && _sr.sprite != null ? _sr.sprite.bounds.size.y : 0.5f;
    public bool IsFlying => _launchedAt >= 0f;

    public static ChatMessage Create(AttackRunner owner, AttackData data, Sprite sprite, int index, Vector2 position)
    {
        GameObject go = owner.CreateSpawnedObject($"ChatMessage_{data.name}_{index}", position);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var m = go.AddComponent<ChatMessage>();
        m._owner = owner;
        m._data = data;
        m._bornAt = Time.time;

        var art = new GameObject("Art");
        art.layer = go.layer;
        art.transform.SetParent(go.transform, false);
        m._sr = art.AddComponent<SpriteRenderer>();
        m._sr.sprite = sprite != null ? sprite : PlaceholderSprites.Square;
        if (sprite == null)
        {
            art.transform.localScale = new Vector3(2.2f, 0.55f, 1f);
            m._sr.color = data.Color;
        }
        // 新しいメッセージほど手前（重ねてつなげるとき、上のメッセージの下の線を隠す）
        m._sr.sortingOrder = 21 + index;

        m._col = go.AddComponent<BoxCollider2D>();
        m._col.isTrigger = true;
        Vector2 size = m._sr.bounds.size;
        m._col.size = new Vector2(Mathf.Max(0.2f, size.x - 0.1f), Mathf.Max(0.2f, size.y - 0.05f));
        m._hitbox = owner.AddHitbox(go, data, m._col);
        m._hitbox.Hit += m.OnHit;
        m._hitbox.Activate(); // 飛ぶ前から、触れた敵に当たる
        go.transform.localScale = Vector3.one * 0.4f;
        return m;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    /// <summary>ためている間：目標の位置へ寄せる（k＝寄せる割合。1ならその場へ）。向きに合わせて左右を反転</summary>
    public void Follow(Vector2 target, int facing, float k)
    {
        if (IsFlying || _dead) return;
        transform.position = Vector2.Lerp(transform.position, target, Mathf.Clamp01(k));
        _sr.flipX = facing < 0;
    }

    /// <summary>指定した向きへ飛ばす（絵は向きに合わせて左右を反転。回転はしない）</summary>
    public void Launch(Vector2 direction)
    {
        if (IsFlying || _dead) return;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        _launchedAt = Time.time;
        transform.localScale = Vector3.one;
        _sr.flipX = direction.x < 0f;
        _velocity = direction.normalized * _data.Speed;
        _dieAt = Time.time + _data.Lifetime;
        _hitbox.Activate();
        Ghost(0.5f, 0.22f); // 出た場所に、残像が一瞬残る
        _nextGhost = Time.time + 0.03f;
    }

    void Update()
    {
        if (_dead) return;
        if (!IsFlying)
        {
            // ぽんと大きくなって出てくる（少し大きくなってから戻る）
            float t = Mathf.Clamp01((Time.time - _bornAt) / PopTime);
            float s = t < 1f ? Mathf.Lerp(0.4f, 1.12f, t) : 1f;
            if (t >= 1f && Time.time - _bornAt < PopTime * 1.6f) s = Mathf.Lerp(1.12f, 1f, (Time.time - _bornAt - PopTime) / (PopTime * 0.6f));
            transform.localScale = Vector3.one * s;
            return;
        }

        if (Time.time >= _dieAt)
        {
            Die(false);
            return;
        }
        // 飛び始めの少しの間だけ、短い残像を続ける
        if (Time.time - _launchedAt < 0.16f && Time.time >= _nextGhost)
        {
            Ghost(0.35f, 0.12f);
            _nextGhost = Time.time + 0.03f;
        }

        Vector2 pos = transform.position;
        Vector2 delta = _velocity * Time.deltaTime;
        float dist = delta.magnitude;
        if (dist > 0f && _data.DestroyOnTerrain)
        {
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(_owner != null ? _owner.SolidMask : (LayerMask)0);
            int n = Physics2D.BoxCast(pos, _col.size * 0.9f, 0f, delta / dist, filter, _hits, dist);
            if (n > 0)
            {
                transform.position = _hits[0].centroid;
                Die(true);
                return;
            }
        }
        transform.position = pos + delta;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        if (_dead) return;
        Vfx.Play(_data.ChatHitFrames, info.HitPoint, 22f, 45);
        if (_data.DestroyOnHit) Die(false); // 飛ぶ前でも、当たったら消える（残りのメッセージは詰めて並び直す）
    }

    void Die(bool showFx)
    {
        if (_dead) return;
        _dead = true;
        if (_hitbox != null) _hitbox.Deactivate();
        if (showFx) Vfx.Play(_data.ChatHitFrames, transform.position, 22f, 45);
        Destroy(gameObject);
    }

    /// <summary>今の絵の残像を、その場に残す（薄くなりながら消える）</summary>
    void Ghost(float alpha, float life)
    {
        var go = new GameObject("ChatGhost");
        go.transform.position = transform.position;
        go.transform.localScale = transform.localScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sr.sprite;
        sr.flipX = _sr.flipX;
        sr.sortingOrder = _sr.sortingOrder - 1;
        Color c = _sr.color;
        c.a *= alpha;
        sr.color = c;
        go.AddComponent<FadeAndDestroy>().Setup(life);
    }
}
