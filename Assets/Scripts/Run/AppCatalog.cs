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
/// 対応アプリ1つ分。バンドルIDの照合、出てくるボス、強さのつまみを持つ。
/// </summary>
[Serializable]
public class AppEntry
{
    [Tooltip("画面に出す名前")]
    public string DisplayName;
    [Tooltip("バンドルID。前方一致で照合する（例：com.adobe.PremierePro は com.adobe.PremierePro.26 にも当たる）")]
    public string[] BundleIdPrefixes;
    public SkillCategory Category = SkillCategory.Unknown;

    [Header("ボス")]
    [Tooltip("ボスのプレハブ（非表示で保存しておく）")]
    public BossController BossPrefab;
    [Tooltip("ボスの設定。使用時間で数値を変えた複製を作って渡す。手に入る能力もここから読む")]
    public BossProfile Profile;
    [Tooltip("ボスが出てくる位置（部屋の床の真ん中が (0, 0)）")]
    public Vector2 SpawnPosition;
    [Tooltip("このアプリのステージのシーン名（Build Settings に登録しておく）")]
    public string StageScene = "Stage_Desktop";

    [Header("使用時間で変えるつまみ（docs 6章）")]
    [Tooltip("ボスのウイルス解除ゲージの最大値")]
    public IntRange GaugeMax = new IntRange(80, 140);
    [Tooltip("第2形態の、攻撃の後の待ち時間（秒）")]
    public FloatRange Phase2Wait = new FloatRange(0.8f, 0.6f);
    [Tooltip("手に入る能力の、使用時間で変える数値")]
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

    public float FullStrengthHours => _fullStrengthHours;
    public AppEntry[] Apps => _apps ?? Array.Empty<AppEntry>();

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
