using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 【仮】担当3 のスキン（Office・Game・Chat）で共通に使う道具（2026-11-18。docs/app-skill-visuals-design.md 0・1章）。
// ・ドット絵：文字の並び → スプライト（RuntimeSprites.FromAscii。1マス＝18ドット）。小さい字（3×5 ドット）もここで描く
// ・攻撃の物に付ける状態（OfficeGameChatTag）と、自分で消える演出（OfficeGameChatFx）
// ・攻撃の物（Target）そのもの（位置・回転・判定）は、さわらない。絵はいつも子の入れ物に付ける
// 型の名前は、持ち場の決まりで Office から始める（中身は3つのスキンで使う）。

/// <summary>担当3 のスキンの共通の道具</summary>
public static class OfficeGameChatKit
{
    /// <summary>1ドット（マス）</summary>
    public const float Dot = 1f / 18f;

    // ───────── 色（0-3・1-6） ─────────
    public static readonly Color EnemyEdge = new Color32(0xFF, 0x45, 0x3A, 0xFF);
    public static readonly Color PlayerEdge = Color.white;
    public static readonly Color Ink = new Color32(0x1C, 0x1C, 0x1E, 0xFF);

    public static Color Hex(string hex) => RuntimeSprites.Hex(hex);
    /// <summary>敵＝赤いふち、プレイヤー＝白いふち</summary>
    public static Color Edge(bool enemy) => enemy ? EnemyEdge : PlayerEdge;
    public static string Key(Color c) => ColorUtility.ToHtmlStringRGBA(c);
    public static Color Alpha(Color c, float a) { c.a = a; return c; }
    public static Color Opaque(Color c) { c.a = 1f; return c; }
    public static Color Lighten(Color c, float k) => Opaque(Color.Lerp(c, Color.white, k));
    public static Color Darken(Color c, float k) => Opaque(Color.Lerp(c, Color.black, k));

    /// <summary>アプリの代表の色（無ければ fallback）</summary>
    public static Color Main(AppTheme theme, Color fallback) => theme != null ? Opaque(theme.Main) : fallback;

    /// <summary>2つの色が近いか（同じ色を2本続けて使わないため）</summary>
    public static bool Near(Color a, Color b, float limit = 0.22f) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < limit * 3f;

    // ───────── 失敗しても遊べるように ─────────

    /// <summary>絵を付ける処理を包む。エラーが出たら記録して false（今の絵のまま。ゲームは止めない）</summary>
    public static bool Safe(Func<bool> body, string where)
    {
        try { return body(); }
        catch (Exception e)
        {
            Debug.LogWarning($"スキンの絵でエラー（{where}）：{e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // ───────── 文字の並び（ドット絵） ─────────

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>key の絵が無ければ build で作る（同じ key は1回だけ作る。色が変わる物は key に色を入れる）</summary>
    public static Sprite Cached(string key, Func<Sprite> build)
    {
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        s = build();
        Cache[key] = s;
        return s;
    }

    public static char[,] Grid(int w, int h)
    {
        var g = new char[Mathf.Max(1, h), Mathf.Max(1, w)];
        for (int y = 0; y < g.GetLength(0); y++)
            for (int x = 0; x < g.GetLength(1); x++) g[y, x] = '.';
        return g;
    }

    public static int W(char[,] g) => g.GetLength(1);
    public static int H(char[,] g) => g.GetLength(0);

    public static string[] Rows(char[,] g)
    {
        int h = H(g), w = W(g);
        var rows = new string[h];
        var line = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) line[x] = g[y, x];
            rows[y] = new string(line);
        }
        return rows;
    }

    /// <summary>文字の並びを、grid の (ox, oy) から写す（'.' は写さない）</summary>
    public static void Stamp(char[,] g, string[] art, int ox, int oy)
    {
        for (int y = 0; y < art.Length; y++)
            for (int x = 0; x < art[y].Length; x++)
                if (art[y][x] != '.' && art[y][x] != ' ') Set(g, ox + x, oy + y, art[y][x]);
    }

    public static void Set(char[,] g, int x, int y, char c)
    {
        if (x >= 0 && y >= 0 && x < W(g) && y < H(g)) g[y, x] = c;
    }

    public static char Get(char[,] g, int x, int y) => x >= 0 && y >= 0 && x < W(g) && y < H(g) ? g[y, x] : '.';

    public static void Fill(char[,] g, int x, int y, int w, int h, char c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++) Set(g, x + i, y + j, c);
    }

    /// <summary>四角のふち（太さ t）</summary>
    public static void Border(char[,] g, int x, int y, int w, int h, char c, int t = 1)
    {
        Fill(g, x, y, w, t, c);
        Fill(g, x, y + h - t, w, t, c);
        Fill(g, x, y, t, h, c);
        Fill(g, x + w - t, y, t, h, c);
    }

    /// <summary>角の丸い四角（角を1ドット落とす）</summary>
    public static void RoundRect(char[,] g, int x, int y, int w, int h, char c)
    {
        Fill(g, x, y, w, h, c);
        Set(g, x, y, '.'); Set(g, x + w - 1, y, '.'); Set(g, x, y + h - 1, '.'); Set(g, x + w - 1, y + h - 1, '.');
    }

