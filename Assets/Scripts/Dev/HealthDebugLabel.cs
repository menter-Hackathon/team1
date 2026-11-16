using UnityEngine;

/// <summary>
/// 【仮】HP・ライフを頭の上に文字で表示する確認用。UI 担当の本番表示ができたら消す。
/// プレイヤーに付けた場合、ライフ0で「GAME OVER（仮）」を表示する。
/// </summary>
public class HealthDebugLabel : MonoBehaviour
{
    [SerializeField] Health _health;
    [SerializeField] string _label = "HP";
    [SerializeField] Vector3 _offset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] bool _showGameOver;

    GUIStyle _style;
    GUIStyle _bigStyle;

    void Awake()
    {
        if (_health == null) _health = GetComponent<Health>();
    }

    void OnGUI()
    {
        Camera cam = Camera.main;
        if (cam == null || _health == null) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color.white;
            _bigStyle = new GUIStyle(_style) { fontSize = 40 };
        }

        Vector3 sp = cam.WorldToScreenPoint(transform.position + _offset);
        if (sp.z > 0f)
        {
            string text = _health.IsDead ? $"{_label} 0/{_health.Max}" : $"{_label} {_health.Current}/{_health.Max}";
            GUI.Label(new Rect(sp.x - 80f, Screen.height - sp.y - 14f, 160f, 28f), text, _style);
        }

        if (_showGameOver && _health.IsDead)
        {
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "GAME OVER（仮）\n<size=20>再生を止めてもう一度押すとやり直せます</size>", _bigStyle);
        }
    }
}
