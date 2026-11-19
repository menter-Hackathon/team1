using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】専用の技の絵を、コードの中のドット絵（文字の並び）から作る（2026-11-18）。絵のファイルを足さずに済む。
/// 1文字＝1ドット。'.' と ' ' は透明。色は palette で文字ごとに決める。1マス＝18ドット（ゲーム中の物と同じ）。
/// 同じ key は1回だけ作って使い回す。デザイン担当の絵ができたら、AttackData の絵やこの呼び出しを差し替えてよい
/// </summary>
public static class RuntimeSprites
{
    public const float PixelsPerUnit = 18f;
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>文字の並び（上の行から）からスプライトを作る。pivot は (0.5, 0.5) が真ん中</summary>
    public static Sprite FromAscii(string key, string[] rows, IReadOnlyDictionary<char, Color32> palette, Vector2? pivot = null)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int h = rows.Length, w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var tex = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false)
        {
            name = $"Runtime_{key}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[tex.width * tex.height];
        for (int y = 0; y < h; y++)
        {
            string row = rows[y];
            for (int x = 0; x < tex.width; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Color32 col = c == '.' || c == ' ' || !palette.TryGetValue(c, out Color32 pc) ? new Color32(0, 0, 0, 0) : pc;
                pixels[(h - 1 - y) * tex.width + x] = col; // テクスチャは下の行から
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot ?? new Vector2(0.5f, 0.5f), PixelsPerUnit);
        sprite.name = key;
        sprite.hideFlags = HideFlags.DontSave;
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>16進の色（"#RRGGBB" か "RRGGBB"、"RRGGBBAA" も可）</summary>
    public static Color32 Hex(string hex)
    {
        hex = hex.TrimStart('#');
        byte P(int i) => System.Convert.ToByte(hex.Substring(i, 2), 16);
        return new Color32(P(0), P(2), P(4), hex.Length >= 8 ? P(6) : (byte)255);
    }

    /// <summary>スプライトを1枚表示する（子の GameObject を作る）。大きさは絵の元の大きさ（1マス＝18ドット）× scale</summary>
    public static SpriteRenderer Show(Transform parent, Sprite sprite, Vector2 localPos, int sortingOrder, float scale = 1f, string objectName = "Sprite")
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}

/// <summary>【仮】ゲームの世界の中に文字を出す（TextMesh。2026-11-18：降ってくる文字列・カーネル・パニックの背景の文字など）</summary>
public static class WorldText
{
    /// <summary>
    /// 文字を出す。height は1行の高さ（マス）。英字は Press Start 2P、日本語は DotGothic16（GameArt に入っていれば）
    /// </summary>
    public static TextMesh Create(Transform parent, string text, float height, Color color, int sortingOrder,
        bool english = true, TextAnchor anchor = TextAnchor.MiddleCenter, string objectName = "Text")
    {
        var go = new GameObject(objectName);
        go.layer = parent != null ? parent.gameObject.layer : 0;
        if (parent != null) go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        Font font = english ? PixelGui.FontEn : PixelGui.FontJa;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        tm.fontSize = 32;
        tm.characterSize = height * 10f / tm.fontSize; // 1行の高さ ≒ fontSize × characterSize ÷ 10
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.text = text;
        SetColor(tm, color);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = sortingOrder;
        return tm;
    }

    /// <summary>
    /// 文字の色を入れる。このプロジェクトはリニアの色空間で、TextMesh の色はそのままだと明るく出るので、直してから入れる
    /// （画面の上の表示の PixelGui.ToGui と同じ）。作ったあとに色を変えるときも、これを使う
    /// </summary>
    public static void SetColor(TextMesh tm, Color color)
    {
        if (tm != null) tm.color = PixelGui.ToGui(color);
    }
}

/// <summary>【仮】画面の上の表示（OnGUI）の道具（2026-11-18）</summary>
public static class ScreenUi
{
    /// <summary>ゲームの世界の位置を、PixelGui の座標（左上が 0、ドットの倍率で割った値）に直す。カメラが無ければ null</summary>
    public static Vector2? WorldToGui(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 s = cam.WorldToScreenPoint(world);
        if (s.z < 0f) return null;
        return new Vector2(s.x / PixelGui.Scale, (Screen.height - s.y) / PixelGui.Scale);
    }

    /// <summary>PixelGui の座標を、ゲームの世界の位置に直す（z は 0）</summary>
    public static Vector2 GuiToWorld(Vector2 gui)
    {
        Camera cam = Camera.main;
        if (cam == null) return gui;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(gui.x * PixelGui.Scale, Screen.height - gui.y * PixelGui.Scale, -cam.transform.position.z));
        return w;
    }

    /// <summary>カメラに映っている範囲（ゲームの世界）。カメラが無ければ戦う部屋の範囲</summary>
    public static Rect ViewRect()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return BattleArena.GetBounds();
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
    }
}
