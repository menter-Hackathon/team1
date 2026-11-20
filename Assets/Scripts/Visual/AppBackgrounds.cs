using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ステージの背景を、そのアプリの画面（UI）の絵にする（2026-11-19。Tools/art/appbg/FORMAT.md）。
/// ・絵：Resources/AppBackgrounds/Bg_&lt;ラベル&gt;.png（Tools/art/appbg/render.py が描く。320×180 ドット・1マス 15 ドット＝今の壁紙と同じ大きさ）
/// ・ラベルの決め方（LabelFor）：
///   ラスボス → Terminal。
///   VS Code・Finder・Premiere Pro・Terminal・Chrome（専用の名前を持つアプリ）→ VSCode／Finder／PremierePro／Terminal／Chrome。
///   ほかはアプリのキットのラベル（AppKits.FindLabel。KitAliases の別名も当たる：Outlook → Mail、Pages → Word、Numbers → Excel など）。
///   どれにも当たらない（キットの無い代用ボス）→ null
/// ・絵が無い（まだ描いていない・ラベルが無い）ときは null。StageTheme が今までどおり壁紙を色を変えて出す
/// ・StageDirector がステージの始めに ForStage を呼ぶ（StageTheme.Apply に渡す）。感染したデスクトップの画面は Find で、開くウィンドウの中に出す
/// </summary>
public static class AppBackgrounds
{
    /// <summary>【仮】false にすると、アプリの背景を出さない（今までの壁紙に戻る）</summary>
    public static bool Enabled = true;

    /// <summary>【仮】false にすると、アプリの UI の足場・ギミック（AppStagePlatforms）を出さない（今までのフォルダの足場に戻る）</summary>
    public static bool StagePlatforms = true;

    /// <summary>Resources の中のフォルダ</summary>
    public const string Folder = "AppBackgrounds";

    /// <summary>今のステージで出しているアプリの背景（無ければ null）。ステージの始めに毎回入れ直す</summary>
    public static Sprite Current { get; private set; }
    /// <summary>今のステージのラベル（無ければ null）</summary>
    public static string CurrentLabel { get; private set; }

    // 専用の名前を持つアプリ（キットの表より先に見る）。名前・バンドルIDは、それぞれのキットの物を使う
    struct Row
    {
        public string Label;
        public string[] Names;
        public string[] Bundles;
        public Row(string label, string[] names, string[] bundles)
        {
            Label = label;
            Names = names;
            Bundles = bundles;
        }
    }

    static Row[] _dedicated;
    static Row[] Dedicated => _dedicated ??= new[]
    {
        new Row("VSCode", VSCodeKit.Names, VSCodeKit.Bundles),
        new Row("Finder", FinderKit.Names, FinderKit.Bundles),
        new Row("PremierePro", PremiereKit.Names, PremiereKit.Bundles),
        new Row("Terminal", TerminalKit.AppNames, TerminalKit.BundleIds),
        new Row("Chrome", ChromeKit.Names, ChromeKit.Bundles),
    };

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Cache.Clear();
        Logged.Clear();
        Current = null;
        CurrentLabel = null;
    }

    /// <summary>そのステージのアプリのラベル（例："Slack"）。分からなければ null</summary>
    public static string LabelFor(StagePlan plan)
    {
        if (plan == null || plan.App == null) return null;
        if (plan.IsFinal) return "Terminal";
        string bundle = plan.App.BundleIdPrefixes != null && plan.App.BundleIdPrefixes.Length > 0 ? plan.App.BundleIdPrefixes[0] : null;
        return LabelFor(plan.App.DisplayName, bundle);
    }

    /// <summary>アプリの名前とバンドルIDから、ラベルを決める。分からなければ null</summary>
    public static string LabelFor(string appName, string bundleId)
    {
        var row = new AppUsageRow { Name = appName ?? "", BundleId = bundleId ?? "", Seconds = 1 };
        foreach (Row d in Dedicated)
        {
            if (AppMatch.Matches(row, d.Names, d.Bundles)) return d.Label;
        }
        return AppKits.FindLabel(row); // 別名（KitAliases）も入っている。AppKits.Enabled は見ない（見た目だけなので）
    }

    /// <summary>そのステージのアプリの背景の絵（無ければ null）。読み込みは1回だけ（覚えておく）</summary>
    public static Sprite Find(StagePlan plan)
    {
        if (!Enabled || plan == null) return null;
        try
        {
            return Load(LabelFor(plan), plan.DisplayName);
        }
        catch (Exception e)
        {
            Debug.LogException(e); // 背景が出せなくても、ステージは今までの壁紙で続ける
            return null;
        }
    }

    /// <summary>ステージの始めに呼ぶ（StageDirector）：そのステージの背景を決めて、Current に入れる（無ければ null を入れる）</summary>
    public static Sprite ForStage(StagePlan plan)
    {
        // ウィンドウのステージ（2026-11-20）：後ろは Mac のデスクトップの壁紙（明るくした物）。無ければ今までどおり
        if (WindowStage.Wants(plan))
        {
            Sprite w = WindowStage.Wallpaper();
            if (w != null) { Current = w; CurrentLabel = LabelFor(plan); return w; }
        }
        Current = Find(plan);
        CurrentLabel = Current != null ? LabelFor(plan) : null;
        return Current;
    }

    static Sprite Load(string label, string appName)
    {
        if (string.IsNullOrEmpty(label))
        {
            Log(appName ?? "?", $"[AppBackgrounds] {appName}：ラベルが無いので、今までの壁紙");
            return null;
        }
        string file = PremiereEditScreen.ResourceLabel(label); // 2026-11-20：Premiere Pro の切り戻し（スイッチがオフなら前の絵）
        if (!Cache.TryGetValue(file, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>($"{Folder}/Bg_{file}");
            Cache[file] = sprite;
        }
        Log(label, sprite != null
            ? $"[AppBackgrounds] {appName} → Bg_{label}"
            : $"[AppBackgrounds] {appName} → Bg_{label} が無いので、今までの壁紙");
        return sprite;
    }

    // 開発中だけ、ラベルごとに1回ログを出す
    static void Log(string key, string message)
    {
        if (!Debug.isDebugBuild || !Logged.Add(key)) return;
        Debug.Log(message);
    }
}
