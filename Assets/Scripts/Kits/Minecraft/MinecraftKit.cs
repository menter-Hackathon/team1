using UnityEngine;

/// <summary>
/// 【仮】Minecraft のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// ・ラベル "Minecraft"（Editor/Kits/MinecraftKitAssets.cs の Label と同じ）。バンドルIDは【要確認】（com.mojang. の前方一致。名前でも見分ける）
/// ・ステージの決まり：MinecraftRule（昼と夜）
/// </summary>
public static class MinecraftKit
{
    public const string Label = "Minecraft";
    public static readonly string[] Names = { "Minecraft", "Minecraft Launcher" };
    public static readonly string[] Bundles = { "com.mojang." };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new MinecraftRule());
    }
}
