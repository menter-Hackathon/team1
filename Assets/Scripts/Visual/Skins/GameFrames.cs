using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Game のスキンの攻撃の絵（2026-11-18。docs/app-skill-visuals-design.md 6-1・6-2）。
/// ・能力：ボム（投げ物＋円。6-1）。Game のプレイヤーの投げ物・円は、どれもボムで描く（1-7 の終わり）
/// ・代用ボスの技（6-2）：コントローラーのボタン・ミサイル・溶岩・照準 → 光の柱・実績の通知・ライブラリのタイル・ダウンロードの列・ロード中
/// どの関数も、その物の場面ごとに呼ばれ、いつも true を返す（この物の絵を受け持つ）
/// </summary>
public static class GameFrames
{
    // ───────── 能力：ボム（投げ物） ─────────

    public static bool Bomb(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Bomb");
        if (phase == DressPhase.Active && tag.Once("body"))
        {
            // 投げた瞬間：手元にボタンの印（丸に B）が 0.15秒
            var bfx = K.Adopted(info, "GameBButton", (Vector2)tag.transform.position + Vector2.up * 0.25f, 0.2f);
            var bn = K.Node(bfx.transform, "B", Vector2.zero);
            K.Show(bn, GameSkinArt.BButton, Vector2.zero, 46, "B");
            bfx.Play(K.PopRise(bfx, bn, 0.15f, 0.15f, 0.06f));

            // 本体：丸い黒い爆弾（白いふち）。導火線の火花が白と黄色で入れ替わる。回りながら飛ぶ
            Sprite a = GameSkinArt.Bomb(0), b = GameSkinArt.Bomb(1);
            var sr = K.Show(tag.Root, a, new Vector2(K.Dot, K.Dot), 20, "Bomb"); // 体のまん中を、判定のまん中へ
            tag.Spin = -540f;
            float lastDot = Time.time;
            tag.Tick = t =>
            {
                sr.sprite = Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0 ? a : b;
                // 通った後に小さい点を 0.05秒ごとに残す（放物線が見える）
                if (Time.time - lastDot >= 0.05f)
                {
                    lastDot = Time.time;
                    TrailDot(t.transform.position);
                }
            };
        }
        if (phase == DressPhase.Vanish && tag.Once("end")) tag.Tick = null; // 爆発は追撃（爆発の円）が描く
        return true;
    }

    static void TrailDot(Vector2 at)
    {
        var fx = K.Fx("GameTrail", at, 0.3f);
        var sr = K.Show(fx.transform, GameSkinArt.Square(2, Color.white), Vector2.zero, 21, "Dot");
        K.SetAlpha(sr, 0.5f);
        fx.Play(FadeSprite(fx, sr, 0.3f, 0.5f));
    }

    static IEnumerator FadeSprite(OfficeGameChatFx fx, SpriteRenderer sr, float dur, float from)
    {
        float start = fx.Now;
        while (fx != null && sr != null)
        {
            K.SetAlpha(sr, from * (1f - Mathf.Clamp01((fx.Now - start) / dur)));
            yield return null;
        }
    }

    // ───────── 能力：ボムの爆発（円） ─────────

    public static bool Blast(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Blast");
        if (phase == DressPhase.Active && tag.Once("boom"))
        {
            var fx = K.Adopted(info, "GameBoom", tag.transform.position, 0.6f);
            fx.Play(Boom(fx, K.SizeOf(info).x, true));
            StageCamera.Shake(0.05f, 0.12f);
        }
        return true;
    }

