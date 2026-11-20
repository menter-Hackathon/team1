using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// 実績1件の定義。achievements.csv からエディタ拡張（Tools ＞ Achievements ＞ CSV から作る）で作る。
    /// Steam スキルの AchievementDefinition（グローバル名前空間）とは別物。名前空間 Team1.Achievements で分けている。
    /// 条件（Condition）は [SerializeReference] で持つ。Inspector で、朝型の時間帯などの値を変えられる。
    /// </summary>
    [CreateAssetMenu(fileName = "Ach_", menuName = "Team1/Achievements/Definition")]
    public class AchievementDefinition : ScriptableObject
    {
        [SerializeField] string _id = "";
        [SerializeField] string _displayName = "";
        [SerializeField, TextArea(2, 4)] string _description = "";
        [Tooltip("一覧に出す、褒める1行（「朝にPCに触れた！」）")]
        [SerializeField] string _praise = "";
        [SerializeField] AchievementCategory _category;
        [SerializeField] AchievementRank _rank;
        [SerializeField] AchievementTrigger _trigger;
        [Tooltip("CSV の condition_type")]
        [SerializeField] string _conditionType = "";
        [Tooltip("目標値（日数・回数・割合・分）")]
        [SerializeField] double _target = 1;
        [Tooltip("ゲームへの影響（表示だけ。新入りのみ）")]
        [SerializeField] string _gameEffect = "";
        [SerializeField] Sprite _icon;
        [SerializeField] Sprite _iconLocked;
        [SerializeReference] IAchievementCondition _condition;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public string Praise => _praise;
        public AchievementCategory Category => _category;
        public AchievementRank Rank => _rank;
        public AchievementTrigger Trigger => _trigger;
        public double Target => _target;
        public Sprite Icon => _icon;
        public Sprite IconLocked => _iconLocked;
        public IAchievementCondition Condition => _condition;

        public AchievementSpec ToSpec() => new AchievementSpec
        {
            Id = _id,
            Name = _displayName,
            Description = _description,
            Praise = _praise,
            Category = _category,
            Rank = _rank,
            Trigger = _trigger,
            Target = _target,
            Condition = _condition ?? AchievementConditions.Create(_conditionType),
            GameEffect = _gameEffect,
        };

#if UNITY_EDITOR
        /// <summary>CSV から作るとき（エディタだけ）。条件は種類が変わったときだけ作り直す（Inspector で変えた値を残す）</summary>
        public void EditorSetup(string id, string displayName, string description, string praise, AchievementCategory category, AchievementRank rank,
            AchievementTrigger trigger, string conditionType, double target, string gameEffect, Sprite icon, Sprite iconLocked)
        {
            _id = id;
            _displayName = displayName;
            _description = description;
            _praise = praise;
            _category = category;
            _rank = rank;
            _trigger = trigger;
            if (_condition == null || _conditionType != conditionType) _condition = AchievementConditions.Create(conditionType);
            _conditionType = conditionType;
            _target = target;
            _gameEffect = gameEffect;
            _icon = icon;
            _iconLocked = iconLocked;
        }
#endif
    }
}
