using UnityEngine;

/// <summary>
/// ステージのカメラ（2026-11-17：ステージの種類）。高さは固定（床の上面から画面の上端まで 11.5 マスが映る位置）。
/// ・FollowX（横スクロール）：プレイヤーを横に追いかける。ステージの左右の端より外は映さない
/// ・Rooms（ウィンドウ移動）：プレイヤーがいる部屋（ウィンドウ1枚＝1画面）の真ん中へ、スッと動く
/// ・LockTo を呼ぶと、その位置で止まる（ボス部屋に入ったとき）
/// </summary>
public class StageCamera : MonoBehaviour
{
    public enum Mode { FollowX = 0, Rooms = 1 }

    [SerializeField] Mode _mode = Mode.FollowX;
    [SerializeField] Transform _target;
    [Tooltip("カメラの真ん中が動ける範囲（横スクロール）")]
    [SerializeField] float _minX;
    [SerializeField] float _maxX;
    [Tooltip("部屋の真ん中の x（ウィンドウ移動）。部屋の幅は、となりの部屋との中間まで")]
    [SerializeField] float[] _roomCentersX;
    [Tooltip("向いている方向に、少し先まで映す（横スクロール）")]
    [SerializeField] float _lookAhead = 2f;
    [Tooltip("追いかける遅れ（秒）")]
    [SerializeField, Min(0f)] float _smoothTime = 0.15f;
    [Tooltip("部屋を移るときにかける秒数のめやす")]
    [SerializeField, Min(0f)] float _roomPanTime = 0.25f;

    PlayerController _player;
    float _velocityX;
    float _look;
    float _lookVelocity;
    bool _locked;
    float _lockX;

    /// <summary>止まっているか（ボス部屋）</summary>
    public bool IsLocked => _locked;

    /// <summary>この x で止める（ボス部屋）</summary>
    public void LockTo(float x)
    {
        _locked = true;
        _lockX = x;
    }

    void Start()
    {
        if (_target == null)
        {
            var p = FindObjectOfType<PlayerController>();
            if (p != null) _target = p.transform;
        }
        _player = _target != null ? _target.GetComponent<PlayerController>() : null;
        Vector3 pos = transform.position;
        pos.x = Goal();
        transform.position = pos;
    }

    void LateUpdate()
    {
        if (_target == null) return;
        float goal = Goal();
        float smooth = _locked || _mode == Mode.Rooms ? _roomPanTime : _smoothTime;
        Vector3 pos = transform.position;
        pos.x = Mathf.SmoothDamp(pos.x, goal, ref _velocityX, smooth);
        transform.position = pos;
    }

    float Goal()
    {
        if (_locked) return _lockX;
        float x = _target != null ? _target.position.x : transform.position.x;
        if (_mode == Mode.Rooms && _roomCentersX != null && _roomCentersX.Length > 0) return RoomCenter(x);
        float want = _player != null ? _player.FacingDirection * _lookAhead : 0f;
        _look = Mathf.SmoothDamp(_look, want, ref _lookVelocity, 0.3f);
        return Mathf.Clamp(x + _look, _minX, _maxX);
    }

    float RoomCenter(float x)
    {
        // となりの部屋の真ん中との中間で区切る
        for (int i = 0; i < _roomCentersX.Length - 1; i++)
        {
            if (x < (_roomCentersX[i] + _roomCentersX[i + 1]) * 0.5f) return _roomCentersX[i];
        }
        return _roomCentersX[_roomCentersX.Length - 1];
    }
}
