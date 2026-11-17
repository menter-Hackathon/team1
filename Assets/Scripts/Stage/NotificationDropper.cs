using UnityEngine;

/// <summary>
/// 降ってくる通知（ギミック。2026-11-17：ステージの種類）。【仮】の値。
/// プレイヤーが範囲（x）の中にいる間、一定の間隔で、プレイヤーの近くの画面の上に通知を出し、少し待ってから落とす。
/// 落ちる場所の床には、出たときから影が出る（予告）。当たるとダメージ。床に着いたら消える。
/// 通知そのものは、ボスの落下物と同じ部品（FallingObject）と攻撃データを使う。
/// </summary>
[RequireComponent(typeof(AttackRunner))]
public class NotificationDropper : MonoBehaviour
{
    [Tooltip("通知の攻撃データ（種類 Spawn：大きさ・落ちる速さ・予告の秒数・ダメージ）")]
    [SerializeField] AttackData _attack;
    [Tooltip("プレイヤーがこの範囲（x）にいる間だけ落とす")]
    [SerializeField] float _minX;
    [SerializeField] float _maxX;
    [Tooltip("落とす間隔（秒）")]
    [SerializeField, Min(0.2f)] float _interval = 2.2f;
    [Tooltip("プレイヤーからずらす量の最大（マス）")]
    [SerializeField, Min(0f)] float _spread = 1.5f;
    [Tooltip("通知を出す高さ（中心）")]
    [SerializeField] float _hangY = 10.8f;

    AttackRunner _runner;
    Transform _player;
    float _next;

    void Awake() => _runner = GetComponent<AttackRunner>();

    void Start()
    {
        var p = FindObjectOfType<PlayerController>();
        if (p != null) _player = p.transform;
        _next = Time.time + 1f;
    }

    void Update()
    {
        if (_attack == null || _player == null) return;
        float px = _player.position.x;
        if (px < _minX || px > _maxX)
        {
            _next = Mathf.Max(_next, Time.time + 0.5f); // 範囲に入ってすぐは落とさない
            return;
        }
        if (Time.time < _next) return;
        _next = Time.time + _interval;
        float x = Mathf.Clamp(px + Random.Range(-_spread, _spread), _minX, _maxX);
        float now = Time.time;
        FallingObject.Spawn(_runner, _attack, new Vector2(x, _hangY), now, now + _attack.TelegraphTime, showShadow: true);
    }
}
