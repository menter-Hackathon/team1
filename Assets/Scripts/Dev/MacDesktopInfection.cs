using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 起動の演出の「感染」（2026-11-20 まさぴ〜「最初にウイルスが入ってやばい倒さなきゃという動機づけの流れがほしい」
/// →「もっと感染したやばい！ってプレイヤーが焦るレベルまで作り込んでほしい」。設計：docs/infection-panic-and-cure-design.md。前の 6 秒版は docs/infection-intro-design.md）。
/// ロック解除のあとの 12 秒（MacDesktopBoot の Infecting）。4つの幕で止まらずに悪くなる：
///   しんにゅう（0〜3）→ ひろがる（3〜6.5。Dock・壁紙・ゲージ・はうウイルス）→ くずれる（6.5〜10。警告の窓 10枚・ゆれ・警報）→ のこるのは1つ（10〜12。99%・暗さ・ゲームのアイコンだけがはじく）。
/// 待ち（ゲームのアイコンを押すまで）も止まらないが、数と音に上限がある。押すと青い輪が広がって押し返し、それから窓が開く。
/// 起動のあとのデスクトップには、うすい「のこり」を出す（全クリアしたら Cured で消える）。
/// RunTitleMenu の DrawBoot（起動の演出の中）と OnGUI（起動のあと）から呼ぶ。DesktopCure（クリア後）も、ここの小さい描き方を使う。
/// 絵は T（感染が始まってからの秒）・Wt（待ちに入ってからの秒）・L（押してからの秒）と番号の乱数だけで決める（物を覚えない）。
/// 字は警告の窓・ゲージ・通知・札の短い一言と、メニューバーの「感染 N」だけ。赤は使わない（窓の左上の丸だけは Mac の部品のまま）。
/// </summary>
public static class MacDesktopInfection
{
    /// <summary>false＝今までの流れ（解除 → すぐデスクトップで待つ）。切り戻し用</summary>
    public static bool Enabled = true;
    /// <summary>true＝全クリアした（DesktopCure が立てる）。起動のあとの「のこり」を出さない。次のランのステージの前の画面で false に戻る</summary>
    public static bool Cured;
    /// <summary>感染のステップの長さ（秒）</summary>
    public const float Duration = 12f;
    /// <summary>これより前は、押しても飛ばさない（解除の押しっぱなし対策）</summary>
    public const float InputGuard = 0.4f;

