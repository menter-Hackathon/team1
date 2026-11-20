using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 起動の演出の「感染」（2026-11-20 まさぴ〜「最初にウイルスが入ってやばい倒さなきゃという動機づけの流れがほしい」。設計：docs/infection-intro-design.md）。
/// ロック解除のあとの 6 秒（MacDesktopBoot の Infecting）：ダウンロードのフォルダから雑魚が跳び出し、Dock のアプリを右から1つずつ紫に染め、警告の窓が出る。
/// 最後の1匹はゲームのアイコンにはじかれて消え、ゲームのアイコンだけ白い枠で光って待つ。起動のあとのデスクトップにも、感染の「のこり」を出す。
/// RunTitleMenu の DrawBoot（起動の演出の中）と OnGUI（起動のあと）から呼ぶ。字は警告の窓の一言とメニューバーの「感染 N」だけ。
/// 絵は DesktopScreen（ステージの間の感染したデスクトップ）と同じ描き方を写して持つ（DesktopScreen は変えない）。
/// t（T）＝感染のステップが始まってからの秒。待ちからあとは Duration で止まるので、1回だけの絵は全部 Duration より前に終える。
/// </summary>
public static class MacDesktopInfection
{
    /// <summary>false＝今までの流れ（解除 → すぐデスクトップで待つ）。切り戻し用</summary>
    public static bool Enabled = true;
    /// <summary>感染のステップの長さ（秒）</summary>
    public const float Duration = 6f;
    /// <summary>これより前は、押しても飛ばさない（解除の押しっぱなし対策）</summary>
    public const float InputGuard = 0.4f;

    // 時刻（T）
    const float LineAt1 = 0.9f, LineAt2 = 3.7f, LineTime = 0.12f;          // 画面を横に走るマゼンタの線
    const float DyeTime = 0.4f, DownloadsDyeAt = 0.9f, DocumentsDyeAt = 2.6f; // フォルダが上から紫に染まる
    const float JumpTime = 0.6f, FirstJump = 1.3f, JumpEvery = 0.45f;       // 雑魚がフォルダから跳ぶ
    const float FinderJump = 3.1f, GameJump = 4.6f;                         // Finder へ・ゲームのアイコンへ（はじかれる）
    const float Warn1At = 4.0f, Warn2At = 4.3f, Warn3At = 5.4f;             // 警告の窓①②、Virus Desktop の窓③
    const float ArriveBurst = 0.15f;                                        // 乗った瞬間のかけら
    const float JumpArc = 36f;                                              // 跳ぶ高さ（ドット）
    const int MaxApps = 4;                                                  // 染める Dock のアプリの数（右から）

