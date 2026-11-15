using UnityEngine;

/// <summary>
/// スキル1つ分の定義。今は表示名・アイコン・カテゴリだけを持つ。
/// 3枠の保持・捨てる・入れ替えの仕組み（SkillLoadout）は、この中身に依存しない。
/// M3 で攻撃データ（AttackData）ができたら、ここに参照を足してスキルの中身にする。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Skill Definition
/// </summary>
[CreateAssetMenu(fileName = "Skill_", menuName = "Team1/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Tooltip("画面に出す名前。空ならアセット名を使う")]
    [SerializeField] string _displayName;
    [SerializeField] Sprite _icon;
    [SerializeField] SkillCategory _category = SkillCategory.Unknown;
    // M3：[SerializeField] AttackData _attack; をここに足す

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public Sprite Icon => _icon;
    public SkillCategory Category => _category;
}
