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

    public bool Matches(AppUsageRow row) => AppMatch.Matches(row, AppNames, BundleIdPrefixes);
}

/// <summary>
/// アプリ専用の能力1つ分（2026-11-18 まさぴ〜決定：Zoom・Finder・Premiere Pro。中身はチームで決める）。
/// 当たったアプリは、カテゴリの能力より先にこの能力を使う（能力えらびのカード・ボスを倒したときの能力・図鑑）。
/// 中身を変えるときは、能力のアセット（名前・アイコン・攻撃データ）を書き換える。アイコンが空なら、アプリのアイコンで代わりに出す
/// </summary>
[Serializable]
public class AppSkillEntry
{
    [Tooltip("どのアプリか（確認用の名前）")]
    public string Label;
    [Tooltip("このアプリの能力")]
    public SkillDefinition Skill;
    [Tooltip("スクリーンタイムでの名前（大文字小文字は区別しない。単語ごとに合わせる：「Adobe Premiere Pro 2025」は「premiere pro」に当たる）")]
    public string[] AppNames;
    [Tooltip("バンドルID（前方一致）")]
    public string[] BundleIdPrefixes;
    [Tooltip("【仮】アプリのキットの行か（2026-11-19。AppKitsBuilder が入れる）。AppKits.Enabled = false・キットの能力のスイッチが切れていれば使わない")]
    public bool FromKit;

    public bool Matches(AppUsageRow row) => AppMatch.Matches(row, AppNames, BundleIdPrefixes);

    /// <summary>今使ってよい行か（キットの行は、キットのスイッチを見る）</summary>
    public bool IsActive => !FromKit || AppKits.IsOn(Label, KitPart.Skill);
}

