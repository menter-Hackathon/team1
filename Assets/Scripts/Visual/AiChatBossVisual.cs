using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI チャットのボスの見た目（2026-11-19。判定には関わらない。見た目・秒数は【仮】）。
/// ・体：アプリのアイコンをドット絵にしたもの（アプリのアイコンを読めたら 40 ドット、だめなら 24 ドットを2倍、それもなければ仮のアイコン）。
///   ふわふわ上下する（羽は 2026-11-19 に外した）
/// ・後ろに、ウイルスの黒い煙をアプリの色味にしたものが、ゆっくり漂い続ける（3つの煙を時間をずらして、ゆっくりコマ送り）
/// ・技の絵（レーザー・電流・ミニロボット）をアプリの色に塗り替える（ボスの設定を、塗り替えた複製に差し替える）
/// ・当たると白く光る。形態変化の演出中は赤く点滅。エラーで止まっている間は震えて、頭の上に「ERROR」のウィンドウ
/// ・第2形態の大技（文字列のレーザー）の予備動作の間は、頭の上に「LOADING」と AI の考え中の表示
/// ・ゲージ100%：煙がはじけて、きれいになったアイコンがきらめきながら上へ消える
/// アプリの見た目は StageDirector が渡す（IBossAppLook）。渡されなかったとき（テストシーン）は、「試すアプリ」の見た目で始める。
/// 色は全部ここで決める（BossController の「見た目は別の部品」をオンにしておく）。
/// </summary>
public class AiChatBossVisual : MonoBehaviour, IBossAppLook
{
    [SerializeField] BossController _boss;
    [Header("絵")]
    [Tooltip("体（アプリのアイコン）")]
    [SerializeField] SpriteRenderer _icon;
    [Tooltip("後ろに漂う煙（いくつでもよい）")]
    [SerializeField] SpriteRenderer[] _smoke;
    [Tooltip("エラーのウィンドウ（頭の上）")]
    [SerializeField] SpriteRenderer _error;
    [Tooltip("煙のコマ（黒いウイルスの煙）。アプリの色味に塗り替える")]
    [SerializeField] Sprite[] _smokeFrames;
    [Tooltip("アプリのアイコンが無いときの体")]
    [SerializeField] Sprite _defaultIcon;
    [Tooltip("アプリの色が無いときの色")]
    [SerializeField] AppTheme _defaultTheme;

    [Header("動き")]
    [Tooltip("煙のコマ送りの速さ（コマ/秒）。敵が倒れたときの煙（14）より、ずっとゆっくり")]
    [SerializeField, Min(0.1f)] float _smokeFps = 3.5f;
    [Tooltip("体のアイコンの大きさ（ドット）。アプリのアイコンを読めたとき")]
    [SerializeField, Range(16, 64)] int _iconPixels = 40;

    [Header("テストシーン（アプリの見た目が渡されなかったとき）")]
    [SerializeField] string _previewAppName = "ChatGPT";
    [SerializeField] string _previewBundleId = "com.openai.chat";

    static readonly Color AngryColor = new Color(1f, 0.35f, 0.35f);

    VirusGauge _gauge;
    BossProfile _baseProfile;    // 塗り替える前の設定
    AppTheme _theme;
    Sprite[] _smokeLook;
    bool _lookApplied;
    float _flashUntil;
    float _releasedAt = -1f;
    int _sparkles;
    Vector3 _iconHome;
    float _errorUntil;
    float _errorStart;
    float _thinkStart = -1f, _thinkEnd;
    string _appName = "";

    /// <summary>今の見た目のアプリの色</summary>
    public AppTheme Theme => _theme;
    /// <summary>考え中（大技の予備動作）の表示中か</summary>
    public bool IsThinking => _thinkStart >= 0f && Time.time < _thinkEnd;

    void Awake()
    {
        if (_boss == null) _boss = GetComponentInParent<BossController>();
        _gauge = _boss != null ? _boss.GetComponent<VirusGauge>() : null;
        if (_icon != null) _iconHome = _icon.transform.localPosition;
        if (!_lookApplied) SetLook(_defaultIcon, _defaultTheme);
    }

