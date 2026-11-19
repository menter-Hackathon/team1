using System.Collections.Generic;
using UnityEngine;

// 【仮】当たったエフェクト・スキンの絵で使う、小さな道具（2026-11-18。docs/app-skill-visuals-design.md 1-2〜1-4）。
// ・FxAnim：1回だけ出て消える絵（動き・重力・浮く・回る・ふくらむ・うすくなる・コマ送り）。スキンからも使ってよい
// ・FxKit：決まった数（強さ → かけらの数・絵の大きさ）、当たった絵を出す位置、かけら、字（黒いふちつき）、カメラの子
// ・FxArt：コードの中のドット絵（星・輪・四角・「！」の三角・チェック）。虹色の待ちカーソルは MacUi.BeachBall（呼び名は残す）
// 時間は Time.deltaTime（スケールあり）。ヒットストップの間は止まって見える（それでよい）

/// <summary>1回だけ出て消える絵。子の SpriteRenderer・TextMesh を全部まとめて、うすくする</summary>
public class FxAnim : MonoBehaviour
{
    float _life = 0.3f;
    float _age;
    Vector2 _velocity;
    float _gravity;
    float _rise;
    float _spin;
    bool _pop;
    float _scale = 1f;
    float _growTo = -1f;
    float _fadeFrom = 0.6f;
    float _delay;
    Sprite[] _frames;
    float _fps;
    bool _loopFrames = true;
    Vector3 _offset;
    SpriteRenderer _main;
    SpriteRenderer[] _srs;
    Color[] _srBase;
    TextMesh[] _tms;
    Color[] _tmBase;
    bool _ready;

    /// <summary>空の入れ物を作る（子に絵を足してから Refresh を呼ぶ）</summary>
    public static FxAnim Create(string objectName, Vector2 pos, float life)
    {
        var go = new GameObject(objectName);
        go.transform.position = pos;
        var a = go.AddComponent<FxAnim>();
        a._life = Mathf.Max(0.01f, life);
        return a;
    }

    /// <summary>絵を1枚出す（大きさは絵の元の大きさ × scale。1マス＝18ドット）</summary>
    public static FxAnim Sprite(Sprite sprite, Vector2 pos, int order, float life, Color? color = null, float scale = 1f, string objectName = "Fx")
    {
        FxAnim a = Create(objectName, pos, life);
        var sr = a.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = color ?? Color.white;
        a._main = sr;
        a._scale = scale;
        a.transform.localScale = new Vector3(scale, scale, 1f);
        return a;
    }

