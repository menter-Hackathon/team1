using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Finder の技と能力の色と絵（2026-11-18。docs/app-boss-skills-design.md 2章）。
/// 絵はコードの中のドット絵から作る（RuntimeSprites.FromAscii。1マス＝18ドット）。形は決まりに沿って、コードで並べる。
/// 点線・実線だけは並べて描く（SpriteDrawMode.Tiled）ので、ここで直接作る。
/// </summary>
public static class FinderArt
{
    /// <summary>1ドット（マス）</summary>
    public const float Dot = 1f / 18f;

    // ───────── 色【仮】 ─────────
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");        // タグの赤・固定した狙い
    public static readonly Color Blue = RuntimeSprites.Hex("0A84FF");       // タグの青・選択の四角
    public static readonly Color Cyan = RuntimeSprites.Hex("5AC8FA");       // 追っている狙い
    public static readonly Color BeamOuter = RuntimeSprites.Hex("FFF4B0");  // 光の帯の外側
    public static readonly Color BarFill = RuntimeSprites.Hex("2B2B2E");    // 検索バー
    public static readonly Color BarEdge = RuntimeSprites.Hex("5A5A5E");
    public static readonly Color RowSelect = RuntimeSprites.Hex("1F5FD1");  // 検索結果の選ばれた行
    public static readonly Color Paper = RuntimeSprites.Hex("F2F2F7");      // 書類の白
    public static readonly Color Grey = RuntimeSprites.Hex("8E8E93");       // トゲ・ファスナー

    /// <summary>タグの色（0＝赤、1＝青）</summary>
    public static Color TagColor(int tag) => tag == 1 ? Blue : Red;

    static readonly Color32 White32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', White32 } };

    // ───────── 線 ─────────

    static Sprite _dotted, _solid;

    /// <summary>点線（2ドット描いて2ドットあける。白。並べて描く用）</summary>
    public static Sprite DottedLine => _dotted != null ? _dotted : (_dotted = MakeStrip("Finder_LineDotted", new[] { true, true, false, false }));
    /// <summary>実線（白。並べて描く用）</summary>
    public static Sprite SolidLine => _solid != null ? _solid : (_solid = MakeStrip("Finder_LineSolid", new[] { true, true, true, true }));

    // 並べて描く絵は FullRect にする（Tight だと透明な所が切られて、並びがずれる）
    static Sprite MakeStrip(string name, bool[] on)
    {
        var tex = new Texture2D(on.Length, 1, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[on.Length];
        for (int i = 0; i < on.Length; i++) px[i] = on[i] ? White32 : new Color32(255, 255, 255, 0);
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, on.Length, 1f), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    // ───────── 丸 ─────────

    /// <summary>白い円（直径 d ドット）</summary>
    public static Sprite Disc(int d) => RuntimeSprites.FromAscii($"Finder_Disc_{d}", Circle(d, d), Mono);

    /// <summary>白い輪（直径 d ドット、太さ t ドット）</summary>
    public static Sprite Ring(int d, int t) => RuntimeSprites.FromAscii($"Finder_Ring_{d}_{t}", Circle(d, t), Mono);

    static string[] Circle(int d, int thickness)
    {
        d = Mathf.Max(2, d);
        var rows = new string[d];
        var chars = new char[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                chars[x] = dist <= r && dist > r - thickness - 0.25f ? 'X' : '.';
            }
            rows[y] = new string(chars);
        }
        return rows;
    }

