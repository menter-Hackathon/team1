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
    /// <summary>元になったアプリのアイコン（16 ドットのドット絵）。無ければ null</summary>
    public Sprite AppIcon;
    /// <summary>対応表に無いアプリから、その場で作った能力か</summary>
    public bool IsGenerated;
    public bool IsFiller => string.IsNullOrEmpty(AppName);
}

/// <summary>その場で作る能力のひな形（基盤の能力）。効果の絵はアプリの色に塗り替え、数値をアプリごとに少し変える</summary>
[Serializable]
public class AbilityTemplate
{
    [Tooltip("能力の名前（カードに出す）")]
    public string Name;
    [Tooltip("元にする攻撃データ")]
    public AttackData Attack;
    [Tooltip("アプリごとに変える数値（変えないなら None）")]
    public AttackStat VaryStat = AttackStat.None;
    [Tooltip("変える数値の範囲（アプリの名前から、この間のどこかに決まる）")]
    public float VaryMin = 1f;
    public float VaryMax = 1f;
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
    [SerializeField, Min(1)] int _pickCount = 1;
    [SerializeField] AbilityCategoryEntry[] _categories;

    [Header("対応表に無いアプリ（その場で能力を作る。2026-11-17）")]
    [Tooltip("ひな形（基盤の能力）。アプリの名前から1つ選ぶ（同じアプリなら、いつも同じ）")]
    [SerializeField] AbilityTemplate[] _templates;
    [Tooltip("1秒あたりのゲージの増え方の目安。作った能力のクールタイムを、これに合わせて決める")]
    [SerializeField, Min(0.1f)] float _gaugePerSecond = 4f;
    [Tooltip("作った能力のクールタイムの範囲（秒）")]
    [SerializeField] Vector2 _cooldownRange = new Vector2(2.5f, 6f);
    [Tooltip("カードにもボスにもしないアプリの名前（大文字小文字は区別しない）")]
    [SerializeField] string[] _excludedNames = { "Finder", "System Settings", "System Preferences", "システム設定", "設定", "Team1" };
    [Tooltip("カードにもボスにもしないアプリのバンドルID（前方一致）")]
    [SerializeField] string[] _excludedBundleIds = { "com.apple.finder", "com.apple.systempreferences" };

    public int CardCount => _cardCount;
    public int PickCount => _pickCount;
    public AbilityCategoryEntry[] Categories => _categories ?? Array.Empty<AbilityCategoryEntry>();
    public AbilityTemplate[] Templates => _templates ?? Array.Empty<AbilityTemplate>();
    public float GaugePerSecond => _gaugePerSecond;
    public Vector2 CooldownRange => _cooldownRange;

    /// <summary>
    /// カードにもボスにもしないアプリか。
    /// ・除外の表にある（Finder・システム設定・このゲーム）
    /// ・名前がバンドルIDの形（「com.google.chrome.ios」など）：Mac に無い、ほかのデバイス（iPhone）のアプリ。この Mac の使用時間ではないので数えない
    /// </summary>
    public bool IsExcluded(AppUsageRow row)
    {
        string name = (row.Name ?? "").Trim();
        if (_excludedNames != null && Array.Exists(_excludedNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) return true;
        if (!string.IsNullOrEmpty(row.BundleId) && _excludedBundleIds != null &&
            Array.Exists(_excludedBundleIds, p => !string.IsNullOrEmpty(p) && row.BundleId.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return true;
        return LooksLikeBundleId(name);
    }

    static bool LooksLikeBundleId(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf(' ') >= 0) return false;
        int dots = 0;
        foreach (char c in name)
        {
            if (c == '.') dots++;
            else if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return dots >= 2;
    }

    /// <summary>このアプリのカテゴリ（能力のあるカテゴリだけ）。無ければ null</summary>
    public AbilityCategoryEntry Find(AppUsageRow row) => Categories.FirstOrDefault(c => c != null && c.Skill != null && c.Matches(row));

    /// <summary>この能力のカテゴリ。無ければ null</summary>
    public AbilityCategoryEntry FindBySkill(SkillDefinition skill) => Categories.FirstOrDefault(c => c != null && c.Skill == skill);

    /// <summary>
    /// 能力えらびのカードを作る（使用時間の長い順）。
    /// appIcon：アプリのアイコン（カードに出す）。generate：カテゴリの分からないアプリの能力をその場で作る（使ったひな形の番号を渡す。作れなければ null）
    /// </summary>
    public List<AbilityCandidate> BuildCandidates(IEnumerable<AppUsageRow> rows,
        Func<AppUsageRow, Sprite> appIcon = null, Func<AppUsageRow, HashSet<int>, SkillDefinition> generate = null)
    {
        var cards = new List<AbilityCandidate>();
        var used = new HashSet<SkillCategory>();
        var usedTemplates = new HashSet<int>();
        foreach (var row in (rows ?? Enumerable.Empty<AppUsageRow>()).Where(r => r.Seconds > 0).OrderByDescending(r => r.Seconds))
        {
            if (cards.Count >= _cardCount) break;
            if (IsExcluded(row)) continue;
            AbilityCategoryEntry entry = Find(row);
            if (entry != null)
            {
                if (!used.Add(entry.Category)) continue;
                cards.Add(new AbilityCandidate { Skill = entry.Skill, Category = entry.Category, AppName = row.Name, Seconds = row.Seconds, AppIcon = appIcon?.Invoke(row) });
                continue;
            }
            SkillDefinition made = generate?.Invoke(row, usedTemplates);
            if (made == null) continue;
            cards.Add(new AbilityCandidate { Skill = made, Category = made.Category, AppName = row.Name, Seconds = row.Seconds, AppIcon = appIcon?.Invoke(row), IsGenerated = true });
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
