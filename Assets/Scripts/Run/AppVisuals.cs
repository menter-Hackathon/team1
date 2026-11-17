using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>アプリ1つ分の見た目（アイコンのドット絵・色）と、その場で作った能力</summary>
public class AppVisual
{
    public string Key;
    public string Name;
    /// <summary>16 ドット（カード・能力の枠）</summary>
    public Sprite Icon16;
    /// <summary>24 ドット（ボスのウィンドウ）</summary>
    public Sprite Icon24;
    /// <summary>12 ドット（ボスのゲージの横）</summary>
    public Sprite Icon12;
    /// <summary>アイコンの色から作った色の組み合わせ</summary>
    public AppTheme Theme;
    /// <summary>Mac から本物のアイコンを読めたか（false なら仮のアイコン）</summary>
    public bool HasRealIcon;
    /// <summary>その場で作った能力（作っていなければ null）。カードとボスの報酬で、同じものを使う</summary>
    public SkillDefinition Generated;
    /// <summary>作った能力のひな形の番号（-1＝まだ作っていない）</summary>
    public int TemplateIndex = -1;
}

/// <summary>
/// アプリごとの見た目と、その場で作る能力（2026-11-17：アイコンからドット絵・対応表に無いアプリの能力）。
/// ・Prepare：アイコンをまとめて読み込み、ドット絵（16・24 ドット）と色の組み合わせを作る。読めなかったアプリは、名前から決まる色の仮のアイコン
/// ・Generate：ひな形（基盤の能力）を1つ選び、数値を少し変え、効果の絵をアプリの色に塗り替え、クールタイムを強さに合わせて決める
/// 作った物はアプリごとに覚えておき、ゲームを閉じるまで同じものを使う（同じアプリなら、いつも同じ能力・同じ色）
/// </summary>
public static class AppVisuals
{
    static readonly Dictionary<string, AppVisual> Cache = new Dictionary<string, AppVisual>();

    /// <summary>アプリのキー（バンドルIDがあればそれ、無ければ名前から）</summary>
    public static string KeyOf(AppUsageRow row) => "app_" + PixelIcon.Hash(!string.IsNullOrEmpty(row.BundleId) ? row.BundleId : row.Name).ToString("x8");

    /// <summary>見た目を用意する（まだのアプリだけ、アイコンをまとめて1回で読む）</summary>
    public static void Prepare(IEnumerable<AppUsageRow> rows)
    {
        var need = new List<AppUsageRow>();
        foreach (var r in rows)
        {
            string key = KeyOf(r);
            if (!Cache.ContainsKey(key) && !need.Any(n => KeyOf(n) == key)) need.Add(r);
        }
        if (need.Count == 0) return;
        Dictionary<string, Texture2D> icons = AppIcons.Fetch(need.Select(r => new AppIcons.Request { Key = KeyOf(r), BundleId = r.BundleId, Name = r.Name }));
        foreach (var r in need)
        {
            string key = KeyOf(r);
            var v = new AppVisual { Key = key, Name = r.Name };
            if (icons.TryGetValue(key, out Texture2D tex))
            {
                v.Icon16 = PixelIcon.Pixelize(tex, 16, $"AppIcon16_{r.Name}");
                v.Icon24 = PixelIcon.Pixelize(tex, 24, $"AppIcon24_{r.Name}");
                v.Icon12 = PixelIcon.Pixelize(tex, 12, $"AppIcon12_{r.Name}");
                v.HasRealIcon = v.Icon16 != null && v.Icon24 != null && v.Icon12 != null;
            }
            Color main;
            Color[] accents;
            if (v.HasRealIcon)
            {
                PixelIcon.ExtractColors(v.Icon24, out main, out accents);
            }
            else
            {
                main = PixelIcon.ColorFromName(r.Name);
                accents = new[] { main };
                v.Icon16 = PixelIcon.Placeholder(main, 16, $"AppIcon16_{r.Name}");
                v.Icon24 = PixelIcon.Placeholder(main, 24, $"AppIcon24_{r.Name}");
                v.Icon12 = PixelIcon.Placeholder(main, 12, $"AppIcon12_{r.Name}");
            }
            v.Theme = AppTheme.CreateRuntime($"Theme_{r.Name}", main, accents);
            Cache[key] = v;
        }
    }

