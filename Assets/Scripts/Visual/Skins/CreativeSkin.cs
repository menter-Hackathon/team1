using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】Creative のスキン（Figma・Photoshop・Illustrator・After Effects・Blender・OBS・GarageBand。2026-11-18。担当4。
// docs/app-skill-visuals-design.md 5章）。
// 何をまねるか：変形の枠（8つのハンドル）・ペンツールの点と線・塗りつぶし・動く点線の選択範囲・色の見本・レイヤー・透明のチェック柄。
// ・能力 バケツ塗り（5-1）は、Creative のスキンの「塗り」の枠（Attack_Ability_PaintBucket）。その場で作る「ペイント」は Generic などの塗り
// ・代用ボスの枠（5-2）。投げ物・円・ワープは Generic に任せる
// ・当たった絵（5-3）：変形の枠が「ギュッ」と縮む＋絵の具のはね。エラーの絵：チェック柄に赤い斜線
public class CreativeSkin : AppSkin
{
    public override SkinId Id => SkinId.Creative;
    public override string NotificationTitle => "Exported";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        AttackFrame f = K.Resolve(frame, info.Data);
        bool bucket = info.Data != null && info.Data.name.Contains("PaintBucket");
        if (bucket || f == AttackFrame.Paint) return GenericFrameFx.Apply<CreativeFxBucket>(AttackFrame.Paint, phase, info);
        switch (f)
        {
            case AttackFrame.SmallShot:
                if (K.IsVolleyTelegraph(info)) return phase != DressPhase.Telegraph || CreativeArt.PenVolley(info);
                return GenericFrameFx.Apply<CreativeFxPenPoint>(f, phase, info);
            case AttackFrame.LongShot:
                if (K.IsVolleyTelegraph(info))
                {
                    if (phase != DressPhase.Telegraph) return true;
                    int n = K.VolleyCount(info, false);
                    Color edge = info.IsEnemy ? K.Red : Color.white;
                    return GenericFrameVolley.Show(info, n, 0.5f, false, (slot, i) =>
                    {
                        K.Plate(slot, Vector2.zero, 7, 7, 1, edge, CreativeArt.Paint3(i), K.OrderDeco, out SpriteRenderer o, out _);
                        return o;
                    }) != null;
                }
                return GenericFrameFx.Apply<CreativeFxBrush>(f, phase, info);
            case AttackFrame.Floor: return GenericFrameFx.Apply<CreativeFxSpill>(f, phase, info);
            case AttackFrame.Pillar: return GenericFrameFx.Apply<CreativeFxGradient>(f, phase, info);
            case AttackFrame.Window: return GenericFrameFx.Apply<CreativeFxLayer>(f, phase, info);
            case AttackFrame.Falling: return GenericFrameFx.Apply<CreativeFxArtboard>(f, phase, info);
            case AttackFrame.TopRow: return GenericFrameFx.Apply<CreativeFxSwatch>(f, phase, info);
            default: return GenericFrameDresser.Dress(f, phase, info);
        }
    }

    // ───────── 当たった絵（5-3） ─────────

    public override bool Hit(HitInfo info)
    {
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
        if (info.Strength == HitStrength.Small)
        {
            K.Shards(info.Point, dir, 1, i => CreativeArt.Drop, i => CreativeArt.Paint3(Random.Range(0, 3)));
            return true;
        }
        bool fin = info.Strength == HitStrength.Finisher;
        bool heavy = info.Strength == HitStrength.Heavy || fin;
        // 相手に変形の枠が付き、100% → 80% に「ギュッ」と縮む
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 size = new Vector2(Mathf.Min(b.size.x, 5f), Mathf.Min(b.size.y, 4f)) + Vector2.one * (fin ? 0.5f : 0.25f);
        Vector2 center = b.size.x > 5f || b.size.y > 4f ? info.Point : (Vector2)b.center;
        Transform root = K.Loose("Gen4_CreativeHit", center);
        CreativeArt.TransformFrame(root, size, fin ? 2 : 1, K.OrderHit);
        float life = heavy ? 0.3f : 0.2f;
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        float from = fin ? 1.5f : 1f;
        tw.Scale(root, Vector3.one * from, Vector3.one * from * 0.8f, 0.1f, 0f, GenericFrameTweens.Ease.OutBack);
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, life * 0.5f, life * 0.5f);
        // 絵の具のはね（マゼンタ・シアン・黄）と、ハンドルの小さい四角
        int n = fin ? 12 : heavy ? 6 : 3;
        K.Shards(info.Point, dir, n, i => i % 4 == 3 ? null : CreativeArt.Drop, i => i % 4 == 3 ? CreativeArt.Blue : CreativeArt.Paint3(i), 40f, fin ? 1.4f : 1f);
        return true;
    }

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 at = new Vector2(b.center.x, b.max.y + 0.6f);
        if (down)
        {
            Rect view = ScreenUi.ViewRect();
            at = Vector2.Lerp(at, view.center, 0.35f);
        }
        float scale = down ? 2f : 1f;
        float life = down ? 0.9f : 0.4f;
        Transform root = K.Loose("Gen4_CreativeError", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        // チェック柄の四角（「画像が無い」）に、赤い斜めの線
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        K.Spr(pic, K.RoundRect(18, 18, 0), Vector2.zero, K.Grey, K.OrderHit, 1f, "Edge");
        K.Spr(pic, CreativeArt.Checker(16, 16, 4), Vector2.zero, Color.white, K.OrderHit + 1, 1f, "Checker");
        K.Spr(pic, CreativeArt.Slash16, Vector2.zero, K.Red, K.OrderHit + 2, 1f, "Slash");
        tw.Pop(pic, Vector3.one * scale, 0.1f).Move(root, at, at + Vector2.up * 0.4f, life);
        foreach (var sr in pic.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, 0.12f, life - 0.12f);
        if (down)
        {
            TextMesh tm = K.Text(root, "Layer deleted", new Vector2(0f, -1.1f), 0.45f, Color.white, K.OrderHit + 3);
            tw.TextAlpha(tm, Color.white, 1f, 0f, 0.15f, life - 0.15f);
        }
        // プレイヤーの絵が、0.1秒だけチェック柄に見える（重ねるだけ）
        if (info.Target != null && b.size.x < 3f)
        {
            Transform over = K.Loose("Gen4_CreativeChecker", b.center);
            int w = Mathf.Clamp(Mathf.RoundToInt(b.size.x * 18f), 4, 40), h = Mathf.Clamp(Mathf.RoundToInt(b.size.y * 18f), 4, 40);
            SpriteRenderer sr = K.Spr(over, CreativeArt.Checker(w, h, 3), Vector2.zero, new Color(1f, 1f, 1f, 0.85f), K.OrderHit - 1, 1f, "Checker");
            GenericFrameTweens.Of(over.gameObject).Alpha(sr, 0.85f, 0.3f, 0.1f).Kill(0.1f);
        }
        return true;
    }
}

