using UnityEngine;

/// <summary>
/// 【仮】突っ込む雑魚（2026-11-19。docs/stage-pacing-design.md 5章。担当3）。歩く雑魚のひな形に赤い角を付けた物（ZakoVariants が作る）。
/// ・ふだんは ZakoWalker のまま歩く
/// ・同じ高さの 6マス以内にプレイヤーが来ると、止まって 0.5秒赤く点滅する（ため）→ 歩く速さの 3倍で、その向きへ走る
/// ・壁・足場の端・1.6秒で止まり、0.6秒ひと休みしてから、また歩く（1.2秒は次のためをしない）
/// ・よけてから、後ろをたたく。体当たり（体の Hitbox）は今の雑魚のまま
/// ため・走る間は ZakoWalker を止めて、自分で KinematicBody2D.Move を呼ぶ（1つの物理フレームに1回だけ）。数値はすべて【仮】
/// </summary>
[RequireComponent(typeof(ZakoWalker), typeof(KinematicBody2D), typeof(Health))]
public class ZakoCharger : MonoBehaviour
{
    [SerializeField] float _chargeSpeed = 6f;
    [SerializeField] float _detectRange = 6f;
    [Tooltip("同じ高さとみなす、足元の高さの差（マス）")]
    [SerializeField] float _sameHeight = 0.9f;
    [SerializeField] float _windup = 0.5f;
    [SerializeField] float _chargeMax = 1.6f;
    [SerializeField] float _recover = 0.6f;
    [SerializeField] float _cooldown = 1.2f;
    [SerializeField] float _gravity = 40f;
    [SerializeField] float _maxFallSpeed = 18f;

    enum State { Walk, Windup, Charge, Recover }

    ZakoWalker _walker;
    KinematicBody2D _body;
    BoxCollider2D _box;
    Health _health;
    Transform _visual;
    SpriteRenderer[] _art;
    Color[] _artBase;
    State _state = State.Walk;
    float _stateAt;
    float _nextCharge;
    int _dir = 1;
    Vector2 _velocity;
    float _nextDust;
    int _groundMask;

    /// <summary>ひな形を作るときに入れる（ZakoVariants）</summary>
    public void Configure(float chargeSpeed) => _chargeSpeed = chargeSpeed;

