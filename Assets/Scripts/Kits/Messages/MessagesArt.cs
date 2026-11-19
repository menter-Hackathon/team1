using UnityEngine;

/// <summary>
/// 【仮】メッセージ（Apple の Messages）のキットの色と絵（2026-11-19。docs/app-kits-1-design.md 16章）。コードの中のドット絵（1マス＝18ドット）
/// </summary>
public static class MessagesArt
{
    public static readonly Color32 Blue = RuntimeSprites.Hex("0A84FF");
    public static readonly Color32 BlueDark = RuntimeSprites.Hex("0060C8");
    public static readonly Color32 Green = RuntimeSprites.Hex("34C759");
    public static readonly Color32 Grey = RuntimeSprites.Hex("E5E5EA");
    public static readonly Color32 GreyDark = RuntimeSprites.Hex("8E8E93");
    public static readonly Color32 BalloonRed = RuntimeSprites.Hex("FF3B30");
    public static readonly Color32 BalloonYellow = RuntimeSprites.Hex("FFCC00");
    public static readonly Color32 BalloonPurple = RuntimeSprites.Hex("AF52DE");
    public static readonly Color32 LaserGreen = RuntimeSprites.Hex("30D158");
    public static readonly Color32 LaserPurple = RuntimeSprites.Hex("BF5AF2");

    /// <summary>花火の色（順に使う）</summary>
    public static readonly Color32[] FireworkColors = { BalloonRed, BalloonYellow, BalloonPurple, Blue, Green };

    /// <summary>アイコン（12×12。緑の角丸に白い吹き出し）</summary>
    public static Sprite Icon
    {
        get
        {
            Sprite s = TalkArt.Cached("Msg_Icon");
            if (s != null) return s;
            var c = new TalkCanvas(12, 12);
            c.Round(0, 0, 12, 12, 3, Green);
            c.Round(2, 2, 8, 6, 3, TalkArt.White);
            c.Put(2, 8, TalkArt.White);
            c.Put(1, 9, TalkArt.White);
            return c.ToSprite("Msg_Icon");
        }
    }

    /// <summary>スラムの大きい青い吹き出し（4×2.5マス＝72×45。しっぽは右下。白い文の棒）</summary>
    public static Sprite SlamBubble
    {
        get
        {
            Sprite s = TalkArt.Cached("Msg_SlamBubble");
            if (s != null) return s;
            var c = new TalkCanvas(72, 45);
            TalkArt.Bubble(c, 0, 0, 72, 45, Blue, BlueDark, true);
            c.Fill(10, 10, 46, 4, TalkArt.White);
            c.Fill(10, 18, 52, 4, TalkArt.White);
            c.Fill(10, 26, 30, 4, TalkArt.White);
            return c.ToSprite("Msg_SlamBubble");
        }
    }

    /// <summary>「…」の入力中の吹き出し（灰色。30×18）。frame で点の明るさが順に変わる（0〜2）</summary>
    public static Sprite TypingBubble(int frame)
    {
        int f = ((frame % 3) + 3) % 3;
        string key = "Msg_Typing_" + f;
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(30, 18);
        c.Round(0, 0, 30, 15, 7, Grey);
        c.Disc(3f, 16f, 2.5f, Grey);
        for (int i = 0; i < 3; i++) c.Disc(8.5f + i * 6.5f, 7.5f, 2.5f, i == f ? Color.Lerp(GreyDark, Color.black, 0.3f) : (Color)GreyDark);
        return c.ToSprite(key);
    }

    /// <summary>風船（18×30。色の丸にひもと光。color：0 赤・1 黄・2 紫）</summary>
    public static Sprite Balloon(int color)
    {
        int i = ((color % 3) + 3) % 3;
        string key = "Msg_Balloon_" + i;
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        Color32 fill = i == 0 ? BalloonRed : i == 1 ? BalloonYellow : BalloonPurple;
        Color32 dark = Color.Lerp(fill, Color.black, 0.3f);
        var c = new TalkCanvas(18, 30);
        c.Disc(9f, 9.5f, 9f, dark);
        c.Disc(9f, 9.5f, 8f, fill);
        c.Disc(6f, 6f, 2f, new Color32(255, 255, 255, 200));
        c.Fill(8, 18, 3, 2, dark);
        for (int y = 20; y < 30; y++) c.Put(9 + ((y / 3) % 2), y, GreyDark);
        return c.ToSprite(key);
    }

    /// <summary>紙吹雪の小さい四角（3×2）</summary>
    public static Sprite Confetti => TalkArt.Make("Msg_Confetti", ConfettiCanvas());

    static TalkCanvas ConfettiCanvas()
    {
        var c = new TalkCanvas(3, 2);
        c.Fill(0, 0, 3, 2, TalkArt.White);
        return c;
    }
}