/// <summary>【仮】Creative の色と絵</summary>
public static class CreativeArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("18A0FB");
    public static readonly Color Magenta = RuntimeSprites.Hex("FF3EA5");
    public static readonly Color Cyan = RuntimeSprites.Hex("00C8FF");
    public static readonly Color Yellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color CheckB = RuntimeSprites.Hex("CCCCCC");
    public static readonly Color Panel = RuntimeSprites.Hex("2C2C2C");

    /// <summary>マゼンタ・シアン・黄を順番に</summary>
    public static Color Paint3(int i)
    {
        switch (((i % 3) + 3) % 3)
        {
            case 0: return Magenta;
            case 1: return Cyan;
            default: return Yellow;
        }
    }

    /// <summary>絵の具のしずく（3×4）</summary>
    public static Sprite Drop => K.Glyph("CrDrop", new[] { ".X.", "XXX", "XXX", ".X." });

    /// <summary>透明のチェック柄（w×h ドット、ます目 cell ドット。白と #CCCCCC）</summary>
    public static Sprite Checker(int w, int h, int cell)
    {
        var rows = new string[h];
        var chars = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) chars[x] = ((x / cell) + (y / cell)) % 2 == 0 ? 'a' : 'b';
            rows[y] = new string(chars);
        }
        return K.Art($"CrChecker_{w}_{h}_{cell}", rows, new Dictionary<char, Color32>
        {
            { 'a', new Color32(255, 255, 255, 255) },
            { 'b', new Color32(0xcc, 0xcc, 0xcc, 255) },
        });
    }

    /// <summary>左下から右上への斜めの線（16ドット、太さ2）</summary>
    public static Sprite Slash16 => K.Make("CrSlash16", 16, 16, (x, y) => Mathf.Abs(x - (15 - y)) <= 1);

    static readonly Dictionary<char, Color32> BucketPal = new Dictionary<char, Color32>
    {
        { 'o', RuntimeSprites.Hex("3A3A3A") },
        { 'g', RuntimeSprites.Hex("C7C7CC") },
        { 'w', new Color32(255, 255, 255, 255) },
    };

    /// <summary>バケツ（12×12）。絵の具の面（4行目）は BucketPaint を重ねて色を付ける</summary>
    public static Sprite Bucket => K.Art("CrBucket", new[]
    {
        "...oooooo...",
        "..oo....oo..",
        ".oo......oo.",
        "oooooooooooo",
        "o..........o",
        "oggwgggggggo",
        ".ogwggggggo.",
        ".ogwggggggo.",
        ".ogwggggggo.",
        "..ogwggggo..",
        "..ogwggggo..",
        "..oooooooo..",
    }, BucketPal);

    public static Sprite BucketPaint => K.Glyph("CrBucketPaint", new[]
    {
        "............",
        "............",
        "............",
        "............",
        ".XXXXXXXXXX.",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
    });

    /// <summary>目の印（レイヤーの表示。7×5）</summary>
    public static Sprite EyeOpen => K.Glyph("CrEyeOpen", new[] { "..XXX..", ".X...X.", "X..X..X", ".X...X.", "..XXX.." });
    public static Sprite EyeClosed => K.Glyph("CrEyeClosed", new[] { ".......", ".......", "XXXXXXX", ".X.X.X.", "......." });

    /// <summary>
    /// 変形の枠（青い線と、角・辺のまん中の小さい四角 8つ）を root のまん中に作る。size はマス、thick は線の太さ（ドット）
    /// </summary>
    public static void TransformFrame(Transform root, Vector2 size, int thick, int order)
    {
        float t = thick * K.Dot;
        K.Box(root, new Vector2(0f, size.y * 0.5f), new Vector2(size.x + t, t), Blue, order, "Top");
        K.Box(root, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x + t, t), Blue, order, "Bottom");
        K.Box(root, new Vector2(-size.x * 0.5f, 0f), new Vector2(t, size.y + t), Blue, order, "Left");
        K.Box(root, new Vector2(size.x * 0.5f, 0f), new Vector2(t, size.y + t), Blue, order, "Right");
        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iy = -1; iy <= 1; iy++)
            {
                if (ix == 0 && iy == 0) continue;
                var p = new Vector2(ix * size.x * 0.5f, iy * size.y * 0.5f);
                K.Plate(root, p, 5, 5, 0, Blue, Color.white, order + 1, out _, out _);
            }
        }
    }

    /// <summary>動く点線（白と黒、2ドットずつ。offset で流れる）を、キャンバスのふちに描く</summary>
    public static void Ants(GenericFrameCanvas cv, int x0, int y0, int w, int h, int offset, Color32 a, Color32 b)
    {
        int i = 0;
        for (int x = x0; x < x0 + w; x++) cv.Set(x, y0, AntColor(i++, offset, a, b));
        for (int y = y0 + 1; y < y0 + h; y++) cv.Set(x0 + w - 1, y, AntColor(i++, offset, a, b));
        for (int x = x0 + w - 2; x >= x0; x--) cv.Set(x, y0 + h - 1, AntColor(i++, offset, a, b));
        for (int y = y0 + h - 2; y > y0; y--) cv.Set(x0, y, AntColor(i++, offset, a, b));
    }

    static Color32 AntColor(int i, int offset, Color32 a, Color32 b) => ((i + offset) / 2) % 2 == 0 ? a : b;

    /// <summary>ペンツールの点の予告（ボスの上に、点が発射する数だけ打たれ、青い線でつながる）</summary>
    public static bool PenVolley(DressInfo info)
    {
        int n = K.VolleyCount(info, true);
        const float spacing = 0.8f;
        Color edge = info.IsEnemy ? K.Red : Color.white;
        return GenericFrameVolley.Show(info, n, spacing, true, (slot, i) =>
        {
            K.Plate(slot, Vector2.zero, 6, 6, 0, Blue, Color.white, K.OrderDeco + 1, out SpriteRenderer o, out _);
            if (i > 0)
            {
                // 前の点への青い線（扇の高さのずれに合わせて傾ける）
                float c = (n - 1) * 0.5f;
                float dy = (Mathf.Abs(i - c) - Mathf.Abs(i - 1 - c)) * 0.12f;
                var d = new Vector2(-spacing, dy);
                SpriteRenderer line = K.Box(slot, d * 0.5f, new Vector2(d.magnitude, K.Dot), Blue, K.OrderDeco, "PenLine");
                line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 180f);
            }
            K.Spr(slot, K.RoundEdge(8, 8, 0), Vector2.zero, edge, K.OrderDeco + 2, 1f, "Edge");
            return o;
        }, K.VolleyRows(info)) != null;
    }
}

