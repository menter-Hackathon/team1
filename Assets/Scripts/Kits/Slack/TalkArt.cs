using System.Collections.Generic;
using UnityEngine;

// 【仮】キット1・担当1（Slack・LINE・メッセージ・Teams）で使い回す絵の道具（2026-11-19）。
// ・コードの中で点を打って絵を作る（TalkCanvas）。絵のファイルは足さない。1マス＝18ドット
// ・角の丸い四角・円・輪・吹き出し・チェック・ハート・親指など、チャットの絵の部品
// ・名前がぶつからないように Talk〜 で始める。ほかの担当のフォルダからは使わない

/// <summary>点を打って絵を作る板（左上が 0,0）</summary>
public sealed class TalkCanvas
{
    public readonly int W, H;
    readonly Color32[] _px;

    public TalkCanvas(int w, int h)
    {
        W = Mathf.Max(1, w);
        H = Mathf.Max(1, h);
        _px = new Color32[W * H];
    }

    /// <summary>1点（y は上から）</summary>
    public void Put(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        _px[(H - 1 - y) * W + x] = c;
    }

    /// <summary>四角をぬる</summary>
    public void Fill(int x, int y, int w, int h, Color32 c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Put(i, j, c);
    }

    /// <summary>角の丸い四角（r：角の丸さ）。edge があれば 1ドットのふち</summary>
    public void Round(int x, int y, int w, int h, int r, Color32 fill, Color32? edge = null)
    {
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                if (!InRound(i, j, w, h, r)) continue;
                bool border = edge.HasValue && (!InRound(i - 1, j, w, h, r) || !InRound(i + 1, j, w, h, r) || !InRound(i, j - 1, w, h, r) || !InRound(i, j + 1, w, h, r));
                Put(x + i, y + j, border ? edge.Value : fill);
            }
        }
    }

    static bool InRound(int i, int j, int w, int h, int r)
    {
        if (i < 0 || j < 0 || i >= w || j >= h) return false;
        r = Mathf.Clamp(r, 0, Mathf.Min(w, h) / 2);
        int cx = i < r ? r : i >= w - r ? w - r - 1 : i;
        int cy = j < r ? r : j >= h - r ? h - r - 1 : j;
        float dx = i - cx, dy = j - cy;
        return dx * dx + dy * dy <= r * r + 0.5f;
    }

    /// <summary>円（真ん中 cx,cy・半径 r。ドット）</summary>
    public void Disc(float cx, float cy, float r, Color32 c)
    {
        for (int j = Mathf.FloorToInt(cy - r - 1); j <= Mathf.CeilToInt(cy + r + 1); j++)
            for (int i = Mathf.FloorToInt(cx - r - 1); i <= Mathf.CeilToInt(cx + r + 1); i++)
            {
                float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) Put(i, j, c);
            }
    }

    /// <summary>輪（真ん中 cx,cy・半径 r・太さ t）</summary>
    public void Ring(float cx, float cy, float r, float t, Color32 c)
    {
        for (int j = Mathf.FloorToInt(cy - r - 1); j <= Mathf.CeilToInt(cy + r + 1); j++)
            for (int i = Mathf.FloorToInt(cx - r - 1); i <= Mathf.CeilToInt(cx + r + 1); i++)
            {
                float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= r && d > r - t) Put(i, j, c);
            }
    }

    /// <summary>文字の並びの絵を重ねる（'.' と ' ' は透明）</summary>
    public void Stamp(int x, int y, string[] rows, IReadOnlyDictionary<char, Color32> palette)
    {
        for (int j = 0; j < rows.Length; j++)
            for (int i = 0; i < rows[j].Length; i++)
            {
                char ch = rows[j][i];
                if (ch == '.' || ch == ' ' || !palette.TryGetValue(ch, out Color32 c)) continue;
                Put(x + i, y + j, c);
            }
    }

    /// <summary>文字の並びの絵を、scale 倍で重ねる（左上 x,y）</summary>
    public void StampScaled(int x, int y, string[] rows, IReadOnlyDictionary<char, Color32> palette, int scale)
    {
        scale = Mathf.Max(1, scale);
        for (int j = 0; j < rows.Length; j++)
            for (int i = 0; i < rows[j].Length; i++)
            {
                char ch = rows[j][i];
                if (ch == '.' || ch == ' ' || !palette.TryGetValue(ch, out Color32 c)) continue;
                Fill(x + i * scale, y + j * scale, scale, scale, c);
            }
    }

    /// <summary>スプライトにする（同じ key は使い回す）</summary>
    public Sprite ToSprite(string key, Vector2? pivot = null) => TalkArt.Make(key, this, pivot);

    internal Color32[] Pixels => _px;
}

