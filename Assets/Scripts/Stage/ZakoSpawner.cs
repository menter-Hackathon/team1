using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雑魚ウイルスを出し続ける（2026-11-17 まさぴ〜決定：雑魚は近接1回で倒れる。その代わり、数と増え方で難しさを保つ）。
/// ・ひな形（非表示の雑魚）を複製して、画面の中（または決めた範囲）の床の上に、煙と一緒に出す。プレイヤーのすぐ近くには出さない
/// ・時間がたつほど、出る間隔が短くなり、同時にいられる数が増える（増え続ける）
/// ・倒す数（ノルマ）があるとき：その数を出し終えるまで、部屋の雑魚の集まり（EnemyWave）をクリアにしない（1画面のステージ：ノルマを倒すとボス）
/// ・ノルマが無いとき：ボスが出てくるまで出し続ける（横スクロール・ウィンドウ移動の道中）。ボス部屋の中には出さない
/// ・ボスの登場が始まったら止める（ボス戦には雑魚を足さない）
/// ・同時にいられる数は、画面の近くにいる雑魚だけで数える。ノルマの無いステージでは、画面から遠く離れた雑魚・穴に落ちた雑魚は消す
///   （道中で置き去りにした雑魚で、出すのが止まらないように）
/// 数値はすべて【仮】。
/// </summary>
public class ZakoSpawner : MonoBehaviour
{
    [Tooltip("複製する雑魚（非表示で置いておく）")]
    [SerializeField] GameObject[] _templates;
    [Tooltip("それぞれのひな形が選ばれる重み")]
    [SerializeField] float[] _weights;
    [SerializeField] Transform _player;

    [Header("ノルマ（1画面のステージ）")]
    [Tooltip("出した雑魚を数える集まり。ノルマを出し終えるまで、この集まりをクリアにしない")]
    [SerializeField] EnemyWave _wave;
    [Tooltip("出す数（0＝ボスが出るまで出し続ける）")]
    [SerializeField, Min(0)] int _quota;

    [Header("増え方")]
    [Tooltip("最初に出すまでの秒数")]
    [SerializeField, Min(0f)] float _startDelay = 1.5f;
    [Tooltip("出す間隔（秒）：最初 → いちばん短く")]
    [SerializeField] Vector2 _interval = new Vector2(3f, 1.2f);
    [Tooltip("同時にいられる数：最初 → いちばん多く")]
    [SerializeField] Vector2Int _alive = new Vector2Int(3, 6);
    [Tooltip("いちばん短く・多くなるまでの秒数")]
    [SerializeField, Min(1f)] float _rampSeconds = 60f;

    [Header("出す場所")]
    [Tooltip("出す範囲（ワールド座標）。大きさ0なら、カメラに映っている範囲")]
    [SerializeField] Rect _area;
    [Tooltip("プレイヤーから、これより近くには出さない（マス）")]
    [SerializeField, Min(0f)] float _minDistance = 4f;
    [Tooltip("ボス部屋（BattleArena）の中には出さない（道中のステージ用）")]
    [SerializeField] bool _avoidBossRoom = true;
    [SerializeField] LayerMask _groundMask;

    readonly List<Health> _spawnedAlive = new List<Health>();
    BossEntrance _entrance;
    Health _playerHealth;
    float _startedAt;
    float _next;
    int _spawned;
    bool _holding;
    bool _stopped;

    /// <summary>これまでに出した数</summary>
    public int Spawned => _spawned;
    /// <summary>今いる、出した雑魚の数（画面の近くにいるものだけ）</summary>
    public int Alive { get { Purge(); return _spawnedAlive.Count; } }

    /// <summary>画面からこれ以上離れた雑魚は数えない・消す（マス）</summary>
    const float FarMargin = 6f;

    void Awake()
    {
        if (_templates != null)
            foreach (var t in _templates) if (t != null) t.SetActive(false);
        if (_wave != null && _quota > 0)
        {
            _wave.Hold(); // ノルマを出し終えるまで、ボスを出さない
            _wave.Expect(_quota); // 画面右上の「ボスが出るまでのゲージ」に、出す予定の数も入れる
            _holding = true;
        }
    }

