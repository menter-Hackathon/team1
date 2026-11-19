using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ChatGPT のキットの色・ドット絵・小さな道具（2026-11-19。docs/app-kits-1-design.md 5章）。担当3 の物。
/// 絵はコードの中のドット絵（RuntimeSprites.FromAscii）。絵のファイルは足さない。
/// 色：暗い地 #202123・白・緑 #10A37F・灰色 #8E8EA0（どれも【仮】）
/// </summary>
public static class ChatGptArt
{
    /// <summary>1ドット（1マス＝18ドット）</summary>
    public const float Px = 1f / 18f;

    public static readonly Color Dark = RuntimeSprites.Hex("202123");
    public static readonly Color Panel = RuntimeSprites.Hex("343541");
    public static readonly Color White = Color.white;
    public static readonly Color Green = RuntimeSprites.Hex("10A37F");
    public static readonly Color Gray = RuntimeSprites.Hex("8E8EA0");
    public static readonly Color LightGray = RuntimeSprites.Hex("C5C5D2");

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'k', RuntimeSprites.Hex("202123") },
        { 'd', RuntimeSprites.Hex("343541") },
        { 'w', RuntimeSprites.Hex("FFFFFF") },
        { 'g', RuntimeSprites.Hex("10A37F") },
        { 'a', RuntimeSprites.Hex("8E8EA0") },
        { 'l', RuntimeSprites.Hex("C5C5D2") },
        { 'r', RuntimeSprites.Hex("FF453A") },
    };

    // ───────── 弾 ─────────

    /// <summary>本物の言葉の弾（白い札に字。赤いふち＝当たる）</summary>
    public static Sprite Token => RuntimeSprites.FromAscii("ChatGPT_Token", new[]
    {
        ".rrrrrrrr.",
        "rwwwwwwwwr",
        "rwkkwkkkwr",
        "rwwwwwwwwr",
        ".rrrrrrrr.",
    }, Pal);

    /// <summary>ウソの言葉の弾（灰色。文字化けの 3コマ。赤は使わない）</summary>
    public static Sprite FakeToken(int frame)
    {
        switch (((frame % 3) + 3) % 3)
        {
            case 0:
                return RuntimeSprites.FromAscii("ChatGPT_Fake0", new[]
                {
                    ".aaaaaaaa.",
                    "alwlkllwla",
                    "akllwlkkla",
                    "alkwllwlla",
                    ".aaaaaaaa.",
                }, Pal);
            case 1:
                return RuntimeSprites.FromAscii("ChatGPT_Fake1", new[]
                {
                    ".aaaa.aaa.",
                    "alklwlkwla",
                    "a.lkllwl.a",
                    "awllkwlkla",
                    ".aaa.aaaa.",
                }, Pal);
            default:
                return RuntimeSprites.FromAscii("ChatGPT_Fake2", new[]
                {
                    "..aaaaaaa.",
                    "awlkwllkla",
                    "alwllkwl.a",
                    "aklwlwllka",
                    ".aaaaaaa..",
                }, Pal);
        }
    }

    // ───────── 考え中・止めるボタン・回る矢印 ─────────

    /// <summary>「考え中」の吹き出し（暗い地の角の丸い板。点は別の絵で重ねる）</summary>
    public static Sprite Bubble => RuntimeSprites.FromAscii("ChatGPT_Bubble", RoundRect(21, 9, 'a', 'k', 2), Pal);

    /// <summary>考え中の点（3×3）</summary>
    public static Sprite Dot => RuntimeSprites.FromAscii("ChatGPT_Dot", new[] { ".w.", "www", ".w." }, Pal);

    /// <summary>止めるボタン（白い丸に、暗い四角）</summary>
    public static Sprite StopButton
    {
        get
        {
            const int n = 13;
            var rows = new string[n];
            for (int y = 0; y < n; y++)
            {
                var c = new char[n];
                for (int x = 0; x < n; x++)
                {
                    float dx = x - 6f, dy = y - 6f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool square = x >= 4 && x <= 8 && y >= 4 && y <= 8;
                    c[x] = d > 6.4f ? '.' : d > 5.4f ? 'a' : square ? 'k' : 'w';
                }
                rows[y] = new string(c);
            }
            return RuntimeSprites.FromAscii("ChatGPT_Stop", rows, Pal);
        }
    }

    /// <summary>回る矢印（Regenerate の印。白い輪に、緑の矢じり）</summary>
    public static Sprite RegenArrow
    {
        get
        {
            const int n = 13;
            var rows = new string[n];
            for (int y = 0; y < n; y++)
            {
                var c = new char[n];
                for (int x = 0; x < n; x++)
                {
                    float dx = x - 6f, dy = 6f - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // 右が 0、上が 90
                    bool ring = d >= 3.6f && d <= 5.4f;
                    bool gap = ang > 20f && ang < 85f; // 右上をあける
                    c[x] = ring && !gap ? 'w' : '.';
                }
                rows[y] = new string(c);
            }
            // 矢じり（輪の切れ目の右側。下向き）
            Put(rows, 10, 2, 'g'); Put(rows, 11, 2, 'g'); Put(rows, 12, 2, 'g');
            Put(rows, 9, 3, 'g'); Put(rows, 10, 3, 'g'); Put(rows, 11, 3, 'g'); Put(rows, 12, 3, 'g');
            Put(rows, 10, 4, 'g'); Put(rows, 11, 4, 'g');
            return RuntimeSprites.FromAscii("ChatGPT_Regen", rows, Pal);
        }
    }

    /// <summary>長い答えの柱（暗い地に、白と灰色の行。左右に緑の線）。区画の大きさに伸ばして使う</summary>
    public static Sprite AnswerColumn
    {
        get
        {
            const int w = 18, h = 48;
            var rows = new string[h];
            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                var c = new char[w];
                bool line = y % 3 == 1;
                int len = 4 + rng.Next(11);
                char ink = rng.Next(4) == 0 ? 'a' : 'w';
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || x == w - 1) c[x] = 'g';
                    else if (line && x >= 2 && x < 2 + len) c[x] = ink;
                    else c[x] = 'k';
                }
                rows[y] = new string(c);
            }
            return RuntimeSprites.FromAscii("ChatGPT_Answer", rows, Pal);
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
    /// ボスの絵を写す（ウソ・ふたごの分身）。src の下の、見えている SpriteRenderer を全部、dst の子に同じ並びで作る。
    /// 位置は src の位置からのずれ（flip なら左右反転）。色に tint を掛ける。order は元の order + orderShift
    /// </summary>
    public static List<SpriteRenderer> CloneLook(Transform src, Transform dst, Color tint, bool flip = false, int orderShift = 0)
    {
        var list = new List<SpriteRenderer>();
        if (src == null || dst == null) return list;
        foreach (var sr in src.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            string n = sr.gameObject.name;
            if (n.StartsWith("Windup_") || n == "Telegraph" || n == "Frame") continue;
            var go = new GameObject("Look_" + n);
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
            Color col = sr.color * tint;
            col.a = sr.color.a * tint.a;
            c.color = col;
            c.sortingLayerID = sr.sortingLayerID;
            c.sortingOrder = sr.sortingOrder + orderShift;
            list.Add(c);
        }
        return list;
    }

    /// <summary>文字化けの小さい四角を、rect の中に count 個（0.15秒で消える）。赤は使わない</summary>
    public static void Glitch(Rect rect, int count, int order)
    {
        for (int i = 0; i < count; i++)
        {
            var p = new Vector2(UnityEngine.Random.Range(rect.xMin, rect.xMax), UnityEngine.Random.Range(rect.yMin, rect.yMax));
            int pick = UnityEngine.Random.Range(0, 3);
            Color c = pick == 0 ? White : pick == 1 ? LightGray : Green;
            int size = UnityEngine.Random.Range(2, 4);
            FxAnim.Sprite(FxArt.Square(size), p, order, 0.15f, c, 1f, "ChatGPTGlitch").FadeFrom(0.6f);
        }
    }

    /// <summary>ボスの体の範囲（無ければ位置のまわり 2×2）</summary>
    public static Bounds BodyOf(AttackRunner owner)
    {
        if (owner != null && owner.TryGetComponent(out BossController boss)) return boss.BodyBounds;
        Vector3 c = owner != null ? (Vector3)owner.OriginPoint : Vector3.zero;
        return new Bounds(c, new Vector3(2f, 2f, 0f));
    }

    /// <summary>ちょっとした例外よけ（演出の失敗で技を止めない）</summary>
    public static void Safe(Action a)
    {
        try { a(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
