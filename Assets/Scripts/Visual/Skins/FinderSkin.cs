using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】Finder のスキン（2026-11-18。担当4。docs/app-skill-visuals-design.md 11章）。
// ・専用の技（Spotlight・スマートフォルダ）と能力（zip圧縮）の絵は、それぞれの部品（Combat/Custom/Finder）の中に足した。
// ・ここは、当たった絵・エラーの絵・通知の題名（11-4）と、第2形態の「ウィンドウ出現」を Finder のウィンドウにする枠（11-4 の下）。
// ・Chrome・VS Code から受けついだ、ほかの枠は false（Generic に任せる。1-1 の順番の 3）
// ・エラーの絵の種類：Spotlight（虫めがねの輪）・SmartFolder_Red / SmartFolder_Blue（タグが貼られて落ちる）
public class FinderSkin : AppSkin
{
    public override SkinId Id => SkinId.Finder;
    public override string NotificationTitle => "Trash emptied";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        if (K.Resolve(frame, info.Data) == AttackFrame.Window) return GenericFrameFx.Apply<FinderFxWindow>(AttackFrame.Window, phase, info);
        return false;
    }

    // ───────── 当たった絵（11-4）：青い選択の四角が相手を囲み、白い書類が飛び散る ─────────

    public override bool Hit(HitInfo info)
    {
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
        if (info.Strength == HitStrength.Small)
        {
            K.Shards(info.Point, dir, 1, i => FinderSkinArt.Doc6, i => Color.white);
            return true;
        }
        bool fin = info.Strength == HitStrength.Finisher;
        bool heavy = info.Strength == HitStrength.Heavy || fin;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        bool big = b.size.x > 4.5f || b.size.y > 3.5f;
        Vector2 size = big ? new Vector2(1.6f, 1.3f) : (Vector2)b.size + Vector2.one * 0.3f;
        Vector2 center = big ? info.Point + dir * 0.3f : (Vector2)b.center;
        Transform root = K.Loose("Gen4_FinderHit", center);
        float life = heavy ? 0.3f : 0.2f;
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        FinderSkinArt.Selection(root, size, K.OrderHit);
        if (heavy) FinderSkinArt.Selection(root, size + Vector2.one * 0.25f, K.OrderHit, 0f); // 大は四角が2重
        float s = fin ? 1.5f : 1f;
        tw.Scale(root, Vector3.one * s * 1.2f, Vector3.one * s, 0.08f, 0f, GenericFrameTweens.Ease.OutQuad);
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, life * 0.5f, life * 0.5f);
        int n = fin ? 12 : heavy ? 6 : 3;
        K.Shards(info.Point, dir, n, i => i % 3 == 2 ? K.Disc(4) : FinderSkinArt.Doc6, i => i % 3 == 2 ? FinderSkinArt.TagColor(i) : Color.white, 40f, fin ? 1.3f : 1f);
        return true;
    }

    // ───────── エラーの絵（11-4）：書類に「？」（ファイルが見つからない）。ダウンはゴミ箱 ─────────

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 head = new Vector2(b.center.x, b.max.y + 0.6f);
        string kind = info.ErrorKind ?? "";

        // 技ごとに足す物
        if (kind.Contains("Spotlight")) MagnifierSnap(b.center);
        if (kind.Contains("SmartFolder")) TagPeel(new Vector2(b.center.x, b.center.y + b.extents.y * 0.3f), kind.Contains("Blue") ? 1 : 0);

        if (down)
        {
            Rect view = ScreenUi.ViewRect();
            Vector2 at = Vector2.Lerp(head, view.center, 0.35f);
            Trash(at);
            return true;
        }
        Transform root = K.Loose("Gen4_FinderError", head);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.42f);
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        K.Spr(pic, FinderSkinArt.MissingDoc, Vector2.zero, Color.white, K.OrderHit, 1f, "Doc");
        tw.Pop(pic, Vector3.one, 0.1f).Move(root, head, head + Vector2.up * 0.4f, 0.4f);
        foreach (var sr in pic.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, 0.12f, 0.28f);
        return true;
    }

    /// <summary>Spotlight が当たった：虫めがねの輪（赤、直径 1.2マス）がプレイヤーを囲んで「カチッ」と縮む</summary>
    static void MagnifierSnap(Vector2 at)
    {
        Transform root = K.Loose("Gen4_FinderSnap", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.3f);
        SpriteRenderer ring = K.Spr(root, K.Ring(22, 2), Vector2.zero, FinderArt.Red, K.OrderHit, 1f, "Ring");
        SpriteRenderer handle = K.Box(root, new Vector2(0.55f, -0.55f), new Vector2(0.45f, 3f * K.Dot), FinderArt.Red, K.OrderHit, "Handle");
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        tw.Scale(root, Vector3.one * 1.8f, Vector3.one, 0.1f, 0f, GenericFrameTweens.Ease.OutBack);
        tw.Alpha(ring, 1f, 0f, 0.1f, 0.18f).Alpha(handle, 1f, 0f, 0.1f, 0.18f);
    }

    /// <summary>スマートフォルダが当たった：タグの丸（10ドット）が「ペタッ」と貼られて、はがれて落ちる</summary>
    static void TagPeel(Vector2 at, int tag)
    {
        Transform root = K.Loose("Gen4_FinderTag", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.42f);
        SpriteRenderer dot = K.Spr(root, FinderArt.TagBall(10, tag), Vector2.zero, Color.white, K.OrderHit + 1, 1f, "Tag");
        tw.Scale(dot.transform, Vector3.one * 2f, Vector3.one, 0.06f, 0f, GenericFrameTweens.Ease.InQuad);
        tw.Move(dot.transform, Vector3.zero, new Vector3(0.3f, -0.9f, 0f), 0.3f, 0.12f, GenericFrameTweens.Ease.InQuad);
        tw.Rotate(dot.transform, 0f, -120f, 0.3f, 0.12f).Alpha(dot, 1f, 0f, 0.15f, 0.27f);
    }

    /// <summary>ダウン：ゴミ箱（16ドット、灰色）のふたが開いて閉じる</summary>
    static void Trash(Vector2 at)
    {
        Transform root = K.Loose("Gen4_FinderTrash", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.95f);
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        K.Spr(pic, FinderSkinArt.TrashBody, new Vector2(0f, -1f) * K.Dot, Color.white, K.OrderHit, 1f, "Body");
        Transform lid = K.Node(pic, "LidPivot", new Vector2(-8f, 7f) * K.Dot);
        K.Spr(lid, FinderSkinArt.TrashLid, new Vector2(8f, 1f) * K.Dot, Color.white, K.OrderHit + 1, 1f, "Lid");
        tw.Pop(pic, Vector3.one * 2f, 0.1f);
        // 閉じる動きを先に入れる（あとに入れた「開く」の始まりの角度 0° が、最初の見た目になる）
        tw.Rotate(lid, 40f, 0f, 0.1f, 0.5f, GenericFrameTweens.Ease.InQuad).Rotate(lid, 0f, 40f, 0.15f, 0.12f, GenericFrameTweens.Ease.OutQuad);
        foreach (var sr in pic.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, 0.15f, 0.78f);
    }
}

