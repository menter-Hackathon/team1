using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Terminal のスキン（ラスボス。2026-11-18。docs/app-skill-visuals-design.md 13章。担当1）。
/// ・エラーの絵（プレイヤーが受けた）：赤い字が頭の上に1行。技ごとに決まった字（エラー弾 ERR!、rm -rf No such file、文字列の雨・fork zsh: killed、体当たり zsh: segmentation fault）
/// ・ダウン：赤い字 Connection closed.
/// ・プレイヤーの攻撃が Terminal のボスに当たった：攻撃のスキンの当たった絵に足して、ボスの横に緑の zsh のジョブの行 [1] + done を積む（4行まで。重いときは [1] + killed）
/// 2026-11-19【仮】Mac の言い方にした（docs/mac-look-design.md 2章 8・9。Linux の起動の行 [ OK ] → zsh のジョブの行、エラーは zsh: の小文字）
/// ・とどめ：画面のまん中より上に、灰色の字 [Process completed] が1字ずつ打たれる。隅の通知は出さない（NotificationTitle は null）
/// ・枠：エラー弾（長弾）は黒い地に赤い字の札 ERR!（後ろに四角いカーソルの残像 2個）。着地の赤い波線は、赤い ^^^^^^（コンパイラのエラーの印）
///   ほかの枠（瞬間移動など）は Generic に任せる
/// 色は今の色（緑 #33FF66・赤 #FF453A・白・灰 #C8C8C8・黒）
/// </summary>
public class TerminalSkin : AppSkin
{
    public override SkinId Id => SkinId.Terminal;
    public override string NotificationTitle => null;

    static readonly Color Green = RuntimeSprites.Hex("33FF66");
    static readonly Color Red = RuntimeSprites.Hex("FF453A");
    static readonly Color Gray = RuntimeSprites.Hex("C8C8C8");

