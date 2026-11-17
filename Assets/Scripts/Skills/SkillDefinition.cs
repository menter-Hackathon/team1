using UnityEngine;

/// <summary>
/// スキル（能力）1つ分の定義。表示名・アイコン・カテゴリと、使ったときに実行する攻撃データ（AttackData）を持つ。
/// クールタイムは攻撃データの「間隔」。使うのは PlayerSkillExecutor。
/// 3枠の保持・捨てる・入れ替えの仕組み（SkillLoadout）は、この中身に依存しない。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Skill Definition
/// </summary>
[CreateAssetMenu(fileName = "Skill_", menuName = "Team1/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Tooltip("画面に出す名前。空ならアセット名を使う")]
    [SerializeField] string _displayName;
    [SerializeField] Sprite _icon;
    [SerializeField] SkillCategory _category = SkillCategory.Unknown;
    [Tooltip("使ったときに実行する攻撃データ。クールタイムはこのデータの「間隔」")]
    [SerializeField] AttackData _attack;
    [Tooltip("手に入れたアプリの色（枠の縁・効果の色）。空なら元の色")]
    [SerializeField] AppTheme _theme;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public Sprite Icon => _icon;
    public SkillCategory Category => _category;
    public AttackData Attack => _attack;
    public AppTheme Theme => _theme;

    /// <summary>攻撃データだけを差し替えた複製を作る（使用時間で強くした能力を渡すとき用）。アセットは書き換えない</summary>
    public SkillDefinition CreateCopy(AttackData attack)
    {
        var copy = Instantiate(this);
        copy.name = name;
        copy._attack = attack;
        return copy;
    }
}
