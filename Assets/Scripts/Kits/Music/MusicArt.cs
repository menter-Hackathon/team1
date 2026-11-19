using System.Collections.Generic;
using UnityEngine;

// 【仮】ミュージック（Apple Music）のキットの絵（2026-11-19。docs/app-kits-2-design.md 4-1）。コードの中のドット絵だけ。
// 赤 #FA243C 〜 ピンク #FB5C74。音符と棒。曲名は出さない

/// <summary>ミュージックのキットの絵</summary>
public static class MusicArt
{
    public static readonly Color32 Red = RuntimeSprites.Hex("FA243C");
    public static readonly Color32 Pink = RuntimeSprites.Hex("FB5C74");
    public static readonly Color32 Light = RuntimeSprites.Hex("FFC2CC");
    public static readonly Color32 Dark = RuntimeSprites.Hex("2B0A10");
    public static readonly Color32 Warn = RuntimeSprites.Hex("FF453A");

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'r', Red }, { 'p', Pink }, { 'l', Light }, { 'd', Dark }, { 'w', new Color32(255, 255, 255, 255) },
        { 'g', RuntimeSprites.Hex("3A3A3C") }, { 'k', RuntimeSprites.Hex("1C1C1E") },
    };

    /// <summary>音符（10ドット）。i で形を変える（八分・連こう）</summary>
    public static Sprite Note(int i) => (i & 1) == 0
        ? RuntimeSprites.FromAscii("Music_Note8", new[]
        {
            "....rr....",
            "....rrr...",
            "....r.rr..",
            "....r..r..",
            "....r.....",
            "....r.....",
            "..ppr.....",
            ".pppp.....",
            ".pppp.....",
            "..pp......",
        }, Pal)
        : RuntimeSprites.FromAscii("Music_Note16", new[]
        {
            "...rrrrrr.",
            "...rrrrrr.",
            "...r....r.",
            "...r....r.",
            "...r....r.",
            ".ppr..ppr.",
            "pppp.pppp.",
            "pppp.pppp.",
            ".pp...pp..",
            "..........",
        }, Pal);

    /// <summary>音の波（弧。8×27 ドット＝0.45×1.5マス）。右向き</summary>
    public static Sprite Wave => RuntimeSprites.FromAscii("Music_Wave", new[]
    {
        "rr......",
        ".rr.....",
        "..rr....",
        "..prr...",
        "...prr..",
        "...lpr..",
        "....lpr.",
        "....lpr.",
        "....lprr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        ".....lpr",
        "....lprr",
        "....lpr.",
        "....lpr.",
        "...lpr..",
        "...prr..",
        "..prr...",
        "..rr....",
        ".rr.....",
        "rr......",
    }, Pal);

    /// <summary>スピーカー（14×36 ドット）</summary>
    public static Sprite Speaker => RuntimeSprites.FromAscii("Music_Speaker", BuildSpeaker(), Pal);

    static string[] BuildSpeaker()
    {
        var rows = new List<string>();
        rows.Add(".kkkkkkkkkkkk.");
        for (int i = 0; i < 34; i++)
        {
            string mid;
            int y = i;
            bool small = y >= 3 && y <= 10, big = y >= 15 && y <= 30;
            if (small)
            {
                int d = Mathf.Abs(y - 6) * 2;
                mid = d <= 2 ? "ggglllllggg" : d <= 6 ? "gggppppggg." : "gggggggggg.";
            }
            else if (big)
            {
                int d = Mathf.Abs(y - 22);
                mid = d <= 2 ? "ggwwwwwwwgg" : d <= 5 ? "gpppppppppg" : d <= 7 ? "ggrrrrrrrgg" : "ggggggggggg";
            }
            else mid = "ggggggggggg";
            if (mid.Length < 11) mid = mid.PadRight(11, 'g');
            rows.Add("kg" + mid.Substring(0, 11) + "k");
        }
        rows.Add(".kkkkkkkkkkkk.");
        return rows.ToArray();
    }

    /// <summary>シャッフルの印（交差した矢印。12ドット）</summary>
    public static Sprite Shuffle => RuntimeSprites.FromAscii("Music_Shuffle", new[]
    {
        ".........w..",
        "ww......www.",
        "..w....w.w.w",
        "...w..w..w..",
        "....ww......",
        "....ww......",
        "...w..w..w..",
        "..w....w.w.w",
        "ww......www.",
        ".........w..",
    }, Pal);

    /// <summary>リピートの印（回る矢印。12ドット）</summary>
    public static Sprite Repeat => RuntimeSprites.FromAscii("Music_Repeat", new[]
    {
        "........w...",
        ".wwwwwwwww..",
        "w.......w...",
        "w...........",
        "w..........w",
        "...........w",
        "...w.......w",
        "..wwwwwwwww.",
        "...w........",
    }, Pal);

    /// <summary>ビートの輪（足元で縮む。円の線）</summary>
    public static Sprite Ring(int d)
    {
        string key = "Music_Ring" + d;
        var rows = new string[d];
        float r = d * 0.5f - 0.5f;
        for (int y = 0; y < d; y++)
        {
            var c = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dist = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                c[x] = dist <= r + 0.3f && dist >= r - 1.1f ? 'w' : '.';
            }
            rows[y] = new string(c);
        }
        return RuntimeSprites.FromAscii(key, rows, Pal);
    }

    /// <summary>イコライザーの棒の色（下が赤、上がピンク）</summary>
    public static Color BarColor(float k) => Color.Lerp(Red, Pink, Mathf.Clamp01(k));
}