    // ───────── 枠 ─────────

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        switch (frame)
        {
            case AttackFrame.LongShot: return DressErrTag(phase, info);
            case AttackFrame.Floor: return DressCarets(phase, info);
            default: return false;
        }
    }

    /// <summary>エラー弾：黒い地に赤い字の札 ERR!（22×9ドット）。後ろに四角いカーソルの残像 2個。字は逆さにしない</summary>
    static bool DressErrTag(DressPhase phase, DressInfo info)
    {
        switch (phase)
        {
            case DressPhase.Telegraph:
                return false; // 予備動作は今のまま
            case DressPhase.Active:
            {
                var root = new GameObject("TerminalErrTag");
                root.layer = info.Target.layer;
                root.transform.SetParent(info.Target.transform, false);
                root.AddComponent<TerminalUpright>();
                RuntimeSprites.Show(root.transform, ErrTag, Vector2.zero, 21, 1f, "Tag");
                // カーソルの残像（進む向きの反対へ。うすく）
                var trail = new GameObject("Trail");
                trail.layer = root.layer;
                trail.transform.SetParent(root.transform, false);
                float px = FxKit.Pixel;
                for (int i = 0; i < 2; i++)
                {
                    SpriteRenderer g = RuntimeSprites.Show(trail.transform, FxArt.Rect(3, 5), new Vector2(-(13f + i * 5f) * px, 0f), 20, 1f, "CursorGhost");
                    g.color = new Color(1f, 1f, 1f, i == 0 ? 0.5f : 0.22f);
                }
                root.GetComponent<TerminalUpright>().Trail = trail.transform;
                return true;
            }
            case DressPhase.Landed:
                return true; // 波線（^^^^^^）は追撃の床が出す
            case DressPhase.Vanish:
            {
                // 札がはじけて、赤い 0・1 が散る
                Vector2 p = info.Target.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 v = new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 5f));
                    FxAnim a = FxKit.Text(i % 2 == 0 ? "0" : "1", p, 0.4f, Red, 22, 0.35f, null).Velocity(v).Gravity(18f).FadeFrom(0.4f);
                    SkinDresser.Adopt(info, a.gameObject);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>赤い波線 → 赤い ^^^^^^（コンパイラのエラーの印）。面に沿って、下の端に並べる</summary>
    static bool DressCarets(DressPhase phase, DressInfo info)
    {
        switch (phase)
        {
            case DressPhase.Telegraph:
                return false;
            case DressPhase.Active:
            {
                int w = Mathf.Max(6, Mathf.RoundToInt(info.HitSize.x * 18f));
                var root = new GameObject("TerminalCarets");
                root.layer = info.Target.layer;
                root.transform.SetParent(info.Target.transform, false);
                root.transform.localPosition = new Vector3(0f, -info.HitSize.y * 0.5f + 2.5f * FxKit.Pixel, 0f);
                SpriteRenderer sr = RuntimeSprites.Show(root.transform, Carets(w), Vector2.zero, 18, 1f, "Carets");
                sr.color = Red;
                // その上に、うすい灰色の「エラーの行」（棒）
                SpriteRenderer line = FxKit.Box(root.transform, new Vector2(-info.HitSize.x * 0.1f, 4.5f * FxKit.Pixel), new Vector2(info.HitSize.x * 0.6f, FxKit.Pixel * 2f), new Color(0.78f, 0.78f, 0.78f, 0.35f), 17, "Line");
                var blink = root.AddComponent<TerminalTickBlink>();
                blink.Setup(sr, info.Data != null && info.Data.PersistDuration > 0f ? info.Data.TickInterval : 0f);
                return true;
            }
            case DressPhase.Vanish:
            {
                // 灰色になって、うすくなって消える
                int w = Mathf.Max(6, Mathf.RoundToInt(info.HitSize.x * 18f));
                Vector3 pos = info.Target.transform.TransformPoint(new Vector3(0f, -info.HitSize.y * 0.5f + 2.5f * FxKit.Pixel, 0f));
                FxAnim a = FxAnim.Sprite(Carets(w), pos, 18, 0.25f, Gray, 1f, "CaretsFade").FadeFrom(0f);
                a.transform.rotation = info.Target.transform.rotation;
                SkinDresser.Adopt(info, a.gameObject);
                return true;
            }
        }
        return false;
    }

    static readonly Dictionary<int, Sprite> CaretCache = new Dictionary<int, Sprite>();

    /// <summary>^ を横に並べた絵（高さ 3ドット、幅 w ドット。^ は 5ドットおき）</summary>
    static Sprite Carets(int w)
    {
        if (CaretCache.TryGetValue(w, out Sprite s) && s != null) return s;
        string[] pattern = { "..W...", ".W.W..", "W...W." };
        var rows = new string[3];
        for (int y = 0; y < 3; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++) line[x] = pattern[y][x % 6];
            rows[y] = new string(line);
        }
        s = RuntimeSprites.FromAscii($"Terminal_Carets{w}", rows, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } });
        CaretCache[w] = s;
        return s;
    }

    /// <summary>ERR! の札（22×9ドット。赤いふち、黒い地、赤い字、白い四角のカーソル）</summary>
    static Sprite ErrTag => RuntimeSprites.FromAscii("Terminal_ErrTag", new[]
    {
        "rrrrrrrrrrrrrrrrrrrrrr",
        "rKKKKKKKKKKKKKKKKKKKKr",
        "rKKRRRKRRKKRRKKRKwwwKr",
        "rKKRKKKRKRKRKRKRKwwwKr",
        "rKKRRKKRRKKRRKKRKwwwKr",
        "rKKRKKKRKRKRKRKKKwwwKr",
        "rKKRRRKRKRKRKRKRKwwwKr",
        "rKKKKKKKKKKKKKKKKKKKKr",
        "rrrrrrrrrrrrrrrrrrrrrr",
    }, new Dictionary<char, Color32>
    {
        { 'r', RuntimeSprites.Hex("FF453A") },
        { 'K', RuntimeSprites.Hex("0C0C0C") },
        { 'R', RuntimeSprites.Hex("FF6B61") },
        { 'w', RuntimeSprites.Hex("F2F2F7") },
    });

    // ───────── 当たった絵・エラーの絵 ─────────

    public override bool Hit(HitInfo info) => false; // Terminal の攻撃が敵に当たることは無い（Generic に任せる）

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        string text = down ? "Connection closed." : ErrorText(info.ErrorKind);
        float h = down ? 0.5f : 0.4f;
        Vector2 p = FxKit.PictureAnchor(info);
        float half = FxKit.TextWidth(text, h) * 0.5f;
        p = FxKit.ClampToView(p, half + 0.3f);
        FxAnim a = FxAnim.Create("TerminalError", p, down ? 1.4f : 0.75f);
        FxKit.AddText(a.transform, text, new Vector2(-half, 0f), h, Red, FxKit.OrderHitPicture, Color.black, true, TextAnchor.MiddleLeft);
        // 字の後ろに、白い四角のカーソル
        SpriteRenderer cursor = FxKit.Box(a.transform, new Vector2(-half, 0f), new Vector2(h * 0.55f, h), Color.white, FxKit.OrderHitPicture, "Cursor");
        a.gameObject.AddComponent<TerminalTyping>().Setup(text, down ? 0.45f : 0.18f, -half, h, cursor);
        a.Refresh().Rise(down ? 0f : 0.2f).FadeFrom(0.75f);
        return true;
    }

    /// <summary>技ごとの字（13-1）。種類は攻撃データの名前から（SkinTables.ErrorKindOf）</summary>
    public static string ErrorText(string kind)
    {
        switch (kind)
        {
            case "RmRf": return "No such file";
            case "StringRain":
            case "Fork": return "zsh: killed";
            case SkinTables.Kinds.Contact: return "zsh: segmentation fault";
            case "Chmod":
            case "DenyPillar": return "zsh: permission denied"; // 【仮】13-1 に無いので足した
            default: return "ERR!";
        }
    }

    // ───────── Terminal のボスに当てたとき（ジョブの行 [1] + done を積む） ─────────

    /// <summary>プレイヤーの攻撃が Terminal のボスに当たった（HitFx が呼ぶ）</summary>
    public static void OnBossHit(BossController boss, HitInfo info)
    {
        if (boss == null) return;
        // 形態変化・追い出しを起こした一撃は、行を消したあとに来るので積まない（戦っている間だけ）
        if (info.Strength != HitStrength.Small && boss.State == BossState.Fighting)
        {
            TerminalOkStack stack = boss.GetComponent<TerminalOkStack>();
            if (stack == null) stack = boss.gameObject.AddComponent<TerminalOkStack>();
            stack.Push(FxKit.IsBig(info.Strength) ? TerminalOkStack.KilledLine : TerminalOkStack.DoneLine);
        }
        // かけら：緑の 0・1 と、白い四角のカーソル
        int n = info.Strength == HitStrength.Small ? 1 : info.Strength == HitStrength.Light ? 2 : 4;
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction.normalized : Vector2.up;
        for (int i = 0; i < n; i++)
        {
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f)) * dir) * Random.Range(4f, 7f) + Vector2.up * 2.5f;
            FxKit.Text(Random.value < 0.5f ? "0" : "1", info.Point, 0.4f, Green, FxKit.OrderFragment, Random.Range(0.3f, 0.45f), new Color(0f, 0f, 0f, 0f))
                .Velocity(v).Gravity(20f).FadeFrom(0.5f);
        }
        if (n >= 2)
        {
            FxAnim.Sprite(FxArt.Rect(3, 5), info.Point, FxKit.OrderFragment, 0.35f, Color.white, 1f, "CursorBit")
                .Velocity(new Vector2(-dir.x * 3f, 5f)).Gravity(18f).FadeFrom(0.5f);
        }
    }

    /// <summary>とどめ：画面のまん中より上に、灰色の字 [Process completed] が1字ずつ打たれる（0.4秒）</summary>
    public static void ProcessCompleted()
    {
        Transform root = FxKit.CameraChild("TerminalProcessCompleted");
        if (root == null) return;
        const string text = "[Process completed]";
        const float h = 0.5f;
        Vector2 view = FxKit.ViewSize();
        float half = FxKit.TextWidth(text, h) * 0.5f;
        var lineRoot = new GameObject("Line").transform;
        lineRoot.SetParent(root, false);
        lineRoot.localPosition = new Vector3(0f, view.y * 0.18f, 0f);
        FxKit.AddText(lineRoot, text, new Vector2(-half, 0f), h, Gray, FxKit.OrderToast + 2, Color.black, true, TextAnchor.MiddleLeft);
        SpriteRenderer cursor = FxKit.Box(lineRoot, new Vector2(-half, 0f), new Vector2(h * 0.55f, h), Gray, FxKit.OrderToast + 2, "Cursor");
        lineRoot.gameObject.AddComponent<TerminalTyping>().Setup(text, 0.4f, -half, h, cursor);
        root.gameObject.AddComponent<ScreenFxLife>().Seconds = 2.8f;
    }
}

