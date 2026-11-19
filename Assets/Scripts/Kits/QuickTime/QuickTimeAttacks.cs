using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>巻き戻しの弾（自分で進む）</summary>
public class QuickTimeRewindBall : CustomEffect
{
    enum State { Out, Hold, Back }

    AttackRunner _owner;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Transform _visual;
    SpriteRenderer _icon, _flash;
    GameObject _aimLine;
    State _state;
    Vector2 _origin, _dir, _stop, _backDir;
    float _out, _outT, _hold, _back, _t0;
    bool _aims;

    internal void Setup(AttackRunner owner, AttackData data, Vector2 origin, Vector2 dir, float outSpeed, float outTime, float hold, float backSpeed, bool aims, float size)
    {
        _owner = owner;
        _origin = origin;
        _dir = dir;
        _out = outSpeed;
        _outT = outTime;
        _hold = hold;
        _back = backSpeed;
        _aims = aims;
        _t0 = Time.time;
        _state = State.Out;
        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = size * 0.42f;
        _hitbox = owner.AddHitbox(gameObject, data, col, false);
        _hitbox.Activate();
        SpriteRenderer sr = AttackRunner.AddVisual(transform, QuickTimeArt.BallSprite, Color.white, Vector2.one * size, 21, "Ball");
        _visual = sr.transform;
        _flash = AttackRunner.AddVisual(transform, PlaceholderSprites.Circle, new Color(1f, 0.27f, 0.23f, 0.6f), Vector2.one * size * 1.3f, 20, "Flash");
        _flash.enabled = false;
        _icon = RuntimeSprites.Show(transform, QuickTimeArt.RewindIcon, new Vector2(0f, size * 0.5f + 0.35f), 22, 1f, "Rewind");
        _icon.enabled = false;
    }

    void Update()
    {
        float t = Time.time - _t0;
        if (_state == State.Hold)
        {
            bool on = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            _flash.enabled = on;
            if (t >= _hold)
            {
                _flash.enabled = false;
                _icon.enabled = false;
                if (_aimLine != null) Destroy(_aimLine);
                _state = State.Back;
                _t0 = Time.time;
            }
        }
    }

    void FixedUpdate()
    {
        float t = Time.time - _t0;
        float dt = Time.fixedDeltaTime;
        Vector2 pos = _rb.position;
        if (_state == State.Out)
        {
            pos += _dir * _out * dt;
            _rb.MovePosition(pos);
            if (t >= _outT || OutOfArena(pos)) BeginHold(pos);
        }
        else if (_state == State.Back)
        {
            if (_aims)
            {
                pos += _backDir * _back * dt;
                _rb.MovePosition(pos);
                if (OutOfArena(pos) || t > 4f) Destroy(gameObject);
                return;
            }
            Vector2 to = _origin - pos;
            float step = _back * dt;
            if (to.magnitude <= step) { Destroy(gameObject); return; }
            _rb.MovePosition(pos + to.normalized * step);
        }
    }

    void BeginHold(Vector2 pos)
    {
        _state = State.Hold;
        _t0 = Time.time;
        _stop = pos;
        _icon.enabled = true;
        if (_aims)
        {
            // この弾だけ、今のプレイヤーへ向きを変える（向きはここで固定。止まっている間に赤い線）
            Vector2 target = _owner != null && _owner.TargetPoint is Vector2 tp ? tp : _origin;
            _backDir = (target - pos).sqrMagnitude > 0.0001f ? (target - pos).normalized : (_origin - pos).normalized;
            _aimLine = KitDress.Line(_owner, pos, pos + _backDir * 25f, _hold);
        }
    }

    static bool OutOfArena(Vector2 p)
    {
        Rect a = BattleArena.GetBounds();
        return p.x < a.xMin - 0.5f || p.x > a.xMax + 0.5f || p.y < a.yMin - 0.5f || p.y > a.yMax + 1f;
    }
}

