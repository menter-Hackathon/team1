using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>起動時の判定に使うスクリーンタイムの取得元</summary>
    public interface IScreenTimeSource
    {
        string Label { get; }
        /// <summary>読む。取れなければ false（PCの使い方の判定を飛ばし、次の起動時にまとめて判定する）</summary>
        bool TryRead(DateTime now, out ScreenTimeSnapshot snapshot, out string error);
    }

    /// <summary>
    /// 本番の取得元（いまは取れない）。
    /// 既存のスクリーンタイムの取得（MacScreenTime：システム設定の「先週」のアプリ一覧を1枚撮って文字を読む）で分かるのは、
    /// 先週1週間のアプリごとの合計だけで、日ごと・1時間ごと・過去の週は取れない。広げ方が決まるまで、PCの使い方の判定は飛ばす。
    /// </summary>
    public sealed class PendingScreenTimeSource : IScreenTimeSource
    {
        public string Label => "未対応";

        public bool TryRead(DateTime now, out ScreenTimeSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = "日ごと・1時間ごとのスクリーンタイムを取る仕組みが、まだありません";
            return false;
        }
    }

    /// <summary>
    /// 確認用の偽データ（JSON）。形：
    /// { "capturedAt": "2026-11-20T21:00:00", "coverageStart": "2026-11-02",
    ///   "sessions": [ { "app": "YouTube", "start": "2026-11-10T20:00:00", "seconds": 3600 } ],
    ///   "daily":    [ { "app": "Safari", "date": "2026-11-11", "seconds": 1200 } ] }
    /// capturedAt が空なら、判定する時点を使う。
    /// </summary>
    public sealed class JsonScreenTimeSource : IScreenTimeSource
    {
#pragma warning disable 0649 // JsonUtility が入れる
        [Serializable] class Session { public string app; public string start; public int seconds; }
        [Serializable] class Daily { public string app; public string date; public int seconds; }
        [Serializable] class Data { public string capturedAt; public string coverageStart; public Session[] sessions; public Daily[] daily; }
#pragma warning restore 0649

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "achievement_screentime_debug.json");

        readonly string _json;
        readonly string _path;

        JsonScreenTimeSource(string json, string path) { _json = json; _path = path; }

        public static JsonScreenTimeSource FromText(string json) => new JsonScreenTimeSource(json, null);
        public static JsonScreenTimeSource FromFile(string path = null) => new JsonScreenTimeSource(null, string.IsNullOrEmpty(path) ? DefaultPath : path);

        public string Label => _path != null ? "偽データ（ファイル）" : "偽データ";

        public bool TryRead(DateTime now, out ScreenTimeSnapshot snapshot, out string error)
        {
            snapshot = null;
            try
            {
                string text = _json;
                if (text == null)
                {
                    if (!File.Exists(_path)) { error = $"偽データがありません（{_path}）"; return false; }
                    text = File.ReadAllText(_path);
                }
                var data = JsonUtility.FromJson<Data>(text);
                if (data == null) { error = "偽データを読めませんでした"; return false; }
                DateTime captured = ParseTime(data.capturedAt) ?? now;
                snapshot = new ScreenTimeSnapshot(captured, ParseTime(data.coverageStart));
                foreach (var s in data.sessions ?? Array.Empty<Session>())
                {
                    var start = ParseTime(s?.start);
                    if (start.HasValue) snapshot.AddSession(s.app, start.Value, s.seconds);
                }
                foreach (var d in data.daily ?? Array.Empty<Daily>())
                {
                    var date = ParseTime(d?.date);
                    if (date.HasValue) snapshot.AddDaily(d.app, date.Value, d.seconds);
                }
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = $"偽データを読めませんでした：{e.Message}";
                return false;
            }
        }

        static DateTime? ParseTime(string text) =>
            !string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : (DateTime?)null;
    }

    /// <summary>そのまま渡す（確認用。プリセットから作ったデータなど）</summary>
    public sealed class FixedScreenTimeSource : IScreenTimeSource
    {
        readonly ScreenTimeSnapshot _snapshot;
        public FixedScreenTimeSource(ScreenTimeSnapshot snapshot, string label = "偽データ") { _snapshot = snapshot; Label = label; }
        public string Label { get; }

        public bool TryRead(DateTime now, out ScreenTimeSnapshot snapshot, out string error)
        {
            snapshot = _snapshot;
            error = snapshot == null ? "データがありません" : null;
            return snapshot != null;
        }
    }

    /// <summary>確認用：プリセットの偽データを、判定する時点から作る</summary>
    public sealed class PresetScreenTimeSource : IScreenTimeSource
    {
        readonly int _preset;
        public PresetScreenTimeSource(int preset) { _preset = Mathf.Clamp(preset, 0, FakeScreenTimePresets.Names.Length - 1); }
        public string Label => "偽データ（" + FakeScreenTimePresets.Names[_preset] + "）";

        public bool TryRead(DateTime now, out ScreenTimeSnapshot snapshot, out string error)
        {
            snapshot = FakeScreenTimePresets.Build(_preset, now);
            error = null;
            return true;
        }
    }

    /// <summary>確認用の偽データのプリセット（判定する時点から逆算して作る）</summary>
    public static class FakeScreenTimePresets
    {
        public static readonly string[] Names = { "PCの使い方を全部解除", "もうすぐ解除（朝型 5日・連続 6日）", "何も解除しない" };

        public static ScreenTimeSnapshot Build(int preset, DateTime now)
        {
            DateTime today = now.Date;
            DateTime thisWeek = WeekMath.WeekStart(now);
            DateTime lastWeek = thisWeek.AddDays(-7);
            DateTime coverage = today.AddDays(-35); // 朝型 III（30日）まで届くように
            var st = new ScreenTimeSnapshot(now, coverage);
            switch (preset)
            {
                case 0:
                    // 31 日分、毎朝 6 時台に 15 分・毎日使う。土曜に VS Code を 90 分。今週だけ Figma。ある1日に 12 種類
                    for (int i = 31; i >= 1; i--)
                    {
                        DateTime d = today.AddDays(-i);
                        if (d < coverage) continue;
                        st.AddSession("Safari", d.AddHours(6), 900);
                        st.AddSession("Slack", d.AddHours(13), 1800);
                    }
                    // デトックス：先々週は YouTube を 10 時間、先週（終わった週）は 2 時間
                    for (int i = 0; i < 5; i++) st.AddSession("YouTube", lastWeek.AddDays(-7 + i).AddHours(21), 7200);
                    st.AddSession("YouTube", lastWeek.AddHours(21), 7200);
                    DateTime sat = lastWeek.AddDays(5);
                    st.AddSession("Visual Studio Code", sat.AddHours(10), 5400);
                    DateTime busy = today.AddDays(-1);
                    for (int i = 0; i < 12; i++) st.AddSession("Tool" + (i + 1), busy.AddHours(10).AddMinutes(i * 5), 120);
                    st.AddSession("Figma", today.AddHours(Math.Min(now.Hour, 1)), 600);
                    break;
                case 1:
                    for (int i = 6; i >= 1; i--) st.AddSession("Safari", today.AddDays(-i).AddHours(i <= 5 ? 6 : 12), 900);
                    break;
                default:
                    st.AddSession("Safari", today.AddDays(-1).AddHours(14), 600);
                    break;
            }
            return st;
        }
    }
}
