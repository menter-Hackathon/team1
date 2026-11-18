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

    static readonly Choice[] Choices =
    {
        new Choice { Label = "Finder", BundleId = "com.apple.finder", Name = "Finder" },
        new Choice { Label = "Premiere", BundleId = "com.adobe.PremierePro", Name = "Adobe Premiere Pro" },
        // ラスボスは毎回最後に足されるので、何か1つのアプリでランを作ってから、ラスボスだけにしぼる
        new Choice { Label = "Terminal", BundleId = "com.apple.finder", Name = "Finder", Final = true },
    };

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
        var box = new Rect(6f, PixelGui.Height - 52f, 254f, 46f);
        PixelGui.Fill(box, new Color(0f, 0f, 0f, 0.8f));
        PixelGui.Text(new Rect(box.x + 6f, box.y + 2f, box.width - 12f, 16f), "【確認用】ボスを選んで遊ぶ", 10, Color.white);
        float bx = box.x + 6f;
        foreach (var c in Choices)
        {
            var r = new Rect(bx, box.y + 20f, 78f, 20f);
            if (PixelGui.DarkButton(r, c.Label, false)) Play(session, c);
            bx += 82f;
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
