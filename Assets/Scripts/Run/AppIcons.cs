using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Mac に入っているアプリのアイコンを読み込む（2026-11-17：アイコンからドット絵を作る）。
/// ・macOS に最初から入っている osascript（JavaScript）で、NSWorkspace からアイコンを PNG に書き出して読む。
///   組み立てるツールも、画面収録の許可もいらない。ビルドしたゲームでも動く
/// ・アプリはバンドルID → 名前（/Applications などの「名前.app」）→ 英語の名前（「カレンダー」→ Calendar）の順に探す
/// ・見つからない（iPhone だけのアプリ・入っていないアプリ）・Mac 以外・失敗したときは、そのアプリを結果に入れない（呼ぶ側が仮のアイコンにする）
/// ・時間がかかりすぎたら止める。失敗してもランは始められる
/// </summary>
public static class AppIcons
{
    public struct Request
    {
        public string Key;
        public string BundleId;
        public string Name;
    }

    [Serializable] class Item { public string key; public string id; public string name; public string alt; }
    [Serializable] class ItemList { public Item[] items; }

    /// <summary>日本語の表示名 → アプリの英語の名前（スクリーンタイムは日本語で出すが、ファイル名は英語のアプリ）</summary>
    static readonly Dictionary<string, string> JapaneseNames = new Dictionary<string, string>
    {
        { "カレンダー", "Calendar" }, { "メール", "Mail" }, { "メモ", "Notes" }, { "ターミナル", "Terminal" },
        { "シミュレーター", "Simulator" }, { "ミュージック", "Music" }, { "写真", "Photos" }, { "メッセージ", "Messages" },
        { "リマインダー", "Reminders" }, { "マップ", "Maps" }, { "プレビュー", "Preview" }, { "ポッドキャスト", "Podcasts" },
        { "ブック", "Books" }, { "ホーム", "Home" }, { "時計", "Clock" }, { "天気", "Weather" }, { "計算機", "Calculator" },
        { "辞書", "Dictionary" }, { "連絡先", "Contacts" }, { "フリーボード", "Freeform" }, { "ボイスメモ", "Voice Memos" },
        { "株価", "Stocks" }, { "ショートカット", "Shortcuts" }, { "探す", "Find My" }, { "テキストエディット", "TextEdit" },
        { "アクティビティモニタ", "Activity Monitor" }, { "スクリーンショット", "Screenshot" }, { "パスワード", "Passwords" },
        { "ゲーム", "Games" }, { "ヒント", "Tips" }, { "ニュース", "News" },
    };

    static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
    static readonly HashSet<string> Missing = new HashSet<string>();

    public static bool CanFetch => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

