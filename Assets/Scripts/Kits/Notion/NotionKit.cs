using UnityEngine;

/// <summary>
/// 【仮】Notion のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 8章。担当0）。
/// ・ボスの技：P1 ブロックを積む（NotionBlockStack）・トグル（データの柱 3本＋絵の差し替え）。P2 は トグル 5本と、ブロックを積む（4個）
///   ビューの切り替え（ZoneStrike を使う）は、ZoneStrike（担当3）ができてから足す【後回し】
/// ・能力：ブロック（NotionBlockSkill）。ギミック：トグルの足場（NotionRule）
/// アセットは Editor/Kits/NotionKitAssets.cs が作る
/// </summary>
public static class NotionKit
{
    public const string Label = "Notion";
    public static string[] AppNames => new[] { "Notion" };
    public static string[] BundleIds => new[] { "notion.id" };
    public const string SkillName = "ブロック";
    public const string SkillDescription = "正面の床に白いブロックを置く。出る瞬間に当たり、しばらく敵の弾を止める壁になる。";
    public static string[] SkillTags => new[] { "設置", "盾" };

    /// <summary>絵の名前（KitDressed から引く）</summary>
    public const string ArtBlock = "notion.block";
    public const string ArtToggle = "notion.toggle";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new NotionRule());
        StageEvents.Register(Label, stage => new NotionDragEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
        KitDress.RegisterArt(ArtBlock, i => NotionArt.Block(i));
        KitDress.RegisterArt(ArtToggle, i => NotionArt.ToggleBox(i));
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
