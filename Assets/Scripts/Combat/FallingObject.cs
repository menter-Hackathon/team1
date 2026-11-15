using UnityEngine;

/// <summary>
/// 落下物（部品 F）・設置物（部品 G）。AttackRunner が AttackData から作る。
/// 流れ：（見えない）→ 現れる → 落ち始める → 床に着く → すぐ消える／すり抜け床として残ってから消える。
/// ・判定は落下中だけ
/// ・影（予告）を出す設定なら、作った瞬間から着地するまで、落ちる場所の床に影を出す
/// 出したあとは自分で進むので、攻撃した人が攻撃を止めても最後まで残る。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingObject : MonoBehaviour
{
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);

    AttackRunner _owner;
    AttackData _data;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    SpriteRenderer _visual;
    SpriteRenderer _shadow;
    Collider2D _platform;
    float _appearAt;
    float _fallAt;
    float _groundY;
    float _removeAt = float.PositiveInfinity;

    public AttackData Data => _data;
    /// <summary>見た目の絵</summary>
    public SpriteRenderer Visual => _visual;
    public bool IsVisible => _visual != null && _visual.enabled;
    public bool IsFalling { get; private set; }
    public bool HasLanded { get; private set; }
    /// <summary>着地したら足場になっている間 true</summary>
    public bool IsPlatform => _platform != null && _platform.enabled;
    /// <summary>着地する床の高さ</summary>
    public float GroundY => _groundY;
    /// <summary>着地した時刻（テスト用）</summary>
    public float LandedTime { get; private set; } = -1f;
    /// <summary>落ち始めた時刻（テスト用）</summary>
    public float FallStartTime => _fallAt;

    /// <summary>
    /// 落下物を1つ作る。hangPosition は現れる位置（中心）。appearAt に現れ、fallAt に落ち始める（どちらも Time.time）。
    /// showShadow なら、落ちる場所の床に今から影を出す。
    /// 【仮】dismissable なら、落ち始めるまでの間、クリックで消せる（タブ増殖のタブ。DismissableTab）
    /// </summary>
    public static FallingObject Spawn(AttackRunner owner, AttackData data, Vector2 hangPosition, float appearAt, float fallAt, bool showShadow, bool dismissable = false)
    {
        GameObject go = owner.CreateSpawnedObject($"Falling_{data.name}", hangPosition);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Vector2 size = data.ObjectSize;
        var hit = go.AddComponent<BoxCollider2D>();
        hit.isTrigger = true;
        hit.size = size;

        var f = go.AddComponent<FallingObject>();
        f._owner = owner;
        f._data = data;
        f._rb = rb;
        f._appearAt = appearAt;
        f._fallAt = fallAt;
        f._visual = AttackRunner.AddVisual(go.transform, data.Sprite, data.Color, size, 19);
        f._visual.enabled = false;
        f._hitbox = owner.AddHitbox(go, data, hit);

        owner.TryFindGround(hangPosition.x, hangPosition.y, out f._groundY);

        if (data.BecomesPlatform)
        {
            // 着地したら、上の端に乗れるすり抜け床になる
            var p = new GameObject("Platform");
            p.layer = LayerMask.NameToLayer("OneWayPlatform");
            p.transform.SetParent(go.transform, false);
            var pc = p.AddComponent<BoxCollider2D>();
            pc.size = size;
            pc.enabled = false;
            f._platform = pc;
        }

        if (showShadow)
        {
            var s = new GameObject("Shadow");
            s.layer = go.layer;
            s.transform.SetParent(go.transform, false);
            s.transform.position = new Vector2(hangPosition.x, f._groundY + 0.12f);
            f._shadow = AttackRunner.AddVisual(s.transform, PlaceholderSprites.Circle, ShadowColor, new Vector2(size.x, 0.35f), 16, "Ellipse");
        }
        if (dismissable) go.AddComponent<DismissableTab>();
        return f;
    }

    void FixedUpdate()
    {
        float now = Time.time;
        if (_shadow != null) _shadow.transform.position = new Vector2(_rb.position.x, _groundY + 0.12f); // 影は床に置いたまま

        if (HasLanded)
        {
            if (now >= _removeAt) Destroy(gameObject);
            else if (_platform != null && _removeAt - now < 1f)
            {
                _visual.enabled = Mathf.FloorToInt(now / 0.1f) % 2 == 0; // 消える1秒前から点滅
            }
            return;
        }

        if (!_visual.enabled && now >= _appearAt) _visual.enabled = true;
        if (!IsFalling && now >= _fallAt)
        {
            IsFalling = true;
            _visual.enabled = true;
            _hitbox.Activate();
        }
        if (!IsFalling) return;

        Vector2 pos = _rb.position;
        float half = _data.ObjectSize.y * 0.5f;
        float nextY = pos.y - _data.FallSpeed * Time.fixedDeltaTime;
        if (nextY - half <= _groundY)
        {
            _rb.MovePosition(new Vector2(pos.x, _groundY + half));
            Land(now);
            return;
        }
        _rb.MovePosition(new Vector2(pos.x, nextY));
    }

    void Land(float now)
    {
        HasLanded = true;
        IsFalling = false;
        LandedTime = now;
        _hitbox.Deactivate(); // 判定は落下中だけ
        if (_shadow != null) Destroy(_shadow.gameObject);

        if (_data.LandRemainTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (_platform != null) _platform.enabled = true;
        _removeAt = now + _data.LandRemainTime;
    }
}
