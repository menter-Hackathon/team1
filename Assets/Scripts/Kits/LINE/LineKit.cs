using UnityEngine;

/// <summary>
/// 【仮】LINE のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 4章）。
/// アセット側は Editor/Kits/LINEKitAssets.cs（ラベル・名前は、ここの定数と同じ物を使う）
/// </summary>
public static class LineKit
{
    public const string Label = "LINE";
    public static readonly string[] Names = { "LINE" };
    public static readonly string[] Bundles = { "jp.naver.line.mac" };

    public const string SkillName = "スタンプ";
    public const string SkillDescription = "目の前に大きいスタンプをドン";
    public static readonly string[] SkillTags = { "前方", "主力" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new LineRule());
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
