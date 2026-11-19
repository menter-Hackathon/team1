using UnityEngine;

/// <summary>
/// ボス B「ウイルスがアプリを着ている」の見た目（2026-11-17 まさぴ〜決定）。判定には関わらない。
/// ・大きなウイルス（黒い煙）が、アプリのウィンドウを鎧のように抱えている
/// ・ゲージが 1/3・2/3 を超えると、ウィンドウにひびが入る（破片が飛ぶ）
/// ・形態変化の演出中（State＝Transition）は、体と顔が赤く点滅する。第2形態からは怒り顔
/// ・ゲージ100%（State＝Ejected）：ウイルスが追い出され（大きい煙）、きれいになったウィンドウがきらめきながら上へ消える。
///   逃げるウイルス本体は、BossController の「追い出されるウイルス」が受け持つ
/// ・瞬間移動で消えるとき・登場で点滅するときは、ボスの見え方（BossController.Visibility）に合わせる
/// 色は全部ここで決める（BossController の「見た目は別の部品」をオンにしておく）。
/// </summary>
public class BossArmorVisual : MonoBehaviour
{
    [SerializeField] BossController _boss;
    [Tooltip("ウイルスの体（コマ送り）")]
    [SerializeField] SpriteRenderer _body;
    [Tooltip("着ているウィンドウ")]
    [SerializeField] SpriteRenderer _window;
    [Tooltip("ウィンドウの絵：0 きれい、1 小さなひび、2 大きなひびと欠け")]
    [SerializeField] Sprite[] _windowStages;
    [Tooltip("ウィンドウをつかむ手（ウィンドウより手前）")]
    [SerializeField] SpriteRenderer _hands;
    [SerializeField] SpriteRenderer _face;
    [Tooltip("顔のコマ：0 ふつう、1 まばたき、2 怒り、3 怒り（まばたき）")]
    [SerializeField] Sprite[] _faceFrames;
    [SerializeField] Color _angryColor = new Color(1f, 0.35f, 0.35f);
    [Tooltip("きれいになったウィンドウが上がる高さ（マス）と、かける秒数")]
    [SerializeField] float _releaseRise = 1.2f;
    [SerializeField] float _releaseTime = 1.6f;

    VirusGauge _gauge;
    int _stage;
    float _glitchUntil;      // ウィンドウの乱れ（2026-11-18：当たったエフェクト 1-3）
    SpriteRenderer _glitch;  // 乱れの絵（まん中の帯を横にずらして重ねる）
    readonly System.Collections.Generic.Dictionary<Sprite, Sprite> _midCache = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

    /// <summary>着ているウィンドウの絵（当たったときに白くする所。HitFx・FlashOverlay が使う）</summary>
    public SpriteRenderer Window => _window;

    // 読むだけの入口（2026-11-19：図鑑の詳しい画面が、プレハブを作らずに絵と位置を読む。docs/ui-text-and-dex-design.md 3-4）。書き換えない
    /// <summary>ウイルスの体（コマ送りは同じ GameObject の SpriteFrameAnimation）</summary>
    public SpriteRenderer Body => _body;
    public SpriteRenderer Hands => _hands;
    public SpriteRenderer Face => _face;
    /// <summary>ウィンドウの絵：0 きれい、1 小さなひび、2 大きなひびと欠け</summary>
    public System.Collections.Generic.IReadOnlyList<Sprite> WindowStages => _windowStages;
    /// <summary>顔のコマ：0 ふつう、1 まばたき、2 怒り、3 怒り（まばたき）</summary>
    public System.Collections.Generic.IReadOnlyList<Sprite> FaceFrames => _faceFrames;

    /// <summary>
    /// ウィンドウの乱れ（重い・とどめ。1-3）：ウィンドウの絵を横に3つに切って、まん中だけ 2ドット横へずらす（seconds 秒）。
    /// 元の絵はさわらず、まん中の帯を重ねる
    /// </summary>
    public void Glitch(float seconds) => _glitchUntil = Mathf.Max(_glitchUntil, Time.time + seconds);
    float _flashUntil;
    float _nextBlink;
    float _blinkUntil;
    float _releasedAt = -1f;
    int _sparkles;
    Vector3 _windowHome;

