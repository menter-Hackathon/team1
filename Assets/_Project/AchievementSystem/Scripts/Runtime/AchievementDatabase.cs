using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// 実績の定義・カテゴリ表・ランク枠をまとめたもの。Resources/AchievementDatabase に置き、AchievementService が読む。
    /// CSV から作るエディタ拡張が、中身を入れる。
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementDatabase", menuName = "Team1/Achievements/Database")]
    public class AchievementDatabase : ScriptableObject
    {
        public const string ResourcePath = "AchievementDatabase";

        [SerializeField] List<AchievementDefinition> _definitions = new List<AchievementDefinition>();
        [SerializeField] AppCategoryTable _categories;
        [Header("ランク枠")]
        [SerializeField] Sprite _frameBronze;
        [SerializeField] Sprite _frameSilver;
        [SerializeField] Sprite _frameGold;
        [Header("調整できる値")]
        [Tooltip("プレイ中の吹き出しを出す秒数")]
        [SerializeField, Min(0.5f)] float _popupSeconds = 3f;
        [Tooltip("使用時間TOP10として数えるアプリの数（ボスの候補と同じ数）")]
        [SerializeField, Min(1)] int _topAppCount = 10;

        public IReadOnlyList<AchievementDefinition> Definitions => _definitions;
        public AppCategoryTable Categories => _categories;
        public float PopupSeconds => _popupSeconds;
        public int TopAppCount => _topAppCount;

        public AchievementDefinition Find(string id) => _definitions.FirstOrDefault(d => d != null && d.Id == id);

        public Sprite Frame(AchievementRank rank) => rank switch
        {
            AchievementRank.Gold => _frameGold,
            AchievementRank.Silver => _frameSilver,
            _ => _frameBronze
        };

        public static AchievementDatabase Load() => Resources.Load<AchievementDatabase>(ResourcePath);

#if UNITY_EDITOR
        public void EditorSet(List<AchievementDefinition> definitions, AppCategoryTable categories, Sprite bronze, Sprite silver, Sprite gold)
        {
            _definitions = definitions;
            _categories = categories;
            _frameBronze = bronze;
            _frameSilver = silver;
            _frameGold = gold;
        }
#endif
    }
}