    void Start()
    {
        // テストシーンなど、アプリの見た目が渡されなかったときは「試すアプリ」の見た目にする
        if (!_lookApplied && !string.IsNullOrEmpty(_previewAppName))
        {
            AppVisual v = AppVisuals.Get(new AppUsageRow { Name = _previewAppName, BundleId = _previewBundleId, Seconds = 1 });
            ApplyAppLook(_previewAppName, v, v != null ? v.Theme : null);
        }
    }

    void OnEnable()
    {
        if (_gauge != null) _gauge.Gained += OnGained;
        if (_boss != null)
        {
            _boss.Ejected += OnEjected;
            _boss.ErrorRaised += OnError;
            if (_boss.TryGetComponent(out AttackRunner runner)) runner.Started += OnAttackStarted; // BossController の準備（Awake）より先に呼ばれても取れるように
        }
        _releasedAt = -1f;
        _sparkles = 0;
        _errorUntil = 0f;
        _thinkStart = -1f;
        if (Time.timeSinceLevelLoad > 0.5f && _boss != null) Vfx.Smoke(_boss.transform.position);
    }

    void OnDisable()
    {
        if (_gauge != null) _gauge.Gained -= OnGained;
        if (_boss != null)
        {
            _boss.Ejected -= OnEjected;
            _boss.ErrorRaised -= OnError;
            if (_boss.TryGetComponent(out AttackRunner runner)) runner.Started -= OnAttackStarted;
        }
    }

    // ───────── アプリの見た目 ─────────

    /// <summary>
    /// アプリの見た目にする（StageDirector・テスト用の切り替えから）。アイコン・羽と煙の色・技の色を差し替える。
    /// 何度呼んでもよい（いつも塗り替える前の設定から作り直す）
    /// </summary>
    public void ApplyAppLook(string appName, AppVisual visual, AppTheme theme)
    {
        _appName = appName ?? "";
        // 対応表から来たボスなど、見た目が無いときは名前でアイコンを探す
        if (visual == null && !string.IsNullOrEmpty(appName)) visual = AppVisuals.Get(new AppUsageRow { Name = appName, Seconds = 1 });
        if (theme == null && visual != null) theme = visual.Theme;
        if (theme == null) theme = _defaultTheme;

        SetLook(BodyIcon(visual), theme);
        _lookApplied = true;

        // 技の絵をアプリの色にする（設定は塗り替えた複製に差し替える。元の設定は覚えておく）
        if (_boss == null) _boss = GetComponentInParent<BossController>(true); // 出てくる前（非表示）に呼ばれることがある
        if (_boss != null && _boss.Profile != null && theme != null)
        {
            if (_baseProfile == null) _baseProfile = _boss.Profile;
            BossProfile look = _baseProfile.CreateRecoloredCopy(s => ThemeRecolor.Effect(s, theme));
            look.SetLook(string.IsNullOrEmpty(appName) ? _baseProfile.DisplayName : appName, visual != null ? visual.Icon12 : null);
            _boss.SetProfile(look);
        }
    }

    /// <summary>体にするアイコン：本物のアイコンを読めたら大きめのドット絵、だめなら 24 ドットを2倍、それも無ければ仮のアイコン</summary>
    Sprite BodyIcon(AppVisual visual)
    {
        if (visual == null) return _defaultIcon;
        if (visual.HasRealIcon)
        {
            // アイコンは AppVisuals が読んで覚えているので、読み直さない（同じキーなら AppIcons がすぐ返す）
            var icons = AppIcons.Fetch(new[] { new AppIcons.Request { Key = visual.Key, Name = visual.Name } });
            if (icons.TryGetValue(visual.Key, out Texture2D tex))
            {
                Sprite big = PixelIcon.Pixelize(tex, _iconPixels, $"AiBossIcon_{visual.Name}");
                if (big != null) return big;
            }
        }
        return visual.Icon24 != null ? visual.Icon24 : _defaultIcon;
    }

