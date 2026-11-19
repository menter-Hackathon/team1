using UnityEngine;

/// <summary>
/// 【仮】Safari のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 4-4）。担当B。
/// ・ラベル・名前・バンドルIDは、アセットを作る側（Editor/Kits/SafariKitAssets.cs）もここを読む
/// ・ステージの決まり：トラッカーをブロック（SafariRule）
/// </summary>
public static class SafariKit
{
    public const string Label = "Safari";
    public static readonly string[] Names = { "Safari" };
    public static readonly string[] Bundles = { "com.apple.Safari" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new SafariRule());
    }
}