    /// <summary>
    /// 爆発（4つの段。合わせて 0.2秒＝今の判定の時間）：白い円 → 黄色の輪が広がる（いちばん外＝判定の円）→ オレンジのぎざぎざの輪 → 灰色の煙。
    /// boom なら、まん中に BOOM!（黄色、赤い影、斜め 8°）
    /// </summary>
    public static IEnumerator Boom(OfficeGameChatFx fx, float diameter, bool boom)
    {
        int D = Mathf.Max(12, Mathf.RoundToInt(diameter * 18f));
        int d0 = Mathf.Clamp(Mathf.RoundToInt(diameter * 18f * 0.375f), 6, D);
        var root = fx.transform;
        var core = K.Show(root, K.DiscSprite(d0, Color.white), Vector2.zero, 46, "Core");
        var ring = K.Show(root, GameSkinArt.JagRing(8, GameSkinArt.Yellow), Vector2.zero, 46, "Ring");
        var jag = K.Show(root, GameSkinArt.JagRing(8, GameSkinArt.Orange), Vector2.zero, 45, "Jag");
        jag.enabled = false;
        var smoke = new List<(Transform t, Vector2 v)>();
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            var s = K.Show(root, GameSkinArt.Square(4, GameSkinArt.Smoke), Vector2.zero, 30, "Smoke");
            s.enabled = false;
            smoke.Add((s.transform, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (diameter * 0.5f) * Random.Range(1.6f, 2.4f)));
        }
        Transform text = null;
        if (boom)
        {
            text = K.Node(root, "Boom", Vector2.zero);
            text.localRotation = Quaternion.Euler(0f, 0f, 8f);
            K.Show(text, K.Text("BOOM!", GameSkinArt.Yellow, 2, null, K.Hex("D7261E")), Vector2.zero, 47, "Text");
        }
        float start = fx.Now;
        while (fx != null)
        {
            float age = fx.Now - start;
            // 1. 白い円
            float cs = K.Pop(age, 0.04f);
            core.transform.localScale = new Vector3(cs, cs, 1f);
            K.SetAlpha(core, 1f - Mathf.Clamp01((age - 0.05f) / 0.07f));
            // 2. 黄色の輪（直径 d0 → D）
            float k2 = K.EaseOut(age / 0.12f);
            int dr = Mathf.Max(6, Mathf.RoundToInt(Mathf.Lerp(d0, D, k2) / 2f) * 2);
            ring.sprite = K.RingSprite(dr, 3, GameSkinArt.Yellow);
            K.SetAlpha(ring, 1f - Mathf.Clamp01((age - 0.2f) / 0.1f));
            // 3. オレンジのぎざぎざの輪
            if (age > 0.03f)
            {
                jag.enabled = true;
                float k3 = K.EaseOut((age - 0.03f) / 0.15f);
                int dj = Mathf.Max(8, Mathf.RoundToInt(Mathf.Lerp(D * 0.55f, D, k3) / 8f) * 8);
                jag.sprite = GameSkinArt.JagRing(dj, GameSkinArt.Orange);
                jag.transform.localRotation = Quaternion.Euler(0f, 0f, age * 90f);
                K.SetAlpha(jag, 1f - Mathf.Clamp01((age - 0.18f) / 0.1f));
            }
            // 4. 灰色の煙（外へ、だんだん遅く）
            if (age > 0.05f)
            {
                float k4 = Mathf.Clamp01((age - 0.05f) / 0.45f);
                foreach (var s in smoke)
                {
                    var sr = s.t.GetComponent<SpriteRenderer>();
                    sr.enabled = true;
                    s.t.localPosition = s.v * K.EaseOut(k4) * 0.5f;
                    K.SetAlpha(sr, 0.9f * (1f - k4));
                }
            }
            if (text != null)
            {
                float ts = K.Pop(age, 0.08f, 1.25f);
                text.localScale = new Vector3(ts, ts, 1f);
                if (age > 0.3f) K.FadeAll(text, 1f - Mathf.Clamp01((age - 0.3f) / 0.1f));
            }
            yield return null;
        }
    }

    // ───────── 小弾：コントローラーのボタン ─────────

    public static bool Buttons(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Button");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) ButtonWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    Color? fill = info.IsEnemy ? (Color?)null : K.Main(info.Theme, GameSkinArt.Sky);
                    K.Show(tag.Root, GameSkinArt.Button(info.Index, info.IsEnemy, fill), Vector2.zero, 20, "Button");
                    tag.Upright = true; // 字が逆さにならないように
                }
                break;
            default:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) tag.ExitShrink(0.08f, 1.3f, 1.3f);
                }
                else if (tag.Once("end"))
                {
                    tag.ExitShrink(0.1f, 1.15f, 0.3f); // 「押された」ようにへこんで消える
                }
                break;
        }
        return true;
    }

    /// <summary>予告：ボスの上に、ボタンが十字の形に並ぶ（発射する数だけ光る）</summary>
    static void ButtonWindup(OfficeGameChatTag tag, DressInfo info)
    {
        int n = info.Data != null ? Mathf.Max(1, info.Data.ProjectileCount) : Mathf.Max(1, info.Count);
        int rep = info.Data != null ? Mathf.Max(1, info.Data.RepeatCount) : 1;
        float dur = info.Duration > 0f ? info.Duration : 0.8f;
        // 十字：Y 上・X 左・B 右・A 下
        var pos = new[] { new Vector2(0f, -8f), new Vector2(8f, 0f), new Vector2(-8f, 0f), new Vector2(0f, 8f) };
        var node = K.Node(tag.Root, "Cross", Vector2.zero);
        var lit = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            K.Show(node, GameSkinArt.DimButton(i), pos[i] * K.Dot, 18, "Dim");
            lit[i] = K.Show(node, GameSkinArt.Button(i, true, null, 9), pos[i] * K.Dot, 19, "Lit");
            lit[i].enabled = false;
        }
        if (rep > 1) K.Show(node, K.Text("x" + rep, Color.white, 1, K.Ink), new Vector2(15f * K.Dot, -10f * K.Dot), 19, "Times");
        tag.Tick = t =>
        {
            float age = t.Age;
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.center.x, body.max.y + 0.8f, t.transform.position.z);
            float s = K.Pop(age, 0.1f);
            node.localScale = new Vector3(s, s, 1f);
            int on = Mathf.Clamp(Mathf.FloorToInt((age - 0.1f) / Mathf.Max(0.05f, dur * 0.12f)) + 1, 0, Mathf.Min(4, n));
            bool blink = K.BlinkOn(dur - age);
            for (int i = 0; i < 4; i++) lit[i].enabled = i < on && blink;
        };
    }

    // ───────── 長弾：ドット絵のミサイル ─────────

    public static bool Missile(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Missile");
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("windup")) MissileWindup(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    Sprite a = GameSkinArt.Missile(0), b = GameSkinArt.Missile(1);
                    var sr = K.Show(tag.Root, a, Vector2.zero, 20, "Missile");
                    tag.FlipLeft = true;
                    tag.Tick = t => sr.sprite = Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? a : b;
                }
                break;
            case DressPhase.Landed:
                if (!tag.Has("windup") && tag.Once("end"))
                {
                    // 着地：小さい爆発（床には溶岩＝追撃の床が出る）
                    var fx = K.Adopted(info, "GameSmallBoom", tag.transform.position, 0.4f);
                    fx.Play(Boom(fx, 1.2f, false));
                }
                break;
            case DressPhase.Vanish:
                if (tag.Has("windup"))
                {
                    if (tag.Once("windupEnd")) MissileWindupEnd(tag, info);
                }
                else if (tag.Once("end"))
                {
                    K.Debris(tag.transform.position, Vector2.up, new[] { GameSkinArt.Square(2, GameSkinArt.Orange), GameSkinArt.Square(2, Color.white) },
                        4, 31, 120f, 2f, 5f, 10f, 0.2f, 0.3f);
                }
                break;
        }
        return true;
    }

    /// <summary>予告：ボスの横に、照準（小）が連射の数だけ並ぶ</summary>
    static void MissileWindup(OfficeGameChatTag tag, DressInfo info)
    {
        int rep = info.Data != null ? Mathf.Max(1, info.Data.RepeatCount) : Mathf.Max(1, info.Count);
        float dur = info.Duration > 0f ? info.Duration : 0.8f;
        float dir = info.Direction.x >= 0f ? 1f : -1f;
        var node = K.Node(tag.Root, "Sights", Vector2.zero);
        for (int i = 0; i < rep; i++)
        {
            var s = K.Show(node, GameSkinArt.Crosshair(9, K.EnemyEdge), new Vector2(dir * i * 0.85f, 0f), 19, "Sight" + i);
            s.enabled = false;
        }
        tag.Tick = t =>
        {
            float age = t.Age;
            Bounds body = K.OwnerBody(info.Owner);
            t.Root.position = new Vector3(body.center.x + dir * (body.extents.x + 0.7f), body.center.y, t.transform.position.z);
            bool blink = K.BlinkOn(dur - age);
            for (int i = 0; i < rep; i++)
            {
                var s = node.Find("Sight" + i);
                if (s == null) continue;
                s.GetComponent<SpriteRenderer>().enabled = age > i * 0.12f && blink;
                s.localRotation = Quaternion.Euler(0f, 0f, -age * 180f);
            }
        };
    }

    static void MissileWindupEnd(OfficeGameChatTag tag, DressInfo info)
    {
        int rep = info.Data != null ? Mathf.Max(1, info.Data.RepeatCount) : 1;
        float interval = info.Data != null ? info.Data.RepeatInterval : 0.3f;
        Transform node = tag.Root != null ? tag.Root.Find("Sights") : null;
        tag.Tick = null;
        var fx = tag.DetachRoot(rep * interval + 0.15f);
        if (fx == null || node == null) return;
        fx.Play(Countdown(fx, node, "Sight", rep, interval));
    }

    /// <summary>1発撃つごとに、印を1つずつ消す</summary>
    public static IEnumerator Countdown(OfficeGameChatFx fx, Transform node, string prefix, int count, float interval)
    {
        float start = fx.Now;
        while (fx != null && node != null)
        {
            float age = fx.Now - start;
            for (int i = 0; i < count; i++)
            {
                var s = node.Find(prefix + i);
                if (s != null) s.GetComponent<SpriteRenderer>().enabled = age < i * interval;
            }
            yield return null;
        }
    }

    // ───────── 床：溶岩のブロック ─────────

    public static bool LavaFloor(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Lava");
        Vector2 size = K.SizeOf(info);
        int w = Mathf.Max(6, Mathf.RoundToInt(size.x * 18f)), h = Mathf.Max(4, Mathf.RoundToInt(size.y * 18f));
        switch (phase)
        {
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    var lavaF = K.Frames(4, i => GameSkinArt.Lava(w, h, i & 1, false, i >= 2));
                    var sr = K.Show(tag.Root, lavaF(0), Vector2.zero, 18, "Lava");
                    float tick = info.Data != null ? Mathf.Max(0.05f, info.Data.TickInterval) : 0.5f;
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        float age = Time.time - start;
                        sr.sprite = lavaF((Mathf.FloorToInt(age / 0.2f) & 1) + (age % tick < 0.06f ? 2 : 0));
                    };
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Root != null)
                {
                    tag.Tick = null;
                    var sr = tag.Root.Find("Lava")?.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = GameSkinArt.Lava(w, h, 0, true); // 黒く固まって消える
                    tag.ExitShrink(0.25f, 1f, 0.6f, new Vector3(0f, -size.y * 0.2f, 0f));
                }
                break;
        }
        return true;
    }

    // ───────── 柱：照準 → 光の柱 ─────────

    public static bool LightPillar(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Pillar");
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
                    layer.position = surface + up * 1f; // 床の上 1マス
                    var crossF = K.Frames(10, i => GameSkinArt.Crosshair(27 + i, K.EnemyEdge));
                    var cross = K.Show(layer, crossF(9), Vector2.zero, 19, "Cross");
                    float dur = info.Duration > 0f ? info.Duration : 1f;
                    float start = Time.time;
                    tag.Tick = t =>
                    {
                        float age = Time.time - start, k = Mathf.Clamp01(age / dur);
                        // 直径 2マス → 1.5マスへ縮む
                        int d = Mathf.RoundToInt(Mathf.Lerp(36f, 27f, k));
                        cross.sprite = crossF(d - 27);
                        cross.enabled = K.BlinkOn(dur - age);
                    };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    tag.Tick = null;
                    BuildBeam(tag, size, info.IsEnemy ? K.EnemyEdge : K.Main(info.Theme, GameSkinArt.Sky), info.IsEnemy);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.12f, 0.05f, 1f); // 細くなって消える
                }
                break;
        }
        return true;
    }

    static void BuildBeam(OfficeGameChatTag tag, Vector2 size, Color c, bool enemy)
    {
        var body = tag.Layer("body");
        float w = size.x, h = size.y, px = K.Dot;
        var pivot = K.Node(body, "Pivot", new Vector2(0f, h * 0.5f)); // 上から下へ落ちてくる
        pivot.localScale = new Vector3(1f, 0.001f, 1f);
        K.Box(pivot, new Vector2(0f, -h * 0.5f), new Vector2(w, h), K.Alpha(c, 0.45f), 40, "Glow");
        K.Box(pivot, new Vector2(0f, -h * 0.5f), new Vector2(w - px * 6f, h), K.Alpha(c, 0.85f), 41, "Beam");
        K.Box(pivot, new Vector2(0f, -h * 0.5f), new Vector2(Mathf.Max(px * 3f, w * 0.3f), h), Color.white, 42, "Core");
        K.Box(pivot, new Vector2(-w * 0.5f + px * 0.5f, -h * 0.5f), new Vector2(px, h), K.Edge(enemy), 43, "EdgeL");
        K.Box(pivot, new Vector2(w * 0.5f - px * 0.5f, -h * 0.5f), new Vector2(px, h), K.Edge(enemy), 43, "EdgeR");
        // 横じまが下へ流れる
        var stripes = new List<Transform>();
        int count = Mathf.CeilToInt(h / 0.55f);
        for (int i = 0; i < count; i++)
            stripes.Add(K.Box(body, Vector2.zero, new Vector2(w - px * 2f, px * 2f), K.Alpha(K.Hex("B3261E"), 0.5f), 43, "Stripe").transform);
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            pivot.localScale = new Vector3(1f, Mathf.Max(0.001f, Mathf.Clamp01(age / 0.05f)), 1f);
            float grown = h * Mathf.Clamp01(age / 0.05f);
            for (int i = 0; i < stripes.Count; i++)
            {
                if (stripes[i] == null) continue;
                float y = h * 0.5f - Mathf.Repeat(i * 0.55f + age * 9f, h); // 上の端から下へ
                stripes[i].localPosition = new Vector3(0f, y, 0f);
                stripes[i].gameObject.SetActive(h * 0.5f - y <= grown);
            }
        };
    }

    // ───────── 窓：実績の通知 ─────────

    public static bool AchievementWindow(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Achievement");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    tag.KeepVisible(info.CurrentVisual); // 点線の枠はそのまま
                    var ringF = K.Frames(8, i => GameSkinArt.LoadingRing(i, true)); // 予告は赤
                    var spin = K.Show(tag.Layer("tele"), ringF(0), Vector2.zero, 19, "Loading");
                    tag.Tick = t => { if (spin != null) spin.sprite = ringF(Mathf.FloorToInt(Time.time / 0.06f)); };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    WindowBody(tag, info, GameSkinArt.Achievement(true, false), 54, 36);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("platform")) SwapWindow(tag, GameSkinArt.Achievement(false, true));
                break;
            case DressPhase.Vanish:
                if (tag.Once("end") && tag.Has("body"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.2f, 1f, 1f, new Vector3(1.4f, 0f, 0f)); // 右へすべって消える
                }
                break;
        }
        return true;
    }

    /// <summary>窓の本体：「パッ」と出る（0 → 115% → 100%）。判定のある間は赤く光る。足場の最後の1秒は元の絵の点滅をまねる</summary>
    public static void WindowBody(OfficeGameChatTag tag, DressInfo info, Sprite sprite, int pw, int ph)
    {
        Vector2 size = K.SizeOf(info);
        var body = tag.Layer("body");
        var pop = K.Node(body, "Pop", Vector2.zero);
        var sr = K.Show(pop, sprite, Vector2.zero, 19, "Window");
        sr.transform.localScale = new Vector3(size.x * 18f / pw, size.y * 18f / ph, 1f);
        var flash = K.Box(pop, Vector2.zero, size, K.Alpha(K.EnemyEdge, 0.45f), 20, "Flash");
        float active = info.Duration > 0f ? info.Duration : 0.1f;
        float start = Time.time;
        tag.Tick = t =>
        {
            float age = Time.time - start;
            float s = K.Pop(age, 0.1f);
            pop.localScale = new Vector3(s, s, 1f);
            if (flash != null) flash.enabled = age < active;
            if (t.Falling != null && t.Falling.HasLanded && t.Falling.Visual != null) sr.enabled = t.Falling.Visual.enabled;
        };
    }

    public static void SwapWindow(OfficeGameChatTag tag, Sprite sprite)
    {
        if (tag.Root == null) return;
        var sr = tag.Root.Find("body/Pop/Window")?.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = sprite;
        var flash = tag.Root.Find("body/Pop/Flash");
        if (flash != null) Object.Destroy(flash.gameObject);
    }

    // ───────── 落下物：ライブラリのタイル ─────────

    public static bool LibraryFall(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Tile");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele")) SkyWarning(tag, info);
                break;
            case DressPhase.Active:
                if (tag.Once("body"))
                {
                    tag.ClearLayer("tele");
                    var sway = K.Node(tag.Layer("body"), "Sway", Vector2.zero);
                    var sr = K.Show(sway, GameSkinArt.LibraryTile(false), Vector2.zero, 19, "Tile");
                    sr.transform.localScale = new Vector3(size.x * 18f / 54f, size.y * 18f / 36f, 1f);
                    float start = Time.time;
                    tag.Tick = t => sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time - start) * 12f) * 2f);
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    // 着地：バーが 100% になって砕ける
                    var sr = tag.Root != null ? tag.Root.Find("body/Sway/Tile")?.GetComponent<SpriteRenderer>() : null;
                    if (sr != null) sr.sprite = GameSkinArt.LibraryTile(true);
                    var bits = new[] { GameSkinArt.Square(4, GameSkinArt.Navy), GameSkinArt.Square(3, GameSkinArt.Sky), GameSkinArt.Square(2, GameSkinArt.PlayGreen) };
                    K.Debris(tag.transform.position, Vector2.up, bits, 9, 31, 75f, 5f, 9f, 22f, 0.35f, 0.55f);
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

    /// <summary>落下物の予告：影はそのまま。影の上の空（画面の上の端）に、赤い「！」が点滅</summary>
    public static void SkyWarning(OfficeGameChatTag tag, DressInfo info)
    {
        tag.KeepVisible(info.CurrentVisual);
        var warn = K.Show(tag.Layer("tele"), OfficeArt.RedTriangle, Vector2.zero, 19, "Warn");
        tag.Tick = t =>
        {
            if (!t.HasLayer("tele")) return;
            t.Layer("tele").position = new Vector3(t.transform.position.x, K.View().yMax - 0.6f, t.transform.position.z);
            warn.enabled = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
        };
    }

    // ───────── 上の列：ダウンロードの列 ─────────

    public static bool DownloadRow(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Download");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
            case DressPhase.Active:
                if (tag.Once("tile"))
                {
                    var pop = K.Node(tag.Layer("body"), "Pop", Vector2.zero);
                    int art = info.Index;
                    var tileF = K.Frames(9, i => GameSkinArt.DownloadTile(i, art));
                    var sr = K.Show(pop, tileF(0), Vector2.zero, 19, "Tile");
                    sr.transform.localScale = new Vector3(size.x * 18f / 27f, size.y * 18f / 11f, 1f);
                    float placed = Time.time;
                    tag.Tick = t =>
                    {
                        float s = K.Pop(Time.time - placed, 0.1f);
                        pop.localScale = new Vector3(s, s, 1f);
                        // 進みのバー：落ちるまでに満タンになる
                        float fallAt = t.Falling != null ? t.Falling.FallStartTime : placed + 1f;
                        float k = Mathf.Clamp01((Time.time - placed) / Mathf.Max(0.1f, fallAt - placed));
                        sr.sprite = tileF(Mathf.RoundToInt(k * 8f));
                    };
                }
                break;
            case DressPhase.Landed:
                if (tag.Once("land"))
                {
                    Vector2 at = (Vector2)tag.transform.position + Vector2.up * 0.2f;
                    var fx = K.Adopted(info, "Game0", at, 0.4f);
                    var n = K.Node(fx.transform, "Text", Vector2.zero);
                    K.Show(n, K.Text("0%", GameSkinArt.Sky, 2, GameSkinArt.Navy), Vector2.zero, 47, "Text");
                    fx.Play(K.PopRise(fx, n, 0.35f, 0.5f, 0.08f));
                    K.Debris(at, Vector2.up, new[] { GameSkinArt.Square(2, GameSkinArt.Sky), GameSkinArt.Square(2, GameSkinArt.Navy) }, 3, 31, 70f, 2f, 4f, 10f, 0.25f, 0.35f);
                }
                break;
            case DressPhase.Vanish:
                if (tag.Once("end"))
                {
                    tag.Tick = null;
                    if (tag.Has("land")) tag.ExitShrink(0.08f, 1.2f, 0.2f);
                    else tag.ExitShrink(0.1f, 0.05f, 1f);
                }
                break;
        }
        return true;
    }

    // ───────── ワープ：ロード中 ─────────

    public static bool LoadingWarp(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Game.Warp");
        Vector2 size = K.SizeOf(info);
        switch (phase)
        {
            case DressPhase.Telegraph:
                if (tag.Once("tele"))
                {
                    // ボスが横じまになって消える（元の所。消える秒数のあいだ）
                    float vanish = info.Data != null ? Mathf.Max(0.15f, info.Data.VanishTime) : 0.5f;
                    Vector2 from = (Vector2)tag.transform.position - info.Direction;
                    var fx = K.Adopted(info, "GameScan", from, vanish + 0.05f);
                    fx.Play(Scanlines(fx, size, vanish));
                    // 行き先：回る輪と字 LOADING
                    var dest = tag.Layer("dest");
                    bool red = info.IsEnemy;
                    var ringF = K.Frames(8, i => GameSkinArt.LoadingRing(i, red));
                    var ring = K.Show(dest, ringF(0), Vector2.zero, 26, "Ring");
                    K.Show(dest, K.Text("LOADING", Color.white, 1, red ? K.EnemyEdge : GameSkinArt.Navy), new Vector2(0f, -0.75f), 26, "Text");
                    tag.Tick = t =>
                    {
                        if (ring != null) ring.sprite = ringF(Mathf.FloorToInt(Time.time / 0.06f));
                    };
                }
                break;
            case DressPhase.Active:
                if (tag.Once("appear") && tag.HasLayer("dest"))
                {
                    tag.Tick = null;
                    tag.ExitShrink(0.15f, 1.4f, 1.4f); // 現れたら、輪がはじける
                }
                break;
        }
        return true;
    }

    static IEnumerator Scanlines(OfficeGameChatFx fx, Vector2 size, float dur)
    {
        var lines = new List<SpriteRenderer>();
        int n = Mathf.CeilToInt(size.y / (K.Dot * 3f));
        for (int i = 0; i < n; i++)
            lines.Add(K.Box(fx.transform, new Vector2(0f, -size.y * 0.5f + i * K.Dot * 3f), new Vector2(size.x, K.Dot), K.Alpha(Color.black, 0f), 30, "Scan"));
        float start = fx.Now;
        while (fx != null)
        {
            float k = Mathf.Clamp01((fx.Now - start) / dur);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null) continue;
                // 横じまがだんだん太く・ずれる
                lines[i].transform.localScale = new Vector3(size.x * (1f + 0.2f * Mathf.Sin(i * 1.7f + k * 20f) * k), K.Dot * (1f + 2f * k), 1f);
                K.SetAlpha(lines[i], 0.75f * Mathf.Sin(k * Mathf.PI));
            }
            yield return null;
        }
    }
}
