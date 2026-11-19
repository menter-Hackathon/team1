using UnityEngine;

/// <summary>
/// 【仮】Finder のステージのキット（2026-11-19。docs/app-feedback-v2-design.md 4-3・1-2）。担当2 の物。
/// ・StagePacer.Started で、Finder のステージ（スキンが Finder）なら、ゴミ箱（FinderTrash）を付ける。ボスの登場でゴミ箱は自分で去る
/// ・場面③の出来事：2026-11-19（docs/stage-gimmick-variety-a.md Finder）、スキンの雨（ファイルが降ってくる。Copying...）をやめて
///   「ドラッグ＆ドロップ」（FinderDragDropEvent。角・壁から斜めに飛ぶファイルと、取るとゴミ箱に入る青いフォルダ）にする
/// ・ほかのアプリのステージでは、何も付けない。StagePaceTable.Enabled = false（StagePacer が無い）のときも、何もしない
/// </summary>
public static class FinderKit
{
    public static readonly string[] Names = { "Finder" };
    public static readonly string[] Bundles = { "com.apple.finder" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
        AppStageEvents.Register("Finder", Names, Bundles, (stage, isFinal) => isFinal ? null : new FinderDragDropEvent());
    }

    static void OnStarted(StagePacer pacer)
    {
        if (pacer == null || !IsFinder(pacer)) return;
        FinderTrash.Attach(pacer);
        // スキンで Finder と分かったのに、出来事の表（名前・バンドルID）に当たらないときは、そのステージの名前を足して登録し直す（PremiereKit と同じ）
        if (!AppStageEvents.Has(pacer.AppName, pacer.BundleId))
        {
            var names = new System.Collections.Generic.List<string>(Names);
            var bundles = new System.Collections.Generic.List<string>(Bundles);
            if (!string.IsNullOrEmpty(pacer.AppName)) names.Add(pacer.AppName);
            if (!string.IsNullOrEmpty(pacer.BundleId)) bundles.Add(pacer.BundleId);
            AppStageEvents.Register("Finder", names.ToArray(), bundles.ToArray(), (stage, isFinal) => isFinal ? null : new FinderDragDropEvent());
        }
    }

    /// <summary>Finder のステージか（スキンが Finder。スキンの表の専用の行と同じ決め方）</summary>
    public static bool IsFinder(StagePacer pacer)
    {
        if (pacer.Skin == SkinId.Finder) return true;
        var row = new AppUsageRow { Name = pacer.AppName ?? "", BundleId = pacer.BundleId ?? "", Seconds = 1 };
        return AppMatch.Matches(row, Names, Bundles);
    }
}