/// <summary>トリムの切る所（自分で進む）</summary>
public class QuickTimeTrimEffect : CustomEffect
{
    readonly List<Hitbox> _hits = new List<Hitbox>();
    readonly List<GameObject> _warn = new List<GameObject>();
    float _cutAt;
    bool _cut;

    internal void Setup(AttackRunner owner, AttackData data, Rect arena, float center, float width, float telegraph)
    {
        _cutAt = Time.time + telegraph;
        float l = center - width * 0.5f, r = center + width * 0.5f;
        float h = arena.height + 1f;
        float cy = arena.yMin + h * 0.5f - 0.5f;
        // 再生バー（部屋の下の端）とつまみ
        var bar = Child("Bar", new Vector2(arena.center.x, arena.yMin + 0.35f));
        AttackRunner.AddVisual(bar.transform, PlaceholderSprites.Square, QuickTimeArt.Bar, new Vector2(arena.width, 0.5f), 18, "Bg");
        AttackRunner.AddVisual(bar.transform, PlaceholderSprites.Square, new Color(1f, 0.84f, 0.04f, 0.5f), new Vector2(width, 0.5f), 19, "Kept").transform.position = new Vector2(center, arena.yMin + 0.35f);
        foreach (float x in new[] { l, r })
        {
            var hd = Child("Handle", new Vector2(x, arena.yMin + 0.6f));
            RuntimeSprites.Show(hd.transform, QuickTimeArt.Handle, Vector2.zero, 22, 1.4f, "Knob");
            AttackRunner.AddVisual(hd.transform, PlaceholderSprites.Square, QuickTimeArt.Yellow, new Vector2(2f * Kit3D.Px, h), 21, "Edge").transform.position = new Vector2(x, cy);
        }
        // 外側：赤いしま（予告）と判定
        AddSide(owner, data, arena.xMin, l, cy, h);
        AddSide(owner, data, r, arena.xMax, cy, h);
    }

    void AddSide(AttackRunner owner, AttackData data, float x0, float x1, float cy, float h)
    {
        float w = x1 - x0;
        if (w <= 0.05f) return;
        float cx = (x0 + x1) * 0.5f;
        var warn = Child("Warn", new Vector2(cx, cy));
        QuickTimeArt.StripeBox(warn.transform, new Vector2(cx, cy), new Vector2(w, h), new Color(1f, 0.27f, 0.23f, 0.45f), 17);
        _warn.Add(warn);
        var hit = Child("Cut", new Vector2(cx, cy));
        var rb = hit.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = hit.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(w, h);
        _hits.Add(owner.AddHitbox(hit, data, box));
        var cutFx = AttackRunner.AddVisual(hit.transform, PlaceholderSprites.Square, new Color(1f, 1f, 1f, 0.7f), new Vector2(w, h), 23, "CutFx");
        cutFx.enabled = false;
    }

    GameObject Child(string name, Vector2 at)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        return go;
    }

    void Update()
    {
        float left = _cutAt - Time.time;
        if (!_cut)
        {
            bool on = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            foreach (var w in _warn) if (w != null) w.SetActive(on);
            if (left > 0f) return;
            _cut = true;
            foreach (var w in _warn) if (w != null) Destroy(w);
            foreach (var h in _hits)
            {
                if (h == null) continue;
                h.Activate();
                Transform fx = h.transform.Find("CutFx");
                if (fx != null) fx.GetComponent<SpriteRenderer>().enabled = true;
            }
            return;
        }
        if (left < -0.2f) Destroy(gameObject);
    }
}

/// <summary>画面収録（自分で進む）</summary>
public class QuickTimeRecordEffect : CustomEffect
{
    const float Sample = 0.05f;

    AttackRunner _owner;
    readonly List<Vector2> _path = new List<Vector2>();
    readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
    GameObject _rec;
    Transform _ghost;
    SpriteRenderer _ghostSr;
    Hitbox _hitbox;
    float _record, _glow, _speedUp, _t0, _nextSample;
    int _stage;
    Vector2 _bodyOffset = new Vector2(0f, 0.5f);

