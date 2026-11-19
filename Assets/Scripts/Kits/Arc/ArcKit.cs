using UnityEngine;

/// <summary>
/// 【仮】Arc のキットの登録（2026-11-19。docs/app-kits-2-design.md 4-5）。担当D。
/// ラベルは Editor の ArcKitAssets.Label と同じ。Chrome（別の担当）とは被らないように、Arc らしさ（Space・Little Arc・アーカイブ・Zap）だけで作る
/// </summary>
public static class ArcKit
{
    public const string Label = "Arc";
    public static readonly string[] Names = { "Arc" };
    public static readonly string[] Bundles = { "company.thebrowser.Browser" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new ArcRule());
    }
}
