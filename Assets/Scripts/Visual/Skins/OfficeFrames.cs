using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Office の代用ボスの技の絵（2026-11-18。docs/app-skill-visuals-design.md 4-2）。動きは Chrome・VS Code の技のまま。
/// 敵の攻撃なので、ふちと予告は赤（0-3）。どの関数も、その物の場面ごとに呼ばれ、いつも true を返す（この物の絵を受け持つ）
/// </summary>
public static class OfficeFrames
{
    // ───────── 小弾：円グラフの切れ端 ─────────

    public static bool Pie(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Pie");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) PieWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    K.Show(tag.Root, OfficeArt.Wedge(OfficeArt.Series(info.Index, info.Theme), true), Vector2.zero, 20, "Slice");
                    tag.Spin = -720f; // 1秒に2回転
                }
                break;
            default:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) tag.ExitShrink(0.08f, 1.5f, 1.5f); // 切り分けた円グラフがはじける
                }
                else if (tag.Once("end"))
                {
                    Color c = OfficeArt.Series(info.Index, info.Theme);
                    K.Debris(tag.transform.position, -K.HitDir(new HitInfo { Direction = info.Direction }), new[] { K.DiscSprite(3, c), OfficeArt.BarBit(c) },
                        4, 21, 160f, 2f, 4f, 8f, 0.2f, 0.3f, false, 0.7f); // うすく砕ける
                }
                break;
        }
        return true;
    }

    /// <summary>予告：ボスの上に円グラフが出て、発射する数に切り分けられる。最後の 0.3秒は赤いふちが点滅</summary>
    static void PieWindup(OfficeGameChatTag tag, DressInfo info)
    {
        AttackData d = info.Data;
        int n = d != null ? Mathf.Max(1, d.ProjectileCount) : Mathf.Max(1, info.Count);
        int rep = d != null ? Mathf.Max(1, d.RepeatCount) : 1;
        float dur = info.Duration > 0f ? info.Duration : 0.8f;
        var node = K.Node(tag.Root, "Pie", Vector2.zero);
        var pieF = K.Frames((n + 1) * 2, i => OfficeArt.Pie(16, n, i / 2, i % 2 == 1 ? K.EnemyEdge : OfficeArt.Grey, info.Theme));
        var sr = K.Show(node, pieF(0), Vector2.zero, 19, "Pie");
        if (rep > 1) K.Show(node, K.Text("x" + rep, Color.white, 1, OfficeArt.Ink), new Vector2(14f * K.Dot, -6f * K.Dot), 19, "Times");
        tag.Tick = t =>
        {
            float age = t.Age;
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.center.x, body.max.y + 0.75f, t.transform.position.z);
            float s = K.Pop(age, 0.1f);
            node.localScale = new Vector3(s, s, 1f);
            int k = age < 0.1f ? 0 : Mathf.Clamp(Mathf.FloorToInt((age - 0.1f) / Mathf.Max(0.05f, dur * 0.55f) * n) + 1, 1, n);
            bool red = dur - age <= 0.3f && Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0;
            sr.sprite = pieF(k * 2 + (red ? 1 : 0));
        };
    }

    // ───────── 長弾：折れ線グラフ ─────────

    public static bool LineShot(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Line");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) LineWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    K.Show(tag.Root, OfficeArt.LineGraph(true), Vector2.zero, 20, "Line");
                    K.SetAlpha(K.Show(tag.Root, OfficeArt.LineGraph(true), new Vector2(-0.5f, 0f), 21, "Trail"), 0.22f);
                    tag.FlipLeft = true;
                }
                break;
            default:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) LineWindupEnd(tag, info);
                }
                else if (tag.Once("end"))
                {
                    K.Debris(tag.transform.position, Vector2.up, new[] { K.DiscSprite(2, OfficeArt.Magenta), K.DiscSprite(2, Color.white) },
                        4, 21, 120f, 2f, 5f, 12f, 0.2f, 0.3f);
                }
                break;
        }
        return true;
    }

    /// <summary>予告：ボスの横に、丸い点が連射の数だけ並ぶ（線でつないで、小さい折れ線グラフに）</summary>
    static void LineWindup(OfficeGameChatTag tag, DressInfo info)
    {
        int rep = info.Data != null ? Mathf.Max(1, info.Data.RepeatCount) : Mathf.Max(1, info.Count);
        float dur = info.Duration > 0f ? info.Duration : 0.8f;
        float dir = info.Direction.x >= 0f ? 1f : -1f;
        var dots = K.Node(tag.Root, "Dots", Vector2.zero);
        var points = new List<Vector2>();
        for (int i = 0; i < rep; i++) points.Add(new Vector2(dir * i * 7f * K.Dot, (i % 2 == 0 ? 0f : 4f) * K.Dot));
        for (int i = 0; i < rep - 1; i++)
        {
            var seg = K.Box(dots, Vector2.zero, Vector2.one, OfficeArt.Magenta, 19, "Seg");
            K.SetSegment(seg.transform, points[i], points[i + 1], K.Dot);
        }
        for (int i = 0; i < rep; i++)
        {
            var dot = K.Show(dots, K.DiscSprite(4, Color.white, OfficeArt.Magenta), points[i], 20, "Dot" + i);
            dot.enabled = false;
        }
        tag.Tick = t =>
        {
            float age = t.Age;
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.center.x + dir * (body.extents.x + 0.6f), body.center.y + 0.3f, t.transform.position.z);
            bool blink = K.BlinkOn(dur - age);
            for (int i = 0; i < rep; i++)
            {
                var d = dots.Find("Dot" + i);
                if (d != null) d.GetComponent<SpriteRenderer>().enabled = age > i * 0.12f && blink;
            }
        };
    }

    /// <summary>撃ち始めたら、点を1発ごとに1つずつ消す（残りの数）</summary>
    static void LineWindupEnd(OfficeGameChatTag tag, DressInfo info)
    {
        int rep = info.Data != null ? Mathf.Max(1, info.Data.RepeatCount) : 1;
        float interval = info.Data != null ? info.Data.RepeatInterval : 0.3f;
        Transform dots = tag.Root != null ? tag.Root.Find("Dots") : null;
        tag.Tick = null;
        var fx = tag.DetachRoot(rep * interval + 0.15f);
        if (fx == null || dots == null) return;
        fx.Play(PopDots(fx, dots, rep, interval));
    }

    static IEnumerator PopDots(OfficeGameChatFx fx, Transform dots, int rep, float interval)
    {
        float start = fx.Now;
        while (fx != null && dots != null)
        {
            float age = fx.Now - start;
            for (int i = 0; i < rep; i++)
            {
                var d = dots.Find("Dot" + i);
                if (d != null) d.GetComponent<SpriteRenderer>().enabled = age < i * interval;
            }
            yield return null;
        }
    }

    // ───────── 床：スペルチェックの赤い波線 ─────────

    public static bool SpellFloor(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Wave");
        Vector2 size = K.SizeOf(info);
        int w = Mathf.Max(6, Mathf.RoundToInt(size.x * 18f)), h = Mathf.Max(5, Mathf.RoundToInt(size.y * 18f));
        switch (phase)
        {
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    var waveF = K.Frames(8, i => OfficeArt.SpellWave(w, h, i & 3, i >= 4));
                    var sr = K.Show(tag.Root, waveF(0), Vector2.zero, 18, "Wave");
                    float tick = info.Data != null ? Mathf.Max(0.05f, info.Data.TickInterval) : 0.5f;
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        float age = Time.time - start;
                        // 0.1秒ごとに 1ドット流れる。判定の瞬間（ヒット間隔ごと）に白く光る
                        sr.sprite = waveF((Mathf.FloorToInt(age / 0.1f) & 3) + (age % tick < 0.06f ? 4 : 0));
                    };
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    tag.Tick = null;
                    var sr = tag.Root.Find("Wave")?.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = OfficeArt.SpellWave(w, h, 0, false, true); // 緑の下線（直った）
                    K.Show(tag.Root, OfficeArt.Check(OfficeArt.Green), new Vector2((w * 0.5f + 5f) * K.Dot, 0f), 18, "Check");
                    tag.ExitShrink(0.22f, 1f, 1f);
                }
                break;
        }
        return true;
    }

    // ───────── 柱：赤字の棒グラフ ─────────

    public static bool RedBar(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.RedBar");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele")) RedBarTelegraph(tag, info, size);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    tag.Tick = null;
                    BuildRedBar(tag, size);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    Transform pivot = tag.Root.Find("body/Pivot");
                    Transform label = tag.Root.Find("body/Label");
                    tag.Tick = null;
                    var fx = tag.DetachRoot(0.16f);
                    if (fx != null) fx.Play(ShrinkDown(fx, pivot, label, 0.1f)); // 下へ縮む
                }
                break;
        }
        return true;
    }

    /// <summary>予告：床にセルの選択の枠（赤）と右下の小さい四角。今の赤丸と同じ場所・同じ秒数。最後の 0.3秒は点滅</summary>
    static void RedBarTelegraph(OfficeGameChatTag tag, DressInfo info, Vector2 size)
    {
        var layer = tag.Layer("tele");
        Vector3 up = tag.transform.up;
        Vector3 surface = info.CurrentVisual != null
            ? info.CurrentVisual.transform.position - up * 0.15f
            : tag.transform.position - up * (size.y * 0.5f);
        layer.position = surface + up * (5f * K.Dot);
        int w = Mathf.Max(8, Mathf.RoundToInt(size.x * 18f));
        K.Box(layer, Vector2.zero, new Vector2(size.x, 9f * K.Dot), K.Alpha(K.EnemyEdge, 0.22f), 16, "Wash");
        K.Show(layer, OfficeArt.CellFrame(w, 9, K.EnemyEdge), Vector2.zero, 17, "Cell");
        float dur = info.Duration > 0f ? info.Duration : 1f;
        float start = Time.time;
        tag.Tick = t =>
        {
            if (t.HasLayer("tele")) t.Layer("tele").gameObject.SetActive(K.BlinkOn(dur - (Time.time - start)));
        };
    }

    /// <summary>本体：赤い棒が床から上へ伸びる（左右は濃い赤、中は赤 6割、セルの線）。上に #REF!</summary>
    static void BuildRedBar(OfficeGameChatTag tag, Vector2 size)
    {
        var body = tag.Layer("body");
        float w = size.x, h = size.y, px = K.Dot;
        var pivot = K.Node(body, "Pivot", new Vector2(0f, -h * 0.5f));
        pivot.localScale = new Vector3(1f, 0.001f, 1f);
        Color red = K.EnemyEdge;
        K.Box(pivot, new Vector2(0f, h * 0.5f), new Vector2(w - px * 6f, h), K.Alpha(red, 0.6f), 40, "Mid");
        for (float y = 1f; y < h - 0.2f; y += 1f)
            K.Box(pivot, new Vector2(0f, y), new Vector2(w - px * 6f, px), K.Alpha(OfficeArt.DarkRed, 0.55f), 41, "CellLine");
        K.Box(pivot, new Vector2(-w * 0.5f + px * 2f, h * 0.5f), new Vector2(px * 2f, h), OfficeArt.DarkRed, 42, "L");
        K.Box(pivot, new Vector2(w * 0.5f - px * 2f, h * 0.5f), new Vector2(px * 2f, h), OfficeArt.DarkRed, 42, "R");
        K.Box(pivot, new Vector2(-w * 0.5f + px * 0.5f, h * 0.5f), new Vector2(px, h), red, 43, "EdgeL");
        K.Box(pivot, new Vector2(w * 0.5f - px * 0.5f, h * 0.5f), new Vector2(px, h), red, 43, "EdgeR");
        K.Box(pivot, new Vector2(0f, h - px * 0.5f), new Vector2(w, px), red, 43, "EdgeT");

        // #REF!（画面の上の端より下に置く）
        Rect view = K.View();
        float topLocal = body.InverseTransformPoint(new Vector3(body.position.x, view.yMax, 0f)).y;
        float labelY = Mathf.Min(h * 0.5f, topLocal) - 0.7f;
        var label = K.Node(body, "Label", new Vector2(0f, labelY));
        K.Show(label, K.Text("#REF!", Color.white, 2, OfficeArt.DarkRed), Vector2.zero, 44, "Text");
        label.gameObject.SetActive(false);

        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            pivot.localScale = new Vector3(1f, Mathf.Max(0.001f, Mathf.Clamp01(age / 0.06f)), 1f);
            if (label != null && age >= 0.06f && !label.gameObject.activeSelf) label.gameObject.SetActive(true);
            if (label != null && label.gameObject.activeSelf)
            {
                float s = K.Pop(age - 0.06f, 0.08f);
                label.localScale = new Vector3(s, s, 1f);
            }
        };
    }

    static IEnumerator ShrinkDown(OfficeGameChatFx fx, Transform pivot, Transform label, float dur)
    {
        float start = fx.Now;
        float sy = pivot != null ? pivot.localScale.y : 1f;
        while (fx != null)
        {
            float k = Mathf.Clamp01((fx.Now - start) / dur);
            if (pivot != null) pivot.localScale = new Vector3(1f, Mathf.Max(0.001f, sy * (1f - k)), 1f);
            if (label != null) K.FadeAll(label, 1f - k);
            if (k >= 1f) { fx.Kill(); yield break; }
            yield return null;
        }
    }

    // ───────── 窓：「保存しますか？」のダイアログ（2026-11-19：Mac の注意のダイアログ。docs/mac-look-design.md 21・22・24） ─────────

    public static bool SaveWindow(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Save");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    tag.KeepVisible(info.CurrentVisual); // 点線の枠はそのまま
                    // 読み込み中：Mac の進行の輪（棒 8本。敵の予告は赤。1-6）
                    Sprite[] spin = MacUi.Spinner(16, MacUi.Red);
                    var sr = K.Show(tag.Layer("tele"), spin[0], Vector2.zero, 19, "Spinner");
                    MacUi.Animate(sr, spin, MacUi.SpinnerFps);
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    BuildSave(tag, info);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("platform") && tag.Root != null)
                {
                    // 足場になった：赤いふちを、ふつうのふちの色に戻す（1-1）
                    var sr = tag.Root.Find("body/Pop/Dialog")?.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = OfficeArt.SaveDialog(OfficeArt.SavePx(K.SizeOf(info)), false);
                    var flash = tag.Root.Find("body/Pop/Flash");
                    if (flash != null) Object.Destroy(flash.gameObject);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null && tag.Has("body"))
                {
                    tag.Tick = null;
                    Transform pop = tag.Root.Find("body/Pop");
                    Vector2Int px = OfficeArt.SavePx(K.SizeOf(info));
                    var fx = tag.DetachRoot(0.3f);
                    // 閉じる：左上の赤い丸が 0.05秒へこむ → まん中へ縮んで消える（0.1秒。1-2。右上へは縮まない）
                    if (fx != null && pop != null)
                    {
                        pop.gameObject.SetActive(true); // 足場の点滅で消えていても、閉じる動きは見せる
                        MacUi.CloseAnim(pop, null, OfficeArt.SaveRedLight(px));
                    }
                }
                break;
        }
        return true;
    }

    static void BuildSave(OfficeGameChatTag tag, DressInfo info)
    {
        Vector2 size = K.SizeOf(info);
        Vector2Int px = OfficeArt.SavePx(size);
        var body = tag.Layer("body");
        var pop = K.Node(body, "Pop", Vector2.zero);
        // 判定のマス×18 のドットで作る（伸ばさない）。影・アイコン・注意の三角・題名を上に重ねる
        // 順番（元の仕様 1-6。窓は 16〜19、弾の芯は 20）：影・後ろの枠 16、地 17、アイコン・題名 18、注意の三角 19、光 20
        K.Show(pop, OfficeArt.SaveDialog(px, true), Vector2.zero, 17, "Dialog");
        OfficeArt.DressSaveDialog(pop, px, info.AppIcon, 17);
        var flash = K.Box(pop, Vector2.zero, size, K.Alpha(K.EnemyEdge, 0.45f), 20, "Flash");
        // 後ろに同じ大きさのうすい枠を2枚、右下へ 3ドットずつ（ポップアップが重なって出る感じ）
        var ghosts = new List<SpriteRenderer>();
        for (int g = 1; g <= 2; g++)
        {
            var gh = K.Show(body, OfficeArt.SaveDialog(px, true), new Vector2(3f * g * K.Dot, -3f * g * K.Dot), 16, "Ghost");
            K.SetAlpha(gh, g == 1 ? 0.4f : 0.22f);
            ghosts.Add(gh);
        }
        float active = info.Duration > 0f ? info.Duration : 0.1f;
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            float s = K.Pop(age, 0.1f);
            pop.localScale = new Vector3(s, s, 1f);
            if (flash != null) flash.enabled = age < active; // 判定のある間は、ウィンドウ全体が赤く光る
            float gk = Mathf.Clamp01(age / 0.2f);
            for (int i = 0; i < ghosts.Count; i++)
                if (ghosts[i] != null) ghosts[i].enabled = gk < 1f;
            // 足場の最後の1秒の点滅は、元の絵の enabled をまねる（アイコン・字・影もいっしょに）
            if (t.Falling != null && t.Falling.HasLanded && t.Falling.Visual != null)
            {
                bool on = t.Falling.Visual.enabled;
                if (pop.gameObject.activeSelf != on) pop.gameObject.SetActive(on);
            }
        };
    }

    // ───────── 落下物：スライド ─────────

    public static bool SlideFall(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Slide");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    tag.KeepVisible(info.CurrentVisual); // 影はそのまま
                    var warn = K.Show(tag.Layer("tele"), OfficeArt.RedTriangle, Vector2.zero, 19, "Warn");
                    tag.Tick = t =>
                    {
                        if (!t.HasLayer("tele")) return;
                        // 影の上の空（落ちてくる高さ＝画面の上の端）に、赤い「！」が点滅
                        t.Layer("tele").position = new Vector3(t.transform.position.x, K.View().yMax - 0.6f, t.transform.position.z);
                        warn.enabled = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
                    };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    var sway = K.Node(tag.Layer("body"), "Sway", Vector2.zero);
                    var sr = K.Show(sway, OfficeArt.Slide(info.Theme), Vector2.zero, 19, "Slide");
                    sr.transform.localScale = new Vector3(size.x * 18f / 54f, size.y * 18f / 36f, 1f);
                    float start = Time.time;
                    tag.Tick = t => sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time - start) * 14f) * 3f);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    // 着地：箇条書きの点と棒が飛び散る
                    Vector2 at = (Vector2)tag.transform.position;
                    K.Debris(at, Vector2.up, OfficeArt.SlideBits(info.Theme), 9, 31, 75f, 5f, 9f, 22f, 0.35f, 0.55f);
                    StageCamera.Shake(0.04f, 0.1f);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.1f, 1.1f, 0.1f, new Vector3(0f, -size.y * 0.45f, 0f));
                }
                break;
        }
        return true;
    }

    // ───────── 上の列：シートのタブ ─────────

    public static bool SheetTab(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Tab");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tab")) BuildTab(tag, info, size);
                break;
            case DressPhase.Active:
                if (tag.Once("tab")) BuildTab(tag, info, size);
                if (tag.Once("fall") && tag.Root != null)
                {
                    // 落ちる：上に白いセルの列（うすい）を引く
                    var body = tag.Layer("body");
                    float colH = 2f, w = size.x - K.Dot * 2f;
                    K.Box(body, new Vector2(0f, size.y * 0.5f + colH * 0.5f), new Vector2(w, colH), K.Alpha(Color.white, 0.16f), 18, "Column");
                    for (int k = 1; k <= 4; k++)
                        K.Box(body, new Vector2(0f, size.y * 0.5f + k * 0.5f), new Vector2(w, K.Dot), K.Alpha(OfficeArt.CellLine, 0.3f), 18, "ColLine");
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    Vector2 at = (Vector2)tag.transform.position + Vector2.up * 0.2f;
                    var fx = K.Adopted(info, "OfficeNA", at, 0.4f);
                    var n = K.Node(fx.transform, "Text", Vector2.zero);
                    K.Show(n, K.Text("#N/A", OfficeArt.Grey, 2, Color.white), Vector2.zero, 47, "NA");
                    fx.Play(K.PopRise(fx, n, 0.35f, 0.5f, 0.08f));
                    K.Debris(at, Vector2.up, new[] { OfficeArt.CellBit, K.DiscSprite(2, OfficeArt.CellLine) }, 3, 31, 70f, 2f, 4f, 10f, 0.25f, 0.35f);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    if (tag.Has("land")) tag.ExitShrink(0.08f, 1.2f, 0.2f);
                    else tag.ExitShrink(0.1f, 0.05f, 1f); // クリックで消した：横に閉じる
                }
                break;
        }
        return true;
    }

    static void BuildTab(OfficeGameChatTag tag, DressInfo info, Vector2 size)
    {
        int number = Mathf.Clamp(info.Index + 1, 1, 9);
        var body = tag.Layer("body");
        var pop = K.Node(body, "Pop", Vector2.zero);
        Sprite calm = OfficeArt.SheetTab(number, true, false), alarmed = OfficeArt.SheetTab(number, true, true);
        var sr = K.Show(pop, calm, Vector2.zero, 19, "Tab");
        sr.transform.localScale = new Vector3(size.x * 18f / 27f, size.y * 18f / 11f, 1f);
        OfficeColumnBand.Reveal(info, tag.transform.position, size);
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            float s = K.Pop(age, 0.1f);
            pop.localScale = new Vector3(s, s, 1f);
            // 落ちる直前（並べ終わってから落ちるまで）：小さくふるえて、下の線が赤くなる
            bool falling = t.Falling != null && t.Falling.IsFalling;
            float hold = info.Data != null ? info.Data.DropHoldTime : 0.5f;
            bool alarm = !falling && t.Falling != null && t.Falling.FallStartTime - Time.time < hold;
            sr.sprite = alarm ? alarmed : calm;
            float shake = alarm ? (Mathf.FloorToInt(Time.time / 0.04f) % 2 == 0 ? 1f : -1f) * K.Dot : 0f;
            pop.localPosition = new Vector3(shake, 0f, 0f);
        };
    }

    // ───────── ワープ：セルの切り取り ─────────

    /// <summary>
    /// 瞬間移動（WarpTelegraph が知らせる。Target＝現れる所、Direction＝移動量、HitSize＝体の大きさ）。
    /// 予告：選択の緑の点線（動く点線）がボスを囲む（切り取り）。行き先にも緑の点線。本体（現れ始め）：貼り付け
    /// </summary>
    public static bool CutWarp(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.Cut");
        Vector2 size = K.SizeOf(info);
        int w = Mathf.RoundToInt(size.x * 18f) + 4, h = Mathf.RoundToInt(size.y * 18f) + 4;
        var antsF = K.Frames(8, i => K.DashedRect(w, h, i, OfficeArt.Select, Color.white));
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    float vanish = info.Data != null ? Mathf.Max(0.15f, info.Data.VanishTime) : 0.5f;
                    Vector2 from = (Vector2)tag.transform.position - info.Direction;
                    var fx = K.Adopted(info, "OfficeCutFrom", from, vanish + 0.1f);
                    var src = K.Show(fx.transform, antsF(0), Vector2.zero, 26, "Ants");
                    fx.Play(March(fx, src, antsF));
                    var dest = K.Show(tag.Layer("dest"), antsF(4), Vector2.zero, 26, "Ants");
                    tag.Tick = t => { if (dest != null) dest.sprite = antsF(Mathf.FloorToInt(Time.time / 0.06f) + 4); };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("paste") && tag.HasLayer("dest"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.18f, 1.08f, 1.08f); // 貼り付けた：点線が少し広がって消える
                }
                break;
        }
        return true;
    }

    static IEnumerator March(OfficeGameChatFx fx, SpriteRenderer sr, System.Func<int, Sprite> frames)
    {
        while (fx != null && sr != null)
        {
            sr.sprite = frames(Mathf.FloorToInt(fx.Now / 0.06f));
            yield return null;
        }
    }
}

