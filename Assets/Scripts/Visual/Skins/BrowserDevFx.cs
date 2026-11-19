using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】担当2 のスキン（Browser・Dev）と Zoom の能力で使い回す、絵と動きの道具（2026-11-18。docs/app-skill-visuals-design.md 0〜3章・7-3）。
// ・絵はコードの中のドット絵（1マス＝18ドット）。文字の並びは BrowserDevGrid で組んでから RuntimeSprites.FromAscii に渡す。
// ・絵の名前（キャッシュのキー）は "BD_" で始める（ほかの担当の絵とぶつけない）。
// ・型の名前は Browser で始める（担当2 の決まり）。
// ・2026-11-19：Mac の部品（窓・信号機・影・閉じる動き）は MacUi（担当1）。担当2 の格子から使う受け口は BrowserDevMac

/// <summary>【仮】色・重なる順番・絵の作り方・置き方・かけら</summary>
public static class BrowserDevFx
{
    public const float Dot = 1f / 18f;

    // ───────── 共通の色（1-6）【仮】 ─────────
    public static readonly Color EnemyEdge = RuntimeSprites.Hex("FF453A");
    public static readonly Color White = Color.white;
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);

    // ───────── 重なる順番（1-6） ─────────
    public const int OrderFloor = 6, OrderTelegraph = 17, OrderBand = 17, OrderWindow = 19, OrderCore = 20, OrderTrail = 21, OrderDeco = 23;
    public const int OrderPillar = 18, OrderPillarDeco = 41, OrderSpark = 45, OrderHit = 47, OrderShard = 48, OrderMark = 51;

    static readonly Color32 W32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', W32 } };

    // ───────── 絵 ─────────

    static Sprite _px;

    /// <summary>白い1ドット（伸ばして四角に使う）</summary>
    public static Sprite Px => _px != null ? _px : (_px = RuntimeSprites.FromAscii("BD_Px", new[] { "X" }, Mono));

    /// <summary>
    /// 文字の並びから絵を作る（キーの頭に "BD_" を付けて、1回だけ作る）。
    /// 格子に色で置いた所（BrowserDevGrid.Put。Mac の部品）は、その色も足す（2026-11-19）
    /// </summary>
    public static Sprite Art(string key, BrowserDevGrid g, Dictionary<char, Color32> palette)
    {
        if (g.AutoPalette.Count == 0) return RuntimeSprites.FromAscii("BD_" + key, g.Rows(), palette);
        var pal = palette != null ? new Dictionary<char, Color32>(palette) : new Dictionary<char, Color32>();
        foreach (var kv in g.AutoPalette) pal[kv.Key] = kv.Value;
        return RuntimeSprites.FromAscii("BD_" + key, g.Rows(), pal);
    }

    /// <summary>文字の並びから絵を作る</summary>
    public static Sprite Art(string key, string[] rows, Dictionary<char, Color32> palette) => RuntimeSprites.FromAscii("BD_" + key, rows, palette);

    static Sprite _dotted;

    /// <summary>点線（2ドット描いて2ドットあける。白。並べて描く用）</summary>
    public static Sprite Dotted => _dotted != null ? _dotted : (_dotted = MakeStrip("BD_Dotted", new[] { true, true, false, false }));

    // 並べて描く絵は FullRect にする（Tight だと透明な所が切られて、並びがずれる）
    static Sprite MakeStrip(string name, bool[] on)
    {
        var tex = new Texture2D(on.Length, 1, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[on.Length];
        for (int i = 0; i < on.Length; i++) px[i] = on[i] ? W32 : new Color32(255, 255, 255, 0);
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, on.Length, 1f), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    // ───────── 置き方 ─────────

    /// <summary>子の空の物を作る</summary>
    public static Transform Child(Transform parent, string name, Vector2 localPos)
    {
        var go = new GameObject(name);
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.localPosition = localPos;
        return go.transform;
    }

    /// <summary>絵を1枚出す（元の大きさ × scale）</summary>
    public static SpriteRenderer Show(Transform parent, Sprite sprite, Vector2 localPos, int order, float scale = 1f, string name = "Sprite")
    {
        SpriteRenderer sr = RuntimeSprites.Show(parent, sprite, localPos, order, scale, name);
        return sr;
    }

    /// <summary>四角を出す（大きさはマス。中心が localCenter）</summary>
    public static SpriteRenderer Rect(Transform parent, Vector2 localCenter, Vector2 size, Color color, int order, string name = "Rect")
    {
        SpriteRenderer sr = RuntimeSprites.Show(parent, Px, localCenter, order, 1f, name);
        sr.transform.localScale = new Vector3(size.x / Dot, size.y / Dot, 1f);
        sr.color = color;
        return sr;
    }

    /// <summary>四角の大きさと位置を変える（Rect で作った物）</summary>
    public static void SetRect(SpriteRenderer sr, Vector2 localCenter, Vector2 size)
    {
        if (sr == null) return;
        sr.transform.localPosition = localCenter;
        sr.transform.localScale = new Vector3(Mathf.Max(0f, size.x) / Dot, Mathf.Max(0f, size.y) / Dot, 1f);
    }

    /// <summary>点線の線を出す（横向き。長さ length マス、太さ thick ドット）</summary>
    public static SpriteRenderer DottedLine(Transform parent, Vector2 localCenter, float length, int thick, float angle, Color color, int order)
    {
        var go = new GameObject("Dotted");
        go.layer = parent != null ? parent.gameObject.layer : 0;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Dotted;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(length, thick * Dot);
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>点線の四角い枠（大きさ size マス）</summary>
    public static void DottedBox(Transform parent, Vector2 center, Vector2 size, Color color, int order, List<SpriteRenderer> into = null)
    {
        float hx = size.x * 0.5f, hy = size.y * 0.5f, t = Dot;
        var a = DottedLine(parent, center + new Vector2(0f, hy - t * 0.5f), size.x, 1, 0f, color, order);
        var b = DottedLine(parent, center + new Vector2(0f, -hy + t * 0.5f), size.x, 1, 0f, color, order);
        var c = DottedLine(parent, center + new Vector2(-hx + t * 0.5f, 0f), size.y, 1, 90f, color, order);
        var d = DottedLine(parent, center + new Vector2(hx - t * 0.5f, 0f), size.y, 1, 90f, color, order);
        if (into != null) into.AddRange(new[] { a, b, c, d });
    }

    /// <summary>どこにも付いていない物を作る（自分で消える物の土台）</summary>
    public static GameObject Loose(string name, Vector2 pos, float angle = 0f)
    {
        var go = new GameObject("BD_" + name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        return go;
    }

    /// <summary>
    /// 動きを付ける。tick(経った秒, 0〜1) を毎フレーム呼び、life 秒で go を消す。
    /// unscaled：実時間で動かす（当たった絵・エラーの絵。ヒットストップの間も「パッ」と出て、ゲームオーバーで時間が止まっても消える）
    /// </summary>
    public static BrowserDevAnim Animate(GameObject go, float life, Action<float, float> tick, bool unscaled = false)
    {
        var a = go.AddComponent<BrowserDevAnim>();
        a.Setup(life, tick, unscaled);
        return a;
    }

    // ───────── 動きの形 ─────────

    /// <summary>「パッ」と出る大きさ（0 → 115% → 100%）。e は出てからの秒、dur は全体の秒</summary>
    public static float Pop(float e, float dur = 0.1f)
    {
        if (e <= 0f) return 0f;
        float t = e / Mathf.Max(0.001f, dur);
        if (t >= 1f) return 1f;
        return t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    public static Color Opaque(Color c)
    {
        c.a = 1f;
        return c;
    }

    // ───────── 情報 ─────────

    public static string NameOf(AttackData d) => d != null ? d.name : "";

    /// <summary>今の絵（攻撃データの絵）。渡されていなければ、子の "Visual" を探す</summary>
    public static SpriteRenderer FindVisual(GameObject target)
    {
        if (target == null) return null;
        Transform v = target.transform.Find("Visual");
        return v != null ? v.GetComponent<SpriteRenderer>() : null;
    }

    /// <summary>体の範囲（移動用の当たり判定 → ほかの当たり判定 → 絵）。無ければ位置の 1×1</summary>
    public static Bounds BoundsOf(GameObject go)
    {
        if (go == null) return new Bounds(Vector3.zero, Vector3.one);
        Collider2D any = null;
        foreach (var col in go.GetComponentsInChildren<Collider2D>())
        {
            if (!col.enabled) continue;
            if (!col.isTrigger) return col.bounds;
            if (any == null) any = col;
        }
        if (any != null) return any.bounds;
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(go.transform.position, Vector3.one);
    }

    /// <summary>攻撃した人（ボス）の体の範囲（瞬間移動できる持ち主はその範囲）</summary>
    public static Bounds OwnerBounds(AttackRunner owner)
    {
        if (owner == null) return new Bounds(Vector3.zero, Vector3.one);
        var w = owner.GetComponent<IWarpable>();
        if (w != null) return w.BodyBounds;
        return BoundsOf(owner.gameObject);
    }

    /// <summary>カメラに映っている範囲</summary>
    public static Rect View() => ScreenUi.ViewRect();

    /// <summary>当たった絵を出す位置：当たった位置から、相手の中心と反対の向きへ 0.3マスずらす（1-2）</summary>
    public static Vector2 HitAnchor(HitInfo info)
    {
        Vector2 p = info.Point;
        Vector2 outward = Vector2.up;
        if (info.Target != null)
        {
            Vector2 c = BoundsOf(info.Target).center;
            Vector2 d = p - c;
            if (d.sqrMagnitude > 0.0004f) outward = d.normalized;
        }
        else if (info.Direction.sqrMagnitude > 0.0001f)
        {
            outward = -info.Direction.normalized;
        }
        return p + outward * 0.3f;
    }

    /// <summary>プレイヤーの頭の上（エラーの絵。頭の上 0.6マス）</summary>
    public static Vector2 HeadOf(GameObject target, Vector2 fallback, float above = 0.6f)
    {
        if (target == null) return fallback + Vector2.up * above;
        Bounds b = BoundsOf(target);
        return new Vector2(b.center.x, b.max.y + above);
    }

    /// <summary>足元（体の下の端）</summary>
    public static Vector2 FeetOf(GameObject target, Vector2 fallback)
    {
        if (target == null) return fallback;
        Bounds b = BoundsOf(target);
        return new Vector2(b.center.x, b.min.y);
    }

    // ───────── かけら ─────────

    /// <summary>
    /// かけらを飛ばす（1-2：向きの先へ扇状 ±spread°、速さ 6〜10マス/秒、重力あり、0.3〜0.5秒で消える）。
    /// pick(i) で i 個目の絵と色を決める
    /// </summary>
    public static void Burst(Vector2 pos, Vector2 dir, int count, Func<int, Sprite> sprite, Func<int, Color> color, int order = OrderShard,
        float spread = 40f, float speedMin = 6f, float speedMax = 10f, float lifeMin = 0.3f, float lifeMax = 0.5f, float gravity = 26f, bool spin = true,
        bool unscaled = false)
    {
        if (count <= 0) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        var go = Loose("Shards", pos);
        var shards = go.AddComponent<BrowserDevShards>();
        shards.Gravity = gravity;
        shards.Unscaled = unscaled;
        for (int i = 0; i < count; i++)
        {
            float a = UnityEngine.Random.Range(-spread, spread);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, a) * dir) * UnityEngine.Random.Range(speedMin, speedMax);
            Sprite s = sprite != null ? sprite(i) : Px;
            Color c = color != null ? color(i) : Color.white;
            shards.Add(s, c, v, UnityEngine.Random.Range(lifeMin, lifeMax), spin ? UnityEngine.Random.Range(-540f, 540f) : 0f, order);
        }
    }

    /// <summary>白い小さい四角（size ドット角）</summary>
    public static Sprite Square(int size)
    {
        var g = new BrowserDevGrid(size, size);
        g.Rect(0, 0, size, size, 'X');
        return Art($"Square_{size}", g, Mono);
    }

    /// <summary>文字（ドット絵の字）を1枚の絵にする。outline なら暗いふち（1ドット）を付ける</summary>
    public static Sprite Text(string text, Color32 color, bool outline = false, Color32? outlineColor = null)
    {
        int w = BrowserDevFont.Width(text);
        int pad = outline ? 1 : 0;
        var g = new BrowserDevGrid(w + pad * 2, BrowserDevFont.Height + pad * 2);
        BrowserDevFont.Stamp(g, text, pad, pad, 'T');
        if (outline) g.Outline('O', true);
        var pal = new Dictionary<char, Color32> { { 'T', color }, { 'O', outlineColor ?? new Color32(20, 20, 24, 230) } };
        return Art($"Text_{text}_{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}_{(outline ? 1 : 0)}", g, pal);
    }

    /// <summary>
    /// 読み込みの輪（直径 d ドット、太さ t ドット、3/4 の弧）。回して使う。色は SpriteRenderer.color
    /// </summary>
    public static Sprite Spinner(int d, int t)
    {
        var g = new BrowserDevGrid(d, d);
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = r - (y + 0.5f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r || dist <= r - t - 0.2f) continue;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180〜180
                if (ang > 0f && ang < 90f) continue; // 右上の 1/4 を空ける
                g.Set(x, y, 'X');
            }
        }
        return Art($"Spinner_{d}_{t}", g, Mono);
    }

    /// <summary>白い円（直径 d ドット）</summary>
    public static Sprite Disc(int d)
    {
        var g = new BrowserDevGrid(d, d);
        g.Disc(0, 0, d, 'X');
        return Art($"Disc_{d}", g, Mono);
    }

    /// <summary>白い輪（直径 d ドット、太さ t ドット）</summary>
    public static Sprite RingSprite(int d, int t)
    {
        var g = new BrowserDevGrid(d, d);
        g.Ring(0, 0, d, t, 'X');
        return Art($"Ring_{d}_{t}", g, Mono);
    }

    /// <summary>チェック（✓。太さ 2ドット、幅 size ドット）。色は palette の 'C'、ふちは 'O'</summary>
    public static void StampCheck(BrowserDevGrid g, int x0, int y0, int size, char c)
    {
        // 短い辺：左下へ、長い辺：右上へ
        int h = Mathf.Max(4, size * 3 / 4);
        int sx = x0 + 1, sy = y0 + h / 2;          // 短い辺の始まり
        int bx = x0 + size / 3, by = y0 + h - 2;    // 底
        DrawThickLine(g, sx, sy, bx, by, c);
        DrawThickLine(g, bx, by, x0 + size - 2, y0 + 1, c);
    }

    static void DrawThickLine(BrowserDevGrid g, int x0, int y0, int x1, int y1, char c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            g.Set(x, y, c);
            g.Set(x, y + 1, c);
            g.Set(x + 1, y, c);
        }
    }

    /// <summary>×（太さ1ドット、size ドット角）を書く</summary>
    public static void StampX(BrowserDevGrid g, int x0, int y0, int size, char c)
    {
        for (int i = 0; i < size; i++)
        {
            g.Set(x0 + i, y0 + i, c);
            g.Set(x0 + size - 1 - i, y0 + i, c);
        }
    }
}

