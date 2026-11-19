using UnityEngine;

/// <summary>
/// 【仮】Cursor のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 3-3）。担当B。
/// ・ラベル・名前・バンドルIDは、アセットを作る側（Editor/Kits/CursorKitAssets.cs）もここを読む
/// ・ステージの決まり：Tab の候補（CursorRule）
/// </summary>
public static class CursorKit
{
    public const string Label = "Cursor";
    public static readonly string[] Names = { "Cursor" };
    public static readonly string[] Bundles = { "com.todesktop.230313mzl4w4u92" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new CursorRule());
        StageEvents.Register(Label, stage => new CursorGhostTextEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
    }
}
