using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 【仮】タイトル画面：使用データの読み込み方を選んでランを始める（仕様書 8.3）。UI 担当の本番画面ができたら消す。
/// スクリーンタイムのスキャン、サンプルでの訓練、利用時間の手入力からランを始める。
/// </summary>
public class RunTitleMenu : MonoBehaviour
{
    static readonly Color Panel = Color.white;
    static readonly Color Surface = new Color32(246, 247, 249, 255);
    static readonly Color Border = new Color32(216, 220, 226, 255);
    static readonly Color Ink = new Color32(32, 37, 46, 255);
    static readonly Color Muted = new Color32(83, 93, 105, 255);
    static readonly Color Accent = new Color32(193, 22, 44, 255);
    static readonly Color AccentHover = new Color32(162, 16, 36, 255);
    static readonly Color AccentSurface = new Color32(253, 232, 236, 255);

    [Tooltip("GameSession が無いとき（このシーンを最初に開いたとき）に使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;

    bool _manual;
    bool _scan;
    bool _scanning;
    List<AppUsage> _detectedApps;
    List<(string Name, int Seconds)> _topApps;
    readonly Dictionary<AppEntry, float> _hours = new Dictionary<AppEntry, float>();
    string _error;
    Texture2D _background;
    Texture2D _scanIcon;
    Vector2 _scroll;
    GUIStyle _title;
    GUIStyle _label;
    GUIStyle _caption;
    GUIStyle _time;
    GUIStyle _rank;
    GUIStyle _errorText;
    GUIStyle _brand;
    GUIStyle _heroTitle;
    GUIStyle _heroCaption;
    GUIStyle _scanButton;
    GUIStyle _button;
    GUIStyle _primaryButton;

    GameSession Session => GameSession.Instance != null ? GameSession.Instance : GameSession.EnsureExists(_catalog);

    void OnGUI()
    {
        GameSession session = Session;
        if (session.IsTransitioning) return;
        if (_title == null) CreateStyles();

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _background, ScaleMode.StretchToFill);
        float width = Mathf.Min(624f, Screen.width - 32f);
        bool wide = width >= 520f;
        float preferredHeight = wide ? 398f : 446f;
        if (_topApps != null) preferredHeight = 424f;
        else if (_manual) preferredHeight = 324f;
        else if (_scan) preferredHeight = 264f;
        float height = Mathf.Min(preferredHeight, Screen.height - 32f);
        if (width <= 64f || height <= 100f) return;
        var frame = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        Fill(new Rect(frame.x - 3f, frame.y + 9f, width + 6f, height + 3f), new Color(0.14f, 0.16f, 0.22f, 0.12f), 11f);
        Fill(frame, Border, 10f);
        Fill(new Rect(frame.x + 1f, frame.y + 1f, width - 2f, height - 2f), Panel, 9f);
        Fill(new Rect(frame.x + 20f, frame.y + 12f, 24f, 24f), Accent, 12f);
        GUI.DrawTexture(new Rect(frame.x + 24f, frame.y + 16f, 16f, 16f), _scanIcon);
        GUI.Label(new Rect(frame.x + 52f, frame.y + 12f, width - 72f, 24f), "Team1", _brand);
        Fill(new Rect(frame.x + 1f, frame.y + 47f, width - 2f, 1f), Border);

        GUILayout.BeginArea(new Rect(frame.x + 22f, frame.y + 62f, width - 44f, height - 80f));
        _scroll = GUILayout.BeginScrollView(_scroll);
        if (_scan) DrawScanPanel(session);
        else if (_manual) DrawManualPanel(session);
        else DrawStartPanel(session, wide);

        if (!string.IsNullOrEmpty(_error))
        {
            GUILayout.Space(8f);
            GUILayout.Label(_error, _errorText);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawStartPanel(GameSession session, bool wide)
    {
        bool canScan = MacScreenTime.CanScan;
        GUILayout.Space(8f);
        GUILayout.Label(canScan ? "感染の痕跡を探す" : "駆除を開始する", _heroTitle);
        GUILayout.Label(canScan ? "先週の利用記録から、この端末の駆除対象を見つけます。" : "訓練するか、先週の利用時間を入力して駆除を始めます。", _heroCaption);
        GUILayout.Space(12f);
        if (canScan && ScanButton())
        {
            _scan = true;
            Scan(session.Catalog);
        }
        if (canScan) GUILayout.Space(13f);

        if (wide) GUILayout.BeginHorizontal();
        if (ActionButton("訓練モード", primary: !canScan)) Begin(new SampleUsageProvider());
        GUILayout.Space(8f);
        if (ActionButton("記録を手入力"))
        {
            _manual = true;
            _error = null;
            _scroll = Vector2.zero;
        }
        if (wide) GUILayout.EndHorizontal();
    }

    void DrawManualPanel(GameSession session)
    {
        GUILayout.Label("記録を手入力", _title);
        GUILayout.Label("アプリごとに、先週の利用時間を入力してください。", _caption);
        GUILayout.Space(12f);
        foreach (var app in session.Catalog != null ? session.Catalog.Apps : System.Array.Empty<AppEntry>())
        {
            if (app == null) continue;
            _hours.TryGetValue(app, out float hours);
            GUILayout.BeginHorizontal();
            GUILayout.Label(app.DisplayName, _label, GUILayout.Width(110f));
            _hours[app] = Mathf.Round(GUILayout.HorizontalSlider(hours, 0f, 40f, GUILayout.ExpandWidth(true)));
            GUILayout.Label($"{_hours[app]:0}時間", _time, GUILayout.Width(68f));
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(14f);
        GUILayout.BeginHorizontal();
        if (ActionButton("戻る", width: 100f)) ReturnToMenu();
        GUILayout.Space(8f);
        if (ActionButton("駆除を開始", primary: true)) BeginManual();
        GUILayout.EndHorizontal();
    }

    void DrawScanPanel(GameSession session)
    {
        if (_topApps == null)
        {
            GUILayout.Label(_scanning ? "スクリーンタイムを確認中" : "端末をスキャン", _title);
            GUILayout.Label("システム設定で「先週」を選び、端末スキャンの案内に進んでください。", _caption);
        }
        else
        {
            GUILayout.Label("スキャン結果", _title);
            GUILayout.Label("使用時間の上位3アプリ", _caption);
            GUILayout.Space(9f);
            for (int i = 0; i < _topApps.Count; i++)
            {
                var app = _topApps[i];
                int minutes = app.Seconds / 60;
                string duration = minutes >= 60 ? $"{minutes / 60}時間 {minutes % 60}分" : minutes > 0 ? $"{minutes}分" : $"{app.Seconds}秒";
                Rect row = GUILayoutUtility.GetRect(0f, 43f, GUILayout.ExpandWidth(true));
                Fill(row, Surface, 7f);
                float timeWidth = Mathf.Min(118f, row.width * 0.38f);
                var rankBadge = new Rect(row.x + 10f, row.y + 7f, 27f, 25f);
                Fill(rankBadge, AccentSurface, 6f);
                GUI.Label(rankBadge, $"{i + 1}", _rank);
                GUI.Label(new Rect(row.x + 45f, row.y, Mathf.Max(0f, row.width - timeWidth - 68f), 39f), app.Name, _label);
                GUI.Label(new Rect(row.xMax - timeWidth - 12f, row.y, timeWidth, 39f), duration, _time);
                float trackWidth = Mathf.Max(0f, row.width - 24f);
                Fill(new Rect(row.x + 12f, row.yMax - 6f, trackWidth, 2f), Border, 1f);
                Fill(new Rect(row.x + 12f, row.yMax - 6f, trackWidth * app.Seconds / _topApps[0].Seconds, 2f), Accent, 1f);
                GUILayout.Space(5f);
            }
            string targets = _detectedApps.Count > 0
                ? $"駆除対象   {string.Join("、", _detectedApps.Select(app => app.DisplayName))}"
                : "駆除対象のアプリは見つかりませんでした。";
            GUILayout.Space(3f);
            GUILayout.Label(targets, _caption);
        }

        if (_scanning) return;
        GUILayout.Space(12f);
        GUILayout.BeginHorizontal();
        bool back = ActionButton("戻る", width: 100f);
        GUILayout.Space(8f);
        if (_detectedApps?.Count > 0)
        {
            if (ActionButton("駆除を開始", primary: true)) Begin(new ManualUsageProvider(_detectedApps));
        }
        else if (ActionButton("もう一度スキャン", primary: true)) Scan(session.Catalog);
        GUILayout.EndHorizontal();
        if (back) ReturnToMenu();
    }

    void ReturnToMenu()
    {
        _scan = false;
        _manual = false;
        _error = null;
        _detectedApps = null;
        _topApps = null;
        _scroll = Vector2.zero;
    }

    bool ActionButton(string text, bool primary = false, float width = 0f)
    {
        var rect = width > 0f
            ? GUILayoutUtility.GetRect(width, 44f, GUILayout.ExpandWidth(false))
            : GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
        bool hover = rect.Contains(Event.current.mousePosition);
        if (primary)
        {
            Fill(rect, hover ? AccentHover : Accent, 7f);
        }
        else
        {
            Fill(rect, Border, 8f);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), rect.height - 2f), hover ? Surface : Panel, 7f);
        }
        return GUI.Button(rect, text, primary ? _primaryButton : _button);
    }

    bool ScanButton()
    {
        Rect slot = GUILayoutUtility.GetRect(0f, 142f, GUILayout.ExpandWidth(true));
        var button = new Rect(slot.center.x - 68f, slot.y, 136f, slot.height);
        var ring = new Rect(button.center.x - 56f, button.y + 2f, 112f, 112f);
        Fill(ring, AccentSurface, 56f);
        Fill(new Rect(ring.x + 8f, ring.y + 8f, 96f, 96f), button.Contains(Event.current.mousePosition) ? AccentHover : Accent, 48f);
        GUI.DrawTexture(new Rect(ring.center.x - 19f, ring.center.y - 19f, 38f, 38f), _scanIcon);
        return GUI.Button(button, "端末をスキャン", _scanButton);
    }

    void CreateStyles()
    {
        _title = TextStyle(30, Ink, bold: true);
        _label = TextStyle(16, Ink, TextAnchor.MiddleLeft, bold: true);
        _caption = TextStyle(14, Muted);
        _errorText = TextStyle(14, Accent);
        _rank = TextStyle(13, Accent, TextAnchor.MiddleCenter, bold: true);
        _time = TextStyle(15, Ink, TextAnchor.MiddleRight);
        _button = TextStyle(15, Ink, TextAnchor.MiddleCenter, bold: true);
        _primaryButton = TextStyle(16, Color.white, TextAnchor.MiddleCenter, bold: true);
        _brand = TextStyle(17, Ink, TextAnchor.MiddleLeft, bold: true);
        _heroTitle = TextStyle(30, Ink, TextAnchor.MiddleCenter, bold: true);
        _heroTitle.wordWrap = true;
        _heroCaption = TextStyle(14, Muted, TextAnchor.MiddleCenter);
        _heroCaption.wordWrap = true;
        _scanButton = TextStyle(15, Accent, TextAnchor.LowerCenter, bold: true);
        _scanButton.padding = new RectOffset(0, 0, 0, 3);
        _scanIcon = CreateScanIcon();

        const int size = 64;
        _background = new Texture2D(size, size, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        var baseColor = new Color32(233, 236, 241, 255);
        var blush = new Color32(250, 229, 233, 255);
        var light = new Color32(245, 247, 250, 255);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x / (size - 1f), y / (size - 1f));
                float rightGlow = Mathf.Exp(-4f * (point - new Vector2(0.85f, 0.95f)).sqrMagnitude);
                float leftGlow = Mathf.Exp(-7f * (point - new Vector2(0.06f, 0.1f)).sqrMagnitude);
                pixels[y * size + x] = Color.Lerp(Color.Lerp(baseColor, blush, rightGlow), light, leftGlow);
            }
        }
        _background.wrapMode = TextureWrapMode.Clamp;
        _background.SetPixels(pixels);
        _background.Apply(false, true);
    }

    static Texture2D CreateScanIcon()
    {
        const int size = 48;
        var icon = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        var lensCenter = new Vector2(20f, 28f);
        var handleStart = new Vector2(28f, 20f);
        var handle = new Vector2(11f, -11f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                float ring = Mathf.Abs((point - lensCenter).magnitude - 11f);
                float fraction = Mathf.Clamp01(Vector2.Dot(point - handleStart, handle) / handle.sqrMagnitude);
                float stem = (point - handleStart - handle * fraction).magnitude;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(2.4f - Mathf.Min(ring, stem)));
            }
        }
        icon.wrapMode = TextureWrapMode.Clamp;
        icon.SetPixels(pixels);
        icon.Apply(false, true);
        return icon;
    }

    static GUIStyle TextStyle(int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
    {
        var style = new GUIStyle
        {
            fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = alignment,
            wordWrap = alignment == TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 2, 2)
        };
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        return style;
    }

    static void Fill(Rect rect, Color color, float radius = 0f)
    {
        if (Event.current.type == EventType.Repaint && rect.width > 0f)
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
    }

    void OnDestroy()
    {
        if (_background != null) Destroy(_background);
        if (_scanIcon != null) Destroy(_scanIcon);
    }

    async void Scan(AppCatalog catalog)
    {
        _scanning = true;
        _error = null;
        _detectedApps = null;
        _topApps = null;
        try
        {
            var result = await MacScreenTime.ScanAsync();
            if (this == null || result.ExitCode == 2) return;
            if (result.ExitCode != 0)
            {
                Debug.LogWarning($"スクリーンタイムを読み取れませんでした。\n{result.Error}");
                _error = "読み取りに失敗しました。共有するウィンドウに「システム設定」を選び、もう一度試してください。";
                return;
            }

            var usages = MacScreenTime.ReadUsages(result.Output, catalog, out var topApps);
            if (topApps.Count > 0)
            {
                _detectedApps = usages;
                _topApps = topApps;
            }
            else _error = "アプリ一覧を読み取れませんでした。スクリーンタイムでアプリ一覧を表示してから、もう一度試してください。";
        }
        catch (System.Exception error)
        {
            if (this == null) return;
            Debug.LogWarning($"端末をスキャンできませんでした。\n{error.Message}");
            _error = "端末をスキャンできませんでした。「戻る」から訓練モードを選べます。";
        }
        finally
        {
            _scanning = false;
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
