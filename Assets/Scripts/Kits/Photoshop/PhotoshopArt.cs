using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Photoshop のキットの絵（2026-11-19。docs/app-kits-2-design.md 3-1）。コードの中のドット絵。
/// 紺 #001E36・水色 #31A8FF・流れる白黒の点線（選択範囲）・透明の市松もよう（Creative のスキンの CreativeArt.Checker と同じ色）・
/// 丸いブラシのカーソル・コピースタンプの照準（⌥ クリックの丸に十字）・レイヤーの目（CreativeArt.EyeOpen／EyeClosed）
/// </summary>
public static class PhotoshopArt
{
    public static readonly Color Navy = RuntimeSprites.Hex("001E36");
    public static readonly Color Blue = RuntimeSprites.Hex("31A8FF");
    public static readonly Color BlueLight = RuntimeSprites.Hex("A6DBFF");
    public static readonly Color Panel = RuntimeSprites.Hex("323232");
    public static readonly Color PanelRow = RuntimeSprites.Hex("3E3E3E");
    public static readonly Color PanelLine = RuntimeSprites.Hex("1E1E1E");
    public static readonly Color CheckGray = RuntimeSprites.Hex("CCCCCC");

    /// <summary>丸いブラシのカーソル（輪。d ドット）</summary>
    public static Sprite BrushRing(int d) => Ring($"PsBrushRing_{d}", d, 1);

    /// <summary>コピースタンプの照準（丸に十字。15×15）</summary>
    public static Sprite Reticle => RuntimeSprites.FromAscii("PsReticle", new[]
    {
        ".......w.......",
        ".....wwwww.....",
        "...ww..w..ww...",
        "..w....w....w..",
        "..w....w....w..",
        ".w.....w.....w.",
        ".w.....w.....w.",
        "wwwwwwwwwwwwwww",
        ".w.....w.....w.",
        ".w.....w.....w.",
        "..w....w....w..",
        "..w....w....w..",
        "...ww..w..ww...",
        ".....wwwww.....",
        ".......w.......",
    }, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>ふりの三日月（白。色は SpriteRenderer で）。幅 w・高さ h ドット</summary>
    public static Sprite Slash(int w, int h)
    {
        string key = $"PsSlash_{w}_{h}";
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            float v = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float rr = Mathf.Sqrt(u * u + v * v);
                c[x] = rr <= 1f && rr >= 0.72f ? 'w' : '.';
            }
            rows[y] = new string(c);
        }
        return RuntimeSprites.FromAscii(key, rows, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) } });
    }

    /// <summary>輪（d ドット、太さ t）</summary>
    public static Sprite Ring(string key, int d, int t)
    {
        var rows = new string[d];
        float c = (d - 1) * 0.5f, r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var row = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                row[x] = dist <= r - 0.2f && dist >= r - t - 0.6f ? 'w' : '.';
            }
            rows[y] = new string(row);
        }
        return RuntimeSprites.FromAscii(key, rows, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) } });
    }

    static Sprite _checkerTile;

    /// <summary>並べて使う市松もようのタイル（16×16 ドット、ます目 8。白と #CCCCCC）。drawMode＝Tiled で使う</summary>
    public static Sprite CheckerTile
    {
        get
        {
            if (_checkerTile != null) return _checkerTile;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            Color32 a = new Color32(255, 255, 255, 255), b = CheckGray;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = ((x / 8) + (y / 8)) % 2 == 0 ? a : b;
            tex.SetPixels32(px);
            tex.Apply();
            _checkerTile = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            _checkerTile.hideFlags = HideFlags.DontSave;
            return _checkerTile;
        }
    }

    /// <summary>プレイヤーの絵（無ければ白い四角）</summary>
    public static Sprite PlayerSprite(int frame = 0)
    {
        Sprite[] walk = GameArt.Instance != null ? GameArt.Instance.PlayerWalk : null;
        if (walk != null && walk.Length > 0) return walk[((frame % walk.Length) + walk.Length) % walk.Length];
        return PlaceholderSprites.Square;
    }

    public static int PlayerFrames => GameArt.Instance != null && GameArt.Instance.PlayerWalk != null ? GameArt.Instance.PlayerWalk.Length : 1;

    /// <summary>四角の流れる点線（白と黒）を parent の子に作る。PhotoshopAnts が流す</summary>
    public static PhotoshopAnts AntsRect(Transform parent, Rect r, int order)
    {
        var go = new GameObject("Ants");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = parent.position;
        var pts = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
        var ants = go.AddComponent<PhotoshopAnts>();
        ants.Parts = PathStroke.Dashes(go.transform, pts, 0.17f, 0.0001f, KitDress.Px, Color.white, order, true);
        return ants;
    }
}

/// <summary>流れる点線（白と黒を 0.12秒ごとに入れ替える）</summary>
public class PhotoshopAnts : MonoBehaviour
{
    internal List<SpriteRenderer> Parts = new List<SpriteRenderer>();

    void Update()
    {
        bool flip = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
        for (int i = 0; i < Parts.Count; i++)
        {
            if (Parts[i] == null) continue;
            Parts[i].color = (i % 2 == 0) == flip ? Color.white : Color.black;
        }
    }
}
