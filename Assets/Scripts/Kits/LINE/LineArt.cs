using UnityEngine;

/// <summary>
/// 【仮】LINE のキットの色と絵（2026-11-19。docs/app-kits-1-design.md 4章）。コードの中のドット絵（1マス＝18ドット）。
/// スタンプは自作の絵（黄色い顔・親指・ハート）。実在のキャラクターは使わない（0-3）
/// </summary>
public static class LineArt
{
    public static readonly Color32 Green = RuntimeSprites.Hex("06C755");
    public static readonly Color32 BubbleGreen = RuntimeSprites.Hex("8DE055");
    public static readonly Color32 BubbleEdge = RuntimeSprites.Hex("5DAE2E");
    public static readonly Color32 Sky = RuntimeSprites.Hex("8CABD9");
    public static readonly Color32 TextBar = RuntimeSprites.Hex("2F5E1A");
    public static readonly Color32 AngryFill = RuntimeSprites.Hex("FF6B5E");
    public static readonly Color32 AngryEdge = RuntimeSprites.Hex("C7261A");
    public static readonly Color32 StickerEdge = RuntimeSprites.Hex("D1D1D6");

    /// <summary>LINE のアイコン（12×12。緑の角丸に白い吹き出し）</summary>
    public static Sprite Icon
    {
        get
        {
            Sprite s = TalkArt.Cached("Line_Icon");
            if (s != null) return s;
            var c = new TalkCanvas(12, 12);
            c.Round(0, 0, 12, 12, 3, Green);
            c.Round(2, 3, 8, 5, 2, TalkArt.White);
            c.Put(3, 8, TalkArt.White);
            c.Put(3, 9, TalkArt.White);
            return c.ToSprite("Line_Icon");
        }
    }

    static readonly string[][] StampRows = { TalkArt.SmileRows, TalkArt.ThumbRows, TalkArt.HeartIconRows };

    /// <summary>スタンプ（54×36。白い切り抜きのシールに、黄色い顔・親指・ハート。index で変わる）</summary>
    public static Sprite Stamp(int index)
    {
        int i = ((index % StampRows.Length) + StampRows.Length) % StampRows.Length;
        string key = "Line_Stamp_" + i;
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(54, 36);
        c.Round(4, 0, 46, 36, 12, TalkArt.White, StickerEdge);
        string[] rows = StampRows[i];
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        c.StampScaled((54 - w * 2) / 2 - 6, (36 - rows.Length * 2) / 2, rows, TalkArt.IconPal, 2);
        // 横に小さいハートと星（にぎやかし）
        c.StampScaled(38, 6, TalkArt.HeartIconRows, TalkArt.IconPal, 1);
        c.Disc(42f, 26f, 2f, TalkArt.Yellow);
        c.Disc(36f, 30f, 1.5f, TalkArt.Yellow);
        return c.ToSprite(key);
    }

    /// <summary>メッセージの吹き出し（40×28。緑。しっぽは右下。angry：赤い怒った吹き出し）</summary>
    public static Sprite Bubble(bool angry)
    {
        string key = angry ? "Line_BubbleAngry" : "Line_Bubble";
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(40, 28);
        TalkArt.Bubble(c, 0, 0, 40, 28, angry ? AngryFill : BubbleGreen, angry ? AngryEdge : BubbleEdge, true);
        if (angry)
        {
            c.StampScaled(14, 5, TalkArt.AngryRows, TalkArt.IconPal, 1);
            // 怒りの印（右上の十字）
            c.Fill(30, 4, 5, 1, TalkArt.White);
            c.Fill(32, 2, 1, 5, TalkArt.White);
        }
        else
        {
            c.Fill(6, 7, 26, 3, TextBar);
            c.Fill(6, 13, 18, 3, TextBar);
        }
        return c.ToSprite(key);
    }

    /// <summary>吹き出しの形の白い絵（点滅の重ね用。40×28）</summary>
    public static Sprite BubbleMask
    {
        get
        {
            Sprite s = TalkArt.Cached("Line_BubbleMask");
            if (s != null) return s;
            var c = new TalkCanvas(40, 28);
            TalkArt.Bubble(c, 0, 0, 40, 28, TalkArt.White, TalkArt.White, true);
            return c.ToSprite("Line_BubbleMask");
        }
    }

    /// <summary>返信の印（緑の丸に白いチェック。16×16）</summary>
    public static Sprite Replied
    {
        get
        {
            Sprite s = TalkArt.Cached("Line_Replied");
            if (s != null) return s;
            var c = new TalkCanvas(16, 16);
            c.Disc(8f, 8f, 8f, Green);
            c.Stamp(4, 5, new[]
            {
                "........",
                ".......W",
                "W.....WW",
                "WW...WW.",
                ".WW.WW..",
                "..WWW...",
                "...W....",
            }, TalkArt.IconPal);
            return c.ToSprite("Line_Replied");
        }
    }
}