/// <summary>
/// 【仮】担当2 の絵（Browser・Dev）から Mac の部品（MacUi。担当1。docs/mac-look-design.md 1章）を使うための受け口。2026-11-19。
/// 色・数・形は MacUi の物だけを使う（ここに色やドットの数を書かない）。担当2 の絵は文字の格子（BrowserDevGrid）で組むので、
/// MacUi の絵を格子に写す・MacUi の Stamp* に格子の Put を渡す、の2つでつなぐ。明るい・暗いは bool dark
/// </summary>
public static class BrowserDevMac
{
    public static MacLook Look(bool dark) => dark ? MacLook.Dark : MacLook.Light;

    // ───────── 色と数（MacUi の物） ─────────
    public static Color32 Light5 => MacUi.Light5;                       // 明るい窓のふち（足場になった窓）
    public static Color32 WindowBg(bool dark) => MacUi.WindowBg(Look(dark));
    public static int Cut(int w) => MacUi.CornerFor(w);                 // 角を欠くドットの数
    public static int TitleHeight(int h) => MacUi.TitleHeightFor(h);
    public static int BodyTop(int h) => MacUi.HeaderH(h);                // 中の面の最初の行
    public static int LightsW => MacUi.LightD * 3 + MacUi.LightGap * 2; // 信号機の幅（11）

