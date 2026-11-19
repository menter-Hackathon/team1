using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】担当4 のスキン（Generic の枠・Creative・Finder・Premiere・AI・Media）で使い回す、絵と動きの道具（2026-11-18。docs/app-skill-visuals-design.md 0〜1章）。
// ・絵はコードの中のドット絵（1マス＝18ドット）。形は白で作り、色は SpriteRenderer.color で付ける（色ごとに絵を作らない）。
// ・絵の名前（キャッシュのキー）は "Gen4_" で始める（ほかの担当の絵とぶつけない）。
// ・型の名前は Generic で始める（スキンの名前で始める決まり）。GenericSkin の partial（担当1 の Hits）とは別の型にする。

/// <summary>【仮】絵の道具（色・形・置き方・かけら）</summary>
public static class GenericFrameKit
{
    public const float Dot = 1f / 18f;

    // ───────── 共通の色（1-6）【仮】 ─────────
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color Yellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color WinLight = RuntimeSprites.Hex("F2F2F7");
    public static readonly Color WinLightShade = RuntimeSprites.Hex("C7C7CC");
    public static readonly Color WinDark = RuntimeSprites.Hex("1E1E1E");
    public static readonly Color WinDarkTitle = RuntimeSprites.Hex("3A3A3A");
    public static readonly Color WinDarkEdge = RuntimeSprites.Hex("5A5A5E");
    public static readonly Color NotifBg = RuntimeSprites.Hex("1C1C1E"); // 2026-11-19：Generic の通知は明るい地（GenericMac.NotifLight）にした。今は使っていない
    public static readonly Color LightR = RuntimeSprites.Hex("FF5F57");
    public static readonly Color LightY = RuntimeSprites.Hex("FEBC2E");
    public static readonly Color LightG = RuntimeSprites.Hex("28C840");
    public static readonly Color Grey = RuntimeSprites.Hex("8E8E93");
    static readonly Color FallbackMain = RuntimeSprites.Hex("707784");

    // ───────── 重なる順番（1-6） ─────────
    public const int OrderFloor = 6, OrderTelegraph = 17, OrderWindow = 19, OrderCore = 20, OrderTrail = 21, OrderDeco = 23;
    public const int OrderPillar = 41, OrderSpark = 45, OrderHit = 47, OrderShard = 48, OrderRing = 49, OrderMark = 51;

    static readonly Color32 W32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', W32 } };
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    // ───────── 色 ─────────

    static readonly Dictionary<Sprite, Color[]> IconColors = new Dictionary<Sprite, Color[]>();

    /// <summary>アプリの代表の色（色の組が無ければアイコンから。どちらも無ければ青みのある灰色）</summary>
    public static Color Main(AppTheme theme, Sprite icon)
    {
        if (theme != null) return Opaque(theme.Main);
        Color[] c = ColorsOfIcon(icon);
        return c != null ? c[0] : FallbackMain;
    }

    /// <summary>アプリの差し色の i 番目（無ければ代表の色を明るくした色）</summary>
    public static Color Accent(AppTheme theme, Sprite icon, int i)
    {
        Color[] acc = theme != null ? theme.Accents : ColorsOfIcon(icon);
        if (acc == null || acc.Length == 0) return Color.Lerp(Main(theme, icon), Color.white, 0.35f);
        Color c = Opaque(acc[Mathf.Abs(i) % acc.Length]);
        // 代表の色と同じ色なら、少し明るくして見分ける
        if (acc.Length == 1 && i % 2 == 1) c = Color.Lerp(c, Color.white, 0.4f);
        return c;
    }

    static Color[] ColorsOfIcon(Sprite icon)
    {
        if (icon == null) return null;
        if (IconColors.TryGetValue(icon, out Color[] c)) return c;
        try
        {
            PixelIcon.ExtractColors(icon, out Color main, out Color[] accents);
            var list = new List<Color> { main };
            if (accents != null) list.AddRange(accents);
            c = list.ToArray();
        }
        catch (Exception)
        {
            c = new[] { FallbackMain };
        }
        IconColors[icon] = c;
        return c;
    }

