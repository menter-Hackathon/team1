using UnityEngine;

/// <summary>
/// 【仮】Figma のキットの登録（2026-11-19。docs/app-kits-2-design.md 2-4）。担当D。
/// ラベルは Editor の FigmaKitAssets.Label と同じ。ボスの技・能力のアセットは「Tools/Team1/アプリのキットを用意」で作る
/// </summary>
public static class FigmaKit
{
    public const string Label = "Figma";
    public static readonly string[] Names = { "Figma" };
    public static readonly string[] Bundles = { "com.figma.Desktop" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new FigmaRule());
    }
}
