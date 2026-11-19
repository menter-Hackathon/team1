using System;
using UnityEngine;

/// <summary>
/// 【仮】落ちる 3D の物（2026-11-19。docs/app-kits-2-design.md X1 の Kit3D.Faller）。担当A の物。
/// 流れ：予告（角だけの赤い枠と「!」・床の影）→ 現れる（白く光る）→ 重力で落ちる（速さが MinHitSpeed 以上の間だけ当たる）→
///       床・足場・置いた箱の上で止まる（球は少し転がる。転がっている間も当たる）→ 乗れる箱・壁になる（地形のレイヤー）→ Remain 秒で点滅して縮んで消える。
/// ・動きは自分で進める（Rigidbody2D は Kinematic。押されて動かない。枠の真下に落ちる）
/// ・止まったとき、プレイヤー・雑魚・ボスの体が重なっていたら、どくまで箱にしない（閉じこめない）。
///   箱になったあとにボスが重なったら（ワープの行き先）、すぐ消える
/// ・ボスの出した物（owner＝ボスの AttackRunner）は、形態変化（50％）・正常化（100％）で消える（CustomEffect）。
///   ギミックの物は ctx.Hazard を owner にする（片づけの表で消える）
/// ・KitBlock（担当D）には頼らない。これだけで動く（Minecraft の砂もこれを呼ぶ）
/// </summary>
public class Kit3DFaller : CustomEffect
{
    const float MaxFallSpeed = 24f;
    const float RollSpeed = 5f;
    const float BlinkTime = 0.6f;
    const float ShrinkTime = 0.2f;

    enum State { Telegraph, Falling, Rolling, Landed, Expiring }

    static readonly System.Collections.Generic.List<Kit3DFaller> All = new System.Collections.Generic.List<Kit3DFaller>();

    AttackRunner _owner;
    Kit3DFallOptions _opt;
    Kit3DShape _shape;
    Vector3 _size;
    Vector2 _box;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    BoxCollider2D _solid;
    SpriteRenderer _shadow;
    Kit3DBracket _bracket;
    State _state = State.Telegraph;
    Vector2 _pos;
    float _vy;
    float _fallAt;
    float _expireAt = float.PositiveInfinity;
    float _blinkStart;
    float _rollLeft;
    int _rollDir;
    float _floorAtSpawn;
    KitProp _prop;
    bool _landedOnce;

    /// <summary>見た目</summary>
    public Kit3DVisual Visual { get; private set; }
    /// <summary>2D の大きさ（マス。当たり判定・箱）</summary>
    public Vector2 BoxSize => _box;
    /// <summary>止まった（箱になった・消えている途中も含む）</summary>
    public bool HasLanded => _state >= State.Landed;
    /// <summary>落ちている・転がっている（当たる）</summary>
    public bool IsDangerous => _state == State.Falling || _state == State.Rolling;
    /// <summary>乗れる箱になっている</summary>
    public bool IsSolid => _solid != null && _solid.enabled;
    /// <summary>止まったとき（1回）</summary>
    public event Action<Kit3DFaller> Landed;
    /// <summary>近接で壊されたとき（1回）</summary>
    public event Action<Kit3DFaller> Broken;

    /// <summary>作る（Kit3D.Faller から）</summary>
    internal static Kit3DFaller Spawn(AttackRunner owner, AttackData data, Kit3DShape shape, Vector2 at, Vector3 size, Color color, Kit3DFallOptions opt)
    {
        GameObject go = owner != null ? owner.CreateSpawnedObject($"Kit3DFaller_{shape}", at) : new GameObject($"Kit3DFaller_{shape}");
        go.transform.position = at;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var f = go.AddComponent<Kit3DFaller>();
        f._owner = owner;
        f._opt = opt;
        f._shape = shape;
        f._size = size;
        f._box = new Vector2(size.x, shape == Kit3DShape.Sphere ? size.x : size.y);
        f._rb = rb;
        f._pos = at;
        f._fallAt = Time.time + Mathf.Max(0f, opt.Telegraph);

        // 当たり判定（落ちている・転がっている間だけ）
        var hit = go.AddComponent<BoxCollider2D>();
        hit.isTrigger = true;
        hit.size = f._box * 0.88f;
        if (owner != null && data != null) f._hitbox = owner.AddHitbox(go, data, hit);

        // 乗れる箱（止まってから。地形のレイヤー）
        var s = new GameObject("Solid");
        s.layer = LayerMask.NameToLayer("Ground");
        s.transform.SetParent(go.transform, false);
        f._solid = s.AddComponent<BoxCollider2D>();
        f._solid.size = f._box - Vector2.one * 0.04f;
        f._solid.enabled = false;

        // 見た目（予告の間はかくす）
        f.Visual = Kit3D.AddVisual(go.transform, shape, size, color, go, opt.Group, opt.Cap > 0 ? opt.Cap : Kit3D.DefaultCap);
        f.Visual.Busy = true;
        f.Visual.Show(false);
        f.SitVisual();

        // 予告：角だけの枠と「!」、床の影
        if (opt.Telegraph > 0f && !opt.NoBracket)
        {
            f._bracket = Kit3D.Bracket(null, at, f._box + Vector2.one * 0.2f, opt.Telegraph, opt.Bang);
            f._bracket.transform.SetParent(go.transform, true);
        }
        if (Kit3D.FindFloor(at.x, at.y - f._box.y * 0.5f + 0.05f, out float floor, true))
        {
            f._floorAtSpawn = floor;
            var sh = new GameObject("Shadow");
            sh.layer = go.layer;
            sh.transform.SetParent(go.transform, false);
            sh.transform.position = new Vector2(at.x, floor + 0.12f);
            Color sc = opt.ShadowColor.a > 0f ? opt.ShadowColor : new Color(0f, 0f, 0f, 0.45f);
            f._shadow = AttackRunner.AddVisual(sh.transform, Kit3DArt.Shadow, sc, new Vector2(f._box.x, 0.35f), 16, "Ellipse");
        }
        else f._floorAtSpawn = float.NegativeInfinity;
        All.Add(f);
        return f;
    }

