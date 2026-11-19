using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Figma のキットの絵（2026-11-19。docs/app-kits-2-design.md 2-4）。コードの中のドット絵。
/// 選択の青 #0D99FF・コンポーネントの紫 #9747FF・カーソルの色（赤紫・緑・紫）・名札（Designer・PM・Client。固定の名前）・
/// 四角の灰色 #D9D9D9（Figma の四角の最初の色）
/// </summary>
public static class FigmaArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("0D99FF");
    public static readonly Color Purple = RuntimeSprites.Hex("9747FF");
    public static readonly Color RectGray = RuntimeSprites.Hex("D9D9D9");
    public static readonly Color Pink = RuntimeSprites.Hex("F24E99");
    /// <summary>カーソルの色（赤紫・緑・紫）</summary>
    public static readonly Color[] CursorColors = { RuntimeSprites.Hex("F24E1E"), RuntimeSprites.Hex("0ACF83"), RuntimeSprites.Hex("A259FF") };
    /// <summary>名札（固定の名前。本物の名前は出さない）</summary>
    public static readonly string[] Names = { "Designer", "PM", "Client" };

    public static Color CursorColor(int i) => CursorColors[((i % CursorColors.Length) + CursorColors.Length) % CursorColors.Length];
    public static string NameOf(int i) => Names[((i % Names.Length) + Names.Length) % Names.Length];

    /// <summary>矢印のカーソル（10×14。白いふち）。左上の先が絵の左上</summary>
    public static Sprite Cursor(Color c)
    {
        string key = "FigmaCursor_" + ColorUtility.ToHtmlStringRGB(c);
        return RuntimeSprites.FromAscii(key, new[]
        {
            "w.........",
            "ww........",
            "wcw.......",
            "wccw......",
            "wcccw.....",
            "wccccw....",
            "wcccccw...",
            "wccccccw..",
            "wcccccccw.",
            "wccccwwwww",
            "wccwcw....",
            "wcw.wcw...",
            "ww..wcw...",
            ".....ww...",
        }, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) }, { 'c', c } }, new Vector2(0.05f, 0.95f));
    }

    /// <summary>選択の枠の角の四角（5×5。白に青いふち）</summary>
    public static Sprite Handle => RuntimeSprites.FromAscii("FigmaHandle", new[]
    {
        "bbbbb",
        "bwwwb",
        "bwwwb",
        "bwwwb",
        "bbbbb",
    }, new Dictionary<char, Color32> { { 'b', Blue }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>赤くなった角の四角</summary>
    public static Sprite HandleRed => RuntimeSprites.FromAscii("FigmaHandleRed", new[]
    {
        "rrrrr",
        "rwwwr",
        "rwwwr",
        "rwwwr",
        "rrrrr",
    }, new Dictionary<char, Color32> { { 'r', KitDress.Red }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>コンポーネントの紫のひし形（11×11）</summary>
    public static Sprite Diamond => RuntimeSprites.FromAscii("FigmaDiamond", new[]
    {
        ".....p.....",
        "....ppp....",
        "...ppppp...",
        "..ppwwwpp..",
        ".ppwpppwpp.",
        "pppwpppwppp",
        ".ppwpppwpp.",
        "..ppwwwpp..",
        "...ppppp...",
        "....ppp....",
        ".....p.....",
    }, new Dictionary<char, Color32> { { 'p', Purple }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>ペンツールの点（白い四角に青いふち。7×7）</summary>
    public static Sprite Anchor => RuntimeSprites.FromAscii("FigmaAnchor", new[]
    {
        "bbbbbbb",
        "bwwwwwb",
        "bwwwwwb",
        "bwwwwwb",
        "bwwwwwb",
        "bwwwwwb",
        "bbbbbbb",
    }, new Dictionary<char, Color32> { { 'b', Blue }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>ハンドルの先の丸（5×5）</summary>
    public static Sprite HandleDot => RuntimeSprites.FromAscii("FigmaHandleDot", new[]
    {
        ".bbb.",
        "bwwwb",
        "bwwwb",
        "bwwwb",
        ".bbb.",
    }, new Dictionary<char, Color32> { { 'b', Blue }, { 'w', new Color32(255, 255, 255, 255) } });

    /// <summary>⌘D のキー（17×11。白いキーに黒い ⌘ と D）</summary>
    public static Sprite CmdD
    {
        get
        {
            string[] cmd = { ".o...o.", "o.o.o.o", ".ooooo.", "..o.o..", ".ooooo.", "o.o.o.o", ".o...o." };
            string[] d = { "oooo.", "o...o", "o...o", "o...o", "o...o", "o...o", "oooo." };
            var rows = new string[11];
            rows[0] = ".kkkkkkkkkkkkkkk.";
            rows[1] = "kwwwwwwwwwwwwwwwk";
            for (int i = 0; i < 7; i++) rows[2 + i] = ("kw" + cmd[i] + "w" + d[i] + "wk").Replace('.', 'w');
            rows[9] = "kwwwwwwwwwwwwwwwk";
            rows[10] = ".kkkkkkkkkkkkkkk.";
            return RuntimeSprites.FromAscii("FigmaCmdD", rows, new Dictionary<char, Color32>
            {
                { 'k', RuntimeSprites.Hex("8E8E93") }, { 'w', new Color32(255, 255, 255, 255) }, { 'o', RuntimeSprites.Hex("1C1C1E") },
            });
        }
    }

    /// <summary>角を1ドット欠いた四角（白。色は SpriteRenderer で付ける）</summary>
    public static Sprite RoundRect(int w, int h)
    {
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++)
            {
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                c[x] = corner ? '.' : 'X';
            }
            rows[y] = new string(c);
        }
        return RuntimeSprites.FromAscii($"FigmaRound_{w}_{h}", rows, new Dictionary<char, Color32> { { 'X', new Color32(255, 255, 255, 255) } });
    }

    /// <summary>名札（色つきの角丸に白い字）を parent の子に作る。左下が pos</summary>
    public static Transform NameTag(Transform parent, string name, Color c, Vector2 localPos, int order)
    {
        var root = new GameObject("NameTag").transform;
        root.gameObject.layer = parent.gameObject.layer;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        const float h = 0.42f;
        float w = name.Length * 0.3f + 0.24f;
        var bg = RuntimeSprites.Show(root, RoundRect(Mathf.RoundToInt(w * 18f), Mathf.RoundToInt(h * 18f)), new Vector2(w * 0.5f, -h * 0.5f), order, 1f, "Bg");
        bg.color = c;
        TextMesh t = WorldText.Create(root, name, 0.3f, Color.white, order + 1, true, TextAnchor.MiddleLeft, "Name");
        t.transform.localPosition = new Vector3(0.12f, -h * 0.5f, 0f);
        return root;
    }

    /// <summary>カーソル＋名札を1つ作る（親なし）。返すのは根元（カーソルの先の位置）</summary>
    public static Transform CursorWithTag(string name, Color c, Vector2 pos, int order, Transform parent = null)
    {
        var root = new GameObject("FigmaCursor").transform;
        if (parent != null) { root.gameObject.layer = parent.gameObject.layer; root.SetParent(parent, false); }
        root.position = pos;
        RuntimeSprites.Show(root, Cursor(c), Vector2.zero, order, 1f, "Arrow");
        NameTag(root, name, c, new Vector2(0.45f, -0.6f), order);
        return root;
    }

    /// <summary>選択の枠（青い細い線と角の四角）を root の子に作る。size はマス。線と角を返す</summary>
    public static List<SpriteRenderer> SelectionFrame(Transform root, Vector2 size, int order, Color? color = null)
    {
        var list = new List<SpriteRenderer>();
        Color c = color ?? Blue;
        Vector2 h = size * 0.5f;
        Vector2 o = root.position;
        float w = 1.5f * KitDress.Px;
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, h.y), o + new Vector2(h.x, h.y), w, c, order, "Top"));
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, -h.y), o + new Vector2(h.x, -h.y), w, c, order, "Bottom"));
        list.Add(PathStroke.Bar(root, o + new Vector2(-h.x, -h.y), o + new Vector2(-h.x, h.y), w, c, order, "Left"));
        list.Add(PathStroke.Bar(root, o + new Vector2(h.x, -h.y), o + new Vector2(h.x, h.y), w, c, order, "Right"));
        for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
                list.Add(RuntimeSprites.Show(root, Handle, new Vector2(ix * h.x, iy * h.y), order + 1, 1f, "Handle"));
        return list;
    }
}
