using UnityEngine;

/// <summary>
/// 落下物（部品 F）・設置物（部品 G）。AttackRunner が AttackData から作る。
/// 流れ：（見えない）→ 現れる → 落ち始める → 床に着く → すぐ消える／すり抜け床として残ってから消える。
/// ・判定は落下中だけ
/// ・影（予告）を出す設定なら、作った瞬間から着地するまで、落ちる場所の床に影を出す
/// 出現型（SpawnInPlace。ポップアップ出現）は落とさない：出る場所に薄い枠（予告）→ その場にパッと現れる →
/// 判定は現れてから判定時間（AttackData の判定時間）だけ → 現れた時刻から「着地後に残る秒数」だけ足場として残る。
/// どちらも、足場は消える1秒前から点滅する。
/// 出したあとは自分で進むので、攻撃した人が攻撃を止めても最後まで残る。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingObject : MonoBehaviour
{
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.7f);

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
    bool _appearInPlace;  // 出現型（落とさない）
    bool _appeared;
    float _hitEnd;
    SpriteRenderer _frame; // 出現型の予告（出る場所の薄い枠）

    public AttackData Data => _data;
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
    /// <summary>出現型（落とさずにその場に出す）なら true</summary>
    public bool IsAppearInPlace => _appearInPlace;
    /// <summary>出現型が現れた時刻。まだなら -1（テスト用）</summary>
    public float AppearedTime { get; private set; } = -1f;
    /// <summary>攻撃判定が出ているか（テスト用）</summary>
    public bool IsHitActive => _hitbox != null && _hitbox.IsActive;

    /// <summary>
    /// 出現型の落下物を1つ作る（ポップアップ出現）。center は出る位置（中心）。
    /// 今から appearAt（Time.time）まで出る場所に薄い枠を出し、appearAt にその場へパッと現れる
    /// </summary>
    public static FallingObject SpawnInPlace(AttackRunner owner, AttackData data, Vector2 center, float appearAt)
    {
        FallingObject f = Spawn(owner, data, center, appearAt, appearAt, false);
        f._appearInPlace = true;
        f.gameObject.name = $"Appear_{data.name}";
        var frame = new GameObject("Frame");
        frame.layer = f.gameObject.layer;
        frame.transform.SetParent(f.transform, false);
        f._frame = AttackRunner.AddVisual(frame.transform, PlaceholderSprites.Frame, FrameColor, data.ObjectSize, 18, "Frame");
        return f;
    }

    /// <summary>
    /// 落下物を1つ作る。hangPosition は現れる位置（中心）。appearAt に現れ、fallAt に落ち始める（どちらも Time.time）。
    /// showShadow なら、落ちる場所の床に今から影を出す
    /// </summary>
    public static FallingObject Spawn(AttackRunner owner, AttackData data, Vector2 hangPosition, float appearAt, float fallAt, bool showShadow)
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

        if (_appearInPlace)
        {
            UpdateAppear(now);
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

    /// <summary>出現型：枠（予告）→ パッと現れる → 判定時間だけ判定 → あとは足場として残る（HasLanded のあとの処理は落下型と同じ）</summary>
    void UpdateAppear(float now)
    {
        if (!_appeared)
        {
            if (now < _appearAt)
            {
                // 予告の枠。最後の0.3秒は点滅させる
                if (_frame != null) _frame.enabled = _appearAt - now > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0;
                return;
            }
            _appeared = true;
            AppearedTime = now;
            if (_frame != null) Destroy(_frame.transform.parent.gameObject);
            _visual.enabled = true;
            _hitbox.Activate();
            if (_platform != null) _platform.enabled = true; // 現れた瞬間から足場
            _hitEnd = now + _data.ActiveTime;
            _removeAt = now + _data.LandRemainTime;
        }
        if (now < _hitEnd) return;

        // 判定はここまで。あとは足場として残る（残る秒数が0ならすぐ消える）
        HasLanded = true;
        LandedTime = now;
        _hitbox.Deactivate();
        if (_data.LandRemainTime <= 0f) Destroy(gameObject);
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
