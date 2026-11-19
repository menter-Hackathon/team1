using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Gemini のキットの色・ドット絵・小さな道具（2026-11-19。docs/app-kits-1-design.md 7章）。担当3 の物。
/// 絵はコードの中のドット絵（RuntimeSprites.FromAscii）。絵のファイルは足さない。
/// 色：青 #4285F4・紫 #9B72CB・ピンク #D96570・白（どれも【仮】）
/// </summary>
public static class GeminiArt
{
    public const float Px = 1f / 18f;

    public static readonly Color Blue = RuntimeSprites.Hex("4285F4");
    public static readonly Color Purple = RuntimeSprites.Hex("9B72CB");
    public static readonly Color Pink = RuntimeSprites.Hex("D96570");
    public static readonly Color Ink = RuntimeSprites.Hex("1F1F1F");

    /// <summary>ごほうびの星の種類（ギミック「回答案を選ぶ」）</summary>
    public enum StarKind { Blue, Purple, Pink }

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'B', RuntimeSprites.Hex("4285F4") },
        { 'b', RuntimeSprites.Hex("8AB4F8") },
        { 'P', RuntimeSprites.Hex("9B72CB") },
        { 'p', RuntimeSprites.Hex("C5A8E8") },
        { 'K', RuntimeSprites.Hex("D96570") },
        { 'k', RuntimeSprites.Hex("F2A7AE") },
        { 'w', RuntimeSprites.Hex("FFFFFF") },
        { 'i', RuntimeSprites.Hex("1F1F1F") },
        { 'g', RuntimeSprites.Hex("DADCE0") },
        { 'f', RuntimeSprites.Hex("8D6E4A") },  // 額ぶち
        { 'F', RuntimeSprites.Hex("C9A86A") },  // 額ぶちの光
        { 's', RuntimeSprites.Hex("AECBFA") },  // 空
        { 'm', RuntimeSprites.Hex("7B5EA7") },  // 山
        { 'G', RuntimeSprites.Hex("34A853") },  // 草
        { 'y', RuntimeSprites.Hex("FBBC04") },  // 太陽
    };

    /// <summary>4つの先がとがった星（ロゴ）。色は左上の青 → 紫 → 右下のピンク。n は一辺のドット（奇数）</summary>
    public static Sprite Star4(int n = 13)
    {
        n = Mathf.Max(5, n | 1);
        float c = (n - 1) * 0.5f;
        var rows = new string[n];
        for (int y = 0; y < n; y++)
        {
            var ch = new char[n];
            for (int x = 0; x < n; x++)
            {
                float ax = Mathf.Abs(x - c) / c, ay = Mathf.Abs(y - c) / c;
                // 星形（アストロイドに近い形）
                bool inside = Mathf.Sqrt(ax) + Mathf.Sqrt(ay) <= 1.02f;
                if (!inside) { ch[x] = '.'; continue; }
                float t = (x + y) / (2f * (n - 1));
                bool core = ax + ay < 0.18f;
                ch[x] = core ? 'w' : t < 0.4f ? 'B' : t < 0.62f ? 'P' : 'K';
            }
            rows[y] = new string(ch);
        }
        return RuntimeSprites.FromAscii("Gemini_Star" + n, rows, Pal);
    }

    /// <summary>ふたご座の弾（小さい星）</summary>
    public static Sprite StarBullet => Star4(9);

    /// <summary>HUD の印のアイコン（星 7ドット）</summary>
    public static Sprite Icon => Star4(7);

    /// <summary>星の光の柱（下の青 → 紫 → 上のピンク。白い光の粒）。区画の大きさに伸ばして使う</summary>
    public static Sprite Beam
    {
        get
        {
            const int w = 12, h = 40;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var ch = new char[w];
                float t = 1f - (float)y / (h - 1); // 下が 0
                char main = t < 0.36f ? 'B' : t < 0.68f ? 'P' : 'K';
                char light = t < 0.36f ? 'b' : t < 0.68f ? 'p' : 'k';
                for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || x == w - 1;
                    bool center = x >= 4 && x <= 7;
                    bool spark = (x * 7 + y * 3) % 17 == 0;
                    ch[x] = spark ? 'w' : edge ? main : center ? light : main;
                }
                rows[y] = new string(ch);
            }
            return RuntimeSprites.FromAscii("Gemini_Beam", rows, Pal);
        }
    }

    /// <summary>回答案の札（角の丸い白い板。区画の上の 1・2・3 の地。字は WorldText で重ねる）</summary>
    public static Sprite Label => RuntimeSprites.FromAscii("Gemini_Label", RoundRect(15, 11, 'g', 'w', 2), Pal);

    /// <summary>安全に決まった札（青い地）</summary>
    public static Sprite LabelSafe => RuntimeSprites.FromAscii("Gemini_LabelSafe", RoundRect(15, 11, 'b', 'B', 2), Pal);

    /// <summary>ごほうびの星の札（角の丸い白い板に、色の星）</summary>
    public static Sprite Tag(StarKind kind)
    {
        char main = kind == StarKind.Blue ? 'B' : kind == StarKind.Purple ? 'P' : 'K';
        char light = kind == StarKind.Blue ? 'b' : kind == StarKind.Purple ? 'p' : 'k';
        const int n = 17;
        string[] rows = RoundRect(n, n, light, 'w', 3);
        float c = (n - 1) * 0.5f;
        for (int y = 2; y < n - 2; y++)
        {
            var ch = rows[y].ToCharArray();
            for (int x = 2; x < n - 2; x++)
            {
                float ax = Mathf.Abs(x - c) / (c - 2f), ay = Mathf.Abs(y - c) / (c - 2f);
                if (Mathf.Sqrt(ax) + Mathf.Sqrt(ay) <= 1.02f) ch[x] = ax + ay < 0.2f ? 'w' : main;
            }
            rows[y] = new string(ch);
        }
        return RuntimeSprites.FromAscii("Gemini_Tag" + kind, rows, Pal);
    }

    /// <summary>星の色</summary>
    public static Color ColorOf(StarKind kind) => kind == StarKind.Blue ? Blue : kind == StarKind.Purple ? Purple : Pink;

    /// <summary>画像生成の額ぶちの絵（4×3 マス＝72×54 ドット。空・太陽・紫の山・草）</summary>
    public static Sprite ImageFrame
    {
        get
        {
            const int w = 72, h = 54;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var ch = new char[w];
                for (int x = 0; x < w; x++)
                {
                    int ex = Mathf.Min(x, w - 1 - x), ey = Mathf.Min(y, h - 1 - y);
                    int e = Mathf.Min(ex, ey);
                    if (e <= 1) { ch[x] = 'f'; continue; }
                    if (e == 2) { ch[x] = 'F'; continue; }
                    if (e == 3) { ch[x] = 'f'; continue; }
                    // 絵の中身
                    float sx = (x - 52f), sy = (y - 14f);
                    bool sun = sx * sx + sy * sy <= 30f;
                    float m1 = 30f + Mathf.Abs(x - 24) * 0.9f;   // 左の山の高さ（上からの行）
                    float m2 = 34f + Mathf.Abs(x - 50) * 0.8f;
                    bool mountain = y >= Mathf.Min(m1, m2);
                    bool grass = y >= 44;
                    ch[x] = grass ? 'G' : mountain ? 'm' : sun ? 'y' : (y < 10 ? 'b' : 's');
                }
                rows[y] = new string(ch);
            }
            // きらめき（AI が作った絵の印）
            Put(rows, 8, 8, 'w'); Put(rows, 7, 8, 'w'); Put(rows, 9, 8, 'w'); Put(rows, 8, 7, 'w'); Put(rows, 8, 9, 'w');
            return RuntimeSprites.FromAscii("Gemini_Image", rows, Pal);
        }
    }

    // ───────── 道具 ─────────

    /// <summary>角の丸い板（border：ふちの文字、fill：中の文字、r：角を何ドット落とすか）</summary>
    public static string[] RoundRect(int w, int h, char border, char fill, int r)
    {
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++)
            {
                int cx = Mathf.Min(x, w - 1 - x), cy = Mathf.Min(y, h - 1 - y);
                if (cx + cy < r) { c[x] = '.'; continue; }
                bool edge = cx == 0 || cy == 0 || cx + cy == r;
                c[x] = edge ? border : fill;
            }
            rows[y] = new string(c);
        }
        return rows;
    }

    static void Put(string[] rows, int x, int y, char ch)
    {
        if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) return;
        var c = rows[y].ToCharArray();
        c[x] = ch;
        rows[y] = new string(c);
    }

    /// <summary>
    /// ボスの絵を写す（ふたごの影）。src の下の見えている SpriteRenderer を、dst の子に同じ並びで作る。
    /// flip なら左右反転。色は tint（ボスの色に掛けず、tint の色にそろえる＝影）
    /// </summary>
    public static List<SpriteRenderer> CloneShadow(Transform src, Transform dst, Color tint, bool flip, int orderShift = -2)
    {
        var list = new List<SpriteRenderer>();
        if (src == null || dst == null) return list;
        foreach (var sr in src.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            string n = sr.gameObject.name;
            if (n.StartsWith("Windup_") || n == "Telegraph" || n == "Frame") continue;
            var go = new GameObject("Shadow_" + n);
            go.layer = dst.gameObject.layer;
            go.transform.SetParent(dst, false);
            Vector3 off = sr.transform.position - src.position;
            if (flip) off.x = -off.x;
            go.transform.localPosition = new Vector3(off.x, off.y, 0f);
            Vector3 ls = sr.transform.lossyScale;
            go.transform.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), 1f);
            var c = go.AddComponent<SpriteRenderer>();
            c.sprite = sr.sprite;
            c.flipX = flip ? !sr.flipX : sr.flipX;
            c.flipY = sr.flipY;
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                c.drawMode = sr.drawMode;
                c.size = sr.size;
            }
            c.color = new Color(tint.r, tint.g, tint.b, tint.a * sr.color.a);
            c.sortingLayerID = sr.sortingLayerID;
            c.sortingOrder = sr.sortingOrder + orderShift;
            list.Add(c);
        }
        return list;
    }

    /// <summary>ボスの体の範囲（無ければ位置のまわり 2×2）</summary>
    public static Bounds BodyOf(AttackRunner owner)
    {
        if (owner != null && owner.TryGetComponent(out BossController boss)) return boss.BodyBounds;
        Vector3 c = owner != null ? (Vector3)owner.OriginPoint : Vector3.zero;
        return new Bounds(c, new Vector3(2f, 2f, 0f));
    }
}
