using UnityEngine;

/// <summary>
/// 実績1つ分のデータ（Steam スキル）。実績を増やすときは、このアセットを作って AchievementPassive の一覧に足す。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Achievement
/// </summary>
[CreateAssetMenu(fileName = "Achievement_", menuName = "Team1/Achievement")]
public class AchievementDefinition : ScriptableObject
{
    [SerializeField] AchievementSpec _spec = new AchievementSpec("実績", AchievementCondition.SkillUses, 3);

    public AchievementSpec Spec => _spec;

    /// <summary>通知に出す名前。AchievementNames.Resolver が設定されていれば、そちらで差し替える（個人に合わせた名前用）</summary>
    public string DisplayName => AchievementNames.Resolve(this);
}

/// <summary>
/// 実績の名前を差し替える口。今は差し替えない。
/// 【今回は対象外】Steam のプレイ履歴（localconfig.vdf など）を読んで、実績の名前を個人に合わせる案がある。
/// 作るときは Resolver に関数を入れる（null を返したら元の名前を使う）。
/// </summary>
public static class AchievementNames
{
    public static System.Func<AchievementDefinition, string> Resolver;

    public static string Resolve(AchievementDefinition def)
    {
        if (def == null) return string.Empty;
        string custom = Resolver?.Invoke(def);
        if (!string.IsNullOrEmpty(custom)) return custom;
        return string.IsNullOrEmpty(def.Spec.Name) ? def.name : def.Spec.Name;
    }
}