    /// <summary>角の丸い四角（w×h、角を cut 段けずる）の中か</summary>
    public static bool Inside(int w, int h, int cut, int x, int y) => MacUi.InRound(x, y, w, h, cut);

    // ───────── 窓（1-2） ─────────

    /// <summary>
    /// 窓（w×h。MacUi.WindowSprite）を、格子の (ox, oy) に写す：角の丸いふち・タイトルバー・左上の信号機・まん中の題名の棒。右上は空。
    /// edge＝ふちの色（敵の攻撃は赤。無ければ MacUi のふちの色）。中身は、このあとに上から描く
    /// </summary>
    public static void StampWindow(BrowserDevGrid g, int ox, int oy, int w, int h, bool dark, Color32? edge = null)
    {
        CopyInto(g, MacUi.WindowSprite(w, h, Look(dark), edge), ox, oy);
    }

    /// <summary>絵（実行中に作った、読める絵）を格子に写す。透明な所は写さない</summary>
    static void CopyInto(BrowserDevGrid g, Sprite s, int ox, int oy)
    {
        Texture2D t = s.texture;
        Rect r = s.textureRect;
        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        Color32[] px = t.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[(y0 + h - 1 - y) * t.width + x0 + x]; // テクスチャは下の行から
                if (c.a > 0) g.Put(ox + x, oy + y, c);
            }
    }

    /// <summary>中の面（タイトルバーの下から、ふちの内側）を字 c で塗る（広告の地など、中身の色）</summary>
    public static void FillBody(BrowserDevGrid g, int ox, int oy, int w, int h, char c)
    {
        int r = Mathf.Max(0, Cut(w) - 1);
        for (int y = BodyTop(h); y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                if (MacUi.InRound(x - 1, y - 1, w - 2, h - 2, r)) g.Set(ox + x, oy + y, c);
    }

    /// <summary>窓の形の白い絵（光る面・残像用）。frameOnly なら、ふちとタイトルバーだけ</summary>
    public static Sprite ShapeSprite(int w, int h, bool frameOnly)
    {
        int cut = Cut(w);
        if (!frameOnly) return MacUi.RoundRectSprite(w, h, cut);
        var g = new BrowserDevGrid(w, h);
        int th = TitleHeight(h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (MacUi.InRound(x, y, w, h, cut) && (y <= th || !MacUi.InRound(x - 1, y - 1, w - 2, h - 2, Mathf.Max(0, cut - 1)))) g.Set(x, y, 'X');
        return BrowserDevFx.Art($"MacFrame_{w}_{h}", g, new Dictionary<char, Color32> { { 'X', new Color32(255, 255, 255, 255) } });
    }

    /// <summary>窓の影（MacUi の影：同じ形を下へ 2ドット）。窓より 1つ後ろの順番に出す</summary>
    public static SpriteRenderer Shadow(Transform parent, Vector2 windowCenter, int w, int h, bool dark, int order)
    {
        SpriteRenderer sr = BrowserDevFx.Show(parent, MacUi.ShadowSprite(w, h), windowCenter + new Vector2(0f, -MacUi.ShadowY * BrowserDevFx.Dot), order, 1f, "Shadow");
        sr.color = MacUi.ShadowColor(Look(dark));
        return sr;
    }

    // ───────── 信号機（1-2） ─────────

    /// <summary>信号機だけの絵（11×3。帯の上などに重ねる。角は 5割すける）</summary>
    public static Sprite LightsSprite()
    {
        var g = new BrowserDevGrid(LightsW, MacUi.LightD);
        MacUi.StampLights(g.Put, 0, 0);
        return BrowserDevFx.Art("MacLights", g, null);
    }

    /// <summary>小さい窓（幅 30ドットより小さい）の信号機：1ドットの点 3つ（x、x＋2、x＋4）</summary>
    public static void StampDots(BrowserDevGrid g, int x, int y) => MacUi.StampLightsTiny(g.Put, x, y);

    /// <summary>赤い丸のまん中（窓のまん中からの位置。マス）</summary>
    public static Vector2 RedLightLocal(int w, int h)
    {
        int th = TitleHeight(h);
        return w < MacUi.TinyWindowW
            ? MacUi.DotToLocal(w, h, 2.5f, 1f + (th - 1) / 2 + 0.5f)
            : MacUi.DotToLocal(w, h, MacUi.LightX + MacUi.LightD * 0.5f, 1f + (th - MacUi.LightD) / 2 + MacUi.LightD * 0.5f);
    }

    /// <summary>
    /// 閉じる動き（MacUi.CloseAnim）：左上の赤い丸が 0.05秒へこむ → 窓がまん中へ縮みながら消える（0.1秒）。右上へ向かって縮む動きは、やめた。
    /// window＝今の窓の絵（w×h ドット、まん中が at）。shadow なら影も付けて一緒に縮める。出した物を返す（呼ぶ側が攻撃した人に預ける）
    /// </summary>
    public static GameObject CloseAnim(Vector2 at, float angle, Sprite window, int w, int h, int order, bool shadow, bool dark)
    {
        GameObject go = BrowserDevFx.Loose("MacClose", at, angle);
        BrowserDevFx.Show(go.transform, window, Vector2.zero, order, 1f, "Window");
        if (shadow) Shadow(go.transform, Vector2.zero, w, h, dark, order - 1);
        MacUi.CloseAnim(go.transform, null, RedLightLocal(w, h));
        // 念のため：MacUi.CloseAnim が動かなかったときも残らないように、長めの時間で消す（ふだんは 0.15秒で MacUi が消す）
        BrowserDevFx.Animate(go, 1f, (e, k) => { });
        return go;
    }
}

/// <summary>【仮】文字の並び（ドット絵）を組む道具。y は上から。'.' は透明</summary>
public sealed class BrowserDevGrid
{
    readonly char[,] _g;
    public int W { get; }
    public int H { get; }

    public BrowserDevGrid(int w, int h)
    {
        W = Mathf.Max(1, w);
        H = Mathf.Max(1, h);
        _g = new char[H, W];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                _g[y, x] = '.';
    }

    public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

    public void Set(int x, int y, char c)
    {
        if (In(x, y)) _g[y, x] = c;
    }

    public char Get(int x, int y) => In(x, y) ? _g[y, x] : '.';

    // 色で置いた所の字（U+0100 から。パレットの英字・数字とぶつけない）。2026-11-19
    readonly Dictionary<uint, char> _autoIndex = new Dictionary<uint, char>();
    readonly Dictionary<char, Color32> _autoPalette = new Dictionary<char, Color32>();

    /// <summary>色で置いた所の字と色（BrowserDevFx.Art がパレットに足す）</summary>
    public IReadOnlyDictionary<char, Color32> AutoPalette => _autoPalette;

    /// <summary>
    /// 1ドットを色で置く（Mac の部品の道具は Action&lt;int,int,Color32&gt; で描くので、その受け口。MacUi.StampLights などにも、そのまま渡せる）。
    /// 透明（a＝0）なら何もしない。すけた色は、下に色で置いた物と混ぜる
    /// </summary>
    public void Put(int x, int y, Color32 c)
    {
        if (!In(x, y)) return;
        if (c.a == 0) return;
        // すけた色は、下に色で置いた物があれば混ぜる（MacUi の信号機の角の 5割などを、そのまま受けられるように）
        if (c.a < 255)
        {
            Color32 under = ColorAt(x, y);
            if (under.a > 0)
            {
                float t = c.a / 255f;
                c = new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(under.r, c.r, t)), (byte)Mathf.RoundToInt(Mathf.Lerp(under.g, c.g, t)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(under.b, c.b, t)), (byte)Mathf.RoundToInt(under.a + (255 - under.a) * t));
            }
        }
        uint k = ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        if (!_autoIndex.TryGetValue(k, out char ch))
        {
            ch = (char)(0x100 + _autoIndex.Count);
            _autoIndex[k] = ch;
            _autoPalette[ch] = c;
        }
        _g[y, x] = ch;
    }

    /// <summary>色で置いた色（無ければ透明）</summary>
    public Color32 ColorAt(int x, int y) => _autoPalette.TryGetValue(Get(x, y), out Color32 c) ? c : new Color32(0, 0, 0, 0);

    public void Rect(int x, int y, int w, int h, char c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                Set(x + i, y + j, c);
    }

    public void Frame(int x, int y, int w, int h, char c)
    {
        for (int i = 0; i < w; i++)
        {
            Set(x + i, y, c);
            Set(x + i, y + h - 1, c);
        }
        for (int j = 0; j < h; j++)
        {
            Set(x, y + j, c);
            Set(x + w - 1, y + j, c);
        }
    }

    /// <summary>円（左上 x, y、直径 d）</summary>
    public void Disc(int x, int y, int d, char c)
    {
        float r = d * 0.5f;
        for (int j = 0; j < d; j++)
            for (int i = 0; i < d; i++)
            {
                float dx = i + 0.5f - r, dy = j + 0.5f - r;
                if (dx * dx + dy * dy <= r * r + 0.3f) Set(x + i, y + j, c);
            }
    }

    /// <summary>輪（左上 x, y、直径 d、太さ t）</summary>
    public void Ring(int x, int y, int d, int t, char c)
    {
        float r = d * 0.5f;
        for (int j = 0; j < d; j++)
            for (int i = 0; i < d; i++)
            {
                float dx = i + 0.5f - r, dy = j + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r + 0.25f && dist > r - t - 0.25f) Set(x + i, y + j, c);
            }
    }

    /// <summary>文字の並びを貼る（'.' と ' ' は貼らない）。recolor があれば、全部その字にする</summary>
    public void Stamp(string[] rows, int x, int y, char recolor = '\0')
    {
        for (int j = 0; j < rows.Length; j++)
        {
            string r = rows[j];
            for (int i = 0; i < r.Length; i++)
            {
                char ch = r[i];
                if (ch == '.' || ch == ' ') continue;
                Set(x + i, y + j, recolor != '\0' ? recolor : ch);
            }
        }
    }

    /// <summary>
    /// 描いた所のまわり（透明な所）に、ふちを付ける。diagonal なら斜めのとなりも見る（8方向）
    /// </summary>
    public void Outline(char edge, bool diagonal = false)
    {
        var add = new List<Vector2Int>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (_g[y, x] != '.') continue;
                bool near = Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1);
                if (!near && diagonal) near = Filled(x - 1, y - 1) || Filled(x + 1, y - 1) || Filled(x - 1, y + 1) || Filled(x + 1, y + 1);
                if (near) add.Add(new Vector2Int(x, y));
            }
        foreach (var p in add) _g[p.y, p.x] = edge;
    }

    bool Filled(int x, int y) => In(x, y) && _g[y, x] != '.';

    /// <summary>ある字を、別の字に置き換える</summary>
    public void Replace(char from, char to)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (_g[y, x] == from) _g[y, x] = to;
    }

    public string[] Rows()
    {
        var rows = new string[H];
        var line = new char[W];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++) line[x] = _g[y, x];
            rows[y] = new string(line);
        }
        return rows;
    }

    public BrowserDevGrid Clone()
    {
        var c = new BrowserDevGrid(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                c._g[y, x] = _g[y, x];
        foreach (var kv in _autoIndex) c._autoIndex[kv.Key] = kv.Value;
        foreach (var kv in _autoPalette) c._autoPalette[kv.Key] = kv.Value;
        return c;
    }
}

