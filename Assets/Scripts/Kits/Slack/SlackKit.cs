using UnityEngine;

/// <summary>
/// 【仮】Slack のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 2章）。
/// アセット側は Editor/Kits/SlackKitAssets.cs（ラベル・名前は、ここの定数と同じ物を使う）
/// </summary>
public static class SlackKit
{
    public const string Label = "Slack";
    public static readonly string[] Names = { "Slack" };
    public static readonly string[] Bundles = { "com.tinyspeck.slackmacgap" };

    /// <summary>能力の名前（カードと HUD）</summary>
    public const string SkillName = "リアクション";
    public const string SkillDescription = "親指を投げる。当てると5秒、ゲージが1.5倍";
    public static readonly string[] SkillTags = { "弾", "強化" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new SlackRule());
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
