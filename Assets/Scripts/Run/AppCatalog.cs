using System;
using UnityEngine;

/// <summary>整数の範囲（使用時間による強さの最小〜最大）</summary>
[Serializable]
public struct IntRange
{
    public int Min;
    public int Max;
    public IntRange(int min, int max) { Min = min; Max = max; }
    /// <summary>強さ t（0〜1）で最小〜最大の間の値を返す</summary>
    public int Lerp(float t) => Mathf.RoundToInt(Mathf.Lerp(Min, Max, t));
}

/// <summary>小数の範囲（使用時間による強さの最小〜最大）。最小より最大が小さくてもよい（待ち時間を短くするときなど）</summary>
[Serializable]
public struct FloatRange
{
    public float Min;
    public float Max;
    public FloatRange(float min, float max) { Min = min; Max = max; }
    public float Lerp(float t) => Mathf.Lerp(Min, Max, t);
}

/// <summary>
/// 代用ボスのアプリに、専用のボスの設定（技）を使う（2026-11-18：Finder・Premiere Pro。docs/app-boss-skills-design.md 1-5）。
/// 見た目（アイコンから作るウィンドウ）・ステージ（代用ボスの1画面）・ステージの色・手に入る能力は、代用ボスのまま。技と強さだけを差し替える
/// </summary>
[Serializable]
public class AppBossEntry
{
    [Tooltip("表の上で見分けるための名前（ゲームには出ない）")]
    public string Label;
    [Tooltip("アプリの名前（単語ごとに比べる。大文字小文字は区別しない）")]
    public string[] AppNames;
    [Tooltip("バンドルIDの前方一致")]
    public string[] BundleIdPrefixes;
    [Tooltip("ボスのプレハブ（動き方に合う物。浮くなら Chrome のもの）。空なら、代用ボスのひな形のプレハブ")]
    public BossController BossPrefab;
    [Tooltip("ボスの設定（技・形態）")]
    public BossProfile Profile;
    [Tooltip("ボスのウイルス解除ゲージの最大値（使用時間で変える）")]
    public IntRange GaugeMax;
    [Tooltip("第2形態の、攻撃の後の待ち時間（秒。使用時間で変える）")]
    public FloatRange Phase2Wait;
    [Tooltip("【仮】アプリのキットの行か（2026-11-19。AppKitsBuilder が入れる）。AppKits.Enabled = false・キットのボスのスイッチが切れていれば使わない")]
    public bool FromKit;

    public bool Matches(AppUsageRow row) => AppMatch.Matches(row, AppNames, BundleIdPrefixes);

    /// <summary>今使ってよい行か（キットの行は、キットのスイッチを見る）</summary>
    public bool IsActive => !(FromKit || (Profile != null && Profile.name.StartsWith("Boss_Kit_"))) || AppKits.IsOn(Label, KitPart.Boss);
}

/// <summary>
/// 対応アプリ1つ分。バンドルIDの照合、出てくるボス、強さのつまみを持つ。
/// </summary>
[Serializable]
public class AppEntry
{
    [Tooltip("画面に出す名前")]
    public string DisplayName;
    [Tooltip("バンドルID。前方一致で照合する（例：com.adobe.PremierePro は com.adobe.PremierePro.26 にも当たる）")]
    public string[] BundleIdPrefixes;
    [Tooltip("スクリーンタイム上での別名。Display Name と異なる名前があるときに追加する")]
    public string[] ScreenTimeNames;
    public SkillCategory Category = SkillCategory.Unknown;

    [Header("ボス")]
    [Tooltip("ボスのプレハブ（非表示で保存しておく）")]
    public BossController BossPrefab;
    [Tooltip("ボスの設定。使用時間で数値を変えた複製を作って渡す。手に入る能力もここから読む")]
    public BossProfile Profile;
    [Tooltip("【仮】本物のこのアプリのときだけ使うボスの設定（2026-11-19：Chrome・VS Code の専用の技）。空なら Profile。代用ボスは Profile を借りるので、こちらは使わない")]
    public BossProfile OwnProfile;
    [Tooltip("オン：対応表に無いアプリの代用ボスの、ひな形にしない（Office・Unity・Steam のボスは、そのアプリ専用の技なので。2026-11-18）")]
    public bool ExcludeFromStandIn;
    [Tooltip("オン：着ているウィンドウとゲージの横のアイコンを、Mac の本物のアプリのアイコンから作る（代用ボス・Terminal と同じ作り方。2026-11-19）。" +
             "ステージの色が空なら、アイコンの色を使う")]
    public bool IconLook;
    [Tooltip("ボスが出てくる位置（部屋の床の真ん中が (0, 0)）")]
    public Vector2 SpawnPosition;
    [Tooltip("このアプリのステージのシーン名（Build Settings に登録しておく）")]
    public string StageScene = "Stage_Desktop";
    [Tooltip("アイコンの色から作った色の組み合わせ。ステージの壁紙・床・足場・ゲージを、この色に塗り替える。空なら元の色のまま")]
    public AppTheme Theme;

    [Header("代用ボスのひな形にするとき（2026-11-19：AI チャットのボス）")]
    [Tooltip("オン：このボスを代用ボスにするのは、同じカテゴリのアプリだけ（カテゴリの分からないアプリのボスには選ばない）")]
    public bool StandInSameCategoryOnly;
    [Tooltip("代用ボスにしたときのステージのシーン。空なら、表の「代用ボスのステージ」")]
    public string StandInStageScene;

