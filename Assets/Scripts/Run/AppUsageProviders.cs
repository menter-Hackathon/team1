using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// アプリの使用データを読み込む窓口（仕様書 6.6）。中身を「自動（usage.json）」「サンプル」「手入力」で差し替える。
/// 取得が間に合わなくても、サンプルか手入力でゲームを始められるようにするため。
/// </summary>
public interface IAppUsageProvider
{
    /// <summary>画面に出す名前</summary>
    string Label { get; }

    /// <summary>読み込む。失敗したら false と理由</summary>
    bool TryLoad(out List<AppUsage> usages, out string error);
}

/// <summary>
/// 自動：取得側が書いた usage.json を読む。
/// 置き場所の既定は Application.persistentDataPath/usage.json
/// （このプロジェクトの設定なら ~/Library/Application Support/DefaultCompany/Team1/usage.json）
/// </summary>
public class FileUsageProvider : IAppUsageProvider
{
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "usage.json");

    readonly string _path;

    public FileUsageProvider(string path = null)
    {
        _path = string.IsNullOrEmpty(path) ? DefaultPath : path;
    }

    /// <summary>読む場所</summary>
    public string FilePath => _path;
    public string Label => "自動";

    public bool TryLoad(out List<AppUsage> usages, out string error)
    {
        usages = null;
        if (!File.Exists(_path))
        {
            error = $"usage.json が見つかりません（{_path}）";
            return false;
        }
        try
        {
            usages = AppUsageJson.Parse(File.ReadAllText(_path));
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = $"usage.json を読めませんでした：{e.Message}";
            return false;
        }
    }
}

/// <summary>サンプル：ゲームに同梱したデータ（Resources/SampleUsage.json）を読む</summary>
public class SampleUsageProvider : IAppUsageProvider
{
    public const string ResourceName = "SampleUsage";

    readonly TextAsset _asset;

    public SampleUsageProvider(TextAsset asset = null)
    {
        _asset = asset;
    }

    public string Label => "サンプル";

    public bool TryLoad(out List<AppUsage> usages, out string error)
    {
        usages = null;
        TextAsset asset = _asset != null ? _asset : Resources.Load<TextAsset>(ResourceName);
        if (asset == null)
        {
            error = $"サンプルデータ（Resources/{ResourceName}.json）がありません";
            return false;
        }
        try
        {
            usages = AppUsageJson.Parse(asset.text);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = $"サンプルデータを読めませんでした：{e.Message}";
            return false;
        }
    }
}

/// <summary>手入力：タイトル画面で入れた使用時間をそのまま渡す</summary>
public class ManualUsageProvider : IAppUsageProvider
{
    readonly List<AppUsage> _usages;

    public ManualUsageProvider(IEnumerable<AppUsage> usages)
    {
        _usages = new List<AppUsage>(usages ?? Array.Empty<AppUsage>());
    }

    public string Label => "手入力";

    public bool TryLoad(out List<AppUsage> usages, out string error)
    {
        usages = new List<AppUsage>(_usages);
        error = null;
        return true;
    }
}