    public FxAnim Velocity(Vector2 v) { _velocity = v; return this; }
    public FxAnim Gravity(float g) { _gravity = g; return this; }
    /// <summary>寿命の間に、上へ r マス浮く</summary>
    public FxAnim Rise(float r) { _rise = r; return this; }
    public FxAnim Spin(float degreesPerSecond) { _spin = degreesPerSecond; return this; }
    /// <summary>出るとき 0 → 115% → 100%（0.1秒）。UI が「パッ」と出る感じ（0-2）</summary>
    public FxAnim Pop() { _pop = true; return this; }
    /// <summary>寿命の間に、大きさを to 倍まで変える（輪が広がるなど）</summary>
    public FxAnim Grow(float to) { _growTo = to; return this; }
    /// <summary>寿命のこの割合（0〜1）から、うすくなり始める。1 ならうすくしない</summary>
    public FxAnim FadeFrom(float t) { _fadeFrom = Mathf.Clamp01(t); return this; }
    /// <summary>出るまで待つ秒数（その間は見えない）</summary>
    public FxAnim Delay(float seconds)
    {
        _delay = Mathf.Max(0f, seconds);
        if (_delay > 0f && _ready) SetAlpha(0f); // 待つ間は、作ったフレームから見せない（2026-11-19）
        return this;
    }
    public FxAnim Frames(Sprite[] frames, float fps, bool loop = true)
    {
        _frames = frames;
        _fps = fps;
        _loopFrames = loop;
        return this;
    }
    public FxAnim Order(int order)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingOrder = order;
        return this;
    }
    public SpriteRenderer Main => _main;
    public float Life => _life;

    /// <summary>子の絵を集め直す（子を足したあとに呼ぶ）</summary>
    public FxAnim Refresh()
    {
        _srs = GetComponentsInChildren<SpriteRenderer>(true);
        _srBase = new Color[_srs.Length];
        for (int i = 0; i < _srs.Length; i++) _srBase[i] = _srs[i].color;
        _tms = GetComponentsInChildren<TextMesh>(true);
        _tmBase = new Color[_tms.Length];
        for (int i = 0; i < _tms.Length; i++) _tmBase[i] = FxKit.TextColor(_tms[i]);
        _ready = true;
        return this;
    }

    void Start()
    {
        if (!_ready) Refresh();
        if (_delay > 0f) SetAlpha(0f);
        else Apply();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (_delay > 0f)
        {
            _delay -= dt;
            SetAlpha(0f);
            return;
        }
        _age += dt;
        if (_age >= _life)
        {
            Destroy(gameObject);
            return;
        }
        _velocity.y -= _gravity * dt;
        _offset += (Vector3)(_velocity * dt);
        Apply();
    }

    void Apply()
    {
        if (!_ready) return;
        float t = Mathf.Clamp01(_age / _life);
        if (_delay > 0f) return;
        Vector3 basePos = transform.position - _lastOffset;
        Vector3 offset = _offset + Vector3.up * (_rise * t);
        transform.position = basePos + offset;
        _lastOffset = offset;

        float s = _scale;
        if (_growTo > 0f) s *= Mathf.Lerp(1f, _growTo, 1f - (1f - t) * (1f - t));
        if (_pop)
        {
            float p = _age / 0.1f;
            s *= p < 0.6f ? Mathf.Lerp(0f, 1.15f, p / 0.6f) : p < 1f ? Mathf.Lerp(1.15f, 1f, (p - 0.6f) / 0.4f) : 1f;
        }
        transform.localScale = new Vector3(s, s, 1f);
        if (_spin != 0f) transform.rotation = Quaternion.Euler(0f, 0f, _spin * _age);
        if (_frames != null && _frames.Length > 0 && _main != null)
        {
            int f = Mathf.FloorToInt(_age * _fps);
            _main.sprite = _frames[_loopFrames ? f % _frames.Length : Mathf.Min(f, _frames.Length - 1)];
        }
        float a = t <= _fadeFrom || _fadeFrom >= 1f ? 1f : 1f - (t - _fadeFrom) / (1f - _fadeFrom);
        SetAlpha(a);
    }

    Vector3 _lastOffset;

    void SetAlpha(float a)
    {
        if (!_ready) return;
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srBase[i];
            c.a *= a;
            _srs[i].color = c;
        }
        for (int i = 0; i < _tms.Length; i++)
        {
            if (_tms[i] == null) continue;
            Color c = _tmBase[i];
            c.a *= a;
            WorldText.SetColor(_tms[i], c);
        }
    }
}

/// <summary>【仮】当たったエフェクトの道具（2026-11-18）</summary>
public static class FxKit
{
    public const float Pixel = 1f / 18f;

    // 重なる順番（1-6 の表）
    public const int OrderHitPicture = 47;
    public const int OrderFragment = 48;
    public const int OrderRing = 49;
    public const int OrderToast = 70;
    public const int OrderScreen = 80;

    // 共通の色（1-6 の表）
    public static readonly Color EnemyRed = RuntimeSprites.Hex("FF453A");
    public static readonly Color MeleeYellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color VirusBlack = RuntimeSprites.Hex("101014");
    public static readonly Color NotifyBg = RuntimeSprites.Hex("1C1C1E");

    /// <summary>強さ → かけらの数（1-2 の表。小は1個）</summary>
    public static int FragmentCount(HitStrength s)
    {
        switch (s)
        {
            case HitStrength.Small: return 1;
            case HitStrength.Light: return 3;
            case HitStrength.Heavy: return 6;
            case HitStrength.Finisher: return 12;
            case HitStrength.Hurt: return 4;
            case HitStrength.Down: return 8;
            default: return 3;
        }
    }

    /// <summary>強さ → 当たった絵の大きさの倍率（小 0＝絵を出さない、とどめ 1.5、ダウン 2）</summary>
    public static float PictureScale(HitStrength s)
    {
        switch (s)
        {
            case HitStrength.Small: return 0f;
            case HitStrength.Finisher: return 1.5f;
            case HitStrength.Down: return 2f;
            default: return 1f;
        }
    }

    /// <summary>大きい当たった絵を使うか（重い・とどめ）</summary>
    public static bool IsBig(HitStrength s) => s == HitStrength.Heavy || s == HitStrength.Finisher;

