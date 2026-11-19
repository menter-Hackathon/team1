using UnityEngine;

/// <summary>
/// 【仮】Discord のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 3章）。担当2。
/// ラベルは Editor/Kits/DiscordKitAssets.cs の Label と同じ "Discord"。
/// ・ボスの技：ボイス（WallShot）・サーバー一覧（WallShot）／第2形態：スタンプ連打（落下物）
/// ・能力：ミュート（弾を消す円・ボスを 2秒止める）
/// ・ギミック：ボイスチャンネル（足場の上でクールタイムが速く進む・ときどき切断）
/// </summary>
public static class DiscordKit
{
    public const string Label = "Discord";
    public static readonly string[] Names = { "Discord" };
    public static readonly string[] Bundles = { "com.hnc.Discord" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new DiscordRule());
    }
}
