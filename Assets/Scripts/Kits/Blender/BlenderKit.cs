using UnityEngine;

/// <summary>
/// 【仮】Blender のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// ・ラベル "Blender"（Editor/Kits/BlenderKitAssets.cs の Label と同じ）
/// ・ステージの決まり：BlenderRule（Default Cube）
/// </summary>
public static class BlenderKit
{
    public const string Label = "Blender";
    public static readonly string[] Names = { "Blender" };
    public static readonly string[] Bundles = { "org.blenderfoundation." };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new BlenderRule());
    }
}
