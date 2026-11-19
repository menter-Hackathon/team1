using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Unity の技2「Rigidbody の球」（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// 流れ：ボスの前に直径 2マスの球が出る（0.3秒）→ 床に赤い矢印の帯（進む向き。0.8秒）→ 毎秒 9マスで反対の壁まで転がる →
///       壁（か置いた箱）で止まり、1秒で消える。
/// ・避け方：ジャンプで越える（ジャンプ 3.2 ＞ 球 2）。足場の上は安全
/// ・第2形態：1.2秒あとに、反対の壁からもう1つ（攻撃データの欄で）
/// </summary>
[CreateAssetMenu(fileName = "UnityBall_", menuName = "Team1/Kits/Unity/Rigidbody Ball")]
public class UnityBallAttack : CustomAttack
{
    [SerializeField, Min(0.5f)] float _diameter = 2f;
    [SerializeField, Min(0f)] float _appear = 0.3f;
    [Tooltip("矢印の帯の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(1f)] float _speed = 9f;
    [Tooltip("第2形態：反対の壁からもう1つ（0 なら出さない）。最初の球からの秒数")]
    [SerializeField, Min(0f)] float _secondDelay;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 boss = owner.transform.position;
        Vector2 target = owner.TargetPoint ?? arena.center;
        int dir = target.x >= boss.x ? 1 : -1;
        float r = _diameter * 0.5f;
        float startX = Mathf.Clamp(boss.x + dir * (1.4f + r), arena.xMin + r + 0.1f, arena.xMax - r - 0.1f);
        UnityBallEffect.Spawn(owner, data, new Vector2(startX, arena.yMin + r), dir, _diameter, _appear, _telegraph, _speed);
        owner.NotifyReleased(data);
        float total = _appear + _telegraph + arena.width / _speed + 0.3f;
        if (_secondDelay > 0f)
        {
            yield return new WaitForSeconds(_secondDelay);
            float x2 = dir > 0 ? arena.xMax - r - 0.3f : arena.xMin + r + 0.3f;
            UnityBallEffect.Spawn(owner, data, new Vector2(x2, arena.yMin + r), -dir, _diameter, _appear, _telegraph, _speed);
            owner.NotifyReleased(data);
        }
        yield return new WaitForSeconds(total);
    }
}

/// <summary>転がる球（自分で進む。ボスの形態変化・正常化で消える）</summary>
public class UnityBallEffect : CustomEffect
{
    enum State { Appear, Telegraph, Roll, Stop }

    AttackRunner _owner;
    Transform _ball;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Kit3DVisual _visual;
    GameObject _band;
    State _state;
    float _until;
    int _dir;
    float _r;
    float _speed;
    float _appear;
    float _nextDust;

    public static UnityBallEffect Spawn(AttackRunner owner, AttackData data, Vector2 at, int dir, float diameter, float appear, float telegraph, float speed)
    {
        GameObject root = owner.CreateSpawnedObject("UnityBall", at);
        var e = root.AddComponent<UnityBallEffect>();
        e._owner = owner;
        e._dir = dir >= 0 ? 1 : -1;
        e._r = diameter * 0.5f;
        e._speed = speed;
        e._appear = Mathf.Max(0.01f, appear);
        e._state = State.Appear;
        e._until = Time.time + e._appear;

        var ball = new GameObject("Ball");
        ball.layer = root.layer;
        ball.transform.SetParent(root.transform, false);
        e._ball = ball.transform;
        e._rb = ball.AddComponent<Rigidbody2D>();
        e._rb.bodyType = RigidbodyType2D.Kinematic;
        e._rb.useFullKinematicContacts = true;
        var col = ball.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = e._r * 0.85f;
        e._hitbox = owner.AddHitbox(ball, data, col);
        e._visual = Kit3D.AddVisual(ball.transform, Kit3DShape.Sphere, Vector3.one * diameter, UnityArt.Object, root, "unity.boss", 8);
        e._visual.Busy = true;
        e._visual.SetScale(0.01f);

        // 床の赤い矢印の帯（進む向き。球から反対の壁まで）
        Rect arena = BattleArena.GetBounds();
        float endX = e._dir > 0 ? arena.xMax : arena.xMin;
        float len = Mathf.Abs(endX - at.x);
        e._band = new GameObject("Band");
        e._band.layer = root.layer;
        e._band.transform.SetParent(root.transform, false);
        e._band.transform.position = new Vector2(at.x + e._dir * len * 0.5f, arena.yMin + 0.25f);
        AttackRunner.AddVisual(e._band.transform, PlaceholderSprites.Square, new Color(1f, 0.27f, 0.23f, 0.35f), new Vector2(len, 0.5f), 17, "Base");
        // 矢印「>」を並べる（縁の無い絵は AddVisual だと伸びるので、自分で並べて描く）
        var cg = new GameObject("Chevrons");
        cg.layer = root.layer;
        cg.transform.SetParent(e._band.transform, false);
        var chev = cg.AddComponent<SpriteRenderer>();
        chev.sprite = UnityArt.Chevron;
        chev.drawMode = SpriteDrawMode.Tiled;
        chev.tileMode = SpriteTileMode.Continuous;
        chev.size = new Vector2(len, 0.5f);
        chev.color = KitDress.Red;
        chev.sortingOrder = 18;
        chev.flipX = e._dir < 0;
        e._band.SetActive(false);
        e._telegraphTime = telegraph;
        return e;
    }