// ───────── 5-1 能力：バケツ塗り（塗り） ─────────

/// <summary>
/// 【仮】バケツ塗り。動く点線の選択範囲 → バケツから段々に広がる塗り（4ドットのます目、0.15秒）→ 残っている間は点線が流れ、
/// 0.5秒ごとの判定で面が波打ち、しずくがはねる → 最後の 0.5秒で下からチェック柄（レイヤーを消した）→ 消える。
/// 塗り終わった瞬間に、4つの角に変形のハンドルが光る
/// </summary>
public class CreativeFxBucket : GenericFrameFx
{
    const float FillTime = 0.15f;
    GenericFrameCanvas _cv;
    int _w, _h;
    Vector2Int _origin;
    float _maxDist;
    Color32 _paint, _hi, _lo, _white, _black, _checkA, _checkB;
    Transform _bucket;
    readonly List<Transform> _handles = new List<Transform>();
    int _lastTick = -1;
    bool _active;

    protected override void Build()
    {
        _w = Px(Size.x);
        _h = Px(Size.y);
        _cv = new GenericFrameCanvas(_w, _h, "Bucket");
        _cv.Show(Root, Vector2.zero, K.OrderPillar, "Paint");
        // 塗りは、中の敵が見えるように少しすかす（点線は濃いまま）
        Color c = Tint;
        _paint = K.WithAlpha(c, 0.62f);
        _hi = K.WithAlpha(Color.Lerp(c, Color.white, 0.45f), 0.85f);
        _lo = K.WithAlpha(Color.Lerp(c, Color.black, 0.18f), 0.7f);
        _white = new Color32(255, 255, 255, 255);
        _black = new Color32(0, 0, 0, 255);
        _checkB = K.WithAlpha(CreativeArt.CheckB, 0.7f);
        _checkA = new Color32(255, 255, 255, 178);

        // バケツは持ち主に近い側の上の角。絵の具はそこから広がる
        int side = -1;
        if (Info.Owner != null && transform.InverseTransformPoint(Info.Owner.OriginPoint).x > 0f) side = 1;
        _origin = new Vector2Int(side < 0 ? 2 : _w - 3, 2);
        _maxDist = 1f;
        foreach (var corner in new[] { new Vector2(0, 0), new Vector2(_w, 0), new Vector2(0, _h), new Vector2(_w, _h) })
        {
            _maxDist = Mathf.Max(_maxDist, Vector2.Distance(corner, _origin));
        }

        _bucket = K.Node(Root, "Bucket", new Vector2(side * (_w * 0.5f + 3f), _h * 0.5f + 5f) * K.Dot);
        K.Spr(_bucket, CreativeArt.Bucket, Vector2.zero, Color.white, K.OrderPillar + 3, 1f, "Bucket");
        K.Spr(_bucket, CreativeArt.BucketPaint, Vector2.zero, c, K.OrderPillar + 4, 1f, "Paint");
        _bucket.localRotation = Quaternion.Euler(0f, 0f, side < 0 ? -40f : 40f);
        _bucket.localScale = Vector3.zero;

        // 4つの角の変形のハンドル（青い四角 3ドット＋白いふち）
        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                Transform h = K.Node(Root, "Handle", new Vector2(ix * _w * 0.5f, iy * _h * 0.5f) * K.Dot);
                K.Plate(h, Vector2.zero, 5, 5, 0, Color.white, CreativeArt.Blue, K.OrderPillar + 2, out _, out _);
                h.gameObject.SetActive(false);
                _handles.Add(h);
            }
        }
        Draw(0f);
    }

    protected override void OnTelegraph() => Begin();
    protected override void OnActive() => Begin();

    void Begin()
    {
        if (_active) return;
        _active = true;
        var tw = GenericFrameTweens.Of(gameObject);
        tw.Pop(_bucket, Vector3.one, 0.08f);
        foreach (var sr in _bucket.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, 0.15f, 0.4f);
    }

    protected override void Tick(float t)
    {
        if (!_active) return;
        Draw(t);
        // 塗り終わった瞬間、角のハンドルが光る（0.35秒）
        bool show = t >= FillTime && t < FillTime + 0.35f;
        foreach (var h in _handles)
        {
            if (h.gameObject.activeSelf != show) h.gameObject.SetActive(show);
        }
        // 判定の瞬間（ヒット間隔ごと）に、しずくが 2個はねる
        AttackData d = Info.Data;
        if (d != null && d.TickInterval > 0f && t > FillTime)
        {
            int tick = Mathf.FloorToInt(t / d.TickInterval);
            if (tick != _lastTick)
            {
                _lastTick = tick;
                Vector2 top = transform.TransformPoint(new Vector3(0f, _h * 0.5f * K.Dot - 0.1f, 0f));
                K.Shards(top, Vector2.up, 2, i => CreativeArt.Drop, i => _paint, 50f);
            }
        }
    }

    void Draw(float t)
    {
        _cv.Clear();
        float hw = HitWindow;
        // 段々に広がる（6段）
        float fill = Mathf.Ceil(Mathf.Clamp01(t / FillTime) * 6f) / 6f;
        float reach = fill * (_maxDist + 4f);
        // 最後の 0.5秒：下からチェック柄
        float ck = Mathf.Clamp01((t - (hw - 0.5f)) / 0.5f);
        int checkRow = _h - Mathf.RoundToInt(ck * _h);
        AttackData d = Info.Data;
        bool ripple = d != null && d.TickInterval > 0f && t > FillTime && (t % d.TickInterval) < 0.1f;
        for (int cy = 0; cy * 4 < _h; cy++)
        {
            for (int cx = 0; cx * 4 < _w; cx++)
            {
                if (Vector2.Distance(new Vector2(cx * 4 + 2, cy * 4 + 2), _origin) > reach) continue;
                for (int y = cy * 4; y < cy * 4 + 4 && y < _h; y++)
                {
                    for (int x = cx * 4; x < cx * 4 + 4 && x < _w; x++)
                    {
                        Color32 col;
                        if (y >= checkRow) col = ((x / 4) + (y / 4)) % 2 == 0 ? _checkA : _checkB;
                        else
                        {
                            int surface = 1 + (ripple && ((x / 3) % 2 == 0) ? 1 : 0);
                            if (y < surface) continue;
                            col = y == surface ? _hi : (y > _h - 4 ? _lo : _paint);
                        }
                        _cv.Set(x, y, col);
                    }
                }
            }
        }
        // 面の上をゆっくり動く明るい点 3個
        if (fill >= 1f && checkRow > 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int x = Mathf.FloorToInt(Mathf.Repeat(t * 9f + k * _w / 3f, _w - 4)) + 2;
                _cv.Set(x, 3, _white);
            }
        }
        // 選択範囲の動く点線（0.1秒ごとに 1ドット流れる）
        CreativeArt.Ants(_cv, 0, 0, _w, _h, Mathf.FloorToInt(t / 0.1f), _white, _black);
        _cv.Apply();
    }

    protected override void OnVanish() => FadeAway(0.1f);
}