    internal void Setup(AttackRunner owner, AttackData data, Rect arena, float record, float glow, float speedUp)
    {
        _owner = owner;
        _record = record;
        _glow = glow;
        _speedUp = speedUp;
        _t0 = Time.time;
        // 上に赤い丸と REC
        _rec = new GameObject("Rec");
        _rec.layer = gameObject.layer;
        _rec.transform.SetParent(transform, false);
        _rec.transform.position = new Vector2(arena.center.x, arena.yMax - 1.6f);
        AttackRunner.AddVisual(_rec.transform, PlaceholderSprites.Square, QuickTimeArt.Bar, new Vector2(2.6f, 0.8f), 30, "Bg");
        RuntimeSprites.Show(_rec.transform, QuickTimeArt.RecDot, new Vector2(-0.8f, 0f), 31, 1f, "Dot").color = QuickTimeArt.Rec;
        WorldText.Create(_rec.transform, "REC", 0.4f, Color.white, 32, true, TextAnchor.MiddleLeft).transform.localPosition = new Vector3(-0.35f, 0.02f, -0.001f);

        // 赤い影（プレイヤーの絵を赤くした物）と判定
        var g = new GameObject("Ghost");
        g.layer = gameObject.layer;
        g.transform.SetParent(transform, false);
        _ghost = g.transform;
        var rb = g.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = g.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.6f, 0.9f);
        _hitbox = owner.AddHitbox(g, data, box, false);
        Sprite ps = null;
        PlayerController pc = KitPlayer.Controller;
        if (pc != null)
        {
            SpriteRenderer psr = pc.GetComponentInChildren<SpriteRenderer>();
            if (psr != null) ps = psr.sprite;
        }
        if (ps != null)
        {
            _ghostSr = RuntimeSprites.Show(g.transform, ps, Vector2.zero, 24, 1f, "Shadow");
            _ghostSr.color = new Color(1f, 0.2f, 0.2f, 0.75f);
        }
        else _ghostSr = AttackRunner.AddVisual(g.transform, PlaceholderSprites.Square, new Color(1f, 0.2f, 0.2f, 0.75f), new Vector2(0.7f, 1f), 24, "Shadow");
        g.SetActive(false);
        if (pc != null)
        {
            var col = pc.GetComponent<BoxCollider2D>();
            if (col != null) _bodyOffset = col.offset;
        }
    }

    void Update()
    {
        float t = Time.time - _t0;
        if (_stage == 0)
        {
            // 録画：通った道に赤い点線
            if (_rec != null) _rec.transform.Find("Dot").gameObject.SetActive(Mathf.FloorToInt(Time.time / 0.4f) % 2 == 0);
            if (Time.time >= _nextSample)
            {
                _nextSample = Time.time + Sample;
                Transform p = KitPlayer.Transform;
                if (p != null)
                {
                    Vector2 at = (Vector2)p.position + _bodyOffset;
                    _path.Add(at);
                    if (_path.Count % 2 == 1)
                    {
                        var d = AttackRunner.AddVisual(transform, PlaceholderSprites.Square, new Color(1f, 0.27f, 0.23f, 0.85f), Vector2.one * (3f * Kit3D.Px), 18, "Dot");
                        d.transform.position = at;
                        _dots.Add(d);
                    }
                }
            }
            if (t < _record) return;
            _stage = 1;
            _t0 = Time.time;
            if (_rec != null) Destroy(_rec);
            foreach (var d in _dots) if (d != null) d.transform.localScale *= 2f; // 太く光る
            return;
        }
        if (_stage == 1)
        {
            bool on = _glow - t > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            foreach (var d in _dots) if (d != null) d.enabled = on;
            if (t < _glow) return;
            _stage = 2;
            _t0 = Time.time;
            if (_path.Count == 0) { Destroy(gameObject); return; }
            _ghost.gameObject.SetActive(true);
            _ghost.position = _path[0];
            _hitbox.Activate();
            return;
        }
        // なぞる（1.5倍の速さ）
        float idx = t * _speedUp / Sample;
        int i = Mathf.FloorToInt(idx);
        if (i >= _path.Count - 1)
        {
            Destroy(gameObject);
            return;
        }
        Vector2 a = _path[i], b = _path[i + 1];
        _ghost.position = Vector2.Lerp(a, b, idx - i);
        if (_ghostSr != null && Mathf.Abs(b.x - a.x) > 0.001f) _ghostSr.flipX = b.x < a.x;
        // なぞり終えた所の点は消す
        int dotsDone = i / 2;
        for (int k = 0; k < dotsDone && k < _dots.Count; k++) if (_dots[k] != null) { Destroy(_dots[k].gameObject); _dots[k] = null; }
    }
}