    public static Color Opaque(Color c)
    {
        c.a = 1f;
        return c;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    /// <summary>明るさを1段ずつ変える（連続ピラーの何本目か）</summary>
    public static Color Step(Color c, int index) => index <= 0 ? c : Color.Lerp(c, Color.white, Mathf.Min(0.6f, 0.16f * index));

    static readonly Dictionary<int, Sprite> PlaceholderIcons = new Dictionary<int, Sprite>();

    /// <summary>アプリのアイコン（無ければ、代表の色の仮のアイコン）</summary>
    public static Sprite IconOf(Sprite icon, Color main)
    {
        if (icon != null) return icon;
        int key = ((int)(main.r * 255) << 16) | ((int)(main.g * 255) << 8) | (int)(main.b * 255);
        if (PlaceholderIcons.TryGetValue(key, out Sprite s) && s != null) return s;
        s = PixelIcon.Placeholder(main, 16, $"Gen4_Icon_{key:x6}");
        PlaceholderIcons[key] = s;
        return s;
    }

    // ───────── 形（白。色は sr.color で付ける） ─────────

    /// <summary>白い絵を関数から作る（f(x, y) の y は上から。'X' が白、'.' が透明）。同じ key は1回だけ作る</summary>
    public static Sprite Make(string key, int w, int h, Func<int, int, bool> on)
    {
        string k = "Gen4_" + key;
        if (Cache.TryGetValue(k, out Sprite s) && s != null) return s;
        var rows = new string[h];
        var chars = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) chars[x] = on(x, y) ? 'X' : '.';
            rows[y] = new string(chars);
        }
        s = RuntimeSprites.FromAscii(k, rows, Mono);
        Cache[k] = s;
        return s;
    }

    /// <summary>色の付いた絵を、文字の並びから作る（行の長さはそろえること）</summary>
    public static Sprite Art(string key, string[] rows, Dictionary<char, Color32> palette, Vector2? pivot = null)
    {
        string k = "Gen4_" + key;
        if (Cache.TryGetValue(k, out Sprite s) && s != null) return s;
        s = RuntimeSprites.FromAscii(k, rows, palette, pivot);
        Cache[k] = s;
        return s;
    }

    /// <summary>白い1色の絵を、文字の並びから作る（'.' 以外が白）</summary>
    public static Sprite Glyph(string key, string[] rows)
    {
        string k = "Gen4_" + key;
        if (Cache.TryGetValue(k, out Sprite s) && s != null) return s;
        int h = rows.Length, w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var fixedRows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var chars = new char[w];
            for (int x = 0; x < w; x++) chars[x] = x < rows[y].Length && rows[y][x] != '.' && rows[y][x] != ' ' ? 'X' : '.';
            fixedRows[y] = new string(chars);
        }
        s = RuntimeSprites.FromAscii(k, fixedRows, Mono);
        Cache[k] = s;
        return s;
    }

    /// <summary>角の丸い四角（w×h、角を r 段けずる）の中か</summary>
    public static bool InRound(int x, int y, int w, int h, int r)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
        return dx + dy >= r;
    }

    /// <summary>角の丸い四角（w×h ドット、角を r 段けずる）</summary>
    public static Sprite RoundRect(int w, int h, int r) => Make($"RR_{w}_{h}_{r}", w, h, (x, y) => InRound(x, y, w, h, r));

    /// <summary>角の丸い四角のふち（太さ1ドット）</summary>
    public static Sprite RoundEdge(int w, int h, int r) => Make($"RE_{w}_{h}_{r}", w, h, (x, y) =>
        InRound(x, y, w, h, r) && (!InRound(x - 1, y, w, h, r) || !InRound(x + 1, y, w, h, r) || !InRound(x, y - 1, w, h, r) || !InRound(x, y + 1, w, h, r)));

    /// <summary>点線の四角のふち（2ドット描いて2ドットあける）</summary>
    public static Sprite DashedEdge(int w, int h) => Make($"DE_{w}_{h}", w, h, (x, y) =>
    {
        bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
        return edge && ((x + y) / 2) % 2 == 0;
    });

    /// <summary>白い円（直径 d ドット）</summary>
    public static Sprite Disc(int d) => FinderArt.Disc(Mathf.Max(2, d));

    /// <summary>白い輪（直径 d、太さ t ドット）</summary>
    public static Sprite Ring(int d, int t) => FinderArt.Ring(Mathf.Max(2, d), Mathf.Max(1, t));

    /// <summary>読み込みの輪（4分の3だけの輪。直径 d ドット）</summary>
    public static Sprite Spinner(int d) => Make($"Spin_{d}", d, d, (x, y) =>
    {
        float r = d * 0.5f, dx = x + 0.5f - r, dy = r - (y + 0.5f);
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        if (dist > r || dist < r - 2.1f) return false;
        float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        return a < 0f || a > 90f; // 右上の4分の1をあける
    });

    /// <summary>4方向のきらめき（✦。大きさ d ドット、奇数）</summary>
    public static Sprite Sparkle(int d)
    {
        d |= 1;
        int c = d / 2;
        return Make($"Spark2_{d}", d, d, (x, y) =>
        {
            // 4つの先がとがった星（辺が内側にへこむ形）：√|x| + √|y| ≦ √c
            int ax = Mathf.Abs(x - c), ay = Mathf.Abs(y - c);
            return Mathf.Sqrt(ax) + Mathf.Sqrt(ay) <= Mathf.Sqrt(c) + 0.2f;
        });
    }

    /// <summary>赤い「！」の三角の形（9×8）。中の「！」は透明</summary>
    public static Sprite WarnTriangle => Glyph("WarnTri", new[]
    {
        "....X....",
        "...XXX...",
        "...X.X...",
        "..XX.XX..",
        "..XX.XX..",
        ".XXXXXXX.",
        ".XXX.XXX.",
        "XXXXXXXXX",
    });

    /// <summary>×（5×5）</summary>
    public static Sprite Cross5 => Glyph("Cross5", new[] { "X...X", ".X.X.", "..X..", ".X.X.", "X...X" });

    /// <summary>チェック（7×6）</summary>
    public static Sprite Check7 => Glyph("Check7", new[] { "......X", ".....XX", "X...XX.", "XX.XX..", ".XXX...", "..X...." });

    /// <summary>小さいハート（7×6）</summary>
    public static Sprite Heart7 => Glyph("Heart7", new[] { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." });

    /// <summary>白い四角（1×1 ドット。伸ばして使う）</summary>
    public static Sprite Pixel => Make("Px", 1, 1, (x, y) => true);

    // ───────── 置き方 ─────────

    /// <summary>空の子を作る（レイヤーは親と同じ）。位置は親からの位置</summary>
    public static Transform Node(Transform parent, string name, Vector2 localPos)
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

    /// <summary>どこにも付けない物を作る（当たった絵・消える演出。自分で消えるようにすること）</summary>
    public static Transform Loose(string name, Vector2 worldPos)
    {
        var go = new GameObject(name);
        go.transform.position = worldPos;
        return go.transform;
    }

    /// <summary>絵を1枚置く（元の大きさ × scale）</summary>
    public static SpriteRenderer Spr(Transform parent, Sprite sprite, Vector2 localPos, Color color, int order, float scale = 1f, string name = "Spr")
    {
        SpriteRenderer sr = RuntimeSprites.Show(parent, sprite, localPos, order, scale, name);
        sr.color = color;
        return sr;
    }

    /// <summary>絵を、決めた大きさ（マス）に合わせて置く</summary>
    public static SpriteRenderer SprSized(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Spr")
    {
        SpriteRenderer sr = RuntimeSprites.Show(parent, sprite, localPos, order, 1f, name);
        Vector2 native = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.0001f), size.y / Mathf.Max(native.y, 0.0001f), 1f);
        sr.color = color;
        return sr;
    }

    /// <summary>四角を1つ置く（位置・大きさはマス）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localPos, Vector2 size, Color color, int order, string name = "Box")
        => SprSized(parent, Pixel, localPos, size, color, order, name);

    /// <summary>置いた絵の大きさ（マス）を変える（絵の元の大きさに合わせて伸ばす）</summary>
    public static void SetSize(SpriteRenderer sr, Vector2 size)
    {
        if (sr == null || sr.sprite == null) return;
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.0001f), size.y / Mathf.Max(native.y, 0.0001f), 1f);
    }

    /// <summary>四角を1つ置く（位置・大きさはドット）</summary>
    public static SpriteRenderer BoxPx(Transform parent, float x, float y, float w, float h, Color color, int order, string name = "Box")
        => Box(parent, new Vector2(x, y) * Dot, new Vector2(w, h) * Dot, color, order, name);

    /// <summary>アイコンを px ドットの大きさで置く</summary>
    public static SpriteRenderer IconAt(Transform parent, Sprite icon, Vector2 localPos, int px, int order, float alpha = 1f, string name = "Icon")
    {
        float s = icon != null ? px * Dot / Mathf.Max(icon.bounds.size.x, 0.0001f) : 1f;
        return Spr(parent, icon, localPos, new Color(1f, 1f, 1f, alpha), order, s, name);
    }

    /// <summary>ふち付きの板（外側 w×h ドットをふちの色、中を fill の色）</summary>
    public static void Plate(Transform parent, Vector2 localPos, int w, int h, int r, Color edge, Color fill, int order, out SpriteRenderer outer, out SpriteRenderer inner)
    {
        outer = Spr(parent, RoundRect(w, h, r), localPos, edge, order, 1f, "PlateEdge");
        inner = Spr(parent, RoundRect(Mathf.Max(1, w - 2), Mathf.Max(1, h - 2), Mathf.Max(0, r - 1)), localPos, fill, order, 1f, "PlateFill");
        inner.sortingOrder = order;
        // 同じ順番で重なると前後が決まらないので、中を少しだけ手前にする
        inner.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
    }

    /// <summary>ゲームの世界に文字を出す（英字。高さはマス）</summary>
    public static TextMesh Text(Transform parent, string text, Vector2 localPos, float height, Color color, int order, bool english = true)
    {
        TextMesh tm = WorldText.Create(parent, text, height, color, order, english);
        tm.transform.localPosition = localPos;
        return tm;
    }

    public static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>点滅（最後の remain 秒のとき、0.08秒ごとに入れ替わる）</summary>
    public static bool BlinkOn(float remain, float window = 0.3f) => remain > window || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;

    /// <summary>持ち主の体の範囲（ボスは IWarpable、無ければ当たり判定・絵）</summary>
    public static Bounds BodyOf(Component owner)
    {
        if (owner == null) return new Bounds(Vector3.zero, Vector3.one);
        var w = owner.GetComponent<IWarpable>();
        if (w != null) return w.BodyBounds;
        foreach (var c in owner.GetComponents<Collider2D>())
        {
            if (c != null && c.enabled && !c.isTrigger) return c.bounds;
        }
        var sr = owner.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds;
        return new Bounds(owner.transform.position + new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0f));
    }

    /// <summary>相手の体の範囲（当たった絵を囲む枠用。大きすぎる時は切る）</summary>
    public static Bounds TargetBounds(GameObject target, Vector2 fallback)
    {
        if (target != null)
        {
            var w = target.GetComponentInParent<IWarpable>();
            if (w != null) return w.BodyBounds;
            foreach (var c in target.GetComponentsInParent<Collider2D>())
            {
                if (c != null && c.enabled && !c.isTrigger) return c.bounds;
            }
            var sr = target.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.bounds.size.x < 6f) return sr.bounds;
        }
        return new Bounds(fallback, new Vector3(0.8f, 0.8f, 0f));
    }

    // ───────── かけら ─────────

    /// <summary>
    /// かけらを n 個、向き dir の先へ扇状（±spread 度）に飛ばす。速さ 6〜10マス/秒・重力あり・0.3〜0.5秒（1-2）。
    /// sprite・color は番号から選ぶ
    /// </summary>
    public static void Shards(Vector2 at, Vector2 dir, int n, Func<int, Sprite> sprite, Func<int, Color> color, float spread = 40f, float scale = 1f, int order = OrderShard)
    {
        if (n <= 0) return;
        Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        for (int i = 0; i < n; i++)
        {
            float a = n > 1 ? Mathf.Lerp(-spread, spread, (i + UnityEngine.Random.Range(0.1f, 0.9f)) / n) : UnityEngine.Random.Range(-spread, spread);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, a) * d) * UnityEngine.Random.Range(6f, 10f);
            GenericFrameShard.Spawn(at, sprite(i), color(i), v, 22f, UnityEngine.Random.Range(0.3f, 0.5f), order, UnityEngine.Random.Range(-540f, 540f), scale);
        }
    }

    /// <summary>
    /// 攻撃データの名前から枠を決める（共通の表 SkinTables.FrameOf が None を返したとき用。その場で作る能力のひな形もふくむ）
    /// </summary>
    public static AttackFrame Resolve(AttackFrame frame, AttackData data)
    {
        if (frame != AttackFrame.None) return frame;
        if (data == null) return AttackFrame.None;
        string n = data.name;
        if (n.Contains("Bomb_Blast") || n.Contains("Nova")) return AttackFrame.Circle;
        if (n.Contains("TrapFloor") || n.Contains("RedWave")) return AttackFrame.Floor;
        if (n.Contains("TabShuriken") || n.Contains("Gen_Spread")) return AttackFrame.SmallShot;
        if (n.Contains("ErrorBullet")) return AttackFrame.LongShot;
        if (n.Contains("Bomb") || n.Contains("Gen_Trap") || n.Contains("Lob")) return AttackFrame.Thrown;
        if (n.Contains("PaintBucket") || n.Contains("Paint")) return AttackFrame.Paint;
        if (n.Contains("Breakpoint") || n.Contains("BarGraph") || n.Contains("Pillar")) return AttackFrame.Pillar;
        if (n.Contains("PopupAppear")) return AttackFrame.Window;
        if (n.Contains("ErrorWindowFall")) return AttackFrame.Falling;
        if (n.Contains("TabMultiply")) return AttackFrame.TopRow;
        if (n.Contains("Warp") || n.Contains("NewTab")) return AttackFrame.Warp;
        return AttackFrame.None;
    }

    /// <summary>弾の枠の「予告」が、弾そのものではなく撃つ人（構えの間）に来たか</summary>
    public static bool IsVolleyTelegraph(DressInfo info) => info.Target != null && info.Target.GetComponent<Projectile>() == null;

    /// <summary>予告で見せる数（呼んだ側の Count。無ければ攻撃データから）</summary>
    public static int VolleyCount(DressInfo info, bool perVolley)
    {
        AttackData d = info.Data;
        if (perVolley) return d != null ? Mathf.Max(1, d.ProjectileCount) : Mathf.Max(1, info.Count);
        if (info.Count > 0) return info.Count;
        return d != null ? Mathf.Max(1, d.ProjectileCount) * Mathf.Max(1, d.RepeatCount) : 2;
    }

    /// <summary>連射の回数（予告の段の数）</summary>
    public static int VolleyRows(DressInfo info) => info.Data != null ? Mathf.Clamp(info.Data.RepeatCount, 1, 3) : 1;
}