/// <summary>【仮】弾の子の絵を、逆さにならないように立てておく（弾は進む向きに回るため）。残像は進む向きの反対へ</summary>
public class TerminalUpright : MonoBehaviour
{
    public Transform Trail;

    void LateUpdate()
    {
        Transform parent = transform.parent;
        if (parent == null) return;
        float angle = parent.eulerAngles.z;
        transform.rotation = Quaternion.identity;
        // 左へ進むときは、残像を右へ（札の字は左右反転しない）
        bool left = angle > 90f && angle < 270f;
        if (Trail != null) Trail.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
    }
}

/// <summary>【仮】残り続ける床の判定の瞬間（ヒット間隔ごと）に、絵を白く光らせる</summary>
public class TerminalTickBlink : MonoBehaviour
{
    SpriteRenderer _sr;
    Color _base;
    float _interval;
    float _age;

    public void Setup(SpriteRenderer sr, float interval)
    {
        _sr = sr;
        _base = sr != null ? sr.color : Color.white;
        _interval = interval;
    }

    void Update()
    {
        if (_sr == null) return;
        _age += Time.deltaTime;
        bool flash = _age < 0.06f || (_interval > 0.05f && Mathf.Repeat(_age, _interval) < 0.06f);
        _sr.color = flash ? Color.white : _base;
    }
}

