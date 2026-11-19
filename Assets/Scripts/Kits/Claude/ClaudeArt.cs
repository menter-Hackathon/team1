using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Claude のキットの色・ドット絵（2026-11-19。docs/app-kits-1-design.md 6章）。担当3 の物。
/// 絵はコードの中のドット絵（RuntimeSprites.FromAscii）。絵のファイルは足さない。
/// 色：オレンジ #D97757・クリーム色 #F0EEE6・濃い茶 #3D3929（どれも【仮】）
/// </summary>
public static class ClaudeArt
{
    public const float Px = 1f / 18f;

    public static readonly Color Orange = RuntimeSprites.Hex("D97757");
    public static readonly Color Cream = RuntimeSprites.Hex("F0EEE6");
    public static readonly Color Brown = RuntimeSprites.Hex("3D3929");
    public static readonly Color Light = RuntimeSprites.Hex("F5C9B4");
    /// <summary>危ない物の赤（予告の輪・数字）</summary>
    public static readonly Color Red = RuntimeSprites.Hex("FF453A");

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'o', RuntimeSprites.Hex("D97757") },
        { 'c', RuntimeSprites.Hex("F0EEE6") },
        { 'b', RuntimeSprites.Hex("3D3929") },
        { 'l', RuntimeSprites.Hex("F5C9B4") },
        { 'w', RuntimeSprites.Hex("FFFFFF") },
        { 'r', RuntimeSprites.Hex("FF453A") },
    };

    /// <summary>火花（ロゴのような、オレンジの 8本の線の星）。n は一辺のドット（奇数）</summary>
    public static Sprite Spark(int n = 13)
    {
        n = Mathf.Max(5, n | 1);
        int c = n / 2;
        var rows = new string[n];
        for (int y = 0; y < n; y++)
        {
            var ch = new char[n];
            for (int x = 0; x < n; x++)
            {
                int dx = x - c, dy = y - c;
                int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                bool ray = dx == 0 || dy == 0 || ax == ay;
                int r = Mathf.Max(ax, ay);
                // 斜めの線は少し短く（ロゴの、長さのそろわない線）
                int len = (ax == ay && ax != 0) ? c - 1 : c;
                if (r <= 1) ch[x] = r == 0 ? 'w' : 'l';
                else if (ray && r <= len) ch[x] = 'o';
                else ch[x] = '.';
            }
            rows[y] = new string(ch);
        }
        return RuntimeSprites.FromAscii("Claude_Spark" + n, rows, Pal);
    }

    /// <summary>火花の弾（小さい火花。白い芯）</summary>
    public static Sprite SparkBullet => Spark(7);

    /// <summary>HUD の印・メーターのアイコン（7ドット）</summary>
    public static Sprite Icon8 => RuntimeSprites.FromAscii("Claude_Icon8", new[]
    {
        "o..o..o",
        ".o.o.o.",
        "..ooo..",
        "ooowooo",
        "..ooo..",
        ".o.o.o.",
        "o..o..o",
    }, Pal);

    /// <summary>アーティファクトのパネル（横 3×縦 0.6 マス＝54×11 ドット。クリーム色に、オレンジの見出しの棒と茶色の行）</summary>
    public static Sprite ArtifactPanel
    {
        get
        {
            const int w = 54, h = 11;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var ch = new char[w];
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    bool edge = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    if (corner) ch[x] = '.';
                    else if (edge) ch[x] = 'b';
                    else if (y <= 2) ch[x] = 'o';                           // 見出しの棒
                    else if (y == 5 && x >= 3 && x < 30) ch[x] = 'b';       // 行
                    else if (y == 7 && x >= 3 && x < 42) ch[x] = 'l';
                    else ch[x] = 'c';
                }
                rows[y] = new string(ch);
            }
            // 見出しの左に、白い小さい火花の点
            rows[1] = Set(rows[1], 2, 'w');
            return RuntimeSprites.FromAscii("Claude_Artifact", rows, Pal);
        }
    }

    /// <summary>じっくり考えるの柱（オレンジのふち・クリーム色の中に、火花の線）。区画の大きさに伸ばして使う</summary>
    public static Sprite Beam
    {
        get
        {
            const int w = 12, h = 36;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var ch = new char[w];
                for (int x = 0; x < w; x++)
                {
                    if (x <= 1 || x >= w - 2) ch[x] = 'o';
                    else if (x == 2 || x == w - 3) ch[x] = 'l';
                    else ch[x] = (y + x) % 6 == 0 ? 'w' : 'c';
                }
                rows[y] = new string(ch);
            }
            return RuntimeSprites.FromAscii("Claude_Beam", rows, Pal);
        }
    }

    /// <summary>足元の印の輪（赤いだ円。床の上に平たく置く）</summary>
    public static Sprite MarkRing
    {
        get
        {
            const int w = 27, h = 9;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var ch = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) * 0.5f) / ((w - 1) * 0.5f);
                    float dy = (y - (h - 1) * 0.5f) / ((h - 1) * 0.5f);
                    float d = dx * dx + dy * dy;
                    ch[x] = d <= 1.05f && d >= 0.45f ? 'r' : '.';
                }
                rows[y] = new string(ch);
            }
            return RuntimeSprites.FromAscii("Claude_MarkRing", rows, Pal);
        }
    }

    static string Set(string row, int x, char c)
    {
        if (x < 0 || x >= row.Length) return row;
        var a = row.ToCharArray();
        a[x] = c;
        return new string(a);
    }
}