// ───────── 5-2 代用ボスの枠 ─────────

/// <summary>小弾：ペンツールの点（白い四角の点から、前後に青いハンドルの線。先に丸）</summary>
public class CreativeFxPenPoint : GenericFrameFx
{
    Transform _body;

    protected override void Build()
    {
        _body = K.Node(Root, "Body", Vector2.zero);
        K.BoxPx(_body, 0f, 0f, 13f, 1f, CreativeArt.Blue, K.OrderCore, "Handle");
        K.Spr(_body, K.Disc(3), new Vector2(-6f, 0f) * K.Dot, CreativeArt.Blue, K.OrderCore, 1f, "End");
        K.Spr(_body, K.Disc(3), new Vector2(6f, 0f) * K.Dot, CreativeArt.Blue, K.OrderCore, 1f, "End");
        K.Plate(_body, Vector2.zero, 8, 8, 0, Edge, Color.white, K.OrderCore + 1, out _, out _);
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    void Flash()
    {
        Transform r = K.Loose("Gen4_PenFlash", transform.position);
        SpriteRenderer sq = K.Spr(r, K.RoundRect(6, 6, 0), Vector2.zero, Color.white, K.OrderSpark);
        GenericFrameTweens.Of(r.gameObject).Scale(sq.transform, Vector3.one, Vector3.one * 2.4f, 0.1f).Alpha(sq, 1f, 0f, 0.1f).Kill(0.12f);
    }

    protected override void OnLanded()
    {
        Flash();
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (!IsLanded) Flash();
        if (Root != null) Destroy(Root.gameObject);
    }
}

/// <summary>長弾：ブラシの線（太い → 細い。マゼンタ・シアン・黄を順番に）</summary>
public class CreativeFxBrush : GenericFrameFx
{
    Transform _body;
    Color _color;

