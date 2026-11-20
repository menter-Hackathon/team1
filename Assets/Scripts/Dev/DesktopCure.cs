using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// クリア後、ウイルスがいなくなった演出（2026-11-20 まさぴ〜「クリア後ウイルスがなくなった演出も作ってほしい」。設計：docs/infection-panic-and-cure-design.md 5章）。
/// ラスボス（Terminal）を倒したあとの DesktopScreen（_target&lt;0）の絵を、この 6.1 秒に入れ替える（DesktopScreen が呼ぶ）。
///   のこり（紫のとばり・紫のフォルダ・紫の Finder と Terminal・はうウイルス・警告の窓 3枚・「感染 4」）
///   → 1.2 Terminal がはじける → 1.35〜2.45 ゲームのアイコンから青い輪が画面いっぱいに広がり、通った所から全部消える
///   → 2.5「感染 0」→ 2.6 ジングル・きらめき・ゲームのアイコンが2回跳ねる → 3.5 通知「このMacは安全です」→ 6.1 おわり。
/// 絵は起動の感染（MacDesktopInfection）と同じ部品・同じ位置。C（画面が出てからの秒）と番号だけで決める（物を覚えない）。
/// 消える時刻は、輪の半径 R＝Rmax×(1−(1−p)²) から1つの計算で出して、絵（Draw）と音（Tick）で同じ物を使う。
/// 紫・マゼンタは C=2.5 から先は出さない。赤は使わない（窓の左上の丸だけは Mac の部品のまま）。字はメニューバーの「感染 N」と通知だけ。
/// </summary>
public static class DesktopCure
{
    /// <summary>false＝今までどおり（DesktopScreen の 2.3 秒の絵）。切り戻し用</summary>
    public static bool Enabled = true;
    /// <summary>Terminal がはじける時刻（C）</summary>
    public const float BurstAt = 1.2f;
    /// <summary>DesktopScreen.RestoreAt（0.7）のあと見せる秒（合計 6.1）</summary>
    public const float Hold = 5.4f;

    // ───────── 時刻（C） ─────────
    const float WaveAt = 1.35f, WaveTime = 1.1f;              // 青い輪が広がる（始めが速い）
    const float FinderAt = 1.45f;                              // Finder が元の色に
    const float OneAt = 2.25f, ZeroAt = 2.5f;                  // 「感染 1」「感染 0」。0 から先は紫を出さない
    const float JingleAt = 2.6f, SecondSparkleAt = 3.6f, NoticeAt = 3.5f;
    const float FlashLen = 0.25f, FlashAlpha = 0.12f;          // 輪が出るときの白い光（設計は 0.30。Linear の色空間で強すぎたので下げた）
    const float GlowLen = 1.0f, GlowAlpha = 0.04f;             // ジングルのときのうすい白（壁紙が明るくなったように）。設計は 0.10。Linear の色空間だと灰色にかぶって見えたので下げた
    const float FolderTime = 0.3f, StripFade = 0.2f, PopTime = 0.3f, CloseTime = 0.15f;
    const float RiseLife = 1.2f, RiseSpeed = 18f;
    const float CrawlBase = 17f;                               // はうウイルスの最初の位置（番号ごとにばらける）
    const int RiseCount = 14, Strips = 16, MaxApps = 8, FragCount = 16;
    const float VeilAlpha = 0.30f;

    static readonly int[] Warns = { 0, 5, 9 };                 // 警告の窓（①⑥⑩の位置）
    static readonly string[] WallTypes = { "Botnet", "Miner", "Poly", "Phish", "Bomb", "Rootkit" };
    static readonly string[] WalkTypes = { "Worm", "Trojan" }; // Dock の上を歩く（0＝ゲームの左、1＝右）
    static readonly string[] SeatTypes = { "Macro", "Worm" };  // フォルダに座る（0＝書類、1＝ダウンロード。起動の感染と同じ）
    const string TerminalType = "Ransomware", FinderType = "Spyware";
    const string NoticeTitle = MacDesktopGui.AppName, NoticeBody = "ウイルス 0。このMacは安全です";
    static readonly string[] StatusText = { "感染 0", "感染 1", "感染 2", "感染 3", "感染 4" };
    static readonly Color TitleBar = new Color32(231, 234, 240, 255); // MacDesktopGui.Window のタイトルバーと同じ