    float _telegraphTime;

    void Update()
    {
        float now = Time.time;
        switch (_state)
        {
            case State.Appear:
                _visual.SetScale(Mathf.Clamp01(1f - (_until - now) / _appear));
                if (now >= _until)
                {
                    _visual.SetScale(1f);
                    _state = State.Telegraph;
                    _until = now + _telegraphTime;
                    _band.SetActive(true);
                }
                break;
            case State.Telegraph:
                if (_band != null)
                {
                    float left = _until - now;
                    _band.SetActive(left > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0);
                }
                if (now >= _until)
                {
                    if (_band != null) Destroy(_band);
                    _state = State.Roll;
                    _hitbox.Activate();
                }
                break;
            case State.Stop:
                float t = 1f - (_until - now);
                _visual.Show(t < 0.6f ? Mathf.FloorToInt(now / 0.08f) % 2 == 0 : true);
                if (t >= 0.6f) _visual.SetScale(Mathf.Clamp01(1f - (t - 0.6f) / 0.4f));
                if (now >= _until) Destroy(gameObject);
                break;
        }
    }

    void FixedUpdate()
    {
        if (_state != State.Roll) return;
        float dt = Time.fixedDeltaTime;
        float step = _speed * dt;
        Vector2 pos = _ball.position;
        // 壁・置いた箱にぶつかったら止まる
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(Kit3D.SolidMask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.CircleCast(pos, _r * 0.8f, new Vector2(_dir, 0f), filter, hits, step + 0.05f);
        Rect arena = BattleArena.GetBounds();
        bool stop = false;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0f && hits[i].collider.bounds.max.y < pos.y - _r * 0.6f) continue; // 床そのもの
            if (hits[i].normal.y > 0.7f) continue; // 床の上面
            stop = true;
        }
        float nx = pos.x + _dir * step;
        if (nx > arena.xMax - _r || nx < arena.xMin + _r) stop = true;
        if (stop)
        {
            _hitbox.Deactivate();
            _state = State.Stop;
            _until = Time.time + 1f;
            _visual.Busy = false;
            return;
        }
        _rb.MovePosition(new Vector2(nx, pos.y));
        _visual.Spin(-_dir * step / _r * Mathf.Rad2Deg);
        // 床にこすれる白いドット
        if (Time.time >= _nextDust)
        {
            _nextDust = Time.time + 0.06f;
            UnityDust.Spawn(transform, new Vector2(pos.x - _dir * _r * 0.6f, pos.y - _r + 0.08f), -_dir);
        }
    }
}

/// <summary>床にこすれる白いドット（すぐ消える）</summary>
public class UnityDust : MonoBehaviour
{
    SpriteRenderer _sr;
    Vector2 _v;
    float _born;

    public static void Spawn(Transform parent, Vector2 at, int dir)
    {
        var go = new GameObject("Dust");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var d = go.AddComponent<UnityDust>();
        d._sr = AttackRunner.AddVisual(go.transform, UnityArt.Dot, Color.white, Vector2.one * (2f * Kit3D.Px), 19, "Dot");
        d._v = new Vector2(dir * Random.Range(1f, 2.5f), Random.Range(0.8f, 2f));
        d._born = Time.time;
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t > 0.35f) { Destroy(gameObject); return; }
        transform.position += (Vector3)(_v * Time.deltaTime);
        _v.y -= 6f * Time.deltaTime;
        Color c = _sr.color;
        c.a = 1f - t / 0.35f;
        _sr.color = c;
    }
}