/// <summary>
/// 【仮】小さいドット絵の字（大文字・数字は 3×5 くらい、小文字は x の高さ 4。下に出る字のために、高さは 6）。
/// 0.4マスより小さい飾りの字に使う（0-1：それより小さい字はドット絵で描く）
/// </summary>
public static class BrowserDevFont
{
    public const int Height = 6;

    static readonly Dictionary<char, string> Raw = new Dictionary<char, string>
    {
        { 'A', ".#./#.#/###/#.#/#.#" }, { 'B', "##./#.#/##./#.#/##." }, { 'C', ".##/#../#../#../.##" },
        { 'D', "##./#.#/#.#/#.#/##." }, { 'E', "###/#../##./#../###" }, { 'F', "###/#../##./#../#.." },
        { 'G', ".##/#../#.#/#.#/.##" }, { 'H', "#.#/#.#/###/#.#/#.#" }, { 'I', "###/.#./.#./.#./###" },
        { 'J', "..#/..#/..#/#.#/.#." }, { 'K', "#.#/#.#/##./#.#/#.#" }, { 'L', "#../#../#../#../###" },
        { 'M', "#...#/##.##/#.#.#/#...#/#...#" }, { 'N', "#..#/##.#/#.##/#..#/#..#" }, { 'O', ".##./#..#/#..#/#..#/.##." },
        { 'P', "##./#.#/##./#../#.." }, { 'Q', ".##./#..#/#..#/#.##/.###" }, { 'R', "##./#.#/##./#.#/#.#" },
        { 'S', ".##/#../.#./..#/##." }, { 'T', "###/.#./.#./.#./.#." }, { 'U', "#.#/#.#/#.#/#.#/###" },
        { 'V', "#.#/#.#/#.#/#.#/.#." }, { 'W', "#...#/#...#/#.#.#/##.##/#...#" }, { 'X', "#.#/#.#/.#./#.#/#.#" },
        { 'Y', "#.#/#.#/.#./.#./.#." }, { 'Z', "###/..#/.#./#../###" },
        { '0', "###/#.#/#.#/#.#/###" }, { '1', ".#./##./.#./.#./###" }, { '2', "##./..#/.#./#../###" },
        { '3', "##./..#/.#./..#/##." }, { '4', "#.#/#.#/###/..#/..#" }, { '5', "###/#../##./..#/##." },
        { '6', ".##/#../###/#.#/###" }, { '7', "###/..#/.#./.#./.#." }, { '8', "###/#.#/###/#.#/###" },
        { '9', "###/#.#/###/..#/##." },
        { '!', "#/#/#/./#" }, { ',', "../../../../.#/#." }, { '.', "./././././#" }, { '?', "##./..#/.#./.../.#." },
        { '-', ".../.../###/.../..." }, { '+', ".../.#./###/.#./..." }, { '/', "..#/..#/.#./#../#.." },
        { ':', "./#/./#/." }, { ' ', "../../../../.." }, { '(', ".#/#./#./#./.#" }, { ')', "#./.#/.#/.#/#." },
        { '{', ".##/.#./#../.#./.##" }, { '}', "##./.#./..#/.#./##." }, { ';', "../.#/../.#/#." },
        { '<', "..#/.#./#../.#./..#" }, { '>', "#../.#./..#/.#./#.." }, { '=', ".../###/.../###/..." },
        { '%', "#.#/..#/.#./#../#.#" }, { '@', ".##./#..#/#.##/#.##/#.../.##." },
        { 'a', ".../.##/#.#/#.#/.##" }, { 'b', "#../##./#.#/#.#/##." }, { 'c', ".../.##/#../#../.##" },
        { 'd', "..#/.##/#.#/#.#/.##" }, { 'e', ".../.#./###/#../.##" }, { 'f', ".##/#../##./#../#.." },
        { 'g', ".../.##/#.#/.##/..#/##." }, { 'h', "#../##./#.#/#.#/#.#" }, { 'i', "#/./#/#/#" },
        { 'k', "#../#.#/##./#.#/#.#" }, { 'l', "#/#/#/#/#" }, { 'm', "...../####./#.#.#/#.#.#/#.#.#" },
        { 'n', ".../##./#.#/#.#/#.#" }, { 'o', ".../.#./#.#/#.#/.#." }, { 'p', ".../##./#.#/#.#/##./#.." },
        { 'r', ".../#.#/##./#../#.." }, { 's', ".../.##/#../..#/##." }, { 't', ".#./###/.#./.#./..#" },
        { 'u', ".../#.#/#.#/#.#/.##" }, { 'v', ".../#.#/#.#/#.#/.#." }, { 'w', "...../#...#/#...#/#.#.#/.#.#." },
        { 'x', ".../#.#/.#./.#./#.#" }, { 'y', ".../#.#/#.#/.##/..#/##." },
    };

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>();

