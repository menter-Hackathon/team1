using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メール（Apple の Mail）のキットの絵（2026-11-19。docs/app-kits-1-design.md 15章）。コードの中のドット絵。1マス＝18ドット。
/// 色：青 #0A84FF・白・封筒の白 #F5F5F7・迷惑メールの紫 #BF5AF2。本物のメールの中身・宛先は出さない
/// </summary>
public static class MailArt
{
    public static readonly Color Blue = RuntimeSprites.Hex("0A84FF");
    public static readonly Color Paper = RuntimeSprites.Hex("F5F5F7");
    public static readonly Color Purple = RuntimeSprites.Hex("BF5AF2");

    static readonly Color32 W = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    static readonly Color32 P = new Color32(0xF5, 0xF5, 0xF7, 0xFF);
    static readonly Color32 S = new Color32(0xC7, 0xC7, 0xCC, 0xFF); // 紙の影
    static readonly Color32 G = new Color32(0x8E, 0x8E, 0x93, 0xFF);
    static readonly Color32 B = new Color32(0x0A, 0x84, 0xFF, 0xFF);
    static readonly Color32 V = new Color32(0xBF, 0x5A, 0xF2, 0xFF);
    static readonly Color32 K = new Color32(0x1C, 0x1C, 0x1E, 0xFF);

    /// <summary>紙飛行機（白。18×12。右へ飛ぶ向き）</summary>
    public static Sprite Plane => RuntimeSprites.FromAscii("Mail_Plane", new[]
    {
        "WW................",
        "WWWWW.............",
        ".WWWWWWWW.........",
        ".WWWWWWWWWWWW.....",
        "..WWWWWWWWWWWWWWW.",
        "..WWWWWWWWWWWWWWWW",
        "..SSSSSSSSSSSWWWW.",
        ".SSSSSSSSSWWWW....",
        ".SSSSSWWWWW.......",
        "SSSWWWW...........",
        "SWW...............",
        "W.................",
    }, new Dictionary<char, Color32> { { 'W', W }, { 'S', S } });

    /// <summary>封筒（ふつう：白に青い帯。16×11）</summary>
    public static Sprite Envelope => RuntimeSprites.FromAscii("Mail_Envelope", new[]
    {
        "GGGGGGGGGGGGGGGG",
        "GPSPPPPPPPPPPSPG",
        "GPPSSPPPPPPSSPPG",
        "GPPPPSSPPSSPPPPG",
        "GPPPPPPSSPPPPPPG",
        "GPPPPPPPPPPPPPPG",
        "GBBBBBBBBBBBBBBG",
        "GPPPPPPPPPPPPPPG",
        "GPPWWWWWWPPPPPPG",
        "GPPPPPPPPPPPPPPG",
        "GGGGGGGGGGGGGGGG",
    }, new Dictionary<char, Color32> { { 'G', G }, { 'P', P }, { 'S', S }, { 'B', B }, { 'W', S } });

    /// <summary>迷惑メールの封筒（紫の「！」・文字化けの帯。16×11）</summary>
    public static Sprite SpamEnvelope => RuntimeSprites.FromAscii("Mail_Spam", new[]
    {
        "GGGGGGGGGGGGGGGG",
        "GPSPPPPPPPPPPSPG",
        "GPPSSPPPPPPSSPPG",
        "GPPPPSSPPSSPPPPG",
        "GPPPPPPSSPPPPVPG",
        "GPPPPPPPPPPPVVVG",
        "GVPVVPVPPVVPVVVG",
        "GPVPPVPVVPPPVVVG",
        "GPPKPKKPKPPPPPPG",
        "GPPPPPPPPPPPPVPG",
        "GGGGGGGGGGGGGGGG",
    }, new Dictionary<char, Color32> { { 'G', G }, { 'P', P }, { 'S', S }, { 'V', V }, { 'K', K } });

    /// <summary>ゴミ箱（灰色。10×12）</summary>
    public static Sprite Trash => RuntimeSprites.FromAscii("Mail_Trash", new[]
    {
        "...GGGG...",
        "GGGGGGGGGG",
        "..........",
        ".GGGGGGGG.",
        ".GPGPGPGG.",
        ".GPGPGPGG.",
        ".GPGPGPGG.",
        ".GPGPGPGG.",
        ".GPGPGPGG.",
        ".GPGPGPGG.",
        ".GGGGGGGG.",
        "..GGGGGG..",
    }, new Dictionary<char, Color32> { { 'G', G }, { 'P', P } });

    /// <summary>能力のアイコン（青い地に白い紙飛行機。16×16）</summary>
    public static Sprite SkillIcon => RuntimeSprites.FromAscii("Mail_SkillIcon", new[]
    {
        "..BBBBBBBBBBBB..",
        ".BBBBBBBBBBBBBB.",
        "BBBBBBBBBBBBWWBB",
        "BBBBBBBBBBWWWWBB",
        "BBBBBBBBWWWWWWBB",
        "BBBBBBWWWWWWWBBB",
        "BBBBWWWWWWWWWBBB",
        "BBWWWWWWWWWWWBBB",
        "BBBBWWWWWWWWBBBB",
        "BBBBBBWWWWWWBBBB",
        "BBBBBBBWWBWWBBBB",
        "BBBBBBBWBBBWBBBB",
        "BBBBBBBWBBBBWBBB",
        "BBBBBBBBBBBBBBBB",
        ".BBBBBBBBBBBBBB.",
        "..BBBBBBBBBBBB..",
    }, new Dictionary<char, Color32> { { 'B', B }, { 'W', W } });

    /// <summary>子の絵を1枚出す（大きさはマス）</summary>
    public static SpriteRenderer Put(Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order, string name = "Art")
    {
        SpriteRenderer sr = AttackRunner.AddVisual(parent, sprite, color, size, order, name);
        sr.transform.localPosition = localPos;
        return sr;
    }

    /// <summary>
    /// 進む向きに回した絵は、左へ向くと逆さになる。左へ向くときは絵を上下に返して、左右の反転だけにする
    /// </summary>
    public static void Upright(SpriteRenderer sr, Vector2 dir)
    {
        if (sr != null) sr.flipY = dir.x < 0f;
    }

    /// <summary>弾の絵（子の Visual）を、向きに合わせて逆さにしない</summary>
    public static void Upright(GameObject go, Vector2 dir)
    {
        if (go != null) Upright(KitDress.FindVisual(go.transform), dir);
    }

    /// <summary>「Re:」の字を、物の上に付ける（黒いふちつき）</summary>
    public static void AddRe(Transform parent, Vector2 localPos)
    {
        var holder = new GameObject("Re");
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = localPos;
        holder.AddComponent<MailUpright>();
        FxKit.AddText(holder.transform, "Re:", Vector2.zero, 0.4f, Color.white, 30);
    }
}

/// <summary>回る物（弾）の子でも、字をまっすぐに保つ。位置は親の上（世界の上）</summary>
public class MailUpright : MonoBehaviour
{
    Vector3 _offset;

    void Awake() => _offset = transform.localPosition;

    void LateUpdate()
    {
        if (transform.parent == null) return;
        transform.rotation = Quaternion.identity;
        transform.position = transform.parent.position + _offset;
    }
}