    void SetLook(Sprite icon, AppTheme theme)
    {
        _theme = theme;
        if (_icon != null)
        {
            _icon.sprite = icon;
            // 24 ドットのアイコンは2倍にして、40 ドットくらいの大きさにそろえる
            float h = icon != null ? icon.rect.height : _iconPixels;
            float k = h <= 24f ? 2f : 1f;
            _icon.transform.localScale = new Vector3(k, k, 1f);
        }
        _smokeLook = Recolor(_smokeFrames, s => theme != null ? ThemeRecolor.Smoke(s, theme) : s);
    }

    static Sprite[] Recolor(Sprite[] src, System.Func<Sprite, Sprite> f)
    {
        if (src == null) return new Sprite[0];
        var r = new Sprite[src.Length];
        for (int i = 0; i < src.Length; i++) r[i] = src[i] != null ? f(src[i]) : null;
        return r;
    }

    // ───────── 出来事 ─────────

    void OnGained(DamageInfo _) => _flashUntil = Time.time + 0.08f;

    void OnEjected()
    {
        _releasedAt = Time.time;
        _sparkles = 0;
        _thinkStart = -1f;
        if (_icon != null) Vfx.Smoke(_icon.transform.position); // ウイルスが押し出される
    }

    void OnError(float duration)
    {
        _errorStart = Time.time;
        _errorUntil = Time.time + duration;
        _thinkStart = -1f;
        if (_icon != null) Vfx.SmokeSmall(_icon.transform.position + new Vector3(0.5f, 0.6f, 0f));
    }

    void OnAttackStarted(AttackData data)
    {
        // 大技（文字列のレーザー）の予備動作の間は、考え中の表示（左端へ寄る間も、少しだけ残す）
        if (data == null || data.Kind != AttackKind.Beam || data.BeamStyle != BeamStyle.CodeStream) return;
        _thinkStart = Time.time;
        _thinkEnd = Time.time + data.Windup + (data.BeamOrigin == BeamOrigin.ArenaLeft ? data.BeamMoveTime : 0f);
    }

    // ───────── 毎フレーム ─────────

    void LateUpdate()
    {
        if (_boss == null) return;
        float now = Time.time;
        float alpha = _boss.Visibility;
        BossState state = _boss.State;
        bool released = state == BossState.Ejected || state == BossState.Normalized;
        bool transition = state == BossState.Transition;
        bool stunned = now < _errorUntil && !released;

        // ── 煙（ゆっくり漂い続ける。はじけたら出さない） ──
        AnimateSmoke(now, !released && alpha > 0.01f, alpha);

        bool showBody = !released && alpha > 0.01f;

        // ── 体の色 ──
        Color tint = Color.white;
        if (transition && Mathf.FloorToInt(now / 0.1f) % 2 == 0) tint = AngryColor;
        if (stunned) tint = Color.Lerp(Color.white, new Color(0.6f, 0.6f, 0.65f), 0.5f); // エラー中は少しくすむ
        if (now < _flashUntil) tint = Color.Lerp(tint, Color.white, 0.6f) * new Color(1.15f, 1.15f, 1.15f, 1f);
        tint.a = alpha;

        // ── エラーのウィンドウ ──
        if (_error != null)
        {
            bool showError = stunned && alpha > 0.01f;
            // 出てすぐは点滅
            _error.enabled = showError && (now - _errorStart > 0.3f || Mathf.FloorToInt(now / 0.06f) % 2 == 0);
        }

        if (_icon == null) return;
        if (!released)
        {
            // ふわふわ上下する。エラー中は小刻みに震える
            float bob = Mathf.Sin(now * 2.2f) * 0.08f;
            Vector3 shake = stunned ? new Vector3(Mathf.Sin(now * 70f) * 0.05f, 0f, 0f) : Vector3.zero;
            _icon.transform.localPosition = _iconHome + new Vector3(0f, bob, 0f) + shake;
            Apply(_icon, showBody, tint);
            return;
        }

        // ゲージ100%：きれいになったアイコンが、きらめきながら上へ消える
        float t = _releasedAt >= 0f ? (now - _releasedAt) / 1.6f : 1f;
        float rise = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
        _icon.transform.localPosition = _iconHome + Vector3.up * (1.2f * rise);
        Color c = Color.white;
        c.a = t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
        Apply(_icon, t < 1f, c);
        if (t < 1f && _sparkles < 3 && t >= _sparkles * 0.25f)
        {
            Vector3 p = _icon.transform.position;
            Vector3 offset = _sparkles switch { 0 => new Vector3(-1.2f, 0.5f, 0f), 1 => new Vector3(1.3f, 0.1f, 0f), _ => new Vector3(0.1f, 1.0f, 0f) };
            Vfx.Sparkle(p + offset);
            _sparkles++;
        }
    }

