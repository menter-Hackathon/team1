using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】ボス戦テストシーンの進行と、画面の仮表示。UI 担当の本番表示・ステージ進行（M5）ができたら消す。
/// ・B：ボスを切り替える（切り替えたボスは最初から）。持っている能力はそのまま
/// ・R：今のボスとプレイヤーのライフ・位置を最初に戻す（【仮】盾ウィンドウも最初の位置・耐久に戻す）
/// ・N：今のボスのゲージを +10（デバッグ用。無敵中でも増える）
/// 画面の右上にゲージと形態、形態変化中は画面の中央に「アップデート中…」のバーを出す。
/// </summary>
public class BossTestDirector : MonoBehaviour
{
    [SerializeField] BossController[] _bosses;
    [SerializeField] Transform _player;
    [Tooltip("最初に出すボスの番号")]
    [SerializeField, Min(0)] int _startIndex;
    [Tooltip("形態変化の演出中に、画面の真ん中に「アップデート中…」のバーを出すか（チャットのボスのテストシーンでは出さない。2026-11-19）")]
    [SerializeField] bool _showUpdateBar = true;

    InputAction _switch;
    InputAction _reset;
    InputAction _addGauge;
    Vector3 _playerStart;
    int _index;
    string _message;
    float _messageUntil;
    GUIStyle _style;
    GUIStyle _bigStyle;
    GUIStyle _smallStyle;

    public BossController Current => _bosses != null && _bosses.Length > 0 ? _bosses[_index] : null;

    void Awake()
    {
        _switch = new InputAction("DebugSwitchBoss", InputActionType.Button, "<Keyboard>/b");
        _reset = new InputAction("DebugResetBoss", InputActionType.Button, "<Keyboard>/r");
        _addGauge = new InputAction("DebugAddGauge", InputActionType.Button, "<Keyboard>/n");
        if (_player != null) _playerStart = _player.position;
        _index = Mathf.Clamp(_startIndex, 0, Mathf.Max(0, (_bosses?.Length ?? 1) - 1));
        if (_bosses == null) return;
        for (int i = 0; i < _bosses.Length; i++)
        {
            if (_bosses[i] != null) _bosses[i].gameObject.SetActive(i == _index);
        }
    }

    void OnEnable()
    {
        _switch.Enable();
        _reset.Enable();
        _addGauge.Enable();
        foreach (var b in _bosses) if (b != null) b.Normalized += OnNormalized;
    }

    void OnDisable()
    {
        _switch.Disable();
        _reset.Disable();
        _addGauge.Disable();
        foreach (var b in _bosses) if (b != null) b.Normalized -= OnNormalized;
    }

    void OnDestroy()
    {
        _switch.Dispose();
        _reset.Dispose();
        _addGauge.Dispose();
    }

    void Update()
    {
        if (_switch.WasPressedThisFrame()) SwitchBoss();
        if (_reset.WasPressedThisFrame()) ResetAll();
        if (_addGauge.WasPressedThisFrame() && Current != null) Current.Gauge.Add(10);
    }

    /// <summary>次のボスに切り替える</summary>
    public void SwitchBoss()
    {
        if (_bosses == null || _bosses.Length == 0) return;
        _bosses[_index].gameObject.SetActive(false);
        _index = (_index + 1) % _bosses.Length;
        _bosses[_index].gameObject.SetActive(true); // OnEnable で最初から始まる
        ResetPlayer();
    }

    /// <summary>今のボスとプレイヤーを最初に戻す</summary>
    public void ResetAll()
    {
        if (Current != null) Current.ResetBoss();
        ResetPlayer();
        foreach (var shield in FindObjectsOfType<ShieldWindow>()) shield.ResetWindow();
    }

    void ResetPlayer()
    {
        if (_player == null) return;
        if (_player.TryGetComponent(out Health health)) health.ResetHealth();
        if (_player.TryGetComponent(out PlayerController controller))
        {
            controller.CanControl = true;
            controller.SetVelocity(Vector2.zero);
        }
        if (_player.TryGetComponent(out KinematicBody2D body)) body.Warp(_playerStart);
    }

    void OnNormalized(SkillDefinition reward)
    {
        _message = reward != null ? $"正常化！ 能力「{reward.DisplayName}」を手に入れた" : "正常化！";
        _messageUntil = Time.time + 3f;
    }

    void OnGUI()
    {
        BossController boss = Current;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color.white;
            _bigStyle = new GUIStyle(_style) { fontSize = 28 };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        if (boss != null)
        {
            // ゲージ（画面の右上。左上はスキル枠の仮表示）
            float w = Mathf.Min(400f, Screen.width - 40f);
            var bar = new Rect(Screen.width - w - 12f, 34f, w, 18f);
            string state = boss.State == BossState.Normalized ? "正常化" : boss.State == BossState.Ejected ? "とどめ待ち" : $"第{boss.Phase}形態";
            GUI.Label(new Rect(bar.x, 6f, w, 26f), $"{boss.Profile.DisplayName}　ウイルス解除 {boss.Gauge.Current}/{boss.Gauge.Max}　{state}", _style);
            DrawBar(bar, boss.Gauge.Ratio, new Color(0.3f, 0.95f, 0.5f));
            // 形態変化の位置（50%）
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bar.x + bar.width * boss.Profile.Phase2Threshold - 1f, bar.y - 3f, 2f, bar.height + 6f), Texture2D.whiteTexture);

            if (_showUpdateBar && boss.State == BossState.Transition)
            {
                var r = new Rect((Screen.width - 360f) * 0.5f, Screen.height * 0.38f, 360f, 22f);
                GUI.Label(new Rect(r.x, r.y - 40f, r.width, 36f), "アップデート中…", _bigStyle);
                DrawBar(r, boss.TransitionProgress, new Color(0.35f, 0.65f, 1f));
            }
        }

        if (Time.time < _messageUntil)
        {
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f), _message, _bigStyle);
        }

        GUI.Label(new Rect(10f, Screen.height - 24f, Screen.width - 20f, 22f),
            "【仮】B：ボス切替　R：やり直し　N：ゲージ+10　J／F：近接　K：ダッシュ　1/2/3：能力　マウス：盾ウィンドウを動かす・落ちる前のタブを消す", _smallStyle);
    }

    static void DrawBar(Rect r, float ratio, Color fill)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(ratio), r.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