    static string[] Glyph(char c)
    {
        if (Glyphs.TryGetValue(c, out string[] g)) return g;
        if (!Raw.TryGetValue(c, out string raw))
        {
            char up = char.ToUpperInvariant(c);
            if (up != c && Raw.TryGetValue(up, out raw)) { }
            else raw = Raw['?'];
        }
        string[] rows = raw.Split('/');
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var full = new string[Height];
        for (int i = 0; i < Height; i++) full[i] = i < rows.Length ? rows[i].PadRight(w, '.') : new string('.', w);
        Glyphs[c] = full;
        return full;
    }

    /// <summary>文字の幅（ドット。字の間は1ドット）</summary>
    public static int Width(string text)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        int w = 0;
        for (int i = 0; i < text.Length; i++) w += Glyph(text[i])[0].Length + (i > 0 ? 1 : 0);
        return w;
    }

    /// <summary>字を貼る（左上 x, y）</summary>
    public static void Stamp(BrowserDevGrid g, string text, int x, int y, char c)
    {
        if (string.IsNullOrEmpty(text)) return;
        int cx = x;
        foreach (char ch in text)
        {
            string[] rows = Glyph(ch);
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++)
                    if (rows[j][i] == '#') g.Set(cx + i, y + j, c);
            cx += rows[0].Length + 1;
        }
    }
}

