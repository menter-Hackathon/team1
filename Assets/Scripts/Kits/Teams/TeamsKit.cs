using UnityEngine;

/// <summary>
/// 【仮】Microsoft Teams のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 17章）。
/// ・ギミックは場面③の専用の出来事（ブレイクアウトルーム。StageEvents）。StageRule は無い
/// ・挙手の柱の絵（KitDressed の絵の名前）を登録する
/// アセット側は Editor/Kits/TeamsKitAssets.cs（ラベル・名前・絵の名前は、ここの定数と同じ物を使う）
/// </summary>
public static class TeamsKit
{
    public const string Label = "Teams";
    public static readonly string[] Names = { "Microsoft Teams", "Teams" };
    public static readonly string[] Bundles = { "com.microsoft.teams" };

    /// <summary>挙手の柱の絵（1.5×4.5）</summary>
    public const string HandArt = "teams.hand";
    /// <summary>能力の挙手の柱の絵（1.5×6）</summary>
    public const string HandTallArt = "teams.hand.tall";

    public const string SkillName = "挙手";
    public const string SkillDescription = "真上に大きい手。ボスの真下から";
    public static readonly string[] SkillTags = { "真上", "主力" };

    /// <summary>出来事の雑魚の上乗せ（両方の部屋に出す分）【仮】</summary>
    const int ExtraZako = 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageEvents.Register(Label, stage => new TeamsBreakoutEvent(), ExtraZako);
        KitDress.RegisterArt(HandArt, i => TeamsArt.Hand(i));
        KitDress.RegisterArt(HandTallArt, i => TeamsArt.HandTall(i));
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