    /// <summary>アイコンを読み込む（覚えておき、2回目からは読み直さない）。キー → 64×64 のアイコン。見つからなかったアプリは入らない</summary>
    public static Dictionary<string, Texture2D> Fetch(IEnumerable<Request> requests, int size = 64, int timeoutMs = 8000)
    {
        var result = new Dictionary<string, Texture2D>();
        var todo = new List<Request>();
        foreach (var r in requests)
        {
            if (string.IsNullOrEmpty(r.Key) || result.ContainsKey(r.Key)) continue;
            if (Cache.TryGetValue(r.Key, out Texture2D tex) && tex != null) result[r.Key] = tex;
            else if (!Missing.Contains(r.Key) && !todo.Exists(t => t.Key == r.Key)) todo.Add(r);
        }
        if (todo.Count == 0 || !CanFetch) return result;

        try
        {
            string dir = Path.Combine(Application.temporaryCachePath, "AppIcons");
            Directory.CreateDirectory(dir);
            string script = Path.Combine(dir, "export_icons.js");
            if (!File.Exists(script) || File.ReadAllText(script) != Script) File.WriteAllText(script, Script);
            string request = Path.Combine(dir, "request.json");
            var list = new ItemList { items = todo.ConvertAll(r => new Item { key = r.Key, id = r.BundleId ?? "", name = r.Name ?? "", alt = EnglishName(r.Name) }).ToArray() };
            File.WriteAllText(request, JsonUtility.ToJson(list)); // 名前（日本語や引用符）はコマンドの引数ではなく、ファイルで渡す

            var start = new ProcessStartInfo("/usr/bin/osascript", $"-l JavaScript \"{script}\" \"{dir}\" {size} \"{request}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(start))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch (Exception) { }
                    Debug.LogWarning("アプリのアイコンの読み込みに時間がかかりすぎたので、やめました（仮のアイコンを使います）");
                    return result;
                }
                if (process.ExitCode != 0)
                {
                    Debug.LogWarning($"アプリのアイコンを読み込めませんでした（仮のアイコンを使います）\n{stderr.Result}");
                    return result;
                }
                foreach (string line in stdout.Result.Split('\n'))
                {
                    string[] parts = line.Trim().Split('\t');
                    if (parts.Length < 2 || string.IsNullOrEmpty(parts[1])) { if (parts.Length > 0) Missing.Add(parts[0]); continue; }
                    string file = Path.Combine(dir, parts[0] + ".png");
                    if (!File.Exists(file)) { Missing.Add(parts[0]); continue; }
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = parts[0] };
                    if (!tex.LoadImage(File.ReadAllBytes(file), false)) { Missing.Add(parts[0]); continue; }
                    Cache[parts[0]] = tex;
                    result[parts[0]] = tex;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"アプリのアイコンを読み込めませんでした（仮のアイコンを使います）\n{e.Message}");
        }
        return result;
    }

    static string EnglishName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return JapaneseNames.TryGetValue(name.Trim(), out string en) ? en : "";
    }

    // 1行に1アプリ「キー<TAB>見つかったアプリの場所」（見つからなければ場所は空）を返す
    const string Script = @"ObjC.import('AppKit');
function run(argv) {
  var outDir = argv[0];
  var size = parseInt(argv[1]);
  var text = $.NSString.stringWithContentsOfFileEncodingError(argv[2], $.NSUTF8StringEncoding, null).js;
  var items = JSON.parse(text).items;
  var ws = $.NSWorkspace.sharedWorkspace;
  var fm = $.NSFileManager.defaultManager;
  var home = $.NSHomeDirectory().js;
  var dirs = ['/Applications/', '/System/Applications/', '/System/Applications/Utilities/', '/Applications/Utilities/', home + '/Applications/'];
  function find(name) {
    if (!name) return null;
    for (var i = 0; i < dirs.length; i++) {
      var p = dirs[i] + name + '.app';
      if (fm.fileExistsAtPath(p)) return p;
    }
    var full = ws.fullPathForApplication(name);
    if (full && !full.isNil()) return full.js;
    return null;
  }
  var lines = [];
  items.forEach(function (it) {
    var path = null;
    try {
      if (it.id) {
        var url = ws.URLForApplicationWithBundleIdentifier(it.id);
        if (url && !url.isNil()) path = url.path.js;
      }
      if (!path) path = find(it.name);
      if (!path) path = find(it.alt);
      if (!path) { lines.push(it.key + '\t'); return; }
      var img = ws.iconForFile(path);
      var rep = $.NSBitmapImageRep.alloc.initWithBitmapDataPlanesPixelsWidePixelsHighBitsPerSampleSamplesPerPixelHasAlphaIsPlanarColorSpaceNameBytesPerRowBitsPerPixel(
        null, size, size, 8, 4, true, false, $.NSDeviceRGBColorSpace, 0, 0);
      $.NSGraphicsContext.saveGraphicsState;
      $.NSGraphicsContext.setCurrentContext($.NSGraphicsContext.graphicsContextWithBitmapImageRep(rep));
      img.drawInRectFromRectOperationFraction($.NSMakeRect(0, 0, size, size), $.NSZeroRect, $.NSCompositingOperationSourceOver, 1.0);
      $.NSGraphicsContext.restoreGraphicsState;
      var png = rep.representationUsingTypeProperties($.NSBitmapImageFileTypePNG, $());
      png.writeToFileAtomically(outDir + '/' + it.key + '.png', true);
      lines.push(it.key + '\t' + path);
    } catch (e) {
      lines.push(it.key + '\t');
    }
  });
  return lines.join('\n');
}
";
}