/// <summary>
/// 【仮】シートのタブの上に出す、表の列の見出し（A B C …の帯。4-2 の上の列の予告）。タブが並ぶたびに、その上の字が出る。
/// 攻撃した人に預ける（形態変化・正常化で消える）。全部落ちたら消える
/// </summary>
public class OfficeColumnBand : MonoBehaviour
{
    static readonly List<OfficeColumnBand> Live = new List<OfficeColumnBand>();

    OfficeGameChatFx _fx;
    AttackRunner _owner;
    AttackData _data;
    float _born, _end;
    int _n;
    float _spacing;
    SpriteRenderer _band;
    readonly Dictionary<int, Transform> _letters = new Dictionary<int, Transform>();
    readonly Dictionary<int, float> _shownAt = new Dictionary<int, float>();
    float _x0;

    public static void Reveal(DressInfo info, Vector2 tabPos, Vector2 size)
    {
        Live.RemoveAll(b => b == null);
        int index = Mathf.Max(0, info.Index);
        OfficeColumnBand band = null;
        foreach (var b in Live)
            if (b._owner == info.Owner && b._data == info.Data && !b._shownAt.ContainsKey(index) && Time.time < b._end) { band = b; break; }
        if (band == null) band = Create(info, tabPos, size, index);
        band.Show(index);
    }

