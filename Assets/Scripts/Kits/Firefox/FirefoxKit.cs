using UnityEngine;

/// <summary>
/// 【仮】Firefox のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 4-6。担当C）。
/// ラベルは Editor/Kits/FirefoxKitAssets.cs の Label と同じ "Firefox"。ステージの決まりは「アドオン」（FirefoxRule）。
/// Chrome の技（Combat/Custom/Chrome）は呼ばない（Chrome は別の担当）
/// </summary>
public static class FirefoxKit
{
    public const string Label = "Firefox";
    public static readonly string[] Names = { "Firefox", "Firefox Developer Edition", "Firefox Nightly" };
    public static readonly string[] Bundles = { "org.mozilla.firefox" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new FirefoxRule());
    }
}
