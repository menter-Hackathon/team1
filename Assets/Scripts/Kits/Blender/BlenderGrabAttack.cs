using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Blender の技1「G（グラブ）」（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// 流れ：ボスの前に立方体（1.5マス。オレンジの輪かく）→ `G` `X` のキーの印と、プレイヤーの高さに横の赤い線（0.8秒。高さは固定）→
///       線の上を毎秒 14マスで反対の壁まで。
/// ・第2形態：そのあと `G` `Z`：立方体がプレイヤーの真上へ動き、縦の赤い線（0.8秒。x は固定）→ 落ちる（攻撃データの欄で）
/// ・当たるのは飛んでいる・落ちている間だけ。動いて位置を合わせている間は当たらない
/// </summary>
[CreateAssetMenu(fileName = "BlenderGrab_", menuName = "Team1/Kits/Blender/Grab")]
public class BlenderGrabAttack : CustomAttack
{
    [SerializeField, Min(0.5f)] float _size = 1.5f;
    [Tooltip("赤い線の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(1f)] float _speed = 14f;
    [Tooltip("第2形態：G Z（真上から落とす）")]
    [SerializeField] bool _thenDrop;
    [SerializeField, Min(1f)] float _dropSpeed = 18f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        GameObject root = owner.CreateSpawnedObject("BlenderGrab", owner.transform.position);
        var e = root.AddComponent<BlenderGrabEffect>();
        e.Setup(owner, data, _size, _telegraph, _speed, _thenDrop, _dropSpeed);
        owner.NotifyReleased(data);
        while (e != null && !e.Done) yield return null;
    }
}

/// <summary>G（グラブ）の立方体（自分で進む。ボスの形態変化・正常化で消える）</summary>
public class BlenderGrabEffect : CustomEffect
{
    enum State { Appear, AimX, FlyX, MoveUp, AimZ, FallZ, Vanish }

    AttackRunner _owner;
    Transform _cube;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Kit3DVisual _visual;
    Transform _keys;
    SpriteRenderer _line;
    State _state;
    float _t0;
    Vector2 _from, _to;
    float _size, _telegraph, _speed, _dropSpeed;
    bool _thenDrop;
    int _dir;
    Rect _arena;

    /// <summary>技の長さが終わった（立方体が壁・床に着いた）</summary>
    public bool Done => _state >= State.Vanish || (!_thenDrop && _state > State.FlyX);

    internal void Setup(AttackRunner owner, AttackData data, float size, float telegraph, float speed, bool thenDrop, float dropSpeed)
    {
        _owner = owner;
        _size = size;
        _telegraph = telegraph;
        _speed = speed;
        _thenDrop = thenDrop;
        _dropSpeed = dropSpeed;
        _arena = BattleArena.GetBounds();
        Vector2 boss = owner.transform.position;
        Vector2 target = owner.TargetPoint ?? _arena.center;
        _dir = target.x >= boss.x ? 1 : -1;
        Vector2 start = new Vector2(Mathf.Clamp(boss.x + _dir * (1.2f + size), _arena.xMin + size, _arena.xMax - size), boss.y);

        var go = new GameObject("Cube");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = start;
        _cube = go.transform;
        _rb = go.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = Vector2.one * size * 0.9f;
        _hitbox = owner.AddHitbox(go, data, box);
        _visual = Kit3D.AddVisual(go.transform, Kit3DShape.Cube, Vector3.one * size, BlenderArt.Object, gameObject, "blender.boss", 8);
        _visual.Busy = true;
        _visual.AddOutline(BlenderArt.Select);
        _visual.SetScale(0.01f);
        _state = State.Appear;
        _t0 = Time.time;
    }