    /// <summary>当たった絵の寿命（軽い 0.2秒、重い 0.3秒、とどめ 0.45秒、被弾 0.4秒）</summary>
    public static float PictureLife(HitStrength s)
    {
        switch (s)
        {
            case HitStrength.Light: return 0.2f;
            case HitStrength.Heavy: return 0.3f;
            case HitStrength.Finisher: return 0.45f;
            case HitStrength.Hurt: return 0.4f;
            case HitStrength.Down: return 0.9f;
            default: return 0.2f;
        }
    }

    /// <summary>
    /// 当たった絵を出す位置：当たった位置から外へ 0.3マス（ボスの顔・弾をかくさない。1-2）。
    /// 相手がプレイヤーなら、頭の上 0.6マス。ダウンは画面のまん中寄り
    /// </summary>
    public static Vector2 PictureAnchor(HitInfo info)
    {
        if (info.TargetIsPlayer)
        {
            Vector2 head = HeadOf(info.Target, info.Point) + Vector2.up * 0.6f;
            if (info.Strength == HitStrength.Down)
            {
                Rect v = ScreenUi.ViewRect();
                head = Vector2.Lerp(head, v.center + Vector2.up * 1f, 0.5f);
            }
            return ClampToView(head, 1.2f);
        }
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction.normalized : Vector2.right;
        return info.Point - dir * 0.3f;
    }

    /// <summary>体の上の端（移動用の当たり判定。無ければ足元 + 1マス）</summary>
    public static Vector2 HeadOf(GameObject target, Vector2 fallback)
    {
        if (target == null) return fallback;
        foreach (var col in target.GetComponentsInChildren<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return new Vector2(col.bounds.center.x, col.bounds.max.y);
        }
        return (Vector2)target.transform.position + Vector2.up * 1f;
    }

    /// <summary>画面の中に収める（margin マス内側）</summary>
    public static Vector2 ClampToView(Vector2 p, float margin)
    {
        Rect v = ScreenUi.ViewRect();
        return new Vector2(Mathf.Clamp(p.x, v.xMin + margin, v.xMax - margin), Mathf.Clamp(p.y, v.yMin + margin * 0.5f, v.yMax - margin));
    }