/// <summary>【仮】動きを付ける部品（毎フレーム tick を呼び、時間が来たら自分の物を消す）</summary>
public sealed class BrowserDevAnim : MonoBehaviour
{
    float _born;
    float _life;
    bool _unscaled;
    Action<float, float> _tick;

    float Now => _unscaled ? Time.unscaledTime : Time.time;

    public void Setup(float life, Action<float, float> tick, bool unscaled = false)
    {
        _unscaled = unscaled;
        _born = Now;
        _life = Mathf.Max(0.01f, life);
        _tick = tick;
        Step();
    }

    void Update() => Step();

    void Step()
    {
        float e = Now - _born;
        try
        {
            _tick?.Invoke(e, Mathf.Clamp01(e / _life));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"BrowserDevAnim：{ex.Message}");
            _tick = null;
        }
        if (e >= _life) Destroy(gameObject);
    }
}

/// <summary>【仮】かけら（重力で落ちながら、うすくなって消える）</summary>
public sealed class BrowserDevShards : MonoBehaviour
{
    struct Piece
    {
        public Transform T;
        public SpriteRenderer R;
        public Vector2 V;
        public float Life;
        public float Spin;
        public Color C;
    }

    readonly List<Piece> _pieces = new List<Piece>();
    float _born = -1f;
    public float Gravity = 26f;
    /// <summary>実時間で動かす（当たった絵のかけら）</summary>
    public bool Unscaled;

