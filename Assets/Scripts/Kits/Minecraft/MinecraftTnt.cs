using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】TNT（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。ボスの技1「TNT」と、能力「TNT」の両方。
/// 流れ：（投げるなら山なりに飛ぶ）→ 床で白く点滅（導火線）。爆発する範囲の円を出す（ボスの物は赤・能力の物は白）→ 爆発（円の判定 0.15秒）。
/// ・近接（プレイヤーの攻撃）で押せる：1回で 3マスすべる（円も一緒に動く）
/// ・能力の物はプレイヤーには当たらない（陣営）。ボスの砂のブロックもこわす
/// ・ボスの物はボスの出した物（形態変化・正常化で消える）
/// </summary>
public class MinecraftTnt : CustomEffect
{
    const float Size = 1f;
    const float PushDistance = 3f;
    const float PushSpeed = 12f;

    enum State { Fly, Fuse, Done }

    AttackRunner _owner;
    Hitbox _blast;
    CircleCollider2D _blastCol;
    Kit3DVisual _visual;
    Transform _ring;
    KitProp _prop;
    State _state;
    Vector2 _from, _to;
    float _t0, _fly, _fuse, _radius, _apex;
    float _pushLeft;
    int _pushDir;
    bool _breaksSand;
    float _floorY;

    /// <summary>
    /// TNT を出す。from から landAt（床の上の位置）へ fly 秒で山なりに飛ぶ（0 ならその場）。止まってから fuse 秒で爆発。
    /// enemy：ボスの物（円は赤）。false なら能力の物（円は白。砂のブロックもこわす）
    /// </summary>
    public static MinecraftTnt Spawn(AttackRunner owner, AttackData data, Vector2 from, Vector2 landAt, float fly, float fuse, float radius, bool enemy)
    {
        GameObject go = owner.CreateSpawnedObject("MinecraftTnt", from);
        var t = go.AddComponent<MinecraftTnt>();
        t._owner = owner;
        t._from = from;
        t._fly = Mathf.Max(0f, fly);
        t._fuse = fuse;
        t._radius = radius;
        t._breaksSand = !enemy;
        t._apex = Mathf.Max(2f, Mathf.Abs(landAt.x - from.x) * 0.35f);
        float floor = landAt.y - Size * 0.5f;
        if (Kit3D.FindFloor(landAt.x, landAt.y + 0.3f, out float f, true)) floor = f;
        t._floorY = floor;
        t._to = new Vector2(landAt.x, floor + Size * 0.5f);
        t._state = State.Fly;
        t._t0 = Time.time;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        t._blastCol = go.AddComponent<CircleCollider2D>();
        t._blastCol.isTrigger = true;
        t._blastCol.radius = radius;
        t._blast = owner.AddHitbox(go, data, t._blastCol);
        t._visual = MinecraftArt.TntVisual(go.transform, Size, go);

        // 爆発する範囲の円（ボスの物は赤。投げた瞬間から、落ちる所に出す）
        var ring = new GameObject("Range");
        ring.layer = go.layer;
        ring.transform.SetParent(go.transform, false);
        ring.transform.position = t._to;
        Color fill = enemy ? new Color(1f, 0.27f, 0.23f, 0.18f) : new Color(1f, 1f, 1f, 0.12f);
        Color edge = enemy ? KitDress.Red : new Color(1f, 1f, 1f, 0.7f);
        AttackRunner.AddVisual(ring.transform, PlaceholderSprites.Circle, fill, Vector2.one * radius * 2f, 16, "Fill");
        int dpx = Mathf.RoundToInt(radius * 2f * 18f);
        RuntimeSprites.Show(ring.transform, MinecraftArt.Ring(dpx, 2), Vector2.zero, 17, 1f, "Edge").color = edge;
        t._ring = ring.transform;
        if (t._fly <= 0f) t.Land();
        return t;
    }

    void Land()
    {
        _state = State.Fuse;
        _t0 = Time.time;
        transform.position = _to;
        if (_ring != null) _ring.localPosition = Vector3.zero;
        // 近接で押せる（壊れない。当たるたびにすべる）
        _prop = KitProp.Spawn((Vector2)transform.position, null, Vector2.one * Size, null, 9999);
        _prop.DestroyOnBreak = false;
        if (_prop.Visual != null) _prop.Visual.enabled = false;
        _prop.Hit += OnPushed;
    }

    void OnPushed(KitProp p, int left)
    {
        Transform pl = KitPlayer.Transform;
        _pushDir = pl != null && pl.position.x > transform.position.x ? -1 : 1;
        _pushLeft = PushDistance;
    }

    void Update()
    {
        float t = Time.time - _t0;
        if (_state == State.Fly)
        {
            float k = Mathf.Clamp01(t / _fly);
            Vector2 p = Vector2.Lerp(_from, _to, k);
            p.y += 4f * _apex * k * (1f - k);
            transform.position = p;
            if (_ring != null) _ring.position = _to;
            _visual.Spin(-360f * Time.deltaTime * Mathf.Sign(_to.x - _from.x));
            if (k >= 1f)
            {
                _visual.SetExtraRotation(Quaternion.identity);
                Land();
            }
            return;
        }
        if (_state != State.Fuse) return;
        // 白く点滅（だんだん速く）
        float period = Mathf.Lerp(0.25f, 0.07f, Mathf.Clamp01(t / _fuse));
        _visual.SetShade(Mathf.FloorToInt(t / period) % 2 == 0 ? 1f : 2.2f);
        if (t >= _fuse) StartCoroutine(Explode());
    }

    void FixedUpdate()
    {
        if (_state != State.Fuse || _pushLeft <= 0f) return;
        float step = Mathf.Min(_pushLeft, PushSpeed * Time.fixedDeltaTime);
        Vector2 pos = transform.position;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(Kit3D.SolidMask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.BoxCast(pos, Vector2.one * Size * 0.9f, 0f, new Vector2(_pushDir, 0f), filter, hits, step + 0.02f);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0f) continue;
            _pushLeft = 0f;
            return;
        }
        pos.x += _pushDir * step;
        _pushLeft -= step;
        if (Kit3D.FindFloor(pos.x, pos.y, out float f, true) && f < pos.y) pos.y = Mathf.Max(f + Size * 0.5f, pos.y - 0.2f);
        transform.position = pos;
        if (_prop != null) _prop.transform.position = pos;
    }

    // 押す判定（KitProp）は別の物なので、一緒に消す
    protected override void Restore()
    {
        if (_prop != null) Destroy(_prop.gameObject);
    }

    IEnumerator Explode()
    {
        _state = State.Done;
        if (_prop != null) Destroy(_prop.gameObject);
        if (_ring != null) Destroy(_ring.gameObject);
        Vector2 at = transform.position;
        MinecraftArt.Explosion(at, _radius);
        if (_owner != null && _owner.Team != Team.Player) GameAudio.Se("se_minecraft_boom", 0.9f); // 能力の TNT は、能力の音の中に爆発が入っている
        _visual.Show(false);
        _blast.Activate();
        if (_breaksSand)
        {
            foreach (var f in Kit3DFaller.InGroup("minecraft.sand"))
                if (f != null && Vector2.Distance(f.transform.position, at) <= _radius + 0.4f) f.Expire(true);
        }
        yield return new WaitForSeconds(0.15f);
        _blast.Deactivate();
        Destroy(gameObject);
    }
}