    /// <summary>円をぬる（中心 cx, cy、半径 r。ドットの中心で測る）</summary>
    public static void Disc(char[,] g, float cx, float cy, float r, char c)
    {
        for (int y = 0; y < H(g); y++)
            for (int x = 0; x < W(g); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) g[y, x] = c;
            }
    }

    /// <summary>線（太さ1）</summary>
    public static void Line(char[,] g, int x0, int y0, int x1, int y1, char c)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        for (int guard = 0; guard < 512; guard++)
        {
            Set(g, x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>絵のまわりの透明なドットを、edge でふちどる（diagonal なら斜めの隣も）</summary>
    public static void Outline(char[,] g, char edge, bool diagonal = false)
    {
        int h = H(g), w = W(g);
        var add = new List<Vector2Int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (g[y, x] != '.') continue;
                bool near = Solid(g, x - 1, y) || Solid(g, x + 1, y) || Solid(g, x, y - 1) || Solid(g, x, y + 1);
                if (!near && diagonal) near = Solid(g, x - 1, y - 1) || Solid(g, x + 1, y - 1) || Solid(g, x - 1, y + 1) || Solid(g, x + 1, y + 1);
                if (near) add.Add(new Vector2Int(x, y));
            }
        foreach (var p in add) g[p.y, p.x] = edge;
    }

    static bool Solid(char[,] g, int x, int y) => Get(g, x, y) != '.';

    /// <summary>色の表（文字 → 色）</summary>
    public static Dictionary<char, Color32> Pal(params (char c, Color col)[] entries)
    {
        var d = new Dictionary<char, Color32>();
        foreach (var e in entries) d[e.c] = e.col;
        return d;
    }

    public static Sprite Make(string key, char[,] g, Dictionary<char, Color32> pal, Vector2? pivot = null) =>
        RuntimeSprites.FromAscii(key, Rows(g), pal, pivot);

    public static Sprite Make(string key, string[] rows, Dictionary<char, Color32> pal, Vector2? pivot = null) =>
        RuntimeSprites.FromAscii(key, rows, pal, pivot);

    // ───────── 小さい字（3×5 ドット。英字は大文字に直す） ─────────

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'A', new[] { ".#.", "#.#", "###", "#.#", "#.#" } },
        { 'B', new[] { "##.", "#.#", "##.", "#.#", "##." } },
        { 'C', new[] { ".##", "#..", "#..", "#..", ".##" } },
        { 'D', new[] { "##.", "#.#", "#.#", "#.#", "##." } },
        { 'E', new[] { "###", "#..", "##.", "#..", "###" } },
        { 'F', new[] { "###", "#..", "##.", "#..", "#.." } },
        { 'G', new[] { ".##", "#..", "#.#", "#.#", ".##" } },
        { 'H', new[] { "#.#", "#.#", "###", "#.#", "#.#" } },
        { 'I', new[] { "###", ".#.", ".#.", ".#.", "###" } },
        { 'J', new[] { "..#", "..#", "..#", "#.#", ".#." } },
        { 'K', new[] { "#.#", "#.#", "##.", "#.#", "#.#" } },
        { 'L', new[] { "#..", "#..", "#..", "#..", "###" } },
        { 'M', new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#" } },
        { 'N', new[] { "#..#", "##.#", "#.##", "#..#", "#..#" } },
        { 'O', new[] { ".#.", "#.#", "#.#", "#.#", ".#." } },
        { 'P', new[] { "##.", "#.#", "##.", "#..", "#.." } },
        { 'Q', new[] { ".#.", "#.#", "#.#", "##.", ".##" } },
        { 'R', new[] { "##.", "#.#", "##.", "#.#", "#.#" } },
        { 'S', new[] { ".##", "#..", ".#.", "..#", "##." } },
        { 'T', new[] { "###", ".#.", ".#.", ".#.", ".#." } },
        { 'U', new[] { "#.#", "#.#", "#.#", "#.#", "###" } },
        { 'V', new[] { "#.#", "#.#", "#.#", "#.#", ".#." } },
        { 'W', new[] { "#...#", "#...#", "#.#.#", "##.##", "#...#" } },
        { 'X', new[] { "#.#", "#.#", ".#.", "#.#", "#.#" } },
        { 'Y', new[] { "#.#", "#.#", ".#.", ".#.", ".#." } },
        { 'Z', new[] { "###", "..#", ".#.", "#..", "###" } },
        { '0', new[] { "###", "#.#", "#.#", "#.#", "###" } },
        { '1', new[] { ".#.", "##.", ".#.", ".#.", "###" } },
        { '2', new[] { "##.", "..#", ".#.", "#..", "###" } },
        { '3', new[] { "##.", "..#", ".#.", "..#", "##." } },
        { '4', new[] { "#.#", "#.#", "###", "..#", "..#" } },
        { '5', new[] { "###", "#..", "##.", "..#", "##." } },
        { '6', new[] { ".##", "#..", "###", "#.#", "###" } },
        { '7', new[] { "###", "..#", ".#.", ".#.", ".#." } },
        { '8', new[] { "###", "#.#", "###", "#.#", "###" } },
        { '9', new[] { "###", "#.#", "###", "..#", "##." } },
        { '!', new[] { "#", "#", "#", ".", "#" } },
        { '?', new[] { "##.", "..#", ".#.", "...", ".#." } },
        { '#', new[] { ".#.#.", "#####", ".#.#.", "#####", ".#.#." } },
        { '%', new[] { "#.#", "..#", ".#.", "#..", "#.#" } },
        { '+', new[] { "...", ".#.", "###", ".#.", "..." } },
        { '/', new[] { "..#", "..#", ".#.", "#..", "#.." } },
        { '.', new[] { ".", ".", ".", ".", "#" } },
        { ':', new[] { ".", "#", ".", "#", "." } },
        { '-', new[] { "...", "...", "###", "...", "..." } },
        { '\'', new[] { "#", "#", ".", ".", "." } },
        { '@', new[] { ".###.", "#.#.#", "#.###", "#....", ".###." } },
        { ' ', new[] { "..", "..", "..", "..", ".." } },
    };

    static string[] Glyph(char c)
    {
        c = char.ToUpperInvariant(c);
        return Glyphs.TryGetValue(c, out string[] g) ? g : Glyphs['?'];
    }

    /// <summary>小さい字の横幅（ドット。scale 倍の前）</summary>
    public static int TextWidth(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0;
        foreach (char c in s) w += Glyph(c)[0].Length + 1;
        return w - 1;
    }

    /// <summary>小さい字を grid に描く（左上 x, y。scale 倍の太さ）</summary>
    public static void DrawText(char[,] g, string s, int x, int y, char c, int scale = 1)
    {
        if (string.IsNullOrEmpty(s)) return;
        scale = Mathf.Max(1, scale);
        int cx = x;
        foreach (char ch in s)
        {
            string[] gl = Glyph(ch);
            for (int j = 0; j < gl.Length; j++)
                for (int i = 0; i < gl[j].Length; i++)
                    if (gl[j][i] == '#') Fill(g, cx + i * scale, y + j * scale, scale, scale, c);
            cx += (gl[0].Length + 1) * scale;
        }
    }

    /// <summary>小さい字の絵（outline を入れると、まわりを1ドットふちどる。読みやすくする）</summary>
    public static Sprite Text(string s, Color color, int scale = 1, Color? outline = null, Color? shadow = null)
    {
        s = (s ?? "").ToUpperInvariant();
        string key = $"OGC_Txt_{s}_{Key(color)}_{scale}_{(outline.HasValue ? Key(outline.Value) : "-")}_{(shadow.HasValue ? Key(shadow.Value) : "-")}";
        return Cached(key, () =>
        {
            int m = outline.HasValue ? 1 : 0;
            int sh = shadow.HasValue ? scale : 0;
            int w = TextWidth(s) * scale + m * 2 + sh, h = 5 * scale + m * 2 + sh;
            var g = Grid(w, h);
            if (shadow.HasValue) DrawText(g, s, m + sh, m + sh, 'S', scale);
            DrawText(g, s, m, m, 'T', scale);
            if (outline.HasValue) Outline(g, 'O', true);
            var pal = Pal(('T', color), ('O', outline ?? Color.clear), ('S', shadow ?? Color.clear));
            return Make(key, g, pal);
        });
    }

    // ───────── 図形の絵 ─────────

    /// <summary>円（直径 d ドット。edge を入れると1ドットのふち）</summary>
    public static Sprite DiscSprite(int d, Color fill, Color? edge = null)
    {
        d = Mathf.Max(2, d);
        string key = $"OGC_Disc_{d}_{Key(fill)}_{(edge.HasValue ? Key(edge.Value) : "-")}";
        return Cached(key, () =>
        {
            var g = Grid(d, d);
            Disc(g, d * 0.5f, d * 0.5f, d * 0.5f, edge.HasValue ? 'E' : 'F');
            if (edge.HasValue) Disc(g, d * 0.5f, d * 0.5f, d * 0.5f - 1f, 'F');
            return Make(key, g, Pal(('F', fill), ('E', edge ?? Color.clear)));
        });
    }

    /// <summary>輪（直径 d ドット、太さ t ドット）</summary>
    public static Sprite RingSprite(int d, int t, Color color)
    {
        d = Mathf.Max(2, d);
        t = Mathf.Clamp(t, 1, d / 2);
        string key = $"OGC_Ring_{d}_{t}_{Key(color)}";
        return Cached(key, () =>
        {
            var g = Grid(d, d);
            float r = d * 0.5f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r && dist > r - t) g[y, x] = 'R';
                }
            return Make(key, g, Pal(('R', color)));
        });
    }

    /// <summary>
    /// 動く点線の四角（選択範囲。w×h ドット、太さ1）。phase で点線がずれる（0〜3 を順に出すと流れる）。
    /// a と b の2色を4ドットずつ交互に置く
    /// </summary>
    public static Sprite DashedRect(int w, int h, int phase, Color a, Color b)
    {
        w = Mathf.Max(4, w); h = Mathf.Max(4, h);
        phase = ((phase % 8) + 8) % 8;
        string key = $"OGC_Dash_{w}_{h}_{phase}_{Key(a)}_{Key(b)}";
        return Cached(key, () =>
        {
            var g = Grid(w, h);
            // ふちを一周する順に番号をふって、4ドットごとに色を変える
            int i = 0;
            void Put(int x, int y) { g[y, x] = ((i + phase) / 4) % 2 == 0 ? 'A' : 'B'; i++; }
            for (int x = 0; x < w; x++) Put(x, 0);
            for (int y = 1; y < h; y++) Put(w - 1, y);
            for (int x = w - 2; x >= 0; x--) Put(x, h - 1);
            for (int y = h - 2; y >= 1; y--) Put(0, y);
            return Make(key, g, Pal(('A', a), ('B', b)));
        });
    }

    // ───────── 物を出す ─────────

    /// <summary>子の入れ物を作る</summary>
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

    /// <summary>絵を1枚出す（子）</summary>
    public static SpriteRenderer Show(Transform parent, Sprite sprite, Vector2 localPos, int order, string name = "Sprite") =>
        RuntimeSprites.Show(parent, sprite, localPos, order, 1f, name);

    /// <summary>1色の四角（size マス）。子を付けないこと（大きさを変えているので）</summary>
    public static SpriteRenderer Box(Transform parent, Vector2 localCenter, Vector2 size, Color color, int order, string name = "Box")
    {
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>四角を (a → b) の線として置く（太さ thick マス）。向きも合わせる</summary>
    public static void SetSegment(Transform box, Vector2 a, Vector2 b, float thick)
    {
        Vector2 d = b - a;
        box.localPosition = (a + b) * 0.5f;
        box.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        box.localScale = new Vector3(Mathf.Max(0.0001f, d.magnitude), thick, 1f);
    }

    public static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>入れ物の下の絵を、まとめてすかす（元の色の濃さ × a）。Mac の字（TextMesh）も、いっしょにすかす（2026-11-19）</summary>
    public static void FadeAll(Transform root, float a)
    {
        if (root == null) return;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!BaseAlpha.TryGetValue(sr, out float b)) BaseAlpha[sr] = b = sr.color.a;
            SetAlpha(sr, b * a);
        }
        foreach (var tm in root.GetComponentsInChildren<TextMesh>())
        {
            if (!BaseText.TryGetValue(tm, out Color c)) BaseText[tm] = c = FxKit.TextColor(tm);
            c.a *= a;
            WorldText.SetColor(tm, c);
        }
        if (BaseAlpha.Count > 256 || BaseText.Count > 128) Prune();
    }

    static readonly List<SpriteRenderer> PruneList = new List<SpriteRenderer>();
    static readonly List<TextMesh> PruneText = new List<TextMesh>();

    static void Prune()
    {
        PruneList.Clear();
        foreach (var kv in BaseAlpha) if (kv.Key == null) PruneList.Add(kv.Key);
        foreach (var k in PruneList) BaseAlpha.Remove(k);
        if (BaseAlpha.Count > 1024) BaseAlpha.Clear();
        PruneText.Clear();
        foreach (var kv in BaseText) if (kv.Key == null) PruneText.Add(kv.Key);
        foreach (var k in PruneText) BaseText.Remove(k);
        if (BaseText.Count > 512) BaseText.Clear();
    }

    static readonly Dictionary<SpriteRenderer, float> BaseAlpha = new Dictionary<SpriteRenderer, float>();
    static readonly Dictionary<TextMesh, Color> BaseText = new Dictionary<TextMesh, Color>();

    /// <summary>
    /// 毎フレーム差し替える絵の入れ物（番号 → 絵）。key を作る文字列の手間を、フレームごとにかけない
    /// </summary>
    public static Func<int, Sprite> Frames(int count, Func<int, Sprite> build)
    {
        var cache = new Sprite[Mathf.Max(1, count)];
        return i =>
        {
            i = ((i % cache.Length) + cache.Length) % cache.Length;
            return cache[i] != null ? cache[i] : (cache[i] = build(i));
        };
    }

    /// <summary>自分で消える演出を出す</summary>
    public static OfficeGameChatFx Fx(string name, Vector2 pos, float life, AttackRunner owner = null, bool unscaled = false) =>
        OfficeGameChatFx.Create(name, pos, life, owner, unscaled);

    /// <summary>
    /// 攻撃の物から離れた演出を、攻撃した人に預ける（SkinDresser.Adopt。形態変化・正常化で消える）。
    /// 自分でも life 秒で消える
    /// </summary>
    public static OfficeGameChatFx Adopted(DressInfo info, string name, Vector2 pos, float life)
    {
        var fx = OfficeGameChatFx.Create(name, pos, life, info.IsEnemy ? info.Owner : null, false);
        SkinDresser.Adopt(info, fx.gameObject);
        return fx;
    }

    /// <summary>
    /// 攻撃の物の子の絵（root）を、攻撃の物から外して、自分で消える演出にする（消える場面。0-2 の4）。
    /// 攻撃の物はすぐあとに消されるので、外してから動かす。攻撃した人に預ける
    /// </summary>
    public static OfficeGameChatFx Detach(DressInfo info, Transform root, float life)
    {
        if (root == null) return null;
        var fx = Adopted(info, root.name + "_Exit", root.position, life);
        root.SetParent(fx.transform, true);
        return fx;
    }

    /// <summary>文字の並びを scale 倍の大きさで写す（大きいスタンプなど）</summary>
    public static void StampScaled(char[,] g, string[] art, int ox, int oy, int scale)
    {
        for (int y = 0; y < art.Length; y++)
            for (int x = 0; x < art[y].Length; x++)
                if (art[y][x] != '.' && art[y][x] != ' ') Fill(g, ox + x * scale, oy + y * scale, scale, scale, art[y][x]);
    }

    /// <summary>
    /// かけら（1-2）。向き dir の先へ扇状（±spread 度）に飛ぶ。重力あり。だんだんすけて消える
    /// </summary>
    public static void Debris(Vector2 pos, Vector2 dir, IList<Sprite> sprites, int count, int order,
        float spread = 40f, float speedMin = 6f, float speedMax = 10f, float gravity = 24f,
        float lifeMin = 0.3f, float lifeMax = 0.5f, bool unscaled = false, float alpha = 1f)
    {
        if (sprites == null || sprites.Count == 0 || count <= 0) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        for (int i = 0; i < count; i++)
        {
            float ang = UnityEngine.Random.Range(-spread, spread);
            Vector2 v = (Vector2)(Quaternion.Euler(0f, 0f, ang) * dir) * UnityEngine.Random.Range(speedMin, speedMax);
            float life = UnityEngine.Random.Range(lifeMin, lifeMax);
            var fx = Fx("Debris", pos, life + 0.05f, null, unscaled);
            var sr = Show(fx.transform, sprites[i % sprites.Count], Vector2.zero, order, "Bit");
            SetAlpha(sr, alpha);
            float spin = UnityEngine.Random.Range(-540f, 540f);
            fx.Play(Fly(fx, sr, v, gravity, life, spin, alpha));
        }
    }

    static IEnumerator Fly(OfficeGameChatFx fx, SpriteRenderer sr, Vector2 v, float gravity, float life, float spin, float alpha)
    {
        float start = fx.Now, last = start;
        while (fx != null)
        {
            float now = fx.Now, dt = now - last;
            last = now;
            v.y -= gravity * dt;
            fx.transform.position += (Vector3)(v * dt);
            fx.transform.Rotate(0f, 0f, spin * dt);
            float k = Mathf.Clamp01((now - start) / life);
            SetAlpha(sr, alpha * (1f - k * k));
            yield return null;
        }
    }

    // ───────── 動きの形 ─────────

    /// <summary>「パッ」と出る大きさ（0 → 115% → 100%。dur 秒。0-2 の1）</summary>
    public static float Pop(float t, float dur = 0.1f, float over = 1.15f)
    {
        if (t <= 0f) return 0f;
        if (t >= dur) return 1f;
        float k = t / dur;
        return k < 0.6f ? Mathf.Lerp(0f, over, k / 0.6f) : Mathf.Lerp(over, 1f, (k - 0.6f) / 0.4f);
    }

    /// <summary>最後の last 秒は点滅する（予告の決まり。0-3）。remain は残りの秒数</summary>
    public static bool BlinkOn(float remain, float last = 0.3f, float period = 0.08f) =>
        remain > last || Mathf.FloorToInt(Time.time / period) % 2 == 0;

    public static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }

    /// <summary>
    /// 当たった絵の動き：「パッ」と出て（snapFrom を入れると、その大きさから「カチッ」と縮む）、
    /// 上へ rise マス浮きながら、最後の4割ですけて消える（1-2）
    /// </summary>
    public static IEnumerator PopRise(OfficeGameChatFx fx, Transform t, float life, float rise = 0.4f, float pop = 0.1f, float scale = 1f, float snapFrom = 0f)
    {
        Vector3 p0 = t.localPosition;
        float start = fx.Now;
        while (fx != null && t != null)
        {
            float age = fx.Now - start, k = Mathf.Clamp01(age / life);
            float s = snapFrom > 0f ? Mathf.Lerp(snapFrom, 1f, Mathf.Clamp01(age / pop)) : Pop(age, pop);
            t.localScale = new Vector3(s * scale, s * scale, 1f);
            t.localPosition = p0 + Vector3.up * (rise * EaseOut(k));
            if (k > 0.6f) FadeAll(t, 1f - (k - 0.6f) / 0.4f);
            yield return null;
        }
    }

    /// <summary>消える動き：t を dur 秒で、大きさ (sx, sy) 倍・すかしながら、move だけ動かす</summary>
    public static IEnumerator ShrinkOut(OfficeGameChatFx fx, Transform t, float dur, float sx, float sy, Vector3 move = default)
    {
        if (t == null) yield break;
        Vector3 s0 = t.localScale, p0 = t.localPosition;
        float start = fx.Now;
        while (fx != null && t != null)
        {
            float k = Mathf.Clamp01((fx.Now - start) / dur);
            t.localScale = new Vector3(s0.x * Mathf.Lerp(1f, sx, k), s0.y * Mathf.Lerp(1f, sy, k), 1f);
            t.localPosition = p0 + move * k;
            FadeAll(t, 1f - k * k);
            if (k >= 1f) { fx.Kill(); yield break; }
            yield return null;
        }
    }

    // ───────── 攻撃の情報 ─────────

    /// <summary>判定の大きさ（マス）。情報に無ければ攻撃データから。円は x が直径（y も x にそろえる）</summary>
    public static Vector2 SizeOf(DressInfo info)
    {
        AttackData d = info.Data;
        bool circle = d != null && d.Kind == AttackKind.Area && d.Shape == AreaShape.Circle;
        Vector2 s = info.HitSize;
        if (s.sqrMagnitude < 0.0001f && d != null)
        {
            switch (d.Kind)
            {
                case AttackKind.Projectile: s = d.HitSize; break;
                case AttackKind.Area: s = d.AreaSize; break;
                case AttackKind.Spawn: s = d.ObjectSize; break;
            }
        }
        if (s.sqrMagnitude < 0.0001f) s = Vector2.one;
        return circle ? new Vector2(s.x, s.x) : s;
    }

    /// <summary>攻撃した人の体の範囲（ボス：IWarpable。無ければコライダー）</summary>
    public static Bounds OwnerBody(AttackRunner owner)
    {
        if (owner == null) return new Bounds(Vector3.zero, Vector3.one);
        var w = owner.GetComponent<IWarpable>();
        if (w != null) return w.BodyBounds;
        foreach (var col in owner.GetComponents<Collider2D>())
            if (!col.isTrigger && col.enabled) return col.bounds;
        return new Bounds(owner.transform.position, Vector3.one);
    }

    /// <summary>当たった相手の体の範囲（ボスなら IWarpable、プレイヤー・雑魚なら動く用のコライダー）</summary>
    public static Bounds BodyOf(GameObject go, Vector2 fallback)
    {
        if (go == null) return new Bounds(fallback, Vector3.one * 0.8f);
        var w = go.GetComponentInParent<IWarpable>();
        if (w != null) return w.BodyBounds;
        foreach (var col in go.GetComponentsInParent<Collider2D>())
            if (!col.isTrigger && col.enabled) return col.bounds;
        foreach (var col in go.GetComponentsInChildren<Collider2D>())
            if (col.enabled) return col.bounds;
        return new Bounds(go.transform.position, Vector3.one * 0.8f);
    }

    /// <summary>当たった絵を出す位置（当たった位置から外へ 0.3マス。1-2）</summary>
    public static Vector2 HitSpot(HitInfo info)
    {
        Vector2 d = info.Direction;
        if (d.sqrMagnitude < 0.0001f) return info.Point + Vector2.up * 0.3f;
        return info.Point - d.normalized * 0.3f;
    }

    /// <summary>かけらの飛ぶ向き（攻撃 → 相手。無ければ上）</summary>
    public static Vector2 HitDir(HitInfo info) =>
        info.Direction.sqrMagnitude > 0.0001f ? info.Direction.normalized : Vector2.up;

    /// <summary>画面（カメラに映っている範囲）</summary>
    public static Rect View() => ScreenUi.ViewRect();

    /// <summary>
    /// content の絵が画面からはみ出さないように、root を動かす（margin マス内側。上は rise マスぶん浮くので、その分も空ける）。
    /// 大きいダイアログ（ダウン）用。2026-11-19
    /// </summary>
    public static void KeepInView(Transform root, Transform content, float margin, float rise = 0f)
    {
        if (root == null || content == null) return;
        var srs = content.GetComponentsInChildren<SpriteRenderer>();
        if (srs.Length == 0) return;
        Bounds b = srs[0].bounds;
        for (int i = 1; i < srs.Length; i++) b.Encapsulate(srs[i].bounds);
        Rect v = View();
        float dx = 0f, dy = 0f;
        if (b.size.x > v.width - margin * 2f) dx = v.center.x - b.center.x;
        else if (b.min.x < v.xMin + margin) dx = v.xMin + margin - b.min.x;
        else if (b.max.x > v.xMax - margin) dx = v.xMax - margin - b.max.x;
        if (b.min.y < v.yMin + margin) dy = v.yMin + margin - b.min.y;
        else if (b.max.y + rise > v.yMax - margin) dy = v.yMax - margin - rise - b.max.y;
        root.position += new Vector3(dx, dy, 0f);
    }

    /// <summary>攻撃データの名前から枠を決める（共通担当の SkinTables.FrameOf が None を返したときの予備）</summary>
    public static AttackFrame FrameFromName(AttackData data)
    {
        string n = data != null ? data.name : null;
        if (string.IsNullOrEmpty(n)) return AttackFrame.None;
        if (n.Contains("TabShuriken") || n.Contains("Gen_Spread")) return AttackFrame.SmallShot;
        if (n.Contains("ErrorBullet")) return AttackFrame.LongShot;
        if (n.Contains("TrapFloor") || n.Contains("RedWave")) return AttackFrame.Floor;
        if (n.Contains("Bomb_Blast") || n.Contains("Gen_Nova")) return AttackFrame.Circle;
        if (n.Contains("Ability_Bomb") || n.Contains("Gen_Trap") || n.Contains("Gen_Lob")) return AttackFrame.Thrown;
        if (n.Contains("Breakpoint") || n.Contains("BarGraph") || n.Contains("Pillar")) return AttackFrame.Pillar;
        if (n.Contains("PaintBucket") || n.Contains("Gen_Paint")) return AttackFrame.Paint;
        if (n.Contains("PopupAppear")) return AttackFrame.Window;
        if (n.Contains("ErrorWindowFall")) return AttackFrame.Falling;
        if (n.Contains("TabMultiply")) return AttackFrame.TopRow;
        if (n.Contains("Warp") || n.Contains("NewTab")) return AttackFrame.Warp;
        return AttackFrame.None;
    }

    public static bool NameIs(AttackData data, string part) => data != null && data.name.Contains(part);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        BaseAlpha.Clear();
        BaseText.Clear();
    }
}

