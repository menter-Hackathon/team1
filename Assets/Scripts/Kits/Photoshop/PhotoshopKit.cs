using UnityEngine;

/// <summary>
/// 【仮】Photoshop のキットの登録（2026-11-19。docs/app-kits-2-design.md 3-1）。担当D。
/// ラベルは Editor の PhotoshopKitAssets.Label と同じ。絵は Creative のスキンの色に合わせる（Premiere の部品は呼ばない）
/// </summary>
public static class PhotoshopKit
{
    public const string Label = "Photoshop";
    public static readonly string[] Names = { "Photoshop", "Adobe Photoshop" };
    public static readonly string[] Bundles = { "com.adobe.Photoshop" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new PhotoshopRule());
    }
}