    /// <summary>用意した見た目（無ければ、その場で仮のアイコンを作る）</summary>
    public static AppVisual Get(AppUsageRow row)
    {
        if (!Cache.TryGetValue(KeyOf(row), out AppVisual v)) Prepare(new[] { row });
        return Cache.TryGetValue(KeyOf(row), out v) ? v : null;
    }

    /// <summary>
    /// 対応表に無いアプリの能力を作る（作ってあれば、それを返す）。
    /// usedTemplates：このランのカードで使ったひな形。同じひな形が並ばないよう、使ったものは次の番号にずらす
    /// </summary>
    public static SkillDefinition Generate(AppUsageRow row, AbilityCatalog catalog, HashSet<int> usedTemplates)
    {
        AppVisual v = Get(row);
        if (v == null || catalog == null || catalog.Templates.Length == 0) return null;
        if (v.Generated != null)
        {
            usedTemplates?.Add(v.TemplateIndex);
            return v.Generated;
        }
        uint hash = PixelIcon.Hash(row.Name);
        int n = catalog.Templates.Length;
        int index = (int)(hash % (uint)n);
        for (int i = 0; i < n && usedTemplates != null && usedTemplates.Contains(index); i++) index = (index + 1) % n;
        float variation = ((hash >> 12) % 1000) / 999f;
        SkillDefinition skill = AbilityForge.Create(catalog, index, variation, v.Icon16, v.Theme);
        if (skill == null) return null;
        v.Generated = skill;
        v.TemplateIndex = index;
        usedTemplates?.Add(index);
        return skill;
    }
}

/// <summary>ひな形から能力を作る（AppVisuals から使う。確認用に、ひな形と違いを指定して直接作ることもできる）</summary>
public static class AbilityForge
{
    /// <summary>ひな形 index を、違い variation（0〜1）で作り、theme の色に塗り替える</summary>
    public static SkillDefinition Create(AbilityCatalog catalog, int index, float variation, Sprite icon, AppTheme theme)
    {
        if (catalog == null || index < 0 || index >= catalog.Templates.Length) return null;
        AbilityTemplate t = catalog.Templates[index];
        if (t == null || t.Attack == null) return null;
        AttackData attack = t.VaryStat != AttackStat.None
            ? t.Attack.CreateScaledCopy(t.VaryStat, Mathf.Lerp(t.VaryMin, t.VaryMax, Mathf.Clamp01(variation)))
            : t.Attack;
        attack = attack.CreateRecoloredCopy(s => ThemeRecolor.Effect(s, theme));
        // クールタイム：1回で増えるゲージの目安 ÷ 1秒あたりの目安（強さが偏らないように）
        Vector2 range = catalog.CooldownRange;
        float cooldown = Mathf.Clamp(ExpectedGauge(attack) / catalog.GaugePerSecond, range.x, range.y);
        attack = attack.CreateScaledCopy(AttackStat.Cooldown, cooldown);
        return SkillDefinition.CreateRuntime(t.Name, icon, SkillCategory.Unknown, attack, theme);
    }

    /// <summary>1回使ったときに増えるゲージの目安（全部当たったとき。弾が複数なら7割、残る判定は当たり直す回数）</summary>
    public static float ExpectedGauge(AttackData a)
    {
        if (a == null) return 0f;
        switch (a.Kind)
        {
            case AttackKind.Area:
                float g = a.GaugeGain * Mathf.Max(1, a.AreaCount);
                if (a.PersistDuration > 0f && a.TickInterval > 0f) g *= Mathf.Max(1f, a.PersistDuration / a.TickInterval);
                return g;
            case AttackKind.Projectile:
                float rate = a.ProjectileCount > 1 ? 0.7f : 1f;
                return (a.GaugeGain + ExpectedGauge(a.OnTerrainHit)) * Mathf.Max(1, a.ProjectileCount) * rate;
            default:
                return 0f;
        }
    }
}