    void Update()
    {
        float now = Time.time;
        float t = now - _t0;
        switch (_state)
        {
            case State.Appear:
                _visual.SetScale(Mathf.Clamp01(t / 0.25f));
                if (t >= 0.25f) BeginAimX();
                break;
            case State.AimX:
                // 最初の 0.2秒で、線の高さへ動く（当たらない）
                _cube.position = Vector2.Lerp(_from, _to, Mathf.Clamp01(t / 0.2f));
                Blink(t);
                if (t >= _telegraph) BeginFlyX();
                break;
            case State.MoveUp:
                _cube.position = Vector2.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f)));
                if (t >= 0.35f) BeginAimZ();
                break;
            case State.AimZ:
                Blink(t);
                if (t >= _telegraph) BeginFallZ();
                break;
            case State.Vanish:
                _visual.SetScale(1f - Mathf.Clamp01(t / 0.2f));
                if (t >= 0.2f) Destroy(gameObject);
                break;
        }
    }

    void Blink(float t)
    {
        if (_line != null) _line.enabled = _telegraph - t > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (_state == State.FlyX)
        {
            Vector2 p = (Vector2)_cube.position + new Vector2(_dir * _speed * dt, 0f);
            _rb.MovePosition(p);
            // 壁・置いた箱（Default Cube など）にぶつかったら止まる
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(Kit3D.SolidMask);
            var hits = new RaycastHit2D[4];
            bool blocked = false;
            int n = Physics2D.BoxCast(_cube.position, Vector2.one * _size * 0.8f, 0f, new Vector2(_dir, 0f), filter, hits, _speed * dt + 0.05f);
            for (int i = 0; i < n; i++) if (hits[i].distance > 0f && Mathf.Abs(hits[i].normal.x) > 0.5f) blocked = true;
            if (blocked || (_dir > 0 && p.x >= _arena.xMax - _size * 0.5f) || (_dir < 0 && p.x <= _arena.xMin + _size * 0.5f))
            {
                _hitbox.Deactivate();
                if (_thenDrop) BeginMoveUp(); else BeginVanish();
            }
        }
        else if (_state == State.FallZ)
        {
            Vector2 p = (Vector2)_cube.position + new Vector2(0f, -_dropSpeed * dt);
            float floor = _arena.yMin + _size * 0.5f;
            if (p.y <= floor)
            {
                p.y = floor;
                _rb.MovePosition(p);
                _hitbox.Deactivate();
                BeginVanish();
                return;
            }
            _rb.MovePosition(p);
        }
    }

    void Clear()
    {
        if (_keys != null) Destroy(_keys.gameObject);
        if (_line != null) Destroy(_line.transform.parent.gameObject);
        _keys = null;
        _line = null;
    }

    void BeginAimX()
    {
        _state = State.AimX;
        _t0 = Time.time;
        _visual.SetScale(1f);
        Vector2 target = _owner != null && _owner.TargetPoint is Vector2 tp ? tp : _arena.center;
        float y = Mathf.Clamp(target.y, _arena.yMin + _size * 0.5f, _arena.yMax - _size);
        _from = _cube.position;
        _to = new Vector2(_cube.position.x, y);
        float endX = _dir > 0 ? _arena.xMax : _arena.xMin;
        _line = BlenderArt.Line(transform, _to, new Vector2(endX, y), KitDress.Red);
        _keys = BlenderArt.Keys(transform, _to + new Vector2(0f, _size * 0.5f + 0.7f), "G", "X");
    }

    void BeginFlyX()
    {
        Clear();
        _state = State.FlyX;
        _t0 = Time.time;
        _cube.position = _to;
        _hitbox.Activate();
    }

    void BeginMoveUp()
    {
        _state = State.MoveUp;
        _t0 = Time.time;
        Vector2 target = _owner != null && _owner.TargetPoint is Vector2 tp ? tp : _arena.center;
        _from = _cube.position;
        _to = new Vector2(Mathf.Clamp(target.x, _arena.xMin + _size, _arena.xMax - _size), _arena.yMax - _size);
    }

    void BeginAimZ()
    {
        _state = State.AimZ;
        _t0 = Time.time;
        _cube.position = _to;
        _line = BlenderArt.Line(transform, _to, new Vector2(_to.x, _arena.yMin), KitDress.Red);
        _keys = BlenderArt.Keys(transform, _to + new Vector2(_size * 0.5f + 1.2f, 0f), "G", "Z");
    }

    void BeginFallZ()
    {
        Clear();
        _state = State.FallZ;
        _t0 = Time.time;
        _hitbox.Activate();
    }

    void BeginVanish()
    {
        Clear();
        _state = State.Vanish;
        _t0 = Time.time;
        _visual.Busy = false;
    }
}
