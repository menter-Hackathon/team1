using UnityEngine;

/// <summary>
/// 【仮】Excel のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 10章）。担当4。
/// ラベル "Excel" は ExcelKitAssets.Label と同じ。ギミック：右肩上がり（ExcelRule）
/// </summary>
public static class ExcelKit
{
    public const string Label = "Excel";
    public static readonly string[] Names = { "Excel", "Microsoft Excel" };
    public static readonly string[] Bundles = { "com.microsoft.Excel" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new ExcelRule());
        StageEvents.Register(Label, stage => new ExcelBarChartEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
    }
}
