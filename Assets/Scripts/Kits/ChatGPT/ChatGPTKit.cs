using UnityEngine;

/// <summary>
/// 【仮】ChatGPT のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 5章）。担当3 の物。
/// ・アプリの名前・バンドルID（AppKits）と、ステージのギミック「考え中…」（StageRules）
/// ・ボスの技・能力のアセットは Editor/Kits/ChatGPTKitAssets.cs（メニュー「Tools/Team1/アプリのキットを用意」）
/// ・ラベルは ChatGPTKitAssets.Label と同じ
/// </summary>
public static class ChatGPTKit
{
    public const string Label = "ChatGPT";
    public static readonly string[] AppNames = { "ChatGPT" };
    public static readonly string[] BundleIds = { "com.openai.chat" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new ChatGptRule());
        StageEvents.Register(Label, stage => new ChatGptStreamEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
    }
}
