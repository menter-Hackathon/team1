using UnityEngine;

/// <summary>
/// 【仮】カレンダーのキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 9章）。担当2。
/// ラベルは Editor/Kits/CalendarKitAssets.cs の Label と同じ "Calendar"。
/// ・ボスの技：予定ブロック・締め切りの線（場に残る技）／第2形態：毎週の予定（と、5列のダブルブッキング）
/// ・能力：予定を入れる（置いて 3秒後に大きい判定）
/// ・ギミック：予定の締め切り（会議室の足場に間に合うと、クールタイムが回復）
/// </summary>
public static class CalendarKit
{
    public const string Label = "Calendar";
    public static readonly string[] Names = { "Calendar", "カレンダー" };
    public static readonly string[] Bundles = { "com.apple.iCal" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new CalendarRule());
    }
}
