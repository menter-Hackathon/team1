using UnityEngine;

/// <summary>
/// 【仮】Word のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 12章）。担当4。
/// ラベル "Word" は WordKitAssets.Label と同じ。ギミック：スペルチェック（WordRule）。
/// 絵の名前（KitDress）：word.comment（コメントの吹き出し）・word.image（大きい画像）・word.textwave（押し出された文字の波）
/// </summary>
public static class WordKit
{
    public const string Label = "Word";
    public static readonly string[] Names = { "Word", "Microsoft Word" };
    public static readonly string[] Bundles = { "com.microsoft.Word" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new WordRule());
        StageEvents.Register(Label, stage => new WordSpellWaveEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
        KitDress.RegisterArt("word.comment", () => WordArt.Comment);
        KitDress.RegisterArt("word.image", () => WordArt.Image);
        KitDress.RegisterArt("word.textwave", () => WordArt.TextWave);
    }
}
