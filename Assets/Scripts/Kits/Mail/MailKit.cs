using UnityEngine;

/// <summary>
/// 【仮】メール（Apple の Mail）のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 15章）。担当2。
/// ラベルは Editor/Kits/MailKitAssets.cs の Label と同じ "Mail"。
/// ・ボスの技：送信（狙う紙飛行機 3つ）・迷惑メール（地雷の封筒）／第2形態：全員に返信（封筒の数＋1 の弾）
/// ・能力：送信（行って戻る紙飛行機）
/// ・ギミック：新着メール（開けると、クールタイム回復か、迷惑メールの爆発）
/// 別名（19章の Outlook）は担当0 の KitAliases が足す（ここでは足さない）
/// </summary>
public static class MailKit
{
    public const string Label = "Mail";
    public static readonly string[] Names = { "Mail", "メール" };
    public static readonly string[] Bundles = { "com.apple.mail" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new MailRule());
    }
}