    void AnimateSmoke(float now, bool show, float alpha)
    {
        if (_smoke == null) return;
        int n = _smokeLook.Length;
        for (int i = 0; i < _smoke.Length; i++)
        {
            SpriteRenderer sr = _smoke[i];
            if (sr == null) continue;
            if (!show || n == 0) { sr.enabled = false; continue; }
            sr.enabled = true;
            // 煙ごとに時間をずらし、ゆっくりコマを送る（いつもどれかの煙が広がっていて、途切れない）
            float phase = now * _smokeFps + i * (n / (float)_smoke.Length);
            sr.sprite = _smokeLook[Mathf.FloorToInt(phase) % n];
            Color c = Color.white;
            c.a = 0.9f * alpha;
            sr.color = c;
        }
    }

    static void Apply(SpriteRenderer r, bool show, Color c)
    {
        if (r == null) return;
        r.enabled = show;
        r.color = c;
    }

    // ───────── 考え中の表示（大技の予備動作） ─────────

    static readonly string[] ThinkLines =
    {
        "Thinking...",
        "> Analyzing user position",
        "> Searching weak points",
        "> Generating response",
        "> Compiling 99999 lines",
    };

    void OnGUI()
    {
        if (!IsThinking || _boss == null || _boss.State != BossState.Fighting) return;
        Camera cam = Camera.main;
        if (cam == null || PixelGui.FontEn == null || _icon == null) return;
        PixelGui.Begin(false);
        float scale = PixelGui.Scale;
        float elapsed = Time.time - _thinkStart;
        float total = Mathf.Max(0.1f, _thinkEnd - _thinkStart);
        float t = Mathf.Clamp01(elapsed / total);

        // 頭の上に、考え中のウィンドウ（ドットのフォント）
        Vector3 head = cam.WorldToScreenPoint(_icon.transform.position + Vector3.up * 1.7f);
        const float w = 200f, lineH = 11f;
        int shown = Mathf.Clamp(Mathf.FloorToInt(t * (ThinkLines.Length + 1)), 0, ThinkLines.Length);
        float h = 32f + shown * lineH;
        float x = Mathf.Clamp(head.x / scale - w * 0.5f, 4f, PixelGui.Width - w - 4f);
        float y = Mathf.Clamp((Screen.height - head.y) / scale - h, 4f, PixelGui.Height - h - 4f);
        var box = new Rect(x, y, w, h);
        PixelGui.Fill(box, new Color(0.05f, 0.06f, 0.1f, 0.88f));
        Color accent = _theme != null ? _theme.Main : Color.cyan;
        accent.a = 1f;
        PixelGui.Fill(new Rect(box.x, box.y, box.width, 2f), accent);

        // LOADING と、点の数が増えていく
        string dots = new string('.', 1 + Mathf.FloorToInt(Time.time * 3f) % 3);
        PixelGui.TextEn(new Rect(box.x + 6f, box.y + 4f, w - 12f, 10f), "LOADING" + dots, 8, Color.white);
        // 進み具合のバー
        var bar = new Rect(box.x + 6f, box.y + 17f, w - 12f, 6f);
        PixelGui.Fill(bar, new Color(1f, 1f, 1f, 0.15f));
        PixelGui.Fill(new Rect(bar.x, bar.y, Mathf.Round(bar.width * t), bar.height), accent);
        // AI の考え中の文が、1行ずつ出てくる（薄い色）
        for (int i = 0; i < shown; i++)
        {
            var c = new Color(0.75f, 0.78f, 0.85f, i == shown - 1 ? 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f) : 0.8f);
            PixelGui.TextEn(new Rect(box.x + 6f, box.y + 28f + i * lineH, w - 12f, lineH), ThinkLines[i], 8, c);
        }
        PixelGui.End();
    }
}
