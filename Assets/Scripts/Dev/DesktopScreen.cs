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
/// ・2026-11-19：Mac の部品に揃えた（docs/mac-look-design.md 2-4 の 39〜42）。矢印は黒に白いふち、開くウィンドウは Mac のウィンドウ、
///   Dock は角の丸い板と区切りとゴミ箱、メニューバーは Finder のメニューと日付。Mac の部品は担当1 の MacUi.Gui で描く
/// 文字の説明は出さない（使用時間などの数字も出さない）。自動で進み、Z・Enter・Space・クリックで早送りできる。
/// 時間はすべて unscaledTime（前のステージは止めてある）。GameSession が自分に付ける。
/// ・2026-11-20（docs/window-stage-and-pickup-design.md 2-4）：ManualClick（既定 true）のときは、次のアプリを自分でクリックして入る。
///   元に戻る所を見せた後、次のアプリが跳ねて待つ（マウスが乗ると大きくなる）。Z・Enter・Space・ゲームパッドの下のボタンなら、今までの矢印が動いてクリックする
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
    const float WaitBounceEvery = 1.6f; // 待ち：次のアプリが跳ねる間隔（2回ずつ）
    const float WaitBounceTime = 0.28f; // 待ち：1回の跳ねの時間
    const float WaitBounceHeight = 6f;  // 待ち：跳ねる高さ（ドット）
    const float HitPad = 6f;            // クリックできる範囲：元のアイコンの四角を広げる幅
    const float HoverIcon = 38f;        // マウスが乗ったときのアイコンの大きさ（Dock の拡大。元は 32）

    /// <summary>true＝次のアプリを自分でクリックして入る（2-4）。false＝今までどおり自動で進む</summary>
    public static bool ManualClick = true;

    static readonly Color FolderBody = new Color32(0x7c, 0xb7, 0xe8, 0xff);
    static readonly Color FolderTab = new Color32(0x5a, 0x95, 0xcc, 0xff);
    static Color FolderShine => MacUi.FolderShine;
    static readonly Color InfectTint = new Color(1f, 0.55f, 1f, 1f);
    static readonly string[] Folders = { "書類", "スクショ", "ダウンロード" };


    RunState _run;
    int _restored = -1; // 今回元に戻るステージ（-1＝なし）
    int _target = -1;   // 次に入るステージ（-1＝なし。最後のクリアの後）
    float _shownAt;
    float _openAt = -1f; // ウィンドウが開き始めた時刻（-1＝まだ）
    bool _visible;
    Sprite _targetBg;    // 次に入るアプリの画面の背景の絵（AppBackgrounds。無ければ null）。描くたびに探さないよう、出すときに1回だけ
    float _clickAt = -1f;  // クリックした時刻（T。-1＝まだ）
    float _autoFrom = -1f; // Z などで矢印が動き始めた時刻（T。-1＝矢印なし）
    Rect _targetHome;      // 次のアプリのアイコンの元の位置（GUI の座標。DrawDock が毎回入れる。幅 0＝まだ無い）
    bool _cursorShown;     // 待ちの始めに本物のマウスの矢印を出したか

    public bool IsVisible => _visible;
    /// <summary>次のアプリのクリックを待っている（ManualClick のとき。自動では進まない）</summary>
    public bool IsWaitingForClick => _visible && !IsDone && ManualClick && _target >= 0 && _openAt < 0f && _clickAt < 0f && T >= TargetStart;
    /// <summary>次のアプリをクリックしたことにする（確認用・ゲームパッド用の口）。待っていなければ何もしない</summary>
    public void ClickTarget() { if (IsWaitingForClick) _clickAt = T; }
    /// <summary>見せ終わった（GameSession が次のシーンを読み込む）</summary>
    public bool IsDone { get; private set; }

    /// <summary>出す。restored：いまクリアしたステージ（-1＝なし）。次に入るのは run.Current（無ければ最後）</summary>
    public void Show(RunState run, int restored)
    {
        _run = run;
        _restored = restored;
        _target = run != null && run.Current != null ? run.CurrentIndex : -1;
        _targetBg = _target >= 0 ? AppBackgrounds.Find(run.Current) : null;
        _shownAt = Time.unscaledTime;
        _openAt = -1f;
        _clickAt = -1f;
        _autoFrom = -1f;
        _targetHome = default;
        _cursorShown = false;
        IsDone = false;
        _visible = true;
        MacDesktopWallpaper.Ensure(this,()=>_visible&&_run!=null);
        _sounded = 0;
        GameAudio.PlayBgm("bgm_desktop");
    }

    int _sounded; // 鳴らした音（ビット）。早送りで時刻が飛んでも、1回ずつ鳴らす

    // 音（2026-11-19）：元に戻る・ラスボスが現れて乱れる・クリック・ウィンドウが開く の瞬間に1回ずつ
    void PlaySounds()
    {
        float t = T;
        if (_restored >= 0 && t >= RestoreAt) SoundOnce(1, "desktop_cure");
        if (TargetIsFinal && t >= FinalAppearAt) SoundOnce(2, "desktop_glitch");
        if (_target >= 0 && _openAt < 0f && t >= ClickAt) SoundOnce(4, "desktop_click");
        if (_openAt >= 0f) SoundOnce(8, "desktop_open");
    }

    void SoundOnce(int bit, string key)
    {
        if ((_sounded & bit) != 0) return;
        _sounded |= bit;
        GameAudio.Event(key);
    }

    public void Hide()
    {
        _visible = false;
        _run = null;
        GetComponent<MacDesktopWallpaper>()?.Refresh();
    }

    float T => Time.unscaledTime - _shownAt;
    bool IsFinalStage(int stage) => _run != null && stage >= 0 && stage < _run.Stages.Count && _run.Stages[stage].IsFinal;
    bool TargetIsFinal => IsFinalStage(_target);
    // ラスボスが Dock に現れる時刻
    float FinalAppearAt => (_restored >= 0 ? RestoreAt : 0f) + FinalDelay;
    // 次のアプリへ向かい始める時刻（元に戻る所を見せてから。ラスボスなら、現れてから）
    float TargetStart => TargetIsFinal ? FinalAppearAt + FinalGlitch + 0.4f : _restored >= 0 ? RestoreAt + 0.5f : 0.3f;
    // 次のアプリがクリックされる時刻（自動なら矢印が着いた時。自分でクリックするときは、クリックするまで来ない）
    float ClickAt => !ManualClick ? TargetStart + CursorTime : _clickAt >= 0f ? _clickAt : float.PositiveInfinity;

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
            if (_target >= 0 && T >= ClickAt + BounceTime) _openAt = Time.unscaledTime;
            else if (_target < 0 && T >= (_restored >= 0 ? RestoreAt : 0f) + FinalHold) IsDone = true;
        }
        else if (Time.unscaledTime - _openAt >= OpenTime)
        {
            IsDone = true;
        }

        if (IsWaitingForClick && !_cursorShown)
        {
            MousePointer.ShowCursor(); // 待ちの始めに、本物のマウスの矢印を出す（3-15）
            _cursorShown = true;
        }
        if (ManualClick)
        {
            if (T >= InputGuard) OnPress();
        }
        else if (T >= InputGuard && Pressed()) Skip();
        PlaySounds();
    }

    // 自分でクリックするとき（2-4）：元に戻る所までは早送りだけ。待ちに入ったら、次のアプリのクリック、または Z などで矢印を動かす
    void OnPress()
    {
        bool key = KeyPressed();
        var mouse = Mouse.current;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (!key && !click) return;
        if (_target < 0) { Skip(); return; } // 最後のクリアの後は今までどおり
        if (_openAt >= 0f || _clickAt >= 0f) return;
        if (T < TargetStart)
        {
            if (_restored >= 0 && T < RestoreAt) _shownAt = Time.unscaledTime - RestoreAt;
            return;
        }
        if (key)
        {
            _autoFrom = T;
            _clickAt = T + CursorTime;
        }
        else if (MouseOnTarget())
        {
            _clickAt = T;
            MousePointer.Consume();
        }
    }

    // キーボード（Z・Enter・Space）とゲームパッドの下のボタン
    static bool KeyPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        var pad = Gamepad.current;
        return pad != null && pad.buttonSouth.wasPressedThisFrame;
    }

    // マウスの位置（GUI の座標）。PixelGui.Begin は原点のまま Scale 倍するだけなので、画面のドットを Scale で割り、上下を返す
    static bool TryMouseGui(out Vector2 p)
    {
        p = default;
        var mouse = Mouse.current;
        if (mouse == null) return false;
        Vector2 s = mouse.position.ReadValue();
        p = new Vector2(s.x / PixelGui.Scale, (Screen.height - s.y) / PixelGui.Scale);
        return true;
    }

    // マウスが次のアプリのアイコン（元の位置の四角を HitPad 広げた範囲）の上にあるか
    bool MouseOnTarget() => _targetHome.width > 0f && TryMouseGui(out Vector2 p) && PixelGui.Inset(_targetHome, -HitPad).Contains(p);

    static bool Pressed()
    {
        if (MenuInput.Confirm()) return true; // キーボードとコントローラー（A／START）。2026-11-20
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
        if (_target >= 0 && _openAt < 0f && (!ManualClick || _autoFrom >= 0f)) DrawCursor(W, H, t, targetHome);
        if (_openAt >= 0f) DrawOpening(target, W, H);
        PixelGui.End();
    }

    // ───────── 壁紙・フォルダ・メニューバー ─────────

    static void DrawWallpaper(float W, float H, float infectedRatio, int infected)
    {
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

    // 右上のフォルダ（感染したアプリの数だけ、上から紫に染まって乱れる）
    static void DrawFolders(float W, int infected)
    {
        int step = Mathf.FloorToInt(Time.unscaledTime * 6f);
        for (int i = 0; i < Folders.Length; i++)
        {
            float cx = W - 44f, y = 43f + i * 58f;
            bool bad = i < infected;
            Color body = bad ? PixelGui.Lilac : FolderBody, tab = bad ? PixelGui.LabelPurple : FolderTab;
            float jx = bad && (step + i) % 5 == 0 ? 2f : 0f;
            var bodyRect = new Rect(cx - 16f + jx, y + 5f, 32f, 22f);
            PixelGui.Fill(new Rect(bodyRect.x, y, 13f, 6f), tab);
            PixelGui.Fill(bodyRect, tab);
            PixelGui.Fill(new Rect(bodyRect.x, bodyRect.y + 3f, bodyRect.width, bodyRect.height - 3f), body);
            if (!bad) PixelGui.Fill(new Rect(bodyRect.x, bodyRect.y + 3f, bodyRect.width, 1f), FolderShine);
            if (bad) PixelGui.Fill(new Rect(bodyRect.x + 4f, bodyRect.y + 8f + (step + i) % 3 * 3f, 20f, 2f), PixelGui.Magenta);
            PixelGui.ShadowText(new Rect(cx - 40f, y + 29f, 80f, 16f), Folders[i], PixelGui.Style(false, 12, TextAnchor.MiddleCenter), PixelGui.White, PixelGui.Ink, 1f);
        }
    }

    static void DrawMenuBar(float W,int infected)
        => MacDesktopGui.MenuBar(W,"Finder",infected>0?$"感染 {infected}":null);

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
        // Mac の Dock（1-5）：角を 4ドット欠いた板。右の端に、縦の区切りの線とゴミ箱
        float appsW = n * icon + (n - 1) * gap;
        float dockW = pad + appsW + gap + 1f + gap + icon + pad, dockH = icon + pad * 2f;
        var dock = new Rect(Mathf.Round((W - dockW) * 0.5f), Mathf.Round(H - dockH - 6f), dockW, dockH);
        MacDesktopGui.DockPlate(dock);

        Rect target = new Rect(W * 0.5f - 16f, H * 0.5f - 16f, 32f, 32f);
        targetHome = target;
        float clickAt = ClickAt;
        _targetHome = default;
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
            if (isTarget) _targetHome = home;
            if (isTarget && IsWaitingForClick)
            {
                // 待ち（2-4）：マウスが乗ると大きくなり（Dock の拡大。下をそろえる）、1.6秒ごとに 2回 小さく跳ねる（「見てほしい」の跳ね方）
                if (MouseOnTarget()) r = new Rect(home.center.x - HoverIcon * 0.5f, home.yMax - HoverIcon, HoverIcon, HoverIcon);
                float ph = (t - TargetStart) % WaitBounceEvery;
                if (ph < WaitBounceTime * 2f) r.y -= Mathf.Round(Mathf.Abs(Mathf.Sin(ph / WaitBounceTime * Mathf.PI)) * WaitBounceHeight);
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
            // 次に入るアプリの下に、起動中の点（1-5：2ドット、#1C1C1E）
            if (isTarget && t >= clickAt) MacUi.Gui.DockDot(home.center.x, dock.yMax - 4f);
        }
        return target;
    }

    // Dock の右の端のゴミ箱（Finder のスキンのゴミ箱の絵を、アイコンと同じ大きさに。ふたは体の上）
    static void DrawTrash(Rect r)
    {
        float s = r.width / 16f;
        PixelGui.DrawSprite(new Rect(r.x, r.y + 2f * s, 16f * s, 14f * s), FinderSkinArt.TrashBody);
        PixelGui.DrawSprite(new Rect(r.x, r.y + 1f * s, 16f * s, 3f * s), FinderSkinArt.TrashLid);
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
        float p = Mathf.Clamp01((t - (ManualClick ? _autoFrom : TargetStart)) / CursorTime);
        p = p * p * (3f - 2f * p);
        var from = new Vector2(W * 0.5f, H * 0.42f);
        var to = new Vector2(targetHome.center.x + 4f, targetHome.center.y + 2f);
        Vector2 pos = Vector2.Lerp(from, to, p);
        float sinceClick = t - ClickAt;
        if (sinceClick >= 0f && sinceClick < 0.12f) pos.y += 1f;
        // Mac の矢印（黒い矢印に白いふち。1-5）
        MacUi.Gui.Arrow(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)));
    }

    // 次のアプリのウィンドウが、アイコンから画面いっぱいへ広がる（Mac のウィンドウ。1-2・2-4 の 40。MacUi.Gui.Window）。
    // 明るいタイトルバー（7）・左上に丸い信号機・まん中にアプリ名・角 4。タイトルバーの下にアプリの色の帯（ツールバー）、中身はステージの空の色
    void DrawOpening(Rect from, float W, float H)
    {
        StagePlan stage = _target >= 0 && _target < _run.Stages.Count ? _run.Stages[_target] : null;
        float p = Mathf.Clamp01((Time.unscaledTime - _openAt) / OpenTime);
        p = 1f - (1f - p) * (1f - p);
        var to = new Rect(12f, 22f, W - 24f, H - 34f);
        var r = new Rect(
            Mathf.Round(Mathf.Lerp(from.x, to.x, p)), Mathf.Round(Mathf.Lerp(from.y, to.y, p)),
            Mathf.Round(Mathf.Lerp(from.width, to.width, p)), Mathf.Round(Mathf.Lerp(from.height, to.height, p)));
        AppTheme theme = stage != null ? stage.App.Theme : null;
        Color bar = new Color32(231,234,240,255);
        Color body = theme != null ? theme.Sky : PixelGui.Panel;
        PixelGui.Fill(r, MacDesktopGui.BorderColor);
        Rect inner = PixelGui.Inset(r, 1f);
        float barH = Mathf.Round(Mathf.Lerp(3f, 24f, p));
        PixelGui.Fill(new Rect(inner.x, inner.y, inner.width, barH), bar);
        PixelGui.Fill(new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH), body);
        if (barH >= 20f && inner.width>=80f)
        {
            MacDesktopGui.TrafficLights(inner);
            if(inner.width>230f&&stage!=null)
                PixelGui.TextFit(new Rect(inner.x+65,inner.y+3,inner.width-130,18),stage.App.DisplayName,12,MacDesktopGui.Ink,TextAnchor.MiddleCenter);
        }
    }

    // 背景の絵を、縦横の比を保って rect いっぱいに描く（はみ出した所は切る。上のバーが見えるように、上をそろえる）
    static void DrawAppBackground(Rect rect, Sprite sprite)
    {
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        if (tex == null || tr.width <= 0f || tr.height <= 0f) return;
        float s = Mathf.Max(rect.width / tr.width, rect.height / tr.height);
        float uw = rect.width / s, uh = rect.height / s; // 見せる所（絵のドット）
        var uv = new Rect((tr.x + (tr.width - uw) * 0.5f) / tex.width, (tr.y + tr.height - uh) / tex.height, uw / tex.width, uh / tex.height);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);
    }

    // 題名に出すアプリ名（英数字だけの名前で 20字まで。日本語などの名前は出さない。
    // 2026-11-19：Google Chrome・Visual Studio Code・Microsoft Excel も出るように、12字から広げた）
    static string TitleOf(StagePlan stage)
    {
        string n = stage != null ? stage.DisplayName : null;
        if (string.IsNullOrEmpty(n) || n.Length > 20) return null;
        foreach (char c in n)
        {
            if (c >= 128 || !(char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '-' || c == '+')) return null;
        }
        return n;
    }
}
