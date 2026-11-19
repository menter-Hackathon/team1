using UnityEngine;

/// <summary>
/// 【仮】とぶ雑魚（2026-11-19。docs/stage-pacing-design.md 5章。担当3）。歩く雑魚のひな形に羽を付けた物（ZakoVariants が作る。ZakoWalker は止めてある）。
/// ・出た場所から、体のまん中が床の 2.5〜3.5マス上になるまで、ふわっと上がる
/// ・上下にゆれながら、横に往復する（出た場所の ±5マス・画面の中。壁で折り返す）。弾は撃たない
/// ・近接はジャンプして当てる（ジャンプの高さ 3.2マスで届く高さ）。能力でも落とせる
/// ・体当たり（体の Hitbox）は今の雑魚のまま
/// 重力は使わない（KinematicBody2D.Move は呼ばず、Rigidbody2D を直接動かす）。数値はすべて【仮】
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class ZakoFlyer : MonoBehaviour
{
    [SerializeField] float _speed = 2f;
    [Tooltip("床から体のまん中までの高さ（マス）")]
    [SerializeField] Vector2 _height = new Vector2(2.5f, 3.5f);
    [SerializeField] float _bob = 0.3f;
    [SerializeField] float _bobPeriod = 1.6f;
    [SerializeField] float _patrol = 5f;
    [SerializeField] float _riseTime = 0.6f;
    [SerializeField] SpriteRenderer _wingL;
    [SerializeField] SpriteRenderer _wingR;
    [SerializeField] Sprite _wingUp;
    [SerializeField] Sprite _wingDown;

    const float BodyHalf = 0.4f; // 足元から体のまん中まで

    Health _health;
    Rigidbody2D _rb;
    Transform _visual;
    int _dir = 1;
    float _startY;
    float _baseY;
    float _minX;
    float _maxX;
    float _born;
    float _phase;
    float _pauseUntil;
    float _knockX;
    bool _ready;
    int _groundMask;

    /// <summary>ひな形を作るときに入れる（ZakoVariants）</summary>
    public void Configure(float speed, SpriteRenderer wingL, SpriteRenderer wingR, Sprite wingUp, Sprite wingDown)
    {
        _speed = speed;
        _wingL = wingL;
        _wingR = wingR;
        _wingUp = wingUp;
        _wingDown = wingDown;
    }

    void Awake()
    {
        _health = GetComponent<Health>();
        _rb = GetComponent<Rigidbody2D>();
        _visual = transform.Find("Visual");
        _groundMask = LayerMask.GetMask("Ground");
    }

    void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _born = Time.time;
        _ready = false;
    }

    void OnDisable() => _health.Damaged -= OnDamaged;

    void Start()
    {
        Vector2 feet = transform.position;
        // 向き：出したときに ZakoSpawner が入れた向き（プレイヤーの方）
        ZakoWalker walker = GetComponent<ZakoWalker>();
        _dir = walker != null ? walker.Direction : (Random.value < 0.5f ? -1 : 1);

        // 床の高さ（空中に出たときは、下の床）
        float floor = feet.y;
        RaycastHit2D hit = Physics2D.Raycast(feet + Vector2.up * 0.3f, Vector2.down, 20f, LayerMask.GetMask("Ground", "OneWayPlatform"));
        if (hit.collider != null) floor = hit.point.y;

        Rect view = ScreenUi.ViewRect();
        float centerY = floor + Random.Range(_height.x, _height.y);
        centerY = Mathf.Min(centerY, view.yMax - 2.4f); // 上の HUD にかくれない
        centerY = Mathf.Max(centerY, floor + 1.4f);
        _baseY = centerY - BodyHalf;
        _startY = feet.y;

        _minX = Mathf.Max(feet.x - _patrol, view.xMin + 0.8f);
        _maxX = Mathf.Min(feet.x + _patrol, view.xMax - 0.8f);
        if (_minX > _maxX) _minX = _maxX = Mathf.Clamp(feet.x, view.xMin + 0.8f, view.xMax - 0.8f);
        _phase = Random.value * _bobPeriod;
        _ready = true;
    }

    void OnDamaged(DamageInfo info)
    {
        _pauseUntil = Time.time + 0.2f;
        _knockX = info.Knockback.x * 0.5f;
    }

    void FixedUpdate()
    {
        if (!_ready || _health.IsDead) return;
        float dt = Time.fixedDeltaTime;
        Vector2 p = _rb.position;
        float x = p.x;
        if (Time.time < _pauseUntil)
        {
            x += _knockX * dt;
            _knockX = Mathf.MoveTowards(_knockX, 0f, 20f * dt);
        }
        else
        {
            x += _dir * _speed * GameBuffs.ZakoSpeed * dt; // 2026-11-19：雑魚の速さの倍率
            bool wall = Physics2D.Raycast(new Vector2(p.x, p.y + BodyHalf), new Vector2(_dir, 0f), 0.55f, _groundMask).collider != null;
            if ((_dir > 0 && x >= _maxX) || (_dir < 0 && x <= _minX) || wall) _dir = -_dir;
        }
        x = Mathf.Clamp(x, Mathf.Min(_minX, p.x), Mathf.Max(_maxX, p.x));

        float k = Mathf.Clamp01((Time.time - _born) / _riseTime);
        k = 1f - (1f - k) * (1f - k);
        float bob = Mathf.Sin((Time.time + _phase) * Mathf.PI * 2f / _bobPeriod) * _bob * k;
        float y = Mathf.Lerp(_startY, _baseY, k) + bob;
        _rb.MovePosition(new Vector2(x, y));
    }

    void Update()
    {
        if (_visual != null)
        {
            Vector3 s = _visual.localScale;
            s.x = Mathf.Abs(s.x) * _dir;
            _visual.localScale = s;
        }
        if (_health.IsDead) return;
        // 羽ばたき（0.12秒ごとに上げ下げ）
        bool up = Mathf.FloorToInt((Time.time + _phase) / 0.12f) % 2 == 0;
        Sprite wing = up ? _wingUp : _wingDown;
        if (wing == null) return;
        if (_wingL != null) _wingL.sprite = wing;
        if (_wingR != null) _wingR.sprite = wing;
    }
}
