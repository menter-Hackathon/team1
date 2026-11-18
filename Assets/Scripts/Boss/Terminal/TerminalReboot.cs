using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ラスボス Terminal の sudo reboot（形態変化の演出）と、ゲージの棒の見せ方（2026-11-18。docs/app-boss-skills-design.md 4-4）。
/// TerminalRebootExtension がボスに足す。BossController の形態変化（ゲージ50%・State＝Transition・TransitionTime の間は無敵）は変えず、見せ方だけを足す。
///
/// 演出（3.5秒の表。演出の長さが違っても、同じ割合で進める）
/// ・0〜0.6秒：一瞬止まる（ヒットストップ）。ゲージの棒が満タンで白く光る。ボスのウィンドウが真っ黒になり、ノイズがはじける
/// ・0.6〜2.6秒：画面全体が暗くなる（黒を8割）。中央に「System Rebooting...」（点が1つずつ増える）と、白い進み具合の棒（横6マス）
/// ・2.6〜3.5秒：背景がカーネル・パニック（TerminalKernelPanic）に切り替わる。棒が右から左へくずれて空・赤になる。
///   ボスが赤いちらつきと一緒に立ち上がる。画面が強くゆれる
/// 演出の間はダメージなし：始まりに場に残っている物を全部消し、体当たり（と被弾範囲）を切る。演出が終わったら戻す
/// 第2形態：ウィンドウのふちとタイトルバーが赤、0.5秒に1回、横にずれる赤いちらつき
///
/// ゲージの棒（IBossGaugeDisplay）：第1形態は本当の50%で満タンに見せる。第2形態は本当の 50〜100% を、空から満タンまでで見せる（赤）
/// 正常化したら、背景を元に戻す（アプリが直ったので）。やり直し（ボスが第1形態に戻った）でも戻す
/// </summary>
[DisallowMultipleComponent]
public class TerminalReboot : MonoBehaviour, IBossGaugeDisplay
{
    // ───────── 時間【仮】（docs 4-4 の表） ─────────
    const float TableTime = 3.5f;
    const float BlackoutEnd = 0.6f;
    const float PanicAt = 2.6f;
    const float DimFadeIn = 0.12f;
    const float DimFadeOut = 0.25f;
    const float DimAlpha = 0.8f;
    const float BarUnits = 6f;
    /// <summary>【仮】ヒットストップ（実時間の秒）と、その間の時間の速さ</summary>
    const float HitStopRealTime = 0.12f;
    const float HitStopScale = 0.02f;
    /// <summary>【仮】第2形態のちらつき（0.5秒に1回、0.08秒）</summary>
    const float GlitchInterval = 0.5f;
    const float GlitchTime = 0.08f;
    const int NoiseCount = 14;

    static readonly Color FrameDark = RuntimeSprites.Hex("7A1712");
    static readonly Color FrameLight = RuntimeSprites.Hex("FF8A80");
    static readonly Color NoiseGray = RuntimeSprites.Hex("8E8E93");

    BossController _boss;
    TerminalKernelPanic _panic;
    bool _hitStop;
    float _hitStopEnd;
    bool _shookPanic;
    bool _contactOff;

    SpriteRenderer _window;
    float _nextFind;
    SpriteRenderer _black;
    SpriteRenderer _frame;
    SpriteRenderer _glitch;
    Transform _noiseRoot;
    readonly List<SpriteRenderer> _noise = new List<SpriteRenderer>();
    float _nextNoise;
    readonly Dictionary<Sprite, Sprite> _frameCache = new Dictionary<Sprite, Sprite>();

    void OnEnable()
    {
        if (_boss == null) _boss = GetComponent<BossController>();
        if (_boss == null) return;
        _boss.TransitionStarted += OnTransitionStarted;
        _boss.Normalized += OnNormalized;
    }

    void OnDisable()
    {
        if (_boss != null)
        {
            _boss.TransitionStarted -= OnTransitionStarted;
            _boss.Normalized -= OnNormalized;
        }
        EndHitStop();
        DestroyPanic();
        HideOverlays();
        _contactOff = false; // 出し直したときは、BossController の ResetBoss が体当たりを戻す
    }

    /// <summary>このボスの設定が Terminal（再起動の仕組みが入っている）か</summary>
    static bool IsTerminal(BossController boss)
    {
        if (boss == null || boss.Profile == null || boss.Profile.Extensions == null) return false;
        foreach (var e in boss.Profile.Extensions)
        {
            if (e is TerminalRebootExtension) return true;
        }
        return false;
    }

    /// <summary>演出の表の上の時間（0〜3.5）。演出中でなければ -1</summary>
    static float TableT(BossController boss) =>
        boss != null && boss.State == BossState.Transition ? boss.TransitionProgress * TableTime : -1f;

    // ───────── 流れ ─────────