    static bool On(int x, int y)
    {
        if (x < 0 || x >= 22 || y < 0 || y >= 7) return false;
        float half = x < 19 ? Mathf.Lerp(0.4f, 3.1f, x / 18f) : 3.1f - (x - 18) * 0.95f;
        return Mathf.Abs(y - 3) <= half;
    }

    static Sprite Outer => K.Make("CrBrushOuter", 22, 7, On);
    static Sprite Inner => K.Make("CrBrushInner", 22, 7, (x, y) => On(x, y) && On(x - 1, y) && On(x + 1, y) && On(x, y - 1) && On(x, y + 1));

    protected override void Build()
    {
        FlipWhenLeft = true;
        _color = CreativeArt.Paint3(Info.Index);
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, Outer, Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        K.Spr(_body, Inner, new Vector2(0f, 0f), _color, K.OrderCore + 1, 1f, "Paint");
        // 毛の筋（明るい線）
        K.BoxPx(_body, 3f, 1f, 12f, 1f, K.WithAlpha(Color.white, 0.45f), K.OrderCore + 2, "Bristle");
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void OnLanded()
    {
        // 床に絵の具が落ちる
        K.Shards(transform.position, Vector2.up, 5, i => CreativeArt.Drop, i => _color, 70f);
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (IsLanded)
        {
            if (Root != null) Destroy(Root.gameObject);
            return;
        }
        K.Shards(transform.position, Vector2.up, 3, i => CreativeArt.Drop, i => _color, 70f);
        FadeAway(0.08f);
    }
}

/// <summary>床：こぼれた絵の具（赤いたまり。判定の瞬間に波打つ。チェック柄に変わって消える）</summary>
public class CreativeFxSpill : GenericFrameFx
{
    GenericFrameCanvas _cv;
    int _w, _h;
    Color32 _paint, _hi;

