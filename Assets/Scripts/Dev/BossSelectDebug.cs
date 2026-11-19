using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【確認用・仮】遊ぶボスを選んで、そのボスのステージだけを遊ぶ（2026-11-18：まさぴ〜が手で試すため。試し終わったら消す）。
/// ・エディタと開発用のビルドだけで出る（本番のビルドでは出ない）
/// ・タイトルの画面の左下に「【確認用】ボスを選んで遊ぶ」と、Finder・Premiere Pro・Terminal のボタン
/// ・押すと、そのアプリだけを使った想定でランを作り、ステージをそのボスだけにしぼる（能力えらびのあと、すぐそのボスのステージ）
/// ・確認用のランの間は、図鑑（app_dex.json）に書かない。タイトルに戻ったら元に戻す
/// 使っている入口：GameSession.StageFilter、AppDex.Suspended（どちらも確認用）
/// 2026-11-19（アプリのキット）：Chrome・VS Code と、キットのアプリ全部（対応アプリの表の「代用ボスの専用の技」・能力の表の
/// 「アプリ専用の能力」の行、AppKits に登録したアプリ）を自動で並べる。小さいボタンの格子で、多いときは < > でページを送る
/// </summary>
public class BossSelectDebug : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (FindObjectOfType<BossSelectDebug>() != null) return;
        var go = new GameObject("BossSelectDebug");
        DontDestroyOnLoad(go);
        go.AddComponent<BossSelectDebug>();
    }

    /// <summary>そのアプリ1つだけを使った想定の使用データ</summary>
    class Provider : IAppUsageProvider
    {
        readonly AppUsage _app;
        public Provider(AppUsage app) { _app = app; }
        public string Label => "確認用";
        public bool TryLoad(out List<AppUsage> usages, out string error)
        {
            usages = new List<AppUsage> { _app };
            error = null;
            return true;
        }
    }

    struct Choice
    {
        public string Label, BundleId, Name;
        public bool Final;
    }

    static readonly Choice[] Fixed =
    {
        new Choice { Label = "Finder", BundleId = "com.apple.finder", Name = "Finder" },
        new Choice { Label = "Premiere", BundleId = "com.adobe.PremierePro", Name = "Adobe Premiere Pro" },
        // ラスボスは毎回最後に足されるので、何か1つのアプリでランを作ってから、ラスボスだけにしぼる
        new Choice { Label = "Terminal", BundleId = "com.apple.finder", Name = "Finder", Final = true },
        new Choice { Label = "Chrome", BundleId = "com.google.Chrome", Name = "Google Chrome" },
        new Choice { Label = "VS Code", BundleId = "com.microsoft.VSCode", Name = "Visual Studio Code" },
    };

    // 格子の大きさ（2026-11-19）
    const int Cols = 4, Rows = 3;
    const float BtnW = 76f, BtnH = 16f, Gap = 3f;

    List<Choice> _choices;
    float _choicesAt = -1f;
    int _page;

    /// <summary>並べるアプリ（決まった物 → 表の行 → AppKits の登録。同じ名前は1つ）。2秒ごとに作り直す</summary>
    List<Choice> Choices(GameSession session)
    {
        if (_choices != null && Time.unscaledTime - _choicesAt < 2f) return _choices;
        _choicesAt = Time.unscaledTime;
        var list = new List<Choice>(Fixed);
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var c in Fixed) seen.Add(c.Label);
        void Add(string label, string[] names, string[] bundles)
        {
            if (string.IsNullOrEmpty(label) || !seen.Add(label)) return;
            string name = names != null && names.Length > 0 && !string.IsNullOrEmpty(names[0]) ? names[0] : label;
            string bundle = bundles != null && bundles.Length > 0 && !string.IsNullOrEmpty(bundles[0]) ? bundles[0] : "";
            list.Add(new Choice { Label = label, Name = name, BundleId = bundle });
        }
        if (session.Catalog != null) foreach (var e in session.Catalog.AppBosses) if (e != null) Add(e.Label, e.AppNames, e.BundleIdPrefixes);
        if (session.Abilities != null) foreach (var e in session.Abilities.AppSkills) if (e != null) Add(e.Label, e.AppNames, e.BundleIdPrefixes);
        foreach (var label in AppKits.Labels) Add(label, AppKits.NamesOf(label), AppKits.BundlesOf(label));
        _choices = list;
        return list;
    }

    /// <summary>【仮】使用時間（強さの真ん中くらい）</summary>
    const double UsageHours = 10.0;

    string _error;

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsTitle(scene.name)) AppDex.Suspended = false; // タイトルに戻ったら、図鑑に書くのを元に戻す
    }

    static bool IsTitle(string sceneName) => sceneName == "RunTitle";

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning || !IsTitle(SceneManager.GetActiveScene().name)) return;
        GUI.depth = 0; // ほかの画面より手前
        PixelGui.Begin(false);
        List<Choice> choices = Choices(session);
        int perPage = Cols * Rows;
        int pages = Mathf.Max(1, (choices.Count + perPage - 1) / perPage);
        _page = Mathf.Clamp(_page, 0, pages - 1);
        float w = Cols * (BtnW + Gap) - Gap + 12f;
        float h = 20f + Rows * (BtnH + Gap) + 4f;
        var box = new Rect(6f, PixelGui.Height - h - 6f, w, h);
        PixelGui.Fill(box, new Color(0f, 0f, 0f, 0.8f));
        PixelGui.Text(new Rect(box.x + 6f, box.y + 2f, box.width - 70f, 16f), "【確認用】ボスを選んで遊ぶ", 10, Color.white);
        if (pages > 1)
        {
            PixelGui.TextEn(new Rect(box.xMax - 66f, box.y + 6f, 24f, 8f), $"{_page + 1}/{pages}", 8, Color.white);
            if (PixelGui.DarkButton(new Rect(box.xMax - 40f, box.y + 3f, 16f, 14f), "<", false)) _page = (_page + pages - 1) % pages;
            if (PixelGui.DarkButton(new Rect(box.xMax - 22f, box.y + 3f, 16f, 14f), ">", false)) _page = (_page + 1) % pages;
        }
        for (int i = 0; i < perPage; i++)
        {
            int idx = _page * perPage + i;
            if (idx >= choices.Count) break;
            Choice c = choices[idx];
            var r = new Rect(box.x + 6f + (i % Cols) * (BtnW + Gap), box.y + 20f + (i / Cols) * (BtnH + Gap), BtnW, BtnH);
            bool pressed = PixelGui.DarkButton(r, "", false);
            PixelGui.TextFit(new Rect(r.x + 3f, r.y - 1f, r.width - 6f, r.height), c.Label, 10, PixelGui.Lavender, TextAnchor.MiddleCenter);
            if (pressed) Play(session, c);
        }
        if (!string.IsNullOrEmpty(_error)) PixelGui.Text(new Rect(box.x, box.y - 16f, 400f, 14f), _error, 10, PixelGui.Orange);
        PixelGui.End();
    }

    void Play(GameSession session, Choice c)
    {
        AppDex.Suspended = true;
        session.StageFilter = plans => c.Final ? plans.Where(p => p.IsFinal).ToList() : plans.Where(p => !p.IsFinal).ToList();
        bool ok = session.StartRun(new Provider(new AppUsage(c.BundleId, c.Name, UsageHours * 3600.0)), out string err);
        session.StageFilter = null;
        if (ok)
        {
            _error = null;
            return;
        }
        AppDex.Suspended = false;
        _error = err;
    }
}
