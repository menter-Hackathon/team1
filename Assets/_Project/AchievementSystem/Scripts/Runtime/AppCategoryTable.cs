using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// アプリ名とカテゴリの対応表（app_categories.csv から作る）。デトックス（エンタメ・SNS）と週末エンジニア（作業）に使う。
    /// match_key が空ならアプリ名で照合する（「Excel / Word / PowerPoint」は「 / 」で分けて、それぞれの名前で照合）。
    /// match_key に複数の名前を入れるときは「|」で区切る。表にないアプリは「その他」で、どちらの対象にもしない。
    /// </summary>
    [CreateAssetMenu(fileName = "AppCategoryTable", menuName = "Team1/Achievements/App Category Table")]
    public class AppCategoryTable : ScriptableObject, IAppCategoryResolver
    {
        [Serializable]
        public class Row
        {
            public string appName = "";
            public AppUsageCategory category;
            [Tooltip("CSV の detox_target（表示用。デトックスの対象はカテゴリで決める）")]
            public bool detoxTarget;
            [Tooltip("スクリーンタイムで取れるアプリ名。空ならアプリ名で照合。複数は「|」区切り")]
            public string matchKey = "";
            public string note = "";
        }

        [SerializeField] List<Row> _rows = new List<Row>();

        AppCategoryMap _map;

        public IReadOnlyList<Row> Rows => _rows;

        public AppUsageCategory Resolve(string appName) => Map.Resolve(appName);

        AppCategoryMap Map
        {
            get
            {
                if (_map != null) return _map;
                _map = new AppCategoryMap();
                foreach (var r in _rows)
                    if (r != null) _map.Add(r.category, AppCategoryMap.KeysOf(r.appName, r.matchKey));
                return _map;
            }
        }

        void OnValidate() => _map = null;

#if UNITY_EDITOR
        public void EditorSetRows(List<Row> rows)
        {
            _rows = rows ?? new List<Row>();
            _map = null;
        }
#endif
    }
}