/// <summary>閉じるつまみ（能力。自分で進む）</summary>
public class QuickTimeTrimSkillEffect : MonoBehaviour
{
    AttackRunner _owner;
    Transform _left, _right;
    Hitbox _hitbox;
    float _reach, _close, _t0, _cy;
    bool _pulled;

    internal void Setup(AttackRunner owner, AttackData data, float reach, float close, float height)
    {
        _owner = owner;
        _reach = reach;
        _close = close;
        _t0 = Time.time;
        Vector2 o = owner.OriginPoint;
        _cy = o.y;
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(reach * 2f, height);
        _hitbox = owner.AddHitbox(gameObject, data, box);
        _hitbox.Activate();
        _left = MakeHandle(height);
        _right = MakeHandle(height);
        _span = AttackRunner.AddVisual(transform, PlaceholderSprites.Square, new Color(1f, 0.84f, 0.04f, 0.25f), new Vector2(reach * 2f, height), 18, "Span").transform;
        _spanBaseX = _span.localScale.x;
    }

    Transform _span;
    float _spanBaseX;

    Transform MakeHandle(float height)
    {
        var go = new GameObject("Handle");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        RuntimeSprites.Show(go.transform, QuickTimeArt.Handle, Vector2.zero, 26, height / (14f / 18f), "Knob");
        return go.transform;
    }

    void Update()
    {
        float t = Time.time - _t0;
        float k = Mathf.Clamp01(t / _close);
        float x = Mathf.Lerp(_reach, 0.25f, k);
        _left.localPosition = new Vector3(-x, 0f, 0f);
        _right.localPosition = new Vector3(x, 0f, 0f);
        if (_span != null) _span.localScale = new Vector3(_spanBaseX * x / Mathf.Max(0.01f, _reach), _span.localScale.y, 1f);
        if (k >= 1f && !_pulled)
        {
            _pulled = true;
            _hitbox.Deactivate();
            PullZako();
        }
        if (t > _close + 0.15f) Destroy(gameObject);
    }

    // 間にいた雑魚を、自分の前へ寄せる（壁の中には入れない）
    void PullZako()
    {
        if (_owner == null) return;
        Vector2 o = _owner.OriginPoint;
        float frontX = o.x + _owner.Facing * 1.1f;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(Kit3D.SolidMask);
        var res = new Collider2D[2];
        foreach (Health h in RewardKit.ZakoInRadius(transform.position, _reach + 0.3f))
        {
            if (h == null || h.IsDead) continue;
            Vector3 p = h.transform.position;
            if (Mathf.Abs(p.y - _cy) > 2f) continue;
            var target = new Vector2(frontX, p.y);
            if (Physics2D.OverlapBox(target + Vector2.up * 0.5f, new Vector2(0.6f, 0.8f), 0f, filter, res) > 0) continue;
            var body = h.GetComponent<KinematicBody2D>();
            if (body != null) body.Warp(new Vector2(target.x, p.y)); // 雑魚は KinematicBody2D で動くので、Warp で動かす
            else h.transform.position = new Vector3(target.x, p.y, p.z);
        }
    }
}
