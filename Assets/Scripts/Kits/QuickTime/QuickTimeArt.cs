using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player のキットの色と絵（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// 黒い半透明の角丸のバー、白いアイコン、トリムは黄 #FFD60A、録画は赤い丸。
/// </summary>
public static class QuickTimeArt
{
    public static readonly Color Bar = new Color(0f, 0f, 0f, 0.62f);
    public static readonly Color Yellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color Rec = RuntimeSprites.Hex("FF3B30");
    public static readonly Color Ball = RuntimeSprites.Hex("F2F2F7");

    static readonly Color32 White32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', White32 } };

    /// <summary>大きい丸い弾（白に灰色のふち・中に再生の三角。16ドット）</summary>
    public static Sprite BallSprite => RuntimeSprites.FromAscii("QuickTime_Ball", new[]
    {
        ".....GGGGGG.....",
        "...GGWWWWWWGG...",
        "..GWWWWWWWWWWG..",
        ".GWWWWWWWWWWWWG.",
        ".GWWWWKWWWWWWWG.",
        "GWWWWWKKWWWWWWWG",
        "GWWWWWKKKWWWWWWG",
        "GWWWWWKKKKWWWWWG",
        "GWWWWWKKKKWWWWWG",
        "GWWWWWKKKWWWWWWG",
        "GWWWWWKKWWWWWWWG",
        ".GWWWWKWWWWWWWG.",
        ".GWWWWWWWWWWWWG.",
        "..GWWWWWWWWWWG..",
        "...GGWWWWWWGG...",
        ".....GGGGGG.....",
    }, new Dictionary<char, Color32>
    {
        { 'G', new Color32(0x8E, 0x8E, 0x93, 0xFF) },
        { 'W', new Color32(0xF2, 0xF2, 0xF7, 0xFF) },
        { 'K', new Color32(0x1C, 0x1C, 0x1E, 0xFF) },
    });

    /// <summary>巻き戻しの印「⏪」（白。14×8 ドット）</summary>
    public static Sprite RewindIcon => RuntimeSprites.FromAscii("QuickTime_Rewind", new[]
    {
        "....XX.....XX.",
        "..XXXX...XXXX.",
        "XXXXXX.XXXXXX.",
        "XXXXXXXXXXXXX.",
        "XXXXXXXXXXXXX.",
        "XXXXXX.XXXXXX.",
        "..XXXX...XXXX.",
        "....XX.....XX.",
    }, Mono);

    /// <summary>録画の赤い丸（10ドット）</summary>
    public static Sprite RecDot => RuntimeSprites.FromAscii("QuickTime_RecDot", new[]
    {
        "...XXXX...",
        ".XXXXXXXX.",
        ".XXXXXXXX.",
        "XXXXXXXXXX",
        "XXXXXXXXXX",
        "XXXXXXXXXX",
        "XXXXXXXXXX",
        ".XXXXXXXX.",
        ".XXXXXXXX.",
        "...XXXX...",
    }, Mono);

    /// <summary>トリムのつまみ（黄色の縦長。6×14 ドット。内側に暗い線）</summary>
    public static Sprite Handle => RuntimeSprites.FromAscii("QuickTime_Handle", new[]
    {
        ".YYYY.",
        "YYYYYY",
        "YYYYYY",
        "YYYYYY",
        "YYKKYY",
        "YYKKYY",
        "YYKKYY",
        "YYKKYY",
        "YYKKYY",
        "YYKKYY",
        "YYYYYY",
        "YYYYYY",
        "YYYYYY",
        ".YYYY.",
    }, new Dictionary<char, Color32> { { 'Y', new Color32(0xFF, 0xD6, 0x0A, 0xFF) }, { 'K', new Color32(0x6B, 0x55, 0x00, 0xFF) } });

    static Sprite _stripes;

    /// <summary>ななめのしま（白。並べて描く用に FullRect。8×8 ドット）</summary>
    public static Sprite Stripes
    {
        get
        {
            if (_stripes != null) return _stripes;
            const int n = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "QuickTime_Stripes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = ((x + y) % n) < 3 ? White32 : new Color32(255, 255, 255, 70);
            tex.SetPixels32(px);
            tex.Apply();
            _stripes = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            _stripes.name = "QuickTime_Stripes";
            _stripes.hideFlags = HideFlags.DontSave;
            return _stripes;
        }
    }

