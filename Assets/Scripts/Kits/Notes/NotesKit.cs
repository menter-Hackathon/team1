using UnityEngine;

/// <summary>
/// 【仮】メモ（Apple の Notes）のキットの実行時の登録（2026-11-19。docs/app-kits-1-design.md 14章。担当0）。
/// ・ボスの技：P1 メモの紙（データの上の列＋絵の差し替え）・クイックメモ（NotesQuickMemo）。P2 は 紙 7枚・クイックメモ・鍵をかける（NotesLockAttack）
/// ・能力：ピン留め（NotesPinSkill）。ギミック：チェックリスト（NotesRule）
/// 19章の別名：リマインダー（KitAliases）。アセットは Editor/Kits/NotesKitAssets.cs が作る
/// </summary>
public static class NotesKit
{
    public const string Label = "Notes";
    public static string[] AppNames => new[] { "Notes", "メモ" };
    public static string[] BundleIds => new[] { "com.apple.Notes" };
    public const string SkillName = "ピン留め";
    public const string SkillDescription = "画びょうを正面に投げる。当たったボスは、しばらくその場に留まる。";
    public static string[] SkillTags => new[] { "遠距離", "足止め" };

    public const string ArtPaper = "notes.paper";
    public const string ArtQuick = "notes.quick";
    public const string ArtPin = "notes.pin";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new NotesRule());
        StageEvents.Register(Label, stage => new NotesChecklistEvent(stage)); // 場面③の出来事（2026-11-19。docs/stage-gimmick-variety-b.md）
        KitDress.RegisterArt(ArtPaper, i => NotesArt.Paper2(i));
        KitDress.RegisterArt(ArtQuick, i => NotesArt.QuickMemo());
        KitDress.RegisterArt(ArtPin, i => NotesArt.Pin());
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
