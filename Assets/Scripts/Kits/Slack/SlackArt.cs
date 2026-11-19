using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Slack のキットの色と絵（2026-11-19。docs/app-kits-1-design.md 2章）。コードの中のドット絵（1マス＝18ドット）。
/// 本物のメッセージ・チャンネル名は出さない（文字は #general・@channel だけ。0-3）
/// </summary>
public static class SlackArt
{
    public static readonly Color32 Purple = RuntimeSprites.Hex("4A154B");
    public static readonly Color32 Blue = RuntimeSprites.Hex("36C5F0");
    public static readonly Color32 Green = RuntimeSprites.Hex("2EB67D");
    public static readonly Color32 Yellow = RuntimeSprites.Hex("ECB22E");
    public static readonly Color32 Red = RuntimeSprites.Hex("E01E5A");
    public static readonly Color32 Badge = RuntimeSprites.Hex("E01E5A");
    public static readonly Color32 PillFill = RuntimeSprites.Hex("E8F5FA");
    public static readonly Color32 PillEdge = RuntimeSprites.Hex("1D9BD1");
    public static readonly Color32 Paper = RuntimeSprites.Hex("FFFFFF");
    public static readonly Color32 Line = RuntimeSprites.Hex("D1D1D6");
    public static readonly Color32 TextBar = RuntimeSprites.Hex("C7C7CC");
    public static readonly Color32 NameBar = RuntimeSprites.Hex("1D1C1D");

    /// <summary>ロゴの4色（吹き出しの色を順に変えるとき）</summary>
    public static readonly Color32[] Logo4 = { Blue, Green, Yellow, Red };

    /// <summary>ロゴ（12×12。4色の棒が風車の形）</summary>
    public static Sprite Logo
    {
        get
        {
            Sprite s = TalkArt.Cached("Slack_Logo");
            if (s != null) return s;
            var c = new TalkCanvas(12, 12);
            StampLogo(c, 0, 0);
            return c.ToSprite("Slack_Logo");
        }
    }

    /// <summary>ロゴを板に描く（左上 x,y。12×12）</summary>
    public static void StampLogo(TalkCanvas c, int x, int y)
    {
        c.Round(x + 3, y + 0, 3, 7, 1, Blue);     // 上の縦（青）
        c.Round(x + 0, y + 6, 3, 3, 1, Blue);
        c.Round(x + 5, y + 3, 7, 3, 1, Green);    // 右の横（緑）
        c.Round(x + 3, y + 0, 3, 3, 1, Green);
        c.Round(x + 6, y + 5, 3, 7, 1, Yellow);   // 下の縦（黄）
        c.Round(x + 9, y + 3, 3, 3, 1, Yellow);
        c.Round(x + 0, y + 6, 7, 3, 1, Red);      // 左の横（赤）
        c.Round(x + 6, y + 9, 3, 3, 1, Red);
    }

    /// <summary>通知の板（未読。2×1.3マス＝36×24。白い板に Slack のロゴと灰色の文の棒、右上に赤いバッジ）</summary>
    public static Sprite Board
    {
        get
        {
            Sprite s = TalkArt.Cached("Slack_Board");
            if (s != null) return s;
            var c = new TalkCanvas(36, 24);
            c.Round(0, 3, 33, 21, 4, Paper, Line);
            StampLogo(c, 3, 8);
            c.Fill(17, 9, 12, 2, NameBar);
            c.Fill(17, 13, 13, 2, TextBar);
            c.Fill(17, 17, 9, 2, TextBar);
            c.Disc(30f, 6f, 6f, Badge);
            c.Disc(30f, 6f, 2f, Paper);
            return c.ToSprite("Slack_Board");
        }
    }

    /// <summary>読んだ板（灰色。既読）</summary>
    public static Sprite BoardRead
    {
        get
        {
            Sprite s = TalkArt.Cached("Slack_BoardRead");
            if (s != null) return s;
            var c = new TalkCanvas(36, 24);
            c.Round(0, 3, 33, 21, 4, RuntimeSprites.Hex("F2F2F7"), Line);
            StampLogo(c, 3, 8);
            c.Fill(17, 9, 12, 2, TextBar);
            c.Fill(17, 13, 13, 2, Line);
            c.Fill(17, 17, 9, 2, Line);
            return c.ToSprite("Slack_BoardRead");
        }
    }

    static readonly string[][] EmojiRows = { TalkArt.ThumbRows, TalkArt.HeartIconRows, TalkArt.SmileRows, TalkArt.PartyRows, TalkArt.EyesRows };

    /// <summary>絵文字の札（リアクションの雨。27×27。うすい青の札に絵文字。index で絵文字が変わる）</summary>
    public static Sprite EmojiTag(int index)
    {
        int i = ((index % EmojiRows.Length) + EmojiRows.Length) % EmojiRows.Length;
        string key = "Slack_EmojiTag_" + i;
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(27, 27);
        c.Round(0, 0, 27, 27, 7, PillFill, PillEdge);
        string[] rows = EmojiRows[i];
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        c.StampScaled((27 - w * 2) / 2, (27 - rows.Length * 2) / 2, rows, TalkArt.IconPal, 2);
        return c.ToSprite(key);
    }

    /// <summary>
    /// ボスのメッセージ（画面の上の灰色の吹き出し。w×h ドット）。左にアイコン（ボスの色の四角）、名前の棒と文の棒。
    /// 文字は出さない（本物のメッセージに見えないように）
    /// </summary>
    public static Sprite MessageBar(int w, int h)
    {
        string key = $"Slack_MessageBar_{w}_{h}";
        Sprite s = TalkArt.Cached(key);
        if (s != null) return s;
        var c = new TalkCanvas(w, h);
        c.Round(0, 0, w, h, 5, RuntimeSprites.Hex("F8F8F8"), Line);
        int a = h - 8;
        c.Round(4, 4, a, a, 3, Purple);
        StampLogo(c, 4 + (a - 12) / 2, 4 + (a - 12) / 2);
        c.Fill(a + 10, 5, 34, 3, NameBar);
        c.Fill(a + 48, 5, 20, 3, TextBar);
        int lines = Mathf.Max(1, (h - 14) / 5);
        for (int i = 0; i < lines; i++) c.Fill(a + 10, 11 + i * 5, Mathf.Max(20, w - a - 30 - i * 40), 2, TextBar);
        return c.ToSprite(key);
    }

    /// <summary>未読のバッジ（右上の数。直径 14 ドットの赤い丸）</summary>
    public static Sprite BadgeDisc => TalkArt.Disc(14);
}
