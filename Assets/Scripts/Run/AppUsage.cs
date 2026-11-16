using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// アプリ1つ分の使用データ（仕様書 6.3）。取得側（れっきー）との受け渡しの形。
/// usage.json の中身は、この形の配列：
///   [{ "id": "com.google.Chrome", "displayName": "Google Chrome", "usageSeconds": 54000, "iconPath": null }]
/// ・id はバンドルID（対応アプリ表と前方一致で照合する）
/// ・usageSeconds は過去7日間の合計秒数。取れなければ null（0 として扱う）
/// ・iconPath は取れなければ null
/// </summary>
[Serializable]
public struct AppUsage
{
    public string Id;
    public string DisplayName;
    public double UsageSeconds;
    public string IconPath;

    public double UsageHours => UsageSeconds / 3600.0;

    public AppUsage(string id, string displayName, double usageSeconds, string iconPath = null)
    {
        Id = id;
        DisplayName = displayName;
        UsageSeconds = usageSeconds;
        IconPath = iconPath;
    }
}

/// <summary>usage.json を読み書きする</summary>
public static class AppUsageJson
{
    [Serializable]
    class Record
    {
        public string id;
        public string displayName;
        public double usageSeconds;
        public string iconPath;
    }

    [Serializable]
    class Wrapper
    {
        public Record[] items;
    }

    /// <summary>JSON の文字列を読む。形が正しくなければ例外を投げる</summary>
    public static List<AppUsage> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("空のデータです");
        string text = json.Trim();
        // JsonUtility は null の数値を読めないので 0 にしておく（取れなかった使用時間は 0 扱い）
        text = Regex.Replace(text, "\"usageSeconds\"\\s*:\\s*null", "\"usageSeconds\":0");
        // 一番外側が配列ならそのまま、{ "items": [...] } の形でも読めるようにする
        if (text.StartsWith("[")) text = "{\"items\":" + text + "}";

        var wrapper = JsonUtility.FromJson<Wrapper>(text);
        var result = new List<AppUsage>();
        if (wrapper?.items == null) return result;
        foreach (var r in wrapper.items)
        {
            if (r == null || string.IsNullOrEmpty(r.id)) continue;
            result.Add(new AppUsage(r.id, r.displayName, Math.Max(0.0, r.usageSeconds), string.IsNullOrEmpty(r.iconPath) ? null : r.iconPath));
        }
        return result;
    }

    /// <summary>JSON の文字列にする（受け渡しと同じ、一番外側が配列の形。サンプルや手入力の内容を書き出すとき用）</summary>
    public static string ToJson(IEnumerable<AppUsage> usages)
    {
        var list = new List<Record>();
        foreach (var u in usages)
        {
            list.Add(new Record { id = u.Id, displayName = u.DisplayName, usageSeconds = u.UsageSeconds, iconPath = u.IconPath });
        }
        // JsonUtility は配列を直接書けないので、{ "items": [...] } で書いてから中の配列だけを取り出す
        string json = JsonUtility.ToJson(new Wrapper { items = list.ToArray() }, true);
        int start = json.IndexOf('[');
        int end = json.LastIndexOf(']');
        return start >= 0 && end > start ? json.Substring(start, end - start + 1) : json;
    }
}