/// <summary>【仮】かけら1つ（飛んで、重力で落ちて、うすくなって消える）</summary>
public class GenericFrameShard : MonoBehaviour
{
    Vector2 _v;
    float _g, _start, _life, _spin;
    SpriteRenderer _sr;
    Color _c;

    public static GenericFrameShard Spawn(Vector2 pos, Sprite sprite, Color color, Vector2 velocity, float gravity, float life, int order, float spin = 0f, float scale = 1f)
    {
        Transform t = GenericFrameKit.Loose("Gen4_Shard", pos);
        t.localScale = new Vector3(scale, scale, 1f);
        var s = t.gameObject.AddComponent<GenericFrameShard>();
        s._sr = t.gameObject.AddComponent<SpriteRenderer>();
        s._sr.sprite = sprite != null ? sprite : GenericFrameKit.Pixel;
        s._sr.sortingOrder = order;
        s._sr.color = color;
        s._c = color;
        s._v = velocity;
        s._g = gravity;
        s._life = Mathf.Max(0.05f, life);
        s._spin = spin;
        s._start = Time.time;
        if (sprite == null) t.localScale = new Vector3(3f * scale, 3f * scale, 1f); // 1ドットの四角は 3ドットにする
        return s;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _v.y -= _g * dt;
        transform.position += (Vector3)(_v * dt);
        if (_spin != 0f) transform.Rotate(0f, 0f, _spin * dt);
        float k = (Time.time - _start) / _life;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Color c = _c;
        c.a *= k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        _sr.color = c;
    }
}

