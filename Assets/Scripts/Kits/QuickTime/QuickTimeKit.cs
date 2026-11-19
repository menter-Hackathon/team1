using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// ・ラベル "QuickTime"（Editor/Kits/QuickTimeKitAssets.cs の Label と同じ）
/// ・ステージの決まり：QuickTimeRule（再生の速さ）
/// </summary>
public static class QuickTimeKit
{
    public const string Label = "QuickTime";
    public static readonly string[] Names = { "QuickTime Player", "QuickTime" };
    public static readonly string[] Bundles = { "com.apple.QuickTimePlayerX" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new QuickTimeRule());
    }
}
