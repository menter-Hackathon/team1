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

    void OnGained(DamageInfo _) => _flashUntil = Time.time + 0.08f;

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
            return;
        }

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

    static void Apply(SpriteRenderer r, bool show, Color c)
    {
        if (r == null) return;
        r.enabled = show;
        r.color = c;
    }
}