/// <summary>
/// 【仮】攻撃の物（Target）に付ける、スキンの絵の状態（2026-11-18）。絵は子の入れ物 Root に付ける（Target の位置・回転にはさわらない）。
/// 攻撃の物が消されると、いっしょに消える（形態変化・正常化で残らない）。
/// 場面の知らせ（Dress）は来ないこともあるので、毎フレーム Tick で攻撃の部品の状態を見て進める
/// </summary>
public class OfficeGameChatTag : MonoBehaviour
{
    /// <summary>どの絵か（"Office.Pie" など）</summary>
    public string Kind;
    public DressInfo Info;
    /// <summary>付けた時刻</summary>
    public float Born;
    /// <summary>本体の絵の入れ物（Target の子）</summary>
    public Transform Root;
    /// <summary>弾が回っても、絵をまっすぐに保つ</summary>
    public bool Upright;
    /// <summary>左へ進む弾は、絵を上下反転する（上下さかさまに見せない）</summary>
    public bool FlipLeft;
    /// <summary>1秒に回る角度（0 なら回さない）</summary>
    public float Spin;
    /// <summary>毎フレーム呼ぶ（動き）</summary>
    public Action<OfficeGameChatTag> Tick;
    /// <summary>何かの番号（色・数字）</summary>
    public int Number;
    /// <summary>この物と組になる物（棒グラフのグラフ全体など）</summary>
    public Component Link;
    public Projectile Projectile;
    public AreaStrike Area;
    public FallingObject Falling;
    readonly HashSet<string> _done = new HashSet<string>();
    readonly Dictionary<string, Transform> _layers = new Dictionary<string, Transform>();