/// <summary>
/// 【仮】小さい動きの道具（大きさ・位置・濃さ・回転を、決めた時間で変える）。付けた物ごと消すこともできる。
/// 時間は Time.time（ヒットストップで止まる）
/// </summary>
public class GenericFrameTweens : MonoBehaviour
{
    public enum Ease { Linear, OutQuad, InQuad, OutBack, Pop }

    enum Kind { Scale, Move, Alpha, TextAlpha, Rotate, Call, Each }

    class Tw
    {
        public Kind K;
        public Transform T;
        public SpriteRenderer Sr;
        public TextMesh Tm;
        public Color TextColor;
        public Vector3 A, B;
        public float Start, Dur;
        public Ease E;
        public Action Act;
        public Action<float> Fn;
    }

    readonly List<Tw> _list = new List<Tw>();
    float _killAt = float.PositiveInfinity;

    /// <summary>go に付いている道具（無ければ足す）</summary>
    public static GenericFrameTweens Of(GameObject go)
    {
        var t = go.GetComponent<GenericFrameTweens>();
        if (t == null) t = go.AddComponent<GenericFrameTweens>();
        return t;
    }

    /// <summary>after 秒後に、この GameObject ごと消す</summary>
    public GenericFrameTweens Kill(float after)
    {
        _killAt = Mathf.Min(_killAt, Time.time + Mathf.Max(0f, after));
        return this;
    }

    public GenericFrameTweens Scale(Transform t, Vector3 from, Vector3 to, float dur, float delay = 0f, Ease e = Ease.OutQuad)
    {
        if (t == null) return this;
        t.localScale = from;
        return Add(new Tw { K = Kind.Scale, T = t, A = from, B = to, Dur = dur, Start = Time.time + delay, E = e });
    }

    /// <summary>0 → 115% → 100% で「パッ」と出す（0-2）</summary>
    public GenericFrameTweens Pop(Transform t, Vector3 size, float dur = 0.1f, float delay = 0f)
        => Scale(t, Vector3.zero, size, dur, delay, Ease.Pop);

    public GenericFrameTweens Move(Transform t, Vector3 fromLocal, Vector3 toLocal, float dur, float delay = 0f, Ease e = Ease.OutQuad)
    {
        if (t == null) return this;
        t.localPosition = fromLocal;
        return Add(new Tw { K = Kind.Move, T = t, A = fromLocal, B = toLocal, Dur = dur, Start = Time.time + delay, E = e });
    }

    public GenericFrameTweens Alpha(SpriteRenderer sr, float from, float to, float dur, float delay = 0f, Ease e = Ease.Linear)
    {
        if (sr == null) return this;
        GenericFrameKit.SetAlpha(sr, from);
        return Add(new Tw { K = Kind.Alpha, Sr = sr, A = new Vector3(from, 0f), B = new Vector3(to, 0f), Dur = dur, Start = Time.time + delay, E = e });
    }

    public GenericFrameTweens TextAlpha(TextMesh tm, Color color, float from, float to, float dur, float delay = 0f)
    {
        if (tm == null) return this;
        WorldText.SetColor(tm, GenericFrameKit.WithAlpha(color, color.a * from));
        return Add(new Tw { K = Kind.TextAlpha, Tm = tm, TextColor = color, A = new Vector3(from, 0f), B = new Vector3(to, 0f), Dur = dur, Start = Time.time + delay, E = Ease.Linear });
    }

    public GenericFrameTweens Rotate(Transform t, float fromDeg, float toDeg, float dur, float delay = 0f, Ease e = Ease.Linear)
    {
        if (t == null) return this;
        t.localRotation = Quaternion.Euler(0f, 0f, fromDeg);
        return Add(new Tw { K = Kind.Rotate, T = t, A = new Vector3(fromDeg, 0f), B = new Vector3(toDeg, 0f), Dur = dur, Start = Time.time + delay, E = e });
    }

    public GenericFrameTweens Call(float delay, Action act) => Add(new Tw { K = Kind.Call, Act = act, Dur = 0f, Start = Time.time + delay });

    /// <summary>dur 秒の間、毎フレーム fn(0〜1) を呼ぶ</summary>
    public GenericFrameTweens Each(float dur, Action<float> fn, float delay = 0f) => Add(new Tw { K = Kind.Each, Fn = fn, Dur = dur, Start = Time.time + delay });

    GenericFrameTweens Add(Tw tw)
    {
        _list.Add(tw);
        return this;
    }

    static float Apply(Ease e, float k)
    {
        switch (e)
        {
            case Ease.OutQuad: return 1f - (1f - k) * (1f - k);
            case Ease.InQuad: return k * k;
            case Ease.OutBack:
            {
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float x = k - 1f;
                return 1f + c3 * x * x * x + c1 * x * x;
            }
            case Ease.Pop: return k < 0.65f ? Mathf.Lerp(0f, 1.15f, k / 0.65f) : Mathf.Lerp(1.15f, 1f, (k - 0.65f) / 0.35f);
            default: return k;
        }
    }

    void Update()
    {
        float now = Time.time;
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            Tw tw = _list[i];
            if (now < tw.Start) continue;
            float k = tw.Dur > 0f ? Mathf.Clamp01((now - tw.Start) / tw.Dur) : 1f;
            float e = Apply(tw.E, k);
            switch (tw.K)
            {
                case Kind.Scale:
                    if (tw.T != null) tw.T.localScale = Vector3.LerpUnclamped(tw.A, tw.B, e);
                    break;
                case Kind.Move:
                    if (tw.T != null) tw.T.localPosition = Vector3.LerpUnclamped(tw.A, tw.B, e);
                    break;
                case Kind.Alpha:
                    GenericFrameKit.SetAlpha(tw.Sr, Mathf.LerpUnclamped(tw.A.x, tw.B.x, e));
                    break;
                case Kind.TextAlpha:
                    if (tw.Tm != null) WorldText.SetColor(tw.Tm, GenericFrameKit.WithAlpha(tw.TextColor, tw.TextColor.a * Mathf.LerpUnclamped(tw.A.x, tw.B.x, e)));
                    break;
                case Kind.Rotate:
                    if (tw.T != null) tw.T.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(tw.A.x, tw.B.x, e));
                    break;
                case Kind.Call:
                    tw.Act?.Invoke();
                    break;
                case Kind.Each:
                    tw.Fn?.Invoke(k);
                    break;
            }
            if (k >= 1f) _list.RemoveAt(i);
        }
        if (now >= _killAt) Destroy(gameObject);
    }
}