    protected override void OnDestroy()
    {
        All.Remove(this);
        base.OnDestroy();
    }

    /// <summary>今ある落ちる物（組で選ぶ。null なら全部）</summary>
    public static System.Collections.Generic.List<Kit3DFaller> InGroup(string group)
    {
        var list = new System.Collections.Generic.List<Kit3DFaller>();
        All.RemoveAll(x => x == null);
        foreach (var f in All) if (group == null || f._opt.Group == group) list.Add(f);
        return list;
    }

    // 見た目の下の端を、箱の下の端に合わせる（回した立方体が床にめりこんで見えないように）
    void SitVisual()
    {
        if (Visual == null) return;
        float bottom = Visual.LowestY();
        Vector3 lp = Visual.transform.localPosition;
        lp.y += -_box.y * 0.5f - bottom;
        Visual.transform.localPosition = lp;
    }

    void FixedUpdate()
    {
        float now = Time.time;
        float dt = Time.fixedDeltaTime;
        switch (_state)
        {
            case State.Telegraph:
                if (now >= _fallAt) StartFall();
                break;
            case State.Falling:
                StepFall(dt);
                break;
            case State.Rolling:
                StepRoll(dt);
                break;
            case State.Landed:
                StepLanded(now);
                break;
        }
        if (IsDangerous && _opt.HitZako) Kit3D.StrikeZako(_pos, Mathf.Max(_box.x, _box.y) * 0.5f + 0.2f, 99, gameObject);
        if (_shadow != null) _shadow.transform.position = new Vector2(_pos.x, _shadow.transform.position.y);
    }

    void Update()
    {
        if (_state != State.Expiring) return;
        float t = Time.time - _blinkStart;
        if (t < BlinkTime)
        {
            Visual.Show(Mathf.FloorToInt(t / 0.08f) % 2 == 0);
            return;
        }
        Visual.Show(true);
        float k = 1f - Mathf.Clamp01((t - BlinkTime) / ShrinkTime);
        Visual.SetScale(k);
        if (k <= 0f) Destroy(gameObject);
    }

    void StartFall()
    {
        _state = State.Falling;
        _vy = 0f;
        if (_bracket != null) Destroy(_bracket.gameObject);
        Visual.Show(true);
        Visual.Flash(0.06f); // 出る瞬間に白い光
        if (_opt.Group == "unity.rule") GameAudio.Event("gm_unity_object_drop"); // Unity のステージの決まりの物体だけ（2026-11-19）
    }

    void SetHit(bool on)
    {
        if (_hitbox == null) return;
        if (on && !_hitbox.IsActive) _hitbox.Activate();
        else if (!on && _hitbox.IsActive) _hitbox.Deactivate();
    }

    void StepFall(float dt)
    {
        _vy = Mathf.Min(MaxFallSpeed, _vy + Mathf.Max(1f, _opt.Gravity) * dt);
        SetHit(_vy >= _opt.MinHitSpeed);
        float step = _vy * dt;
        float bottom = _pos.y - _box.y * 0.5f;
        if (FloorBelow(bottom, step + 0.02f, out float floorY))
        {
            _pos.y = floorY + _box.y * 0.5f;
            _rb.MovePosition(_pos);
            if (_opt.RollDistance > 0f && !_landedOnce)
            {
                _state = State.Rolling;
                _rollLeft = _opt.RollDistance;
                _rollDir = _opt.RollDir != 0 ? Math.Sign(_opt.RollDir) : AwayFromPlayer();
                return;
            }
            Land();
            return;
        }
        _pos.y -= step;
        _rb.MovePosition(_pos);
        Rect arena = BattleArena.GetBounds();
        if (_pos.y < Mathf.Min(arena.yMin, _floorAtSpawn) - 12f) Destroy(gameObject); // 穴に落ちた
    }