    /// <summary>
    /// かけらを飛ばす（向きの先へ扇状 ±40°、速さ 6〜10マス/秒、重力あり、0.3〜0.5秒で消える。1-2）。
    /// sprites か colors のどちらかを入れる（両方なら、絵を色で塗る）
    /// </summary>
    public static void Fragments(Vector2 point, Vector2 direction, int count, Sprite[] sprites, Color[] colors, float scale = 1f)
    {
        if (count <= 0) return;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-40f, 40f);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, angle) * dir) * Random.Range(6f, 10f);
            v.y += 2f; // 少し上へはねる
            Sprite s = sprites != null && sprites.Length > 0 ? sprites[i % sprites.Length] : FxArt.Square(3);
            Color c = colors != null && colors.Length > 0 ? colors[i % colors.Length] : Color.white;
            FxAnim.Sprite(s, point, OrderFragment, Random.Range(0.3f, 0.5f), c, scale, "Fragment")
                .Velocity(v).Gravity(28f).Spin(Random.Range(-540f, 540f)).FadeFrom(0.5f);
        }
    }

    /// <summary>ウイルスの黒いドット（はがれる。1-3・1-4）</summary>
    public static void VirusDots(Vector2 point, Vector2 direction, int count)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-70f, 70f);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, angle) * dir) * Random.Range(4f, 8f);
            v.y += 3f;
            FxAnim.Sprite(FxArt.Square(Random.Range(2, 4)), point, OrderFragment, Random.Range(0.35f, 0.55f), VirusBlack, 1f, "VirusDot")
                .Velocity(v).Gravity(24f).FadeFrom(0.6f);
        }
    }

    /// <summary>白い輪が広がる（直径 0 → diameter マス、seconds 秒。1-2 の「重い」）</summary>
    public static void Ring(Vector2 point, float diameter, float seconds, Color color, float delay = 0f)
    {
        Sprite[] frames = FxArt.RingFrames(Mathf.Max(4, Mathf.RoundToInt(diameter * 9f)));
        FxAnim.Sprite(frames[0], point, OrderRing, seconds + 0.03f, color, 1f, "Ring")
            .Frames(frames, frames.Length / Mathf.Max(0.01f, seconds), false).FadeFrom(0.55f).Delay(delay);
    }

    /// <summary>
    /// 字を出す（黒いふち 1ドットつき。英字は Press Start 2P）。height は字の高さ（マス。0.4 以上）。
    /// 寿命・動きは返した FxAnim で決める（Rise・Pop など）
    /// </summary>
    public static FxAnim Text(string text, Vector2 pos, float height, Color color, int order, float life, Color? outline = null, bool english = true, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        FxAnim a = FxAnim.Create("FxText", pos, life);
        AddText(a.transform, text, Vector2.zero, height, color, order, outline, english, anchor);
        return a.Refresh();
    }

    /// <summary>親の下に、ふちつきの字を足す（入れ物の FxAnim は呼んだ側が Refresh する）</summary>
    public static void AddText(Transform parent, string text, Vector2 localPos, float height, Color color, int order, Color? outline = null, bool english = true, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        Color o = outline ?? new Color(0f, 0f, 0f, 1f);
        if (o.a > 0f)
        {
            Vector2[] offsets = { new Vector2(-Pixel, 0f), new Vector2(Pixel, 0f), new Vector2(0f, -Pixel), new Vector2(0f, Pixel) };
            foreach (var off in offsets)
            {
                TextMesh s = WorldText.Create(parent, text, height, o, order, english, anchor, "Outline");
                s.transform.localPosition = localPos + off;
            }
        }
        TextMesh tm = WorldText.Create(parent, text, height, color, order, english, anchor, "Text");
        tm.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
    }

    /// <summary>字の幅のめやす（マス）。Press Start 2P は1字がほぼ正方形</summary>
    public static float TextWidth(string text, float height) => (text != null ? text.Length : 0) * height;

    /// <summary>TextMesh の色（WorldText.SetColor で直す前の色）。直した色から戻す</summary>
    public static Color TextColor(TextMesh tm)
    {
        if (tm == null) return Color.white;
        Color c = tm.color;
        // WorldText.SetColor はリニアの色空間用に色を直している（PixelGui.ToGui）。元に戻す
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.gamma;
        return c;
    }

    /// <summary>四角を1枚出す（線・板。大きさはマス）。親の子にする</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localPos, Vector2 size, Color color, int order, string objectName = "Box")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, PlaceholderSprites.Square, color, size, order, objectName);
        sr.transform.localPosition = localPos;
        return sr;
    }

    /// <summary>カメラの子の入れ物（画面に貼り付く絵。自動の撮影に写る）。カメラが無ければ null</summary>
    public static Transform CameraChild(string objectName)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        var go = new GameObject(objectName);
        go.layer = cam.gameObject.layer;
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(1f, -cam.transform.position.z));
        return go.transform;
    }

    /// <summary>カメラに映っている大きさ（マス）。カメラが無ければ 21.3×12</summary>
    public static Vector2 ViewSize()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return new Vector2(21.33f, 12f);
        return new Vector2(cam.orthographicSize * 2f * cam.aspect, cam.orthographicSize * 2f);
    }

    /// <summary>アプリの色（無ければ fallback）</summary>
    public static Color MainOf(AppTheme theme, Color fallback) => theme != null ? theme.Main : fallback;

    /// <summary>アプリの差し色（3色まで。無ければ代表の色と、その明るい色・暗い色）</summary>
    public static Color[] AccentsOf(AppTheme theme, Color fallback)
    {
        Color main = MainOf(theme, fallback);
        Color[] acc = theme != null ? theme.Accents : null;
        var list = new List<Color> { main };
        if (acc != null) foreach (var c in acc) if (list.Count < 3 && !Similar(c, main)) list.Add(c);
        if (list.Count < 2) list.Add(Color.Lerp(main, Color.white, 0.45f));
        if (list.Count < 3) list.Add(Color.Lerp(main, Color.black, 0.3f));
        for (int i = 0; i < list.Count; i++) { Color c = list[i]; c.a = 1f; list[i] = c; }
        return list.ToArray();
    }

    static bool Similar(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.15f;
}