/// <summary>
/// 【仮】攻撃の物（弾・指定位置の判定・落下物）に付けて、場面ごとに絵を変える部品の土台（1-1 の Dress）。
/// ・同じ物に場面ごとに何回か呼ばれる。同じ場面が2回来ても1回だけ動く。予告を飛ばして本体から来てもよい
/// ・絵は全部 Target の子（Root）に作る（形態変化・正常化で Target ごと消える）。消える場面では Root を外して、自分で消えるようにする
/// </summary>
public abstract class GenericFrameFx : MonoBehaviour
{
    protected DressInfo Info;
    protected AttackFrame Frame;
    protected DressPhase Phase;
    protected float PhaseStart;
    /// <summary>今の場面が続く秒数（呼んだ側が入れた DressInfo.Duration。0＝分からない）</summary>
    protected float PhaseDuration;
    protected Transform Root;
    /// <summary>ふち（敵＝赤、プレイヤー＝白）</summary>
    protected Color Edge;
    /// <summary>アプリの代表の色</summary>
    protected Color Main;
    /// <summary>敵なら赤、プレイヤーならアプリの色（0-3「赤い部分 → アプリの色」）</summary>
    protected Color Tint;
    protected Sprite Icon;
    /// <summary>LateUpdate で絵の向きをまっすぐに直す（回る弾の上でも、絵は回さない）</summary>
    protected bool Upright;
    /// <summary>LateUpdate で、左へ進むときに上下を返す（さかさまにしない）</summary>
    protected bool FlipWhenLeft;

    bool _built, _hasPhase;
    bool _landedShown;

    protected bool IsLanded => _landedShown;

    /// <summary>target に部品 T を付けて（付いていればそれを使って）、場面を知らせる</summary>
    public static bool Apply<T>(AttackFrame frame, DressPhase phase, DressInfo info) where T : GenericFrameFx
    {
        if (info.Target == null) return false;
        T fx = info.Target.GetComponent<T>();
        if (fx == null) fx = info.Target.AddComponent<T>();
        fx.Handle(frame, phase, info);
        return true;
    }

    void Handle(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        Info = info;
        Frame = frame;
        if (!_built)
        {
            _built = true;
            Edge = info.IsEnemy ? GenericFrameKit.Red : Color.white;
            Main = GenericFrameKit.Main(info.Theme, info.AppIcon);
            Tint = info.IsEnemy ? GenericFrameKit.Red : Main;
            Icon = GenericFrameKit.IconOf(info.AppIcon, Main);
            Root = GenericFrameKit.Node(transform, "Gen4_SkinArt", Vector2.zero);
            Build();
        }
        if (_hasPhase && phase == Phase) return;
        _hasPhase = true;
        Phase = phase;
        PhaseStart = Time.time;
        PhaseDuration = info.Duration;
        switch (phase)
        {
            case DressPhase.Telegraph: OnTelegraph(); break;
            case DressPhase.Active: OnActive(); break;
            case DressPhase.Landed:
                if (!_landedShown)
                {
                    _landedShown = true;
                    OnLanded();
                }
                break;
            case DressPhase.Vanish: OnVanish(); break;
        }
    }

    /// <summary>最初に1回（場面より前）。Root はできている</summary>
    protected virtual void Build() { }
    protected virtual void OnTelegraph() { }
    protected virtual void OnActive() { }
    protected virtual void OnLanded() { }
    /// <summary>消える：ふつうは、うすくなって消える</summary>
    protected virtual void OnVanish() => FadeAway(0.12f);
    /// <summary>毎フレーム。t は今の場面になってからの秒数</summary>
    protected virtual void Tick(float t) { }

    protected virtual void Update()
    {
        if (_built && _hasPhase && Root != null) Tick(Time.time - PhaseStart);
    }

    protected virtual void LateUpdate()
    {
        if (Root == null) return;
        if (Upright) Root.rotation = Quaternion.identity;
        else if (FlipWhenLeft)
        {
            bool left = transform.right.x < -0.01f;
            Root.localScale = new Vector3(1f, left ? -1f : 1f, 1f);
        }
    }

    /// <summary>絵の根元を Target から外す（消える演出用）。life 秒後に消える。外した根元を返す（無ければ null）</summary>
    protected Transform Detach(float life)
    {
        if (Root == null) return null;
        Transform r = Root;
        Root = null;
        r.SetParent(null, true);
        GenericFrameTweens.Of(r.gameObject).Kill(life);
        SkinDresser.Adopt(Info, r.gameObject); // 形態変化・正常化でも消えるように、攻撃した人に預ける
        return r;
    }

    /// <summary>外して、うすくしながら消す</summary>
    protected void FadeAway(float dur, float scaleTo = 1f)
    {
        Transform r = Detach(dur + 0.02f);
        if (r == null) return;
        var tw = GenericFrameTweens.Of(r.gameObject);
        foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, dur);
        foreach (var tm in r.GetComponentsInChildren<TextMesh>()) tm.gameObject.SetActive(false);
        if (scaleTo != 1f) tw.Scale(r, r.localScale, r.localScale * scaleTo, dur);
    }

    /// <summary>予告の秒数（呼んだ側が入れた秒数。無ければ攻撃データの予告・予備動作）</summary>
    protected float TelegraphTime => Phase == DressPhase.Telegraph && PhaseDuration > 0f ? PhaseDuration
        : Info.Data != null ? (Info.Data.TelegraphTime > 0f ? Info.Data.TelegraphTime : Info.Data.Windup) : 0.8f;

    /// <summary>弾が地形に当たっても消えない設定か（そのときは着地で絵をかくさない）</summary>
    protected bool KeepsFlyingAfterLanding => Info.Data != null && !Info.Data.DestroyOnTerrain;

    /// <summary>判定の秒数（持続があれば持続）</summary>
    protected float HitWindow => Info.Data != null ? Mathf.Max(0.05f, Info.Data.HitWindow) : 0.3f;

    /// <summary>判定の大きさ（マス）。来ていなければ攻撃データから</summary>
    protected Vector2 Size
    {
        get
        {
            Vector2 s = Info.HitSize;
            if (s.x > 0.01f && s.y > 0.01f) return s;
            if (Info.Data == null) return Vector2.one;
            switch (Info.Data.Kind)
            {
                case AttackKind.Area: return Info.Data.Shape == AreaShape.Circle ? new Vector2(Info.Data.AreaSize.x, Info.Data.AreaSize.x) : Info.Data.AreaSize;
                case AttackKind.Spawn: return Info.Data.ObjectSize;
                default: return Info.Data.HitSize;
            }
        }
    }

    /// <summary>判定の大きさ（ドット。四捨五入）</summary>
    protected int Px(float units) => Mathf.Max(1, Mathf.RoundToInt(units * 18f));
}