    void Awake()
    {
        if (_boss == null) _boss = GetComponentInParent<BossController>();
        _gauge = _boss != null ? _boss.GetComponent<VirusGauge>() : null;
        if (_window != null) _windowHome = _window.transform.localPosition;
    }

    void OnEnable()
    {
        if (_gauge != null)
        {
            _gauge.Changed += OnGaugeChanged;
            _gauge.Gained += OnGained;
        }
        if (_boss != null) _boss.Ejected += OnEjected;
        ResetLook();
        // シーンの途中で出てきたとき（雑魚を倒して登場・テストシーンの切り替え）は、ウイルスの煙を出す
        if (Time.timeSinceLevelLoad > 0.5f && _boss != null) Vfx.Smoke(_boss.transform.position);
    }

    void OnDisable()
    {
        if (_gauge != null)
        {
            _gauge.Changed -= OnGaugeChanged;
            _gauge.Gained -= OnGained;
        }
        if (_boss != null) _boss.Ejected -= OnEjected;
    }

    /// <summary>着ているウィンドウの絵を差し替える（代用ボス：アプリのアイコンから作ったウィンドウ。2026-11-17）</summary>
    public void SetWindowStages(Sprite[] stages)
    {
        if (stages == null || stages.Length == 0) return;
        _windowStages = stages;
        if (_window != null) _window.sprite = _windowStages[Mathf.Clamp(_stage, 0, _windowStages.Length - 1)];
    }

    void ResetLook()
    {
        _stage = 0;
        _releasedAt = -1f;
        _sparkles = 0;
        if (_window != null) _window.transform.localPosition = _windowHome;
    }

    void OnGaugeChanged(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        if (ratio <= 0f)
        {
            ResetLook(); // やり直し
            return;
        }
        int stage = ratio >= 2f / 3f ? 2 : ratio >= 1f / 3f ? 1 : 0;
        if (stage > _stage && _releasedAt < 0f && _window != null)
        {
            Vfx.Shards(_window.transform.position);
            Vfx.SmokeSmall(_window.transform.position + new Vector3(0.6f, -0.3f, 0f));
        }
        _stage = Mathf.Max(_stage, stage);
    }

    // 2026-11-18：当たったときの光り方は HitFx（ウィンドウだけ白く重ねる。煙と顔は白くしない 1-3）に任せた。
    // 白く重ねる絵が作れないときだけ、今までの光り方（ウィンドウを少し暗く）に戻す
    void OnGained(DamageInfo _)
    {
        if (!FlashOverlay.Available) _flashUntil = Time.time + 0.08f;
    }

    void OnEjected()
    {
        _releasedAt = Time.time;
        _sparkles = 0;
        if (_body != null) Vfx.Smoke(_body.transform.position); // ウイルスが押し出される
    }

