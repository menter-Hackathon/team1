using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Arc のキットの絵（2026-11-19。docs/app-kits-2-design.md 4-5）。コードの中のドット絵。
/// ピンク #FF8FB1〜むらさき #8E7CFF〜水色 #7FD6FF のグラデーション・角が大きく丸い・左のサイドバーと Space の丸。
/// Chrome（タブ・恐竜）とは被らないように、グラデーションの帯・Little Arc の小窓・タブのアーカイブ・Zap の稲妻にする
/// </summary>
public static class ArcArt
{
    public static readonly Color Pink = RuntimeSprites.Hex("FF8FB1");
    public static readonly Color Purple = RuntimeSprites.Hex("8E7CFF");
    public static readonly Color Cyan = RuntimeSprites.Hex("7FD6FF");
    /// <summary>ウイルスの Space の色（青むらさき）</summary>
    public static readonly Color Virus = RuntimeSprites.Hex("3B2A8C");
    public static readonly Color VirusLight = RuntimeSprites.Hex("5B49D6");

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>グラデーションの板（w×h ドット、角を r ドット丸める）。vertical：上から下へ a→b→c。横なら左から右へ</summary>
    public static Sprite Gradient(int w, int h, int r, Color a, Color b, Color c, bool vertical, float alpha = 1f)
    {
        string key = $"ArcGrad_{w}_{h}_{r}_{ColorUtility.ToHtmlStringRGB(a)}{ColorUtility.ToHtmlStringRGB(b)}{ColorUtility.ToHtmlStringRGB(c)}_{vertical}_{alpha:0.00}";
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        w = Mathf.Clamp(w, 1, 512); h = Mathf.Clamp(h, 1, 512);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!InsideRound(x, y, w, h, r)) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                float t = vertical ? 1f - (y + 0.5f) / h : (x + 0.5f) / w;
                Color col = t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
                col.a = alpha;
                px[y * w + x] = col;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        s.hideFlags = HideFlags.DontSave;
        Cache[key] = s;
        return s;
    }

    static bool InsideRound(int x, int y, int w, int h, int r)
    {
        if (r <= 0) return true;
        int cx = x < r ? r : x >= w - r ? w - r - 1 : x;
        int cy = y < r ? r : y >= h - r ? h - r - 1 : y;
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r + 0.5f;
    }

    /// <summary>まるい弾（d ドット。ピンクからむらさき）</summary>
    public static Sprite Orb(int d) => Gradient(d, d, d / 2, Cyan, Purple, Pink, true);

    /// <summary>赤い矢印（7×9。右向き）</summary>
    public static Sprite Arrow => RuntimeSprites.FromAscii("ArcArrow", new[]
    {
        "rr.....",
        ".rr....",
        "..rr...",
        "...rr..",
        "....rr.",
        "...rr..",
        "..rr...",
        ".rr....",
        "rr.....",
    }, new Dictionary<char, Color32> { { 'r', KitDress.Red } });

    /// <summary>Space の切り替えボタン（18×36 ドット＝1×2マス。角の丸い板に、ピンクとむらさきの丸）。active：ウイルスの Space のとき光る</summary>
    public static Sprite SwitchButton(bool active)
    {
        string key = "ArcSwitch_" + active;
        if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
        const int w = 18, h = 36;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        Color plate = active ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.55f);
        Color edge = active ? Pink : new Color(0.7f, 0.7f, 0.75f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                if (InsideRound(x, y, w, h, 6))
                {
                    c = InsideRound(x - 1, y - 1, w - 2, h - 2, 5) && x > 0 && y > 0 && x < w - 1 && y < h - 1 ? plate : edge;
                    float d1 = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(9f, 25f));
                    float d2 = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(9f, 11f));
                    if (d1 <= 4.6f) c = Pink;                         // 上の丸：自分の Space
                    if (d2 <= 4.6f) c = active ? Purple : new Color(0.75f, 0.72f, 0.95f, 1f); // 下の丸：ウイルスの Space
                }
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit);
        s.hideFlags = HideFlags.DontSave;
        Cache[key] = s;
        return s;
    }

    /// <summary>稲妻（from から to へ、ぎざぎざの線）を parent の子に作る。白い芯とピンクの外側</summary>
    public static void Lightning(Transform parent, Vector2 from, Vector2 to, int order)
    {
        var pts = new List<Vector2> { from };
        Vector2 d = to - from;
        Vector2 n = new Vector2(-d.y, d.x).normalized;
        const int seg = 7;
        for (int i = 1; i < seg; i++) pts.Add(from + d * (i / (float)seg) + n * Random.Range(-0.35f, 0.35f));
        pts.Add(to);
        for (int i = 1; i < pts.Count; i++)
        {
            PathStroke.Bar(parent, pts[i - 1], pts[i], 5f * KitDress.Px, Pink, order, "Glow");
            PathStroke.Bar(parent, pts[i - 1], pts[i], 2f * KitDress.Px, Color.white, order + 1, "Core");
        }
    }
}
