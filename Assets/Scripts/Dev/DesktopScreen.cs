using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】感染したデスクトップの画面（2026-11-18：メンターのFB「もっと Mac に入り込んだ敵を倒す雰囲気に」「敵を倒す動機は？」から。まさぴ〜了承）。
/// ステージに入る前の暗転の間に、GameSession.Transition が出す（シーンは増やさない。リトライでは出さない）。
/// ・Mac 風のデスクトップ：上のメニューバー（時計は本物の時刻）・右のフォルダ・下の Dock。Dock には、よく使うアプリ（使用時間の上位）を本物のアイコンで並べる
/// ・このランのボスのアプリは感染している：紫に染まってチラつき、ウイルス（雑魚）が乗っている。壁紙とフォルダも、感染したアプリの数だけ乱れる
/// ・ステージをクリアした後は、そのアプリのウイルスがはじけて元の色に戻り、壁紙の乱れも減る
/// ・次に入るアプリへマウスの矢印が動いてクリックし、アイコンが跳ね、そのアプリのウィンドウが開いて画面いっぱいに広がり、ステージへ
/// ・ラスボス（Terminal。2026-11-18）は、それまでのアプリを全部元に戻すまで Dock にいない。
///   最後のアプリが元に戻ってデスクトップがきれいになった直後に、乱れとともに Dock の端へ現れ、そのウィンドウが開いてラスボスのステージへ
/// ・最後のステージの後は、全部のアプリが元に戻ったデスクトップを見せてからリザルトへ
/// 文字の説明は出さない（使用時間などの数字も出さない）。自動で進み、Z・Enter・Space・クリックで早送りできる。
/// 時間はすべて unscaledTime（前のステージは止めてある）。GameSession が自分に付ける。
/// </summary>
public class DesktopScreen : MonoBehaviour
{
    const float RestoreAt = 0.7f;   // クリアしたアプリが元に戻る時刻
    const float CursorTime = 0.7f;  // マウスの矢印が次のアプリへ動く時間
    const float BounceTime = 0.6f;  // クリックしてからアイコンが跳ねている時間
    const float OpenTime = 0.45f;   // ウィンドウが開いて広がる時間
    const float FinalHold = 1.6f;   // 最後：全部戻ったデスクトップを見せる時間
    const float InputGuard = 0.4f;  // 出た瞬間の押しっぱなしで早送りしない
    const float FinalDelay = 0.45f; // 前のアプリが元に戻ってから、ラスボスが Dock に現れるまで
    const float FinalGlitch = 0.35f; // ラスボスが現れるときに乱れている時間

    static readonly Color WallClean = new Color32(0x4f, 0x86, 0xb8, 0xff);
    static readonly Color WallInfected = new Color32(0x2a, 0x0c, 0x3e, 0xff);
    static readonly Color MenuBar = new Color32(0xee, 0xee, 0xf2, 0xff);
    static readonly Color FolderBody = new Color32(0x7c, 0xb7, 0xe8, 0xff);
    static readonly Color FolderTab = new Color32(0x5a, 0x95, 0xcc, 0xff);
    static readonly Color InfectTint = new Color(1f, 0.55f, 1f, 1f);
    static readonly string[] Folders = { "書類", "スクショ", "ダウンロード" };

    // マウスの矢印（X＝黒い縁、W＝白）
    static readonly string[] Cursor =
    {
        "X.........",
        "XX........",
        "XWX.......",
        "XWWX......",
        "XWWWX.....",
        "XWWWWX....",
        "XWWWWWX...",
        "XWWWWWWX..",
        "XWWWWWWWX.",
        "XWWWWXXXXX",
        "XWWXWWX...",
        "XWX.XWWX..",
        "XX..XWWX..",
        ".....XWWX.",
        ".....XXX..",
    };

    RunState _run;
    int _restored = -1; // 今回元に戻るステージ（-1＝なし）
    int _target = -1;   // 次に入るステージ（-1＝なし。最後のクリアの後）
    float _shownAt;
    float _openAt = -1f; // ウィンドウが開き始めた時刻（-1＝まだ）
    bool _visible;

    public bool IsVisible => _visible;
    /// <summary>見せ終わった（GameSession が次のシーンを読み込む）</summary>
    public bool IsDone { get; private set; }