    void OnTransitionStarted()
    {
        if (!IsTerminal(_boss)) return;
        _shookPanic = false;
        DestroyPanic();
        BeginHitStop();
        FindWindow();
        Vector2 at = _window != null ? (Vector2)_window.bounds.center : (Vector2)_boss.BodyBounds.center; // 被弾範囲を切る前に取る
        Vfx.Shards(at);
        TerminalCameraShake.Shake(0.25f, 0.12f);

        // 演出の 3.5秒の間、プレイヤーはダメージを受けない（docs 4-4 の OK の条件）：
        // 場に残っている物（赤い波線などのふつうの判定）も全部消し、体当たりを切る。演出が終わったら Update で戻す
        if (_boss.Runner != null) _boss.Runner.ClearSpawned();
        _boss.SetContactEnabled(false);
        _contactOff = true;
    }

    void OnNormalized(SkillDefinition _) => DestroyPanic();

    void Update()
    {
        if (_hitStop && Time.unscaledTime >= _hitStopEnd) EndHitStop();
        if (_boss == null || !IsTerminal(_boss))
        {
            DestroyPanic();
            return;
        }

        BossState state = _boss.State;
        // 演出が終わったら、体当たりと被弾範囲を戻す（正常化・追い出しのときは BossController が切ったままにする）
        if (_contactOff && state != BossState.Transition)
        {
            _contactOff = false;
            if (state == BossState.Fighting) _boss.SetContactEnabled(true);
        }
        if (state == BossState.Transition)
        {
            float t = TableT(_boss);
            if (t >= PanicAt)
            {
                CreatePanic();
                if (!_shookPanic)
                {
                    _shookPanic = true;
                    TerminalCameraShake.Shake(TableTime - PanicAt, 0.35f);
                }
            }
        }
        else if (state == BossState.Normalized || (state == BossState.Fighting && _boss.Phase <= 1))
        {
            DestroyPanic(); // 正常化・やり直し（ResetBoss はイベントを出さないので、形態を見て戻す）
        }
        else if (_boss.Phase >= 2)
        {
            CreatePanic(); // 第2形態の途中から足されたときなど
        }
    }

    void CreatePanic()
    {
        if (_panic != null) return;
        _panic = TerminalKernelPanic.Create();
    }

    void DestroyPanic()
    {
        if (_panic == null) return;
        Destroy(_panic.gameObject);
        _panic = null;
    }

    // ───────── ヒットストップ ─────────
    // 時間の速さ（Time.timeScale）は GameSession も使う（ゲームオーバー・デスクトップの画面で 0）。
    // 1 のときだけ変え、自分の値のままのときだけ 1 に戻す

    void BeginHitStop()
    {
        GameSession s = GameSession.Instance;
        if (s != null && (s.IsTransitioning || s.IsGameOver)) return;
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;
        Time.timeScale = HitStopScale;
        _hitStop = true;
        _hitStopEnd = Time.unscaledTime + HitStopRealTime;
    }

    void EndHitStop()
    {
        if (!_hitStop) return;
        _hitStop = false;
        if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
    }

    // ───────── ボスのウィンドウに重ねる絵 ─────────

    void LateUpdate()
    {
        if (_boss == null || !IsTerminal(_boss))
        {
            HideOverlays();
            return;
        }
        if (_window == null && Time.time >= _nextFind)
        {
            _nextFind = Time.time + 1f;
            FindWindow();
        }
        if (_window == null)
        {
            HideOverlays();
            return;
        }
        EnsureOverlays();

        BossState state = _boss.State;
        float t = TableT(_boss);
        float vis = _boss.Visibility;
        Sprite sprite = _window.sprite;
        bool shown = _window.enabled && sprite != null && vis > 0.01f;

        // 0〜2.6秒：真っ黒（倒したように見せる）
        bool black = shown && t >= 0f && t < PanicAt;
        SetOverlay(_black, black, sprite, new Color(0f, 0f, 0f, vis), 0f);

        // 2.6秒〜と第2形態：ふちとタイトルバーが赤
        bool red = shown && (t >= PanicAt || (state == BossState.Fighting && _boss.Phase >= 2));
        SetOverlay(_frame, red, red ? FrameSprite(sprite) : null, new Color(1f, 1f, 1f, vis), 0f);

        // 赤いちらつき（横にずれる）。立ち上がる間は速く、第2形態は 0.5秒に1回
        bool glitch = false;
        float offset = 0f;
        if (red)
        {
            float now = Time.time;
            glitch = t >= PanicAt ? Mathf.FloorToInt(now / 0.1f) % 2 == 0 : Mathf.Repeat(now, GlitchInterval) < GlitchTime;
            offset = (Mathf.FloorToInt(now / 0.04f) % 2 == 0 ? 2f : -2f) / Mathf.Max(1f, sprite.pixelsPerUnit);
        }
        SetOverlay(_glitch, glitch, sprite, TerminalArt.WithAlpha(TerminalArt.Red, 0.55f * vis), offset);

        // 0〜0.6秒：ノイズがはじける
        UpdateNoise(shown && t >= 0f && t < BlackoutEnd);
    }