    /// <summary>Target の状態を取る（無ければ作る）</summary>
    public static OfficeGameChatTag Of(DressInfo info, string kind)
    {
        var tag = info.Target.GetComponent<OfficeGameChatTag>();
        if (tag == null)
        {
            tag = info.Target.AddComponent<OfficeGameChatTag>();
            tag.Kind = kind;
            tag.Born = Time.time;
            tag.Info = info;
            tag.Projectile = info.Target.GetComponent<Projectile>();
            tag.Area = info.Target.GetComponent<AreaStrike>();
            tag.Falling = info.Target.GetComponent<FallingObject>();
            tag.Root = OfficeGameChatKit.Node(info.Target.transform, "SkinArt", Vector2.zero);
        }
        else
        {
            // 後の場面の情報で足りない所を埋める（色・アイコンは最初の物を使い続ける）
            if (tag.Info.Data == null) tag.Info.Data = info.Data;
            if (tag.Info.Owner == null) tag.Info.Owner = info.Owner;
            if (tag.Info.Theme == null) tag.Info.Theme = info.Theme;
            if (tag.Info.AppIcon == null) tag.Info.AppIcon = info.AppIcon;
        }
        return tag;
    }

    /// <summary>key の処理をまだしていなければ true（1回だけする物に使う）</summary>
    public bool Once(string key) => _done.Add(key);
    public bool Has(string key) => _done.Contains(key);

