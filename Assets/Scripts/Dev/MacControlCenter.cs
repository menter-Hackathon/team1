using UnityEngine;

/// <summary>
/// メニューバー右上のコントロールセンター（2026-11-20 まさぴ〜「デスクトップに残っている歯車は消して実際のマックと同じように右上の部分を押すと
/// コントロールパネルが出てきて。設定を変えれるようにして欲しい。そこもウイルスに最初は汚染されているような感じに」）。デザイン：Fable。
/// ・タイトルのデスクトップ（RunTitleMenu が自分に足す）。メニューバーのスイッチ2本（MacDesktopGui.ControlCenterIcon）を押すと開く・閉じる。
///   外を押す・Esc・B でも閉じる（外を押したクリックは後ろに通さない）。起動の演出の間・設定（PauseMenu）が開いている間は開かない
/// ・本物：サウンド（全体・BGM・効果音のスライダー。GameAudio の音量。10% きざみ）と、キー設定（PauseMenu のキー設定のページを開く）。
///   Wi-Fi・Bluetooth・AirDrop・集中モードは飾り
/// ・全クリアするまで（MacDesktopInfection.Cured が false の間）は感染した見た目：紫の地・脈打つマゼンタの縁・飾りのずれ・かけら・ウイルス3匹。
///   飾りやウイルスを押すと、ひるむだけ（消えない。倒すのはゲームの中で）。ずれは絵だけで、設定はいつでも変えられる。字で状況は言わない
/// ・マウスだけ（キーボード・コントローラーは、タイトルの窓の「設定」から同じ設定に届く）
/// </summary>
public sealed class MacControlCenter : MonoBehaviour
{
    const float PanelW = 188f, PanelH = 202f;
    const float PokeTime = 0.25f;

    static MacControlCenter _i;
    bool _open;
    int _openedFrame = -1, _closedFrame = -1;
    float _openedAt, _pokeAt = -10f;
    int _drag = -1; // ドラッグ中のスライダー（0 全体・1 BGM・2 効果音。無ければ -1）