/// <summary>【仮】Finder のスキンの絵（FinderArt にない物）</summary>
public static class FinderSkinArt
{
    static readonly Dictionary<char, Color32> DocPal = new Dictionary<char, Color32>
    {
        { 'o', RuntimeSprites.Hex("6E6E73") },
        { 'w', RuntimeSprites.Hex("F2F2F7") },
        { 'f', RuntimeSprites.Hex("C7C7CC") },
        { 'q', RuntimeSprites.Hex("8E8E93") },
        { 'k', RuntimeSprites.Hex("3A3A3C") },
        { 'g', RuntimeSprites.Hex("AEAEB2") },
        { 'd', RuntimeSprites.Hex("7C7C80") },
    };

    public static Color TagColor(int i) => i % 3 == 0 ? FinderArt.Red : i % 3 == 1 ? FinderArt.Blue : FinderArt.Grey;

    /// <summary>白い書類（6ドット）</summary>
    public static Sprite Doc6 => K.Art("FnDoc6", new[]
    {
        "oooo..",
        "owwfo.",
        "owwwwo",
        "owwwwo",
        "owwwwo",
        "oooooo",
    }, DocPal);

    /// <summary>書類に「？」（14×16）</summary>
    public static Sprite MissingDoc => K.Art("FnMissingDoc", new[]
    {
        "ooooooooo.....",
        "owwwwwwwoo....",
        "owwwwwwwofo...",
        "owwwwwwwoffo..",
        "owwwwqqqwooooo",
        "owwwqwwwqwwwwo",
        "owwwwwwwqwwwwo",
        "owwwwwwqwwwwwo",
        "owwwwwqwwwwwwo",
        "owwwwwqwwwwwwo",
        "owwwwwwwwwwwwo",
        "owwwwwqwwwwwwo",
        "owwwwwwwwwwwwo",
        "owwwwwwwwwwwwo",
        "owwwwwwwwwwwwo",
        "oooooooooooooo",
    }, DocPal);

    /// <summary>ゴミ箱の体（16×14）</summary>
    public static Sprite TrashBody => K.Art("FnTrashBody", new[]
    {
        "................",
        "................",
        "..kkkkkkkkkkkk..",
        "..kggggggggggk..",
        "..kgdggdggdggk..",
        "...kgdgdggdgk...",
        "...kgdgdggdgk...",
        "...kgdgdggdgk...",
        "...kgdgdggdgk...",
        "...kgdgdggdgk...",
        "...kgdgdggdgk...",
        "...kggggggggk...",
        "....kkkkkkkk....",
        "................",
    }, DocPal);