/// <summary>アプリの見分け方（バンドルIDの前方一致か、名前を単語ごとに）</summary>
public static class AppMatch
{
    public static bool Matches(AppUsageRow row, string[] names, string[] bundleIdPrefixes)
    {
        if (!string.IsNullOrEmpty(row.BundleId) && bundleIdPrefixes != null)
        {
            foreach (var p in bundleIdPrefixes)
            {
                if (!string.IsNullOrEmpty(p) && row.BundleId.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        if (string.IsNullOrWhiteSpace(row.Name) || names == null) return false;
        string n = row.Name.Trim().ToLowerInvariant();
        foreach (var a in names)
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
/// ・アプリ専用の能力があるアプリ（Zoom・Finder・Premiere Pro。2026-11-18）は、カテゴリより先にその能力にする（カテゴリの1枚とは別に数える）
/// ・カテゴリが分からないアプリ・能力の無いカテゴリは、その場で能力を作る
/// ・枚数に足りなければ、まだ出ていないカテゴリの能力で埋める
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Ability Catalog
/// </summary>
[CreateAssetMenu(fileName = "AbilityCatalog", menuName = "Team1/Ability Catalog")]
public class AbilityCatalog : ScriptableObject
{
    [Tooltip("並べる枚数")]
    [SerializeField, Min(1)] int _cardCount = 3;
    [Tooltip("選ぶ枚数")]
    [SerializeField, Min(1)] int _pickCount = 1;
    [SerializeField] AbilityCategoryEntry[] _categories;
    [Tooltip("アプリ専用の能力（2026-11-18：Zoom・Finder・Premiere Pro）。カテゴリより先に使う")]
    [SerializeField] AppSkillEntry[] _appSkills;

    [Header("対応表に無いアプリ（その場で能力を作る。2026-11-17）")]
    [Tooltip("ひな形（基盤の能力）。アプリの名前から1つ選ぶ（同じアプリなら、いつも同じ）")]
    [SerializeField] AbilityTemplate[] _templates;
    [Tooltip("1秒あたりのゲージの増え方の目安。作った能力のクールタイムを、これに合わせて決める")]
    [SerializeField, Min(0.1f)] float _gaugePerSecond = 4f;
    [Tooltip("作った能力のクールタイムの範囲（秒）")]
    [SerializeField] Vector2 _cooldownRange = new Vector2(2.5f, 6f);
    [Tooltip("カードにもボスにもしないアプリの名前（大文字小文字は区別しない）")]
    [SerializeField] string[] _excludedNames = { "System Settings", "System Preferences", "システム設定", "設定", "Team1" };
    [Tooltip("カードにもボスにもしないアプリのバンドルID（前方一致）。Finder は 2026-11-18 から専用の能力があるので外した")]
    [SerializeField] string[] _excludedBundleIds = { "com.apple.systempreferences" };

    public int CardCount => _cardCount;
    public int PickCount => _pickCount;
    public AbilityCategoryEntry[] Categories => _categories ?? Array.Empty<AbilityCategoryEntry>();
    public AppSkillEntry[] AppSkills => _appSkills ?? Array.Empty<AppSkillEntry>();
    public AbilityTemplate[] Templates => _templates ?? Array.Empty<AbilityTemplate>();
    public float GaugePerSecond => _gaugePerSecond;
    public Vector2 CooldownRange => _cooldownRange;

    /// <summary>
    /// カードにもボスにもしないアプリか。
    /// ・除外の表にある（システム設定・このゲーム。Finder は 2026-11-18 から除外しない）
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

    /// <summary>このアプリ専用の能力（能力が入っているものだけ）。無ければ null</summary>
    /// <remarks>
    /// 2026-11-19：キットの行はスイッチを見る。ラスボス（Terminal）の能力の行は、ここでは返さない
    /// （カードは AddFinalBossCard だけが、図鑑で倒していれば1枚だけ出す。2枚にならないように）
    /// </remarks>
    public AppSkillEntry FindAppSkill(AppUsageRow row)
    {
        SkillDefinition finalSkill = FinalBossSkill(out _);
        return AppSkills.FirstOrDefault(a => a != null && a.Skill != null && a.IsActive && a.Skill != finalSkill && a.Matches(row));
    }

    /// <summary>ラスボス（Terminal）の手に入る能力（無ければ null）。final にラスボスの行</summary>
    static SkillDefinition FinalBossSkill(out AppEntry final)
    {
        AppCatalog catalog = GameSession.Instance != null ? GameSession.Instance.Catalog : null;
        final = catalog != null ? catalog.FinalBoss : null;
        return final != null && final.Profile != null ? final.Profile.Reward : null;
    }

    /// <summary>
    /// このアプリから出る能力：専用の能力 → カテゴリの能力の順。どちらも無ければ null（その場で作る能力は AppVisuals.Generate）
    /// </summary>
    public SkillDefinition SkillFor(AppUsageRow row) => FindAppSkill(row)?.Skill ?? Find(row)?.Skill;

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
        var usedAppSkills = new HashSet<SkillDefinition>();
        var usedTemplates = new HashSet<int>();
        foreach (var row in (rows ?? Enumerable.Empty<AppUsageRow>()).Where(r => r.Seconds > 0).OrderByDescending(r => r.Seconds))
        {
            if (cards.Count >= _cardCount) break;
            if (IsExcluded(row)) continue;
            AppSkillEntry own = FindAppSkill(row);
            if (own != null)
            {
                // アプリ専用の能力：カテゴリの1枚とは別に数える（同じ能力は1枚だけ）
                if (!usedAppSkills.Add(own.Skill)) continue;
                cards.Add(new AbilityCandidate { Skill = own.Skill, Category = own.Skill.Category, AppName = row.Name, Seconds = row.Seconds, AppIcon = appIcon?.Invoke(row) });
                continue;
            }
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
        AddFinalBossCard(cards, appIcon);
        return cards;
    }

    /// <summary>
    /// 【仮】ラスボス（Terminal）の能力のカード（2026-11-19。docs/app-feedback-v2-design.md 6-3 の (ア)）。
    /// 図鑑で Terminal を倒していたら、いちばん使用時間の短いアプリの1枚を Terminal のカードに入れ替える（枚数は同じ）。
    /// スクリーンタイムに Terminal があっても無くても1枚だけ（FindAppSkill はこの能力を返さない）。倒す前は出さない。
    /// 能力は Boss_Terminal の「手に入る能力」（無ければ何もしない）。キットのスイッチ（AppKits の "Terminal" の能力）が切れていれば出さない
    /// </summary>
    void AddFinalBossCard(List<AbilityCandidate> cards, Func<AppUsageRow, Sprite> appIcon)
    {
        try
        {
            SkillDefinition skill = FinalBossSkill(out AppEntry final);
            if (skill == null || cards.Exists(c => c.Skill == skill)) return;
            string bundle = final.BundleIdPrefixes != null && final.BundleIdPrefixes.Length > 0 ? final.BundleIdPrefixes[0] : "";
            string label = AppKits.FindLabel(final.DisplayName, bundle) ?? final.DisplayName;
            if (!AppKits.IsOn(label, KitPart.Skill)) return;
            if (!DefeatedInDex(final)) return;
            var row = new AppUsageRow { Name = final.DisplayName, BundleId = bundle ?? "", Seconds = 1 };
            var card = new AbilityCandidate { Skill = skill, Category = skill.Category, AppName = final.DisplayName, Seconds = 0, AppIcon = appIcon?.Invoke(row) };
            if (cards.Count < _cardCount) { cards.Add(card); return; }
            // いちばん使用時間の短いアプリのカード（アプリの無い埋め草のカードがあれば、それを先に）
            int at = cards.FindLastIndex(c => string.IsNullOrEmpty(c.AppName));
            if (at < 0)
            {
                at = cards.Count - 1;
                for (int i = 0; i < cards.Count; i++) if (cards[i].Seconds < cards[at].Seconds) at = i;
            }
            cards[at] = card;
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>図鑑で、そのアプリを倒したことがあるか（表示名・別名・バンドルIDのどれかで当たる記録）</summary>
    static bool DefeatedInDex(AppEntry app)
    {
        var names = new List<string> { app.DisplayName };
        if (app.ScreenTimeNames != null) names.AddRange(app.ScreenTimeNames);
        foreach (AppDexEntry e in AppDex.Entries)
        {
            if (e == null || !e.IsDefeated) continue;
            if (AppMatch.Matches(e.Row, names.ToArray(), app.BundleIdPrefixes)) return true;
        }
        return false;
    }
}