    void Awake()
    {
        _walker = GetComponent<ZakoWalker>();
        _body = GetComponent<KinematicBody2D>();
        _box = GetComponent<BoxCollider2D>();
        _health = GetComponent<Health>();
        _visual = transform.Find("Visual");
        Transform root = _visual != null ? _visual : transform;
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.name != "Horns") list.Add(sr);
        }
        _art = list.ToArray();
        _artBase = new Color[_art.Length];
        for (int i = 0; i < _art.Length; i++) _artBase[i] = _art[i].color;
        _groundMask = LayerMask.GetMask("Ground", "OneWayPlatform");
    }

    void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _nextCharge = Time.time + 0.8f; // 出てすぐは突っ込まない
        Enter(State.Walk);
    }

    void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        SetRed(false);
    }

    void OnDamaged(DamageInfo info)
    {
        if (_health.IsDead) SetRed(false);
    }

    void Update()
    {
        if (_health.IsDead)
        {
            if (_state != State.Walk) Enter(State.Walk);
            return;
        }
        float now = Time.time;
        switch (_state)
        {
            case State.Walk:
                if (now >= _nextCharge && Detect(out int dir))
                {
                    _dir = dir;
                    Enter(State.Windup);
                }
                break;
            case State.Windup:
                SetRed(Mathf.FloorToInt((now - _stateAt) / 0.08f) % 2 == 0);
                FaceVisual();
                if (now - _stateAt >= _windup) Enter(State.Charge);
                break;
            case State.Charge:
                FaceVisual();
                if (now >= _nextDust)
                {
                    _nextDust = now + 0.08f;
                    Vector2 feet = transform.position;
                    FxAnim.Sprite(FxArt.Square(2), feet + new Vector2(-_dir * 0.35f, 0.08f), 30, 0.25f, new Color(0.8f, 0.8f, 0.82f, 0.9f), 1f, "ChargeDust")
                        .Velocity(new Vector2(-_dir * 2f, 1.2f)).FadeFrom(0.3f);
                }
                if (now - _stateAt >= _chargeMax) Enter(State.Recover);
                break;
            case State.Recover:
                if (now - _stateAt >= _recover) Enter(State.Walk);
                break;
        }
    }

    void FixedUpdate()
    {
        if (_state == State.Walk || _health.IsDead) return; // 歩く間は ZakoWalker が動かす
        float dt = Time.fixedDeltaTime;
        _velocity.x = _state == State.Charge ? _dir * _chargeSpeed * GameBuffs.ZakoSpeed : Mathf.MoveTowards(_velocity.x, 0f, 30f * dt);
        if (_state == State.Charge && _body.IsGrounded && !HasGroundAhead()) { Enter(State.Recover); _velocity.x = 0f; } // 足場の端で止まる
        _velocity.y = Mathf.Max(_velocity.y - _gravity * dt, -_maxFallSpeed);
        _body.Move(_velocity * dt);
        if (_body.IsGrounded && _velocity.y < 0f) _velocity.y = 0f;
        if (_state == State.Charge && ((_dir > 0 && _body.HitWallRight) || (_dir < 0 && _body.HitWallLeft))) Enter(State.Recover);
    }

    void Enter(State s)
    {
        _state = s;
        _stateAt = Time.time;
        switch (s)
        {
            case State.Walk:
                SetRed(false);
                if (_walker != null && !_walker.enabled)
                {
                    _walker.enabled = true;
                    _walker.SetDirection(_dir);
                }
                _nextCharge = Mathf.Max(_nextCharge, Time.time + _cooldown);
                break;
            case State.Windup:
                if (_walker != null) _walker.enabled = false;
                _velocity = Vector2.zero;
                break;
            case State.Charge:
                SetRed(true);
                StageCamera.Shake(0.03f, 0.1f);
                break;
            case State.Recover:
                SetRed(false);
                break;
        }
    }

    /// <summary>同じ高さの、決めた距離の中にプレイヤーがいるか。いれば、その向き</summary>
    bool Detect(out int dir)
    {
        dir = 1;
        Transform player = RewardKit.PlayerOf(StagePacer.Current);
        if (player == null) return false;
        Health ph = player.GetComponent<Health>();
        if (ph != null && ph.IsDead) return false;
        Vector2 me = transform.position, p = player.position;
        float dx = p.x - me.x;
        if (Mathf.Abs(dx) > _detectRange || Mathf.Abs(p.y - me.y) > _sameHeight) return false;
        dir = dx >= 0f ? 1 : -1;
        // 間に壁があれば突っ込まない
        if (Physics2D.Raycast(me + Vector2.up * 0.4f, new Vector2(dir, 0f), Mathf.Abs(dx), LayerMask.GetMask("Ground")).collider != null) return false;
        return true;
    }

    bool HasGroundAhead()
    {
        if (_box == null) return true;
        Bounds b = _box.bounds;
        var probe = new Vector2(b.center.x + _dir * (b.extents.x + 0.05f), b.min.y + 0.05f);
        return Physics2D.Raycast(probe, Vector2.down, 0.3f, _groundMask).collider != null;
    }

    void FaceVisual()
    {
        if (_visual == null) return;
        Vector3 s = _visual.localScale;
        s.x = Mathf.Abs(s.x) * _dir;
        _visual.localScale = s;
    }

    void SetRed(bool on)
    {
        if (_art == null) return;
        for (int i = 0; i < _art.Length; i++)
        {
            if (_art[i] == null) continue;
            Color c = on ? Color.Lerp(_artBase[i], FxKit.EnemyRed, 0.85f) : _artBase[i];
            c.a = _artBase[i].a;
            _art[i].color = c;
        }
    }
}
