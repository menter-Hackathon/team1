using UnityEngine;

/// <summary>
/// ターゲットをなめらかに追いかける2Dカメラ。向いている方向を少し先まで映す。
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] Transform _target;
    [Tooltip("ターゲットからのずらし量（キャラの足元が基準なので少し上を映す）")]
    [SerializeField] Vector2 _offset = new Vector2(0f, 1.5f);
    [Tooltip("追従の遅れ（秒）。0で完全に固定")]
    [SerializeField, Min(0f)] float _smoothTime = 0.12f;
    [Tooltip("向いている方向に先読みする距離")]
    [SerializeField] float _lookAhead = 1.5f;
    [SerializeField, Min(0f)] float _lookAheadSmoothTime = 0.3f;

    PlayerController _player;
    Vector3 _velocity;
    float _currentLookAhead;
    float _lookAheadVelocity;

    public void SetTarget(Transform target)
    {
        _target = target;
        _player = target != null ? target.GetComponent<PlayerController>() : null;
    }

    void Start()
    {
        SetTarget(_target);
        if (_target != null) transform.position = GoalPosition();
    }

    void LateUpdate()
    {
        if (_target == null) return;

        float wantLookAhead = _player != null ? _player.FacingDirection * _lookAhead : 0f;
        _currentLookAhead = Mathf.SmoothDamp(_currentLookAhead, wantLookAhead, ref _lookAheadVelocity, _lookAheadSmoothTime);

        transform.position = Vector3.SmoothDamp(transform.position, GoalPosition(), ref _velocity, _smoothTime);
    }

    Vector3 GoalPosition()
    {
        Vector3 p = _target.position;
        return new Vector3(p.x + _offset.x + _currentLookAhead, p.y + _offset.y, transform.position.z);
    }
}