/// <summary>チャットの絵の部品と色</summary>
public static class TalkArt
{
    /// <summary>1ドット（マス）</summary>
    public const float Px = 1f / 18f;

    public static readonly Color32 White = new Color32(255, 255, 255, 255);
    public static readonly Color32 Ink = new Color32(0x1C, 0x1C, 0x1E, 255);
    public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    /// <summary>危ない物の赤（#FF453A）</summary>
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color32 Grey = RuntimeSprites.Hex("8E8E93");
    public static readonly Color32 LightGrey = RuntimeSprites.Hex("E5E5EA");
    public static readonly Color32 Yellow = RuntimeSprites.Hex("FFCC00");
    public static readonly Color32 YellowDark = RuntimeSprites.Hex("E0A100");
    public static readonly Color32 Heart = RuntimeSprites.Hex("FF375F");

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>板からスプライトを作る（key ごとに1回）</summary>
    public static Sprite Make(string key, TalkCanvas c, Vector2? pivot = null)
    {
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false)
        {
            name = "Talk_" + key,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(c.Pixels);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, c.W, c.H), pivot ?? new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        s.name = key;
        s.hideFlags = HideFlags.DontSave;
        Cache[key] = s;
        return s;
    }

    /// <summary>作った絵があれば返す（無ければ null）</summary>
    public static Sprite Cached(string key) => Cache.TryGetValue(key, out Sprite s) ? s : null;

    // ───────── 基本の形 ─────────

    /// <summary>白い円（直径 d ドット）。色は SpriteRenderer で掛ける</summary>
    public static Sprite Disc(int d)
    {
        string key = "Disc_" + d;
        Sprite s = Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(d, d);
        c.Disc(d * 0.5f, d * 0.5f, d * 0.5f, White);
        return c.ToSprite(key);
    }

    /// <summary>白い輪（直径 d・太さ t ドット）</summary>
    public static Sprite Ring(int d, int t)
    {
        string key = $"Ring_{d}_{t}";
        Sprite s = Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(d, d);
        c.Ring(d * 0.5f, d * 0.5f, d * 0.5f, t, White);
        return c.ToSprite(key);
    }

    /// <summary>白い四角の枠（w×h ドット・太さ t）。dashed なら点線（4 描いて 3 あける）</summary>
    public static Sprite Frame(int w, int h, int t, bool dashed)
    {
        string key = $"Frame_{w}_{h}_{t}_{dashed}";
        Sprite s = Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(w, h);
        for (int x = 0; x < w; x++)
        {
            if (dashed && x % 7 >= 4) continue;
            c.Fill(x, 0, 1, t, White);
            c.Fill(x, h - t, 1, t, White);
        }
        for (int y = 0; y < h; y++)
        {
            if (dashed && y % 7 >= 4) continue;
            c.Fill(0, y, t, 1, White);
            c.Fill(w - t, y, t, 1, White);
        }
        return c.ToSprite(key);
    }

    /// <summary>吹き出し（w×h ドット。しっぽは下。tailRight：右下）</summary>
    public static void Bubble(TalkCanvas c, int x, int y, int w, int h, Color32 fill, Color32? edge, bool tailRight)
    {
        int bodyH = h - 4;
        c.Round(x, y, w, bodyH, Mathf.Min(6, bodyH / 2), fill, edge);
        int tx = tailRight ? x + w - 9 : x + 4;
        for (int i = 0; i < 4; i++)
        {
            int len = 4 - i;
            int sx = tailRight ? tx + i : tx + (4 - len) - i + 1;
            c.Fill(sx, y + bodyH - 1 + i, len, 1, fill);
            if (edge.HasValue)
            {
                c.Put(tailRight ? sx + len : sx - 1, y + bodyH - 1 + i, edge.Value);
            }
        }
    }

    // ───────── 小さい印 ─────────

    public static readonly Dictionary<char, Color32> IconPal = new Dictionary<char, Color32>
    {
        { 'Y', RuntimeSprites.Hex("FFCC00") }, { 'D', RuntimeSprites.Hex("C98A00") }, { 'K', RuntimeSprites.Hex("1C1C1E") },
        { 'W', new Color32(255, 255, 255, 255) }, { 'R', RuntimeSprites.Hex("FF375F") }, { 'r', RuntimeSprites.Hex("C21E43") },
        { 'G', RuntimeSprites.Hex("30D158") }, { 'B', RuntimeSprites.Hex("0A84FF") }, { 'S', RuntimeSprites.Hex("FF9F0A") },
        { 'P', RuntimeSprites.Hex("F2A9C4") }, { 'g', RuntimeSprites.Hex("8E8E93") },
    };

    /// <summary>親指（12×12。黄色）</summary>
    public static readonly string[] ThumbRows =
    {
        "....DD......",
        "...DYYD.....",
        "...DYYD.....",
        "...DYYD.....",
        "..DYYYDDDDD.",
        "DDDYYYYYYYYD",
        "DYDYYYYYYDDD",
        "DYDYYYYYYYYD",
        "DYDYYYYYYDDD",
        "DYDYYYYYYYYD",
        "DDDYYYYYYDD.",
        "...DDDDDDD..",
    };
    public static Sprite Thumb => RuntimeSprites.FromAscii("Talk_Thumb", ThumbRows, IconPal);

    /// <summary>ハート（11×10）</summary>
    public static readonly string[] HeartIconRows =
    {
        ".rr...rr...",
        "rRRr.rRRr..",
        "rRWRrRRRRr.",
        "rRRRRRRRRr.",
        "rRRRRRRRRr.",
        ".rRRRRRRr..",
        "..rRRRRr...",
        "...rRRr....",
        "....rr.....",
        "...........",
    };
    public static Sprite HeartIcon => RuntimeSprites.FromAscii("Talk_Heart", HeartIconRows, IconPal);

    /// <summary>にっこり顔（12×12）</summary>
    public static readonly string[] SmileRows =
    {
        "....DDDD....",
        "..DDYYYYDD..",
        ".DYYYYYYYYD.",
        ".DYYKYYKYYD.",
        "DYYYKYYKYYYD",
        "DYYYYYYYYYYD",
        "DYPYYYYYYPYD",
        "DYYKYYYYKYYD",
        ".DYYKKKKYYD.",
        ".DYYYYYYYYD.",
        "..DDYYYYDD..",
        "....DDDD....",
    };
    public static Sprite Smile => RuntimeSprites.FromAscii("Talk_Smile", SmileRows, IconPal);

    /// <summary>怒った顔（12×12。赤い）</summary>
    public static readonly string[] AngryRows =
    {
        "....rrrr....",
        "..rrSSSSrr..",
        ".rSSSSSSSSr.",
        ".rSKSSSSKSr.",
        "rSSSKSSKSSSr",
        "rSSKKSSKKSSr",
        "rSSSSSSSSSSr",
        "rSSSSSSSSSSr",
        ".rSSKKKKSSr.",
        ".rSKSSSSKSr.",
        "..rrSSSSrr..",
        "....rrrr....",
    };
    public static Sprite Angry => RuntimeSprites.FromAscii("Talk_Angry", AngryRows, IconPal);

    /// <summary>目（12×8。じっと見ている）</summary>
    public static readonly string[] EyesRows =
    {
        ".WWWW..WWWW.",
        "WWWWWWWWWWWW",
        "WWKKWWWWKKWW",
        "WWKKWWWWKKWW",
        "WWWWWWWWWWWW",
        ".WWWW..WWWW.",
        "............",
        "............",
    };
    public static Sprite Eyes => RuntimeSprites.FromAscii("Talk_Eyes", EyesRows, IconPal);

    /// <summary>クラッカー（12×12）</summary>
    public static readonly string[] PartyRows =
    {
        "........R..B",
        "..G...Y.....",
        ".......B..Y.",
        "...R........",
        ".....DD..G..",
        "....DYYD....",
        "...DYSYD..R.",
        "..DYYSYD....",
        ".DYSYYD.....",
        "DYYSYD......",
        "DSYYD.......",
        "DDDD........",
    };
    public static Sprite Party => RuntimeSprites.FromAscii("Talk_Party", PartyRows, IconPal);

    /// <summary>チェック（9×7。白）</summary>
    public static Sprite Check => RuntimeSprites.FromAscii("Talk_Check", new[]
    {
        "........W",
        ".......WW",
        "W.....WW.",
        "WW...WW..",
        ".WW.WW...",
        "..WWW....",
        "...W.....",
    }, IconPal);

    /// <summary>赤い「！」（4×10）</summary>
    public static Sprite Bang => RuntimeSprites.FromAscii("Talk_Bang", new[]
    {
        ".RR.",
        "RRRR",
        "RRRR",
        ".RR.",
        ".RR.",
        ".RR.",
        "....",
        ".RR.",
        ".RR.",
        "....",
    }, new Dictionary<char, Color32> { { 'R', RuntimeSprites.Hex("FF453A") } });

    /// <summary>手（挙手。12×14。黄色）</summary>
    public static readonly string[] HandRows =
    {
        "...D.D.D....",
        "..DYDYDYD...",
        "..DYDYDYD.D.",
        "..DYDYDYDDYD",
        "..DYDYDYDYYD",
        "..DYYYYYDYD.",
        "D.DYYYYYYYD.",
        "DDDYYYYYYYD.",
        "DYDYYYYYYYD.",
        ".DYYYYYYYYD.",
        "..DYYYYYYD..",
        "...DYYYYYD..",
        "...DYYYYD...",
        "....DDDD....",
    };
    public static Sprite Hand => RuntimeSprites.FromAscii("Talk_Hand", HandRows, IconPal);

    /// <summary>ヘッドホン（14×12。緑）</summary>
    public static readonly string[] HeadphonesRows =
    {
        "....GGGGGG....",
        "..GG......GG..",
        ".G..........G.",
        "G............G",
        "G............G",
        "G............G",
        "GGG........GGG",
        "GWGG......GGWG",
        "GWGG......GGWG",
        "GWGG......GGWG",
        "GGG........GGG",
        "..............",
    };
    public static Sprite Headphones => RuntimeSprites.FromAscii("Talk_Headphones", HeadphonesRows, IconPal);

    // ───────── 置く道具 ─────────

    /// <summary>子の入れ物を作る（ゲームの世界の位置）</summary>
    public static Transform Child(Transform parent, string name, Vector2 worldPos)
    {
        var go = new GameObject(name);
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        return go.transform;
    }

    /// <summary>絵を1枚、決めた大きさ（マス）で出す</summary>
    public static SpriteRenderer Show(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Sprite")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, sprite, color, size, order, name);
        sr.transform.localPosition = localPos;
        return sr;
    }

    /// <summary>絵を1枚、元の大きさ（1ドット＝1/18マス）× scale で出す</summary>
    public static SpriteRenderer ShowNative(Transform parent, Sprite sprite, Vector2 localPos, int order, float scale = 1f, string name = "Sprite")
        => RuntimeSprites.Show(parent, sprite, localPos, order, scale, name);

    /// <summary>色の濃さを変える</summary>
    public static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>文字（英字。高さはマス）</summary>
    public static TextMesh Text(Transform parent, string text, Vector2 localPos, float height, Color color, int order, bool english = true, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        TextMesh tm = WorldText.Create(parent, text, height, color, order, english, anchor);
        tm.transform.localPosition = localPos;
        return tm;
    }
}