/// <summary>【仮】字を1字ずつ打つ（ふちの字もいっしょに）。後ろに四角いカーソル（点滅）</summary>
public class TerminalTyping : MonoBehaviour
{
    string _text;
    float _time;
    float _left;
    float _height;
    SpriteRenderer _cursor;
    TextMesh[] _tms;
    float _age;
    int _shown = -1;

    public void Setup(string text, float typeTime, float left, float height, SpriteRenderer cursor)
    {
        _text = text ?? "";
        _time = Mathf.Max(0.01f, typeTime);
        _left = left;
        _height = height;
        _cursor = cursor;
        _tms = GetComponentsInChildren<TextMesh>(true);
        Apply(0);
    }

    void Update()
    {
        _age += Time.deltaTime;
        int n = Mathf.Clamp(Mathf.CeilToInt(_text.Length * _age / _time), 0, _text.Length);
        if (n != _shown) Apply(n);
        if (_cursor != null)
        {
            Vector3 p = _cursor.transform.localPosition;
            p.x = _left + FxKit.TextWidth(_text.Substring(0, n), _height) + _height * 0.35f;
            _cursor.transform.localPosition = p;
            // 打ち終わったら点滅
            bool on = n < _text.Length || Mathf.FloorToInt(_age / 0.25f) % 2 == 0;
            _cursor.enabled = on;
        }
    }

    void Apply(int n)
    {
        _shown = n;
        string s = _text.Substring(0, n);
        foreach (var tm in _tms) if (tm != null) tm.text = s;
    }
}

/// <summary>
/// 【仮】Terminal のボスの横に積む緑の行（13-1。2026-11-19：[ OK ] → zsh のジョブの行 [1] + done・[1] + killed）。当てるたびに下に足し、上へ押し上げる（4行まで。古い行から消える）。
/// 形態変化・追い出し・正常化・やり直し（ゲージが 0 に戻った）で、全部消す
/// </summary>
public class TerminalOkStack : MonoBehaviour
{
    const int MaxLines = 4;
    const float LineHeight = 0.4f;
    const float LineStep = 0.5f;
    const float LineLife = 2.6f;
    static readonly Color Green = RuntimeSprites.Hex("33FF66");
    /// <summary>軽い当たり・重い当たりの行（zsh のジョブの行）【仮】</summary>
    public const string DoneLine = "[1] + done";
    public const string KilledLine = "[1] + killed";

    class Line
    {
        public Transform Root;
        public TextMesh[] Texts;
        public float Born;
    }

    BossController _boss;
    VirusGauge _gauge;
    Transform _root;
    Vector2 _bodyOffset = new Vector2(0f, 1.5f); // 体の中心の、ボスの位置からのずれ（被弾範囲が切れている間は、覚えた値）
    Vector2 _bodySize = new Vector2(4f, 3f);
    readonly List<Line> _lines = new List<Line>();
    float _side = 1f;