    // ───────── 状態（音と、最初の Draw で作る Dock・位置だけ） ─────────
    static int _sounded, _pops, _closes;
    static List<MacDesktopGui.DockItem> _items; // Begin のあとの最初の Draw で1回作る
    static int _terminal = -1;                  // _items の中の Terminal（-1＝無い）
    static float _lw = -1f, _lh = -1f;          // 位置を出した画面の大きさ（-1＝まだ）
    static Rect _dock;
    static Vector2 _center;                     // 輪のまん中（ゲームのアイコンの元の位置のまん中）
    static float _rmax, _fragR;
    // 消える時刻（C）。輪が通る時刻を、効果が C=2.5 までに終わるように切る
    static readonly float[] _wallGone = new float[6], _walkGone = new float[2], _folderGone = new float[2], _warnGone = new float[3], _stripGone = new float[Strips];
    static readonly float[] _riseX = new float[RiseCount];
    static readonly float[] _popOrder = new float[10], _closeOrder = new float[3]; // 音の順（小さい順）
    static readonly System.Func<Vector2, bool> InsideRing = p => (p - _center).sqrMagnitude <= _fragR * _fragR;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        _sounded = 0; _pops = 0; _closes = 0; _items = null; _terminal = -1; _lw = -1f; _lh = -1f;
    }

    /// <summary>始める（DesktopScreen.Show）。音の旗を戻し、タイトルのデスクトップをきれいにする（Cured）</summary>
    public static void Begin()
    {
        ResetState();
        MacDesktopInfection.Cured = true;
    }

    // ───────── 音（Update から） ─────────

    /// <summary>時刻を過ぎた音を1回ずつ鳴らす。c＝画面が出てからの秒</summary>
    public static void Tick(float c)
    {
        if (!Enabled) return;
        Once(1, c >= BurstAt, "desktop_cure");
        Once(2, c >= WaveAt, "cure_wave");
        if (_lw > 0f) // 消える時刻は最初の Draw で決まる
        {
            while (_pops < _popOrder.Length && c >= _popOrder[_pops])
            {
                GameAudio.Event("cure_pop", Mathf.Min(1.8f, 1f + 0.1f * _pops)); // 1匹ごとに少しずつ高く
                _pops++;
            }
            while (_closes < _closeOrder.Length && c >= _closeOrder[_closes])
            {
                GameAudio.Event("tab_close");
                _closes++;
            }
        }
        Once(4, c >= ZeroAt, "cure_zero");
        if ((_sounded & 8) == 0 && c >= JingleAt)
        {
            _sounded |= 8;
            GameAudio.PlayJingle("jingle_cure", "");
        }
        Once(16, c >= NoticeAt, "toast");
    }

    static void Once(int bit, bool due, string key)
    {
        if (!due || (_sounded & bit) != 0) return;
        _sounded |= bit;
        GameAudio.Event(key);
    }

    // ───────── 絵（OnGUI から） ─────────

    /// <summary>のこり → 輪 → きれい → 通知 を描く。PixelGui.Begin 〜 End まで自分でやる。restoredStage＝いま倒したステージ（Terminal）</summary>
    public static void Draw(float c, RunState run, int restoredStage)
    {
        if (_items == null) BuildItems(run, restoredStage);
        PixelGui.Begin(false);
        try
        {
            float W = PixelGui.Width, H = PixelGui.Height;
            Layout(W, H);
            DrawBack(c, W, H);
            MacDesktopGui.Folders(W);
            MacDesktopGui.MenuBar(W, "Finder", StatusText[Infected(c)]);
            MacDesktopGui.Dock(W, H, _items, -1, Hop(c));
            DrawFront(c, W, H);
        }
        finally { PixelGui.End(); }
    }

    // Dock のアプリ：ボスのアプリは全部、ほかのアプリは合わせて 8つになるまで（元の順のまま）
    static void BuildItems(RunState run, int restored)
    {
        _items = new List<MacDesktopGui.DockItem>();
        _terminal = -1;
        if (run == null || run.Dock == null) return;
        int bosses = 0;
        foreach (DockApp a in run.Dock) if (a != null && a.Stage >= 0) bosses++;
        int others = Mathf.Max(0, MaxApps - bosses);
        foreach (DockApp a in run.Dock)
        {
            if (a == null) continue;
            if (a.Stage < 0)
            {
                if (others <= 0) continue;
                others--;
            }
            else if (a.Stage == restored) _terminal = _items.Count;
            _items.Add(new MacDesktopGui.DockItem(a.Name, a.Icon));
        }
    }

    // 位置と、物が消える時刻（画面の大きさが変わったときだけ出し直す）
    static void Layout(float W, float H)
    {
        if (W == _lw && H == _lh) return;
        _lw = W; _lh = H;
        _dock = MacDesktopInfection.DockRect(W, H, _items.Count);
        _center = MacDesktopInfection.IconRect(_dock, 1).center;
        _rmax = MacDesktopInfection.FarCorner(_center, W, H);
        float popLatest = ZeroAt - PopTime; // かけらが 2.5 までに消えるように

        for (int i = 0; i < _wallGone.Length; i++) _wallGone[i] = Mathf.Min(popLatest, PassAt(WallRect(i, WaveAt).center));
        for (int j = 0; j < _walkGone.Length; j++) _walkGone[j] = Mathf.Min(popLatest, PassAt(WalkRect(j, WaveAt, out _).center));
        for (int i = 0; i < _folderGone.Length; i++) _folderGone[i] = Mathf.Min(ZeroAt - FolderTime, PassAt(new Vector2(W - 47f, 56f + i * 64f)));
        for (int k = 0; k < _warnGone.Length; k++) _warnGone[k] = Mathf.Min(popLatest, PassAt(MacDesktopInfection.WarnRect(Warns[k], W, H).center));
        Rect f = MacDesktopInfection.Full(W, H);
        for (int s = 0; s < Strips; s++) _stripGone[s] = Mathf.Min(ZeroAt - StripFade, PassAt(new Vector2(Strip(f, s).center.x, f.center.y)));
        for (int k = 0; k < RiseCount; k++)
        {
            var rng = new System.Random(k * 7919 + 101);
            _riseX[k] = Mathf.Round(8f + (float)rng.NextDouble() * Mathf.Max(1f, W - 108f));
        }

        // 音の順：消えるウイルス 10匹（はう 6・歩く 2・フォルダ 2）と、閉じる窓 3枚
        int n = 0;
        foreach (float t in _wallGone) _popOrder[n++] = t;
        foreach (float t in _walkGone) _popOrder[n++] = t;
        foreach (float t in _folderGone) _popOrder[n++] = t;
        System.Array.Sort(_popOrder);
        System.Array.Copy(_warnGone, _closeOrder, _closeOrder.Length);
        System.Array.Sort(_closeOrder);
    }

    // 輪の半径（始めが速い）。C<1.35 は 0
    static float Radius(float c)
    {
        float p = Mathf.Clamp01((c - WaveAt) / WaveTime);
        return _rmax * (1f - (1f - p) * (1f - p));
    }

    // 輪が p を通る時刻（Radius の逆）
    static float PassAt(Vector2 p)
    {
        float x = Mathf.Clamp01(Vector2.Distance(p, _center) / Mathf.Max(1f, _rmax));
        return WaveAt + WaveTime * (1f - Mathf.Sqrt(1f - x));
    }

    // はうウイルス・歩くウイルスの時刻：輪が出たら止まる（消える時刻が1つに決まるように）
    static float CrawlTime(float c) => CrawlBase + Mathf.Min(c, WaveAt);

    // 壁紙をはうウイルス（ゆっくり：速さ半分）
    static Rect WallRect(int i, float c) => new Rect(MacDesktopInfection.WallPos(i, _lw, _lh, CrawlTime(c), 0.5f), new Vector2(26f, 24f));

    // Dock の上を歩くウイルス（ゆっくり）。ゲームのアイコンのまわりには入らない（起動の感染と同じ範囲）
    static Rect WalkRect(int j, float c, out bool facingLeft)
    {
        Rect game = MacDesktopInfection.IconRect(_dock, 1);
        float lo, hi;
        if (j == 0) { lo = _dock.x - 50f; hi = game.center.x - 96f; }
        else { lo = game.center.x + 70f; hi = _dock.xMax + 50f - 26f; }
        float range = Mathf.Max(1f, hi - lo);
        float phase = j * 41f + CrawlTime(c) * (20f + (j * 7) % 21) * 0.5f;
        facingLeft = Mathf.FloorToInt(phase / range) % 2 == 1;
        return new Rect(Mathf.Round(lo + Mathf.PingPong(phase, range)), _dock.y - 23f, 26f, 24f);
    }

    // とばりの帯 s（画面を横に 16本に分けた1本）
    static Rect Strip(Rect f, int s)
    {
        float x0 = Mathf.Round(f.x + f.width * s / Strips), x1 = Mathf.Round(f.x + f.width * (s + 1) / Strips);
        return new Rect(x0, f.y, x1 - x0, f.height);
    }

    // メニューバーの「感染 N」（時刻で決める）
    static int Infected(float c) => c < BurstAt ? 4 : c < FinderAt ? 3 : c < OneAt ? 2 : c < ZeroAt ? 1 : 0;

    // ゲームのアイコンの跳ね（MacDesktopGui.Dock の launchProgress。0→0.5 で1回、0→1 で2回）
    static float Hop(float c)
    {
        if (c >= WaveAt && c < WaveAt + 0.25f) return (c - WaveAt) / 0.25f * 0.5f; // 輪が出るとき、1回
        if (c >= JingleAt && c < JingleAt + 0.5f) return (c - JingleAt) / 0.5f;    // ジングルのとき、2回
        return -1f;
    }

    // 紫のとばり・壁紙をはうウイルス・かけら（フォルダ・Dock より先）
    static void DrawBack(float c, float W, float H)
    {
        if (c >= ZeroAt) return; // ここから先は紫を出さない

        // とばり：16本の帯。輪が帯のまん中を通ったら、0.2 秒で外がわへ引いて消える
        Rect f = MacDesktopInfection.Full(W, H);
        for (int s = 0; s < Strips; s++)
        {
            float since = c - _stripGone[s];
            if (since >= StripFade) continue;
            float k = Mathf.Clamp01(since / StripFade);
            Rect r = Strip(f, s);
            if (k > 0f)
            {
                float w = Mathf.Round(r.width * (1f - k));
                if (r.center.x >= _center.x) r.x = r.xMax - w;
                r.width = w;
            }
            VeilStrip(r, f, VeilAlpha * (1f - k));
        }

        // 壁紙をはう 6匹。輪が通ったら、白い火花とマゼンタのかけらで消える
        for (int i = 0; i < WallTypes.Length; i++)
        {
            float gone = _wallGone[i];
            if (c >= gone) { MacDesktopInfection.Pop(WallRect(i, gone).center, c - gone); continue; }
            MacDesktopInfection.DrawVirus(WallRect(i, c), WallTypes[i], i, 1f, MacDesktopInfection.WallFacing(i, W, CrawlTime(c) * 0.5f));
        }

        // かけら：輪の外にある物だけ
        if (c < WaveAt + WaveTime)
        {
            _fragR = Radius(c);
            MacDesktopInfection.Fragments(W, H, FragCount, 0.3f, 6f, c >= WaveAt ? InsideRing : null);
        }
    }

    // とばりの帯1本（MacDesktopInfection.Veil と同じ塗り方。ふちは帯の中だけ）
    static void VeilStrip(Rect r, Rect f, float a)
    {
        if (a <= 0.001f || r.width <= 0f) return;
        PixelGui.Fill(r, Of(PixelGui.Ink, a * 0.75f));
        PixelGui.Fill(r, Of(PixelGui.LabelPurple, a * 0.45f));
        Color edge = Of(PixelGui.Ink, a * 0.5f);
        PixelGui.Fill(new Rect(r.x, f.y, r.width, 24f), edge);
        PixelGui.Fill(new Rect(r.x, f.yMax - 32f, r.width, 32f), edge);
        FillWithin(new Rect(f.x, f.y + 24f, 32f, f.height - 56f), r, edge);
        FillWithin(new Rect(f.xMax - 32f, f.y + 24f, 32f, f.height - 56f), r, edge);
    }

    // band を r の左右の範囲で切って塗る
    static void FillWithin(Rect band, Rect r, Color color)
    {
        float x0 = Mathf.Max(band.x, r.x), x1 = Mathf.Min(band.xMax, r.xMax);
        if (x1 > x0) PixelGui.Fill(new Rect(x0, band.y, x1 - x0, band.height), color);
    }

    // フォルダと Dock の紫・歩くウイルス・警告の窓 → 輪 → きらめき → 通知 → 白い光（Dock のすぐ後）
    static void DrawFront(float c, float W, float H)
    {
        if (c < ZeroAt)
        {
            // フォルダ：輪が来たら、紫が上へ引いて下から青に戻る。座っていたウイルスは消える
            for (int i = 0; i < _folderGone.Length; i++)
            {
                float gone = _folderGone[i];
                MacDesktopInfection.Folder(i, W, 1f - Mathf.Clamp01((c - gone) / FolderTime));
                if (c < gone) MacDesktopInfection.SeatOnFolder(i, W, SeatTypes[i], 20 + i);
                else MacDesktopInfection.Pop(new Vector2(W - 47f, 43f + i * 64f - 4f), c - gone);
            }

            // Finder（1.45 で元の色に）と Terminal（1.2 で元の色に）
            Rect finder = MacDesktopInfection.Hovered(MacDesktopInfection.IconRect(_dock, 0));
            if (c < FinderAt)
            {
                MacDesktopInfection.InfectedFinder(finder);
                MacDesktopInfection.DrawVirus(OnIcon(finder), FinderType, 0, 1f, false);
            }
            else MacDesktopInfection.Burst(finder, c - FinderAt);
            if (_terminal >= 0)
            {
                int i = _terminal + 2;
                Rect r = MacDesktopInfection.Hovered(MacDesktopInfection.IconRect(_dock, i));
                if (c < BurstAt)
                {
                    MacDesktopInfection.InfectedApp(r, _items[_terminal].Icon, i);
                    MacDesktopInfection.DrawVirus(OnIcon(r), TerminalType, i, 1f, false);
                }
                else MacDesktopInfection.Burst(r, c - BurstAt);
            }

            // Dock の上を歩く 2匹
            for (int j = 0; j < WalkTypes.Length; j++)
            {
                float gone = _walkGone[j];
                if (c >= gone) { MacDesktopInfection.Pop(WalkRect(j, gone, out _).center, c - gone); continue; }
                Rect r = WalkRect(j, c, out bool left);
                MacDesktopInfection.DrawVirus(r, WalkTypes[j], 6 + j, 1f, left);
            }

            // 警告の窓 3枚（ときどきふるえる）。輪が通ったら、まん中へ縮んで閉じる。上に乗ったウイルスも消える
            for (int k = 0; k < Warns.Length; k++)
            {
                int i = Warns[k];
                Rect r = MacDesktopInfection.WarnRect(i, W, H);
                var rider = new Rect(r.xMax - 34f, r.y - 22f, 26f, 24f);
                float gone = _warnGone[k];
                if (c < gone)
                {
                    MacDesktopInfection.WarnWindow(r, MacDesktopInfection.WarnText[i % MacDesktopInfection.WarnText.Length], (c + k * 0.37f) % 1.1f, i, true);
                    MacDesktopInfection.DrawVirus(rider, MacDesktopInfection.AllTypes[i % MacDesktopInfection.AllTypes.Length], 30 + i, 1f, false);
                    continue;
                }
                float s = c - gone;
                if (s < CloseTime) Closing(r, 1f - s / CloseTime);
                MacDesktopInfection.Pop(rider.center, s);
            }
        }

        // 青い輪（終わりぎわにうすくなる）
        if (c >= WaveAt && c < WaveAt + WaveTime)
        {
            float p = (c - WaveAt) / WaveTime;
            MacDesktopInfection.Ring(_center, Radius(c), p < 0.8f ? 1f : 1f - (p - 0.8f) / 0.2f);
        }

        // きらめき：Dock のアイコンに左から順に（2回）と、壁紙をゆっくり上がる 14個
        if (c >= JingleAt)
        {
            int icons = _items.Count + 2;
            for (int i = 0; i < icons; i++)
            {
                Vector2 ic = MacDesktopInfection.Hovered(MacDesktopInfection.IconRect(_dock, i)).center;
                MacDesktopInfection.Sparkle(ic, c - (JingleAt + 0.1f * i), 0.4f, 2f);
                MacDesktopInfection.Sparkle(ic, c - (SecondSparkleAt + 0.1f * i), 0.4f, 2f);
            }
            for (int k = 0; k < RiseCount; k++)
            {
                float since = c - (JingleAt + 0.2f * k);
                if (since < 0f || since >= RiseLife) continue;
                MacDesktopInfection.Sparkle(new Vector2(_riseX[k], Mathf.Round(H - 80f - RiseSpeed * since)), since, RiseLife, 1f);
            }
        }

        // 通知「（ゲームの名前 AppName）／ウイルス 0。このMacは安全です」（右から入る）
        if (c >= NoticeAt) DrawNotice(W, 1f - Mathf.Clamp01((c - NoticeAt) / 0.2f));

        // 白い光（輪が出るとき 0.30 → 0、ジングルのとき 0.10 → 0）
        Rect full = MacDesktopInfection.Full(W, H);
        if (c >= WaveAt && c < WaveAt + FlashLen) PixelGui.Fill(full, new Color(1f, 1f, 1f, FlashAlpha * (1f - (c - WaveAt) / FlashLen)));
        if (c >= JingleAt && c < JingleAt + GlowLen) PixelGui.Fill(full, new Color(1f, 1f, 1f, GlowAlpha * (1f - (c - JingleAt) / GlowLen)));
    }

    // アイコンの上に乗るウイルスの位置
    static Rect OnIcon(Rect r) => new Rect(r.center.x - 13f, r.y - 16f, 26f, 24f);

    // 閉じていく窓（k＝大きさ 1→0）。小さいと Mac の窓の部品が入らないので、ふち・紙・タイトルバーだけで描く。中身は半分より小さくなったら描かない
    static void Closing(Rect r, float k)
    {
        float w = Mathf.Round(r.width * k), h = Mathf.Round(r.height * k);
        if (w < 4f || h < 4f) return;
        var q = new Rect(Mathf.Round(r.center.x - w * 0.5f), Mathf.Round(r.center.y - h * 0.5f), w, h);
        int rad = Mathf.Max(1, Mathf.RoundToInt(6f * k));
        MacDesktopGui.Rounded(new Rect(q.x + Mathf.Round(3f * k), q.y + Mathf.Round(5f * k), w, h), new Color(0.02f, 0.04f, 0.08f, 0.28f), rad);
        MacDesktopGui.Rounded(q, MacDesktopGui.BorderColor, rad);
        MacDesktopGui.Rounded(PixelGui.Inset(q, 1f), MacDesktopGui.Paper, Mathf.Max(1, rad - 1));
        float bar = Mathf.Max(2f, Mathf.Round(27f * k));
        MacDesktopGui.Rounded(new Rect(q.x + 1f, q.y + 1f, w - 2f, bar - 1f), TitleBar, Mathf.Max(1, rad - 1));
        PixelGui.Fill(new Rect(q.x + 1f, q.y + Mathf.Round(bar * 0.5f), w - 2f, bar - Mathf.Round(bar * 0.5f)), TitleBar);
        PixelGui.Fill(new Rect(q.x + 1f, q.y + bar, w - 2f, 1f), MacDesktopGui.BorderColor);
        if (k < 0.5f) return;
        var icon = new Rect(Mathf.Round(q.x + 12f * k), Mathf.Round(q.y + 36f * k), Mathf.Round(24f * k), Mathf.Round(24f * k));
        MacDesktopGui.Rounded(icon, PixelGui.Lilac, Mathf.Max(1, Mathf.RoundToInt(4f * k)));
        if (k >= 0.8f) MacDesktopGui.TrafficLights(q);
    }

    // 右上の通知（MacDesktopInfection.Notice と同じ形）。下の一言が入るように、幅を左へ広げる（右のはしは同じ W-98）
    static void DrawNotice(float W, float slide)
    {
        GUIStyle style = PixelGui.Style(false, 12);
        float need = Mathf.Max(PixelGui.Measure(NoticeTitle, style), PixelGui.Measure(NoticeBody, style));
        float w = Mathf.Min(W - 110f, Mathf.Max(188f, Mathf.Ceil(need) + 52f));
        var r = new Rect(Mathf.Round(W - 98f - w + slide * (w + 112f)), 32f, w, 44f);
        MacDesktopGui.Rounded(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.18f), 6);
        MacDesktopGui.Rounded(r, MacDesktopGui.BorderColor, 6);
        MacDesktopGui.Rounded(PixelGui.Inset(r, 1f), MacDesktopGui.Paper, 5);
        var icon = new Rect(r.x + 9f, r.y + 10f, 24f, 24f);
        MacDesktopGui.Rounded(icon, MacDesktopGui.Blue, 4);
        Sprite[] z = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        if (z != null && z.Length > 0) PixelGui.DrawSprite(new Rect(icon.x + 4f, icon.y + 4f, 16f, 16f), z[0]); // Dock のゲームのアイコンと同じ絵
        PixelGui.TextFit(new Rect(r.x + 40f, r.y + 5f, r.width - 46f, 16f), NoticeTitle, 12, MacDesktopGui.Ink);
        PixelGui.TextFit(new Rect(r.x + 40f, r.y + 22f, r.width - 46f, 16f), NoticeBody, 12, MacDesktopGui.Muted);
    }

    static Color Of(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