/// <summary>【仮】コードの中のドット絵（2026-11-18）。色はどれも白（SpriteRenderer の色で塗る）か、決まった色</summary>
public static class FxArt
{
    static readonly Dictionary<char, Color32> White = new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } };
    static readonly Dictionary<int, Sprite> Squares = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite[]> Rings = new Dictionary<int, Sprite[]>();

    /// <summary>白い四角（n×n ドット）</summary>
    public static Sprite Square(int n)
    {
        n = Mathf.Clamp(n, 1, 32);
        if (Squares.TryGetValue(n, out Sprite s) && s != null) return s;
        var rows = new string[n];
        for (int i = 0; i < n; i++) rows[i] = new string('W', n);
        s = RuntimeSprites.FromAscii($"Fx_Square{n}", rows, White);
        Squares[n] = s;
        return s;
    }

    /// <summary>白い長方形（w×h ドット）</summary>
    public static Sprite Rect(int w, int h)
    {
        w = Mathf.Clamp(w, 1, 64);
        h = Mathf.Clamp(h, 1, 64);
        var rows = new string[h];
        for (int i = 0; i < h; i++) rows[i] = new string('W', w);
        return RuntimeSprites.FromAscii($"Fx_Rect{w}x{h}", rows, White);
    }

    /// <summary>白い4方向の星（11ドット。まん中が少し黄色い。近接の当たった絵 1-4）</summary>
    public static Sprite Star4 => RuntimeSprites.FromAscii("Fx_Star4", new[]
    {
        ".....W.....",
        ".....W.....",
        "....WWW....",
        "....WYW....",
        "...WWYWW...",
        "WWWWYYYWWWW",
        "...WWYWW...",
        "....WYW....",
        "....WWW....",
        ".....W.....",
        ".....W.....",
    }, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) }, { 'Y', RuntimeSprites.Hex("FFF3B0") } });

    /// <summary>小さいきらめき（7ドット、白）</summary>
    public static Sprite Twinkle => RuntimeSprites.FromAscii("Fx_Twinkle", new[]
    {
        "...W...",
        "...W...",
        "..WWW..",
        "WWWWWWW",
        "..WWW..",
        "...W...",
        "...W...",
    }, White);

    /// <summary>赤い「！」の三角（12ドット。雑魚＝ウイルスの体当たり 1-4）</summary>
    public static Sprite WarningTriangle => RuntimeSprites.FromAscii("Fx_WarnTriangle", new[]
    {
        ".....RR.....",
        "....RRRR....",
        "....RWWR....",
        "...RRWWRR...",
        "...RRWWRR...",
        "..RRRWWRRR..",
        "..RRRWWRRR..",
        ".RRRRRRRRRR.",
        ".RRRRWWRRRR.",
        "RRRRRWWRRRRR",
        "RRRRRRRRRRRR",
        "DDDDDDDDDDDD",
    }, new Dictionary<char, Color32> { { 'R', RuntimeSprites.Hex("FF453A") }, { 'W', RuntimeSprites.Hex("FFFFFF") }, { 'D', RuntimeSprites.Hex("8A1F18") } });

    /// <summary>チェック（白。9×7ドット）</summary>
    public static Sprite Check => RuntimeSprites.FromAscii("Fx_Check", new[]
    {
        "........W",
        ".......WW",
        "W.....WW.",
        "WW...WW..",
        ".WW.WW...",
        "..WWW....",
        "...W.....",
    }, White);

    /// <summary>白い円（d ドット）</summary>
    public static Sprite Disc(int d)
    {
        d = Mathf.Clamp(d, 2, 48);
        string key = $"Fx_Disc{d}";
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                line[x] = dx * dx + dy * dy <= r * r ? 'W' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, White);
    }

    /// <summary>白い輪のコマ（半径 2 → maxRadius ドット。太さ 2ドット → 1ドット）。広がる輪に使う</summary>
    public static Sprite[] RingFrames(int maxRadius)
    {
        maxRadius = Mathf.Clamp(maxRadius, 3, 40);
        if (Rings.TryGetValue(maxRadius, out Sprite[] cached) && cached != null && cached.Length > 0 && cached[0] != null) return cached;
        const int frames = 7;
        var list = new Sprite[frames];
        for (int f = 0; f < frames; f++)
        {
            float k = (f + 1) / (float)frames;
            float radius = Mathf.Lerp(2f, maxRadius, 1f - (1f - k) * (1f - k));
            float thick = f < frames - 2 ? 2f : 1.2f;
            int d = Mathf.CeilToInt(radius * 2f) + 2;
            var rows = new string[d];
            float c = d * 0.5f;
            for (int y = 0; y < d; y++)
            {
                var line = new char[d];
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    line[x] = dist <= radius && dist >= radius - thick ? 'W' : '.';
                }
                rows[y] = new string(line);
            }
            list[f] = RuntimeSprites.FromAscii($"Fx_Ring{maxRadius}_{f}", rows, White);
        }
        Rings[maxRadius] = list;
        return list;
    }

    /// <summary>
    /// 虹色の待ちカーソル（16ドットの円。6色の扇形。8コマで1回転。10章のエラーの絵）。
    /// 2026-11-19：MacUi.BeachBall へ移した（docs/mac-look-design.md 2章 6）。呼び名は残す
    /// </summary>
    public static Sprite[] BeachBall => MacUi.BeachBall();
}
