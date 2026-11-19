using System.Collections.Generic;
using UnityEngine;

// 【仮】KitMotion の予告・印の絵（2026-11-19）。担当0 の物。どれも白（SpriteRenderer の色で塗る）。1マス＝18ドット

/// <summary>KitMotion の予告・印の絵</summary>
public static class KitMotionArt
{
    static readonly Dictionary<char, Color32> White = new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } };

    /// <summary>右向きの矢印（7×7。画面の端の予告）</summary>
    public static Sprite Arrow => RuntimeSprites.FromAscii("KitMotion_Arrow", new[]
    {
        "...W...",
        "...WW..",
        "WWWWWW.",
        "WWWWWWW",
        "WWWWWW.",
        "...WW..",
        "...W...",
    }, White);

    /// <summary>右向きの山かっこ（5×7。動く床の流れ）</summary>
    public static Sprite Chevron => RuntimeSprites.FromAscii("KitMotion_Chevron", new[]
    {
        "WW...",
        ".WW..",
        "..WW.",
        "...WW",
        "..WW.",
        ".WW..",
        "WW...",
    }, White);

    /// <summary>床の割れ目（12×5。下からの予告。横に並べる）</summary>
    public static Sprite Crack => RuntimeSprites.FromAscii("KitMotion_Crack", new[]
    {
        "....W.......",
        "...W.W....W.",
        "WWW...W..W.W",
        ".......WW...",
        "........W...",
    }, White, new Vector2(0.5f, 0.6f));

    /// <summary>「！」（3×9）</summary>
    public static Sprite Exclaim => RuntimeSprites.FromAscii("KitMotion_Exclaim", new[]
    {
        "WWW",
        "WWW",
        "WWW",
        "WWW",
        ".W.",
        "...",
        "...",
        "WWW",
        "WWW",
    }, White);

    /// <summary>白い円（d ドット）</summary>
    public static Sprite Disc(int d) => FxArt.Disc(d);

    /// <summary>白い輪（直径 d ドット・太さ 1ドット）。dotted なら点線</summary>
    public static Sprite Ring(int d, bool dotted = false)
    {
        d = Mathf.Clamp(d, 4, 64);
        string key = $"KitMotion_Ring{d}{(dotted ? "d" : "")}";
        var rows = new string[d];
        float r = d * 0.5f;
        int segs = Mathf.Max(8, d / 2) & ~1; // 点線の数（偶数）
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool on = dist <= r && dist >= r - 1.1f;
                if (on && dotted)
                {
                    float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                    on = Mathf.FloorToInt(a * segs) % 2 == 0;
                }
                line[x] = on ? 'W' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, White);
    }

    /// <summary>白い四角（n ドット）</summary>
    public static Sprite Dot(int n) => FxArt.Square(n);

    /// <summary>横の点線（長さ lengthPx ドット・太さ 2ドット。3ドット描いて 2ドットあける）。縦にするときは 90度回す</summary>
    public static Sprite Dashes(int lengthPx)
    {
        lengthPx = Mathf.Clamp(lengthPx, 2, 400);
        var line = new char[lengthPx];
        for (int x = 0; x < lengthPx; x++) line[x] = x % 5 < 3 ? 'W' : '.';
        string row = new string(line);
        return RuntimeSprites.FromAscii($"KitMotion_Dash{lengthPx}", new[] { row, row }, White);
    }

    /// <summary>点線の四角い枠を出す（center・size はマス。seconds 秒で消える。最後の 0.3秒は点滅）。parent の子</summary>
    public static void DashedFrame(Transform parent, Vector2 center, Vector2 size, Color color, int order, float seconds)
    {
        float px = KitDress.Px;
        int w = Mathf.Max(2, Mathf.RoundToInt(size.x / px));
        int h = Mathf.Max(2, Mathf.RoundToInt(size.y / px));
        Vector2 hs = new Vector2(w * px, 2f * px), vs = new Vector2(h * px, 2f * px);
        KitMotionMark.Create(parent, Dashes(w), center + new Vector2(0f, size.y * 0.5f - px), hs, 0f, color, order, seconds, "FrameTop");
        KitMotionMark.Create(parent, Dashes(w), center + new Vector2(0f, -size.y * 0.5f + px), hs, 0f, color, order, seconds, "FrameBottom");
        KitMotionMark.Create(parent, Dashes(h), center + new Vector2(-size.x * 0.5f + px, 0f), vs, 90f, color, order, seconds, "FrameLeft");
        KitMotionMark.Create(parent, Dashes(h), center + new Vector2(size.x * 0.5f - px, 0f), vs, 90f, color, order, seconds, "FrameRight");
    }
}

/// <summary>
/// 【仮】予告の印1枚（矢印・割れ目・点線の丸など）。ゆっくり明滅して、最後の 0.3秒は速く点滅し、時間が来たら消える。
/// seconds が PositiveInfinity なら、親が消えるまで明滅し続ける
/// </summary>
public class KitMotionMark : MonoBehaviour
{
    SpriteRenderer _sr;
    Color _color;
    float _endAt;
    float _born;

    /// <summary>絵</summary>
    public SpriteRenderer Renderer => _sr;

    /// <summary>印を出す（pos は世界の位置。size はマス。angle は度）。parent の子にする</summary>
    public static KitMotionMark Create(Transform parent, Sprite sprite, Vector2 pos, Vector2 size, float angle, Color color, int order, float seconds, string objectName = "Mark")
    {
        var go = new GameObject(objectName);
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var m = go.AddComponent<KitMotionMark>();
        m._sr = AttackRunner.AddVisual(go.transform, sprite, color, size, order);
        m._color = color;
        m._born = Time.time;
        m._endAt = Time.time + Mathf.Max(0.05f, seconds);
        return m;
    }

    void Update()
    {
        float left = _endAt - Time.time;
        if (left <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (_sr == null) return;
        float a;
        if (left < 0.3f) a = Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? 1f : 0.25f;
        else a = 0.7f + 0.3f * Mathf.Sin((Time.time - _born) * 9f);
        Color c = _color;
        c.a *= a;
        _sr.color = c;
    }
}
