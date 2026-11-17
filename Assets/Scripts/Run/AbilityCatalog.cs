using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>能力えらびのカード1枚（どのアプリから来た、どの能力か）</summary>
public struct AbilityCandidate
{
    public SkillDefinition Skill;
    public SkillCategory Category;
    /// <summary>元になったアプリの名前。使用データに無いカテゴリで埋めたカードは null</summary>
    public string AppName;
    /// <summary>そのアプリの使用時間（秒）。埋めたカードは 0</summary>
    public double Seconds;
    public bool IsFiller => string.IsNullOrEmpty(AppName);
}

/// <summary>使用データの1行（名前・バンドルID・秒）。スクリーンタイムは名前だけ、ファイルや手入力はバンドルIDもある</summary>
public struct AppUsageRow
{
    public string Name;
    public string BundleId;
    public double Seconds;
}

/// <summary>カテゴリ1つ分：そのカテゴリの能力と、カテゴリに入るアプリ</summary>
[Serializable]
public class AbilityCategoryEntry
{
    public SkillCategory Category = SkillCategory.Unknown;
    [Tooltip("このカテゴリのアプリから出る能力")]
    public SkillDefinition Skill;
    [Tooltip("スクリーンタイムでの名前（大文字小文字は区別しない。単語ごとに合わせる：「Adobe Photoshop 2025」は「photoshop」に当たる）")]
    public string[] AppNames;
    [Tooltip("バンドルID（前方一致）。ファイル・サンプル・手入力の使用データ用")]
    public string[] BundleIdPrefixes;

    public bool Matches(AppUsageRow row)
    {
        if (!string.IsNullOrEmpty(row.BundleId) && BundleIdPrefixes != null)
        {
            foreach (var p in BundleIdPrefixes)
            {
                if (!string.IsNullOrEmpty(p) && row.BundleId.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        if (string.IsNullOrWhiteSpace(row.Name) || AppNames == null) return false;
        string n = row.Name.Trim().ToLowerInvariant();
        foreach (var a in AppNames)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            string alias = a.Trim().ToLowerInvariant();
            if (n == alias || n.StartsWith(alias + " ") || n.EndsWith(" " + alias) || n.Contains(" " + alias + " ")) return true;
        }
        return false;
    }
}

/// <summary>
/// カテゴリごとの能力の表（2026-11-17 決定：ゲームの流れ）。
/// ・使用時間の長い順にアプリをカテゴリへ振り分け、そのカテゴリの能力をカードにする（同じカテゴリは1枚にまとめる）
/// ・カテゴリが分からないアプリ・能力の無いカテゴリは飛ばす
/// ・5枚に足りなければ、まだ出ていないカテゴリの能力で埋める（いつも5枚から選べる）
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Ability Catalog
/// </summary>
[CreateAssetMenu(fileName = "AbilityCatalog", menuName = "Team1/Ability Catalog")]
public class AbilityCatalog : ScriptableObject
{
    [Tooltip("並べる枚数")]
    [SerializeField, Min(1)] int _cardCount = 5;
    [Tooltip("選ぶ枚数")]
    [SerializeField, Min(1)] int _pickCount = 3;
    [SerializeField] AbilityCategoryEntry[] _categories;

    public int CardCount => _cardCount;
    public int PickCount => _pickCount;
    public AbilityCategoryEntry[] Categories => _categories ?? Array.Empty<AbilityCategoryEntry>();

    /// <summary>このアプリのカテゴリ（能力のあるカテゴリだけ）。無ければ null</summary>
    public AbilityCategoryEntry Find(AppUsageRow row) => Categories.FirstOrDefault(c => c != null && c.Skill != null && c.Matches(row));

    /// <summary>この能力のカテゴリ。無ければ null</summary>
    public AbilityCategoryEntry FindBySkill(SkillDefinition skill) => Categories.FirstOrDefault(c => c != null && c.Skill == skill);

    /// <summary>能力えらびのカードを作る（使用時間の長い順）</summary>
    public List<AbilityCandidate> BuildCandidates(IEnumerable<AppUsageRow> rows)
    {
        var cards = new List<AbilityCandidate>();
        var used = new HashSet<SkillCategory>();
        foreach (var row in (rows ?? Enumerable.Empty<AppUsageRow>()).Where(r => r.Seconds > 0).OrderByDescending(r => r.Seconds))
        {
            if (cards.Count >= _cardCount) break;
            AbilityCategoryEntry entry = Find(row);
            if (entry == null || !used.Add(entry.Category)) continue;
            cards.Add(new AbilityCandidate { Skill = entry.Skill, Category = entry.Category, AppName = row.Name, Seconds = row.Seconds });
        }
        foreach (var entry in Categories)
        {
            if (cards.Count >= _cardCount) break;
            if (entry == null || entry.Skill == null || !used.Add(entry.Category)) continue;
            cards.Add(new AbilityCandidate { Skill = entry.Skill, Category = entry.Category });
        }
        return cards;
    }
}