    void LateUpdate()
    {
        if (_boss == null) return;
        float now = Time.time;
        float alpha = _boss.Visibility;
        BossState state = _boss.State;
        bool released = state == BossState.Ejected || state == BossState.Normalized;
        bool transition = state == BossState.Transition;
        bool angryBlink = transition && Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        bool flash = now < _flashUntil;

        // ── ウイルスの体・手・顔 ──
        Color tint = angryBlink ? _angryColor : Color.white;
        if (flash) tint = Color.Lerp(tint, Color.white, 0.6f);
        tint.a = alpha;
        bool showVirus = !released && alpha > 0.01f;
        Apply(_body, showVirus, tint);
        Apply(_hands, showVirus, tint);
        Apply(_face, showVirus, tint);
        if (_face != null && _faceFrames != null && _faceFrames.Length >= 4)
        {
            if (now >= _nextBlink)
            {
                _blinkUntil = now + 0.12f;
                _nextBlink = now + Random.Range(2.2f, 3.8f);
            }
            bool angry = _boss.Phase >= 2 || transition;
            _face.sprite = _faceFrames[(angry ? 2 : 0) + (now < _blinkUntil ? 1 : 0)];
        }

        // ── 着ているウィンドウ ──
        if (_window == null) return;
        if (!released)
        {
            // ウィンドウは白っぽいので、当たったときは少し暗くして光ったように見せる
            Color w = flash ? new Color(0.75f, 0.75f, 0.8f) : Color.white;
            w.a = alpha;
            Apply(_window, alpha > 0.01f, w);
            _window.transform.localPosition = _windowHome;
            if (_windowStages != null && _windowStages.Length > 0) _window.sprite = _windowStages[Mathf.Clamp(_stage, 0, _windowStages.Length - 1)];
            UpdateGlitch(now, alpha);
            return;
        }
        UpdateGlitch(now, 0f);

        // ゲージ100%：きれいになったウィンドウが、きらめきながら上へ消える
        float t = _releasedAt >= 0f ? (now - _releasedAt) / Mathf.Max(0.01f, _releaseTime) : 1f;
        if (_windowStages != null && _windowStages.Length > 0) _window.sprite = _windowStages[0];
        float rise = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t)); // だんだんゆっくり
        _window.transform.localPosition = _windowHome + Vector3.up * (_releaseRise * rise);
        Color c = Color.white;
        c.a = t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
        Apply(_window, t < 1f, c);
        // きらめきを3回
        if (t < 1f && _sparkles < 3 && t >= _sparkles * 0.25f)
        {
            Vector3 p = _window.transform.position;
            Vector3 offset = _sparkles switch { 0 => new Vector3(-1.4f, 0.6f, 0f), 1 => new Vector3(1.5f, 0.2f, 0f), _ => new Vector3(0.2f, 1.1f, 0f) };
            Vfx.Sparkle(p + offset);
            _sparkles++;
        }
    }

    /// <summary>乱れの絵（まん中の帯を 2ドット横へずらして、ウィンドウの手前に重ねる）</summary>
    void UpdateGlitch(float now, float alpha)
    {
        bool on = now < _glitchUntil && alpha > 0.01f && _window.enabled && _window.sprite != null;
        if (!on)
        {
            if (_glitch != null) _glitch.enabled = false;
            return;
        }
        if (_glitch == null)
        {
            var go = new GameObject("WindowGlitch");
            go.layer = _window.gameObject.layer;
            go.transform.SetParent(_window.transform, false);
            _glitch = go.AddComponent<SpriteRenderer>();
        }
        Sprite mid = MiddleBand(_window.sprite);
        _glitch.enabled = mid != null;
        if (mid == null) return;
        _glitch.sprite = mid;
        _glitch.flipX = _window.flipX;
        _glitch.sortingLayerID = _window.sortingLayerID;
        _glitch.sortingOrder = _window.sortingOrder;
        _glitch.color = new Color(1f, 1f, 1f, alpha);
        float px = 2f / Mathf.Max(1f, _window.sprite.pixelsPerUnit);
        _glitch.transform.localPosition = new Vector3(Mathf.FloorToInt(now / 0.02f) % 2 == 0 ? px : -px, 0f, -0.015f);
    }

    /// <summary>ウィンドウの絵の、まん中の 1/3 の帯（同じテクスチャの一部。読み出しできない絵でも作れる）</summary>
    Sprite MiddleBand(Sprite src)
    {
        if (src == null) return null;
        if (_midCache.TryGetValue(src, out Sprite cached) && cached != null) return cached;
        Rect r = src.rect;
        float h3 = Mathf.Floor(r.height / 3f);
        if (h3 < 1f) return null;
        var band = new Rect(r.x, r.y + h3, r.width, r.height - h3 * 2f);
        Vector2 pivot = new Vector2(src.pivot.x / band.width, (src.pivot.y - h3) / band.height);
        Sprite made = Sprite.Create(src.texture, band, pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        made.name = src.name + "_GlitchBand";
        made.hideFlags = HideFlags.DontSave;
        _midCache[src] = made;
        return made;
    }

    static void Apply(SpriteRenderer r, bool show, Color c)
    {
        if (r == null) return;
        r.enabled = show;
        r.color = c;
    }
}