    /// <summary>名前つきの入れ物（Root の子）。無ければ作る</summary>
    public Transform Layer(string name)
    {
        if (_layers.TryGetValue(name, out Transform t) && t != null) return t;
        if (Root == null) return null;
        t = OfficeGameChatKit.Node(Root, name, Vector2.zero);
        _layers[name] = t;
        return t;
    }

    /// <summary>名前つきの入れ物を消す</summary>
    public void ClearLayer(string name)
    {
        if (_layers.TryGetValue(name, out Transform t) && t != null) Destroy(t.gameObject);
        _layers.Remove(name);
    }

    public bool HasLayer(string name) => _layers.TryGetValue(name, out Transform t) && t != null;

    /// <summary>今の絵を外して、自分で消える演出にする（このあと Root は空になる。攻撃した人に預ける）</summary>
    public OfficeGameChatFx DetachRoot(float life)
    {
        if (Root == null) return null;
        var fx = OfficeGameChatKit.Detach(Info, Root, life);
        Root = null;
        Tick = null; // 外した絵は、もうこの物からは動かさない
        _layers.Clear();
        return fx;
    }

    /// <summary>今の絵を外して、dur 秒で (sx, sy) 倍に縮めながらすかして消す（move だけ動かす）</summary>
    public void ExitShrink(float dur, float sx, float sy, Vector3 move = default)
    {
        Transform r = Root;
        var fx = DetachRoot(dur + 0.05f);
        if (fx != null) fx.Play(OfficeGameChatKit.ShrinkOut(fx, r, dur, sx, sy, move));
    }

