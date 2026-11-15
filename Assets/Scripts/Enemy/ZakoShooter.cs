using UnityEngine;

/// <summary>
/// 撃つ雑魚ウイルス（仕様書 4.1）。その場に留まり、一定の間隔でプレイヤーに弾を撃つ。
/// 弾は攻撃データ（AttackData、種類 Projectile）で決める。撃つ前の予備動作の間、体が光る。
/// 倒れたら撃つのをやめる（消すのは DisableOnDeath）。
/// </summary>
[RequireComponent(typeof(AttackRunner), typeof(Health))]
public class ZakoShooter : MonoBehaviour
{
    [Tooltip("撃つ弾の攻撃データ（種類 Projectile）")]
    [SerializeField] AttackData _attack;
    [Tooltip("撃ってから次に撃つまでの秒数")]
    [SerializeField, Min(0.1f)] float _interval = 2.5f;
    [Tooltip("出てきてから最初に撃つまでの秒数")]
    [SerializeField, Min(0f)] float _firstDelay = 1.5f;
    [Tooltip("プレイヤーがこの距離より遠いと撃たない")]
    [SerializeField, Min(0f)] float _range = 14f;

    [Header("見た目")]
    [Tooltip("予備動作中に光らせる絵。空なら子のスプライトすべて")]
    [SerializeField] SpriteRenderer[] _renderers;
    [SerializeField] Color _windupColor = Color.white;
    [Tooltip("向きに合わせて左右反転させる見た目（無くてもよい）")]
    [SerializeField] Transform _visualRoot;

    AttackRunner _runner;
    Health _health;
    Color[] _baseColors;
    float _nextShotTime;
    bool _windingUp;

    /// <summary>予備動作中（光っている間）か</summary>
    public bool IsWindingUp => _windingUp;

    void Awake()
    {
        _runner = GetComponent<AttackRunner>();
        _health = GetComponent<Health>();
        if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
    }

    void OnEnable()
    {
        _nextShotTime = Time.time + _firstDelay;
        _runner.Started += OnStarted;
        _runner.Released += OnReleased;
        _health.Died += OnDied;
    }

    void OnDisable()
    {
        _runner.Started -= OnStarted;
        _runner.Released -= OnReleased;
        _health.Died -= OnDied;
        SetWindup(false);
    }

    void OnStarted(AttackData _) => SetWindup(true);
    void OnReleased(AttackData _) => SetWindup(false);

    void OnDied()
    {
        _runner.Cancel();
        SetWindup(false);
    }

    void Update()
    {
        Transform target = _runner.Target;
        if (target != null)
        {
            _runner.Facing = target.position.x >= transform.position.x ? 1 : -1;
            if (_visualRoot != null)
            {
                Vector3 s = _visualRoot.localScale;
                s.x = Mathf.Abs(s.x) * _runner.Facing;
                _visualRoot.localScale = s;
            }
        }

        if (_health.IsDead || _attack == null || target == null || _runner.IsRunning) return;
        if (Time.time < _nextShotTime) return;
        if (Vector2.Distance(target.position, transform.position) > _range) return;

        if (_runner.Run(_attack)) _nextShotTime = Time.time + _interval;
    }

    void SetWindup(bool on)
    {
        _windingUp = on;
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = on ? Color.Lerp(_baseColors[i], _windupColor, 0.7f) : _baseColors[i];
            c.a = _baseColors[i].a;
            _renderers[i].color = c;
        }
    }
}
