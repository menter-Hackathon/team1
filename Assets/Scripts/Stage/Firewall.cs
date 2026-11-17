using UnityEngine;

/// <summary>
/// ファイアウォール（ギミック。2026-11-17：ステージの種類）。【仮】の値。
/// 一定の間隔でオン・オフする炎の壁。オンのときに触れるとダメージ（敵の攻撃と同じく、ライフが1減る）。
/// オンになる少し前に、うすく点滅して知らせる。上下の発生装置はいつも見える。
/// 判定は子の Hitbox（体当たりと同じ、触れている間ずっと。連続で当たらないのはプレイヤーの無敵時間のおかげ）。
/// </summary>
public class Firewall : MonoBehaviour
{
    [SerializeField] Hitbox _hitbox;
    [Tooltip("炎の絵")]
    [SerializeField] SpriteRenderer _flame;
    [SerializeField, Min(0.1f)] float _onTime = 1.6f;
    [SerializeField, Min(0.1f)] float _offTime = 1.8f;
    [Tooltip("オンになる前に知らせる秒数")]
    [SerializeField, Min(0f)] float _warnTime = 0.6f;
    [Tooltip("繰り返しのずらし（秒）")]
    [SerializeField] float _phase;

    bool _on;

    /// <summary>今、炎が出ているか</summary>
    public bool IsOn => _on;

    void Update()
    {
        float cycle = _onTime + _offTime;
        float t = Mathf.Repeat(Time.time + _phase, cycle);
        bool on = t < _onTime;
        bool warn = !on && t > cycle - _warnTime;
        if (on != _on)
        {
            _on = on;
            if (_hitbox != null)
            {
                if (on) _hitbox.Activate();
                else _hitbox.Deactivate();
            }
        }
        if (_flame == null) return;
        _flame.enabled = on || warn;
        Color c = _flame.color;
        c.a = on ? 1f : (Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 ? 0.35f : 0.1f);
        _flame.color = c;
    }
}