    [Header("使用時間で変えるつまみ（docs 6章）")]
    [Tooltip("ボスのウイルス解除ゲージの最大値")]
    public IntRange GaugeMax = new IntRange(80, 140);
    [Tooltip("第2形態の、攻撃の後の待ち時間（秒）")]
    public FloatRange Phase2Wait = new FloatRange(0.8f, 0.6f);
    [Tooltip("（使わない）能力の強さは、使用時間ではなくレベルで決める（2026-11-17 決定）。項目だけ残している")]
    public AttackStat AbilityStat = AttackStat.None;
    public FloatRange AbilityValue;

    /// <summary>このアプリのバンドルIDか（前方一致、大文字小文字は区別しない）</summary>
    public bool Matches(string bundleId)
    {
        if (string.IsNullOrEmpty(bundleId) || BundleIdPrefixes == null) return false;
        foreach (var prefix in BundleIdPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && bundleId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

/// <summary>
/// 対応アプリ表（仕様書 6.2：ゲームが対応しているアプリだけが登場する）。
/// アプリを増やすときは、ここに1行足して、ボスのプレハブとプロフィールを用意する。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ App Catalog
/// </summary>
[CreateAssetMenu(fileName = "AppCatalog", menuName = "Team1/App Catalog")]
public class AppCatalog : ScriptableObject
{
    [Tooltip("この時間（過去7日間の合計）以上使っていれば、強さが最大になる")]
    [SerializeField, Min(0.1f)] float _fullStrengthHours = 20f;
    [SerializeField] AppEntry[] _apps;
    [Tooltip("代用ボス（表に無いアプリのボス）のステージのシーン（2026-11-17）")]
    [SerializeField] string _standInStageScene = "Stage_Desktop";
    [Header("ラスボス（2026-11-18 まさぴ〜決定：Terminal）")]
    [Tooltip("毎回、最後のステージに出るボス（使用時間に関係なく出る。ランダムの候補からは外す）。プレハブかプロフィールが空なら出さない。強さはいつも最大")]
    [SerializeField] AppEntry _finalBoss;
    [Header("代用ボスの専用の技（2026-11-18：Finder・Premiere Pro）")]
    [Tooltip("当たったアプリの代用ボスは、ここのボスの設定（技）を使う。見た目・ステージ・能力は代用ボスのまま")]
    [SerializeField] AppBossEntry[] _appBosses;

    public float FullStrengthHours => _fullStrengthHours;
    public AppBossEntry[] AppBosses => _appBosses ?? Array.Empty<AppBossEntry>();

    /// <summary>このアプリ専用のボスの設定（無ければ null）</summary>
    public AppBossEntry FindAppBoss(AppUsageRow row)
    {
        foreach (var e in AppBosses)
        {
            if (e != null && e.Profile != null && e.IsActive && e.Matches(row)) return e; // キットの行はスイッチを見る（2026-11-19）
        }
        return null;
    }
    public AppEntry[] Apps => _apps ?? Array.Empty<AppEntry>();
    public string StandInStageScene => _standInStageScene;
    /// <summary>ラスボス（プレハブとプロフィールが入っているときだけ）。無ければ null</summary>
    public AppEntry FinalBoss => _finalBoss != null && _finalBoss.BossPrefab != null && _finalBoss.Profile != null ? _finalBoss : null;

    /// <summary>ラスボスのアプリか（バンドルID・表示名・スクリーンタイムでの別名）。ランダムのボスの候補から外すのに使う</summary>
    public bool IsFinalBossApp(string bundleId, string name)
    {
        AppEntry f = FinalBoss;
        if (f == null) return false;
        if (f.Matches(bundleId)) return true;
        if (string.IsNullOrWhiteSpace(name)) return false;
        string n = name.Trim();
        return string.Equals(n, f.DisplayName, StringComparison.OrdinalIgnoreCase) ||
               (f.ScreenTimeNames != null && Array.Exists(f.ScreenTimeNames, x => string.Equals(n, x, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// スクリーンタイムの名前が、その名前（表示名・別名）のアプリか。同じ名前か、「名前 - 説明」の形。
    /// 2026-11-20：「-」の前に空白が無い形（読み取りの揺れ：「Google Chrome-ウェブ・・」）も当たる。同じアプリが図鑑・ランに2つ出ないように
    /// </summary>
    public static bool NameMatches(string name, string label)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(label)) return false;
        string n = name.Trim(), l = label.Trim();
        if (n.Equals(l, StringComparison.OrdinalIgnoreCase)) return true;
        if (!n.StartsWith(l, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = n.Substring(l.Length).TrimStart();
        return rest.Length > 0 && "-－–—".IndexOf(rest[0]) >= 0;
    }

    /// <summary>名前に当たる対応アプリ（表示名・スクリーンタイムでの別名。「名前 - 説明」の形も当たる）。無ければ null</summary>
    public AppEntry FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string n = name.Trim();
        bool Same(string label) => NameMatches(n, label);
        foreach (var app in Apps)
        {
            if (app == null) continue;
            if (Same(app.DisplayName) || (app.ScreenTimeNames != null && Array.Exists(app.ScreenTimeNames, Same))) return app;
        }
        return null;
    }

    /// <summary>バンドルIDに当たる対応アプリ。無ければ null（対応外のアプリは登場しない）</summary>
    public AppEntry Find(string bundleId)
    {
        foreach (var app in Apps)
        {
            if (app != null && app.Matches(bundleId)) return app;
        }
        return null;
    }

    /// <summary>使用時間（秒）→ 強さ t（0〜1）。最大の時間以上は 1</summary>
    public float StrengthFromSeconds(double seconds)
    {
        return Mathf.Clamp01((float)(seconds / 3600.0 / _fullStrengthHours));
    }
}
