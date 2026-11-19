using UnityEngine;

/// <summary>
/// 【仮】Gemini のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 7章）。担当3 の物。
/// ・アプリの名前（AppKits）・ステージのギミック「回答案を選ぶ」（StageRules）・能力「ふたご星」の柱の絵（KitDress）
/// ・ボスの技・能力のアセットは Editor/Kits/GeminiKitAssets.cs（メニュー「Tools/Team1/アプリのキットを用意」）
/// ・ラベルは GeminiKitAssets.Label と同じ。Mac の専用のアプリが無いので、バンドルID は空（名前で当てる）
/// </summary>
public static class GeminiKit
{
    public const string Label = "Gemini";
    public static readonly string[] AppNames = { "Gemini" };
    public static readonly string[] BundleIds = new string[0];
    /// <summary>能力「ふたご星」の柱の絵の名前（KitDressed の絵の名前）</summary>
    public const string StarPillarArt = "gemini.starpillar";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new GeminiRule());
        StageEvents.Register(Label, stage => new GeminiTwinStarsEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
        KitDress.RegisterArt(StarPillarArt, () => GeminiArt.Beam);
    }
}
