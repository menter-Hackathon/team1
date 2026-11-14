using UnityEngine;

/// <summary>
/// 被弾したときと無敵の間、見た目を点滅させる。プレイヤー・敵・ボス共通。
/// </summary>
[RequireComponent(typeof(Health))]
public class DamageBlink : MonoBehaviour
{
    [Tooltip("点滅させる絵。空なら子のスプライトをすべて点滅させる")]
    [SerializeField] SpriteRenderer[] _renderers;
    [Tooltip("点滅の間隔（秒）")]
    [SerializeField, Min(0.01f)] float _interval = 0.08f;
    [Tooltip("被弾した直後に最低限点滅する秒数（無敵時間が無い敵用）")]
    [SerializeField, Min(0f)] float _minBlinkTime = 0.15f;

    Health _health;
    float _flashUntil = float.NegativeInfinity;
    bool _visible = true;

    void Awake()
    {
        _health = GetComponent<Health>();
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    void OnEnable() => _health.Damaged += OnDamaged;

    void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        SetVisible(true);
    }

    void OnDamaged(DamageInfo _) => _flashUntil = Time.time + _minBlinkTime;

    void Update()
    {
        bool blinking = !_health.IsDead && (Time.time < _flashUntil || _health.IsInvincible);
        bool visible = !blinking || Mathf.FloorToInt(Time.time / _interval) % 2 == 1;
        if (visible != _visible) SetVisible(visible);
    }

    void SetVisible(bool visible)
    {
        _visible = visible;
        foreach (var r in _renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}
