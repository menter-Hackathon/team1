using UnityEngine;

/// <summary>出した人（ボスの AttackRunner）を受け取る部品（2026-11-19）。AttackRunner が雑魚をプレハブから出したときに渡す</summary>
public interface ISummonedBy
{
    void SetSummoner(AttackRunner summoner);
}

/// <summary>
/// 雑魚を出し続けるウィンドウ（2026-11-19。ブラウザのボスの大技。数値・見た目は【仮】）。プレハブに付け、ボスが召喚（Minion）で置く。
/// ・壊されるまで（Health が0になるまで）、決まった間隔で雑魚（プレハブ）を出し続ける。同時にいられる数まで
/// ・出した雑魚はボスの出した物として登録する（ボスが正常化したら一緒に消える）
/// ・出すときは、ウィンドウが少し光り、下から雑魚が出てくる
/// 固さは Health の最大値（プレハブで決める）。被弾範囲は子の Hurtbox
/// </summary>
[RequireComponent(typeof(Health))]
public class SpawnerWindow : MonoBehaviour, ISummonedBy
{
    [Tooltip("出す雑魚のプレハブ")]
    [SerializeField] GameObject _minion;
    [Tooltip("雑魚を出す間隔（秒）")]
    [SerializeField, Min(0.2f)] float _interval = 3f;
    [Tooltip("置かれてから、1体目を出すまでの秒数")]
    [SerializeField, Min(0f)] float _firstDelay = 1.2f;
    [Tooltip("このウィンドウから出た雑魚が、同時にいられる数")]
    [SerializeField, Min(1)] int _maxAlive = 4;
    [Tooltip("光らせる絵（ウィンドウ）")]
    [SerializeField] SpriteRenderer _art;

    Health _health;
    AttackRunner _summoner;
    float _next;
    float _flashUntil;
    readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    public void SetSummoner(AttackRunner summoner) => _summoner = summoner;

    void Awake() => _health = GetComponent<Health>();

    void OnEnable() => _next = Time.time + _firstDelay;

    /// <summary>ウィンドウの大きさ（絵の見た目の大きさ。絵が無ければ 2.6×1.9）</summary>
    Vector2 Size => _art != null && _art.sprite != null ? (Vector2)_art.bounds.size : new Vector2(2.6f, 1.9f);

    void Start()
    {
        // 置かれた所が床や壁に近くても、ウィンドウ全体が部屋の中に収まるように寄せる（床に埋まらない）
        Rect arena = BattleArena.GetBounds();
        Vector2 half = Size * 0.5f;
        Vector3 p = transform.position;
        p.x = arena.width > half.x * 2f ? Mathf.Clamp(p.x, arena.xMin + half.x, arena.xMax - half.x) : arena.center.x;
        p.y = arena.height > half.y * 2f ? Mathf.Clamp(p.y, arena.yMin + half.y + 0.2f, arena.yMax - half.y) : arena.center.y;
        transform.position = p;
    }

    void Update()
    {
        if (_health.IsDead || _minion == null) return;
        if (_art != null) _art.color = Time.time < _flashUntil ? new Color(1f, 0.75f, 0.75f) : Color.white;
        if (Time.time < _next) return;
        _next = Time.time + _interval;
        _spawned.RemoveAll(g => g == null || !g.activeInHierarchy || (g.TryGetComponent(out Health h) && h.IsDead));
        if (_spawned.Count >= _maxAlive) return;

        // ウィンドウの下から出す（部屋の中に収める）
        Rect arena = BattleArena.GetBounds();
        Vector2 size = Size;
        Vector2 at = (Vector2)transform.position + new Vector2(Random.Range(-0.3f, 0.3f) * size.x, -size.y * 0.5f + 0.5f);
        at.x = Mathf.Clamp(at.x, arena.xMin + 0.6f, arena.xMax - 0.6f);
        GameObject go = Instantiate(_minion, at, Quaternion.identity);
        go.name = _minion.name;
        go.SetActive(true);
        if (_summoner != null) _summoner.Register(go);
        _spawned.Add(go);
        _flashUntil = Time.time + 0.15f;
        Vfx.SmokeSmall(at + Vector2.up * 0.3f);
    }
}
