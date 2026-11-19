using System;
using System.Collections.Generic;

// 【仮】ほかの有名なアプリに、今あるキットを名前だけ足して使う（2026-11-19。docs/app-kits-1-design.md 19章）。担当0 の物。
// ・新しくは作らない。キットの「アプリの名前」に別名を足すだけ（ボス・能力・ギミックが全部そのまま当たる）。色はアイコンから今までどおり。
// ・【要相談⑧】決まるまでは入れる（元のキットができたら当たる。キットが無いうちは何も起きない）。止めるときは Enabled = false。
// ・キットのラベルは、担当によって違う名前になっているかもしれないので、候補を並べた（最初に見つかった物に足す）。
// ・AppKits（実行時の登録）と AppKitsBuilder（対応アプリの表・能力の表の行）の両方が、ここを見て名前を足す。
// ・ChatGPT・Claude・Slack・Discord・LINE のキットは、ほかのアプリには使わない（そのアプリだけの印象が強い）。

/// <summary>キットの別名（19章）</summary>
public static class KitAliases
{
    /// <summary>【仮】false にすると、別名を足さない</summary>
    public static bool Enabled = true;

    /// <summary>別名1行：足す先のキットのラベルの候補・アプリの名前・バンドルID</summary>
    public struct Row
    {
        public string[] Labels;
        public string[] Names;
        public string[] Bundles;
    }

    /// <summary>19章の表（名前・バンドルIDは【仮】。分からない物は名前だけ）</summary>
    public static readonly Row[] Table =
    {
        new Row { Labels = new[] { "Mail", "AppleMail", "Mails" }, Names = new[] { "Outlook", "Microsoft Outlook" }, Bundles = new[] { "com.microsoft.Outlook" } },
        new Row { Labels = new[] { "Word", "MicrosoftWord" }, Names = new[] { "Pages" }, Bundles = new[] { "com.apple.iWork.Pages" } },
        new Row { Labels = new[] { "Excel", "MicrosoftExcel" }, Names = new[] { "Numbers" }, Bundles = new[] { "com.apple.iWork.Numbers" } },
        new Row { Labels = new[] { "Notes", "AppleNotes", "Memo" }, Names = new[] { "Reminders", "リマインダー" }, Bundles = new[] { "com.apple.reminders" } },
        new Row { Labels = new[] { "Notion" }, Names = new[] { "Obsidian", "Linear" }, Bundles = new[] { "md.obsidian", "com.linear" } },
        new Row { Labels = new[] { "Teams", "MicrosoftTeams" }, Names = new[] { "Google Meet", "Meet" }, Bundles = new string[0] },
        new Row { Labels = new[] { "Messages", "Message", "AppleMessages" }, Names = new[] { "Telegram", "WhatsApp", "Messenger" }, Bundles = new[] { "ru.keepcoder.Telegram", "net.whatsapp.WhatsApp", "desktop.WhatsApp", "com.facebook.archon" } },
        new Row { Labels = new[] { "Gemini", "GoogleGemini" }, Names = new[] { "Perplexity", "Copilot", "Microsoft Copilot" }, Bundles = new[] { "ai.perplexity.mac", "com.microsoft.copilot" } },
    };

    /// <summary>そのラベルのキットに足す別名（無ければ空）</summary>
    public static string[] NamesFor(string label) => Collect(label, true);

    /// <summary>そのラベルのキットに足すバンドルID（無ければ空）</summary>
    public static string[] BundlesFor(string label) => Collect(label, false);

    /// <summary>元の名前に、別名を足した物（同じ名前は1つ）</summary>
    public static string[] WithNames(string label, string[] names) => Merge(names, NamesFor(label));

    /// <summary>元のバンドルIDに、別名のバンドルIDを足した物</summary>
    public static string[] WithBundles(string label, string[] bundles) => Merge(bundles, BundlesFor(label));

    static string[] Collect(string label, bool names)
    {
        if (!Enabled || string.IsNullOrEmpty(label)) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var r in Table)
        {
            if (r.Labels == null || Array.FindIndex(r.Labels, l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)) < 0) continue;
            string[] add = names ? r.Names : r.Bundles;
            if (add != null) list.AddRange(add);
        }
        return list.ToArray();
    }

    static string[] Merge(string[] a, string[] b)
    {
        if (b == null || b.Length == 0) return a ?? Array.Empty<string>();
        var list = new List<string>(a ?? Array.Empty<string>());
        foreach (var s in b)
        {
            if (string.IsNullOrEmpty(s) || list.Exists(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(s);
        }
        return list.ToArray();
    }
}
