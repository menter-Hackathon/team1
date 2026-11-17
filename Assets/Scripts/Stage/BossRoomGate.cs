using UnityEngine;

/// <summary>
/// ボス部屋の入口（2026-11-17：ステージの種類）。横スクロール・ウィンドウ移動のステージで使う。
/// プレイヤーが入口の線（x）を越えたら：入口をふさぐ壁を出す → カメラをボス部屋で止める → ボスの登場を始める。
/// 道中の雑魚は、全部倒さなくてよい。
/// </summary>
public class BossRoomGate : MonoBehaviour
{
    [SerializeField] BossEntrance _entrance;
    [SerializeField] StageCamera _camera;
    [Tooltip("入ったら出す壁（最初は非表示）")]
    [SerializeField] GameObject _wall;
    [Tooltip("プレイヤーがこの x より右に来たら、ボス部屋に入ったとする")]
    [SerializeField] float _triggerX;
    [Tooltip("カメラを止める x（ボス部屋の真ん中）")]
    [SerializeField] float _lockCameraX;

    Transform _player;
    bool _closed;

    /// <summary>ボス部屋に入ったか</summary>
    public bool IsClosed => _closed;

    void Start()
    {
        var p = FindObjectOfType<PlayerController>();
        if (p != null) _player = p.transform;
        if (_wall != null) _wall.SetActive(false);
    }

    void Update()
    {
        if (_closed || _player == null || _player.position.x < _triggerX) return;
        _closed = true;
        if (_wall != null)
        {
            _wall.SetActive(true);
            Vfx.SmokeSmall(_wall.transform.position);
        }
        if (_camera != null) _camera.LockTo(_lockCameraX);
        if (_entrance != null) _entrance.Trigger();
    }
}
