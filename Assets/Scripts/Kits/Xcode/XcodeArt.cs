using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Xcode のキットの色・絵・小さな道具（2026-11-19。docs/app-kits-2-design.md 3-2）。
/// 絵はコードの中のドット絵（1マス＝18ドット）。形はコードで並べる（XcodeArt.Grid）。色は仕様書のとおり（どれも【仮】）。
/// </summary>
public static class XcodeArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("1575F9");
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");
    public static readonly Color Yellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color Green = RuntimeSprites.Hex("30D158");
    public static readonly Color Screen = RuntimeSprites.Hex("5AC8FA");

    public const float Px = 1f / 18f;

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) },
        { 'K', RuntimeSprites.Hex("101014") },
        { 'k', RuntimeSprites.Hex("2C2C2E") },
        { 'S', RuntimeSprites.Hex("C7C7CC") },
        { 's', RuntimeSprites.Hex("8E8E93") },
        { 'L', RuntimeSprites.Hex("E5E5EA") },
        { 'B', (Color32)Blue },
        { 'b', RuntimeSprites.Hex("0B4FB3") },
        { 'C', (Color32)Screen },
        { 'c', RuntimeSprites.Hex("2E7DB8") },
        { 'Y', (Color32)Yellow },
        { 'R', (Color32)Red },
        { 'G', (Color32)Green },
    };

    /// <summary>ドットの方眼（左上が 0,0。y は下へ）。形を四角で塗って、絵にする</summary>
    public class Grid
    {
        readonly char[,] _c;
        public readonly int W, H;
        public Grid(int w, int h)
        {
            W = w; H = h;
            _c = new char[w, h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) _c[x, y] = '.';
        }
        public void Set(int x, int y, char c) { if (x >= 0 && y >= 0 && x < W && y < H) _c[x, y] = c; }
        public void Rect(int x, int y, int w, int h, char c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, y + j, c); }
        /// <summary>角を1ドット欠いた四角</summary>
        public void Round(int x, int y, int w, int h, char c)
        {
            Rect(x + 1, y, w - 2, h, c);
            Rect(x, y + 1, w, h - 2, c);
        }
        public Sprite ToSprite(string key)
        {
            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var line = new char[W];
                for (int x = 0; x < W; x++) line[x] = _c[x, y];
                rows[y] = new string(line);
            }
            return RuntimeSprites.FromAscii(key, rows, Pal);
        }
    }

    /// <summary>Build の大きいハンマー（頭が下。横 40×縦 48 ドット）</summary>
    public static Sprite Hammer()
    {
        var g = new Grid(40, 48);
        // 柄（青い、Xcode のアイコンの色）
        g.Rect(17, 0, 6, 34, 'b');
        g.Rect(18, 0, 4, 34, 'B');
        g.Rect(17, 2, 6, 2, 'W');
        // 頭（銀色。下が打つ面、左にくぎ抜き）
        g.Round(2, 32, 36, 14, 's');
        g.Round(3, 33, 34, 12, 'S');
        g.Rect(3, 42, 34, 3, 's');
        g.Rect(5, 34, 30, 2, 'L');
        g.Rect(0, 34, 3, 6, 's');
        return g.ToSprite("XC_Hammer");
    }

    /// <summary>能力のハンマー（横向き。頭が右。40×24）</summary>
    public static Sprite SkillHammer()
    {
        var g = new Grid(40, 24);
        g.Rect(0, 9, 28, 6, 'b');
        g.Rect(0, 10, 28, 4, 'B');
        g.Round(26, 0, 14, 24, 's');
        g.Round(27, 1, 12, 22, 'S');
        g.Rect(28, 2, 2, 20, 'L');
        return g.ToSprite("XC_SkillHammer");
    }

    /// <summary>HUD の枠の絵（ハンマー 16×16）</summary>
    public static Sprite HammerIcon()
    {
        var g = new Grid(16, 16);
        for (int i = 0; i < 10; i++) { g.Set(2 + i, 13 - i, 'B'); g.Set(3 + i, 13 - i, 'B'); g.Set(2 + i, 14 - i, 'b'); }
        g.Round(8, 1, 7, 6, 's');
        g.Round(9, 2, 5, 4, 'S');
        return g.ToSprite("XC_HammerIcon");
    }

    /// <summary>iPhone（Simulator。2×4 マス＝36×72 ドット）。on＝画面が点いている</summary>
    public static Sprite Phone(bool on)
    {
        var g = new Grid(36, 72);
        g.Round(0, 0, 36, 72, 'k');
        g.Round(1, 1, 34, 70, 'K');
        g.Round(3, 3, 30, 66, on ? 'C' : 'k');
        g.Round(12, 4, 12, 4, 'K'); // ノッチ
        if (on)
        {
            g.Rect(6, 14, 24, 7, 'W'); // 通知
            g.Rect(8, 16, 4, 3, 'B');
            g.Rect(14, 16, 13, 1, 's');
            g.Rect(14, 18, 9, 1, 's');
            g.Rect(6, 24, 24, 7, 'L');
            g.Rect(8, 26, 4, 3, 'R');
            g.Rect(14, 26, 12, 1, 's');
        }
        g.Rect(13, 66, 10, 1, on ? 'K' : 's'); // ホームバー
        return g.ToSprite(on ? "XC_PhoneOn" : "XC_PhoneOff");
    }

    /// <summary>通知の弾（白い角の丸い板に青い四角。16×10）</summary>
    public static Sprite Notification()
    {
        var g = new Grid(16, 10);
        g.Round(0, 0, 16, 10, 's');
        g.Round(1, 1, 14, 8, 'W');
        g.Rect(2, 3, 4, 4, 'B');
        g.Rect(7, 3, 6, 1, 's');
        g.Rect(7, 6, 4, 1, 's');
        return g.ToSprite("XC_Notification");
    }

    /// <summary>黄色い警告の三角に「!」（16×14）</summary>
    public static Sprite Warning()
    {
        var g = new Grid(16, 14);
        for (int y = 0; y < 14; y++)
        {
            int half = y / 2 + 1;
            g.Rect(8 - half, y, half * 2, 1, 'Y');
        }
        g.Rect(7, 4, 2, 5, 'K');
        g.Rect(7, 10, 2, 2, 'K');
        return g.ToSprite("XC_Warning");
    }

    /// <summary>床を走る衝撃（低い波。白と青。12×14）</summary>
    public static Sprite Shock()
    {
        var g = new Grid(12, 14);
        g.Rect(2, 4, 8, 10, 'C');
        g.Rect(4, 1, 4, 13, 'W');
        g.Rect(0, 9, 12, 5, 'C');
        g.Rect(1, 11, 10, 3, 'W');
        return g.ToSprite("XC_Shock");
    }

    // ───────── 道具 ─────────

    public static SpriteRenderer Box(Transform parent, Vector2 localCenter, Vector2 size, Color color, int order, string name = "Box")
        => FxKit.Box(parent, localCenter, size, color, order, name);

    public static void SetBox(SpriteRenderer sr, Vector2 localCenter, Vector2 size)
    {
        if (sr == null) return;
        sr.transform.localPosition = localCenter;
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(Mathf.Max(0f, size.x) / Mathf.Max(0.001f, native.x), Mathf.Max(0f, size.y) / Mathf.Max(0.001f, native.y), 1f);
    }

    public static TextMesh Text(Transform parent, string text, Vector2 localPos, float height, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        TextMesh tm = WorldText.Create(parent, text, Mathf.Max(0.4f, height), color, order, true, anchor);
        tm.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.001f);
        return tm;
    }

    /// <summary>床の赤い影（平たい楕円。幅 w マス）</summary>
    public static SpriteRenderer Shadow(Transform parent, Vector2 localPos, float w, int order)
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, PlaceholderSprites.Circle, new Color(Red.r, Red.g, Red.b, 0.6f), new Vector2(w, 0.4f), order, "Shadow");
        sr.transform.localPosition = localPos;
        return sr;
    }

    public static Hitbox AddHit(AttackRunner owner, AttackData data, Transform parent, Vector2 localCenter, Vector2 size, string name = "Hit")
    {
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        return owner.AddHitbox(go, data, col);
    }

    public static bool Blink(float left, float slow = 0.12f, float fast = 0.05f)
    {
        float step = left < 0.3f ? fast : slow;
        return left > 0.6f || Mathf.FloorToInt(Time.time / step) % 2 == 0;
    }

    public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    public static Vector2 HeadOf(AttackRunner owner)
    {
        if (owner == null) return Vector2.zero;
        var boss = owner.GetComponent<BossController>();
        if (boss != null)
        {
            Bounds b = boss.BodyBounds;
            return new Vector2(b.center.x, b.max.y + 0.5f);
        }
        return owner.OriginPoint + Vector2.up * 1.2f;
    }

    /// <summary>狙う相手（プレイヤー）の位置。いなければ部屋の真ん中</summary>
    public static Vector2 TargetOf(AttackRunner owner)
    {
        if (owner != null && owner.TargetPoint is Vector2 t) return t;
        return BattleArena.GetBounds().center;
    }
}
