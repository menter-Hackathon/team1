using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Discord のキットの絵（2026-11-19。docs/app-kits-1-design.md 3章）。コードの中のドット絵。1マス＝18ドット。
/// 色：青紫 #5865F2・オンラインの緑 #23A55A・赤 #ED4245・暗い地 #313338。
/// 本物のロゴ・サーバーの名前は使わない（丸いアイコンは色と白い形だけ）
/// </summary>
public static class DiscordArt
{
    public static readonly Color Blurple = RuntimeSprites.Hex("5865F2");
    public static readonly Color Green = RuntimeSprites.Hex("23A55A");
    public static readonly Color Red = RuntimeSprites.Hex("ED4245");
    public static readonly Color Dark = RuntimeSprites.Hex("313338");
    public static readonly Color Darker = RuntimeSprites.Hex("1E1F22");
    public static readonly Color Yellow = RuntimeSprites.Hex("FEE75C");

    /// <summary>サーバーのアイコンの色（順に）</summary>
    public static readonly Color[] ServerColors = { Blurple, Green, Yellow, Red };

    static readonly Color32 W = new Color32(255, 255, 255, 255);
    static readonly Color32 G = new Color32(0x23, 0xA5, 0x5A, 255);
    static readonly Color32 L = new Color32(0x57, 0xF2, 0x87, 255);
    static readonly Color32 R = new Color32(0xED, 0x42, 0x45, 255);
    static readonly Color32 K = new Color32(0x1E, 0x1F, 0x22, 255);

    /// <summary>音の波（緑の弧 3本。22×14。右へ進む向き）</summary>
    public static Sprite Wave => RuntimeSprites.FromAscii("Discord_Wave", new[]
    {
        "......GG.....GG....GG.",
        ".......GG.....GG....GG",
        "........GL.....GL....G",
        "........GL.....GL....G",
        ".........GL.....GL...G",
        ".........GL.....GL...G",
        ".........GL.....GL...G",
        ".........GL.....GL...G",
        ".........GL.....GL...G",
        ".........GL.....GL...G",
        "........GL.....GL....G",
        "........GL.....GL....G",
        ".......GG.....GG....GG",
        "......GG.....GG....GG.",
    }, new Dictionary<char, Color32> { { 'G', G }, { 'L', L } });

    /// <summary>サーバーのアイコン（丸。白で塗る。色は sr.color で）</summary>
    public static Sprite ServerDisc => CallArt.Disc(22);
    /// <summary>選ばれたサーバー（角の丸い四角。Discord の選択の形）</summary>
    public static Sprite ServerSelected => CallArt.Round(22, 22, 7);

    /// <summary>サーバーのアイコンの中の白い形（コントローラーの形。12×8）</summary>
    public static Sprite ServerGlyph => RuntimeSprites.FromAscii("Discord_Glyph", new[]
    {
        "..WWWWWWWW..",
        ".WWWWWWWWWW.",
        "WW.WWWWWW.WW",
        "W...WWWW...W",
        "WW.WWWWWW.WW",
        "WWWWWWWWWWWW",
        "WWW......WWW",
        ".W........W.",
    }, new Dictionary<char, Color32> { { 'W', W } });

