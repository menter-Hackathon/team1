using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ゲーム中の画面表示。カメラの子に置き、ゲームと同じドットの大きさの絵で描く（文字は出さない）。
/// ・左上：ライフ（ハート）と、能力の枠3つ（枠の下に 1・2・3）。選択中の枠はオレンジ
///   クールタイム中は、残りの割合だけ上から暗くする。入れ替え待ちのときは、新しい能力を枠の右に出して点滅させる
///   X 長押しの進み具合は、選択中の枠の下に赤い棒で出す
/// ・右上：ボスが出たら、アプリのアイコンとウイルス解除ゲージ。第2形態になる所に印。正常化したら白
/// ・色：能力の枠の縁はその能力のアプリの色、ゲージはそのステージのアプリの色（AppTheme。2026-11-17 決定）
/// UI 担当の本番の画面ができたら差し替える。
/// </summary>
public class PixelHud : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] Health _playerHealth;
    [SerializeField] PlayerSkills _skills;
    [SerializeField] PlayerSkillExecutor _executor;
    [Tooltip("ステージの進行（あれば、そのステージのボスを出す）。無ければ、出ているボスを探す")]
    [SerializeField] StageDirector _director;
    [Tooltip("並び順（ゲームの物より手前）")]
    [SerializeField] int _sortingOrder = 200;

    const float Ppu = GameArt.PixelsPerUnit;
    static readonly Color GaugeColor = new Color32(0x2a, 0xb1, 0x1a, 0xff);
    static readonly Color GaugeDoneColor = Color.white; // 正常化：きれいになった（白）
    static readonly Color PreBossColor = new Color32(0xc8, 0x24, 0x32, 0xff); // ボスが出るまでのゲージ（ウイルスの赤）
    static readonly Color PreBossIconColor = new Color(0.22f, 0.22f, 0.26f, 1f); // まだ来ていないボスのアイコンは暗い影
    static readonly Color GaugeAngryColor = new Color32(0xff, 0x40, 0x40, 0xff);
    static readonly Color CooldownColor = new Color(0f, 0f, 0f, 0.62f);
    static readonly Color DiscardColor = new Color32(0xff, 0x00, 0x00, 0xff);

    GameArt _art;
    Transform _root;
    readonly List<SpriteRenderer> _hearts = new List<SpriteRenderer>();
    SpriteRenderer[] _slotFrames, _slotIcons, _slotCooldowns, _digits;
    SpriteRenderer _pendingFrame, _pendingIcon, _discardBar;
    Transform _bossRoot;
    SpriteRenderer _bossIcon, _gaugeFrame, _gaugeMark;
    readonly SpriteRenderer[] _gaugeFills = new SpriteRenderer[4]; // アプリの色が4色なら4つに区切って塗る
    BossController _boss;
    float _nextBossSearch;
    float _lastAspect = -1f;

    void Start()
    {
        _art = GameArt.Instance;
        if (_camera == null) _camera = GetComponentInParent<Camera>();
        if (_art == null || _camera == null)
        {
            enabled = false;
            return;
        }
        if (_skills == null) _skills = FindObjectOfType<PlayerSkills>();
        if (_skills != null)
        {
            if (_playerHealth == null) _playerHealth = _skills.GetComponent<Health>();
            if (_executor == null) _executor = _skills.GetComponent<PlayerSkillExecutor>();
        }
        Build();
    }

    // ───────── 作る ─────────

    void Build()
    {
        _root = new GameObject("HudRoot").transform;
        _root.SetParent(transform, false);

        int lives = _playerHealth != null ? _playerHealth.Max : 3;
        for (int i = 0; i < lives; i++) _hearts.Add(Part($"Heart{i}", _art.Heart, 0));

        int n = _skills != null ? _skills.Capacity : 3;
        _slotFrames = new SpriteRenderer[n];
        _slotIcons = new SpriteRenderer[n];
        _slotCooldowns = new SpriteRenderer[n];
        _digits = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            _slotFrames[i] = Part($"Slot{i}", _art.Slot, 0);
            _slotIcons[i] = Part($"SlotIcon{i}", null, 1);
            _slotCooldowns[i] = Part($"SlotCooldown{i}", _art.Pixel, 2);
            _slotCooldowns[i].color = CooldownColor;
            Sprite digit = _art.Digits != null && i < _art.Digits.Length ? _art.Digits[i] : null;
            _digits[i] = Part($"Digit{i}", digit, 0);
        }
        _pendingFrame = Part("PendingSlot", _art.SlotSelected, 0);
        _pendingIcon = Part("PendingIcon", null, 1);
        _discardBar = Part("DiscardBar", _art.Pixel, 3);
        _discardBar.color = DiscardColor;

        _bossRoot = new GameObject("Boss").transform;
        _bossRoot.SetParent(_root, false);
        _bossIcon = Part("BossIcon", null, 0, _bossRoot);
        _gaugeFrame = Part("GaugeFrame", _art.GaugeFrame, 0, _bossRoot);
        _gaugeFrame.drawMode = SpriteDrawMode.Tiled;
        for (int i = 0; i < _gaugeFills.Length; i++)
            _gaugeFills[i] = Part($"GaugeFill{i}", _art.GaugeFill != null ? _art.GaugeFill : _art.Pixel, 1, _bossRoot);
        _gaugeMark = Part("GaugeMark", _art.Pixel, 2, _bossRoot);
        _gaugeMark.color = Color.black;
        Layout();
    }

    SpriteRenderer Part(string name, Sprite sprite, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent != null ? parent : _root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = _sortingOrder + order;
        return sr;
    }

    // 画面の左上（ドット）からの位置に、左上をそろえて置く
    void PlaceTopLeft(SpriteRenderer sr, float px, float py)
    {
        if (sr == null) return;
        Vector2 origin = TopLeft();
        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.rect.size : Vector2.zero;
        Vector2 pivot = sr.sprite != null ? sr.sprite.pivot : Vector2.zero;
        // スプライトの原点（pivot）から左上へのずれを足す
        float x = origin.x + (px + pivot.x) / Ppu;
        float y = origin.y - (py + (size.y - pivot.y)) / Ppu;
        sr.transform.localPosition = new Vector3(x, y, 0f);
    }

    // 画面の右上（ドット）からの位置
    void PlaceTopRight(SpriteRenderer sr, float pxFromRight, float py, float widthPx)
    {
        float viewW = ViewWidthPx();
        PlaceTopLeft(sr, viewW - pxFromRight - widthPx, py);
    }

    Vector2 TopLeft()
    {
        float h = _camera.orthographicSize;
        float w = h * _camera.aspect;
        return new Vector2(-w, h);
    }

    float ViewWidthPx() => _camera.orthographicSize * _camera.aspect * 2f * Ppu;

    void Layout()
    {
        _lastAspect = _camera.aspect;
        _root.localPosition = new Vector3(0f, 0f, 10f); // カメラの前（z=0 の平面）
        for (int i = 0; i < _hearts.Count; i++) PlaceTopLeft(_hearts[i], 6 + i * 11, 5);
        for (int i = 0; i < _slotFrames.Length; i++)
        {
            float x = 6 + i * 23;
            PlaceTopLeft(_slotFrames[i], x, 17);
            PlaceTopLeft(_digits[i], x + 7, 38);
        }
        PlaceTopLeft(_pendingFrame, 6 + _slotFrames.Length * 23 + 6, 17);

        // ゲージ：枠 100×8 ドット（9スライスで伸ばす）
        _gaugeFrame.size = new Vector2(100f / Ppu, 8f / Ppu);
        PlaceSizedTopLeft(_gaugeFrame, ViewWidthPx() - 6 - 100, 7, 100, 8);
    }

    // 9スライスで大きさを指定した絵の置き方（左上をそろえる）
    void PlaceSizedTopLeft(SpriteRenderer sr, float px, float py, float wPx, float hPx)
    {
        Vector2 origin = TopLeft();
        Vector2 pivot = sr.sprite != null ? sr.sprite.pivot / sr.sprite.rect.size : new Vector2(0.5f, 0.5f);
        float x = origin.x + (px + wPx * pivot.x) / Ppu;
        float y = origin.y - (py + hPx * (1f - pivot.y)) / Ppu;
        sr.transform.localPosition = new Vector3(x, y, 0f);
    }

    // ───────── 毎フレーム ─────────

    void LateUpdate()
    {
        if (!Mathf.Approximately(_camera.aspect, _lastAspect)) Layout();
        UpdateHearts();
        UpdateSlots();
        UpdateBoss();
    }

    void UpdateHearts()
    {
        int life = _playerHealth != null ? _playerHealth.Current : 0;
        for (int i = 0; i < _hearts.Count; i++) _hearts[i].sprite = i < life ? _art.Heart : _art.HeartEmpty;
    }

    void UpdateSlots()
    {
        if (_skills == null) return;
        bool pending = _skills.HasPending;
        bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / 0.25f) % 2 == 0;
        for (int i = 0; i < _slotFrames.Length; i++)
        {
            bool selected = i == _skills.SelectedSlot;
            // 入れ替え待ちのときは、全部の枠を点滅させて「1/2/3 で選ぶ」ことを見せる
            bool highlight = pending ? blinkOn : selected;
            SkillDefinition skill = _skills.GetSkill(i);
            // 能力が入っている枠は、そのアプリの色の縁（選択中は2重）。空の枠は今までどおり
            Sprite themed = skill != null ? ThemeRecolor.Slot(skill.Theme, highlight) : null;
            _slotFrames[i].sprite = themed != null ? themed : (highlight ? _art.SlotSelected : _art.Slot);

            _slotIcons[i].sprite = skill != null ? skill.Icon : null;
            float x = 6 + i * 23;
            PlaceTopLeft(_slotIcons[i], x + 2, 19);

            float ratio = skill != null && _executor != null ? _executor.GetCooldownRatio(skill) : 0f;
            int rows = Mathf.CeilToInt(ratio * 16f);
            _slotCooldowns[i].enabled = rows > 0;
            if (rows > 0)
            {
                _slotCooldowns[i].transform.localScale = new Vector3(16f, rows, 1f);
                PlaceScaledTopLeft(_slotCooldowns[i], x + 2, 19);
            }
        }

        _pendingFrame.enabled = pending;
        _pendingIcon.enabled = pending;
        if (pending)
        {
            Sprite themedPending = _skills.Pending != null ? ThemeRecolor.Slot(_skills.Pending.Theme, blinkOn) : null;
            _pendingFrame.sprite = themedPending != null ? themedPending : (blinkOn ? _art.SlotSelected : _art.Slot);
            _pendingIcon.sprite = _skills.Pending != null ? _skills.Pending.Icon : null;
            PlaceTopLeft(_pendingIcon, 6 + _slotFrames.Length * 23 + 6 + 2, 19);
        }

        float hold = _skills.DiscardHoldProgress;
        int w = Mathf.RoundToInt(hold * 20f);
        _discardBar.enabled = w > 0;
        if (w > 0)
        {
            _discardBar.transform.localScale = new Vector3(w, 2f, 1f);
            float bx = pending ? 6 + _slotFrames.Length * 23 + 6 : 6 + _skills.SelectedSlot * 23;
            PlaceScaledTopLeft(_discardBar, bx, 46);
        }
    }

    // 1ドットの絵を拡大して使うときの置き方（左上をそろえる）
    void PlaceScaledTopLeft(SpriteRenderer sr, float px, float py)
    {
        Vector2 origin = TopLeft();
        Vector3 s = sr.transform.localScale;
        Vector2 pivot = sr.sprite != null ? sr.sprite.pivot / sr.sprite.rect.size : new Vector2(0.5f, 0.5f);
        float x = origin.x + (px + s.x * pivot.x) / Ppu;
        float y = origin.y - (py + s.y * (1f - pivot.y)) / Ppu;
        sr.transform.localPosition = new Vector3(x, y, 0f);
    }

    void UpdateBoss()
    {
        BossController boss = FindBoss();
        EnemyWave wave = boss == null ? PreBossWave() : null;
        bool show = boss != null || wave != null;
        _bossRoot.gameObject.SetActive(show);
        if (!show) return;
        if (wave != null)
        {
            DrawPreBoss(wave);
            return;
        }

        _bossIcon.color = Color.white;
        _bossIcon.sprite = boss.Profile != null ? boss.Profile.Icon : null;
        PlaceTopRight(_bossIcon, 6 + 100 + 3, 5, 12);
        float ratio = boss.Gauge != null ? Mathf.Clamp01(boss.Gauge.Ratio) : 0f;
        int fillW = Mathf.RoundToInt(ratio * 96f);
        // ゲージの色：そのステージのアプリの色（4色なら左から4つに区切る）。形態変化中は赤く点滅、正常化で白
        AppTheme theme = _director != null && _director.Plan != null ? _director.Plan.App.Theme : null;
        Color[] colors = theme != null ? theme.Accents : new[] { GaugeColor };
        bool angry = boss.State == BossState.Transition && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        int n = Mathf.Clamp(colors.Length, 1, _gaugeFills.Length);
        float left = ViewWidthPx() - 6 - 100 + 2;
        for (int i = 0; i < _gaugeFills.Length; i++)
        {
            SpriteRenderer fill = _gaugeFills[i];
            int segStart = i * 96 / n, segEnd = (i + 1) * 96 / n;
            int w = i < n ? Mathf.Clamp(fillW - segStart, 0, segEnd - segStart) : 0;
            fill.enabled = w > 0;
            if (w <= 0) continue;
            float fillH = fill.sprite != null ? fill.sprite.rect.height : 4f;
            fill.transform.localScale = new Vector3(w, 4f / fillH, 1f);
            PlaceScaledTopLeft(fill, left + segStart, 9);
            bool done = boss.State == BossState.Normalized || boss.State == BossState.Ejected;
            fill.color = done ? GaugeDoneColor : angry ? GaugeAngryColor : colors[i];
        }

        float threshold = boss.Profile != null ? boss.Profile.Phase2Threshold : 0.5f;
        _gaugeMark.enabled = boss.State == BossState.Fighting || boss.State == BossState.Transition;
        _gaugeMark.transform.localScale = new Vector3(1f, 6f, 1f);
        PlaceScaledTopLeft(_gaugeMark, ViewWidthPx() - 6 - 100 + 2 + Mathf.Round(threshold * 96f), 8);
    }

    /// <summary>
    /// 雑魚を倒すとボスが出るステージ（1画面）で、ボスが出る前なら、その雑魚の集まり（2026-11-17）。
    /// 横スクロール・ウィンドウ移動（ボス部屋の入口でボスが出る）と、ボスが出たあとは null
    /// </summary>
    EnemyWave PreBossWave()
    {
        if (_director == null || _director.Plan == null || _director.Entrance == null || _director.Wave == null) return null;
        if (_director.Entrance.WaitsForGate || _director.Entrance.HasAppeared) return null;
        return _director.Wave;
    }

    /// <summary>
    /// ボスが出るまでのゲージ（文字なし）：右上に、これから出るボスのアイコンを暗い影で出し、雑魚を倒すごとにゲージがたまる。
    /// 満タンになると点滅し、ボスが出てきたら、そのままボスのゲージに切り替わる
    /// </summary>
    void DrawPreBoss(EnemyWave wave)
    {
        _bossIcon.sprite = _director.Plan.Profile != null ? _director.Plan.Profile.Icon : null;
        _bossIcon.color = PreBossIconColor;
        PlaceTopRight(_bossIcon, 6 + 100 + 3, 5, 12);
        int fillW = Mathf.RoundToInt(wave.Progress * 96f);
        float left = ViewWidthPx() - 6 - 100 + 2;
        bool blink = wave.IsCleared && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        for (int i = 0; i < _gaugeFills.Length; i++)
        {
            SpriteRenderer fill = _gaugeFills[i];
            fill.enabled = i == 0 && fillW > 0;
            if (!fill.enabled) continue;
            float fillH = fill.sprite != null ? fill.sprite.rect.height : 4f;
            fill.transform.localScale = new Vector3(fillW, 4f / fillH, 1f);
            PlaceScaledTopLeft(fill, left, 9);
            fill.color = blink ? Color.white : PreBossColor;
        }
        _gaugeMark.enabled = false;
    }

    BossController FindBoss()
    {
        if (_director != null)
        {
            if (_director.Boss == null || _director.Entrance == null || !_director.Entrance.HasAppeared) return null;
            return _director.Boss;
        }
        if (_boss != null && _boss.isActiveAndEnabled) return _boss;
        if (Time.unscaledTime < _nextBossSearch) return null;
        _nextBossSearch = Time.unscaledTime + 0.25f;
        _boss = null;
        foreach (var b in FindObjectsOfType<BossController>())
        {
            if (b.isActiveAndEnabled)
            {
                _boss = b;
                break;
            }
        }
        // 雑魚戦のテストシーン：ボスが登場するまでは出さない
        if (_boss != null)
        {
            var entrance = FindObjectOfType<BossEntrance>();
            if (entrance != null && !entrance.HasAppeared) return null;
        }
        return _boss;
    }
}