    /// <summary>タグの丸（タグの色に、白いふち1ドット）。tag は 0＝赤、1＝青</summary>
    public static Sprite TagBall(int d, int tag)
    {
        d = Mathf.Max(3, d);
        var g = Grid(d, d);
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r) g[y, x] = dist > r - 1.25f ? 'R' : 'T';
            }
        }
        var pal = new Dictionary<char, Color32> { { 'T', (Color32)TagColor(tag) }, { 'R', White32 } };
        return RuntimeSprites.FromAscii($"Finder_TagBall_{d}_{tag}", Rows(g), pal);
    }

    // ───────── 検索バーの虫めがね（12ドット） ─────────

    static readonly string[] MagnifierRows =
    {
        "..MMMM......",
        ".MM..MM.....",
        "MM....MM....",
        "M......M....",
        "M......M....",
        "MM....MM....",
        ".MM..MM.....",
        "..MMMMM.....",
        "......MMM...",
        ".......MMM..",
        "........MMM.",
        ".........MM.",
    };

    static Sprite _magnifier;

    public static Sprite Magnifier => _magnifier != null ? _magnifier : (_magnifier = RuntimeSprites.FromAscii("Finder_Magnifier", MagnifierRows,
        new Dictionary<char, Color32> { { 'M', RuntimeSprites.Hex("A0A0A5") } }));

    // ───────── 書類（zip・散弾・トゲ付きファイル） ─────────

    static Dictionary<char, Color32> DocPalette(int tag = 0) => new Dictionary<char, Color32>
    {
        { 'O', RuntimeSprites.Hex("6E6E73") }, // ふち
        { 'W', (Color32)Paper },
        { 'F', RuntimeSprites.Hex("C7C7CC") }, // 折れた角
        { 'Z', (Color32)Grey },                // ファスナー・トゲ
        { 'T', (Color32)TagColor(tag) },
    };

    /// <summary>
    /// 書類の形を g に描く（左上 ox, oy、横 w、縦 h、右上の折れ c ドット）。zipper ならまん中に縦のファスナー
    /// </summary>
    static void DrawDoc(char[,] g, int ox, int oy, int w, int h, int c, bool zipper)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                char ch = x == 0 || y == 0 || x == w - 1 || y == h - 1 ? 'O' : 'W';
                int fx = x - (w - c); // 折れた角の中での横の位置
                if (fx >= 0 && y <= c)
                {
                    if (fx > y) ch = '.';                       // 切り落とした角
                    else if (fx == y) ch = 'O';                 // 斜めのふち
                    else if (y == c || fx == 0) ch = 'O';       // 折れた所のふち
                    else ch = 'F';
                }
                if (ch != '.') g[oy + y, ox + x] = ch;
            }
        }
        if (!zipper) return;
        int zx = (w - 1) / 2;
        for (int y = Mathf.Max(1, c / 2); y < h - 1; y++)
        {
            g[oy + y, ox + zx] = 'Z';
            int tooth = y % 2 == 0 ? zx - 1 : zx + 1;
            if (tooth > 0 && tooth < w - 1) g[oy + y, ox + tooth] = 'Z';
        }
    }

    /// <summary>zip の大きさ（縦のドット）。2026-11-19（docs/app-feedback-v2-design.md 4-2）：空 10・1個 13・2〜3個 16・4個以上 18</summary>
    public static int ZipHeight(int count) => ZipTierHeight(ZipTier(count));

    /// <summary>zip の段（0＝空、1＝1個、2＝2〜3個、3＝4個以上）。2026-11-19</summary>
    public static int ZipTier(int count) => count <= 0 ? 0 : count == 1 ? 1 : count <= 3 ? 2 : 3;

    /// <summary>段ごとの zip の縦のドット</summary>
    public static int ZipTierHeight(int tier) => tier <= 0 ? 10 : tier == 1 ? 13 : tier == 2 ? 16 : 18;

    static int ZipWidth(int h) => Mathf.RoundToInt(h * 0.8f);

    // 毎フレーム引くので、大きさごとに覚えておく（文字の並びを作り直さない）
    static readonly Sprite[] ZipCache = new Sprite[32];
    static readonly Sprite[] ZipIconCache = new Sprite[32];
    static readonly Sprite[] SpikeCache = new Sprite[2];
    static Sprite _pellet, _smartFolder;

    /// <summary>ゲームの世界に浮かぶ zip（縦 h ドット）</summary>
    public static Sprite Zip(int h)
    {
        h = Mathf.Clamp(h, 4, ZipCache.Length - 1);
        if (ZipCache[h] != null) return ZipCache[h];
        int w = ZipWidth(h);
        var g = Grid(w, h);
        DrawDoc(g, 0, 0, w, h, Mathf.Max(2, w / 4), true);
        return ZipCache[h] = RuntimeSprites.FromAscii($"Finder_Zip_{h}", Rows(g), DocPalette());
    }

    /// <summary>HUD の枠に出す zip。どの大きさも 18×18 の中に描く（枠の中で大きさの違いが見えるように）</summary>
    public static Sprite ZipIcon(int h)
    {
        const int canvas = 18;
        h = Mathf.Clamp(h, 4, canvas);
        if (ZipIconCache[h] != null) return ZipIconCache[h];
        int w = ZipWidth(h);
        var g = Grid(canvas, canvas);
        DrawDoc(g, (canvas - w) / 2, (canvas - h) / 2, w, h, Mathf.Max(2, w / 4), true);
        return ZipIconCache[h] = RuntimeSprites.FromAscii($"Finder_ZipIcon_{h}", Rows(g), DocPalette());
    }

    // ───────── 段ごとの zip（2026-11-19。docs/app-feedback-v2-design.md 4-2）【仮】 ─────────

    static readonly Sprite[] TierCache = new Sprite[4];
    static readonly Sprite[] TierIconCache = new Sprite[4];
    static readonly Sprite[] ShotCache = new Sprite[3];

    /// <summary>
    /// 段ごとの zip（ゲームの世界）。1＝ファスナーの上から書類の角が1枚のぞく、2＝ふくらんで書類が3枚のぞく、3＝同じで、ファスナーが白い。
    /// 上に書類がのぞく分、絵は zip より 4ドット高い（原点は zip の真ん中より少し上になる）
    /// </summary>
    public static Sprite ZipTierSprite(int tier)
    {
        tier = Mathf.Clamp(tier, 0, 3);
        if (TierCache[tier] != null) return TierCache[tier];
        return TierCache[tier] = DrawTier(tier, 0, $"Finder_ZipTier_{tier}");
    }

    /// <summary>HUD の枠に出す段ごとの zip（22×22 の中に描く）</summary>
    public static Sprite ZipTierIcon(int tier)
    {
        tier = Mathf.Clamp(tier, 0, 3);
        if (TierIconCache[tier] != null) return TierIconCache[tier];
        return TierIconCache[tier] = DrawTier(tier, 22, $"Finder_ZipTierIcon_{tier}");
    }

    static Sprite DrawTier(int tier, int canvas, string key)
    {
        int h = ZipTierHeight(tier);
        int w = ZipWidth(h) + (tier >= 2 ? 2 : 0); // ふくらむ
        const int peek = 4;
        int cw = canvas > 0 ? canvas : w;
        int ch = canvas > 0 ? canvas : h + peek;
        var g = Grid(cw, ch);
        int ox = (cw - w) / 2;
        int oy = canvas > 0 ? (ch - h) / 2 + peek / 2 : peek;
        // のぞく書類（zip の後ろ。上の端から 3〜4ドット出る）
        if (tier == 1) PeekDoc(g, ox + w - 6, oy - 3, 4, 6);
        if (tier >= 2)
        {
            PeekDoc(g, ox + 1, oy - 2, 4, 6);
            PeekDoc(g, ox + w / 2 - 2, oy - 4, 4, 7);
            PeekDoc(g, ox + w - 5, oy - 3, 4, 6);
        }
        DrawDoc(g, ox, oy, w, h, Mathf.Max(2, w / 4), true);
        var pal = DocPalette();
        if (tier >= 3) pal['Z'] = (Color32)Cyan; // ファスナーが光る【仮】（白は書類の地と見分けられないので、明るい水色）
        return RuntimeSprites.FromAscii(key, Rows(g), pal);
    }

    static void PeekDoc(char[,] g, int x0, int y0, int w, int h)
    {
        int gh = g.GetLength(0), gw = g.GetLength(1);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x0 + x, gy = y0 + y;
                if (gx < 0 || gy < 0 || gx >= gw || gy >= gh) continue;
                g[gy, gx] = x == 0 || y == 0 || x == w - 1 ? 'O' : 'W';
            }
        }
    }

    /// <summary>
    /// 弾幕の弾（2026-11-19）。0＝書類、1＝フォルダ（Finder の青）、2＝画像（山と太陽）。どれも 5〜6ドット
    /// </summary>
    public static Sprite Shot(int kind)
    {
        kind = Mathf.Clamp(kind, 0, 2);
        if (kind == 0) return Pellet;
        if (ShotCache[kind] != null) return ShotCache[kind];
        if (kind == 1)
        {
            return ShotCache[1] = RuntimeSprites.FromAscii("Finder_ShotFolder", new[]
            {
                "DDD...",
                "DDDDDD",
                "LLLLLL",
                "BBBBBB",
                "BBBBBB",
            }, new Dictionary<char, Color32> { { 'B', RuntimeSprites.Hex("5AB0F0") }, { 'D', RuntimeSprites.Hex("2F7FC4") }, { 'L', RuntimeSprites.Hex("8FCBF7") } });
        }
        return ShotCache[2] = RuntimeSprites.FromAscii("Finder_ShotImage", new[]
        {
            "OOOOOO",
            "OWWWYO",
            "OWGWWO",
            "OGGGWO",
            "OGGGGO",
            "OOOOOO",
        }, new Dictionary<char, Color32>
        {
            { 'O', RuntimeSprites.Hex("6E6E73") }, { 'W', (Color32)Paper }, { 'G', RuntimeSprites.Hex("34C759") }, { 'Y', RuntimeSprites.Hex("FFD60A") },
        });
    }

    /// <summary>空の zip の1発（白い書類、5ドット）。散弾の書類と同じ絵</summary>
    public static Sprite SingleShot => Pellet;

    /// <summary>散弾の小さい書類（5ドット）</summary>
    public static Sprite Pellet
    {
        get
        {
            if (_pellet != null) return _pellet;
            var g = Grid(4, 5);
            DrawDoc(g, 0, 0, 4, 5, 1, false);
            return _pellet = RuntimeSprites.FromAscii("Finder_Pellet", Rows(g), DocPalette());
        }
    }

    /// <summary>トゲ付きファイル（18×18。白い書類14ドットのまわりに灰色のトゲ、右下にタグの丸5ドット）。tag は 0＝赤、1＝青</summary>
    public static Sprite SpikeFile(int tag)
    {
        tag = tag == 1 ? 1 : 0;
        if (SpikeCache[tag] != null) return SpikeCache[tag];
        const int n = 18;
        var g = Grid(n, n);
        DrawDoc(g, 2, 2, 14, 14, 4, false);
        // トゲ：上下左右に4本ずつ（三角）
        int[] at = { 4, 7, 10, 13 };
        foreach (int p in at)
        {
            g[0, p] = 'Z'; g[1, p - 1] = 'Z'; g[1, p] = 'Z'; g[1, p + 1] = 'Z';                 // 上
            g[n - 1, p] = 'Z'; g[n - 2, p - 1] = 'Z'; g[n - 2, p] = 'Z'; g[n - 2, p + 1] = 'Z'; // 下
            g[p, 0] = 'Z'; g[p - 1, 1] = 'Z'; g[p, 1] = 'Z'; g[p + 1, 1] = 'Z';                 // 左
            g[p, n - 1] = 'Z'; g[p - 1, n - 2] = 'Z'; g[p, n - 2] = 'Z'; g[p + 1, n - 2] = 'Z'; // 右
        }
        // タグの丸（5ドット）
        string[] dot = { ".TTT.", "TTTTT", "TTTTT", "TTTTT", ".TTT." };
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (dot[y][x] == 'T') g[10 + y, 10 + x] = 'T';
            }
        }
        return SpikeCache[tag] = RuntimeSprites.FromAscii($"Finder_SpikeFile_{tag}", Rows(g), DocPalette(tag));
    }

    // ───────── スマートフォルダ（36×29。Finder の青いフォルダに白い歯車） ─────────

    static readonly string[] GearRows =
    {
        "...WWW...",
        ".W.WWW.W.",
        "..WWWWW..",
        "WWWW.WWWW",
        "WWW...WWW",
        "WWWW.WWWW",
        "..WWWWW..",
        ".W.WWW.W.",
        "...WWW...",
    };

    public static Sprite SmartFolder
    {
        get
        {
            if (_smartFolder != null) return _smartFolder;
            const int w = 36, h = 29;
            var g = Grid(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char ch = '.';
                    if (y <= 3 && x >= 1 && x <= 13 && !(y == 0 && (x == 1 || x == 13))) ch = 'D'; // つまみ
                    if (y >= 3) ch = 'D';                                                           // 後ろの面
                    if (y >= 7 && y <= h - 2) ch = y == 7 ? 'L' : 'B';                              // 前の面
                    bool corner = (y == 3 || y == h - 1) && (x == 0 || x == w - 1);
                    if (!corner && ch != '.') g[y, x] = ch;
                }
            }
            for (int y = 0; y < GearRows.Length; y++)
            {
                for (int x = 0; x < GearRows[y].Length; x++)
                {
                    if (GearRows[y][x] == 'W') g[13 + y, 13 + x] = 'W';
                }
            }
            var pal = new Dictionary<char, Color32>
            {
                { 'B', RuntimeSprites.Hex("5AB0F0") },
                { 'D', RuntimeSprites.Hex("2F7FC4") },
                { 'L', RuntimeSprites.Hex("8FCBF7") },
                { 'W', White32 },
            };
            return _smartFolder = RuntimeSprites.FromAscii("Finder_SmartFolder", Rows(g), pal);
        }
    }

    // ───────── 道具 ─────────

    static char[,] Grid(int w, int h)
    {
        var g = new char[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) g[y, x] = '.';
        }
        return g;
    }

    static string[] Rows(char[,] g)
    {
        int h = g.GetLength(0), w = g.GetLength(1);
        var rows = new string[h];
        var chars = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) chars[x] = g[y, x];
            rows[y] = new string(chars);
        }
        return rows;
    }

    /// <summary>子の GameObject を作る（レイヤーは親と同じ。位置はゲームの世界で指定）</summary>
    public static Transform Child(Transform parent, string objectName, Vector2 worldPosition)
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        return go.transform;
    }

    /// <summary>狙う相手（プレイヤー）の体（移動用の当たり判定）の範囲。相手がいなければ false</summary>
    public static bool TryGetTargetBounds(AttackRunner owner, out Bounds bounds)
    {
        bounds = default;
        if (owner == null || owner.Target == null || !owner.Target.gameObject.activeInHierarchy) return false;
        foreach (var col in owner.Target.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled)
            {
                bounds = col.bounds;
                return true;
            }
        }
        bounds = new Bounds(owner.Target.position + new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0f));
        return true;
    }

    static readonly Collider2D[] HurtBuffer = new Collider2D[8];

    /// <summary>
    /// 円の範囲が、相手（owner の当てる相手のレイヤーの被弾範囲）に重なっているか。
    /// ダメージのない弾（タグの弾）が、Hitbox を使わずにプレイヤーへの当たりを調べるのに使う
    /// </summary>
    public static bool TouchesTarget(AttackRunner owner, Vector2 center, float radius)
    {
        if (owner == null) return false;
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(owner.TargetLayers);
        int n = Physics2D.OverlapCircle(center, radius, filter, HurtBuffer);
        for (int i = 0; i < n; i++)
        {
            if (HurtBuffer[i] == null || !HurtBuffer[i].TryGetComponent(out Hurtbox hb)) continue;
            if (hb.Owner != null && hb.Owner.Team != owner.Team) return true;
        }
        return false;
    }
}
