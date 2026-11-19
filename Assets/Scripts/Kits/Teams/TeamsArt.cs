using UnityEngine;

/// <summary>
/// 【仮】Microsoft Teams のキットの色と絵（2026-11-19。docs/app-kits-1-design.md 17章）。コードの中のドット絵（1マス＝18ドット）
/// </summary>
public static class TeamsArt
{
    public static readonly Color32 Purple = RuntimeSprites.Hex("6264A7");
    public static readonly Color32 PurpleDark = RuntimeSprites.Hex("464775");
    public static readonly Color32 PurpleLight = RuntimeSprites.Hex("A6A7DC");
    public static readonly Color32 ShareRed = RuntimeSprites.Hex("C4314B");
    public static readonly Color32 HandYellow = RuntimeSprites.Hex("FFAA44");
    public static readonly Color32 HandDark = RuntimeSprites.Hex("C77A1E");

    /// <summary>アイコン（12×12。紫の角丸に白い T）</summary>
    public static Sprite Icon
    {
        get
        {
            Sprite s = TalkArt.Cached("Teams_Icon");
            if (s != null) return s;
            var c = new TalkCanvas(12, 12);
            c.Round(0, 0, 12, 12, 3, Purple);
            c.Fill(3, 3, 6, 2, TalkArt.White);
            c.Fill(5, 3, 2, 7, TalkArt.White);
            return c.ToSprite("Teams_Icon");
        }
    }

    /// <summary>挙手の柱（w×h ドット。上に黄色い手、下へ腕）。柱の判定の絵に使う</summary>
    public static Sprite HandPillar(int w, int h)
    {
        string key = $"Teams_HandPillar_{w}_{h}";
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(w, h);
        int handW = 12 * 2, handH = 14 * 2;
        int armW = Mathf.Max(6, w / 2);
        int armX = (w - armW) / 2;
        // 腕（下まで）
        c.Fill(armX, handH - 4, armW, h - handH + 4, HandYellow);
        c.Fill(armX, handH - 4, 1, h - handH + 4, HandDark);
        c.Fill(armX + armW - 1, handH - 4, 1, h - handH + 4, HandDark);
        // 袖（紫の帯を下の方に）
        c.Fill(armX - 1, h - h / 3, armW + 2, h / 3, Purple);
        c.Fill(armX - 1, h - h / 3, armW + 2, 2, PurpleLight);
        // 手
        var pal = new System.Collections.Generic.Dictionary<char, Color32>(TalkArt.IconPal) { ['Y'] = HandYellow, ['D'] = HandDark };
        c.StampScaled((w - handW) / 2, 0, TalkArt.HandRows, pal, 2);
        return c.ToSprite(key);
    }

    /// <summary>挙手の柱（1.5×4.5マス）</summary>
    public static Sprite Hand(int index) => HandPillar(27, 81);

    /// <summary>能力の挙手の柱（1.5×6マス）</summary>
    public static Sprite HandTall(int index) => HandPillar(27, 108);

    /// <summary>ブレイクアウトルームの壁（12×72。紫の板に明るいふち）</summary>
    public static Sprite Wall
    {
        get
        {
            Sprite s = TalkArt.Cached("Teams_Wall");
            if (s != null) return s;
            var c = new TalkCanvas(12, 72);
            c.Fill(0, 0, 12, 72, Purple);
            c.Fill(0, 0, 1, 72, PurpleLight);
            c.Fill(11, 0, 1, 72, PurpleDark);
            c.Fill(0, 0, 12, 2, PurpleLight);
            for (int y = 10; y < 72; y += 14) c.Fill(3, y, 6, 1, PurpleDark);
            return c.ToSprite("Teams_Wall");
        }
    }
}