    /// <summary>
    /// 色の付いたサーバーのアイコン（直径 22 の丸に、白いコントローラーの形）。selected：角の丸い四角（選ばれた形）。
    /// 弾の絵にも使う（i は色の番号）
    /// </summary>
    public static Sprite ServerIcon(int i, bool selected)
    {
        int n = ((i % ServerColors.Length) + ServerColors.Length) % ServerColors.Length;
        const int d = 22;
        string key = $"Discord_Server{n}_{(selected ? "S" : "C")}";
        Color32 c = ServerColors[n];
        string[] glyph =
        {
            "..WWWWWWWW..",
            ".WWWWWWWWWW.",
            "WW.WWWWWW.WW",
            "W...WWWW...W",
            "WW.WWWWWW.WW",
            "WWWWWWWWWWWW",
            "WWW......WWW",
            ".W........W.",
        };
        var rows = new string[d];
        float r = d * 0.5f;
        int gx = (d - 12) / 2, gy = (d - 8) / 2;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                bool inside;
                if (selected)
                {
                    // 角の丸い四角（角 7）
                    float cx = Mathf.Clamp(x + 0.5f, 7f, d - 7f), cy = Mathf.Clamp(y + 0.5f, 7f, d - 7f);
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    inside = dx * dx + dy * dy <= 49f;
                }
                else
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    inside = dx * dx + dy * dy <= r * r;
                }
                char ch = inside ? 'C' : '.';
                int lx = x - gx, ly = y - gy;
                if (inside && lx >= 0 && lx < 12 && ly >= 0 && ly < 8 && glyph[ly][lx] == 'W') ch = 'W';
                line[x] = ch;
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, new Dictionary<char, Color32> { { 'C', c }, { 'W', W } });
    }

    /// <summary>選んでいる印（白い棒。4×12）</summary>
    public static Sprite Pill => CallArt.Round(4, 12, 2);

    /// <summary>赤い斜線のマイク（ミュート。16×16）</summary>
    public static Sprite MutedMic => RuntimeSprites.FromAscii("Discord_MutedMic", new[]
    {
        "..............RR",
        "......WWWW...RRR",
        ".....WWWWWW.RRR.",
        ".....WWWWWWRRR..",
        ".....WWWWWRRR...",
        ".....WWWWRRR....",
        "..W..WWWRRRW..W.",
        "..W..WWRRRWW..W.",
        "..W...RRRWW...W.",
        "...W.RRRWW...W..",
        "....RRRWWWWWW...",
        "...RRR..WW......",
        "..RRR...WW......",
        ".RRR..WWWWWW....",
        "RRR.............",
        "RR..............",
    }, new Dictionary<char, Color32> { { 'W', W }, { 'R', R } });

    /// <summary>スピーカーの印（ボイスチャンネル。緑。12×10）</summary>
    public static Sprite Speaker => RuntimeSprites.FromAscii("Discord_Speaker", new[]
    {
        "....G.......",
        "...GG...G...",
        "..GGG....G..",
        "GGGGG..G..G.",
        "GGGGG...G.G.",
        "GGGGG...G.G.",
        "GGGGG..G..G.",
        "..GGG....G..",
        "...GG...G...",
        "....G.......",
    }, new Dictionary<char, Color32> { { 'G', L } });

    /// <summary>話している人の緑のふち（w×h ドット、太さ 2）</summary>
    public static Sprite SpeakingFrame(int w, int h) => CallArt.Frame(w, h, 2);

    /// <summary>ミュートの円（白い輪。色は sr.color で）</summary>
    public static Sprite MuteRing(int d) => CallArt.Ring(d, 3);

    /// <summary>能力のアイコン（HUD の枠。暗い地に赤い斜線のマイク。16×16）</summary>
    public static Sprite MuteIcon => RuntimeSprites.FromAscii("Discord_MuteIcon", new[]
    {
        "KKKKKKKKKKKKKKKK",
        "KKKKKKKKKKKKKRRK",
        "KKKKKKWWWWKKRRRK",
        "KKKKKWWWWWWRRRKK",
        "KKKKKWWWWWRRRKKK",
        "KKKKKWWWWRRRKKKK",
        "KKWKKWWWRRRWKKWK",
        "KKWKKWWRRRWWKKWK",
        "KKWKKKRRRWWKKKWK",
        "KKKWKRRRWWKKKWKK",
        "KKKKRRRWWWWWWKKK",
        "KKKRRRKKWWKKKKKK",
        "KKRRRKKKWWKKKKKK",
        "KRRRKKWWWWWWKKKK",
        "KRRKKKKKKKKKKKKK",
        "KKKKKKKKKKKKKKKK",
    }, new Dictionary<char, Color32> { { 'W', W }, { 'R', R }, { 'K', K } });

    /// <summary>子の絵を1枚出す（大きさはマス。絵を伸び縮みさせる）</summary>
    public static SpriteRenderer Put(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Art")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, sprite, color, size, order, name);
        sr.transform.localPosition = localPos;
        return sr;
    }
}