/// <summary>
/// 【仮】弾の枠の予告（構えの間、撃つ人の上に「これから何発来るか」を並べる。0-2 の 2）。
/// 撃つ人の子に作り、構えが終わるか、技が止められたら消える
/// </summary>
public class GenericFrameVolley : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    float _end;
    readonly List<SpriteRenderer> _edges = new List<SpriteRenderer>();

    /// <summary>build(根元, 番号) で1個ずつ絵を作る。並べる数は count。0.15秒ごとに1個ずつ出る</summary>
    public static GenericFrameVolley Show(DressInfo info, int count, float spacingUnits, bool above, Func<Transform, int, SpriteRenderer> build, int rows = 1)
    {
        if (info.Target == null || build == null) return null;
        string name = "Gen4_Volley";
        Transform old = info.Target.transform.Find(name);
        if (old != null) Destroy(old.gameObject);
        Bounds b = GenericFrameKit.BodyOf(info.Owner != null ? (Component)info.Owner : info.Target.transform);
        Vector2 at = above ? new Vector2(b.center.x, b.max.y + 0.45f) : new Vector2(b.max.x + 0.4f, b.center.y + 0.3f);
        Transform root = GenericFrameKit.Node(info.Target.transform, name, Vector2.zero);
        root.position = at;
        var v = root.gameObject.AddComponent<GenericFrameVolley>();
        v._owner = info.Owner;
        v._data = info.Data;
        float windup = info.Data != null ? Mathf.Max(0.2f, info.Data.Windup) : 0.8f;
        v._end = Time.time + windup;
        count = Mathf.Clamp(count, 1, 12);
        rows = Mathf.Clamp(rows, 1, 3);
        var tw = GenericFrameTweens.Of(root.gameObject);
        float step = Mathf.Min(0.15f, windup * 0.6f / (count * rows));
        int k = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int i = 0; i < count; i++, k++)
            {
                // 上に並べる：扇の形（連射は「もう1段」上に重ねる）。横に並べる：縦に1列
                Vector2 p = above
                    ? new Vector2((i - (count - 1) * 0.5f) * spacingUnits + r * 0.12f, Mathf.Abs(i - (count - 1) * 0.5f) * -0.12f + r * 0.45f)
                    : new Vector2(r * 0.45f, -i * spacingUnits);
                Transform slot = GenericFrameKit.Node(root, $"Slot_{k}", p);
                SpriteRenderer edge = build(slot, k);
                if (edge != null) v._edges.Add(edge);
                tw.Pop(slot, Vector3.one, 0.1f, k * step);
            }
        }
        return v;
    }

    void Update()
    {
        // 構えが終わった（撃った）・技が止められた
        if (Time.time >= _end || _owner == null || (_data != null && _owner.Current != _data))
        {
            Destroy(gameObject);
            return;
        }
        bool on = GenericFrameKit.BlinkOn(_end - Time.time);
        foreach (var e in _edges) if (e != null) e.enabled = on;
        transform.rotation = Quaternion.identity;
    }
}

/// <summary>
/// 【仮】1枚の絵に、毎フレーム自分でドットを描く（バケツ塗りの広がり・動く点線など、形が変わり続ける物用）。
/// 絵は物ごとに作るので、使い終わったら Dispose（GenericFrameCanvasOwner を付ければ、その GameObject と一緒に消える）
/// </summary>
public class GenericFrameCanvas
{
    public readonly int W, H;
    public readonly Texture2D Tex;
    public readonly Sprite Sprite;
    readonly Color32[] _px;

    public GenericFrameCanvas(int w, int h, string name)
    {
        W = Mathf.Max(1, w);
        H = Mathf.Max(1, h);
        Tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "Gen4_Canvas_" + name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        _px = new Color32[W * H];
        Tex.SetPixels32(_px);
        Tex.Apply(false);
        Sprite = Sprite.Create(Tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        Sprite.name = Tex.name;
        Sprite.hideFlags = HideFlags.DontSave;
    }

    public void Clear() => Array.Clear(_px, 0, _px.Length);

    /// <summary>ドットを1つ置く（y は上から）</summary>
    public void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        _px[(H - 1 - y) * W + x] = c;
    }

    public Color32 Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? default : _px[(H - 1 - y) * W + x];

    /// <summary>四角を塗る（y は上から）</summary>
    public void Fill(int x0, int y0, int w, int h, Color32 c)
    {
        for (int y = y0; y < y0 + h; y++)
        {
            for (int x = x0; x < x0 + w; x++) Set(x, y, c);
        }
    }

    public void Apply()
    {
        Tex.SetPixels32(_px);
        Tex.Apply(false);
    }

    public void Dispose()
    {
        if (Sprite != null) UnityEngine.Object.Destroy(Sprite);
        if (Tex != null) UnityEngine.Object.Destroy(Tex);
    }

    /// <summary>絵を置いて、その GameObject が消えるときに絵も消す</summary>
    public SpriteRenderer Show(Transform parent, Vector2 localPos, int order, string name = "Canvas")
    {
        SpriteRenderer sr = RuntimeSprites.Show(parent, Sprite, localPos, order, 1f, name);
        sr.gameObject.AddComponent<GenericFrameCanvasOwner>().Canvas = this;
        return sr;
    }
}

/// <summary>【仮】自分で描く絵を、GameObject と一緒に消す</summary>
public class GenericFrameCanvasOwner : MonoBehaviour
{
    public GenericFrameCanvas Canvas;

    void OnDestroy() => Canvas?.Dispose();
}

/// <summary>
/// 【仮】Mac の部品の、担当4 の持ち場（Generic の枠・Finder・Premiere・デスクトップの画面）用の入口（2026-11-19。docs/mac-look-design.md 1章）。
/// ・色・数・ウィンドウの絵・進行の輪・印は、担当1 の MacUi から取る（色やドットをここに書かない）。
/// ・MacUi に無い物だけ、ここで足す：板（地・ふち・影を別の絵にして、足場になったらふちの色を変える）・閉じる動き（外した根元に付ける）・
///   ゲームの世界のメニューバー（上の列の予告）・タイトルバーの高さを変えられる OnGUI のウィンドウ（日本語の題名が入る高さ）。
/// ・明るい・暗いは bool dark（MacLook に直して MacUi に渡す）
/// </summary>
public static class GenericMac
{
    // ───────── 色（1-1。MacUi から） ─────────
    public static Color Red => MacUi.Red;
    public static Color Blue => MacUi.Blue;
    public static Color Gray1 => MacUi.Gray1;
    public static Color Light1 => MacUi.Light1;
    public static Color Light3 => MacUi.Light3;
    public static Color Light4 => MacUi.Light4;
    public static Color Light5 => MacUi.Light5;
    /// <summary>字（明るい面）</summary>
    public static Color Ink => MacUi.Gray6;
    public static Color DarkEdge => MacUi.Edge(MacLook.Dark);
    public static Color DarkBg => MacUi.WindowBg(MacLook.Dark);
    public static Color LightR => MacUi.LightR;
    /// <summary>通知の見た目（MacUi.BannerDark で暗い地に切り替わる＝要相談 6）</summary>
    public static bool BannerDark => MacUi.BannerLook == MacLook.Dark;
    /// <summary>通知の地</summary>
    public static Color NotifLight => MacUi.BannerBg(MacUi.BannerLook);
    /// <summary>通知の題名の棒の色</summary>
    public static Color BannerInk => MacUi.Ink(MacUi.BannerLook);

    // ───────── 数（1-2。MacUi から） ─────────
    /// <summary>影：下へずらすドット</summary>
    public const int ShadowDrop = MacUi.ShadowY;
    /// <summary>進行の輪の 1周の秒数（8コマ）</summary>
    public static float SpinnerPeriod => 8f / MacUi.SpinnerFps;