    float Now => Unscaled ? Time.unscaledTime : Time.time;

    public void Add(Sprite s, Color c, Vector2 v, float life, float spin, int order)
    {
        SpriteRenderer sr = RuntimeSprites.Show(transform, s, Vector2.zero, order, 1f, "Shard");
        sr.color = c;
        _pieces.Add(new Piece { T = sr.transform, R = sr, V = v, Life = life, Spin = spin, C = c });
    }

    void Update()
    {
        if (_born < 0f) _born = Now;
        float e = Now - _born;
        float dt = Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        bool alive = false;
        for (int i = 0; i < _pieces.Count; i++)
        {
            Piece p = _pieces[i];
            if (p.T == null) continue;
            if (e >= p.Life)
            {
                Destroy(p.T.gameObject);
                p.T = null;
                _pieces[i] = p;
                continue;
            }
            alive = true;
            p.V.y -= Gravity * dt;
            p.T.localPosition += (Vector3)(p.V * dt);
            if (p.Spin != 0f) p.T.localRotation = Quaternion.Euler(0f, 0f, p.Spin * e);
            float k = e / p.Life;
            Color c = p.C;
            c.a *= k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            p.R.color = c;
            _pieces[i] = p;
        }
        if (!alive || e > 2f) Destroy(gameObject);
    }
}