    /// <summary>この物ができてからの秒数</summary>
    public float Age => Time.time - Born;

    /// <summary>
    /// 呼んだ側がかくした「今の絵」を、見せたままにする（影・点線の枠など、仕様で「そのまま」の物。
    /// SkinDresser は true を返すと forceRenderingOff にするので、次のフレームから戻す）
    /// </summary>
    public void KeepVisible(SpriteRenderer sr)
    {
        if (sr != null && !_keep.Contains(sr)) _keep.Add(sr);
    }

    readonly List<SpriteRenderer> _keep = new List<SpriteRenderer>();

    /// <summary>弾の進む向き（無ければ右）</summary>
    public Vector2 Heading
    {
        get
        {
            if (Projectile != null && Projectile.Velocity.sqrMagnitude > 0.0001f) return Projectile.Velocity.normalized;
            return transform.right;
        }
    }

    void LateUpdate()
    {
        for (int i = 0; i < _keep.Count; i++)
            if (_keep[i] != null) _keep[i].forceRenderingOff = false;
        if (Root != null)
        {
            if (Spin != 0f) Root.rotation = Quaternion.Euler(0f, 0f, (Time.time - Born) * Spin);
            else if (Upright) Root.rotation = Quaternion.identity;
            if (FlipLeft && !Upright && Spin == 0f)
            {
                bool left = transform.right.x < -0.01f;
                Vector3 s = Root.localScale;
                s.y = Mathf.Abs(s.y) * (left ? -1f : 1f);
                Root.localScale = s;
            }
        }
        if (Tick == null) return;
        try { Tick(this); }
        catch (Exception e)
        {
            // 絵の動きでエラーが出たら、その物の動きだけ止める（毎フレーム出し続けない。攻撃は止めない）
            Tick = null;
            Debug.LogWarning($"スキンの絵の動きでエラー（{Kind}）：{e.Message}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// 【仮】自分で消える演出（2026-11-18）。攻撃の物から離れた絵（消える演出・かけら・予告の飾り）に使う。
/// ・life 秒で消える。時間の速さが 0 のまま（ゲームオーバーなど）でも、実時間の上限で消える
/// ・持ち主がボスなら、ボスが戦闘中でなくなったら（形態変化・正常化）すぐ消える
/// </summary>
public class OfficeGameChatFx : MonoBehaviour
{
    float _dieAt;
    float _hardCap;
    bool _unscaled;
    bool _watchOwner;
    AttackRunner _owner;
    BossController _boss;
    Transform _follow;
    Vector3 _offset;
    bool _following;

    public float Born { get; private set; }
    public float Now => _unscaled ? Time.unscaledTime : Time.time;
    public float Age => Now - Born;

    public static OfficeGameChatFx Create(string name, Vector2 pos, float life, AttackRunner owner = null, bool unscaled = false)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var fx = go.AddComponent<OfficeGameChatFx>();
        fx._unscaled = unscaled;
        fx.Born = fx.Now;
        fx._dieAt = fx.Born + Mathf.Max(0.01f, life);
        fx._hardCap = Time.unscaledTime + life + 3f;
        if (owner != null)
        {
            fx._owner = owner;
            fx._watchOwner = true;
            fx._boss = owner.GetComponent<BossController>();
        }
        return fx;
    }

    /// <summary>動きを始める（この物が消えると止まる）</summary>
    public OfficeGameChatFx Play(IEnumerator routine)
    {
        if (routine != null) StartCoroutine(routine);
        return this;
    }

    /// <summary>t の位置（＋offset）についていく。t が消えたら自分も消える</summary>
    public void Follow(Transform t, Vector3 offset)
    {
        _follow = t;
        _offset = offset;
        _following = t != null;
        if (t != null) transform.position = t.position + offset;
    }

    /// <summary>消える時刻を延ばす</summary>
    public void Extend(float life)
    {
        _dieAt = Mathf.Max(_dieAt, Now + life);
        _hardCap = Mathf.Max(_hardCap, Time.unscaledTime + life + 3f);
    }

    /// <summary>今すぐ消える</summary>
    public void Kill() => Destroy(gameObject);

    void LateUpdate()
    {
        if (Now >= _dieAt || Time.unscaledTime >= _hardCap)
        {
            Destroy(gameObject);
            return;
        }
        if (_watchOwner && (_owner == null || (_boss != null && _boss.State != BossState.Fighting)))
        {
            Destroy(gameObject);
            return;
        }
        if (_following)
        {
            if (_follow == null)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = _follow.position + _offset;
        }
    }
}
