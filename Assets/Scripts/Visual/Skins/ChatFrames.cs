using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Chat のスキンの攻撃の絵（2026-11-18。docs/app-skill-visuals-design.md 7-1 と 1-7 の Chat の行）。
/// ・代用ボスの技（7-1）：通知バッジ・メッセージの吹き出し・未読の赤い線・メンションの連打・大きい吹き出し・大きいスタンプ・通知の板・退出と参加
/// ・その場で作る能力（1-7）：ピラー・連続ピラー（メンションの連打）、拡散ショット（バッジ）、トラップ弾（紙飛行機 → 未読の線を2段）、
///   ロブ弾・ノヴァ（吹き出しがふくらんではじけ、既読とハートが輪になって広がる）、ペイント（吹き出しが積み重なる）
/// プレイヤー版は「赤いふち → 白いふち」「赤い部分 → アプリの色」。どの関数も、その物の場面ごとに呼ばれ、いつも true を返す
/// </summary>
public static class ChatFrames
{
    static Color Fill(DressInfo info) => info.IsEnemy ? ChatArt.BadgeRed : ChatArt.App(info.Theme);

    // ───────── 小弾：通知バッジ ─────────

    public static bool BadgeShot(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Badge");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) BadgeWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    K.Show(tag.Root, ChatArt.Badge(BadgeText(info), Fill(info), K.Edge(info.IsEnemy)), Vector2.zero, 20, "Badge");
                    tag.Upright = true;
                }
                break;
            default:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) tag.ExitShrink(0.08f, 1.4f, 1.4f);
                }
                else if (tag.Once("end"))
                {
                    tag.ExitShrink(0.08f, 1.6f, 1.6f); // はじけて消える
                    K.Debris(tag.transform.position, Vector2.up, new[] { K.DiscSprite(3, Fill(info)), K.DiscSprite(2, Color.white) }, 3, 21, 160f, 2f, 4f, 6f, 0.2f, 0.3f);
                }
                break;
        }
        return true;
    }

    /// <summary>バッジの数字：弾ごとに 1 2 3 …。第2形態（連射が2回以上）の最後の1発は 99+</summary>
    static string BadgeText(DressInfo info)
    {
        int n = Mathf.Max(0, info.Index) + 1;
        bool repeated = info.Data != null && info.Data.RepeatCount > 1;
        if (info.IsEnemy && repeated && info.Count > 0 && info.Index == info.Count - 1) return "99+";
        return n > 99 ? "99+" : n.ToString();
    }

    /// <summary>予告：ボスのウィンドウの右上にバッジが出て、数字が 1 → 撃つ数へ増える</summary>
    static void BadgeWindup(OfficeGameChatTag tag, DressInfo info)
    {
        int total = Mathf.Max(1, info.Count);
        float dur = info.Duration > 0f ? info.Duration : 0.8f;
        var node = K.Node(tag.Root, "Badge", Vector2.zero);
        var sr = K.Show(node, ChatArt.Badge("1", ChatArt.BadgeRed, K.EnemyEdge), Vector2.zero, 19, "Badge");
        int shown = 1;
        float changed = Time.time;
        tag.Tick = t =>
        {
            float age = t.Age;
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.max.x - 0.15f, body.max.y - 0.05f, t.transform.position.z);
            int n = Mathf.Clamp(1 + Mathf.FloorToInt(age / Mathf.Max(0.04f, dur * 0.6f / total)), 1, total);
            if (n != shown)
            {
                shown = n;
                changed = Time.time;
                sr.sprite = ChatArt.Badge(n.ToString(), ChatArt.BadgeRed, K.EnemyEdge);
            }
            float s = age < 0.1f ? K.Pop(age, 0.1f) : 1f + 0.25f * Mathf.Max(0f, 1f - (Time.time - changed) / 0.08f); // 数字が増えるたびに跳ねる
            node.localScale = new Vector3(s, s, 1f);
            sr.enabled = K.BlinkOn(dur - age);
        };
    }

    // ───────── 長弾：メッセージの吹き出し ─────────

    public static bool MessageShot(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Message");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) TypingWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    K.Show(tag.Root, ChatArt.Message(ChatArt.App(info.Theme), info.IsEnemy), Vector2.zero, 20, "Message");
                    tag.FlipLeft = true;
                }
                break;
            default:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) tag.ExitShrink(0.1f, 0.2f, 0.2f);
                }
                else if (tag.Once("end"))
                {
                    K.Debris(tag.transform.position, Vector2.up, ChatArt.Bits(ChatArt.App(info.Theme)), 3, 21, 120f, 2f, 5f, 10f, 0.25f, 0.35f);
                }
                break;
        }
        return true;
    }

    /// <summary>予告：ボスの横に「入力中…」（灰色の吹き出しに、順に跳ねる点 3つ）</summary>
    static void TypingWindup(OfficeGameChatTag tag, DressInfo info)
    {
        float dir = info.Direction.x >= 0f ? 1f : -1f;
        var node = K.Node(tag.Root, "Typing", Vector2.zero);
        K.Show(node, ChatArt.TypingBubble(), Vector2.zero, 19, "Bubble");
        var dots = TypingDots(node, Vector2.up * K.Dot, 20);
        tag.Tick = t =>
        {
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.center.x + dir * (body.extents.x + 0.65f), body.center.y + 0.6f, t.transform.position.z);
            float s = K.Pop(t.Age, 0.1f);
            node.localScale = new Vector3(s, s, 1f);
            BounceDots(dots, t.Age);
        };
    }

    /// <summary>「入力中…」の点 3つ（子の絵）</summary>
    public static List<Transform> TypingDots(Transform parent, Vector2 center, int order, int size = 2, Color? color = null)
    {
        var list = new List<Transform>();
        for (int i = 0; i < 3; i++)
            list.Add(K.Show(parent, K.DiscSprite(size + 1, color ?? ChatArt.GreyDark), center + new Vector2((i - 1) * (size + 2) * K.Dot, 0f), order, "Dot").transform);
        return list;
    }

    /// <summary>点を順に跳ねさせる</summary>
    public static void BounceDots(List<Transform> dots, float age)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;
            float ph = Mathf.Repeat(age * 2.5f - i * 0.18f, 1f);
            Vector3 p = dots[i].localPosition;
            p.y = (ph < 0.3f ? Mathf.Sin(ph / 0.3f * Mathf.PI) * 2f : 0f) * K.Dot + K.Dot;
            dots[i].localPosition = p;
        }
    }

    // ───────── 床：未読の赤い線（プレイヤーのトラップ弾の床は、アプリの色で2段） ─────────

    public static bool UnreadFloor(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Unread");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    int rows = info.IsEnemy ? 1 : 2;
                    Color c = Fill(info);
                    Sprite tagSprite = info.IsEnemy ? ChatArt.NewTag(ChatArt.BadgeRed) : ChatArt.NewTag(c, Color.white);
                    float tagW = tagSprite.bounds.size.x;
                    var lines = new List<SpriteRenderer>();
                    for (int r = 0; r < rows; r++)
                    {
                        float y = rows == 1 ? -size.y * 0.1f : (r == 0 ? -size.y * 0.25f : size.y * 0.15f);
                        // 線は右の端を支点にする（消えるとき、左から消える）
                        var pivot = K.Node(tag.Root, "Row", new Vector2(size.x * 0.5f, y));
                        lines.Add(K.Box(pivot, new Vector2(-size.x * 0.5f, 0f), new Vector2(size.x, K.Dot), c, 18, "Line"));
                        K.Show(pivot, tagSprite, new Vector2(-tagW * 0.5f, 0f), 18, "New"); // 線の右の端（判定の高さの中）
                    }
                    float tick = info.Data != null ? Mathf.Max(0.05f, info.Data.TickInterval) : 0.5f;
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        bool flash = (Time.time - start) % tick < 0.06f; // 判定の瞬間に線が光る
                        foreach (var l in lines) if (l != null) l.color = flash ? Color.white : c;
                    };
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    tag.Tick = null;
                    var rows = new List<Transform>();
                    foreach (Transform ch in tag.Root) rows.Add(ch);
                    var fx = tag.DetachRoot(0.25f);
                    if (fx != null) fx.Play(WipeRows(fx, rows, 0.2f)); // 線が左から消える（読んだ）
                }
                break;
        }
        return true;
    }

    static IEnumerator WipeRows(OfficeGameChatFx fx, List<Transform> rows, float dur)
    {
        float start = fx.Now;
        while (fx != null)
        {
            float k = Mathf.Clamp01((fx.Now - start) / dur);
            foreach (var r in rows)
            {
                if (r == null) continue;
                r.localScale = new Vector3(Mathf.Max(0.001f, 1f - k), 1f, 1f);
                K.FadeAll(r, 1f - k * k);
            }
            if (k >= 1f) { fx.Kill(); yield break; }
            yield return null;
        }
    }

    // ───────── 柱：メンションの連打 ─────────

    public static bool MentionPillar(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Mention");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    var layer = tag.Layer("tele");
                    Vector3 up = tag.transform.up;
                    Vector3 surface = info.CurrentVisual != null
                        ? info.CurrentVisual.transform.position - up * 0.15f
                        : tag.transform.position - up * (size.y * 0.5f);
                    layer.position = surface + up * (6f * K.Dot);
                    K.Box(layer, Vector2.zero, new Vector2(size.x, 11f * K.Dot), K.Alpha(info.IsEnemy ? K.EnemyEdge : ChatArt.MentionYellow, 0.2f), 16, "Wash");
                    var mark = K.Show(layer, ChatArt.Mention(info.IsEnemy), Vector2.zero, 17, "At");
                    float dur = info.Duration > 0f ? info.Duration : 1f;
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        if (!t.HasLayer("tele")) return;
                        float age = Time.time - start;
                        mark.enabled = K.BlinkOn(dur - age);
                        float s = K.Pop(age, 0.1f);
                        mark.transform.localScale = new Vector3(s, s, 1f);
                    };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    tag.Tick = null;
                    BuildStack(tag, info, size);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    tag.Tick = null;
                    Transform stack = tag.Root.Find("body");
                    var fx = tag.DetachRoot(0.2f);
                    if (fx != null && stack != null) fx.Play(PopFromTop(fx, stack, 0.14f));
                }
                break;
        }
        return true;
    }

    /// <summary>吹き出しが下から上へ、すき間なく積み上がる（左右交互。アプリの色と灰色）。左右にふち（敵＝赤、プレイヤー＝白）</summary>
    static void BuildStack(OfficeGameChatTag tag, DressInfo info, Vector2 size)
    {
        var body = tag.Layer("body");
        int pw = Mathf.Max(10, Mathf.RoundToInt(size.x * 18f));
        const int ph = 12;
        float bh = ph * K.Dot;
        int count = Mathf.Max(1, Mathf.CeilToInt(size.y / bh));
        // 連続ピラーは、1本ごとに色の明るさを1段変える（1-7）
        Color app = ChatArt.App(info.Theme);
        if (!info.IsEnemy && info.Index > 0) app = K.Lighten(app, Mathf.Min(0.5f, 0.15f * info.Index));
        var bubbles = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            bool mine = i % 2 == 0;
            Color c = mine ? app : ChatArt.Grey;
            Sprite s = ChatArt.Bubble(pw, ph, c, mine, true, mine ? K.Darken(app, 0.35f) : ChatArt.GreyDark);
            var b = K.Show(body, s, new Vector2(0f, -size.y * 0.5f + (i + 0.5f) * bh), 41, "Bubble").transform;
            b.gameObject.SetActive(false);
            bubbles.Add(b);
        }
        Color edge = K.Edge(info.IsEnemy);
        var edgeL = K.Box(body, new Vector2(-size.x * 0.5f + K.Dot * 0.5f, 0f), new Vector2(K.Dot, size.y), edge, 43, "EdgeL");
        var edgeR = K.Box(body, new Vector2(size.x * 0.5f - K.Dot * 0.5f, 0f), new Vector2(K.Dot, size.y), edge, 43, "EdgeR");
        var wash = K.Box(body, Vector2.zero, size, K.Alpha(edge, 0.12f), 40, "Wash");
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            // 0.08秒で全部積み上がる（判定は始まっているので、すぐに見せる）
            int shown = Mathf.Clamp(Mathf.CeilToInt(age / 0.08f * count) + 1, 1, count);
            for (int i = 0; i < count; i++)
            {
                if (bubbles[i] == null) continue;
                bool on = i < shown;
                if (on && !bubbles[i].gameObject.activeSelf) bubbles[i].gameObject.SetActive(true);
                if (on)
                {
                    float s = K.Pop(age - i * 0.08f / count, 0.06f);
                    bubbles[i].localScale = new Vector3(1f, s, 1f);
                }
            }
            float k = Mathf.Clamp01(age / 0.08f);
            foreach (var e in new[] { edgeL, edgeR })
            {
                if (e == null) continue;
                e.transform.localScale = new Vector3(K.Dot, Mathf.Max(0.001f, size.y * k), 1f);
                e.transform.localPosition = new Vector3(e.transform.localPosition.x, -size.y * 0.5f + size.y * k * 0.5f, 0f);
            }
            if (wash != null) wash.enabled = true;
        };
    }

    /// <summary>上から、はじけて消える</summary>
    static IEnumerator PopFromTop(OfficeGameChatFx fx, Transform stack, float dur)
    {
        var items = new List<Transform>();
        foreach (Transform ch in stack)
            if (ch.name == "Bubble" || ch.name == "Cell") items.Add(ch); // ふちの線は最後まで残して、すかすだけ
        items.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));
        float start = fx.Now;
        while (fx != null)
        {
            float k = Mathf.Clamp01((fx.Now - start) / dur);
            int gone = Mathf.FloorToInt(k * items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                if (i < gone) items[i].gameObject.SetActive(false);
                else if (i == gone) items[i].localScale = new Vector3(items[i].localScale.x * 1.02f, items[i].localScale.y * 1.02f, 1f);
            }
            K.FadeAll(stack, 1f - k * 0.3f);
            if (k >= 1f) { fx.Kill(); yield break; }
            yield return null;
        }
    }

    // ───────── 窓：大きい吹き出し ─────────

    public static bool BigBubble(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Big");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    tag.KeepVisible(info.CurrentVisual); // 点線の枠はそのまま
                    var dots = TypingDots(tag.Layer("tele"), Vector2.zero, 19, 3, K.EnemyEdge); // 予告は赤
                    tag.Tick = t => BounceDots(dots, t.Age);
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    GameFrames.WindowBody(tag, info, ChatArt.BigBubble(true, false), 54, 36);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("platform")) GameFrames.SwapWindow(tag, ChatArt.BigBubble(false, true));
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Has("body"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.12f, 0.15f, 0.15f); // しぼんで消える
                }
                break;
        }
        return true;
    }

    // ───────── 落下物：大きいスタンプ ─────────

    public static bool StampFall(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Stamp");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele")) GameFrames.SkyWarning(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    var sway = K.Node(tag.Layer("body"), "Sway", Vector2.zero);
                    var sr = K.Show(sway, ChatArt.Stamp(info.Index, ChatArt.App(info.Theme)), Vector2.zero, 19, "Stamp");
                    sr.transform.localScale = new Vector3(size.x * 18f / 54f, size.y * 18f / 36f, 1f);
                    float start = Time.time;
                    tag.Tick = t => sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time - start) * 16f) * 4f);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    // 着地：小さいリアクション（ハート・親指）が 6個飛ぶ
                    K.Debris(tag.transform.position, Vector2.up, ChatArt.Reactions, 6, 31, 75f, 4f, 8f, 14f, 0.45f, 0.65f);
                    StageCamera.Shake(0.04f, 0.1f);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.1f, 1.15f, 0.1f, new Vector3(0f, -size.y * 0.45f, 0f));
                }
                break;
        }
        return true;
    }

    // ───────── 上の列：通知の板 ─────────

    public static bool NotifyRow(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Notify");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
            case DressPhase.Active:
                if (tag.Once("panel"))
                {
                    var slide = K.Node(tag.Layer("body"), "Slide", Vector2.zero);
                    // Mac の小さい通知（角の丸い明るい板・左にアプリのアイコン・題名は棒・右上に時刻の棒）。
                    // 判定のマス×18 のドットで作る（伸ばさない）。敵は赤いふち、でなければ影だけ（2026-11-19 docs/mac-look-design.md 25・1-4）
                    Vector2 px = new Vector2(Mathf.Round(size.x * 18f), Mathf.Round(size.y * 18f));
                    MacUi.PlaceBanner(slide, Vector2.zero, px, info.AppIcon, ChatArt.NotifyTitle, info.IsEnemy ? MacUi.Red : (Color?)null, 19);
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        // 右からすべり込む（0.15秒）
                        float k = K.EaseOut((Time.time - start) / 0.15f);
                        slide.localPosition = new Vector3((1f - k) * 1.5f, 0f, 0f);
                        K.FadeAll(slide, k);
                    };
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    // 着地で、バッジの 1
                    Vector2 at = (Vector2)tag.transform.position + new Vector2(size.x * 0.4f, size.y * 0.5f + 0.1f);
                    var fx = K.Adopted(info, "ChatBadge1", at, 0.45f);
                    var n = K.Node(fx.transform, "Badge", Vector2.zero);
                    K.Show(n, ChatArt.Badge("1", ChatArt.BadgeRed, K.EnemyEdge), Vector2.zero, 47, "Badge");
                    fx.Play(K.PopRise(fx, n, 0.4f, 0.4f, 0.1f));
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    if (tag.Has("land")) tag.ExitShrink(0.08f, 1.2f, 0.2f);
                    else tag.ExitShrink(0.12f, 1f, 1f, new Vector3(1.2f, 0f, 0f)); // クリックで消した：右へ流れる（通知を払った）
                }
                break;
        }
        return true;
    }

    // ───────── ワープ：退出と参加 ─────────

    public static bool StatusWarp(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Warp");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    // 元の所：頭の上に灰色の丸（オフライン）
                    float vanish = info.Data != null ? Mathf.Max(0.15f, info.Data.VanishTime) : 0.5f;
                    Vector2 from = (Vector2)tag.transform.position - info.Direction;
                    var fx = K.Adopted(info, "ChatOffline", from + Vector2.up * (size.y * 0.5f + 0.45f), vanish + 0.1f);
                    var off = K.Node(fx.transform, "Dot", Vector2.zero);
                    K.Show(off, ChatArt.Status(false), Vector2.zero, 26, "Dot");
                    fx.Play(K.PopRise(fx, off, vanish + 0.1f, 0.1f, 0.08f));
                    // 行き先：緑の丸（オンライン）が点滅
                    var dot = K.Show(tag.Layer("dest"), ChatArt.Status(true), new Vector2(0f, size.y * 0.5f + 0.45f), 26, "Online");
                    tag.Tick = t => { if (dot != null) dot.enabled = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0; };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("appear") && tag.HasLayer("dest"))
                {
                    tag.Tick = null;
                    var dot = tag.Root.Find("dest/Online")?.GetComponent<SpriteRenderer>();
                    if (dot != null) dot.enabled = true;
                    tag.ExitShrink(0.25f, 1.5f, 1.5f); // 現れた：丸がついて、ふくらんで消える
                }
                break;
        }
        return true;
    }

    // ───────── その場で作る能力：紙飛行機（投げ物） ─────────

    public static bool Plane(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Plane");
        Color app = ChatArt.App(info.Theme);
        if (phase == DressPhase.Active && tag.Once("body"))
        {
            K.Show(tag.Root, ChatArt.Plane(app), Vector2.zero, 20, "Plane");
            tag.FlipLeft = true; // 進む向きに回る（左へ飛ぶときも、上下さかさまにしない）
            float last = Time.time;
            tag.Tick = t =>
            {
                if (Time.time - last < 0.06f) return;
                last = Time.time;
                var fx = K.Fx("ChatTrail", t.transform.position, 0.25f);
                var sr = K.Show(fx.transform, K.DiscSprite(2, app), Vector2.zero, 21, "Dot");
                fx.Play(K.ShrinkOut(fx, sr.transform, 0.22f, 0.3f, 0.3f));
            };
        }
        else if ((phase == DressPhase.Landed || phase == DressPhase.Vanish) && tag.Once("end"))
        {
            tag.Tick = null;
            K.Debris(tag.transform.position, Vector2.up, new[] { K.DiscSprite(2, Color.white), K.DiscSprite(2, app) }, 4, 21, 120f, 2f, 4f, 8f, 0.2f, 0.3f);
        }
        return true;
    }

    // ───────── その場で作る能力：吹き出しがはじける（円） ─────────

    public static bool BubblePop(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Pop");
        if (phase == DressPhase.Active && tag.Once("pop"))
        {
            var fx = K.Adopted(info, "ChatPop", tag.transform.position, 0.55f);
            fx.Play(PopRoutine(fx, K.SizeOf(info).x, ChatArt.App(info.Theme)));
        }
        return true;
    }

    /// <summary>大きい吹き出し（アプリの色）が 0 → 100% にふくらんで、はじける。中から既読のチェックとハートが輪になって広がる（いちばん外＝判定の円）</summary>
    static IEnumerator PopRoutine(OfficeGameChatFx fx, float diameter, Color app)
    {
        int D = Mathf.Max(12, Mathf.RoundToInt(diameter * 18f / 2f) * 2);
        var bubble = K.Show(fx.transform, ChatArt.RoundBubble(D, app), Vector2.zero, 45, "Bubble");
        bubble.transform.localScale = Vector3.zero;
        var ring = K.Show(fx.transform, K.RingSprite(D, 2, Color.white), Vector2.zero, 46, "Ring");
        ring.enabled = false;
        const int n = 10;
        var items = new List<Transform>();
        for (int i = 0; i < n; i++)
        {
            Sprite s = i % 2 == 0 ? ChatArt.Read(app) : ChatArt.Heart;
            var it = K.Show(fx.transform, s, Vector2.zero, 47, "Item").transform;
            it.gameObject.SetActive(false);
            items.Add(it);
        }
        float start = fx.Now;
        const float inflate = 0.12f;
        while (fx != null)
        {
            float age = fx.Now - start;
            if (age < inflate)
            {
                float s = K.EaseOut(age / inflate) * 1.04f;
                bubble.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                // はじける
                bubble.enabled = false;
                float k = Mathf.Clamp01((age - inflate) / 0.15f);
                ring.enabled = age - inflate < 0.08f;
                float r = diameter * 0.5f * Mathf.Lerp(0.3f, 0.92f, K.EaseOut(k));
                for (int i = 0; i < n; i++)
                {
                    if (items[i] == null) continue;
                    items[i].gameObject.SetActive(true);
                    float a = (i * 360f / n + 90f) * Mathf.Deg2Rad;
                    items[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                    float fade = 1f - Mathf.Clamp01((age - inflate - 0.15f) / 0.2f);
                    K.FadeAll(items[i], fade);
                }
            }
            yield return null;
        }
    }

    // ───────── その場で作る能力：吹き出しが積み重なる（塗り） ─────────

    public static bool BubblePaint(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Chat.Paint");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Active:
                if (tag.Once("body")) BuildPaint(tag, info, size);
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    tag.Tick = null;
                    Transform grid = tag.Root.Find("body");
                    var fx = tag.DetachRoot(0.22f);
                    if (fx != null && grid != null) fx.Play(PopFromTop(fx, grid, 0.18f));
                }
                break;
        }
        return true;
    }

    /// <summary>吹き出しが下から積み重なって、範囲をうめる（1個 20×10ドット、左右交互。0.15秒）。いちばん上の列に「入力中…」の点</summary>
    static void BuildPaint(OfficeGameChatTag tag, DressInfo info, Vector2 size)
    {
        var body = tag.Layer("body");
        Color app = ChatArt.App(info.Theme);
        int cols = Mathf.Max(1, Mathf.FloorToInt(size.x * 18f / 21f));
        int rows = Mathf.Max(1, Mathf.FloorToInt(size.y * 18f / 11f));
        float cw = size.x / cols, ch = size.y / rows;
        var cells = new List<Transform>();
        var dots = new List<Transform>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool mine = (r + c) % 2 == 0;
                bool top = r == rows - 1;
                Color fill = mine ? app : ChatArt.Grey;
                Sprite s = ChatArt.Bubble(20, 10, fill, mine, false, mine ? K.Darken(app, 0.35f) : ChatArt.GreyDark, top);
                Vector2 p = new Vector2(-size.x * 0.5f + (c + 0.5f) * cw, -size.y * 0.5f + (r + 0.5f) * ch);
                var cell = K.Node(body, "Cell", p);
                K.Show(cell, s, Vector2.zero, 41, "Bubble");
                if (top) dots.AddRange(TypingDots(cell, Vector2.zero, 42));
                cell.gameObject.SetActive(false);
                cells.Add(cell);
            }
        var flash = K.Box(tag.Root, Vector2.zero, size, K.Alpha(Color.white, 0.35f), 43, "Flash");
        flash.enabled = false;
        float tick = info.Data != null ? Mathf.Max(0.05f, info.Data.TickInterval) : 0.5f;
        float life = info.Duration > 0f ? info.Duration : 2f;
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            int shown = Mathf.Clamp(Mathf.CeilToInt(age / 0.15f * cells.Count), 1, cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == null) continue;
                if (i < shown && !cells[i].gameObject.activeSelf) cells[i].gameObject.SetActive(true);
                if (i < shown)
                {
                    float s = K.Pop(age - i * 0.15f / cells.Count, 0.08f);
                    cells[i].localScale = new Vector3(s, s, 1f);
                }
            }
            BounceDots(dots, age);
            if (flash != null) flash.enabled = age > 0.15f && age % tick < 0.06f; // 判定の瞬間に光る
            if (life - age < 0.4f) K.FadeAll(body, Mathf.Clamp01((life - age) / 0.4f) * 0.6f + 0.4f);
        };
    }
}