    static MacLook LookOf(bool dark) => dark ? MacLook.Dark : MacLook.Light;

    /// <summary>角を欠くドット（幅 80 以上は 3、それより小さいと 2）</summary>
    public static int CornerOf(int w) => MacUi.CornerFor(w);
    /// <summary>タイトルバーの高さ（ウィンドウの高さが 20 より小さいと 5）</summary>
    public static int TitleHOf(int h) => MacUi.TitleHeightFor(h);
    /// <summary>小さいウィンドウ（幅 30 より小さい）は、信号機が 1ドットの点</summary>
    public static bool SmallLights(int w) => w < MacUi.TinyWindowW;

    // ───────── (a) 絵（MacUi から） ─────────

    static readonly Color32 Clear32 = new Color32(0, 0, 0, 0);

    /// <summary>
    /// ウィンドウの絵（MacUi.WindowSprite）：角の丸い地・1ドットのふち・タイトルバー（左上に信号機、まん中に題名の棒）・その下の線。
    /// edge：ふちの色（null＝決まりの色、透明＝ふちを描かない＝上に別のふちを重ねるとき）
    /// </summary>
    public static Sprite WindowSprite(int w, int h, bool dark, Color32? edge = null) => MacUi.WindowSprite(w, h, LookOf(dark), edge);

    /// <summary>赤い丸のまん中（ウィンドウの絵のまん中からのドット。y は上が +）。閉じる動きでへこませる所（MacUi の信号機の位置と同じ）</summary>
    public static Vector2 RedLightCenter(int w, int h)
    {
        int th = TitleHOf(h);
        if (SmallLights(w)) return new Vector2(2.5f - w * 0.5f, h * 0.5f - (1 + (th - 1) / 2 + 0.5f));
        float top = 1 + (th - MacUi.LightD) / 2;
        return new Vector2(MacUi.LightX + MacUi.LightD * 0.5f - w * 0.5f, h * 0.5f - (top + MacUi.LightD * 0.5f));
    }

    /// <summary>3×3 の丸（白。角は 5割）。色は sr.color で付ける</summary>
    public static Sprite Dot3 => GenericFrameKit.Art("MacDot3", new[] { "hwh", "www", "hwh" }, new Dictionary<char, Color32>
    {
        { 'w', new Color32(255, 255, 255, 255) },
        { 'h', new Color32(255, 255, 255, 128) },
    });

    /// <summary>進行の輪の1コマ（MacUi.Spinner。白で作るので sr.color で色を付ける＝灰 #8E8E93、敵は赤）。d は 8 か 16</summary>
    public static Sprite SpinnerFrame(int d, int frame)
    {
        Sprite[] frames = MacUi.Spinner(d >= 12 ? 16 : 8, Color.white);
        return frames[((frame % frames.Length) + frames.Length) % frames.Length];
    }

    /// <summary>進行の輪の、t 秒目のコマ</summary>
    public static int SpinnerIndex(float t) => Mathf.FloorToInt(Mathf.Max(0f, t) * MacUi.SpinnerFps) % 8;

    // ───────── (b) ゲームの世界に置く ─────────

    /// <summary>影（ウィンドウと同じ形。下へ 2ドット）を置く</summary>
    public static SpriteRenderer Shadow(Transform parent, Vector2 localPos, int w, int h, int r, int order, bool dark = false)
        => GenericFrameKit.Spr(parent, MacUi.ShadowSprite(w, h, r), localPos + new Vector2(0f, -ShadowDrop) * GenericFrameKit.Dot,
            MacUi.ShadowColor(LookOf(dark)), order, 1f, "Shadow");

    /// <summary>
    /// 角の丸い板（MacUi.PlaceBanner の板の部分）：影（shadow のとき）・地・1ドットのふち。edge が null ならふちは置かない（影だけ）。
    /// 返すのはふち（足場になったときに色を変える・消すのに使う）
    /// </summary>
    public static SpriteRenderer Plate(Transform parent, Vector2 localPos, int w, int h, int r, Color fill, Color? edge, int order, bool shadow, out SpriteRenderer fillSr)
    {
        if (shadow) Shadow(parent, localPos, w, h, r, order - 1, BannerDark);
        fillSr = GenericFrameKit.Spr(parent, GenericFrameKit.RoundRect(w, h, r), localPos, fill, order, 1f, "Fill");
        SpriteRenderer e = GenericFrameKit.Spr(parent, GenericFrameKit.RoundEdge(w, h, r), localPos, edge ?? Color.clear, order, 1f, "Edge");
        // 同じ順番で重なると前後が決まらないので、ふちを少しだけ手前にする
        e.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
        e.enabled = edge.HasValue;
        return e;
    }

    /// <summary>
    /// ウィンドウを置く（MacUi.PlaceWindow と同じ形。ふちを別の絵にした）：影・ウィンドウの絵（タイトルバー・信号機・題名の棒）・ふち。
    /// edge が null なら決まりのふちの色（敵は赤を入れる。足場になったら決まりの色に戻す）。返すのはふち
    /// </summary>
    public static SpriteRenderer PlaceWindow(Transform parent, Vector2 localPos, int w, int h, bool dark, Color? edge, int order, bool shadow = true)
    {
        int r = CornerOf(w);
        if (shadow) Shadow(parent, localPos, w, h, r, order - 1, dark);
        GenericFrameKit.Spr(parent, WindowSprite(w, h, dark, Clear32), localPos, Color.white, order, 1f, "Window");
        SpriteRenderer e = GenericFrameKit.Spr(parent, GenericFrameKit.RoundEdge(w, h, r), localPos, edge ?? MacUi.Edge(LookOf(dark)), order, 1f, "Edge");
        e.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
        return e;
    }

    /// <summary>
    /// 閉じる動き（MacUi.CloseAnim と同じ動き。GenericFrameFx.Detach で外した根元に付ける）：左上の赤い丸が 0.05秒へこむ → まん中へ縮みながら消える（0.1秒）。
    /// window は外した根元（自分で消える物。0.16秒より長く残すこと）、body はウィンドウの絵の親（まん中が絵のまん中）
    /// </summary>
    public static void CloseAnim(Transform window, Transform body, int w, int h, int order)
    {
        if (window == null) return;
        var tw = GenericFrameTweens.Of(window.gameObject);
        if (body != null) GenericFrameKit.Spr(body, Dot3, RedLightCenter(w, h) * GenericFrameKit.Dot, MacUi.LightRPressed, order, 1f, "Dent");
        Vector3 s = window.localScale;
        tw.Scale(window, s, s * 0.08f, 0.1f, 0.05f, GenericFrameTweens.Ease.InQuad);
        foreach (var sr in window.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.1f, 0.05f);
        foreach (var tm in window.GetComponentsInChildren<TextMesh>()) tm.gameObject.SetActive(false);
    }