    /// <summary>出す。restored：いまクリアしたステージ（-1＝なし）。次に入るのは run.Current（無ければ最後）</summary>
    public void Show(RunState run, int restored)
    {
        _run = run;
        _restored = restored;
        _target = run != null && run.Current != null ? run.CurrentIndex : -1;
        _shownAt = Time.unscaledTime;
        _openAt = -1f;
        IsDone = false;
        _visible = true;
    }

    public void Hide()
    {
        _visible = false;
        _run = null;
    }

    float T => Time.unscaledTime - _shownAt;
    bool IsFinalStage(int stage) => _run != null && stage >= 0 && stage < _run.Stages.Count && _run.Stages[stage].IsFinal;
    bool TargetIsFinal => IsFinalStage(_target);
    // ラスボスが Dock に現れる時刻
    float FinalAppearAt => (_restored >= 0 ? RestoreAt : 0f) + FinalDelay;
    // 次のアプリへ向かい始める時刻（元に戻る所を見せてから。ラスボスなら、現れてから）
    float TargetStart => TargetIsFinal ? FinalAppearAt + FinalGlitch + 0.4f : _restored >= 0 ? RestoreAt + 0.5f : 0.3f;

    // Dock に出すか：ラスボスは、次がラスボスになって現れる時刻を過ぎてから（倒した後はずっと出す）
    bool IsShown(int stage, float t)
    {
        if (!IsFinalStage(stage)) return true;
        if (_run.CurrentIndex > stage) return true;
        return stage == _target && t >= FinalAppearAt;
    }

    void Update()
    {
        if (!_visible || IsDone) return;
        if (_openAt < 0f)
        {
            if (_target >= 0 && T >= TargetStart + CursorTime + BounceTime) _openAt = Time.unscaledTime;
            else if (_target < 0 && T >= (_restored >= 0 ? RestoreAt : 0f) + FinalHold) IsDone = true;
        }
        else if (Time.unscaledTime - _openAt >= OpenTime)
        {
            IsDone = true;
        }

        if (T >= InputGuard && Pressed()) Skip();
    }

    static bool Pressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    // 早送り：元に戻る所までは飛ばして見せ、そのあとはすぐウィンドウを開く
    void Skip()
    {
        if (_openAt >= 0f) return;
        if (_restored >= 0 && T < RestoreAt)
        {
            _shownAt = Time.unscaledTime - RestoreAt;
            return;
        }
        if (_target >= 0) _openAt = Time.unscaledTime;
        else IsDone = true;
    }

    bool IsInfected(int stage, float t) => stage >= 0 && (stage >= _run.CurrentIndex || (stage == _restored && t < RestoreAt));

    void OnGUI()
    {
        if (!_visible || _run == null) return;
        GUI.depth = -500; // ステージの表示・ゲームオーバーより手前、暗転（-1000）より奥
        PixelGui.Begin(false);
        float W = PixelGui.Width, H = PixelGui.Height, t = T;
        // 感染の数と割合は、Dock に出ているボスのアプリで数える（ラスボスが現れる前は、きれいになったデスクトップに見える）
        int infected = 0, shown = 0;
        for (int i = 0; i < _run.Stages.Count; i++)
        {
            if (!IsShown(i, t)) continue;
            shown++;
            if (IsInfected(i, t)) infected++;
        }
        float ratio = shown > 0 ? infected / (float)shown : 0f;

        DrawWallpaper(W, H, ratio, infected);
        DrawFolders(W, infected);
        DrawMenuBar(W, infected);
        Rect target = DrawDock(W, H, t, out Rect targetHome);
        if (_target >= 0 && _openAt < 0f) DrawCursor(W, H, t, targetHome);
        if (_openAt >= 0f) DrawOpening(target, W, H);
        PixelGui.End();
    }

    // ───────── 壁紙・フォルダ・メニューバー ─────────

