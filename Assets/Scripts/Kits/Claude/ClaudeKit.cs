using UnityEngine;

/// <summary>
/// 【仮】Claude のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 6章）。担当3 の物。
/// ・アプリの名前・バンドルID（AppKits）と、ステージのギミック「使用上限」（StageRules）
/// ・ボスの技・能力のアセットは Editor/Kits/ClaudeKitAssets.cs（メニュー「Tools/Team1/アプリのキットを用意」）
/// ・ラベルは ClaudeKitAssets.Label と同じ
/// </summary>
public static class ClaudeKit
{
    public const string Label = "Claude";
    public static readonly string[] AppNames = { "Claude" };
    public static readonly string[] BundleIds = { "com.anthropic.claudefordesktop" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new ClaudeRule());
    }
}