    static OfficeColumnBand Create(DressInfo info, Vector2 tabPos, Vector2 size, int index)
    {
        AttackData d = info.Data;
        int n = Mathf.Max(1, info.Count);
        float spacing = d != null ? d.PlaceSpacing : 3f;
        float life = d != null ? (n - 1) * d.PlaceInterval + d.DropHoldTime + (n - 1) * d.DropInterval + 0.8f : 4f;
        float x0 = tabPos.x - index * spacing;
        float y = Mathf.Min(tabPos.y + size.y * 0.5f + 0.27f, K.View().yMax - 0.24f);
        var fx = K.Adopted(info, "OfficeColumnBand", new Vector2(x0, y), life + 0.3f);
        var band = fx.gameObject.AddComponent<OfficeColumnBand>();
        band._fx = fx;
        band._owner = info.Owner;
        band._data = d;
        band._born = Time.time;
        band._end = Time.time + life;
        band._n = n;
        band._spacing = spacing;
        band._x0 = x0;
        band._band = K.Box(fx.transform, Vector2.zero, new Vector2(0.001f, 0.42f), K.Alpha(K.Hex("E6E6E6"), 0.85f), 17, "Band");
        Live.Add(band);
        return band;
    }

    void Show(int index)
    {
        if (_shownAt.ContainsKey(index)) return;
        _shownAt[index] = Time.time;
        string letter = ((char)('A' + Mathf.Clamp(index, 0, 25))).ToString();
        var t = K.Node(transform, "Letter", new Vector2(index * _spacing, 0f));
        K.Show(t, K.Text(letter, K.Hex("5F6368")), Vector2.zero, 18, "Text");
        _letters[index] = t;
    }

    void Update()
    {
        try { Step(); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Office の列の見出しでエラー：{e.Message}");
            if (_fx != null) _fx.Kill();
        }
    }

    void Step()
    {
        float now = Time.time;
        int max = -1;
        foreach (var kv in _shownAt) max = Mathf.Max(max, kv.Key);
        // 帯は左から伸びる（並んだタブの右まで）
        float width = (max + 1) * _spacing;
        float k = Mathf.Clamp01((now - _born) / 0.15f);
        float wNow = Mathf.Max(0.001f, width * k);
        _band.transform.localPosition = new Vector2(-_spacing * 0.5f + wNow * 0.5f, 0f);
        _band.transform.localScale = new Vector3(wNow, 0.42f, 1f);
        foreach (var kv in _letters)
        {
            float s = K.Pop(now - _shownAt[kv.Key], 0.1f);
            if (kv.Value != null) kv.Value.localScale = new Vector3(s, s, 1f);
        }
        if (now > _end)
        {
            float f = 1f - Mathf.Clamp01((now - _end) / 0.25f);
            K.FadeAll(transform, f);
            if (f <= 0f) _fx.Kill();
        }
    }

    void OnDestroy() => Live.Remove(this);
}