    /// <summary>開いている間 true</summary>
    public static bool IsOpen => _i != null && _i._open;
    /// <summary>ほかの画面がキー入力を受け付けないとき true（開いている間と、閉じたのと同じフレーム）</summary>
    public static bool BlocksInput => _i != null && (_i._open || _i._closedFrame == Time.frameCount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _i = null;

    void OnEnable() => _i = this;
    void OnDisable()
    {
        if (_i == this) _i = null;
        MacDesktopGui.ControlCenterGlow = 0f;
    }

    static bool Infected => MacDesktopInfection.Enabled && !MacDesktopInfection.Cured;
    static bool CanUse => !RunTitleMenu.IsBootPlaying && !PauseMenu.IsOpen && (GameSession.Instance == null || !GameSession.Instance.IsTransitioning);
    static float Now => Time.unscaledTime;

    /// <summary>開く（確認用にも外から呼べる）</summary>
    public static void Open()
    {
        if (_i == null || _i._open || !CanUse) return;
        _i._open = true;
        _i._openedFrame = Time.frameCount;
        _i._openedAt = Now;
        _i._drag = -1;
        GameAudio.Event("ui_decide");
    }

    void EndDrag()
    {
        if (_drag >= 0 && GUIUtility.hotControl != 0) GUIUtility.hotControl = 0;
        _drag = -1;
    }

    void Close(bool sound)
    {
        if (!_open) return;
        _open = false;
        EndDrag();
        _closedFrame = Time.frameCount;
        if (sound) GameAudio.Event("ui_cancel");
    }

    void Update()
    {
        if (_drag >= 0 && (UnityEngine.InputSystem.Mouse.current == null || !UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)) EndDrag(); // 窓の外で離した
        if (!_open) return;
        if (!CanUse) { Close(false); return; }
        if (Time.frameCount == _openedFrame) return;
        if (MenuInput.Back) Close(true); // Esc・Backspace・B
    }

    // ───────── 描く・押す ─────────

    void OnGUI()
    {
        GUI.depth = -100; // タイトル・図鑑（-50）・実績（-60）より手前、設定（PauseMenu -200）より奥
        PixelGui.Begin(false);
        try
        {
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive); // スライダーをドラッグしている間、マウスをつかむ
            float W = PixelGui.Width;
            Rect icon = MacDesktopGui.ControlCenterIcon;
            bool usable = CanUse && icon.width > 0f;
            bool overIcon = usable && icon.Contains(e.mousePosition);
            if (e.type == EventType.Repaint) MacDesktopGui.ControlCenterGlow = _open && usable ? 0.22f : overIcon ? 0.12f : 0f;
            if (!usable) return; // 閉じるのは Update
            if (e.type == EventType.MouseDown && e.button == 0 && overIcon)
            {
                if (_open) Close(true); else Open();
                e.Use();
                return;
            }
            if (!_open) return;

            float right = Mathf.Min(W - 6f, Mathf.Round(icon.center.x + PanelW * 0.5f));
            float slide = Mathf.Round(6f * (1f - Mathf.Clamp01((Now - _openedAt) / 0.12f))); // 開くとき 0.12 秒で上から下がる
            Rect p = new Rect(Mathf.Max(6f, right - PanelW), Mathf.Round(icon.yMax + 3f) - slide, PanelW, PanelH);
            bool infected = Infected;
            float poke = Now - _pokeAt;
            float hop = infected && poke >= 0f && poke < PokeTime ? 3f : 0f;

            Rect wifi = new Rect(p.x + 6f, p.y + 6f, 86f, 62f);
            Rect focus = new Rect(p.x + 98f, p.y + 6f, 84f, 28f);
            Rect keys = new Rect(p.x + 98f, p.y + 40f, 84f, 28f);
            Rect sound = new Rect(p.x + 6f, p.y + 74f, 176f, 122f);
            Rect[] viruses = infected ? VirusRects(p, wifi, sound, hop) : null;

            // 押す（描く前に。マウスの出来事だけ）
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Vector2 m = e.mousePosition;
                int slider = SliderAt(sound, m);
                if (slider >= 0) { _drag = slider; GUIUtility.hotControl = control; SetVolume(slider, ValueAt(sound, slider, m.x)); }
                else if (keys.Contains(m)) OpenKeys();
                else if (infected && (HitAny(viruses, m) || wifi.Contains(m) || focus.Contains(m))) Poke();
                else if (!p.Contains(m)) Close(true); // 外を押したら閉じる（そのクリックは後ろに通さない）
                e.Use();
                if (!_open) return;
            }
            else if (e.type == EventType.MouseDrag && _drag >= 0) { SetVolume(_drag, ValueAt(sound, _drag, e.mousePosition.x)); e.Use(); }
            else if (e.type == EventType.MouseUp) { if (_drag >= 0 || p.Contains(e.mousePosition)) e.Use(); EndDrag(); }
            else if (e.type == EventType.ScrollWheel && p.Contains(e.mousePosition)) e.Use();

            if (e.type != EventType.Repaint) return;
            DrawPanel(p, infected);
            DrawConnectivity(wifi, infected, e.mousePosition);
            DrawFocus(focus, infected, e.mousePosition);
            DrawKeys(keys, infected, e.mousePosition);
            DrawSound(sound, infected);
            if (infected)
            {
                Fragments(new Rect(p.x + 6f, p.y + 6f, PanelW - 12f, 62f));
                DrawViruses(viruses);
                if (poke >= 0f && poke < PokeTime) MacDesktopGui.Rounded(p, MacDesktopInfection.Magenta(0.25f * (1f - poke / PokeTime)), 8);
            }
        }
        finally { PixelGui.End(); }
    }

    void OpenKeys()
    {
        Close(false);
        GameAudio.Event("ui_decide");
        PauseMenu.OpenSettings(true);
    }

    void Poke()
    {
        _pokeAt = Now;
        GameAudio.Event("ui_cancel");
    }

    static bool HitAny(Rect[] rects, Vector2 m)
    {
        if (rects == null) return false;
        foreach (Rect r in rects) if (r.Contains(m)) return true;
        return false;
    }

    // ───────── 音量 ─────────

    static readonly string[] SliderLabels = { "全体", "BGM", "効果音" };

    static Rect Track(Rect sound, int i) => new Rect(sound.x + 48f, sound.y + 27f + i * 32f, 92f, 12f);

    static int SliderAt(Rect sound, Vector2 m)
    {
        for (int i = 0; i < 3; i++)
        {
            Rect t = Track(sound, i);
            if (new Rect(t.x - 4f, t.y - 6f, t.width + 8f, t.height + 12f).Contains(m)) return i;
        }
        return -1;
    }

    static float ValueAt(Rect sound, int i, float x)
    {
        Rect t = Track(sound, i);
        return Mathf.Clamp01((x - t.x) / t.width);
    }

    static float GetVolume(int i) => i == 0 ? GameAudio.MasterVolume : i == 1 ? GameAudio.BgmVolume : GameAudio.SeVolume;

    static void SetVolume(int i, float v)
    {
        v = Mathf.Clamp01(Mathf.Round(v * 10f) / 10f); // 10% きざみ（PauseMenu と同じ）
        if (Mathf.Approximately(v, GetVolume(i))) return;
        if (i == 0) GameAudio.MasterVolume = v;
        else if (i == 1) GameAudio.BgmVolume = v;
        else GameAudio.SeVolume = v;
        GameAudio.Event("ui_move"); // 変えた大きさで鳴る
    }

    // ───────── 絵 ─────────

    static readonly Color Bg = new Color32(38, 38, 42, 240);
    static readonly Color Tile = new Color32(62, 62, 68, 255);
    static readonly Color Label = new Color32(238, 238, 242, 255);
    static readonly Color Sub = new Color32(160, 160, 170, 255);
    static readonly Color Dark = new Color32(70, 70, 78, 255);
    static Color Of(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static void DrawPanel(Rect p, bool infected)
    {
        MacDesktopGui.Rounded(new Rect(p.x + 2f, p.y + 3f, p.width, p.height), new Color(0f, 0f, 0f, 0.3f), 9);
        Color edge = Of(Color.white, 0.16f);
        if (infected) edge = MacDesktopInfection.Magenta(Mathf.Lerp(0.35f, 0.7f, 0.5f + 0.5f * Mathf.Sin(Now * Mathf.PI * 2f / 0.9f))); // 0.9 秒で脈打つ
        MacDesktopGui.Rounded(p, edge, 8);
        MacDesktopGui.Rounded(PixelGui.Inset(p, 1f), Bg, 7);
        if (infected) MacDesktopGui.Rounded(PixelGui.Inset(p, 1f), Of(PixelGui.LabelPurple, 0.35f), 7);
    }

    // タイルの地（飾りは感染していると紫に沈む。キー設定とサウンドはうすく染めるだけ）
    static void TileBg(Rect r, bool infected, bool decor, Vector2 mouse)
    {
        MacDesktopGui.Rounded(r, Tile, 6);
        if (infected) MacDesktopGui.Rounded(r, Of(PixelGui.LabelPurple, decor ? 0.55f : 0.25f), 6);
        if (r.Contains(mouse)) MacDesktopGui.Rounded(r, Of(Color.white, 0.06f), 6);
    }

    // ずれ（InfectedApp と同じ式。そのコマだけ ±2 ドット横にずれて、逆がわにマゼンタの残像）
    static float Glitch(bool infected, int index)
    {
        if (!infected) return 0f;
        int step = Mathf.FloorToInt(Now * 10f) + index * 3;
        if (step % 7 != 0 && step % 11 != 0) return 0f;
        return step % 2 == 0 ? 2f : -2f;
    }

    // 丸いアイコン（14×14）と、その右の文字。index：ずれの番号
    static void Toggle(float x, float y, string[] glyph, string label, float labelWidth, bool infected, int index)
    {
        float dx = Glitch(infected, index);
        Color disc = infected ? PixelGui.Magenta : MacDesktopGui.Blue;
        if (dx != 0f)
        {
            Disc(new Vector2(x + 7f - dx, y + 7f), 7, MacDesktopInfection.Magenta(0.45f));
            PixelGui.TextFit(new Rect(x + 18f - dx, y - 2f, labelWidth, 18f), label, 12, MacDesktopInfection.Magenta(0.45f));
        }
        Disc(new Vector2(x + 7f + dx, y + 7f), 7, disc);
        Glyph(glyph, x + 7f + dx, y + 7f, Color.white);
        PixelGui.TextFit(new Rect(x + 18f + dx, y - 2f, labelWidth, 18f), label, 12, Label);
    }

    static void DrawConnectivity(Rect r, bool infected, Vector2 mouse)
    {
        TileBg(r, infected, true, mouse);
        Toggle(r.x + 5f, r.y + 5f, WifiGlyph, "Wi-Fi", r.width - 25f, infected, 0);
        Toggle(r.x + 5f, r.y + 24f, BluetoothGlyph, "Bluetooth", r.width - 25f, infected, 1);
        Toggle(r.x + 5f, r.y + 43f, AirDropGlyph, "AirDrop", r.width - 25f, infected, 2);
    }

    static void DrawFocus(Rect r, bool infected, Vector2 mouse)
    {
        TileBg(r, infected, true, mouse);
        Toggle(r.x + 5f, r.y + 7f, MoonGlyph, "集中モード", r.width - 24f, infected, 3);
    }

    static void DrawKeys(Rect r, bool infected, Vector2 mouse)
    {
        TileBg(r, infected, false, mouse);
        Disc(new Vector2(r.x + 12f, r.y + 14f), 7, Dark);
        Glyph(KeyboardGlyph, r.x + 12f, r.y + 14f, Color.white);
        PixelGui.TextFit(new Rect(r.x + 23f, r.y + 5f, r.width - 34f, 18f), "キー設定", 12, Label);
        Glyph(ChevronGlyph, r.xMax - 8f, r.y + 14f, Sub);
    }

    void DrawSound(Rect r, bool infected)
    {
        TileBg(r, infected, false, new Vector2(-1f, -1f)); // 大きいタイルは、乗っても明るくしない
        PixelGui.Text(new Rect(r.x + 8f, r.y + 3f, 100f, 18f), "サウンド", 12, Sub);
        for (int i = 0; i < 3; i++)
        {
            Rect t = Track(r, i);
            PixelGui.TextFit(new Rect(r.x + 8f, t.y - 3f, 38f, 18f), SliderLabels[i], 12, Label);
            float v = GetVolume(i);
            MacDesktopGui.Rounded(t, infected ? Of(PixelGui.LabelPurple, 0.75f) : Of(Color.white, 0.18f), 6);
            float fill = Mathf.Max(t.height, Mathf.Round(t.width * v));
            MacDesktopGui.Rounded(new Rect(t.x, t.y, fill, t.height), infected ? MacDesktopInfection.Magenta(0.9f) : Label, 6);
            Glyph(SpeakerGlyph, t.x + 6f, t.center.y, infected ? Color.white : Dark);
            float knob = _drag == i ? 7f : 6f;
            Vector2 c = new Vector2(t.x + fill - 6f, t.center.y);
            Disc(c + new Vector2(0f, 1f), Mathf.RoundToInt(knob), new Color(0f, 0f, 0f, 0.35f));
            Disc(c, Mathf.RoundToInt(knob), Color.white);
            PixelGui.TextEn(new Rect(t.xMax + 5f, t.y + 2f, 26f, 8f), $"{Mathf.RoundToInt(v * 100f)}%", 8, Label);
        }
    }

    // ウイルス：Spyware は Wi-Fi のタイルの上のふちに座る、Adware は Wi-Fi とサウンドのタイルのさかいめに座る、Worm はパネルの下のふちを往復する
    static Rect[] VirusRects(Rect p, Rect wifi, Rect sound, float hop)
    {
        float span = PanelW - 40f;
        float x = Mathf.PingPong(Now * span / 4f, span); // 4 秒で片道
        return new[]
        {
            new Rect(wifi.x + 4f, wifi.y - 16f - hop, 26f, 24f),
            new Rect(sound.x + 64f, sound.y - 16f - hop, 26f, 24f),
            new Rect(p.x + 14f + Mathf.Round(x), p.yMax - 16f - hop, 26f, 24f),
        };
    }

    static void DrawViruses(Rect[] rects)
    {
        bool left = Mathf.FloorToInt(Now / 4f) % 2 == 1; // もどるときは絵を反転（4 秒で片道）
        MacDesktopInfection.DrawVirus(rects[0], "Spyware", 21, 1f, false);
        MacDesktopInfection.DrawVirus(rects[1], "Adware", 22, 1f, true);
        MacDesktopInfection.DrawVirus(rects[2], "Worm", 23, 1f, left);
    }

    // かけら（飾りのタイルの帯だけ。2本、0.25 秒ごとに場所が変わる。3つに1つは黒）
    static void Fragments(Rect band)
    {
        var rng = new System.Random(Mathf.FloorToInt(Now * 4f) * 7919 + 17);
        for (int i = 0; i < 2; i++)
        {
            int w = rng.Next(6, 41), h = rng.Next(2, 5);
            float x = band.x + rng.Next(0, Mathf.Max(1, (int)band.width - w));
            float y = band.y + rng.Next(0, Mathf.Max(1, (int)band.height - h));
            PixelGui.Fill(new Rect(x, y, w, h), rng.Next(3) == 0 ? new Color(0f, 0f, 0f, 0.45f) : MacDesktopInfection.Magenta(0.2f));
        }
    }

    static void Disc(Vector2 c, int radius, Color color)
    {
        c = new Vector2(Mathf.Round(c.x), Mathf.Round(c.y));
        for (int y = -radius; y <= radius; y++)
        {
            int half = Mathf.FloorToInt(Mathf.Sqrt(radius * radius - y * y));
            PixelGui.Fill(new Rect(c.x - half, c.y + y, half * 2 + 1, 1), color);
        }
    }

    // ドット絵（# が点）を、中心 (cx, cy) に描く
    static void Glyph(string[] g, float cx, float cy, Color color)
    {
        float ox = Mathf.Round(cx - g[0].Length * 0.5f), oy = Mathf.Round(cy - g.Length * 0.5f);
        for (int y = 0; y < g.Length; y++)
            for (int x = 0; x < g[y].Length; x++)
                if (g[y][x] == '#') PixelGui.Fill(new Rect(ox + x, oy + y, 1f, 1f), color);
    }

    static readonly string[] WifiGlyph = { ".#####.", "#.....#", "..###..", ".#...#.", "...#...", "...#..." };
    static readonly string[] BluetoothGlyph = { "..#..", "..##.", "#.#.#", ".###.", "#.#.#", "..##.", "..#.." };
    static readonly string[] AirDropGlyph = { ".###.", "#...#", "#.#.#", "#...#", ".###." };
    static readonly string[] MoonGlyph = { "..##.", ".##..", "##...", "##...", ".##..", "..##." };
    static readonly string[] KeyboardGlyph = { "#######", "#.#.#.#", "#######", "#.###.#", "#######" };
    static readonly string[] ChevronGlyph = { "#..", ".#.", "..#", ".#.", "#.." };
    static readonly string[] SpeakerGlyph = { "..#..", ".##.#", "###..", ".##.#", "..#.." };
}