    protected override void Build()
    {
        _w = Px(Size.x);
        _h = Px(Size.y);
        _cv = new GenericFrameCanvas(_w, _h, "Spill");
        _cv.Show(Root, Vector2.zero, K.OrderFloor + 1, "Spill");
        Color c = Tint;
        _paint = c;
        _hi = Color.Lerp(c, Color.white, 0.4f);
        Draw(0f, false);
    }

    void Draw(float t, bool checker)
    {
        _cv.Clear();
        AttackData d = Info.Data;
        bool ripple = d != null && d.PersistDuration > 0f && d.TickInterval > 0f && (t % d.TickInterval) < 0.1f;
        int pool = Mathf.Min(_h, _h >= 12 ? 9 : 5);
        // たまりは広がりながら出る（0.1秒）
        float spread = Mathf.Clamp01(t / 0.1f);
        int half = Mathf.RoundToInt(_w * 0.5f * Mathf.Max(0.3f, spread));
        for (int x = _w / 2 - half; x < _w / 2 + half; x++)
        {
            float wave = Mathf.Sin(x * 0.55f + t * 7f) * (ripple ? 1.4f : 0.6f);
            int top = _h - pool + Mathf.RoundToInt(wave) + (x < _w / 2 - half + 2 || x > _w / 2 + half - 3 ? 2 : 0);
            for (int y = Mathf.Max(0, top); y < _h; y++)
            {
                Color32 col = checker ? (((x / 3) + (y / 3)) % 2 == 0 ? new Color32(255, 255, 255, 255) : (Color32)CreativeArt.CheckB) : (y == top ? _hi : _paint);
                _cv.Set(x, y, col);
            }
        }
        // はねたしずく（ゆっくり上下する）
        if (!checker && spread >= 1f)
        {
            for (int k = 0; k < 3; k++)
            {
                int x = Mathf.RoundToInt(_w * (0.25f + k * 0.25f));
                int y = _h - pool - 3 - Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(t * 5f + k * 1.7f)) * 2f);
                _cv.Fill(x, y, 2, 2, _paint);
            }
        }
        _cv.Apply();
    }

    protected override void Tick(float t) => Draw(t, false);

    protected override void OnVanish()
    {
        Draw(1f, true);
        FadeAway(0.15f);
    }
}

/// <summary>柱：縦のグラデーション（赤 → 白の4段）。左右に動く点線。上から消える（消しゴム）</summary>
public class CreativeFxGradient : GenericFrameFx
{
    float _wu, _hu;
    GenericFrameCanvas _ants, _sideL, _sideR;
    SpriteRenderer _antsSr;
    readonly List<SpriteRenderer> _segments = new List<SpriteRenderer>();
    bool _active;
    Color32 _edge;

    protected override void Build()
    {
        Vector2 s = Size;
        _wu = s.x;
        _hu = s.y;
        _edge = Edge;
    }

    protected override void OnTelegraph()
    {
        int w = Px(_wu);
        _ants = new GenericFrameCanvas(w, 6, "GradTele");
        _antsSr = _ants.Show(Root, new Vector2(0f, -_hu * 0.5f + 3f * K.Dot), K.OrderTelegraph, "Ants");
        _antsSr.color = Color.white;
    }

