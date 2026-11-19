using UnityEngine;

/// <summary>
/// 【仮】GitHub Desktop のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 2-2）。担当B。
/// ・ラベル・名前・バンドルIDは、アセットを作る側（Editor/Kits/GitHubDesktopKitAssets.cs）もここを読む
/// ・ステージの決まり：マージの衝突（GitHubDesktopRule）
/// </summary>
public static class GitHubDesktopKit
{
    public const string Label = "GitHubDesktop";
    public static readonly string[] Names = { "GitHub Desktop" };
    public static readonly string[] Bundles = { "com.github.GitHubClient" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new GitHubDesktopRule());
    }
}
