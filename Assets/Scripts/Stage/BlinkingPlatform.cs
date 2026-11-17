using UnityEngine;

/// <summary>
/// 開いたり閉じたりするウィンドウの足場（ギミック。2026-11-17：ステージの種類）。【仮】の値。
/// ・出ている間はすり抜け床として乗れる。消える少し前に点滅する
/// ・消えている間は、うすく形だけを出す（どこに出るか分かるように）。判定はない
/// 繰り返しの長さ＝出ている秒数＋消えている秒数。ずらし（秒）を変えると、並べた足場が順番に開く
/// </summary>
public class BlinkingPlatform : MonoBehaviour
{
    [SerializeField] Collider2D _collider;
    [SerializeField] SpriteRenderer[] _renderers;
    [SerializeField, Min(0.1f)] float _onTime = 2.2f;
    [SerializeField, Min(0.1f)] float _offTime = 1.4f;
    [Tooltip("消える前に点滅する秒数")]
    [SerializeField, Min(0f)] float _warnTime = 0.6f;
    [Tooltip("繰り返しのずらし（秒）")]
    [SerializeField] float _phase;
    [Tooltip("消えている間の濃さ")]
    [SerializeField, Range(0f, 1f)] float _ghostAlpha = 0.18f;

    /// <summary>今、乗れるか</summary>
    public bool IsOpen => _collider != null && _collider.enabled;

    void Update()
    {
        float cycle = _onTime + _offTime;
        float t = Mathf.Repeat(Time.time + _phase, cycle);
        bool open = t < _onTime;
        bool warn = open && t > _onTime - _warnTime;
        if (_collider != null && _collider.enabled != open) _collider.enabled = open;
        float alpha = !open ? _ghostAlpha : warn && Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0 ? 0.35f : 1f;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}
