using UnityEngine;

/// <summary>
/// 【仮】PowerPoint のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 11章）。担当4。
/// ラベル "PowerPoint" は PowerPointKitAssets.Label と同じ。ギミック：レーザーポインター（PowerPointRule）
/// </summary>
public static class PowerPointKit
{
    public const string Label = "PowerPoint";
    public static readonly string[] Names = { "PowerPoint", "Microsoft PowerPoint" };
    public static readonly string[] Bundles = { "com.microsoft.Powerpoint" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new PowerPointRule());
        StageEvents.Register(Label, stage => new PowerPointBounceEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
    }
}
