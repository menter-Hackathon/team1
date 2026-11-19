using UnityEngine;

/// <summary>
/// 【仮】雑魚戦テストシーンの画面表示。UI 担当の本番表示・ステージ進行（M5）ができたら消す。
/// ・雑魚戦の間：画面の上に「雑魚ウイルス 残り N/M」
/// ・ボスが出たら：ボスのウイルス解除ゲージと形態
/// ・ボス出現、正常化（ステージクリア）のときにメッセージ
/// やり直すときは、再生を止めてもう一度押す。
/// </summary>
public class StageTestDirector : MonoBehaviour
{
    [SerializeField] EnemyWave _wave;
    [SerializeField] BossEntrance _entrance;

    string _message;
    float _messageUntil;
    bool _cleared;
    GUIStyle _style;
    GUIStyle _bigStyle;
    GUIStyle _smallStyle;

    BossController Boss => _entrance != null ? _entrance.Boss : null;

    void Start() => GameAudio.StageStarted(); // ステージの曲（ボスが出たら BossEntrance がボスの曲にする）

    void OnEnable()
    {
        if (_entrance != null) _entrance.Appearing += OnBossAppearing;
        if (Boss != null) Boss.Normalized += OnNormalized;
    }

    void OnDisable()
    {
        if (_entrance != null) _entrance.Appearing -= OnBossAppearing;
        if (Boss != null) Boss.Normalized -= OnNormalized;
    }

    void OnBossAppearing(BossController boss) => Show($"{boss.Profile.DisplayName} が出現！", 2f);

    void OnNormalized(SkillDefinition reward)
    {
        _cleared = true;
        Show(reward != null ? $"ステージクリア！ 能力「{reward.DisplayName}」を手に入れた" : "ステージクリア！", 4f);
    }

    void Show(string message, float seconds)
    {
        _message = message;
        _messageUntil = Time.time + seconds;
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color.white;
            _bigStyle = new GUIStyle(_style) { fontSize = 28 };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        float w = Mathf.Min(400f, Screen.width - 40f);
        var bar = new Rect(Screen.width - w - 12f, 34f, w, 18f);
        BossController boss = Boss;

        if (_entrance != null && _entrance.HasAppeared && boss != null)
        {
            string state = boss.State == BossState.Normalized ? "正常化" : boss.State == BossState.Ejected ? "とどめ待ち" : $"第{boss.Phase}形態";
            GUI.Label(new Rect(bar.x, 6f, w, 26f), $"{boss.Profile.DisplayName}　ウイルス解除 {boss.Gauge.Current}/{boss.Gauge.Max}　{state}", _style);
            DrawBar(bar, boss.Gauge.Ratio, new Color(0.3f, 0.95f, 0.5f));
            if (boss.State == BossState.Transition)
            {
                var r = new Rect((Screen.width - 360f) * 0.5f, Screen.height * 0.38f, 360f, 22f);
                GUI.Label(new Rect(r.x, r.y - 40f, r.width, 36f), "アップデート中…", _bigStyle);
                DrawBar(r, boss.TransitionProgress, new Color(0.35f, 0.65f, 1f));
            }
        }
        else if (_wave != null)
        {
            GUI.Label(new Rect(bar.x, 6f, w, 26f), $"雑魚ウイルス　残り {_wave.Remaining}/{_wave.Total}", _style);
        }

        if (Time.time < _messageUntil)
        {
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f), _message, _bigStyle);
        }
        else if (_cleared)
        {
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f), "ステージクリア（仮）", _bigStyle);
        }

        GUI.Label(new Rect(10f, Screen.height - 24f, Screen.width - 20f, 22f),
            "【仮】J／F：近接　K／左Shift：ダッシュ　1/2/3：能力　マウス：盾ウィンドウを動かす・落ちる前のタブを消す　（やり直すときは再生し直す）", _smallStyle);
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