    /// <summary>着ているウィンドウ（子の「Window」。無ければ 48×36 ドットの絵）を探す</summary>
    void FindWindow()
    {
        _window = null;
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in all)
        {
            if (sr != null && sr.gameObject.name == "Window")
            {
                _window = sr;
                return;
            }
        }
        foreach (var sr in all)
        {
            if (sr == null || sr.sprite == null || sr.gameObject.name.StartsWith("Terminal")) continue;
            if (Mathf.RoundToInt(sr.sprite.rect.width) == 48 && Mathf.RoundToInt(sr.sprite.rect.height) == 36)
            {
                _window = sr;
                return;
            }
        }
    }

    void EnsureOverlays()
    {
        if (_black != null && _black.transform.parent == _window.transform) return;
        if (_black != null) Destroy(_black.gameObject);
        if (_frame != null) Destroy(_frame.gameObject);
        if (_glitch != null) Destroy(_glitch.gameObject);
        // 同じ並び順のまま、カメラに少し近づけて、ウィンドウの手前に描く（ウイルスの手より奥）
        _black = MakeOverlay("TerminalBlackout", -0.02f);
        _frame = MakeOverlay("TerminalRedFrame", -0.03f);
        _glitch = MakeOverlay("TerminalGlitch", -0.04f);
    }

    SpriteRenderer MakeOverlay(string objectName, float z)
    {
        var go = new GameObject(objectName);
        go.layer = _window.gameObject.layer;
        go.transform.SetParent(_window.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.enabled = false;
        return sr;
    }

    void SetOverlay(SpriteRenderer sr, bool on, Sprite sprite, Color color, float offsetX)
    {
        if (sr == null) return;
        sr.enabled = on && sprite != null;
        if (!sr.enabled) return;
        sr.sprite = sprite;
        sr.color = color;
        sr.flipX = _window.flipX;
        sr.flipY = _window.flipY;
        sr.sortingLayerID = _window.sortingLayerID;
        sr.sortingOrder = _window.sortingOrder;
        Vector3 p = sr.transform.localPosition;
        p.x = offsetX;
        p.y = 0f;
        sr.transform.localPosition = p;
    }

    void HideOverlays()
    {
        if (_black != null) _black.enabled = false;
        if (_frame != null) _frame.enabled = false;
        if (_glitch != null) _glitch.enabled = false;
        if (_noiseRoot != null && _noiseRoot.gameObject.activeSelf) _noiseRoot.gameObject.SetActive(false);
    }

    /// <summary>ウィンドウの絵と同じ大きさで、ふち（3重）とタイトルバーだけを赤くした絵（閉じるボタンは残す）</summary>
    Sprite FrameSprite(Sprite src)
    {
        if (src == null) return null;
        if (_frameCache.TryGetValue(src, out Sprite cached) && cached != null) return cached;
        int w = Mathf.RoundToInt(src.rect.width), h = Mathf.RoundToInt(src.rect.height);
        if (w <= 0 || h <= 0) return null;
        Color[] source = null;
        if (src.texture != null && src.texture.isReadable)
        {
            Rect tr = src.textureRect;
            source = src.texture.GetPixels(Mathf.RoundToInt(tr.x), Mathf.RoundToInt(tr.y), w, h);
        }
        var px = new Color32[w * h];
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row; // 上から数えた行（テクスチャは下の行から）
            for (int x = 0; x < w; x++)
            {
                int i = row * w + x;
                if (source != null && source[i].a <= 0.01f) continue; // 欠けた角などは塗らない
                int ring = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                Color c;
                if (ring == 0) c = FrameDark;
                else if (ring == 1) c = TerminalArt.Red;
                else if (ring == 2) c = FrameLight;
                else if (y >= 3 && y <= 6 && x < w - 9) c = TerminalArt.Red; // タイトルバー
                else continue;
                px[i] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = $"{src.name}_TerminalRed",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(px);
        tex.Apply();
        Vector2 pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
        Sprite made = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
        made.name = tex.name;
        made.hideFlags = HideFlags.DontSave;
        _frameCache[src] = made;
        return made;
    }

    void UpdateNoise(bool on)
    {
        if (!on)
        {
            if (_noiseRoot != null && _noiseRoot.gameObject.activeSelf) _noiseRoot.gameObject.SetActive(false);
            return;
        }
        if (_noiseRoot == null)
        {
            var go = new GameObject("TerminalNoise");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _noiseRoot = go.transform;
            for (int i = 0; i < NoiseCount; i++)
            {
                _noise.Add(TerminalArt.Box(_noiseRoot, Vector2.zero, Vector2.one * TerminalArt.Pixel * 3f, TerminalArt.White, 0, "Noise"));
            }
        }
        if (!_noiseRoot.gameObject.activeSelf) _noiseRoot.gameObject.SetActive(true);
        if (Time.time < _nextNoise) return;
        _nextNoise = Time.time + 0.04f;

        Bounds b = _window.bounds;
        int order = _window.sortingOrder + 2;
        foreach (var sr in _noise)
        {
            if (sr == null) continue;
            sr.enabled = Random.value < 0.7f;
            sr.sortingLayerID = _window.sortingLayerID;
            sr.sortingOrder = order;
            float r = Random.value;
            sr.color = r < 0.45f ? TerminalArt.White : r < 0.8f ? NoiseGray : TerminalArt.Green;
            float size = TerminalArt.Pixel * Random.Range(2, 6);
            Vector2 native = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(size / Mathf.Max(native.x, 0.001f), size / Mathf.Max(native.y, 0.001f), 1f);
            sr.transform.position = new Vector3(
                Random.Range(b.min.x - 0.3f, b.max.x + 0.3f),
                Random.Range(b.min.y - 0.3f, b.max.y + 0.3f),
                0f);
        }
    }

    // ───────── 画面の上の表示（System Rebooting...） ─────────

    void OnGUI()
    {
        if (_boss == null || _boss.State != BossState.Transition || !IsTerminal(_boss)) return;
        if (TerminalArt.GuiHidden()) return;
        float t = TableT(_boss);
        if (t < BlackoutEnd) return;
        float k = t < PanicAt
            ? Mathf.Clamp01((t - BlackoutEnd) / DimFadeIn)
            : 1f - Mathf.Clamp01((t - PanicAt) / DimFadeOut);
        if (k <= 0f) return;

        GUI.depth = 3; // 画面表示・コマンドの行より手前
        PixelGui.Begin(false);
        float sw = PixelGui.Width, sh = PixelGui.Height;
        PixelGui.Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, DimAlpha * k));

        if (t < PanicAt)
        {
            Color white = TerminalArt.WithAlpha(TerminalArt.White, k);
            GUIStyle style = PixelGui.Style(true, 16);
            // 点の数が変わっても文字が左右に動かないように、「...」まで入れた幅で左をそろえる
            float tw = Mathf.Ceil(PixelGui.Measure("System Rebooting...", style));
            float x = Mathf.Round((sw - tw) * 0.5f);
            float y = Mathf.Round(sh * 0.5f - 16f);
            int dots = 1 + Mathf.FloorToInt((t - BlackoutEnd) / 0.3f) % 3;
            PixelGui.Draw(new Rect(x, y, tw + 16f, 16f), "System Rebooting" + new string('.', dots), style, white);

            // 進み具合の棒（白、横6マス）
            float bw = Mathf.Round(BarUnits * TerminalArt.GuiPerUnit());
            var bar = new Rect(Mathf.Round((sw - bw) * 0.5f), y + 26f, bw, 6f);
            PixelGui.Border(bar, white, 1f);
            float p = Mathf.Clamp01((t - BlackoutEnd) / (PanicAt - BlackoutEnd));
            PixelGui.Fill(new Rect(bar.x + 2f, bar.y + 2f, Mathf.Round((bar.width - 4f) * p), bar.height - 4f), white);
        }
        PixelGui.End();
    }

    // ───────── ゲージの棒（IBossGaugeDisplay） ─────────

    public float DisplayRatio(BossController boss)
    {
        if (boss == null || boss.Gauge == null) return 0f;
        float real = Mathf.Clamp01(boss.Gauge.Ratio);
        if (!IsTerminal(boss)) return real;
        float th = boss.Profile != null ? Mathf.Clamp(boss.Profile.Phase2Threshold, 0.05f, 0.95f) : 0.5f;
        if (boss.State == BossState.Transition)
        {
            float t = TableT(boss);
            if (t < PanicAt) return 1f;                                   // 満タン（倒しかけに見せる）
            return 1f - Mathf.Clamp01((t - PanicAt) / (TableTime - PanicAt)); // 右から左へくずれて空に
        }
        if (boss.Phase >= 2) return Mathf.Clamp01((real - th) / (1f - th));
        return Mathf.Clamp01(real / th);
    }

    public Color? FillColor(BossController boss)
    {
        if (boss == null || !IsTerminal(boss)) return null;
        if (boss.State == BossState.Transition) return TableT(boss) < PanicAt ? TerminalArt.White : TerminalArt.Red;
        if (boss.Phase >= 2) return TerminalArt.Red;
        return null;
    }

    public bool ShowThresholdMark(BossController boss) => !IsTerminal(boss); // 50% で満タンに見せるので、50% の印は出さない
}