    /// <summary>ゴミ箱のふた（16×3。左の端を軸に開く）</summary>
    public static Sprite TrashLid => K.Art("FnTrashLid", new[]
    {
        "......kkkk......",
        ".kkkkkkkkkkkkkk.",
        ".kggggggggggggk.",
    }, DocPal);

    /// <summary>リターンキーの印（⏎。6ドット）</summary>
    public static Sprite Return6 => K.Glyph("FnReturn6", new[]
    {
        ".....X",
        ".....X",
        "..X..X",
        ".XXXXX",
        "..X...",
        "......",
    });

    /// <summary>青い選択の四角（ふち1ドット、中は 2割）。alphaFill が 0 ならふちだけ</summary>
    public static void Selection(Transform root, Vector2 size, int order, float alphaFill = 0.2f)
    {
        if (alphaFill > 0f) K.Box(root, Vector2.zero, size, K.WithAlpha(FinderArt.Blue, alphaFill), order, "Fill");
        float t = K.Dot;
        K.Box(root, new Vector2(0f, size.y * 0.5f), new Vector2(size.x + t, t), FinderArt.Blue, order + 1, "Edge");
        K.Box(root, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x + t, t), FinderArt.Blue, order + 1, "Edge");
        K.Box(root, new Vector2(-size.x * 0.5f, 0f), new Vector2(t, size.y + t), FinderArt.Blue, order + 1, "Edge");
        K.Box(root, new Vector2(size.x * 0.5f, 0f), new Vector2(t, size.y + t), FinderArt.Blue, order + 1, "Edge");
    }

    /// <summary>紙くずの丸（6ドット）</summary>
    public static Sprite PaperBall => K.Art("FnPaperBall", new[]
    {
        ".owwo.",
        "owfwwo",
        "wwwfww",
        "wfwwww",
        "owwfwo",
        ".owwo.",
    }, DocPal);
}

// ───────── 11-4：第2形態の「ウィンドウ出現」を、Finder のウィンドウにする ─────────

/// <summary>
/// Finder のウィンドウ（Mac のウィンドウ。2026-11-19：docs/mac-look-design.md 2-4 の 36）。
/// タイトルバー・左上の信号機・まん中の題名の棒・影は Generic の窓の Window の見た目（GenericMac.PlaceWindow）。
/// 中身：左に灰色の細い帯＝サイドバーとタグの丸 3つ、右に小さい書類 2×2。
/// 出る・足場の動きは Generic の窓（2-3 と同じ）。消えるときは、赤い丸がへこんでまん中へ縮む（1-2）
/// </summary>
public class FinderFxWindow : GenericFxWindow
{
    protected override GenericPlateLook Look => GenericPlateLook.Window;
    protected override Color PanelColor => GenericMac.Light1;
    protected override bool SlideOnVanish => false;

    protected override void BuildContent(Transform body, int w, int h)
    {
        // サイドバー（灰色の細い帯）とタグの丸 3つ。タイトルバー（ふち＋7ドット＋下の線）の下から
        int top = GenericMac.TitleHOf(h) + 2;
        float sideW = Mathf.Max(8f, w * 0.26f);
        float sideX = -w * 0.5f + 1f + sideW * 0.5f;
        float bodyTop = h * 0.5f - top, bodyH = h - top - 1f;
        // 下の角のふちにかからないように、1ドット短くする
        K.BoxPx(body, sideX, bodyTop - (bodyH - 1f) * 0.5f, sideW, bodyH - 1f, RuntimeSprites.Hex("E3E3E8"), K.OrderWindow + 1, "Sidebar");
        Color[] tags = { FinderArt.Red, FinderArt.Blue, FinderArt.Grey };
        for (int i = 0; i < 3; i++)
        {
            float y = bodyTop - 5f - i * 7f;
            K.Spr(body, K.Disc(3), new Vector2(sideX - sideW * 0.5f + 4f, y) * K.Dot, tags[i], K.OrderWindow + 2, 1f, "Tag");
            K.BoxPx(body, sideX + 2f, y, sideW - 9f, 1f, K.Grey, K.OrderWindow + 2, "TagName");
        }
        // 右に小さい書類 2×2
        float areaL = -w * 0.5f + 1f + sideW, areaR = w * 0.5f - 1f;
        float cx = (areaL + areaR) * 0.5f, cy = bodyTop - bodyH * 0.5f;
        for (int ix = 0; ix < 2; ix++)
        {
            for (int iy = 0; iy < 2; iy++)
            {
                var p = new Vector2(cx + (ix - 0.5f) * 13f, cy + (0.5f - iy) * 11f);
                K.Spr(body, FinderSkinArt.Doc6, p * K.Dot, Color.white, K.OrderWindow + 2, 1.5f, "Doc");
                K.BoxPx(body, p.x, p.y - 7f, 7f, 1f, K.Grey, K.OrderWindow + 2, "DocName");
            }
        }
    }
}