    /// <summary>
    /// メニューバー（1-5。ゲームの世界）：横 widthUnits マス・高さ 0.5マス。左に印（MacUi.Mark）と棒 4本、右に小さい印 3つ（MacUi.StatusIcon）。
    /// alpha は全体の濃さ（予告は 0.4＝6割すかす）。parent の原点がまん中
    /// </summary>
    public static void MenuBarWorld(Transform parent, float widthUnits, float alpha, int order)
    {
        const float D = GenericFrameKit.Dot;
        float w = widthUnits / D; // ドット
        GenericFrameKit.Box(parent, Vector2.zero, new Vector2(widthUnits, 9f * D), new Color(Light1.r, Light1.g, Light1.b, 0.8f * alpha), order, "MenuBar");
        GenericFrameKit.BoxPx(parent, 0f, -4.5f, w, 1f, GenericFrameKit.WithAlpha(Light4, alpha), order, "Line");
        Color ink = GenericFrameKit.WithAlpha(Ink, alpha);
        float x = -w * 0.5f + 6f;
        GenericFrameKit.Spr(parent, MacUi.Mark(7), new Vector2(x + 3.5f, 0.5f) * D, ink, order + 1, 1f, "Mark");
        x += 7f + 6f;
        // 太字のアプリ名（長い棒）→ メニューの棒 3本
        float[] lens = { 14f, 9f, 9f, 11f };
        for (int i = 0; i < lens.Length; i++)
        {
            GenericFrameKit.BoxPx(parent, x + lens[i] * 0.5f, 0.5f, lens[i], i == 0 ? 3f : 2f, i == 0 ? ink : GenericFrameKit.WithAlpha(Ink, alpha * 0.7f), order + 1, "Menu");
            x += lens[i] + 6f;
        }
        // 右：Wi-Fi・電池・スイッチ（右から並べる）
        float rx = w * 0.5f - 6f;
        for (int i = 2; i >= 0; i--)
        {
            GenericFrameKit.Spr(parent, MacUi.StatusIcon(i), new Vector2(rx - 3.5f, 0.5f) * D, ink, order + 1, 1f, "Status");
            rx -= 7f + 5f;
        }
    }

    // ───────── (c) OnGUI（PixelGui の座標） ─────────

    /// <summary>【仮】OnGUI の Mac の部品。MacUi.Gui に無い物（上と下で角を変えられる塗り・タイトルバーの高さを変えられるウィンドウ）</summary>
    public static class Gui
    {
        /// <summary>角の丸い四角を塗る（角を top・bottom ドット欠く）。行ごとに塗るので、すけた色でも重ならない</summary>
        public static void RoundFill(Rect r, int top, int bottom, Color c)
        {
            float x = Mathf.Round(r.x), y = Mathf.Round(r.y), w = Mathf.Round(r.width), h = Mathf.Round(r.height);
            if (w <= 0f || h <= 0f) return;
            int lim = Mathf.FloorToInt(Mathf.Min(w, h) * 0.5f);
            top = Mathf.Clamp(top, 0, lim);
            bottom = Mathf.Clamp(bottom, 0, lim);
            for (int i = 0; i < top; i++) PixelGui.Fill(new Rect(x + (top - i), y + i, w - 2f * (top - i), 1f), c);
            PixelGui.Fill(new Rect(x, y + top, w, h - top - bottom), c);
            for (int i = 0; i < bottom; i++) PixelGui.Fill(new Rect(x + (bottom - i), y + h - 1f - i, w - 2f * (bottom - i), 1f), c);
        }

        /// <summary>
        /// ウィンドウ（MacUi.Gui.Window と同じ形で、タイトルバーの高さ titleH を変えられる。日本語の題名 12 は 14 が要る）：
        /// 影・角の丸い地・1ドットのふち・タイトルバー（左上に信号機、まん中に題名）・その下の線。中の面の四角を返す。
        /// 信号機は、タイトルバーが 12 以上なら直径 5（間 2）、それより低いと 3（MacUi の決まり）【仮】。dent＝0 なら赤い丸がへこんでいる
        /// </summary>
        public static Rect Window(Rect r, string title, bool dark, float titleH = 14f, int corner = 3, bool ja = false, int dent = -1, bool shadow = true)
        {
            r = new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));
            MacLook look = LookOf(dark);
            Color bar = MacUi.TitleBg(look);
            if (shadow) RoundFill(new Rect(r.x, r.y + ShadowDrop, r.width, r.height), corner, corner, MacUi.ShadowColor(look));
            // ふちの色で全体を塗ってから、内側を塗る（角のふちのドットが、ゲームの世界の絵と同じ形になる）
            RoundFill(r, corner, corner, MacUi.Edge(look));
            Rect inner = PixelGui.Inset(r, 1f);
            if (inner.width <= 0f || inner.height <= 0f) return inner;
            int ic = Mathf.Max(0, corner - 1);
            float th = Mathf.Min(Mathf.Round(titleH), inner.height);
            RoundFill(new Rect(inner.x, inner.y, inner.width, th), ic, 0, bar);
            float bodyY = inner.y + th;
            if (inner.yMax - bodyY >= 1f)
            {
                var lineRect = new Rect(inner.x, bodyY, inner.width, 1f);
                PixelGui.Fill(lineRect, bar);
                PixelGui.Fill(lineRect, MacUi.TitleLine(look));
                bodyY += 1f;
            }
            var body = new Rect(inner.x, bodyY, inner.width, Mathf.Max(0f, inner.yMax - bodyY));
            if (body.height > 0f) RoundFill(body, 0, Mathf.Min(ic, Mathf.FloorToInt(body.height)), MacUi.WindowBg(look));
            if (th >= 5f && inner.width >= MacUi.TinyWindowW)
            {
                int d = th >= 12f ? 5 : MacUi.LightD;
                int gap = d >= 5 ? 2 : MacUi.LightGap;
                float lx = r.x + (d >= 5 ? 6f : MacUi.LightX), ly = Mathf.Round(inner.y + (th - d) * 0.5f);
                MacUi.Gui.Lights(lx, ly, d, true, false, gap);
                if (dent == 0) MacUi.StampDisc((px, py, c) => PixelGui.Fill(new Rect(lx + px, ly + py, 1f, 1f), c), 0, 0, d, MacUi.LightRPressed);
            }
            if (!string.IsNullOrEmpty(title) && th >= 9f)
            {
                Color ink = MacUi.Ink(look);
                if (ja) PixelGui.Text(new Rect(inner.x, inner.y, inner.width, th), title, 12, ink, TextAnchor.MiddleCenter);
                else PixelGui.TextEn(new Rect(inner.x, Mathf.Round(inner.y + (th - 8f) * 0.5f), inner.width, 8f), title, 8, ink, TextAnchor.MiddleCenter);
            }
            return body;
        }

        /// <summary>題名の幅（日本語 12 か英字 8）</summary>
        public static float TitleWidth(string title, bool ja) => string.IsNullOrEmpty(title) ? 0f : Mathf.Ceil(PixelGui.Measure(title, PixelGui.Style(!ja, ja ? 12 : 8)));
    }
}