    void Start()
    {
        _entrance = FindObjectOfType<BossEntrance>();
        if (_player != null) _playerHealth = _player.GetComponent<Health>();
        _startedAt = Time.time;
        _next = Time.time + _startDelay;
    }

    void Update()
    {
        if (_stopped) return;
        if (_entrance != null && _entrance.IsTriggered) { Stop(); return; }
        if (_quota > 0 && _spawned >= _quota) { Release(); return; }
        if (_player == null || (_playerHealth != null && _playerHealth.IsDead)) return;
        if (Time.time < _next) return;

        float t = Mathf.Clamp01((Time.time - _startedAt) / _rampSeconds);
        int cap = Mathf.RoundToInt(Mathf.Lerp(_alive.x, _alive.y, t));
        if (Alive >= cap) { _next = Time.time + 0.3f; return; }
        bool ok = TrySpawn();
        _next = Time.time + (ok ? Mathf.Lerp(_interval.x, _interval.y, t) : 0.3f);
    }

    void Stop()
    {
        _stopped = true;
        Release();
    }

    void Release()
    {
        if (!_holding) return;
        _holding = false;
        _wave.Release();
    }

    void Purge()
    {
        _spawnedAlive.RemoveAll(h => h == null || h.IsDead);
        if (_quota > 0 || _area.width > 0f) return; // 1画面のステージ（ノルマあり・範囲あり）は、雑魚が部屋から出ないので消さない
        Rect view = CameraView();
        for (int i = _spawnedAlive.Count - 1; i >= 0; i--)
        {
            Vector2 p = _spawnedAlive[i].transform.position;
            bool below = p.y < view.yMin - 3f;
            bool far = p.x < view.xMin - FarMargin || p.x > view.xMax + FarMargin;
            if (!below && !far) continue;
            Destroy(_spawnedAlive[i].gameObject); // 置き去り・穴に落ちた雑魚は消す
            _spawnedAlive.RemoveAt(i);
        }
    }

    bool TrySpawn()
    {
        GameObject template = Pick();
        if (template == null) return false;
        Rect view = _area.width > 0f ? _area : CameraView();
        Rect arena = BattleArena.GetBounds();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Random.Range(view.xMin + 1f, view.xMax - 1f);
            if (Mathf.Abs(x - _player.position.x) < _minDistance) continue;
            var from = new Vector2(x, view.yMax - 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, view.height, _groundMask);
            if (hit.collider == null || hit.distance < 0.2f) continue; // 床が無い（穴）・壁の中
            Vector2 feet = hit.point;
            if (_avoidBossRoom && arena.Contains(feet + Vector2.up * 0.5f)) continue;
            if (Vector2.Distance(feet, _player.position) < _minDistance) continue;

            GameObject go = Instantiate(template, feet, Quaternion.identity);
            go.name = template.name.Replace("Template_", "") + "_" + (_spawned + 1);
            go.SetActive(true);
            if (go.TryGetComponent(out ZakoWalker walker)) walker.SetDirection(_player.position.x >= feet.x ? 1 : -1);
            Vfx.Smoke(feet + Vector2.up * 0.4f);
            if (go.TryGetComponent(out Health health))
            {
                _spawnedAlive.Add(health);
                if (_wave != null && _quota > 0) _wave.AddEnemy(health);
            }
            _spawned++;
            return true;
        }
        return false;
    }

    GameObject Pick()
    {
        if (_templates == null || _templates.Length == 0) return null;
        float total = 0f;
        for (int i = 0; i < _templates.Length; i++) total += Weight(i);
        float r = Random.value * total;
        for (int i = 0; i < _templates.Length; i++)
        {
            r -= Weight(i);
            if (r <= 0f) return _templates[i];
        }
        return _templates[_templates.Length - 1];
    }

    float Weight(int i) => _weights != null && i < _weights.Length ? Mathf.Max(0f, _weights[i]) : 1f;

    static Rect CameraView()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return new Rect(-10f, 0f, 20f, 11f);
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
    }
}
