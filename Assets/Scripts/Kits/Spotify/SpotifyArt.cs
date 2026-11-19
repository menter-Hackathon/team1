using System.Collections.Generic;
using UnityEngine;

// 【仮】Spotify のキットの絵（2026-11-19。docs/app-kits-2-design.md 4-2）。コードの中のドット絵だけ。
// 黒 #121212・緑 #1DB954。Wrapped はピンク #F037A5・黄 #FFF04D・青 #4100F5。曲名は出さない

/// <summary>Spotify のキットの絵</summary>
public static class SpotifyArt
{
    public static readonly Color32 Black = RuntimeSprites.Hex("121212");
    public static readonly Color32 Green = RuntimeSprites.Hex("1DB954");
    public static readonly Color32 Pink = RuntimeSprites.Hex("F037A5");
    public static readonly Color32 Yellow = RuntimeSprites.Hex("FFF04D");
    public static readonly Color32 Blue = RuntimeSprites.Hex("4100F5");
    public static readonly Color32 Warn = RuntimeSprites.Hex("FF453A");
    public static readonly Color32 Gray = RuntimeSprites.Hex("B3B3B3");

    static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
    {
        { 'g', Green }, { 'k', Black }, { 'w', new Color32(255, 255, 255, 255) }, { 'd', RuntimeSprites.Hex("0E7A35") },
        { 'y', Yellow }, { 'r', Warn }, { 's', Gray },
    };

    /// <summary>Wrapped のカードの色（3色を順に）</summary>
    public static Color CardColor(int i) => (i % 3) switch { 0 => (Color)Pink, 1 => (Color)Yellow, _ => (Color)Blue };
    /// <summary>カードの字の色（黄のカードは黒）</summary>
    public static Color CardInk(int i) => i % 3 == 1 ? (Color)Black : Color.white;
    /// <summary>カードの字</summary>
    public static string CardText(int i) => (i % 3) switch { 0 => "#1", 1 => "TOP 5", _ => "99%" };

    /// <summary>緑の音符（10ドット）</summary>
    public static Sprite Note => RuntimeSprites.FromAscii("Spotify_Note", new[]
    {
        "....gg....",
        "....ggg...",
        "....g.gg..",
        "....g..g..",
        "....g.....",
        "....g.....",
        "..ggg.....",
        ".gggg.....",
        ".gggg.....",
        "..gg......",
    }, Pal);

    /// <summary>スキップの印 ⏭（12×8）</summary>
    public static Sprite SkipIcon => RuntimeSprites.FromAscii("Spotify_Skip", new[]
    {
        "w....w....ww",
        "ww...ww...ww",
        "www..www..ww",
        "wwww.wwww.ww",
        "wwww.wwww.ww",
        "www..www..ww",
        "ww...ww...ww",
        "w....w....ww",
    }, Pal);

    /// <summary>再生キューの印（0＝↑ 高い弾・1＝↓ 低い弾・2＝● ねらう弾。12ドット。黒い丸の地に白）</summary>
    public static Sprite QueueIcon(int kind)
    {
        switch (kind)
        {
            case 0:
                return RuntimeSprites.FromAscii("Spotify_QUp", new[]
                {
                    "...kkkkkk...", ".kkkkwwkkkk.", ".kkkwwwwkkk.", "kkkwwwwwwkkk", "kkwwwwwwwwkk", "kkkkkwwkkkkk",
                    "kkkkkwwkkkkk", "kkkkkwwkkkkk", "kkkkkwwkkkkk", ".kkkkwwkkkk.", ".kkkkkkkkkk.", "...kkkkkk...",
                }, Pal);
            case 1:
                return RuntimeSprites.FromAscii("Spotify_QDown", new[]
                {
                    "...kkkkkk...", ".kkkkkkkkkk.", ".kkkkwwkkkk.", "kkkkkwwkkkkk", "kkkkkwwkkkkk", "kkkkkwwkkkkk",
                    "kkkkkwwkkkkk", "kkwwwwwwwwkk", "kkkwwwwwwkkk", ".kkkwwwwkkk.", ".kkkkwwkkkk.", "...kkkkkk...",
                }, Pal);
            default:
                return RuntimeSprites.FromAscii("Spotify_QAim", new[]
                {
                    "...kkkkkk...", ".kkkkkkkkkk.", ".kkkwwwwkkk.", "kkkwwwwwwkkk", "kkwwwwwwwwkk", "kkwwwrrwwwkk",
                    "kkwwwrrwwwkk", "kkwwwwwwwwkk", "kkkwwwwwwkkk", ".kkkwwwwkkk.", ".kkkkkkkkkk.", "...kkkkkk...",
                }, Pal);
        }
    }

    /// <summary>Premium の札（緑の丸の地に白い 3本の弧＝Spotify っぽい印。16ドット）</summary>
    public static Sprite Premium => RuntimeSprites.FromAscii("Spotify_Premium", new[]
    {
        ".....gggggg.....",
        "...gggggggggg...",
        "..gggggggggggg..",
        ".gggwwwwwwwwggg.",
        ".ggwggggggggwgg.",
        "gggggwwwwwwggggg",
        "ggggwggggggwgggg",
        "gggggggggggggggg",
        "ggggggwwwwgggggg",
        "gggggwggggwggggg",
        ".gggggggggggggg.",
        ".ggggggwwggggggg",
        "..gggggggggggg..",
        "...gggggggggg...",
        ".....gggggg.....",
        "................",
    }, Pal);

    /// <summary>広告の印（拡声器。8ドット。HUD の印）</summary>
    public static Sprite AdIcon => RuntimeSprites.FromAscii("Spotify_Ad", new[]
    {
        "......w.",
        "....www.",
        "ss.wwww.",
        "sswwwww.",
        "sswwwww.",
        "ss.wwww.",
        "....www.",
        "......w.",
    }, Pal);

    /// <summary>Premium の印（8ドット。HUD の印）</summary>
    public static Sprite PremiumIcon => RuntimeSprites.FromAscii("Spotify_PremiumIcon", new[]
    {
        "..gggg..",
        ".gwwwwg.",
        "gwggggwg",
        "ggwwwwgg",
        "gwggggwg",
        "gggwwggg",
        ".gggggg.",
        "..gggg..",
    }, Pal);
}