/// <summary>
/// 【仮】攻撃の物（Target）に付けて、絵を持つ部品の土台。SkinDresser.Dress が場面ごとに何回呼んでも、1つだけ付ける。
/// 続く動きは、攻撃の部品の公開された状態（落ちているか・判定が出ているか・速さ）と、今の絵の enabled（呼んだ側がかくすのは forceRenderingOff）を
/// 毎フレーム読んで動かす。場面の知らせ（予告・本体・着地・消える）は、それぞれ1回だけの演出に使う
/// </summary>
public abstract class BrowserDevDriver : MonoBehaviour
{
    public DressInfo Info;
    protected float Born;
    /// <summary>いちばん新しい場面の今の絵（予告なら予告の絵、本体なら本体の絵）</summary>
    protected SpriteRenderer Current;
    /// <summary>予告の場面の今の絵（影・点線の枠・床の赤丸）</summary>
    protected SpriteRenderer TelegraphSource;
    protected bool Told, Activated, Landed, Vanished;
    protected float ActiveAt = -1f;
    bool _ok;

    public int Index => Info.Index;

    /// <summary>Target にこの部品を付けて（付いていればそれを使って）、場面を知らせる。付けられなければ null</summary>
    public static T Claim<T>(DressInfo info, DressPhase phase) where T : BrowserDevDriver
    {
        if (info.Target == null) return null;
        T d = info.Target.GetComponent<T>();
        if (d == null)
        {
            d = info.Target.AddComponent<T>();
            d.Init(info);
        }
        if (!d._ok) return null;
        d.Step(phase, info);
        return d;
    }

    void Init(DressInfo info)
    {
        Info = info;
        Born = Time.time;
        Current = info.CurrentVisual != null ? info.CurrentVisual : BrowserDevFx.FindVisual(info.Target);
        try
        {
            Build();
            _ok = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{GetType().Name}：絵を作れません（{e}）");
            enabled = false;
        }
    }

    /// <summary>絵を作る（最初の1回。どの場面から来ても呼ばれる）</summary>
    protected abstract void Build();

    void Step(DressPhase p, DressInfo info)
    {
        if (info.CurrentVisual != null) Current = info.CurrentVisual;
        if (info.Duration > 0f) Info.Duration = info.Duration;
        if (info.Direction.sqrMagnitude > 0.0001f) Info.Direction = info.Direction;
        try
        {
            switch (p)
            {
                case DressPhase.Telegraph:
                    if (Told) return;
                    Told = true;
                    TelegraphSource = info.CurrentVisual;
                    OnTelegraph(info);
                    break;
                case DressPhase.Active:
                    if (Activated) return;
                    Activated = true;
                    ActiveAt = Time.time;
                    OnActive(info);
                    break;
                case DressPhase.Landed:
                    if (Landed) return;
                    Landed = true;
                    OnLanded(info);
                    break;
                case DressPhase.Vanish:
                    if (Vanished) return;
                    Vanished = true;
                    OnVanish(info);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{GetType().Name}（{p}）：{e}");
        }
    }

    protected virtual void OnTelegraph(DressInfo info) { }
    protected virtual void OnActive(DressInfo info) { }
    protected virtual void OnLanded(DressInfo info) { }
    protected virtual void OnVanish(DressInfo info) { }

    /// <summary>今の絵が出ている状態か（出る時刻・最後の点滅を、今の絵とそろえる）。今の絵が無ければ true</summary>
    protected bool SourceVisible => Current == null || Current.enabled;

    /// <summary>敵＝赤いふち、プレイヤー＝白いふち（0-3）</summary>
    protected Color EdgeColor => Info.IsEnemy ? BrowserDevFx.EnemyEdge : BrowserDevFx.White;

    /// <summary>Target の子でない飾りを、攻撃した人に預ける（形態変化・正常化で消える）</summary>
    protected GameObject Adopt(GameObject fx)
    {
        if (fx != null) SkinDresser.Adopt(Info, fx);
        return fx;
    }

    /// <summary>
    /// 今の絵（呼んだ側がかくした物）の写しを、その子に作る（影・点線の枠は「今のまま」出すため）。
    /// 元の絵と一緒に動き、一緒に消える。表示のオン・オフは MirrorEnabled でそろえる
    /// </summary>
    protected static SpriteRenderer Replicate(SpriteRenderer src, string name, Transform parent = null)
    {
        if (src == null) return null;
        var go = new GameObject(name);
        go.layer = src.gameObject.layer;
        if (parent == null)
        {
            go.transform.SetParent(src.transform, false);
        }
        else
        {
            // 元の絵が先に消されても残るように、別の親に付ける（位置・回転・大きさは元のまま）
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            Vector3 ls = src.transform.lossyScale, ps = parent.lossyScale;
            go.transform.localScale = new Vector3(ls.x / Mathf.Max(0.0001f, ps.x), ls.y / Mathf.Max(0.0001f, ps.y), 1f);
        }
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.drawMode = src.drawMode;
        if (src.drawMode != SpriteDrawMode.Simple)
        {
            sr.tileMode = src.tileMode;
            sr.size = src.size;
        }
        sr.color = src.color;
        sr.flipX = src.flipX;
        sr.flipY = src.flipY;
        sr.sortingOrder = src.sortingOrder;
        sr.enabled = src.enabled;
        return sr;
    }
}
