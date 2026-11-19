using UnityEngine;

/// <summary>
/// 【仮】Keynote のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 13章）。担当4。
/// ラベル "Keynote" は KeynoteKitAssets.Label と同じ。ギミック：拍手（KeynoteRule）。
/// 絵の名前（KitDress）：keynote.number（大きい数字。index 0,1,2 → 1,2,3）
/// </summary>
public static class KeynoteKit
{
    public const string Label = "Keynote";
    public static readonly string[] Names = { "Keynote" };
    public static readonly string[] Bundles = { "com.apple.iWork.Keynote" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new KeynoteRule());
        KitDress.RegisterArt("keynote.number", i => KeynoteArt.BigNumber(1 + (i % 3)));
    }
}
