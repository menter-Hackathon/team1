using UnityEngine;

/// <summary>
/// 【仮】コピースタンプの分身（PhotoshopCloneAttack が出す）。床の上を、プレイヤーへ歩く。
/// 赤い輪かくの予告（点滅）→ 半透明の水色の分身（触れると当たる）→ 時間で消える。近接・能力が1回当たると消える（KitProp）
/// </summary>
public class PhotoshopCloneEffect : CustomEffect
{
    AttackRunner _owner;
    float _walkAt, _dieAt, _speed;
    SpriteRenderer _body;
    Hitbox _hit;
    KitProp _prop;
    float _bottom;
    int _frame;
    float _nextFrame;

    internal void Setup(AttackRunner owner, AttackData data, float telegraph, float speed, float life)
    {
        _owner = owner;
        _speed = speed;
        _walkAt = Time.time + telegraph;
        _dieAt = _walkAt + life;
        Sprite s = PhotoshopArt.PlayerSprite(0);
        _bottom = s.bounds.min.y;
        _body = RuntimeSprites.Show(transform, s, new Vector2(0f, -_bottom), 12, 1f, "Clone");
        _body.color = KitDress.Red;

        // 触れると当たる（歩き出してから）
        var hitGo = new GameObject("Hit");
        hitGo.layer = gameObject.layer;
        hitGo.transform.SetParent(transform, false);
        hitGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var rb = hitGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hitGo.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.6f, 0.9f);
        _hit = owner.AddHitbox(hitGo, data, box, false);
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _dieAt) { Pop(); return; }
        Transform target = _owner != null ? _owner.Target : null;
        if (now < _walkAt)
        {
            // 予告：赤い形が点滅
            if (_body != null) _body.enabled = (_walkAt - now) > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0;
            if (_body != null && target != null) _body.flipX = target.position.x < transform.position.x;
            return;
        }
        if (_prop == null)
        {
            _body.enabled = true;
            _body.color = new Color(PhotoshopArt.Blue.r, PhotoshopArt.Blue.g, PhotoshopArt.Blue.b, 0.6f);
            _hit.Activate();
            _prop = KitProp.Spawn(transform.position + new Vector3(0f, 0.5f, 0f), null, new Vector2(0.9f, 1.1f), p => Pop(), 1);
            if (_prop.Visual != null) _prop.Visual.enabled = false;
            _prop.DestroyOnBreak = false;
            _prop.transform.SetParent(transform, true);
        }
        if (target != null)
        {
            float dx = target.position.x - transform.position.x;
            int dir = dx >= 0f ? 1 : -1;
            if (Mathf.Abs(dx) > 0.05f) transform.position += new Vector3(dir * _speed * Time.deltaTime, 0f, 0f);
            _body.flipX = dir < 0;
        }
        if (now >= _nextFrame)
        {
            _nextFrame = now + 0.12f;
            _frame++;
            _body.sprite = PhotoshopArt.PlayerSprite(_frame);
        }
        float left = _dieAt - now;
        if (left < 0.4f) { Color c = _body.color; c.a = 0.6f * left / 0.4f; _body.color = c; }
    }

    void Pop()
    {
        FxKit.Ring((Vector2)transform.position + new Vector2(0f, 0.5f), 1.2f, 0.2f, PhotoshopArt.Blue);
        Destroy(gameObject);
    }
}
