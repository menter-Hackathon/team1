using UnityEngine;

/// <summary>
/// 倒れたときに、ウイルスの煙を出す（雑魚・プレイヤー）。
/// 「消す絵」を入れると、倒れたあとはその絵を出さない（プレイヤーが煙になって消える）。
/// </summary>
[RequireComponent(typeof(Health))]
public class DeathVfx : MonoBehaviour
{
    [Tooltip("大きい煙にするか（オフなら小さい煙）")]
    [SerializeField] bool _big;
    [Tooltip("煙を出す位置（足元からのずれ）")]
    [SerializeField] Vector2 _offset = new Vector2(0f, 0.5f);
    [Tooltip("倒れたら見えなくする絵（空なら何も消さない）")]
    [SerializeField] SpriteRenderer[] _hide;

    Health _health;

    void Awake() => _health = GetComponent<Health>();
    void OnEnable() => _health.Died += OnDied;
    void OnDisable() => _health.Died -= OnDied;

    void OnDied()
    {
        Vector2 pos = (Vector2)transform.position + _offset;
        if (_big) Vfx.Smoke(pos);
        else Vfx.SmokeSmall(pos);
    }

    bool _hidden;

    void LateUpdate()
    {
        // 被弾の点滅（DamageBlink）が絵を戻すことがあるので、倒れている間は毎フレーム消しておく。
        // テストシーンのやり直しなどでライフが戻ったら、また出す
        if (_hide == null || _hide.Length == 0) return;
        bool dead = _health.IsDead;
        if (!dead && !_hidden) return;
        foreach (var r in _hide)
        {
            if (r != null) r.enabled = !dead;
        }
        _hidden = dead;
    }
}