    // ───────── 時刻（T） ─────────
    const float BreachAt = 0.8f;                                   // しんにゅう：線・ダウンロードが紫に・光
    const float DownloadsDyeAt = 0.8f, DocumentsDyeAt = 3.0f, DyeTime = 0.4f;
    const float NoticeIn = 1.1f, NoticeOut = 3.4f;                 // 最初の通知（不明なファイル）
    static readonly float[] AppLand = { 2.0f, 2.8f, 3.5f, 4.1f };  // Dock のアプリ（右から）に乗る時刻
    static readonly string[] LandType = { "Worm", "Trojan", "Spyware", "Adware" };
    const float FinderLand = 4.6f;
    const string FinderType = "Ransomware";
    static readonly float[] SpreadPitch = { 1.0f, 1.12f, 1.24f, 1.36f };
    const float HeartFrom = 2.0f;
    const float VeilFrom = 2.0f, VeilMax = 0.58f, VeilLinger = 0.18f;
    const float GaugeAt = 3.2f;
    const float DroneAt = 3.5f;
    const float CrawlFrom = 4.0f, CrawlEvery = 0.4f;
    static readonly string[] WallTypes = { "Botnet", "Miner", "Poly", "Phish", "Bomb", "Rootkit" };
    static readonly string[] DockTypes = { "Worm", "Trojan", "Spyware", "Adware", "Ransomware", "Macro" };
    internal static readonly string[] AllTypes = { "Adware", "Bomb", "Botnet", "Macro", "Miner", "Phish", "Poly", "Ransomware", "Rootkit", "Spyware", "Trojan", "Worm" };
    const float CollapseAt = 6.5f, BiteFrom = 6.5f;
    static readonly float[] WarnAt = { 6.5f, 6.95f, 7.35f, 7.7f, 8.0f, 8.25f, 8.5f, 8.7f, 8.9f, 9.1f };
    internal static readonly Vector2[] WarnSpot =
    {
        new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.55f), new Vector2(0.40f, 0.95f), new Vector2(0.75f, 0.05f), new Vector2(0.00f, 0.70f),
        new Vector2(0.60f, 0.60f), new Vector2(0.25f, 0.35f), new Vector2(1.00f, 0.25f), new Vector2(0.12f, 1.00f), new Vector2(0.85f, 0.90f),
    };
    internal static readonly string[] WarnText = { "ウイルスを検出しました", "アプリが感染しています", "ファイルを開けません", "書類がこわれました", "操作できません" };
    static readonly float[] AlarmAt = { 6.5f, 8.1f, 9.7f };
    const float DarkAt = 10f;
    static readonly float[] LeapHit = { 10.3f, 10.8f, 11.5f };      // ゲームのアイコンに跳びかかって、はじかれる時刻
    static readonly string[] LeapType = { "Bomb", "Phish", "Ransomware" };
    const float LeapTime = 0.4f;
    const float RingFrom = 10.3f, SlamAt = 11.2f, HaloFrom = 11.5f, TagAt = 11.6f;
    const float PushTime = 0.9f;                                   // 押した後、青い輪が広がる時間
    const float WaitPopupEvery = 1.4f, WaitPopupSoundUntil = 12f;
    const float CursorFrom = 5.0f, BatteryFrom = 5.0f, BatteryEmpty = 11.0f, ClockFrom = 7.0f;
    const float DroneOffAt = 40f;

    static readonly Color InfectTint = new Color(1f, 0.55f, 1f, 1f); // DesktopScreen と同じ染め
    internal static Color Magenta(float a) => new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, a);
    static Color Of(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ───────── 状態（音と、待ち・押した時刻だけ） ─────────
    static long _sounded;          // 1回だけの音（ビット）
    static float _waitFrom = -1f;  // 待ちに入った時刻（unscaled）
    static float _launchAt = -1f;  // ゲームのアイコンを押した時刻
    static float _nextBeat = -1f, _lastBeat = -10f;
    static SeLoop _drone;
    static bool _droneTried, _shieldDone;
    static int _waitLeapSounded, _waitPopSounded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Cured = false; _sounded = 0; _waitFrom = -1f; _launchAt = -1f; _nextBeat = -1f; _lastBeat = -10f;
        _drone = null; _droneTried = false; _shieldDone = false; _waitLeapSounded = 0; _waitPopSounded = 0;
    }

    static float Now => Time.unscaledTime;
    static float Wt => _waitFrom < 0f ? -1f : Now - _waitFrom;
    static float L => _launchAt < 0f ? -1f : Now - _launchAt;
    static int Apps(int appCount) => Mathf.Clamp(appCount, 0, AppLand.Length);
    static float Ramp(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

    // 折れ線（x を小さい順に並べた点を、まっすぐつなぐ）
    static float Curve(float t, params float[] xy)
    {
        if (t <= xy[0]) return xy[1];
        for (int i = 2; i < xy.Length; i += 2)
            if (t <= xy[i]) return Mathf.Lerp(xy[i - 1], xy[i + 1], (t - xy[i - 2]) / (xy[i] - xy[i - 2]));
        return xy[xy.Length - 1];
    }

    // 乗った数（アプリ＋Finder）
    static int Infected(float t, int appCount)
    {
        if (t < 0f) return 0;
        int n = 0;
        for (int k = 0; k < Apps(appCount); k++)
            if (t >= AppLand[k]) n++;
        if (t >= FinderLand) n++;
        return n;
    }

    // 跳ぶ時間：最初の2匹は 0.6 秒、3匹目から 0.45 秒（間が詰まって見える）
    static float JumpDur(int order) => order < 2 ? 0.6f : 0.45f;

    // ───────── 音（Update から） ─────────

    /// <summary>時刻を過ぎた音を鳴らす。t＝MacDesktopBoot.InfectionTime。launched＝ゲームのアイコンを押した後（Bouncing から）</summary>
    public static void Tick(float t, int appCount, bool launched = false)
    {
        if (!Enabled || t < 0f) return;
        if (launched)
        {
            if (_launchAt < 0f) _launchAt = Now;
            if (!_shieldDone)
            {
                _shieldDone = true;
                GameAudio.Event("inf_shield");
                StopDrone(0.4f);
            }
            return; // 押した後は感染の音を全部止める
        }
        if (t >= Duration && _waitFrom < 0f) _waitFrom = Now;

        // 1回だけの音
        Once(0, t >= BreachAt, "inf_breach");
        Once(1, t >= NoticeIn, "toast");
        int n = Apps(appCount);
        for (int k = 0; k < n; k++)
        {
            Once(2 + k, t >= AppLand[k] - JumpDur(k), "inf_skitter");
            Once(7 + k, t >= AppLand[k], "inf_spread", SpreadPitch[k]);
        }
        Once(6, t >= FinderLand - JumpDur(n), "inf_skitter");
        Once(11, t >= FinderLand, "inf_spread", 1.5f);
        Once(12, t >= GaugeAt, "inf_popup", 0.8f);
        for (int i = 0; i < WarnAt.Length; i++) Once(13 + i, t >= WarnAt[i], "inf_popup", 0.9f + 0.06f * i);
        for (int i = 0; i < AlarmAt.Length; i++) Once(23 + i, t >= AlarmAt[i], "inf_alarm", 1f + 0.06f * i);
        for (int i = 0; i < LeapHit.Length; i++)
        {
            Once(26 + i, t >= LeapHit[i] - LeapTime, "inf_skitter");
            Once(29 + i, t >= LeapHit[i], "inf_repel", 1f + 0.06f * i);
        }
        Once(32, t >= SlamAt, "inf_slam");
        Once(33, t >= TagAt, "toast");

        // 心臓：だんだん速く。待ちでは小さく、だんだん遅く
        if (t >= HeartFrom)
        {
            if (_nextBeat < 0f) _nextBeat = Now;
            if (Now >= _nextBeat)
            {
                bool wait = _waitFrom >= 0f;
                if (wait) GameAudio.Event("inf_heart_soft");
                else GameAudio.Event("inf_heart", t < SlamAt ? Mathf.Lerp(0.9f, 1.15f, (t - HeartFrom) / (SlamAt - HeartFrom)) : 1.15f);
                _lastBeat = Now;
                _nextBeat = Now + (wait ? WaitBeat(Wt) : HeartInterval(t));
            }
        }

        // 低いうなり（鳴らし続ける）。待ちの 40 秒で消す
        if (!_droneTried && t >= DroneAt) { _droneTried = true; _drone = SeLoop.Play("inf_drone"); }
        if (_waitFrom >= 0f && Wt >= DroneOffAt) StopDrone(2f);

        // 待ち：はじく音・窓の出直し
        if (_waitFrom >= 0f)
        {
            float wt = Wt;
            while (WaitLeapHit(_waitLeapSounded + 1) <= wt)
            {
                _waitLeapSounded++;
                float at = WaitLeapHit(_waitLeapSounded);
                if (at < DroneOffAt || _waitLeapSounded % 2 == 0) GameAudio.Event("inf_repel");
            }
            while ((_waitPopSounded + 1) * WaitPopupEvery <= wt)
            {
                _waitPopSounded++;
                if (_waitPopSounded * WaitPopupEvery < WaitPopupSoundUntil) GameAudio.Event("inf_popup_soft");
            }
        }
    }

    /// <summary>飛ばしたとき：まだ鳴らしていない1回だけの音を鳴らしたことにする（まとめて鳴らない）。うなり・心臓は待ちの形で続く</summary>
    public static void MuteRest() => _sounded = ~0L;

    static void Once(int bit, bool due, string key, float pitch = 1f)
    {
        long mask = 1L << bit;
        if (!due || (_sounded & mask) != 0) return;
        _sounded |= mask;
        GameAudio.Event(key, pitch);
    }

    static void StopDrone(float fade)
    {
        if (_drone != null) _drone.Stop(fade);
        _drone = null;
    }

    static float HeartInterval(float t) => t >= SlamAt ? 0.55f : Curve(t, 2.0f, 0.95f, 6.5f, 0.70f, 10.0f, 0.45f, 11.2f, 0.38f);
    static float WaitBeat(float wt) => wt < 15f ? 0.55f : wt < 40f ? 0.75f : 1.0f;

    // 待ちで n 回目（1〜）にはじく時刻（Wt）。15 秒までは 1.7 秒ごと、そのあと 3.4 秒ごと
    static float WaitLeapHit(int n)
    {
        if (n <= 0) return 0f;
        const int early = 8; // 1.7×8＝13.6
        return n <= early ? n * 1.7f : early * 1.7f + (n - early) * 3.4f;
    }

    // ───────── ゆれ ─────────

    /// <summary>画面のゆれ（ドット）。RunTitleMenu の DrawBoot が GUI.matrix をずらす。待ち・押した後は 0</summary>
    public static Vector2 Shake(float t)
    {
        if (!Enabled || t < 0f || t >= Duration || _launchAt >= 0f) return Vector2.zero;
        float amp = 0f;
        foreach (float land in AppLand) if (t >= land && t < land + 0.12f) amp = 2f;
        if (t >= FinderLand && t < FinderLand + 0.12f) amp = 2f;
        if (t >= CollapseAt && t < 9f) amp = Mathf.Max(amp, 1f);
        if (t >= 9f && t < SlamAt) amp = 2f;
        if (t >= SlamAt && t < SlamAt + 0.3f) amp = Mathf.Lerp(4f, 0f, (t - SlamAt) / 0.3f);
        int a = Mathf.RoundToInt(amp);
        if (a <= 0) return Vector2.zero;
        var rng = new System.Random(Mathf.FloorToInt(Now * 30f) * 7121);
        return new Vector2(rng.Next(-a, a + 1), rng.Next(-a, a + 1));
    }

    // ───────── 起動の演出の中（DrawBoot） ─────────

    /// <summary>メニューバーに出す「感染 N」（0 なら null）</summary>
    public static string Status(float t, int appCount)
    {
        if (!Enabled) return null;
        int n = Infected(t, appCount);
        return n > 0 ? $"感染 {n}" : null;
    }

    /// <summary>紫のとばり・壁紙をはうウイルス・かけら・横の線・画面全体の光（フォルダ・Dock より先に描く）。t&lt;0 なら何もしない</summary>
    public static void DrawBack(float t, float W, float H, int appCount)
    {
        if (!Enabled || t < 0f) return;
        float l = L;
        float push = l < 0f ? 0f : Mathf.Clamp01(l / PushTime);
        float veil = Curve(t, VeilFrom, 0f, 6.5f, 0.25f, 10f, 0.50f, SlamAt, VeilMax);
        if (l >= 0f) veil = Mathf.Lerp(VeilMax, VeilLinger, push);
        Veil(W, H, veil);

        Rect dock = DockRect(W, H, appCount);
        Rect game = IconRect(dock, 1);
        // 壁紙をはう 6匹（T=4.0 から 0.8 秒ごと。壁紙とDockで交互に出る）。押した後は輪が通ると消える
        for (int i = 0; i < WallTypes.Length; i++)
        {
            float appear = CrawlFrom + CrawlEvery * (i * 2);
            if (t < appear) continue;
            Vector2 p = WallPos(i, W, H, Now, 1f);
            if (t >= DarkAt) p = Swarm(p, game, Ramp(t, DarkAt, DarkAt + 1f));
            var r = new Rect(p.x, p.y, 26f, 24f);
            if (l >= 0f && !Survives(r.center, game.center, W, H, l, out float since, 20f, ref r)) { Pop(r.center, since); continue; }
            DrawVirus(r, WallTypes[i], i, 1f, WallFacing(i, W, Now));
            Appear(r, t - appear);
        }

        int frags = t < CollapseAt ? Mathf.Min(20, 4 * Infected(t, appCount)) : Mathf.Min(40, 20 + Mathf.FloorToInt((t - CollapseAt) * 6f));
        if (l >= 0f) frags = Mathf.RoundToInt(Mathf.Lerp(40, 6, push));
        if (frags > 0) Fragments(W, H, frags, 0.25f + 0.03f * Mathf.Min(5, Infected(t, appCount)), t < CollapseAt ? 8f : 12f);

        if (t < Duration && l < 0f)
        {
            Line(t, BreachAt, W, H * 0.35f);
            Line(t, DocumentsDyeAt, W, H * 0.6f);
            Line(t, CollapseAt, W, H * 0.25f);
            Line(t, 9.7f, W, H * 0.5f);
            // 画面全体の光：設計は 0.15・0.18・0.30。Linear の色空間だと画面がマゼンタ一色に見えたので、4割くらいに下げた
            Flash(t, BreachAt, 0.06f, 0.06f, W, H);
            foreach (float a in AlarmAt) Flash(t, a, 0.08f, 0.07f, W, H);
            Flash(t, SlamAt, 0.10f, 0.12f, W, H);
        }
    }

    /// <summary>フォルダ・Dock の紫・乗ったウイルス・Dock を歩くウイルス・警告の窓・ゲージ・暗さ・跳びかかってはじかれるウイルス・青い輪・札・押し返す輪
    /// （MacDesktopGui.Dock のすぐ後、ゲームの窓より先に描く）。apps＝Dock のアプリ。hop＝ゲームのアイコンの跳ね（Dock に渡した物）。launched＝押した後</summary>
    public static void DrawFront(float t, float W, float H, IReadOnlyList<MacDesktopGui.DockItem> apps, float hop, bool launched)
    {
        if (!Enabled || t < 0f) return;
        if (launched && _launchAt < 0f) _launchAt = Now;
        float l = L, wt = Wt;
        int count = apps != null ? apps.Count : 0;
        int n = Apps(count);
        Rect dock = DockRect(W, H, count);
        Rect gameHome = IconRect(dock, 1);
        Rect game = Hovered(gameHome);
        if (hop >= 0f) game.y -= Mathf.Abs(Mathf.Sin(hop * Mathf.PI * 2f)) * 7f; // MacDesktopGui.Dock の跳ねと同じ

        // 通知（不明なファイル）
        if (t >= NoticeIn && t < NoticeOut + 0.2f && l < 0f)
        {
            float slide = t < NoticeOut ? 1f - Ramp(t, NoticeIn, NoticeIn + 0.2f) : Ramp(t, NoticeOut, NoticeOut + 0.2f);
            Notice(W, slide, "ダウンロード", "不明なファイルを開きました", false);
        }

        // フォルダ（ダウンロード → 書類）。染まりきったら、ウイルスが座る
        Folder(1, W, Dye(t, DownloadsDyeAt));
        Folder(0, W, Dye(t, DocumentsDyeAt));
        if (t >= DownloadsDyeAt + DyeTime) SeatOnFolder(1, W, "Worm", 20);
        if (t >= DocumentsDyeAt + DyeTime) SeatOnFolder(0, W, "Macro", 21);

        // Dock：右のアプリから順に染まる。最後は Finder
        for (int k = 0; k < n; k++)
        {
            if (t < AppLand[k]) continue;
            int i = count + 1 - k;
            Rect r = Hovered(IconRect(dock, i));
            InfectedApp(r, apps[i - 2].Icon, i);
            Bites(r, i, t);
            DrawVirus(new Rect(r.center.x - 13f, r.y - 16f, 26f, 24f), LandType[k], i, 1f, false);
            Arrive(r, t - AppLand[k]);
        }
        if (t >= FinderLand)
        {
            Rect r = Hovered(IconRect(dock, 0));
            InfectedFinder(r);
            Bites(r, 0, t);
            DrawVirus(new Rect(r.center.x - 13f, r.y - 16f, 26f, 24f), FinderType, 0, 1f, false);
            Arrive(r, t - FinderLand);
        }

        // Dock の上を歩く 6匹（ゲームのアイコンのまわりには入らない）
        for (int j = 0; j < DockTypes.Length; j++)
        {
            float appear = CrawlFrom + CrawlEvery * (j * 2 + 1);
            if (t < appear) continue;
            var r = new Rect(DockWalkX(j, dock, gameHome, Now, out bool left), dock.y - 23f, 26f, 24f);
            if (l >= 0f && !Survives(r.center, game.center, W, H, l, out float since, 20f, ref r)) { Pop(r.center, since); continue; }
            DrawVirus(r, DockTypes[j], 6 + j, 1f, left);
            Appear(r, t - appear);
        }

        // 警告の窓 10枚（待ちでは古い順に出直す）とゲージの窓
        for (int i = 0; i < WarnAt.Length; i++)
        {
            if (t < WarnAt[i]) continue;
            Rect r = WarnRect(i, W, H);
            float since = t - WarnAt[i];
            int text = i;
            if (wt >= 0f && l < 0f)
            {
                // 1.4 秒ごとに 1枚（古い順）、0.15 秒消えて同じ所に出直す（一言は次の文に）
                int cycle = Mathf.FloorToInt(wt / WaitPopupEvery);
                if (cycle >= 1)
                {
                    int done = cycle - 1; // 出直した回数の合計 − 1
                    text = i + (done >= i ? (done - i) / WarnAt.Length + 1 : 0);
                    if (done % WarnAt.Length == i)
                    {
                        float into = wt - cycle * WaitPopupEvery;
                        if (into < 0.15f) continue;
                        since = into - 0.15f;
                    }
                }
            }
            if (l >= 0f)
            {
                if (!Survives(r.center, game.center, W, H, l, out float gone, 0f, ref r)) { Shatter(r.center, gone); continue; }
            }
            WarnWindow(r, WarnText[text % WarnText.Length], since, i, true);
            DrawVirus(new Rect(r.xMax - 34f, r.y - 22f, 26f, 24f), AllTypes[i % AllTypes.Length], 30 + i, 1f, false);
        }
        if (t >= GaugeAt) Gauge(t, W, H, game.center, l);

        // ダウンロードから Dock のアイコンへ跳ぶウイルス
        for (int k = 0; k < n; k++) Jump(t, AppLand[k] - JumpDur(k), JumpDur(k), IconRect(dock, count + 1 - k), W, LandType[k], count + 1 - k);
        Jump(t, FinderLand - JumpDur(n), JumpDur(n), IconRect(dock, 0), W, FinderType, 0);

        // にせの矢印（勝手に動いて窓を開けていく。Spyware が乗っている）
        if (t >= CursorFrom) FakeCursor(t, W, H, game, l);

        // 暗さ（ゲームのアイコンのまわりだけ明るい）
        if (t >= DarkAt)
        {
            float dark = 0.35f * Ramp(t, DarkAt, DarkAt + 1f);
            float beat = Now - _lastBeat;
            if (beat >= 0f && beat < 0.2f && l < 0f) dark += 0.07f * (1f - beat / 0.2f);
            if (l >= 0f) dark *= 1f - Mathf.Clamp01(l / PushTime);
            if (dark > 0.001f) Darkness(W, H, PixelGui.Inset(game, -10f), dark);
        }

        // 跳びかかって、はじかれる（A の3回と、待ちのくり返し）
        float lastHit = -10f;
        if (l < 0f)
        {
            if (wt < 0f)
            {
                for (int i = 0; i < LeapHit.Length; i++)
                {
                    if (t >= LeapHit[i] - LeapTime && t < LeapHit[i] + 0.35f) Leap(game, t - (LeapHit[i] - LeapTime), LeapType[i], 40 + i, 1f + i * 0.25f);
                    if (t >= LeapHit[i]) lastHit = Mathf.Max(lastHit, Now - (t - LeapHit[i]));
                }
            }
            else
            {
                for (int m = 1; ; m++)
                {
                    float hit = WaitLeapHit(m);
                    if (hit - LeapTime > wt) break;
                    if (wt < hit + 0.35f) Leap(game, wt - (hit - LeapTime), AllTypes[(m + 3) % AllTypes.Length], 50 + m, 1f);
                    if (wt >= hit) lastHit = Now - (wt - hit);
                }
            }
        }

        // 逃げ場：青い輪・白い枠ときらめき・札（押した後は消える）
        if (!launched && l < 0f)
        {
            if (t >= RingFrom) GuardRing(game, Now - lastHit);
            if (t >= HaloFrom) Halo(game);
            if (t >= TagAt) Tag(gameHome, dock, t, wt);
        }

        // 押した後：青い輪がアイコンから画面いっぱいへ
        if (l >= 0f && l < PushTime) Ring(game.center, PushRadius(l, game.center, W, H), 1f);
    }

    static Font _menuFont;

    /// <summary>メニューバーの電池と時計がおかしくなる（MenuBar のすぐ後に呼ぶ。MacDesktopGui.MenuBar と同じ計算で場所を出して、実際の画面のドットで上から描く）。
    /// 電池：T=5 からマゼンタになって 11 までに減り、待ちでは点滅。時計：T=7 から数字がでたらめに回る。押した後は元のまま</summary>
    public static void DrawMenuBarOverlay(float t, float W)
    {
        if (!Enabled || t < BatteryFrom || _launchAt >= 0f) return;
        Matrix4x4 m = GUI.matrix; Color col = GUI.color; bool en = GUI.enabled;
        GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true;
        try
        {
            if (_menuFont == null) _menuFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Hiragino Sans", "Arial" }, 16);
            float w = W * PixelGui.Scale;
            float scale = Mathf.Clamp(Screen.height / 810f, 1f, 2f), h = Mathf.Round(25 * scale);
            Color bar = new Color32(28, 28, 30, 255), text = new Color32(246, 246, 248, 255);
            var style = new GUIStyle { font = _menuFont, fontSize = Mathf.RoundToInt(13 * scale), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = PixelGui.ToGui(text) } };
            float right = w - 18 * scale;
            string date = System.DateTime.Now.ToString("M月d日(ddd)  HH:mm", System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
            float dateWidth = style.CalcSize(new GUIContent(date)).x;
            if (t >= ClockFrom)
            {
                // 時計：数字だけでたらめに（0.08 秒ごと。待ちでは 0.5 秒ごと）
                int step = Mathf.FloorToInt(Now / (_waitFrom >= 0f ? 0.5f : 0.08f));
                var rng = new System.Random(step * 4217);
                var chars = date.ToCharArray();
                for (int i = 0; i < chars.Length; i++) if (char.IsDigit(chars[i])) chars[i] = (char)('0' + rng.Next(10));
                PixelGui.Fill(new Rect(right - dateWidth - 2 * scale, 1, dateWidth + 4 * scale, h - 2), bar);
                var glitchStyle = new GUIStyle(style) { normal = { textColor = PixelGui.ToGui(Color.Lerp(text, PixelGui.Magenta, 0.5f)) } };
                GUI.Label(new Rect(right - dateWidth, 0, dateWidth + 8 * scale, h), new string(chars), glitchStyle);
            }
            right -= dateWidth + 19 * scale + 34 * scale;
            var battery = new Rect(right - 24 * scale, 8 * scale, 23 * scale, 10 * scale);
            // 電池：中の白を消して、マゼンタの棒が減っていく（待ちでは点滅）
            PixelGui.Fill(new Rect(battery.x + 2 * scale, battery.y + 2 * scale, 17 * scale, 6 * scale), bar);
            float left = Mathf.Lerp(17f, 2f, Ramp(t, BatteryFrom, BatteryEmpty));
            bool blinkOff = _waitFrom >= 0f && Mathf.FloorToInt(Now * 2f) % 2 == 1;
            if (!blinkOff) PixelGui.Fill(new Rect(battery.x + 2 * scale, battery.y + 2 * scale, Mathf.Round(left * scale), 6 * scale), PixelGui.Magenta);
        }
        finally { GUI.matrix = m; GUI.color = col; GUI.enabled = en; }
    }

    // ───────── 起動のあと（ふつうの OnGUI）：のこり ─────────

    /// <summary>起動のあとのメニューバーの「感染 N」（全クリアしたら null）</summary>
    public static string LingerStatus(int appCount) => Enabled && !Cured ? $"感染 {Apps(appCount) + 1}" : null;

    /// <summary>起動のあとの紫のとばり・壁紙をはう3匹・かけら（フォルダより先に描く）</summary>
    public static void DrawLingerBack(float W, float H)
    {
        if (!Enabled || Cured) return;
        Veil(W, H, VeilLinger);
        for (int i = 0; i < 3; i++)
            DrawVirus(new Rect(WallPos(i, W, H, Now, 0.5f), new Vector2(26f, 24f)), WallTypes[i], i, 1f, WallFacing(i, W, Now * 0.5f));
        Fragments(W, H, 6, 0.20f, 4f);
    }

    /// <summary>起動のあとのフォルダと Dock（MacDesktopGui.Dock のすぐ後に描く）。items＝Dock に渡した物。最小化した窓の項目には重ねない</summary>
    public static void DrawLingerFront(float W, float H, IReadOnlyList<MacDesktopGui.DockItem> items)
    {
        if (!Enabled || Cured) return;
        Folder(0, W, 1f);
        Folder(1, W, 1f);
        int count = items != null ? items.Count : 0;
        int apps = 0;
        for (int j = 0; j < count; j++) if (items[j].Window == null) apps++;
        Rect dock = DockRect(W, H, count);
        Rect finder = Hovered(IconRect(dock, 0));
        InfectedFinder(finder);
        DrawVirus(new Rect(finder.center.x - 13f, finder.y - 16f, 26f, 24f), FinderType, 0, 1f, false);
        int seen = 0;
        for (int j = 0; j < count && seen < AppLand.Length; j++)
        {
            if (items[j].Window != null) continue;
            int k = Mathf.Min(AppLand.Length, apps) - 1 - seen; // 右から何番目か（起動の演出と同じ種類）
            seen++;
            Rect r = Hovered(IconRect(dock, j + 2));
            InfectedApp(r, items[j].Icon, j + 2);
            DrawVirus(new Rect(r.center.x - 13f, r.y - 16f, 26f, 24f), LandType[Mathf.Clamp(k, 0, LandType.Length - 1)], j + 2, 1f, false);
        }
    }

    // ───────── 位置（DesktopCure からも使う） ─────────

    // MacDesktopGui.Dock と同じ計算（Finder・ゲーム・アプリの順）
    internal static Rect DockRect(float W, float H, int appCount)
    {
        float w = (2 + appCount) * 42f + 12f;
        return new Rect(Mathf.Round((W - w) * 0.5f), H - 54, w, 48);
    }

    internal static Rect IconRect(Rect dock, int i) => new Rect(dock.x + 9 + i * 42, dock.y + 6, 30, 30);

    // マウスが乗ると 3 ドット上がる（MacDesktopGui.Dock と同じ条件）
    internal static Rect Hovered(Rect r)
    {
        if (GUI.enabled && r.Contains(Event.current.mousePosition)) r.y -= 3f;
        return r;
    }

    static float Dye(float t, float at) => t < at ? 0f : Mathf.Clamp01((t - at) / DyeTime);

    // 画面全体（メニューバーの下から。ゆれで端が見えないよう、左右と下は 8 ドット外まで）
    internal static Rect Full(float W, float H) => new Rect(-8f, 26f, W + 16f, H - 18f);

    // 警告の窓の位置（フォルダとゲームのアイコンを隠さない「置ける四角」の中の割合）
    internal static Rect WarnRect(int i, float W, float H)
    {
        float spanX = Mathf.Min(W - 306f, 440f), spanY = Mathf.Min(H - 196f, 230f);
        float left = Mathf.Max(8f, Mathf.Round((W - 290f - spanX) * 0.5f)), top = 30f;
        Vector2 s = WarnSpot[i % WarnSpot.Length];
        return new Rect(Mathf.Round(left + s.x * spanX), Mathf.Round(top + s.y * spanY), 200f, 72f);
    }

    // 壁紙をはうウイルスの位置（四角の中をまっすぐ進み、はしではね返る）
    internal static Vector2 WallPos(int i, float W, float H, float time, float speed)
    {
        var box = new Rect(8f, 30f, W - 106f, H - 130f);
        float sx = (30f + (i * 9) % 26) * speed, sy = (22f + (i * 13) % 20) * speed;
        float x = box.x + Mathf.PingPong(i * 53.7f + time * sx, Mathf.Max(1f, box.width - 26f));
        float y = box.y + Mathf.PingPong(i * 31.3f + time * sy, Mathf.Max(1f, box.height - 24f));
        return new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    // 進む向き（左へ進むときは絵を反転）
    internal static bool WallFacing(int i, float W, float time)
    {
        float sx = 30f + (i * 9) % 26;
        return Mathf.FloorToInt((i * 53.7f + time * sx) / Mathf.Max(1f, W - 132f)) % 2 == 1;
    }

    // T=10 から、壁紙のウイルスがゲームのアイコンへ 20% 寄る（群がる。まわりの「入れない所」には入らない）
    static Vector2 Swarm(Vector2 p, Rect game, float k)
    {
        Vector2 q = Vector2.Lerp(p, new Vector2(game.center.x - 13f, game.center.y - 12f), 0.2f * k);
        var noGo = PixelGui.Inset(game, -14f);
        return new Rect(q.x, q.y, 26f, 24f).Overlaps(noGo) ? p : new Vector2(Mathf.Round(q.x), Mathf.Round(q.y));
    }

    // Dock の上を歩くウイルスの x（1匹はゲームの左、5匹は右の中だけを往復）
    static float DockWalkX(int j, Rect dock, Rect game, float time, out bool facingLeft)
    {
        float lo, hi;
        if (j == 0) { lo = dock.x - 50f; hi = game.center.x - 96f; }
        else { lo = game.center.x + 70f; hi = dock.xMax + 50f - 26f; }
        float range = Mathf.Max(1f, hi - lo);
        float phase = j * 41f + time * (20f + (j * 7) % 21);
        facingLeft = Mathf.FloorToInt(phase / range) % 2 == 1;
        return Mathf.Round(lo + Mathf.PingPong(phase, range));
    }

    // 押した後の青い輪の半径（始めが速い）
    static float PushRadius(float l, Vector2 c, float W, float H)
    {
        float rmax = FarCorner(c, W, H);
        float p = Mathf.Clamp01(l / PushTime);
        return rmax * (1f - (1f - p) * (1f - p));
    }

    internal static float FarCorner(Vector2 c, float W, float H)
        => Mathf.Max(Mathf.Max(c.magnitude, Vector2.Distance(c, new Vector2(W, 0f))), Mathf.Max(Vector2.Distance(c, new Vector2(0f, H)), Vector2.Distance(c, new Vector2(W, H))));

    // 輪が d の所を通る L（PushRadius の逆）
    static float PassTime(float d, float rmax)
    {
        float x = Mathf.Clamp01(d / Mathf.Max(1f, rmax));
        return (1f - Mathf.Sqrt(1f - x)) * PushTime;
    }

    // 押した後：輪がまだ通っていなければ true（push＞0 なら、輪が近づくほど外へ押される）。通った後は false と、通ってからの秒
    static bool Survives(Vector2 p, Vector2 c, float W, float H, float l, out float since, float push, ref Rect r)
    {
        float pass = PassTime(Vector2.Distance(p, c), FarCorner(c, W, H));
        since = l - pass;
        if (since >= 0f) return false;
        if (push > 0f)
        {
            float near = Mathf.Clamp01(1f + since / 0.3f); // 通る 0.3 秒前から押される
            Vector2 dir = (p - c).sqrMagnitude > 0.01f ? (p - c).normalized : Vector2.up;
            r.position += dir * push * near;
        }
        return true;
    }

    // ───────── 絵（DesktopCure からも使う） ─────────

    // 紫のとばり（壁紙が紫に沈む）。ふちはもう1段暗い
    internal static void Veil(float W, float H, float a)
    {
        if (a <= 0.001f) return;
        Rect f = Full(W, H);
        PixelGui.Fill(f, Of(PixelGui.Ink, a * 0.75f));
        PixelGui.Fill(f, Of(PixelGui.LabelPurple, a * 0.45f));
        Color edge = Of(PixelGui.Ink, a * 0.5f);
        PixelGui.Fill(new Rect(f.x, f.y, f.width, 24f), edge);
        PixelGui.Fill(new Rect(f.x, f.yMax - 32f, f.width, 32f), edge);
        PixelGui.Fill(new Rect(f.x, f.y + 24f, 32f, f.height - 56f), edge);
        PixelGui.Fill(new Rect(f.xMax - 32f, f.y + 24f, 32f, f.height - 56f), edge);
    }

    // 壁紙のかけら（DesktopScreen.DrawWallpaper と同じ。3つに1つは黒）。skip＝描かない所（輪の内がわなど）
    internal static void Fragments(float W, float H, int count, float alpha, float rate, System.Func<Vector2, bool> skip = null)
    {
        int step = Mathf.FloorToInt(Now * rate);
        var rng = new System.Random(step * 7919);
        for (int i = 0; i < count; i++)
        {
            int w = rng.Next(6, 61), h = rng.Next(2, 7);
            float x = rng.Next(0, Mathf.Max(1, (int)(W - w)));
            float y = rng.Next(26, Mathf.Max(27, (int)(H - 8 - h)));
            bool black = rng.Next(3) == 0;
            if (skip != null && skip(new Vector2(x + w * 0.5f, y))) continue;
            PixelGui.Fill(new Rect(x, y, w, h), black ? new Color(0f, 0f, 0f, 0.45f) : Magenta(alpha));
        }
    }

    static void Line(float t, float at, float W, float y)
    {
        if (t < at || t >= at + 0.12f) return;
        PixelGui.Fill(new Rect(-8f, Mathf.Round(y), W + 16f, 4f), Magenta(0.5f));
    }

    static void Flash(float t, float at, float len, float a, float W, float H)
    {
        if (t < at || t >= at + len) return;
        PixelGui.Fill(Full(W, H), Magenta(a));
    }

    // フォルダ（0＝書類、1＝ダウンロード）を上から紫に染める。p＝染まった割合（1→0 で、下から青に戻る）
    internal static void Folder(int i, float W, float p)
    {
        if (p <= 0f) return;
        float x = W - 63f, y = 43f + i * 64f;
        float bottom = y + Mathf.Round(26f * p);
        FillAbove(new Rect(x, y, 14f, 6f), PixelGui.LabelPurple, bottom);
        FillAbove(new Rect(x, y + 4f, 32f, 22f), PixelGui.LabelPurple, bottom);
        FillAbove(new Rect(x + 1f, y + 8f, 30f, 17f), PixelGui.Lilac, bottom);
        if (p < 1f) return;
        int step = Mathf.FloorToInt(Now * 6f);
        PixelGui.Fill(new Rect(x + 5f, y + 11f + ((step + i) % 3) * 4f, 22f, 2f), PixelGui.Magenta);
    }

    // 染まったフォルダに座るウイルス
    internal static void SeatOnFolder(int i, float W, string type, int index)
    {
        float cx = W - 47f, top = 43f + i * 64f;
        DrawVirus(new Rect(cx - 13f, top - 16f, 26f, 24f), type, index, 1f, false);
    }

    static void FillAbove(Rect r, Color color, float bottom)
    {
        if (r.y >= bottom) return;
        r.height = Mathf.Min(r.height, bottom - r.y);
        PixelGui.Fill(r, color);
    }

    // 染まったアプリのアイコン：うす紫の台に紫の絵。ときどきずれて、マゼンタの残像（DesktopScreen.DrawAppIcon と同じ）
    internal static void InfectedApp(Rect r, Sprite icon, int index)
    {
        MacDesktopGui.Rounded(r, PixelGui.Lilac, 5);
        if (icon == null) return;
        int step = Mathf.FloorToInt(Now * 10f) + index * 3;
        bool glitch = step % 7 == 0 || step % 11 == 0;
        float dx = glitch ? (step % 2 == 0 ? 2f : -2f) : 0f;
        Rect inner = PixelGui.Inset(r, 2f);
        PixelGui.DrawSprite(new Rect(inner.x + dx, inner.y, inner.width, inner.height), icon, InfectTint);
        if (glitch) PixelGui.DrawSprite(new Rect(inner.x - dx * 2f, inner.y, inner.width, inner.height), icon, Magenta(0.45f));
    }

    // 染まった Finder（紫を重ねてマゼンタの線を走らせる）
    internal static void InfectedFinder(Rect r)
    {
        MacDesktopGui.Rounded(r, Of(PixelGui.LabelPurple, 0.55f), 5);
        int step = Mathf.FloorToInt(Now * 10f);
        PixelGui.Fill(new Rect(r.x + 3f, r.y + 6f + (step % 3) * 8f, 24f, 2f), PixelGui.Magenta);
    }

    // かじられた穴（T=6.5 から 0.5 秒に 1個、最大 8個）
    static void Bites(Rect r, int index, float t)
    {
        if (t < BiteFrom) return;
        int n = Mathf.Min(8, Mathf.FloorToInt((t - BiteFrom) / 0.5f) + 1);
        var rng = new System.Random(index * 977 + 13);
        for (int k = 0; k < n; k++)
            PixelGui.Fill(new Rect(r.x + 2f + rng.Next(0, 24), r.y + 2f + rng.Next(0, 24), 3f, 3f), PixelGui.Ink);
    }

    // 乗った瞬間：アイコンのまわりがマゼンタと濃い紫のかけらで乱れる（DesktopScreen.DrawArrival と同じ）
    static void Arrive(Rect r, float since)
    {
        if (since < 0f || since >= 0.15f) return;
        var rng = new System.Random(Mathf.FloorToInt(Now * 20f));
        float a = 1f - since / 0.15f;
        for (int k = 0; k < 10; k++)
        {
            int w = rng.Next(4, 20), h = rng.Next(2, 6);
            var b = new Rect(r.x - 4f + rng.Next(0, Mathf.Max(1, (int)(r.width + 8f - w))), r.y - 4f + rng.Next(0, Mathf.Max(1, (int)(r.height + 8f - h))), w, h);
            Color c = rng.Next(2) == 0 ? PixelGui.Magenta : PixelGui.Ink;
            PixelGui.Fill(b, Of(c, a));
        }
    }

    // 出た瞬間（0.2 秒）：白い火花
    static void Appear(Rect r, float since)
    {
        if (since < 0f || since >= 0.2f) return;
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.HitSpark : null;
        if (frames == null || frames.Length == 0) { PixelGui.Fill(PixelGui.Inset(r, 6f), new Color(1f, 1f, 1f, 1f - since / 0.2f)); return; }
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(since / 0.2f * frames.Length))];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.center.x - fr.width * 0.5f), Mathf.Round(r.center.y - fr.height * 0.5f), fr.width, fr.height), f);
    }

    /// <summary>ウイルスの絵（26×24。種類ごとの歩く絵。歩く絵でない種類は 1コマ目で、0.3 秒ごとに 1 ドット上下）。絵が無ければ雑魚の絵</summary>
    internal static void DrawVirus(Rect r, string type, int index, float alpha, bool flip)
    {
        Sprite[] frames = VirusArt.Frames(type);
        Color tint = new Color(1f, 1f, 1f, alpha);
        if (frames == null || frames.Length == 0)
        {
            Sprite z = ZakoFrame(index * 0.37f);
            if (z != null) PixelGui.DrawSprite(new Rect(r.center.x - 8f, r.yMax - 16f, 16f, 16f), z, tint);
            return;
        }
        bool walks = VirusArt.TryGetSpec(type, out VirusArt.Spec spec) && spec.Fps > 0f;
        Sprite f = walks ? frames[Mathf.FloorToInt(Now * 6f + index * 0.37f * frames.Length) % frames.Length] : frames[0];
        if (!walks && Mathf.FloorToInt(Now / 0.3f + index) % 2 == 0) r.y -= 1f;
        DrawSpriteFlip(r, f, tint, flip);
    }

    static void DrawSpriteFlip(Rect rect, Sprite sprite, Color tint, bool flip)
    {
        if (!flip) { PixelGui.DrawSprite(rect, sprite, tint); return; }
        Texture2D tex = sprite.texture;
        Rect s = sprite.textureRect;
        var uv = new Rect((s.x + s.width) / tex.width, s.y / tex.height, -s.width / tex.width, s.height / tex.height);
        Color saved = GUI.color;
        GUI.color = PixelGui.ToGui(tint);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);
        GUI.color = saved;
    }

    /// <summary>警告の窓（Mac の窓。押せない）。since＝出てからの秒（0.15 秒ふるえる）。glitch＝ときどきずれてマゼンタの線</summary>
    internal static void WarnWindow(Rect r, string text, float since, int index, bool glitch)
    {
        int glitchStep = -1;
        if (since < 0.15f) r.x += Mathf.FloorToInt(Now * 30f) % 2 == 0 ? 2f : -2f;
        else if (glitch)
        {
            int step = Mathf.FloorToInt(Now * 10f) + index * 4;
            if (step % 9 == 0) { r.x += 2f; glitchStep = step; }
        }
        Rect inner = MacDesktopGui.Window(r, "警告", out _);
        var icon = new Rect(inner.x, inner.y, 24f, 24f);
        MacDesktopGui.Rounded(icon, PixelGui.Lilac, 4);
        Sprite zako = ZakoFrame(index * 0.37f);
        if (zako != null) PixelGui.DrawSprite(new Rect(icon.x + 4f, icon.y + 4f, 16f, 16f), zako);
        PixelGui.TextFit(new Rect(inner.x + 32f, inner.y + 2f, inner.width - 32f, 20f), text, 12, MacDesktopGui.Ink);
        if (glitchStep >= 0) PixelGui.Fill(new Rect(r.x + 1f, r.y + 30f + (glitchStep / 9 % 3) * 13f, r.width - 2f, 2f), PixelGui.Magenta);
    }

    // ゲージの窓「感染が広がっています N%」
    static void Gauge(float t, float W, float H, Vector2 c, float l)
    {
        var r = new Rect(Mathf.Round((W - 326f) * 0.5f), Mathf.Round(30f + (H - 196f) * 0.40f), 236f, 80f);
        if (l >= 0f && !Survives(r.center, c, W, H, l, out float gone, 0f, ref r)) { Shatter(r.center, gone); return; }
        if (t >= 9f && Mathf.FloorToInt(Now * 2f) % 2 == 0) r.y += 1f;
        float since = t - GaugeAt;
        if (since < 0.15f) r.x += Mathf.FloorToInt(Now * 30f) % 2 == 0 ? 2f : -2f;
        int value = Mathf.FloorToInt(Curve(t, GaugeAt, 3f, 6.5f, 35f, 10f, 88f, SlamAt, 99f));
        float wt = Wt;
        if (wt >= 0f && (wt % 1.3f) < 0.1f) value = 98; // 待ち：ときどき 98% にふるえる
        Rect inner = MacDesktopGui.Window(r, "警告", out _);
        PixelGui.TextFit(new Rect(inner.x, inner.y - 2f, inner.width - 44f, 18f), "感染が広がっています", 12, MacDesktopGui.Ink);
        PixelGui.Text(new Rect(inner.xMax - 44f, inner.y - 2f, 44f, 18f), value + "%", 12, PixelGui.LabelPurple, TextAnchor.MiddleRight);
        var bar = new Rect(inner.x, inner.y + 20f, inner.width, 8f);
        PixelGui.Fill(bar, MacDesktopGui.BorderColor);
        float fill = Mathf.Round(bar.width * value / 100f);
        PixelGui.Fill(new Rect(bar.x, bar.y, fill, bar.height), PixelGui.LabelPurple);
        PixelGui.Fill(new Rect(bar.x + Mathf.Max(0f, fill - 6f), bar.y, Mathf.Min(6f, fill), bar.height), PixelGui.Magenta);
        if (value >= 98 && Mathf.FloorToInt(Now * 4f) % 2 == 0)
            PixelGui.Fill(new Rect(bar.xMax - 2f, bar.y, 2f, bar.height), MacDesktopGui.Blue); // のこりの 1% だけ青（まだ間に合う）
    }

    // 暗さ：穴の外を4つの四角で塗る
    static void Darkness(float W, float H, Rect hole, float a)
    {
        Rect f = Full(W, H);
        Color c = Of(PixelGui.Ink, a);
        PixelGui.Fill(new Rect(f.x, f.y, f.width, Mathf.Max(0f, hole.y - f.y)), c);
        PixelGui.Fill(new Rect(f.x, hole.yMax, f.width, Mathf.Max(0f, f.yMax - hole.yMax)), c);
        PixelGui.Fill(new Rect(f.x, hole.y, Mathf.Max(0f, hole.x - f.x), hole.height), c);
        PixelGui.Fill(new Rect(hole.xMax, hole.y, Mathf.Max(0f, f.xMax - hole.xMax), hole.height), c);
    }

    // にせの矢印の位置（時刻ごとの点をなめらかにつなぐ。窓が出る 0.1 秒前に、その窓の左上へ着く）
    static Vector2 CursorPos(float t, float W, float H, Rect game)
    {
        var folder = new Vector2(W - 47f, 120f);
        if (t < 6.4f) return Ease(folder, WarnRect(0, W, H).position, (t - CursorFrom) / (6.4f - CursorFrom));
        for (int i = 1; i < WarnAt.Length; i++)
        {
            float arrive = WarnAt[i] - 0.1f, leave = i == 1 ? 6.4f : WarnAt[i - 1] - 0.1f;
            if (t < arrive) return Ease(WarnRect(i - 1, W, H).position, WarnRect(i, W, H).position, (t - leave) / (arrive - leave));
        }
        var last = WarnRect(WarnAt.Length - 1, W, H).position;
        var edge = new Vector2(game.xMax + 14f, game.y - 18f); // 「入れない所」のすぐ外
        var rest = new Vector2(Mathf.Round(W * 0.4f), Mathf.Round(H * 0.45f));
        if (t < 10f) return last;
        if (t < 10.3f) return Ease(last, edge, (t - 10f) / 0.3f);
        if (t < 10.8f) return Ease(edge, rest, (t - 10.3f) / 0.5f);
        return rest;
    }

    static Vector2 Ease(Vector2 a, Vector2 b, float p)
    {
        p = Mathf.Clamp01(p);
        p = p * p * (3f - 2f * p);
        return Vector2.Lerp(a, b, p);
    }

    // にせの矢印：マゼンタのふち（矢印の絵にマゼンタを掛ける）。後ろに残像2つ。待ちでは ±1 ドットふるえる
    static void FakeCursor(float t, float W, float H, Rect game, float l)
    {
        Vector2 p = CursorPos(t, W, H, game);
        if (_waitFrom >= 0f)
        {
            var rng = new System.Random(Mathf.FloorToInt(Now * 12f) * 31);
            p += new Vector2(rng.Next(-1, 2), rng.Next(-1, 2));
        }
        var r = new Rect(Mathf.Round(p.x), Mathf.Round(p.y), 10f, 15f);
        if (l >= 0f && !Survives(r.center, game.center, W, H, l, out float gone, 20f, ref r)) { Pop(r.center, gone); return; }
        Sprite arrow = MacUi.Arrow();
        if (t < Duration)
            for (int g = 2; g >= 1; g--)
            {
                Vector2 q = CursorPos(t - g * 0.05f, W, H, game);
                MacUi.Gui.Sprite(new Rect(Mathf.Round(q.x), Mathf.Round(q.y), 10f, 15f), arrow, Magenta(0.25f * (3 - g)));
            }
        MacUi.Gui.Sprite(r, arrow, PixelGui.Magenta);
        DrawVirus(new Rect(r.x + 6f, r.y + 8f, 26f, 24f), "Spyware", 77, 1f, false);
    }

    // ダウンロードのフォルダから、的のアイコンの上へ山なりに跳ぶウイルス（後ろにマゼンタの残像）
    static void Jump(float t, float start, float dur, Rect target, float W, string type, int index)
    {
        float s = t - start;
        if (s < 0f || s >= dur) return;
        var from = new Vector2(W - 47f - 13f, 120f - 12f);
        var to = new Vector2(target.center.x - 13f, target.y - 16f);
        if (s >= 0.06f) DrawVirus(new Rect(JumpPos(s - 0.06f, dur, from, to), new Vector2(26f, 24f)), type, index, 0.45f, true);
        DrawVirus(new Rect(JumpPos(s, dur, from, to), new Vector2(26f, 24f)), type, index, 1f, true);
    }

    static Vector2 JumpPos(float s, float dur, Vector2 from, Vector2 to)
    {
        float p = Mathf.Clamp01(s / dur);
        Vector2 pos = Vector2.Lerp(from, to, p);
        pos.y -= Mathf.Sin(p * Mathf.PI) * 36f;
        return new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
    }

    // 跳びかかって、はじかれるウイルス。s＝跳び始めてからの秒（0〜0.4 跳ぶ、0.4〜0.75 はじかれて戻りながら散る）
    static void Leap(Rect game, float s, string type, int index, float power)
    {
        var to = new Vector2(game.center.x - 13f, game.y - 18f);
        var from = to + new Vector2(64f, -64f);
        if (s < LeapTime)
        {
            float p = s / LeapTime;
            Vector2 pos = Vector2.Lerp(from, to, p);
            pos.y -= Mathf.Sin(p * Mathf.PI) * 18f;
            DrawVirus(new Rect(Mathf.Round(pos.x), Mathf.Round(pos.y), 26f, 24f), type, index, 1f, true);
            return;
        }
        float b = s - LeapTime;
        if (b < 0.25f) PixelGui.Fill(PixelGui.Inset(game, -2f), new Color(1f, 1f, 1f, 1f - b / 0.25f)); // 白い光
        float q = Mathf.Clamp01(b / 0.3f);
        Vector2 back = Vector2.Lerp(to, to + (from - to).normalized * 60f * power, 1f - (1f - q) * (1f - q));
        if (q < 0.5f) DrawVirus(new Rect(Mathf.Round(back.x), Mathf.Round(back.y), 26f, 24f), type, index, 1f - q * 2f, true);
        Shards(new Vector2(back.x + 13f, back.y + 12f), b, 6, 50f * power);
    }

    // マゼンタのかけら（GameArt.Shards があれば）
    static void Shards(Vector2 c, float since, int count, float speed)
    {
        if (since < 0f || since >= 0.35f) return;
        float a = 1f - since / 0.35f;
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Shards : null;
        for (int k = 0; k < count; k++)
        {
            float ang = k * Mathf.PI * 2f / count + 0.4f;
            Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed * since;
            if (frames != null && frames.Length > 0)
            {
                Sprite f = frames[k % frames.Length];
                Rect fr = f.textureRect;
                PixelGui.DrawSprite(new Rect(Mathf.Round(p.x - fr.width * 0.5f), Mathf.Round(p.y - fr.height * 0.5f), fr.width, fr.height), f, Magenta(a));
            }
            else PixelGui.Fill(new Rect(Mathf.Round(p.x) - 1f, Mathf.Round(p.y) - 1f, 3f, 3f), Magenta(a));
        }
    }

    /// <summary>輪が通って消える物：白と青のかけら 6個（0.25 秒）</summary>
    internal static void Shatter(Vector2 c, float since)
    {
        if (since < 0f || since >= 0.25f) return;
        float a = 1f - since / 0.25f;
        for (int k = 0; k < 6; k++)
        {
            float ang = k * Mathf.PI / 3f + 0.2f;
            Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (12f + since * 120f);
            PixelGui.Fill(new Rect(Mathf.Round(p.x) - 1f, Mathf.Round(p.y) - 1f, 3f, 3f), k % 2 == 0 ? new Color(1f, 1f, 1f, a) : Of(MacDesktopGui.Blue, a));
        }
    }

    /// <summary>輪が通って消えるウイルス：白い火花とマゼンタのかけら 4個（0.3 秒）</summary>
    internal static void Pop(Vector2 c, float since)
    {
        if (since < 0f || since >= 0.3f) return;
        Appear(new Rect(c.x - 13f, c.y - 12f, 26f, 24f), since);
        Shards(c, since, 4, 60f);
    }

    // 逃げ場の青い輪（角の丸い枠。はじいた瞬間は白、0.3 秒で青に戻る。ゆっくり明滅）
    static void GuardRing(Rect game, float sinceHit)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Now * Mathf.PI * 2f);
        Color c = Color.Lerp(Color.white, MacDesktopGui.Blue, Mathf.Clamp01(sinceHit / 0.3f));
        c.a = sinceHit < 0.3f ? 1f : 0.6f + 0.4f * wave;
        RoundBorder(PixelGui.Inset(game, -5f), c, 2f);
    }

    static void RoundBorder(Rect r, Color c, float t)
    {
        PixelGui.Fill(new Rect(r.x + 3f, r.y, r.width - 6f, t), c);
        PixelGui.Fill(new Rect(r.x + 3f, r.yMax - t, r.width - 6f, t), c);
        PixelGui.Fill(new Rect(r.x, r.y + 3f, t, r.height - 6f), c);
        PixelGui.Fill(new Rect(r.xMax - t, r.y + 3f, t, r.height - 6f), c);
        PixelGui.Fill(new Rect(r.x + 1f, r.y + 1f, 2f, 2f), c);
        PixelGui.Fill(new Rect(r.xMax - 3f, r.y + 1f, 2f, 2f), c);
        PixelGui.Fill(new Rect(r.x + 1f, r.yMax - 3f, 2f, 2f), c);
        PixelGui.Fill(new Rect(r.xMax - 3f, r.yMax - 3f, 2f, 2f), c);
    }

    // ゲームのアイコンの合図：白い枠（1秒に 1.5 往復でこさが変わる）と、1.6 秒ごとの右上のきらめき
    static void Halo(Rect r)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Now * Mathf.PI * 2f * 1.5f);
        PixelGui.Border(PixelGui.Inset(r, -2f), new Color(1f, 1f, 1f, 0.55f + 0.45f * wave), 1f);
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        float ph = Now % 1.6f;
        if (ph >= 0.4f) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(ph / 0.4f * frames.Length))];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.xMax - fr.width * 0.5f), Mathf.Round(r.y - fr.height * 0.5f), fr.width, fr.height), f);
    }

    // 青い札「Virus Desktop で駆除」（Dock の名前の札と同じ形。下に小さい三角）
    static void Tag(Rect game, Rect dock, float t, float wt)
    {
        var r = new Rect(Mathf.Round(game.center.x - 64f), dock.y - 26f, 128f, 18f);
        r.y += Mathf.Round(4f * (1f - Ramp(t, TagAt, TagAt + 0.15f)));
        if (wt >= 0f && Mathf.FloorToInt(Now / 1.2f) % 2 == 1) r.y -= 1f;
        MacDesktopGui.Rounded(r, MacDesktopGui.Blue, 3);
        PixelGui.Text(new Rect(r.x, r.y - 1f, r.width, r.height), "Virus Desktop で駆除", 12, Color.white, TextAnchor.MiddleCenter);
        float cx = Mathf.Round(game.center.x);
        PixelGui.Fill(new Rect(cx - 2f, r.yMax, 5f, 1f), MacDesktopGui.Blue);
        PixelGui.Fill(new Rect(cx - 1f, r.yMax + 1f, 3f, 1f), MacDesktopGui.Blue);
        PixelGui.Fill(new Rect(cx, r.yMax + 2f, 1f, 1f), MacDesktopGui.Blue);
    }

    /// <summary>青い輪（48個の 3×3 の四角。青と白を交互。内がわ 6 ドットにもう1本うすく）</summary>
    internal static void Ring(Vector2 c, float radius, float alpha)
    {
        if (radius <= 0f) return;
        for (int k = 0; k < 48; k++)
        {
            float ang = k * Mathf.PI * 2f / 48f;
            var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 p = c + d * radius;
            PixelGui.Fill(new Rect(Mathf.Round(p.x) - 1f, Mathf.Round(p.y) - 1f, 3f, 3f), k % 2 == 0 ? Of(MacDesktopGui.Blue, alpha) : new Color(1f, 1f, 1f, alpha));
            if (radius > 8f)
            {
                Vector2 q = c + d * (radius - 6f);
                PixelGui.Fill(new Rect(Mathf.Round(q.x) - 1f, Mathf.Round(q.y) - 1f, 3f, 3f), Of(MacDesktopGui.Blue, alpha * 0.5f));
            }
        }
    }

    /// <summary>右上の通知（フォルダの左）。slide＝0 で出きった、1 で右へ隠れた。friendly＝青い台（Virus Desktop）、でなければうす紫の台にウイルス</summary>
    internal static void Notice(float W, float slide, string title, string body, bool friendly)
    {
        var r = new Rect(Mathf.Round(W - 326f + slide * 340f), 32f, 228f, 44f); // 設計 2-11 は 188 幅。一言が切れたので 228 に（右のはしはフォルダの左のまま）
        MacDesktopGui.Rounded(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.18f), 6);
        MacDesktopGui.Rounded(r, MacDesktopGui.BorderColor, 6);
        MacDesktopGui.Rounded(PixelGui.Inset(r, 1f), MacDesktopGui.Paper, 5);
        var icon = new Rect(r.x + 9f, r.y + 10f, 24f, 24f);
        MacDesktopGui.Rounded(icon, friendly ? MacDesktopGui.Blue : PixelGui.Lilac, 4);
        if (friendly)
        {
            Sprite[] z = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
            if (z != null && z.Length > 0) PixelGui.DrawSprite(new Rect(icon.x + 4f, icon.y + 4f, 16f, 16f), z[0]);
        }
        else DrawVirus(new Rect(icon.x - 1f, icon.y, 26f, 24f), "Worm", 0, 1f, false);
        PixelGui.TextFit(new Rect(r.x + 40f, r.y + 5f, r.width - 46f, 16f), title, 12, MacDesktopGui.Ink);
        PixelGui.TextFit(new Rect(r.x + 40f, r.y + 22f, r.width - 46f, 16f), body, 12, MacDesktopGui.Muted);
    }

    /// <summary>はじける：白い光・マゼンタのかけら・きらめき（DesktopScreen.DrawRestore と同じ。0.7 秒で終わる）</summary>
    internal static void Burst(Rect r, float since)
    {
        if (since < 0f || since >= 0.7f) return;
        if (since < 0.25f) PixelGui.Fill(PixelGui.Inset(r, -2f), new Color(1f, 1f, 1f, 1f - since / 0.25f));
        float d = since * 70f, a = Mathf.Clamp01(1f - since / 0.6f);
        for (int k = 0; k < 8; k++)
        {
            float ang = k * Mathf.PI / 4f + 0.3f;
            var p = new Rect(Mathf.Round(r.center.x + Mathf.Cos(ang) * d) - 1f, Mathf.Round(r.center.y - Mathf.Abs(Mathf.Sin(ang)) * d * 0.9f) - 1f, 3f, 3f);
            PixelGui.Fill(p, Magenta(a));
        }
        Sparkle(r.center, since, 0.7f, 2f);
    }

    /// <summary>きらめき（GameArt.Sparkle）を c のまん中に、len 秒で1回</summary>
    internal static void Sparkle(Vector2 c, float since, float len, float scale)
    {
        if (since < 0f || since >= len) return;
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(since / len * frames.Length))];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(c.x - fr.width * scale * 0.5f), Mathf.Round(c.y - fr.height * scale * 0.5f), fr.width * scale, fr.height * scale), f);
    }

    static Sprite ZakoFrame(float offset)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.FloorToInt(Now * 6f + offset * frames.Length) % frames.Length];
    }
}
