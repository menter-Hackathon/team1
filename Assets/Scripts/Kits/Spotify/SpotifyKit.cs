using UnityEngine;

/// <summary>
/// 【仮】Spotify のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 4-2。担当C）。
/// ラベルは Editor/Kits/SpotifyKitAssets.cs の Label と同じ "Spotify"。ステージの決まりは「広告と Premium」（SpotifyRule）
/// </summary>
public static class SpotifyKit
{
    public const string Label = "Spotify";
    public static readonly string[] Names = { "Spotify" };
    public static readonly string[] Bundles = { "com.spotify.client" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new SpotifyRule());
    }
}
