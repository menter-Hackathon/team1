using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】タイトル画面：使用データの読み込み方を選んでランを始める（仕様書 8.3）。UI 担当の本番画面ができたら消す。
/// ・自動：usage.json を読む　・サンプル：同梱のデータ　・手入力：対応アプリごとに使用時間（過去7日間の合計）を入れる
/// 自動で対応アプリが見つからなければ、理由を出して、サンプルか手入力を選んでもらう（仕様書 2.3）。
/// </summary>
public class RunTitleMenu : MonoBehaviour
{
    [Tooltip("GameSession が無いとき（このシーンを最初に開いたとき）に使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;

    bool _manual;
    readonly Dictionary<AppEntry, float> _hours = new Dictionary<AppEntry, float>();
    string _error;
    GUIStyle _title;
    GUIStyle _label;
    GUIStyle _button;

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void OnGUI()
    {
        GameSession session = Session;
        if (session.IsTransitioning) return;
        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _label.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        float w = 420f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;
        GUI.Label(new Rect(0f, y, Screen.width, 60f), "Team1", _title);
        y += 90f;

        if (!_manual)
        {
            if (GUI.Button(new Rect(x, y, w, 44f), "自動", _button)) Begin(new FileUsageProvider());
            y += 54f;
            if (GUI.Button(new Rect(x, y, w, 44f), "サンプル", _button)) Begin(new SampleUsageProvider());
            y += 54f;
            if (GUI.Button(new Rect(x, y, w, 44f), "手入力", _button)) { _manual = true; _error = null; }
            y += 54f;
        }
        else
        {
            foreach (var app in session.Catalog != null ? session.Catalog.Apps : new AppEntry[0])
            {
                if (app == null) continue;
                if (!_hours.ContainsKey(app)) _hours[app] = 0f;
                GUI.Label(new Rect(x, y, 160f, 30f), app.DisplayName, _label);
                _hours[app] = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 160f, y + 10f, 180f, 20f), _hours[app], 0f, 40f));
                GUI.Label(new Rect(x + 350f, y, 70f, 30f), $"{_hours[app]:0} h", _label);
                y += 38f;
            }
            y += 10f;
            if (GUI.Button(new Rect(x, y, w * 0.5f - 5f, 44f), "戻る", _button)) { _manual = false; _error = null; }
            if (GUI.Button(new Rect(x + w * 0.5f + 5f, y, w * 0.5f - 5f, 44f), "開始", _button)) BeginManual();
            y += 54f;
        }

        if (!string.IsNullOrEmpty(_error))
        {
            GUI.Label(new Rect(x - 100f, y + 10f, w + 200f, 60f), _error, _label);
        }
    }

    void BeginManual()
    {
        var usages = new List<AppUsage>();
        foreach (var pair in _hours)
        {
            if (pair.Value <= 0f || pair.Key.BundleIdPrefixes == null || pair.Key.BundleIdPrefixes.Length == 0) continue;
            usages.Add(new AppUsage(pair.Key.BundleIdPrefixes[0], pair.Key.DisplayName, pair.Value * 3600.0));
        }
        Begin(new ManualUsageProvider(usages));
    }

    void Begin(IAppUsageProvider provider)
    {
        if (!Session.StartRun(provider, out string error)) _error = error;
    }
}
