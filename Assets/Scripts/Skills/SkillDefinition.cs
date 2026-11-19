using UnityEngine;

/// <summary>
/// スキル（能力）1つ分の定義。表示名・アイコン・カテゴリと、使ったときに実行する攻撃データ（AttackData）を持つ。
/// クールタイムは攻撃データの「間隔」。使うのは PlayerSkillExecutor。
/// 3枠の保持・捨てる・入れ替えの仕組み（SkillLoadout）は、この中身に依存しない。
/// ・「順番に切り替わる攻撃」があれば、使うたびにその順で出す（Office：棒グラフ → 円グラフ → 折れ線グラフ）。
///   このとき「攻撃データ」は一覧の先頭と同じにしておく（クールタイムの表示・ほかの仕組みが読むため）
/// ・「装備中ずっと効く効果」があれば、枠に入っている間だけ効く（Steam：実績解除のバフ）。攻撃データは空でよい
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
    [Tooltip("【仮】見た目のスキン（どのアプリの絵か。2026-11-18：スキルの見た目）。None ならカテゴリ・アプリ専用の能力から決める（SkinTables.ForSkill）")]
    [SerializeField] SkinId _skin = SkinId.None;
    [Tooltip("使うたびに順番に切り替わる攻撃（Office のグラフ）。空なら毎回「攻撃データ」を使う。クールタイムは使った攻撃データの「間隔」")]
    [SerializeField] AttackData[] _attackCycle;
    [Tooltip("装備している間ずっと効く効果（Steam の実績解除）。空なら無し")]
    [SerializeField] SkillPassive _passive;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public Sprite Icon => _icon;
    public SkillCategory Category => _category;
    public AttackData Attack => _attack;
    public AppTheme Theme => _theme;
    /// <summary>この能力に持たせたスキン（その場で作る能力は、作ったアプリの名前から）。無ければ None</summary>
    public SkinId Skin => _skin;
    /// <summary>使うたびに順番に切り替わる攻撃。無ければ空</summary>
    public AttackData[] AttackCycle => _attackCycle ?? System.Array.Empty<AttackData>();
    public bool HasAttackCycle => _attackCycle != null && _attackCycle.Length > 0;
    /// <summary>装備している間ずっと効く効果。無ければ null</summary>
    public SkillPassive Passive => _passive;

    /// <summary>turn 回目（0始まり）に使ったときの攻撃データ</summary>
    public AttackData GetAttack(int turn)
    {
        if (!HasAttackCycle) return _attack;
        int n = _attackCycle.Length;
        return _attackCycle[((turn % n) + n) % n];
    }

    /// <summary>ゲームの中で能力を作る（対応表に無いアプリの能力。2026-11-17）。アセットにはしない。skin：見た目のスキン（2026-11-18）</summary>
    public static SkillDefinition CreateRuntime(string displayName, Sprite icon, SkillCategory category, AttackData attack, AppTheme theme, SkinId skin = SkinId.None)
    {
        var skill = CreateInstance<SkillDefinition>();
        skill.name = displayName;
        skill._displayName = displayName;
        skill._icon = icon;
        skill._category = category;
        skill._attack = attack;
        skill._theme = theme;
        skill._skin = skin;
        return skill;
    }

    /// <summary>攻撃データだけを差し替えた複製を作る（使用時間で強くした能力を渡すとき用）。アセットは書き換えない</summary>
    public SkillDefinition CreateCopy(AttackData attack)
    {
        var copy = Instantiate(this);
        copy.name = name;
        copy._attack = attack;
        return copy;
    }
}
