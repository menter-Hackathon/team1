using UnityEngine;

/// <summary>
/// 【仮】Zoom のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 18章）。担当2。ボスとギミックだけ。
/// 能力は今ある「ギャラリービュー」（AbilityCatalog の Zoom の行）のまま。キットは能力を作らない（PlayerSkill＝null）。
/// ラベルは Editor/Kits/ZoomKitAssets.cs の Label と同じ "Zoom"。
/// ・ボスの技：ギャラリービュー（タイルが赤くなる。ZoneStrike）・退出ボタン（落下物 2個）／第2形態：リアクションの嵐（拍手が 5つ）
/// ・ギミック：40分で切れる（40秒でゲージを 25 増やさないと、ゲージが 8 戻る）
/// </summary>
public static class ZoomKit
{
    public const string Label = "Zoom";
    public static readonly string[] Names = { "Zoom", "zoom.us" };
    public static readonly string[] Bundles = { "us.zoom.xos" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new ZoomRule());
        KitDress.RegisterArt("zoom.leave", () => ZoomArt.LeaveButton());
    }
}