    void StepRoll(float dt)
    {
        SetHit(true);
        float step = Mathf.Min(_rollLeft, RollSpeed * dt);
        // 壁・置いた箱にぶつかったら止まる
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(Kit3D.SolidMask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.BoxCast(_pos, _box * 0.9f, 0f, new Vector2(_rollDir, 0f), filter, hits, step + 0.02f);
        bool blocked = false;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            blocked = true;
        }
        if (blocked || step <= 0f) { Land(); return; }
        _pos.x += _rollDir * step;
        _rollLeft -= step;
        _rb.MovePosition(_pos);
        float r = Mathf.Max(0.1f, _box.y * 0.5f);
        Visual.Spin(-_rollDir * step / r * Mathf.Rad2Deg);
        if (_rollLeft <= 0f) Land();
    }

    int AwayFromPlayer()
    {
        Transform p = KitPlayer.Transform;
        if (p == null) return UnityEngine.Random.value < 0.5f ? -1 : 1;
        return p.position.x <= _pos.x ? 1 : -1;
    }

    bool FloorBelow(float bottom, float distance, out float floorY)
    {
        floorY = float.NegativeInfinity;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_opt.LandOnPlatforms ? Kit3D.GroundMask : Kit3D.SolidMask);
        var hits = new RaycastHit2D[6];
        bool found = false;
        float half = _box.x * 0.4f;
        for (int k = -1; k <= 1; k++)
        {
            var origin = new Vector2(_pos.x + k * half, bottom + 0.02f);
            int n = Physics2D.Raycast(origin, Vector2.down, filter, hits, distance + 0.02f);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance <= 0f) continue; // 始点が地形の中（すり抜け床の中など）
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (!found || hits[i].point.y > floorY) floorY = hits[i].point.y;
                found = true;
            }
        }
        // 止まっているほかの物の上（まだ箱になっていない物も。積もる砂）
        foreach (var o in All)
        {
            if (o == null || o == this || o._state != State.Landed) continue;
            if (Mathf.Abs(o._pos.x - _pos.x) >= (o._box.x + _box.x) * 0.5f - 0.05f) continue;
            float top = o._pos.y + o._box.y * 0.5f;
            if (top > bottom + 0.03f || bottom - top > distance) continue;
            if (!found || top > floorY) floorY = top;
            found = true;
        }
        return found;
    }

    void Land()
    {
        _state = State.Landed;
        SetHit(false);
        if (_shadow != null) Destroy(_shadow.gameObject);
        Visual.Busy = false;
        Visual.SetShade(_opt.LandedShade > 0f ? _opt.LandedShade : 1f);
        if (_expireAt == float.PositiveInfinity) _expireAt = _opt.Remain > 0f ? Time.time + _opt.Remain : Time.time;
        if (_opt.BreakHits > 0 && _prop == null)
        {
            _prop = KitProp.Spawn(_pos, null, _box, OnPropBroken, _opt.BreakHits);
            _prop.DestroyOnBreak = false;
            if (_prop.Visual != null) _prop.Visual.enabled = false;
            _prop.transform.SetParent(transform, true);
            _prop.Hit += (p, left) => { if (Visual != null) Visual.Flash(0.08f); };
        }
        if (_landedOnce) return;
        _landedOnce = true;
        GameAudio.Event("faller_land");
        try { Landed?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
        if (_opt.Remain <= 0f) Expire(true);
    }

    void StepLanded(float now)
    {
        if (now >= _expireAt) { Expire(false); return; }
        // 下の物が消えたら、また落ちる（砂・積んだ箱）
        if (!FloorBelow(_pos.y - _box.y * 0.5f, 0.06f, out _))
        {
            _state = State.Falling;
            _vy = 0f;
            if (_solid != null) _solid.enabled = false;
            if (_prop != null) { Destroy(_prop.gameObject); _prop = null; } // 止まったら作り直す（子の Rigidbody2D は一緒に動かないので）
            Visual.Busy = true;
            return;
        }
        if (!_opt.Solid) return;
        if (!_solid.enabled)
        {
            // だれも重なっていなくなったら、乗れる箱にする
            if (!Kit3D.BodyOverlaps(_pos, _box * 0.96f, transform)) _solid.enabled = true;
            return;
        }
        // ワープしたボスが重なったら、すぐ消える
        if (Kit3D.BossOverlaps(_pos, _box * 0.9f)) Expire(true);
    }

    void OnPropBroken(KitProp p)
    {
        try { Broken?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
        Expire(true);
    }

    /// <summary>消す（fast：点滅しないで、すぐ縮む）</summary>
    public void Expire(bool fast)
    {
        if (_state == State.Expiring) return;
        _state = State.Expiring;
        SetHit(false);
        if (_solid != null) _solid.enabled = false;
        if (_prop != null) Destroy(_prop.gameObject);
        if (_bracket != null) Destroy(_bracket.gameObject);
        if (_shadow != null) Destroy(_shadow.gameObject);
        if (Visual == null) { Destroy(gameObject); return; }
        Visual.Busy = false;
        Visual.Show(true);
        _blinkStart = Time.time - (fast ? BlinkTime : 0f);
    }
}