    protected override void OnActive()
    {
        if (_active) return;
        _active = true;
        if (_antsSr != null) Destroy(_antsSr.gameObject);
        _antsSr = null;
        Color c0 = K.Step(Tint, Info.Index);
        float segH = _hu / 4f;
        for (int i = 0; i < 4; i++)
        {
            Color c = Color.Lerp(c0, Color.white, i * 0.28f);
            c.a = 0.72f;
            _segments.Add(K.Box(Root, new Vector2(0f, -_hu * 0.5f + segH * (i + 0.5f)), new Vector2(_wu - 2f * K.Dot, segH), c, K.OrderPillar, "Grad"));
        }
        int hpx = Px(_hu);
        _sideL = new GenericFrameCanvas(1, hpx, "GradL");
        _sideR = new GenericFrameCanvas(1, hpx, "GradR");
        _sideL.Show(Root, new Vector2(-_wu * 0.5f + 0.5f * K.Dot, 0f), K.OrderPillar + 1, "AntsL");
        _sideR.Show(Root, new Vector2(_wu * 0.5f - 0.5f * K.Dot, 0f), K.OrderPillar + 1, "AntsR");
        SpriteRenderer flash = K.Box(Root, Vector2.zero, new Vector2(_wu, _hu), new Color(1f, 1f, 1f, 0.7f), K.OrderPillar + 2, "Flash");
        GenericFrameTweens.Of(gameObject).Alpha(flash, 0.7f, 0f, 0.08f);
    }

    protected override void Tick(float t)
    {
        int offset = Mathf.FloorToInt(Time.time / 0.1f);
        if (Phase == DressPhase.Telegraph && _ants != null && _antsSr != null)
        {
            _ants.Clear();
            CreativeArt.Ants(_ants, 0, 0, _ants.W, _ants.H, offset, _edge, new Color32(0, 0, 0, 0));
            _ants.Apply();
            _antsSr.enabled = K.BlinkOn(TelegraphTime - t);
            return;
        }
        if (!_active || _sideL == null) return;
        foreach (var cv in new[] { _sideL, _sideR })
        {
            cv.Clear();
            for (int y = 0; y < cv.H; y++) cv.Set(0, y, ((y + offset) / 2) % 2 == 0 ? _edge : new Color32(0, 0, 0, 255));
            cv.Apply();
        }
    }

    protected override void OnVanish()
    {
        // 上から消える（消しゴム）
        var segs = new List<SpriteRenderer>(_segments);
        Transform r = Detach(0.16f);
        if (r == null) return;
        GenericFrameTweens.Of(r.gameObject).Each(0.14f, k =>
        {
            int erased = Mathf.FloorToInt(k * (segs.Count + 1));
            for (int i = 0; i < segs.Count; i++)
            {
                if (segs[i] != null) segs[i].enabled = i < segs.Count - erased;
            }
            foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.name.StartsWith("Ants")) K.SetAlpha(sr, 1f - k);
            }
        });
    }
}

/// <summary>窓：レイヤーの行（暗いパネルに、目の印・チェック柄の小さい絵・レイヤーの名前）。目が閉じて消える（非表示）</summary>
public class CreativeFxLayer : GenericFxWindow
{
    readonly List<SpriteRenderer> _eyes = new List<SpriteRenderer>();

    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（レイヤーのパネル）なので、今の板のまま（2026-11-19）
    protected override Color PanelColor => K.WithAlpha(CreativeArt.Panel, 0.97f);
    protected override int Corner => 2;
    protected override bool SlideOnVanish => false;

    protected override void BuildContent(Transform body, int w, int h)
    {
        // レイヤーのパネル：3行。いちばん上が選ばれた行（青）
        int rows = Mathf.Max(1, h / 12);
        float rowH = (h - 2f) / rows;
        for (int r = 0; r < rows; r++)
        {
            float cy = h * 0.5f - 1f - rowH * (r + 0.5f);
            if (r == 0) K.BoxPx(body, 0f, cy, w - 2f, rowH, K.WithAlpha(CreativeArt.Blue, 0.45f), K.OrderWindow + 1, "Selected");
            else K.BoxPx(body, 0f, cy + rowH * 0.5f, w - 2f, 1f, K.WithAlpha(Color.white, 0.12f), K.OrderWindow + 1, "Line");
            _eyes.Add(K.Spr(body, CreativeArt.EyeOpen, new Vector2(-w * 0.5f + 6f, cy) * K.Dot, Color.white, K.OrderWindow + 2, 1f, "Eye"));
            K.Spr(body, K.RoundRect(10, 10, 0), new Vector2(-w * 0.5f + 16f, cy) * K.Dot, K.Grey, K.OrderWindow + 2, 1f, "ThumbEdge");
            K.Spr(body, r == 0 ? CreativeArt.Checker(8, 8, 2) : K.RoundRect(8, 8, 0), new Vector2(-w * 0.5f + 16f, cy) * K.Dot,
                r == 0 ? Color.white : CreativeArt.Paint3(r + Info.Index), K.OrderWindow + 3, 1f, "Thumb");
            float barL = -w * 0.5f + 23f, barR = w * 0.5f - 4f;
            float len = Mathf.Max(4f, (barR - barL) * (r == 0 ? 0.8f : 0.6f));
            K.BoxPx(body, barL + len * 0.5f, cy, len, 2f, r == 0 ? Color.white : K.WinLightShade, K.OrderWindow + 2, "Name");
        }
    }