    static void DrawWallpaper(float W, float H, float infectedRatio, int infected)
    {
        Color baseColor = Color.Lerp(WallClean, WallInfected, infectedRatio);
        const int bands = 12; // 上を明るく、下を暗く（ドット絵の階調）
        for (int i = 0; i < bands; i++)
        {
            float y0 = Mathf.Round(H * i / bands), y1 = Mathf.Round(H * (i + 1) / bands);
            PixelGui.Fill(new Rect(0f, y0, W, y1 - y0), Shade(baseColor, Mathf.Lerp(1.18f, 0.78f, i / (float)(bands - 1))));
        }
        if (infected <= 0) return;

        // 乱れ：感染したアプリの数だけ、マゼンタと黒のかけらがチラつく。ときどき横に裂ける
        int step = Mathf.FloorToInt(Time.unscaledTime * 8f);
        var rng = new System.Random(step * 7919);
        for (int i = 0; i < infected * 12; i++)
        {
            float w = rng.Next(6, 60), h = rng.Next(2, 7);
            var r = new Rect(rng.Next(0, Mathf.Max(1, (int)(W - w))), rng.Next(16, Mathf.Max(17, (int)(H - h))), w, h);
            PixelGui.Fill(r, rng.Next(3) == 0 ? new Color(0f, 0f, 0f, 0.45f) : new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, 0.25f + 0.15f * infected));
        }
        if (step % 10 == 0)
        {
            float ty = rng.Next(20, Mathf.Max(21, (int)H - 60));
            PixelGui.Fill(new Rect(0f, ty, W, rng.Next(3, 10)), new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, 0.5f));
        }
    }

    // 明るさだけ変える（不透明のまま。下の前のステージを透かさない）
    static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

    // 右上のフォルダ（感染したアプリの数だけ、上から紫に染まって乱れる）
    static void DrawFolders(float W, int infected)
    {
        int step = Mathf.FloorToInt(Time.unscaledTime * 6f);
        for (int i = 0; i < Folders.Length; i++)
        {
            float cx = W - 44f, y = 30f + i * 58f;
            bool bad = i < infected;
            Color body = bad ? PixelGui.Lilac : FolderBody, tab = bad ? PixelGui.LabelPurple : FolderTab;
            float jx = bad && (step + i) % 5 == 0 ? 2f : 0f;
            var bodyRect = new Rect(cx - 16f + jx, y + 5f, 32f, 22f);
            PixelGui.Fill(new Rect(bodyRect.x, y, 13f, 6f), tab);
            PixelGui.Fill(bodyRect, tab);
            PixelGui.Fill(new Rect(bodyRect.x, bodyRect.y + 3f, bodyRect.width, bodyRect.height - 3f), body);
            if (bad) PixelGui.Fill(new Rect(bodyRect.x + 4f, bodyRect.y + 8f + (step + i) % 3 * 3f, 20f, 2f), PixelGui.Magenta);
            PixelGui.ShadowText(new Rect(cx - 40f, y + 29f, 80f, 16f), Folders[i], PixelGui.Style(false, 12, TextAnchor.MiddleCenter), PixelGui.White, PixelGui.Ink, 1f);
        }
    }

    static void DrawMenuBar(float W, int infected)
    {
        PixelGui.Fill(new Rect(0f, 0f, W, 16f), MenuBar);
        PixelGui.Fill(new Rect(0f, 16f, W, 1f), PixelGui.Frame);
        PixelGui.TextEn(new Rect(10f, 4f, 80f, 8f), "Desktop", 8, PixelGui.Ink);
        PixelGui.Text(new Rect(80f, 0f, 260f, 16f), "ファイル　編集　表示　ウインドウ", 12, PixelGui.BodyText);
        string clock = System.DateTime.Now.ToString("H:mm");
        float cw = Mathf.Ceil(PixelGui.Measure(clock, PixelGui.Style(true, 8)));
        PixelGui.TextEn(new Rect(W - 10f - cw, 4f, cw + 2f, 8f), clock, 8, PixelGui.Ink);
        if (infected <= 0) return;
        // 感染の印：ウイルスの絵と数
        Sprite zako = ZakoFrame(0f);
        string count = $"x{infected}";
        float nw = Mathf.Ceil(PixelGui.Measure(count, PixelGui.Style(true, 8)));
        float x = W - 22f - cw - nw;
        PixelGui.TextEn(new Rect(x, 4f, nw + 2f, 8f), count, 8, PixelGui.Magenta);
        if (zako != null) PixelGui.DrawSprite(new Rect(x - 17f, 0f, 16f, 16f), zako);
    }

    // ───────── Dock ─────────

    // Dock を描き、次に入るアプリのアイコンの位置（跳ねている今の位置と、元の位置）を返す
    Rect DrawDock(float W, float H, float t, out Rect targetHome)
    {
        const float icon = 32f, gap = 10f, pad = 8f;
        var apps = new System.Collections.Generic.List<DockApp>();
        foreach (DockApp a in _run.Dock)
        {
            if (IsShown(a.Stage, t)) apps.Add(a);
        }
        int n = Mathf.Max(1, apps.Count);
        float dockW = n * icon + (n - 1) * gap + pad * 2f, dockH = icon + pad * 2f;
        var dock = new Rect(Mathf.Round((W - dockW) * 0.5f), Mathf.Round(H - dockH - 6f), dockW, dockH);
        PixelGui.Fill(dock, new Color(1f, 1f, 1f, 0.28f));
        PixelGui.Border(dock, new Color(1f, 1f, 1f, 0.6f), 1f);
        PixelGui.Fill(new Rect(dock.x + 1f, dock.y + 1f, dock.width - 2f, 1f), new Color(1f, 1f, 1f, 0.5f));

        Rect target = new Rect(W * 0.5f - 16f, H * 0.5f - 16f, 32f, 32f);
        targetHome = target;
        float clickAt = TargetStart + CursorTime;
        for (int i = 0; i < apps.Count; i++)
        {
            DockApp app = apps[i];
            var home = new Rect(dock.x + pad + i * (icon + gap), dock.y + pad, icon, icon);
            Rect r = home;
            bool isTarget = app.Stage >= 0 && app.Stage == _target;
            if (isTarget && _openAt < 0f && t >= clickAt)
            {
                // クリックされて跳ねる（Mac の Dock のように）
                float b = (t - clickAt) * 9f;
                r.y -= Mathf.Round(Mathf.Abs(Mathf.Sin(b)) * 10f);
            }
            if (isTarget)
            {
                target = r;
                targetHome = home;
            }
            bool infected = IsInfected(app.Stage, t);
            DrawAppIcon(r, app.Icon, infected, i);
            if (infected) DrawVirusOn(r, i);
            if (IsFinalStage(app.Stage) && app.Stage == _target) DrawArrival(r, t - FinalAppearAt);
            if (app.Stage >= 0 && app.Stage == _restored) DrawRestore(r, t - RestoreAt);
            // 次に入るアプリの下に、起動中の点
            if (isTarget && t >= clickAt) PixelGui.Fill(new Rect(home.center.x - 1f, dock.yMax - 4f, 2f, 2f), PixelGui.White);
        }
        return target;
    }

    static void DrawAppIcon(Rect r, Sprite icon, bool infected, int index)
    {
        if (icon == null)
        {
            PixelGui.Fill(r, PixelGui.Frame);
            return;
        }
        if (!infected)
        {
            PixelGui.DrawSprite(r, icon);
            return;
        }
        // 感染：紫に染まり、ときどきずれて、マゼンタの残像が出る
        int step = Mathf.FloorToInt(Time.unscaledTime * 10f) + index * 3;
        bool glitch = step % 7 == 0 || step % 11 == 0;
        float dx = glitch ? (step % 2 == 0 ? 2f : -2f) : 0f;
        PixelGui.DrawSprite(new Rect(r.x + dx, r.y, r.width, r.height), icon, InfectTint);
        if (glitch) PixelGui.DrawSprite(new Rect(r.x - dx * 2f, r.y, r.width, r.height), icon, new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, 0.45f));
    }

    // ラスボスが現れるとき：アイコンの上がマゼンタと黒のかけらで乱れる
    static void DrawArrival(Rect r, float since)
    {
        if (since < 0f || since > FinalGlitch) return;
        var rng = new System.Random(Mathf.FloorToInt(Time.unscaledTime * 20f));
        float a = 1f - since / FinalGlitch;
        for (int k = 0; k < 10; k++)
        {
            float w = rng.Next(4, 20), h = rng.Next(2, 6);
            var b = new Rect(r.x - 4f + rng.Next(0, (int)(r.width + 8f - w)), r.y - 4f + rng.Next(0, (int)(r.height + 8f - h)), w, h);
            Color c = rng.Next(2) == 0 ? PixelGui.Magenta : PixelGui.Ink;
            PixelGui.Fill(b, new Color(c.r, c.g, c.b, a));
        }
    }

    // アイコンに乗っているウイルス（雑魚の歩く絵）
    static void DrawVirusOn(Rect r, int index)
    {
        Sprite f = ZakoFrame(index * 0.37f);
        if (f == null) return;
        PixelGui.DrawSprite(new Rect(r.xMax - 12f, r.y - 9f, 16f, 16f), f);
    }

    // 元に戻る瞬間：白く光り、ウイルスのかけらが飛び散り、きらめく
    static void DrawRestore(Rect r, float since)
    {
        if (since < 0f || since > 0.9f) return;
        if (since < 0.25f) PixelGui.Fill(PixelGui.Inset(r, -2f), new Color(1f, 1f, 1f, 1f - since / 0.25f));
        float d = since * 70f, a = Mathf.Clamp01(1f - since / 0.6f);
        for (int k = 0; k < 8; k++)
        {
            float ang = k * Mathf.PI / 4f + 0.3f;
            var p = new Rect(Mathf.Round(r.center.x + Mathf.Cos(ang) * d) - 1f, Mathf.Round(r.center.y - Mathf.Abs(Mathf.Sin(ang)) * d * 0.9f) - 1f, 3f, 3f);
            PixelGui.Fill(p, new Color(PixelGui.Magenta.r, PixelGui.Magenta.g, PixelGui.Magenta.b, a));
        }
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(since * 12f) % frames.Length)];
        Rect fr = f.textureRect;
        PixelGui.DrawSprite(new Rect(Mathf.Round(r.center.x - fr.width), Mathf.Round(r.center.y - fr.height), fr.width * 2f, fr.height * 2f), f);
    }

    static Sprite ZakoFrame(float offset)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.FloorToInt(Time.unscaledTime * 6f + offset * frames.Length) % frames.Length];
    }

    // ───────── マウスの矢印・ウィンドウが開く ─────────

    // 画面の真ん中から、次に入るアプリへ矢印が動き、クリックする（押した瞬間は少し沈む）
    void DrawCursor(float W, float H, float t, Rect targetHome)
    {
        float p = Mathf.Clamp01((t - TargetStart) / CursorTime);
        p = p * p * (3f - 2f * p);
        var from = new Vector2(W * 0.5f, H * 0.42f);
        var to = new Vector2(targetHome.center.x + 4f, targetHome.center.y + 2f);
        Vector2 pos = Vector2.Lerp(from, to, p);
        float sinceClick = t - (TargetStart + CursorTime);
        if (sinceClick >= 0f && sinceClick < 0.12f) pos.y += 1f;
        float x0 = Mathf.Round(pos.x), y0 = Mathf.Round(pos.y);
        for (int row = 0; row < Cursor.Length; row++)
        {
            string line = Cursor[row];
            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                if (c == '.') continue;
                PixelGui.Fill(new Rect(x0 + col, y0 + row, 1f, 1f), c == 'X' ? PixelGui.Ink : PixelGui.White);
            }
        }
    }

    // 次のアプリのウィンドウが、アイコンから画面いっぱいへ広がる（タイトルバーはアプリの色、中身はステージの空の色）
    void DrawOpening(Rect from, float W, float H)
    {
        StagePlan stage = _target >= 0 && _target < _run.Stages.Count ? _run.Stages[_target] : null;
        float p = Mathf.Clamp01((Time.unscaledTime - _openAt) / OpenTime);
        p = 1f - (1f - p) * (1f - p);
        var to = new Rect(12f, 22f, W - 24f, H - 34f);
        var r = new Rect(
            Mathf.Round(Mathf.Lerp(from.x, to.x, p)), Mathf.Round(Mathf.Lerp(from.y, to.y, p)),
            Mathf.Round(Mathf.Lerp(from.width, to.width, p)), Mathf.Round(Mathf.Lerp(from.height, to.height, p)));
        AppTheme theme = stage?.Theme;
        Color bar = theme != null ? theme.Main : PixelGui.Magenta;
        Color body = theme != null ? theme.Sky : PixelGui.Panel;
        PixelGui.Fill(r, PixelGui.FrameDark);
        Rect inner = PixelGui.Inset(r, 1f);
        float barH = Mathf.Round(Mathf.Lerp(3f, 14f, p));
        PixelGui.Fill(new Rect(inner.x, inner.y, inner.width, barH), bar);
        PixelGui.Fill(new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH), body);
        if (barH >= 10f)
        {
            Color[] lights = { new Color32(0xff, 0x5f, 0x57, 0xff), new Color32(0xfe, 0xbc, 0x2e, 0xff), new Color32(0x28, 0xc8, 0x40, 0xff) };
            for (int i = 0; i < lights.Length; i++) PixelGui.Fill(new Rect(inner.x + 5f + i * 8f, inner.y + 4f, 5f, 5f), lights[i]);
        }
        if (stage != null && stage.Icon16 != null && inner.height > 60f)
        {
            float s = Mathf.Clamp(Mathf.Floor(inner.height / 64f), 1f, 3f) * 16f;
            PixelGui.DrawSprite(new Rect(Mathf.Round(inner.center.x - s * 0.5f), Mathf.Round(inner.center.y - s * 0.5f + barH * 0.5f), s, s), stage.Icon16);
        }
    }
}
