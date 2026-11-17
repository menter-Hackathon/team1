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

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void OnGUI()
    {
        GameSession session = Session;
        if (session.IsTransitioning) return;
        PixelGui.Begin();

        AppEntry[] apps = session.Catalog != null ? session.Catalog.Apps : new AppEntry[0];
        float w = 300f;
        float contentH = _manual ? apps.Length * 30f + 44f : 3 * 36f;
        float panelH = 70f + contentH + (string.IsNullOrEmpty(_error) ? 0f : 40f) + 16f;
        float x = (PixelGui.Width - w) * 0.5f;
        float y = Mathf.Max(8f, (PixelGui.Height - panelH) * 0.5f);
        GUI.Box(new Rect(x, y, w, panelH), GUIContent.none, PixelGui.Panel);

        // タイトルと、歩くプレイヤー
        GameArt art = GameArt.Instance;
        if (art != null && art.PlayerWalk != null && art.PlayerWalk.Length > 0)
        {
            Sprite frame = art.PlayerWalk[Mathf.FloorToInt(Time.unscaledTime * 8f) % art.PlayerWalk.Length];
            PixelGui.DrawSpriteAt(x + 18f, y + 10f, frame, 2f / 3f); // 3倍の絵を 2/3 にして、1ドット＝仮想の2ドット
        }
        GUI.Label(new Rect(x, y + 12f, w, 40f), "Team1", PixelGui.Title);
        float cy = y + 70f;
        float bx = x + 20f, bw = w - 40f;

        if (!_manual)
        {
            if (GUI.Button(new Rect(bx, cy, bw, 28f), "自動", PixelGui.Button)) Begin(new FileUsageProvider());
            cy += 36f;
            if (GUI.Button(new Rect(bx, cy, bw, 28f), "サンプル", PixelGui.Button)) Begin(new SampleUsageProvider());
            cy += 36f;
            if (GUI.Button(new Rect(bx, cy, bw, 28f), "手入力", PixelGui.Button)) { _manual = true; _error = null; }
            cy += 36f;
        }
        else
        {
            foreach (var app in apps)
            {
                if (app == null) continue;
                if (!_hours.ContainsKey(app)) _hours[app] = 0f;
                GUI.Label(new Rect(bx, cy, 90f, 24f), app.DisplayName, PixelGui.Label);
                _hours[app] = Mathf.Round(GUI.HorizontalSlider(new Rect(bx + 92f, cy + 8f, bw - 92f - 44f, 16f), _hours[app], 0f, 40f));
                GUI.Label(new Rect(bx + bw - 40f, cy, 40f, 24f), $"{_hours[app]:0} h", PixelGui.Label);
                cy += 30f;
            }
            cy += 8f;
            if (GUI.Button(new Rect(bx, cy, bw * 0.5f - 4f, 28f), "戻る", PixelGui.Button)) { _manual = false; _error = null; }
            if (GUI.Button(new Rect(bx + bw * 0.5f + 4f, cy, bw * 0.5f - 4f, 28f), "開始", PixelGui.Button)) BeginManual();
            cy += 36f;
        }

        if (!string.IsNullOrEmpty(_error))
        {
            GUI.Label(new Rect(bx, cy, bw, 40f), _error, PixelGui.Small);
        }
        PixelGui.End();
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
