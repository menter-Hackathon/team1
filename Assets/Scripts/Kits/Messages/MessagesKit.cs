using UnityEngine;

/// <summary>
/// 【仮】メッセージ（Apple の Messages）のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 16章）。
/// アセット側は Editor/Kits/MessagesKitAssets.cs（ラベル・名前は、ここの定数と同じ物を使う）。
/// 19章の別名（Telegram・WhatsApp・Messenger）は【要相談】のため、まだ入れていない
/// </summary>
public static class MessagesKit
{
    public const string Label = "Messages";
    public static readonly string[] Names = { "Messages", "メッセージ" };
    public static readonly string[] Bundles = { "com.apple.MobileSMS" };

    public const string SkillName = "花火";
    public const string SkillDescription = "空中に花火を3発。浮いているボスに届く";
    public static readonly string[] SkillTags = { "空中", "連続" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new MessagesRule());
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