    protected override void OnVanishContent(Transform body)
    {
        foreach (var e in _eyes) if (e != null) e.sprite = CreativeArt.EyeClosed;
    }
}

/// <summary>落下物：アートボード（白い板。左上に名札、まわりに変形のハンドル 8個）。着地でハンドルが外へ飛ぶ</summary>
public class CreativeFxArtboard : GenericFxFalling
{
    protected override Color PanelColor => Color.white;

    protected override void BuildContent(Transform body, int w, int h)
    {
        // 中の絵（デザインの途中）：マゼンタの四角・シアンの丸・黄色の棒
        K.BoxPx(body, -w * 0.25f, 2f, 14f, 10f, CreativeArt.Magenta, K.OrderWindow + 1, "Rect");
        K.Spr(body, K.Disc(12), new Vector2(w * 0.22f, 3f) * K.Dot, CreativeArt.Cyan, K.OrderWindow + 1, 1f, "Circle");
        K.BoxPx(body, 0f, -h * 0.5f + 7f, w - 14f, 3f, CreativeArt.Yellow, K.OrderWindow + 1, "Bar");
        // 左上の名札
        K.BoxPx(body, -w * 0.5f + 7f, h * 0.5f + 3f, 12f, 2f, K.Grey, K.OrderWindow + 1, "Name");
        // 選択の枠と、ハンドル 8個
        Transform frame = K.Node(body, "Frame", Vector2.zero);
        CreativeArt.TransformFrame(frame, new Vector2(w + 2f, h + 2f) * K.Dot, 1, K.OrderWindow + 2);
    }

    protected override void LandedBurst(Vector2 at)
    {
        K.Shards(at, Vector2.up, 8, i => null, i => i % 2 == 0 ? CreativeArt.Blue : Color.white, 90f, 1.3f);
        K.Shards(at, Vector2.up, 4, i => CreativeArt.Drop, i => CreativeArt.Paint3(i), 60f);
    }
}

/// <summary>上の列：色の見本（下の端がしずくの形）。着地で、その色のはね</summary>
public class CreativeFxSwatch : GenericFxTopRow
{
    Color Swatch => Info.IsEnemy ? CreativeArt.Paint3(Info.Index) : Main;

    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（色の見本）なので、今の板のまま（2026-11-19）

    protected override Color PanelColor => Swatch;

    protected override void BuildContent(Transform body, int w, int h)
    {
        // 下の端にしずく
        float bottom = -h * 0.5f;
        K.BoxPx(body, -w * 0.25f, bottom - 1.5f, 2f, 3f, Swatch, K.OrderWindow, "Drip");
        K.BoxPx(body, w * 0.05f, bottom - 1f, 2f, 2f, Swatch, K.OrderWindow, "Drip");
        K.BoxPx(body, w * 0.3f, bottom - 2f, 2f, 4f, Swatch, K.OrderWindow, "Drip");
        // 光の筋と、色の番号の代わりの白い棒
        K.BoxPx(body, -w * 0.5f + 5f, h * 0.5f - 2.5f, 6f, 1f, K.WithAlpha(Color.white, 0.6f), K.OrderWindow + 1, "Shine");
        K.BoxPx(body, w * 0.15f, -1f, w * 0.45f, 1f, K.WithAlpha(Color.white, 0.85f), K.OrderWindow + 1, "Hex");
    }

    protected override void BuildBand(Transform root, float spacing)
    {
        // 画面の上に、色の見本の帯（四角が並ぶ）
        int n = Mathf.Max(3, Mathf.RoundToInt(spacing / 0.35f));
        for (int i = 0; i < n; i++)
        {
            Color c = i % 4 == 3 ? Main : CreativeArt.Paint3(i);
            c.a = 0.45f;
            K.Box(root, new Vector2(-spacing * 0.5f + spacing * (i + 0.5f) / n, Size.y * 0.5f + 0.25f), new Vector2(spacing / n - K.Dot, 0.3f), c, K.OrderTelegraph, "Swatch");
        }
    }

    protected override void LandedBurst(Vector2 at)
    {
        Color c = Swatch;
        K.Shards(at, Vector2.up, 6, i => CreativeArt.Drop, i => c, 70f, 1.2f);
        GenericFrameBits.Pop(at, null, 0, Edge, c, 2);
    }
}
