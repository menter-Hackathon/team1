using UnityEngine;

/// <summary>
/// 【仮】Xcode のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 3-2）。担当B。
/// ・ラベル・名前・バンドルIDは、アセットを作る側（Editor/Kits/XcodeKitAssets.cs）もここを読む
/// ・ステージの決まり：Build（XcodeRule）
/// </summary>
public static class XcodeKit
{
    public const string Label = "Xcode";
    public static readonly string[] Names = { "Xcode" };
    public static readonly string[] Bundles = { "com.apple.dt.Xcode" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new XcodeRule());
    }
}
