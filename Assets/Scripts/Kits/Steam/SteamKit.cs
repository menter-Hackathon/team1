using UnityEngine;

/// <summary>
/// 【仮】Steam のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 2-3。担当C）。
/// ラベルは Editor/Kits/SteamKitAssets.cs の Label と同じ "Steam"。ステージの決まりは「実績解除」（SteamRule）
/// </summary>
public static class SteamKit
{
    public const string Label = "Steam";
    public static readonly string[] Names = { "Steam" };
    public static readonly string[] Bundles = { "com.valvesoftware.steam" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new SteamRule());
    }
}