    /// <summary>速さの印（8ドット。HUD の倍率の印）</summary>
    public static Sprite SpeedIcon => RuntimeSprites.FromAscii("QuickTime_SpeedIcon", new[]
    {
        "X...X...",
        "XX..XX..",
        "XXX.XXX.",
        "XXXXXXXX",
        "XXXXXXXX",
        "XXX.XXX.",
        "XX..XX..",
        "X...X...",
    }, Mono);

    /// <summary>しまの板を置く（parent の子。world の真ん中と大きさ）</summary>
    public static SpriteRenderer StripeBox(Transform parent, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject("Stripes");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Stripes;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>札（黒い半透明の角丸＋白い字。"2x" など）を置く</summary>
    public static TextMesh Tag(Transform parent, string text, Vector2 localPos, int order)
    {
        AttackRunner.AddVisual(parent, PlaceholderSprites.Square, Bar, new Vector2(Mathf.Max(0.9f, text.Length * 0.4f + 0.3f), 0.6f), order, "TagBg").transform.localPosition = localPos;
        TextMesh tm = WorldText.Create(parent, text, 0.4f, Color.white, order + 1, true);
        tm.transform.localPosition = new Vector3(localPos.x, localPos.y + 0.02f, -0.001f);
        return tm;
    }
}

/// <summary>相手の頭の上についていく札（"2x"・"0.5x"。相手が消えたら消える）</summary>
public class QuickTimeTag : MonoBehaviour
{
    Transform _target;
    float _height;
    TextMesh _text;
    SpriteRenderer _bg;
    float _blinkUntil;

    public static QuickTimeTag Attach(Transform target, string text, float height)
    {
        var go = new GameObject("QuickTimeTag");
        go.transform.position = target.position + Vector3.up * height;
        var t = go.AddComponent<QuickTimeTag>();
        t._target = target;
        t._height = height;
        t._text = QuickTimeArt.Tag(go.transform, text, Vector2.zero, 62);
        t._bg = go.transform.Find("TagBg").GetComponent<SpriteRenderer>();
        return t;
    }

    /// <summary>字を変える</summary>
    public void SetText(string text)
    {
        if (_text != null) _text.text = text;
        if (_bg != null)
        {
            Vector2 size = new Vector2(Mathf.Max(0.9f, text.Length * 0.4f + 0.3f), 0.6f);
            Vector2 native = _bg.sprite.bounds.size;
            _bg.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        }
    }

    /// <summary>seconds 秒点滅する（変わる前）</summary>
    public void Blink(float seconds) => _blinkUntil = Time.time + seconds;

    void LateUpdate()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        Health h = _target.GetComponent<Health>();
        if (h != null && h.IsDead) { Destroy(gameObject); return; }
        transform.position = _target.position + Vector3.up * _height;
        bool on = Time.time >= _blinkUntil || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        if (_text != null) _text.gameObject.SetActive(on);
        if (_bg != null) _bg.enabled = on;
    }
}

/// <summary>
/// 【仮】QuickTime のまねの再生バー（OnGUI。2026-11-19）。担当A。画面の下に小さく。ステージの進み（雑魚の残り）でつまみが右へ動く（飾り）
/// </summary>
public class QuickTimeBar : MonoBehaviour
{
    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        StagePacer p = StagePacer.Current;
        float progress = p != null ? Mathf.Clamp01(p.Progress) : 0f;
        GUI.depth = 12;
        PixelGui.Begin(false);
        float w = 150f, h = 12f;
        float x = (PixelGui.Width - w) * 0.5f, y = PixelGui.Height - h - 4f;
        PixelGui.Fill(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.45f));
        // 再生の三角
        for (int i = 0; i < 4; i++) PixelGui.Fill(new Rect(x + 5f + i, y + 2f + i, 1f, 8f - i * 2f), Color.white);
        float bx = x + 14f, bw = w - 20f;
        PixelGui.Fill(new Rect(bx, y + 5f, bw, 2f), new Color(1f, 1f, 1f, 0.35f));
        PixelGui.Fill(new Rect(bx, y + 5f, Mathf.Round(bw * progress), 2f), Color.white);
        PixelGui.Fill(new Rect(bx + Mathf.Round(bw * progress) - 2f, y + 3f, 4f, 6f), Color.white);
        PixelGui.End();
    }
}