    static readonly Color InfectTint = new Color(1f, 0.55f, 1f, 1f); // DesktopScreen と同じ染め
    static Color Magenta(float a) => new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, a);

    static int _sounded; // 鳴らした SE（ビット）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { _sounded = 0; }

    static int Apps(int appCount) => Mathf.Clamp(appCount, 0, MaxApps);
    static float AppJump(int k) => FirstJump + JumpEvery * k; // k＝右から何番目のアプリか
    static float Land(float jump) => jump + JumpTime;

    // 乗った数（アプリ＋Finder）
    static int Infected(float t, int appCount)
    {
        if (t < 0f) return 0;
        int n = 0;
        for (int k = 0; k < Apps(appCount); k++)
            if (t >= Land(AppJump(k))) n++;
        if (t >= Land(FinderJump)) n++;
        return n;
    }

    // ───────── 音（Update から） ─────────

    /// <summary>時刻を過ぎた SE を1回ずつ鳴らす。t＝MacDesktopBoot.InfectionTime。appCount＝Dock のアプリの数</summary>
    public static void Tick(float t, int appCount)
    {
        if (!Enabled || t < 0f) return;
        Sound(0, t >= LineAt1, "desktop_glitch", 0.8f);
        for (int k = 0; k < Apps(appCount); k++)
            Sound(1 + k, t >= Land(AppJump(k)), "desktop_glitch", 1.5f + 0.1f * k); // 乗るたびに高く
        Sound(5, t >= Land(FinderJump), "desktop_glitch", 1f);
        Sound(6, t >= Warn1At, "popup_open", 0.9f);
        Sound(7, t >= Warn2At, "popup_open", 0.8f);
        Sound(8, t >= Land(GameJump), "desktop_cure", 1f); // ゲームのアイコンがウイルスをはじく
        Sound(9, t >= Warn3At, "toast", 1f);
    }

    /// <summary>飛ばしたとき：まだ鳴らしていない SE を鳴らしたことにする（まとめて鳴らない）</summary>
    public static void MuteRest() => _sounded = ~0;

    static void Sound(int bit, bool due, string key, float pitch)
    {
        int mask = 1 << bit;
        if (!due || (_sounded & mask) != 0) return;
        _sounded |= mask;
        GameAudio.Event(key, pitch);
    }

    // ───────── 起動の演出の中（DrawBoot） ─────────

    /// <summary>メニューバーに出す「感染 N」（0 なら null）</summary>
    public static string Status(float t, int appCount)
    {
        if (!Enabled) return null;
        int n = Infected(t, appCount);
        return n > 0 ? $"感染 {n}" : null;
    }

    /// <summary>壁紙のかけら・横の線（フォルダ・Dock より先に描く）。t&lt;0 なら何もしない</summary>
    public static void DrawBack(float t, float W, float H, int appCount)
    {
        if (!Enabled || t < 0f) return;
        if (t >= Duration) { Fragments(W, H, 20, 0.40f, 8f); return; }
        int n = Infected(t, appCount);
        if (n > 0) Fragments(W, H, 4 * n, 0.25f + 0.03f * n, 8f);
        Line(t, LineAt1, W, H * 0.35f);
        Line(t, LineAt2, W, H * 0.6f);
    }

    /// <summary>フォルダ・Dock の重ね絵・雑魚・警告の窓・はじく・白い枠（MacDesktopGui.Dock のすぐ後、ゲームの窓より先に描く）。
    /// apps＝Dock のアプリ（MacDesktopGui.Dock に渡した物）。hop＝ゲームのアイコンの跳ね（Dock に渡した物）。launched＝ゲームを押した後</summary>
    public static void DrawFront(float t, float W, float H, IReadOnlyList<MacDesktopGui.DockItem> apps, float hop, bool launched)
    {
        if (!Enabled || t < 0f) return;
        int count = apps != null ? apps.Count : 0;
        int n = Apps(count);
        Folder(1, W, Dye(t, DownloadsDyeAt));
        Folder(0, W, Dye(t, DocumentsDyeAt));

        Rect dock = DockRect(W, H, count);
        // 乗られたアプリ（右から）と Finder：紫に染まり、雑魚が乗る
        for (int k = 0; k < n; k++)
        {
            float land = Land(AppJump(k));
            if (t < land) continue;
            int i = count + 1 - k;
            Rect r = Hovered(IconRect(dock, i));
            InfectedApp(r, apps[i - 2].Icon, i);
            Virus(r, i);
            Arrive(r, t - land);
        }
        if (t >= Land(FinderJump))
        {
            Rect r = Hovered(IconRect(dock, 0));
            InfectedFinder(r);
            Virus(r, 0);
            Arrive(r, t - Land(FinderJump));
        }

        // 警告の窓（タイトルの窓が開くと、その下にかくれる）。
        // 縦に 62 ドットずつずらす：次の窓が前の窓の一言を隠さないように（設計 3-5 は 34 ドット。撮影で ① の字が隠れたので広げた）
        Warn(t, Warn1At, W, H, -160f, -104f, "警告", "ウイルスを検出しました", false, 0);
        Warn(t, Warn2At, W, H, -115f, -42f, "警告", "アプリが感染しています", false, 1);
        Warn(t, Warn3At, W, H, -70f, 20f, "Virus Desktop", "Virus Desktop で駆除", true, 2);

        // 跳んでいる雑魚（窓②の角の前を通るので、窓より後）
        for (int k = 0; k < n; k++) Jump(t, AppJump(k), IconRect(dock, count + 1 - k), W, k);
        Jump(t, FinderJump, IconRect(dock, 0), W, 4);
        Jump(t, GameJump, IconRect(dock, 1), W, 5);

        // ゲームのアイコン：はじく・白い枠ときらめき（押した後は消す）
        Rect game = Hovered(IconRect(dock, 1));
        if (hop >= 0f) game.y -= Mathf.Abs(Mathf.Sin(hop * Mathf.PI * 2f)) * 7f; // MacDesktopGui.Dock の跳ねと同じ
        float gameLand = Land(GameJump);
        if (t >= gameLand)
        {
            Burst(game, t - gameLand);
            if (!launched) Halo(game);
        }
    }

    // ───────── 起動のあと（ふつうの OnGUI）：のこり ─────────

    /// <summary>起動のあとのメニューバーの「感染 N」</summary>
    public static string LingerStatus(int appCount) => Enabled ? $"感染 {Apps(appCount) + 1}" : null;

    /// <summary>起動のあとの壁紙のかけら（うすく少なく。フォルダより先に描く）</summary>
    public static void DrawLingerBack(float W, float H)
    {
        if (!Enabled) return;
        Fragments(W, H, 6, 0.20f, 4f);
    }

    /// <summary>起動のあとのフォルダと Dock（MacDesktopGui.Dock のすぐ後に描く）。items＝Dock に渡した物。最小化した窓の項目には重ねない</summary>
    public static void DrawLingerFront(float W, float H, IReadOnlyList<MacDesktopGui.DockItem> items)
    {
        if (!Enabled) return;
        Folder(0, W, 1f);
        Folder(1, W, 1f);
        int count = items != null ? items.Count : 0;
        Rect dock = DockRect(W, H, count);
        Rect finder = Hovered(IconRect(dock, 0));
        InfectedFinder(finder);
        Virus(finder, 0);
        for (int j = 0; j < count && j < MaxApps; j++)
        {
            if (items[j].Window != null) continue;
            Rect r = Hovered(IconRect(dock, j + 2));
            InfectedApp(r, items[j].Icon, j + 2);
            Virus(r, j + 2);
        }
    }

    // ───────── 位置 ─────────

    // MacDesktopGui.Dock と同じ計算（Finder・ゲーム・アプリの順）
    static Rect DockRect(float W, float H, int appCount)
    {
        float w = (2 + appCount) * 42f + 12f;
        return new Rect(Mathf.Round((W - w) * 0.5f), H - 54, w, 48);
    }

    static Rect IconRect(Rect dock, int i) => new Rect(dock.x + 9 + i * 42, dock.y + 6, 30, 30);

    // マウスが乗ると 3 ドット上がる（MacDesktopGui.Dock と同じ条件）
    static Rect Hovered(Rect r)
    {
        if (GUI.enabled && r.Contains(Event.current.mousePosition)) r.y -= 3f;
        return r;
    }

    static float Dye(float t, float at) => t < at ? 0f : Mathf.Clamp01((t - at) / DyeTime);

    // ───────── 絵 ─────────

    // 壁紙のかけら（DesktopScreen.DrawWallpaper と同じ。3つに1つは黒）
    static void Fragments(float W, float H, int count, float alpha, float rate)
    {
        int step = Mathf.FloorToInt(Time.unscaledTime * rate);
        var rng = new System.Random(step * 7919);
        for (int i = 0; i < count; i++)
        {
            int w = rng.Next(6, 61), h = rng.Next(2, 7);
            float x = rng.Next(0, Mathf.Max(1, (int)(W - w)));
            float y = rng.Next(26, Mathf.Max(27, (int)(H - 8 - h)));
            PixelGui.Fill(new Rect(x, y, w, h), rng.Next(3) == 0 ? new Color(0f, 0f, 0f, 0.45f) : Magenta(alpha));
        }
    }

    // 画面を横に走るマゼンタの線（0.12 秒）
    static void Line(float t, float at, float W, float y)
    {
        if (t < at || t >= at + LineTime) return;
        PixelGui.Fill(new Rect(0f, Mathf.Round(y), W, 4f), Magenta(0.5f));
    }

    // フォルダ（0＝書類、1＝ダウンロード）を上から紫に染める。p＝染まった割合
    static void Folder(int i, float W, float p)
    {
        if (p <= 0f) return;
        float x = W - 63f, y = 43f + i * 64f;
        float bottom = y + Mathf.Round(26f * p);
        FillAbove(new Rect(x, y, 14f, 6f), PixelGui.LabelPurple, bottom);
        FillAbove(new Rect(x, y + 4f, 32f, 22f), PixelGui.LabelPurple, bottom);
        FillAbove(new Rect(x + 1f, y + 8f, 30f, 17f), PixelGui.Lilac, bottom);
        if (p < 1f) return;
        int step = Mathf.FloorToInt(Time.unscaledTime * 6f);
        PixelGui.Fill(new Rect(x + 5f, y + 11f + ((step + i) % 3) * 4f, 22f, 2f), PixelGui.Magenta);
    }

    // bottom より上の所だけ塗る
    static void FillAbove(Rect r, Color color, float bottom)
    {
        if (r.y >= bottom) return;
        r.height = Mathf.Min(r.height, bottom - r.y);
        PixelGui.Fill(r, color);
    }

    // 染まったアプリのアイコン：うす紫の台に紫の絵。ときどきずれて、マゼンタの残像（DesktopScreen.DrawAppIcon と同じ）
    static void InfectedApp(Rect r, Sprite icon, int index)
    {
        MacDesktopGui.Rounded(r, PixelGui.Lilac, 5);
        if (icon == null) return;
        int step = Mathf.FloorToInt(Time.unscaledTime * 10f) + index * 3;
        bool glitch = step % 7 == 0 || step % 11 == 0;
        float dx = glitch ? (step % 2 == 0 ? 2f : -2f) : 0f;
        Rect inner = PixelGui.Inset(r, 2f);
        PixelGui.DrawSprite(new Rect(inner.x + dx, inner.y, inner.width, inner.height), icon, InfectTint);
        if (glitch) PixelGui.DrawSprite(new Rect(inner.x - dx * 2f, inner.y, inner.width, inner.height), icon, Magenta(0.45f));
    }

    // 染まった Finder（絵は描き直せないので、紫を重ねてマゼンタの線を走らせる）
    static void InfectedFinder(Rect r)
    {
        Color purple = PixelGui.LabelPurple;
        MacDesktopGui.Rounded(r, new Color(purple.r, purple.g, purple.b, 0.55f), 5);
        int step = Mathf.FloorToInt(Time.unscaledTime * 10f);
        PixelGui.Fill(new Rect(r.x + 3f, r.y + 6f + (step % 3) * 8f, 24f, 2f), PixelGui.Magenta);
    }

    // アイコンに乗っている雑魚（DesktopScreen.DrawVirusOn と同じ位置）
    static void Virus(Rect r, int index)
    {
        Sprite f = ZakoFrame(index * 0.37f);
        if (f != null) PixelGui.DrawSprite(new Rect(r.xMax - 12f, r.y - 9f, 16f, 16f), f);
    }

    // 乗った瞬間：アイコンのまわりがマゼンタと濃い紫のかけらで乱れる（DesktopScreen.DrawArrival と同じ）
    static void Arrive(Rect r, float since)
    {
        if (since < 0f || since >= ArriveBurst) return;
        var rng = new System.Random(Mathf.FloorToInt(Time.unscaledTime * 20f));
        float a = 1f - since / ArriveBurst;
        for (int k = 0; k < 10; k++)
        {
            int w = rng.Next(4, 20), h = rng.Next(2, 6);
            var b = new Rect(r.x - 4f + rng.Next(0, Mathf.Max(1, (int)(r.width + 8f - w))), r.y - 4f + rng.Next(0, Mathf.Max(1, (int)(r.height + 8f - h))), w, h);
            Color c = rng.Next(2) == 0 ? PixelGui.Magenta : PixelGui.Ink;
            PixelGui.Fill(b, new Color(c.r, c.g, c.b, a));
        }
    }

    // ダウンロードのフォルダから、的のアイコンの右上へ山なりに跳ぶ雑魚（後ろにマゼンタの残像）
    static void Jump(float t, float jumpAt, Rect target, float W, int index)
    {
        float since = t - jumpAt;
        if (since < 0f || since >= JumpTime) return;
        Sprite f = ZakoFrame(index * 0.37f);
        if (f == null) return;
        var from = new Vector2(W - 47f - 8f, 120f - 8f); // ダウンロードのフォルダのまん中
        var to = new Vector2(target.xMax - 12f, target.y - 9f);
        if (since >= 0.06f) PixelGui.DrawSprite(new Rect(JumpPos(since - 0.06f, from, to), new Vector2(16f, 16f)), f, Magenta(0.45f));
        PixelGui.DrawSprite(new Rect(JumpPos(since, from, to), new Vector2(16f, 16f)), f);
    }

    static Vector2 JumpPos(float since, Vector2 from, Vector2 to)
    {
        float p = Mathf.Clamp01(since / JumpTime);
        Vector2 pos = Vector2.Lerp(from, to, p);
        pos.y -= Mathf.Sin(p * Mathf.PI) * JumpArc;
        return new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
    }

    // 警告の窓（Mac の窓。ボタンなし・押せない）。friendly＝Virus Desktop の窓③（青い台・ゆれない）
    static void Warn(float t, float at, float W, float H, float ox, float oy, string title, string text, bool friendly, int index)
    {
        if (t < at) return;
        Rect tw = MacDesktopGui.CenteredWindow(W, H, 420, 298); // タイトルの窓と同じ所
        var r = new Rect(Mathf.Round(tw.center.x + ox), Mathf.Round(tw.center.y + oy), 216f, 76f);
        float since = t - at;
        int glitchStep = -1;
        if (!friendly)
        {
            if (since < 0.15f) r.x += Mathf.FloorToInt(Time.unscaledTime * 30f) % 2 == 0 ? 2f : -2f; // 出た瞬間だけゆれる
            else
            {
                int step = Mathf.FloorToInt(Time.unscaledTime * 10f) + index * 4;
                if (step % 9 == 0) { r.x += 2f; glitchStep = step; }
            }
        }
        Rect inner = MacDesktopGui.Window(r, title, out _);
        var icon = new Rect(inner.x, inner.y + 2f, 24f, 24f);
        MacDesktopGui.Rounded(icon, friendly ? MacDesktopGui.Blue : PixelGui.Lilac, 4);
        Sprite zako = friendly ? FirstZakoFrame() : ZakoFrame(index * 0.37f);
        if (zako != null) PixelGui.DrawSprite(new Rect(icon.x + 4f, icon.y + 4f, 16f, 16f), zako);
        PixelGui.TextFit(new Rect(inner.x + 32f, inner.y + 4f, inner.width - 32f, 20f), text, 12, MacDesktopGui.Ink);
        if (glitchStep >= 0) PixelGui.Fill(new Rect(r.x + 1f, r.y + 30f + (glitchStep / 9 % 3) * 14f, r.width - 2f, 2f), PixelGui.Magenta);
    }

    // ゲームのアイコンが雑魚をはじく：白い光・マゼンタのかけら・きらめき（DesktopScreen.DrawRestore と同じ。0.7 秒で終わる）
    static void Burst(Rect r, float since)
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
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(since / 0.7f * frames.Length))];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.center.x - fr.width), Mathf.Round(r.center.y - fr.height), fr.width * 2f, fr.height * 2f), f);
    }

    // ゲームのアイコンの合図：白い枠（1秒に 1.5 往復でこさが変わる）と、1.6 秒ごとの右上のきらめき
    static void Halo(Rect r)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.5f);
        PixelGui.Border(PixelGui.Inset(r, -2f), new Color(1f, 1f, 1f, 0.55f + 0.45f * wave), 1f);
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        float ph = Time.unscaledTime % 1.6f;
        if (ph >= 0.4f) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(ph / 0.4f * frames.Length))];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.xMax - fr.width * 0.5f), Mathf.Round(r.y - fr.height * 0.5f), fr.width, fr.height), f);
    }

    static Sprite ZakoFrame(float offset)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.FloorToInt(Time.unscaledTime * 6f + offset * frames.Length) % frames.Length];
    }

    // Dock のゲームのアイコンと同じ1コマ目
    static Sprite FirstZakoFrame()
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }
}