    void Awake()
    {
        _boss = GetComponent<BossController>();
        _gauge = GetComponent<VirusGauge>();
    }

    void OnEnable()
    {
        if (_boss != null)
        {
            _boss.TransitionStarted += Clear;
            _boss.Ejected += Clear;
            _boss.Normalized += OnNormalized;
        }
        if (_gauge != null) _gauge.Changed += OnGauge;
    }

    void OnDisable()
    {
        if (_boss != null)
        {
            _boss.TransitionStarted -= Clear;
            _boss.Ejected -= Clear;
            _boss.Normalized -= OnNormalized;
        }
        if (_gauge != null) _gauge.Changed -= OnGauge;
        Clear();
    }

    void OnNormalized(SkillDefinition _) => Clear();
    void OnGauge(int current, int max)
    {
        if (current <= 0) Clear();
    }

    public void Push(string text)
    {
        if (_root == null)
        {
            _root = new GameObject("TerminalOkStack").transform;
            _root.SetParent(transform, false);
        }
        if (_lines.Count == 0) _side = PickSide();
        var go = new GameObject("OkLine");
        go.transform.SetParent(_root, false);
        FxKit.AddText(go.transform, text, Vector2.zero, LineHeight, Green, 52, Color.black, true, _side > 0f ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
        _lines.Add(new Line { Root = go.transform, Texts = go.GetComponentsInChildren<TextMesh>(), Born = Time.time });
        while (_lines.Count > MaxLines)
        {
            if (_lines[0].Root != null) Destroy(_lines[0].Root.gameObject);
            _lines.RemoveAt(0);
        }
        Layout();
    }

    /// <summary>行を出す側：プレイヤーと反対側（当たった絵・弾をかくさない）。画面に入らなければ逆へ</summary>
    /// <summary>体の範囲（被弾範囲が切れている間＝瞬間移動・演出中は、最後に取れた範囲を今の位置へ動かした物）</summary>
    Bounds Body()
    {
        if (_boss != null)
        {
            Bounds b = _boss.BodyBounds;
            if (b.size.sqrMagnitude > 0.05f)
            {
                _bodyOffset = (Vector2)(b.center - transform.position);
                _bodySize = b.size;
            }
        }
        return new Bounds((Vector2)transform.position + _bodyOffset, _bodySize);
    }

    float PickSide()
    {
        Bounds b = Body();
        float side = 1f;
        if (_boss != null && _boss.Runner != null && _boss.Runner.Target != null) side = _boss.Runner.Target.position.x >= b.center.x ? -1f : 1f;
        Rect view = ScreenUi.ViewRect();
        float need = FxKit.TextWidth(KilledLine, LineHeight) + 0.3f;
        if (side > 0f && b.max.x + need > view.xMax) side = -1f;
        else if (side < 0f && b.min.x - need < view.xMin) side = 1f;
        return side;
    }

    void Layout()
    {
        if (_root == null || _boss == null) return;
        Bounds b = Body();
        float x = (_side > 0f ? b.max.x + 0.25f : b.min.x - 0.25f) - transform.position.x;
        float baseY = b.center.y - transform.position.y - 0.6f;
        for (int i = 0; i < _lines.Count; i++)
        {
            int fromBottom = _lines.Count - 1 - i;
            if (_lines[i].Root != null) _lines[i].Root.localPosition = new Vector3(x, baseY + fromBottom * LineStep, 0f);
        }
    }

    void LateUpdate()
    {
        if (_lines.Count == 0) return;
        float now = Time.time;
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            Line l = _lines[i];
            float age = now - l.Born;
            if (l.Root == null || age >= LineLife)
            {
                if (l.Root != null) Destroy(l.Root.gameObject);
                _lines.RemoveAt(i);
                continue;
            }
            float a = age < LineLife - 0.4f ? 1f : (LineLife - age) / 0.4f;
            for (int k = 0; k < l.Texts.Length; k++)
            {
                if (l.Texts[k] == null) continue;
                Color c = k < l.Texts.Length - 1 ? Color.black : Green; // ふちの字が先、本体の字が最後
                c.a = a;
                WorldText.SetColor(l.Texts[k], c);
            }
        }
        Layout();
    }

    void Clear()
    {
        foreach (var l in _lines) if (l.Root != null) Destroy(l.Root.gameObject);
        _lines.Clear();
    }
}
