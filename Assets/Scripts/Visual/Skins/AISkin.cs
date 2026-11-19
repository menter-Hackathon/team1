using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】AI のスキン（ChatGPT・Claude・Codex・Perplexity。2026-11-18。担当4。docs/app-skill-visuals-design.md 8章・1-7 の AI の行）。
// 何をまねるか：入力の欄と点滅するカーソル・1語ずつ出てくる回答・「考え中」の点・止めるボタン（■）・きらめきの印（✦）。
// ワープは Generic に任せる（今の見た目のまま）
public class AISkin : AppSkin
{
    public override SkinId Id => SkinId.AI;
    public override string NotificationTitle => "Done";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        AttackFrame f = K.Resolve(frame, info.Data);
        switch (f)
        {
            case AttackFrame.SmallShot:
                if (K.IsVolleyTelegraph(info)) return phase != DressPhase.Telegraph || AISkinArt.Thinking(info, true);
                return GenericFrameFx.Apply<AIFxToken>(f, phase, info);
            case AttackFrame.LongShot:
                if (K.IsVolleyTelegraph(info)) return phase != DressPhase.Telegraph || AISkinArt.Thinking(info, false);
                return GenericFrameFx.Apply<AIFxSentence>(f, phase, info);
            case AttackFrame.Thrown:
                if (K.IsVolleyTelegraph(info)) return false;
                return GenericFrameFx.Apply<AIFxSparkOrb>(f, phase, info);
            case AttackFrame.Floor: return GenericFrameFx.Apply<AIFxGarbled>(f, phase, info);
            case AttackFrame.Pillar: return GenericFrameFx.Apply<AIFxAnswer>(f, phase, info);
            case AttackFrame.Paint: return GenericFrameFx.Apply<AIFxTyping>(f, phase, info);
            case AttackFrame.Circle: return GenericFrameFx.Apply<AIFxPrompt>(f, phase, info);
            case AttackFrame.Window: return GenericFrameFx.Apply<AIFxCard>(f, phase, info);
            case AttackFrame.Falling: return GenericFrameFx.Apply<AIFxCodeBlock>(f, phase, info);
            case AttackFrame.TopRow: return GenericFrameFx.Apply<AIFxChip>(f, phase, info);
            default: return GenericFrameDresser.Dress(f, phase, info);
        }
    }

    // ───────── 当たった絵：4方向のきらめき（回りながら出る）。大は、言葉の札が 4個、外へ ─────────

    public override bool Hit(HitInfo info)
    {
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
        Color main = K.Main(info.Theme, info.AppIcon);
        if (info.Strength == HitStrength.Small)
        {
            K.Shards(info.Point, dir, 1, i => K.Sparkle(5), i => AISkinArt.SparkYellow);
            return true;
        }
        bool fin = info.Strength == HitStrength.Finisher;
        bool heavy = info.Strength == HitStrength.Heavy || fin;
        Vector2 at = info.Point + dir * 0.3f;
        float life = heavy ? 0.3f : 0.2f;
        Transform root = K.Loose("Gen4_AIHit", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        int d = heavy ? 23 : 13;
        float s = fin ? 1.5f : 1f;
        SpriteRenderer big = K.Spr(root, K.Sparkle(d), Vector2.zero, main, K.OrderHit, 1f, "Spark");
        SpriteRenderer core = K.Spr(root, K.Sparkle(d / 2), Vector2.zero, Color.white, K.OrderHit + 1, 1f, "Core");
        tw.Scale(root, Vector3.zero, Vector3.one * s, 0.1f, 0f, GenericFrameTweens.Ease.OutBack).Rotate(root, -45f, 45f, life);
        tw.Alpha(big, 1f, 0f, life * 0.4f, life * 0.6f).Alpha(core, 1f, 0f, life * 0.4f, life * 0.6f);
        if (heavy)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 v = Quaternion.Euler(0f, 0f, 45f + i * 90f) * Vector2.right;
                Transform chip = K.Loose("Gen4_AIChip", at);
                K.Plate(chip, Vector2.zero, 10, 5, 1, Color.white, AISkinArt.Dark, K.OrderHit + 2, out SpriteRenderer o, out SpriteRenderer n);
                K.BoxPx(chip, 0f, 0f, 5f, 1f, Color.white, K.OrderHit + 3, "Word");
                var ct = GenericFrameTweens.Of(chip.gameObject).Kill(0.32f);
                ct.Move(chip, at, at + v * (fin ? 1.4f : 0.9f), 0.3f).Alpha(o, 1f, 0f, 0.12f, 0.18f).Alpha(n, 1f, 0f, 0.12f, 0.18f);
            }
        }
        K.Shards(info.Point, dir, fin ? 12 : heavy ? 6 : 3, i => i % 2 == 0 ? K.Sparkle(5) : null, i => i % 2 == 0 ? AISkinArt.SparkYellow : Color.white);
        return true;
    }

    // ───────── エラーの絵：暗い札に赤い字 429（左に赤い「！」）。ダウンは Rate limit ─────────

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 at = new Vector2(b.center.x, b.max.y + 0.7f);
        if (down) at = Vector2.Lerp(at, ScreenUi.ViewRect().center, 0.35f);
        float scale = down ? 2f : 1f, life = down ? 0.9f : 0.4f;
        Transform root = K.Loose("Gen4_AIError", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        K.Plate(pic, Vector2.zero, 38, 14, 2, K.WinDarkEdge, AISkinArt.Dark, K.OrderHit, out SpriteRenderer o, out SpriteRenderer n);
        SpriteRenderer warn = K.Spr(pic, K.WarnTriangle, new Vector2(-12f, 0f) * K.Dot, K.Red, K.OrderHit + 1, 1f, "Warn");
        TextMesh tm = K.Text(pic, "429", new Vector2(4f, 0f) * K.Dot, 0.42f, K.Red, K.OrderHit + 2);
        tw.Pop(pic, Vector3.one * scale, 0.1f).Move(root, at, at + Vector2.up * 0.4f, life);
        tw.Alpha(o, 1f, 0f, 0.12f, life - 0.12f).Alpha(n, 1f, 0f, 0.12f, life - 0.12f).Alpha(warn, 1f, 0f, 0.12f, life - 0.12f).TextAlpha(tm, K.Red, 1f, 0f, 0.12f, life - 0.12f);
        if (down)
        {
            TextMesh rl = K.Text(root, "Rate limit", new Vector2(0f, -0.75f), 0.45f, Color.white, K.OrderHit + 2);
            tw.TextAlpha(rl, Color.white, 1f, 0f, 0.15f, life - 0.15f);
        }
        return true;
    }
}

/// <summary>【仮】AI の色と絵</summary>
public static class AISkinArt
{
    public static readonly Color Dark = RuntimeSprites.Hex("202123");
    public static readonly Color SparkYellow = RuntimeSprites.Hex("FFE08A");
    public static readonly Color[] Code = { RuntimeSprites.Hex("569CD6"), RuntimeSprites.Hex("CE9178"), RuntimeSprites.Hex("4EC9B0"), RuntimeSprites.Hex("DCDCAA") };

    /// <summary>止めるボタン（■。角の丸い四角 8ドット）</summary>
    public static Sprite Stop8 => K.RoundRect(8, 8, 2);

    /// <summary>コピーの印（4×4 の四角のふち）</summary>
    public static Sprite Copy5 => K.Glyph("AICopy5", new[] { "XXXX.", "X..XX", "X..XX", "XXXXX", ".XXXX" });

    /// <summary>
    /// 予備動作：「考え中」の点 3つが順に光る（小弾はボスの上、長弾はボスの横に点滅するカーソル）
    /// </summary>
    public static bool Thinking(DressInfo info, bool dots)
    {
        Color edge = info.IsEnemy ? K.Red : Color.white;
        if (dots)
        {
            return GenericFrameVolley.Show(info, 3, 0.32f, true, (slot, i) =>
            {
                SpriteRenderer d = K.Spr(slot, K.Disc(5), Vector2.zero, Color.white, K.OrderDeco + 1, 1f, "Dot");
                slot.gameObject.AddComponent<AISkinPulse>().Setup(d, i * 0.18f);
                return K.Spr(slot, K.Ring(7, 1), Vector2.zero, edge, K.OrderDeco, 1f, "Edge");
            }) != null;
        }
        int n = K.VolleyCount(info, false);
        return GenericFrameVolley.Show(info, n, 0.45f, false, (slot, i) =>
        {
            K.Plate(slot, Vector2.zero, 6, 9, 1, edge, Color.white, K.OrderDeco, out SpriteRenderer o, out SpriteRenderer c);
            slot.gameObject.AddComponent<AISkinPulse>().Setup(c, 0f, 0.25f, true);
            return o;
        }) != null;
    }

    /// <summary>白い棒の行（文）を描く：y 行目に、言葉ごとにすき間のある棒。seed で長さが変わる</summary>
    public static void TextLine(GenericFrameCanvas cv, int x0, int y, int maxW, int seed, Color32 c, float fill = 1f)
    {
        int x = x0;
        int end = x0 + Mathf.RoundToInt(maxW * fill);
        uint h = (uint)(seed * 2654435761u);
        while (x < end)
        {
            h = h * 1103515245u + 12345u;
            int word = 2 + (int)((h >> 16) % 6u);
            for (int i = 0; i < word && x < end; i++, x++) cv.Set(x, y, c);
            x++; // 言葉のすき間
        }
    }
}

/// <summary>【仮】点滅（考え中の点・カーソル）</summary>
public class AISkinPulse : MonoBehaviour
{
    SpriteRenderer _sr;
    float _phase, _period = 0.54f;
    bool _blink;

    public void Setup(SpriteRenderer sr, float phase, float period = 0.54f, bool blink = false)
    {
        _sr = sr;
        _phase = phase;
        _period = period;
        _blink = blink;
    }

    void Update()
    {
        if (_sr == null) return;
        float k = Mathf.Repeat(Time.time - _phase, _period) / _period;
        if (_blink) _sr.enabled = k < 0.5f;
        else K.SetAlpha(_sr, k < 0.33f ? 1f : 0.3f);
    }
}

// ───────── 小弾：言葉のかけら（トークン）。角の丸い小さい札、暗い地に白い棒 1本（1発ごとに長さが違う） ─────────

public class AIFxToken : GenericFrameFx
{
    Transform _body;

    protected override void Build()
    {
        Upright = true;
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Plate(_body, Vector2.zero, 12, 7, 2, Edge, AISkinArt.Dark, K.OrderCore, out _, out _);
        int len = 3 + (Info.Index * 3) % 5;
        K.BoxPx(_body, 0f, 0f, len, 1f, Color.white, K.OrderCore + 1, "Word");
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    void Scatter() => K.Shards(transform.position, Vector2.up, 4, i => null, i => i % 2 == 0 ? Color.white : Tint, 80f, 0.6f);

    protected override void OnLanded()
    {
        Scatter();
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (!IsLanded) Scatter();
        if (Root != null) Destroy(Root.gameObject);
    }
}

// ───────── 長弾：打たれていく文（白い棒の列が後ろに伸び、先頭に四角いカーソル） ─────────

public class AIFxSentence : GenericFrameFx
{
    Transform _body;
    readonly List<SpriteRenderer> _words = new List<SpriteRenderer>();
    SpriteRenderer _cursor;

    protected override void Build()
    {
        FlipWhenLeft = true;
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Plate(_body, Vector2.zero, 22, 9, 2, Edge, AISkinArt.Dark, K.OrderCore, out _, out _);
        _cursor = K.BoxPx(_body, 7.5f, 0f, 4f, 7f, Color.white, K.OrderCore + 1, "Cursor");
        float x = 4.5f;
        int[] lens = { 3, 4, 2, 4 };
        foreach (int l in lens)
        {
            x -= l + 1f;
            SpriteRenderer w = K.BoxPx(_body, x + l * 0.5f, 0f, l, 1f, Color.white, K.OrderCore + 1, "Word");
            w.enabled = false;
            _words.Add(w);
        }
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        int shown = Mathf.Min(_words.Count, Mathf.FloorToInt(t / 0.05f));
        for (int i = 0; i < _words.Count; i++) _words[i].enabled = i < shown;
        _cursor.enabled = Mathf.FloorToInt(t / 0.12f) % 2 == 0;
    }

    protected override void OnLanded()
    {
        K.Shards(transform.position, Vector2.up, 4, i => null, i => Color.white, 70f, 0.6f);
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (IsLanded)
        {
            if (Root != null) Destroy(Root.gameObject);
            return;
        }
        FadeAway(0.08f);
    }
}

// ───────── 投げ物（1-7）：きらめきの玉（✦。アプリの色と白）。回る ─────────

public class AIFxSparkOrb : GenericFrameFx
{
    Transform _body;
    float _spin;

    protected override void Build()
    {
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, K.Sparkle(15), Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        K.Spr(_body, K.Sparkle(13), Vector2.zero, Main, K.OrderCore + 1, 1f, "Spark");
        K.Spr(_body, K.Sparkle(7), Vector2.zero, Color.white, K.OrderCore + 2, 1f, "Core");
        _spin = Info.Direction.x < 0f ? 540f : -540f;
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    void Burst() => K.Shards(transform.position, Vector2.up, 5, i => K.Sparkle(5), i => i % 2 == 0 ? Main : AISkinArt.SparkYellow, 80f);

    protected override void OnLanded()
    {
        Burst();
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (!IsLanded) Burst();
        if (Root != null) Destroy(Root.gameObject);
    }

    protected override void LateUpdate()
    {
        if (Root != null) Root.rotation = Quaternion.Euler(0f, 0f, _spin * (Time.time - PhaseStart));
    }
}

// ───────── 床：文字化けした文（うその答え）。赤と白の小さい四角が 0.1秒ごとに入れ替わる帯 ─────────

public class AIFxGarbled : GenericFrameFx
{
    GenericFrameCanvas _cv;
    int _w, _h, _rows, _last = -1;
    Color32 _a, _b;

    protected override void Build()
    {
        Vector2 s = Size;
        _w = Px(s.x);
        _h = Px(s.y);
        _rows = s.y >= 0.8f ? 2 : 1; // トラップ弾の床は2段（1-7）
        _cv = new GenericFrameCanvas(_w, _h, "Garbled");
        _cv.Show(Root, Vector2.zero, K.OrderFloor + 1, "Garbled");
        _a = Info.IsEnemy ? K.Red : Main;
        _b = Color.white;
        Draw(0, false);
    }

    void Draw(int step, bool grey)
    {
        _cv.Clear();
        for (int r = 0; r < _rows; r++)
        {
            int y0 = _h - 4 - r * 5;
            for (int x = 1; x + 3 <= _w; x += 4)
            {
                uint h = (uint)((x * 73856093) ^ (r * 19349663) ^ (step * 83492791));
                if ((h >> 3) % 5 == 0) continue; // ところどころ空ける
                Color32 c = grey ? new Color32(0x8e, 0x8e, 0x93, 255) : ((h >> 7) % 2 == 0 ? _a : _b);
                _cv.Fill(x, y0, 3, 3, c);
            }
        }
        _cv.Apply();
    }

    protected override void Tick(float t)
    {
        int step = Mathf.FloorToInt(t / 0.1f);
        if (step == _last) return;
        _last = step;
        Draw(step, false);
    }

    protected override void OnVanish()
    {
        Draw(_last, true); // 灰色になって消える
        FadeAway(0.15f);
    }
}

// ───────── 柱：入力の欄 → 長い回答（白い棒の行が、下から上へ速く流れる柱）。止めるボタンが出て消える ─────────

public class AIFxAnswer : GenericFrameFx
{
    float _wu, _hu;
    Transform _tele;
    SpriteRenderer _caret;
    GenericFrameCanvas _cv;
    SpriteRenderer _cvSr;

    protected override void Build()
    {
        Vector2 s = Size;
        _wu = s.x;
        _hu = s.y;
    }

    protected override void OnTelegraph()
    {
        // 床に、角の丸い入力の欄と、点滅するカーソル
        _tele = K.Node(Root, "Telegraph", new Vector2(0f, -_hu * 0.5f + 4.5f * K.Dot));
        int w = Mathf.Max(12, Px(_wu));
        K.Plate(_tele, Vector2.zero, w, 9, 3, Edge, AISkinArt.Dark, K.OrderTelegraph, out _, out _);
        _caret = K.BoxPx(_tele, -w * 0.5f + 4f, 0f, 1f, 5f, Color.white, K.OrderTelegraph + 1, "Caret");
        K.Spr(_tele, K.Sparkle(5), new Vector2(w * 0.5f - 5f, 0f) * K.Dot, Main, K.OrderTelegraph + 1, 1f, "Spark");
    }

    protected override void OnActive()
    {
        if (_tele != null) Destroy(_tele.gameObject);
        int w = Px(_wu), h = Px(_hu);
        _cv = new GenericFrameCanvas(w, h, "Answer");
        _cvSr = _cv.Show(Root, Vector2.zero, K.OrderPillar, "Answer");
        SpriteRenderer flash = K.Box(Root, Vector2.zero, new Vector2(_wu, _hu), new Color(1f, 1f, 1f, 0.6f), K.OrderPillar + 1, "Flash");
        GenericFrameTweens.Of(gameObject).Alpha(flash, 0.6f, 0f, 0.08f);
        Draw(0f);
    }

    void Draw(float t)
    {
        if (_cv == null) return;
        _cv.Clear();
        int w = _cv.W, h = _cv.H;
        Color32 bg = K.WithAlpha(K.Step(AISkinArt.Dark, 0), 0.6f);
        Color32 edge = Edge;
        Color32 text = Color.white, accent = Tint;
        int scroll = Mathf.FloorToInt(t * 90f); // 速く流れる（ドット/秒）
        for (int y = 0; y < h; y++)
        {
            for (int x = 1; x < w - 1; x++) _cv.Set(x, y, bg);
            _cv.Set(0, y, edge);
            _cv.Set(w - 1, y, edge);
        }
        // 下から上へ流れる行（3ドットおき）
        for (int y = h - 2; y >= 1; y--)
        {
            int line = (h - y) + scroll;
            if (line % 3 != 0) continue;
            int id = line / 3;
            float fill = 0.45f + ((id * 37) % 50) / 100f;
            AISkinArt.TextLine(_cv, 3, y, w - 6, id, id % 7 == 0 ? accent : text, fill);
        }
        _cv.Apply();
    }

    protected override void Tick(float t)
    {
        if (Phase == DressPhase.Telegraph)
        {
            if (_caret != null) _caret.enabled = Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0;
            if (_tele != null) _tele.gameObject.SetActive(K.BlinkOn(TelegraphTime - t));
            return;
        }
        if (Phase == DressPhase.Active) Draw(t);
    }

    protected override void OnVanish()
    {
        if (Root != null)
        {
            // 止めるボタン（■）が出て、流れが止まり、消える
            Transform stop = K.Node(Root, "Stop", Vector2.zero);
            K.Spr(stop, K.Disc(14), Vector2.zero, AISkinArt.Dark, K.OrderPillar + 2, 1f, "Circle");
            K.Spr(stop, K.Ring(14, 1), Vector2.zero, Color.white, K.OrderPillar + 3, 1f, "Ring");
            K.Spr(stop, AISkinArt.Stop8, Vector2.zero, Color.white, K.OrderPillar + 3, 0.75f, "Square");
        }
        FadeAway(0.2f);
    }
}

// ───────── 塗り（1-7）：白い棒の行（文）が、上から1行ずつ打たれて範囲をうめる（暗い地 6割）。行の先にカーソル ─────────

public class AIFxTyping : GenericFrameFx
{
    GenericFrameCanvas _cv;
    int _lastKey = -1;

    protected override void Build()
    {
        _cv = new GenericFrameCanvas(Px(Size.x), Px(Size.y), "Typing");
        _cv.Show(Root, Vector2.zero, K.OrderPillar, "Typing");
    }

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        int w = _cv.W, h = _cv.H;
        int rows = Mathf.Max(1, (h - 3) / 4);
        float typeTime = 0.3f;
        int typed = Mathf.Min(rows, Mathf.FloorToInt(t / typeTime * rows) + 1);
        AttackData d = Info.Data;
        bool pulse = d != null && d.TickInterval > 0f && t > typeTime && (t % d.TickInterval) < 0.06f;
        bool caret = Mathf.FloorToInt(t / 0.25f) % 2 == 0;
        float remain = HitWindow - t;
        float fade = remain < 0.5f ? Mathf.Clamp01(remain / 0.5f) : 1f;
        int key = typed * 100 + (pulse ? 10 : 0) + (caret ? 1 : 0) + Mathf.RoundToInt(fade * 10f) * 1000;
        if (key == _lastKey) return;
        _lastKey = key;
        _cv.Clear();
        Color32 bg = K.WithAlpha(AISkinArt.Dark, 0.6f * fade);
        _cv.Fill(0, 0, w, h, bg);
        Color32 edge = K.WithAlpha(Edge, fade);
        for (int x = 0; x < w; x++) { _cv.Set(x, 0, edge); _cv.Set(x, h - 1, edge); }
        for (int y = 0; y < h; y++) { _cv.Set(0, y, edge); _cv.Set(w - 1, y, edge); }
        Color32 text = K.WithAlpha(pulse ? Color.Lerp(Color.white, Main, 0.5f) : Color.white, fade);
        int lastEnd = 3;
        for (int r = 0; r < typed; r++)
        {
            int y = 3 + r * 4;
            float fill = r == typed - 1 && typed < rows ? 0.5f : 0.7f + ((r * 29) % 30) / 100f;
            AISkinArt.TextLine(_cv, 3, y, w - 6, r + 1, text, fill);
            lastEnd = 3 + Mathf.RoundToInt((w - 6) * fill);
        }
        if (caret)
        {
            int cy = 3 + (typed - 1) * 4;
            _cv.Fill(Mathf.Min(w - 3, lastEnd + 1), cy - 2, 2, 4, K.WithAlpha(Main, fade));
        }
        _cv.Apply();
    }

    protected override void OnVanish() => FadeAway(0.1f);
}

// ───────── 円（1-7）：入力の欄が一瞬出て、言葉の札が輪になって広がる。まん中に大きいきらめき ─────────

public class AIFxPrompt : GenericFrameFx
{
    bool _shown;

    protected override void OnTelegraph() => Show();
    protected override void OnActive() => Show();

    void Show()
    {
        if (_shown) return;
        _shown = true;
        Upright = true;
        float d = Size.x, life = HitWindow;
        var tw = GenericFrameTweens.Of(gameObject);
        // 判定の外の輪（いちばん外＝判定）
        SpriteRenderer ring = K.Spr(Root, K.Ring(Px(d), 1), Vector2.zero, Edge, K.OrderSpark, 1f, "Ring");
        tw.Scale(ring.transform, Vector3.one * 0.15f, Vector3.one, life * 0.7f).Alpha(ring, 1f, 0.2f, life * 0.3f, life * 0.7f);
        SpriteRenderer fill = K.SprSized(Root, PlaceholderSprites.Circle, Vector2.zero, Vector2.one * d, K.WithAlpha(Main, 0.2f), K.OrderSpark - 2, "Fill");
        Vector3 fs = fill.transform.localScale;
        tw.Scale(fill.transform, fs * 0.15f, fs, life * 0.7f).Alpha(fill, 0.25f, 0f, life * 0.4f, life * 0.6f);
        // 入力の欄（一瞬）
        Transform box = K.Node(Root, "Prompt", Vector2.zero);
        K.Plate(box, Vector2.zero, 34, 9, 3, Edge, AISkinArt.Dark, K.OrderSpark + 1, out SpriteRenderer bo, out SpriteRenderer bi);
        tw.Pop(box, Vector3.one, 0.06f).Alpha(bo, 1f, 0f, 0.06f, 0.06f).Alpha(bi, 1f, 0f, 0.06f, 0.06f);
        // 言葉の札が輪になって広がる
        for (int i = 0; i < 8; i++)
        {
            Vector2 v = Quaternion.Euler(0f, 0f, i * 45f + 22.5f) * Vector2.right;
            Transform chip = K.Node(Root, "Chip", Vector2.zero);
            K.Plate(chip, Vector2.zero, 11, 6, 2, Color.white, i % 2 == 0 ? Main : AISkinArt.Dark, K.OrderSpark + 2, out SpriteRenderer co, out SpriteRenderer ci);
            SpriteRenderer word = K.BoxPx(chip, 0f, 0f, 3f + i % 3 * 2f, 1f, Color.white, K.OrderSpark + 3, "Word");
            tw.Move(chip, Vector3.zero, v * (d * 0.5f - 0.3f), life * 0.7f, 0.03f);
            tw.Alpha(co, 1f, 0f, life * 0.3f, life * 0.7f).Alpha(ci, 1f, 0f, life * 0.3f, life * 0.7f).Alpha(word, 1f, 0f, life * 0.3f, life * 0.7f);
        }
        // まん中に大きいきらめき
        Transform spark = K.Node(Root, "Spark", Vector2.zero);
        SpriteRenderer s1 = K.Spr(spark, K.Sparkle(25), Vector2.zero, Main, K.OrderSpark + 4, 1f, "Big");
        SpriteRenderer s2 = K.Spr(spark, K.Sparkle(13), Vector2.zero, Color.white, K.OrderSpark + 5, 1f, "Core");
        tw.Scale(spark, Vector3.zero, Vector3.one, 0.12f, 0f, GenericFrameTweens.Ease.OutBack).Rotate(spark, 0f, 90f, life);
        tw.Alpha(s1, 1f, 0f, life * 0.4f, life * 0.6f).Alpha(s2, 1f, 0f, life * 0.4f, life * 0.6f);
    }

    protected override void OnVanish() => FadeAway(0.1f, 1.1f);
}

// ───────── 窓：回答のカード（暗いカード、左上にきらめき、棒 3本、右下に小さい四角 2つ＝コピー・やり直し） ─────────

public class AIFxCard : GenericFxWindow
{
    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（回答のカード）なので、今の板のまま（2026-11-19）
    protected override Color PanelColor => K.WithAlpha(AISkinArt.Dark, 0.97f);
    protected override bool SlideOnVanish => false;

    protected override void BuildContent(Transform body, int w, int h)
    {
        K.Spr(body, K.Sparkle(9), new Vector2(-w * 0.5f + 7f, h * 0.5f - 7f) * K.Dot, Main, K.OrderWindow + 1, 1f, "Spark");
        float x0 = -w * 0.5f + 14f, x1 = w * 0.5f - 5f;
        float[] lens = { 0.9f, 0.75f, 0.5f };
        for (int i = 0; i < 3; i++)
        {
            float len = (x1 - x0) * lens[i];
            K.BoxPx(body, x0 + len * 0.5f, h * 0.5f - 7f - i * 6f, len, 2f, i == 0 ? Color.white : K.WinLightShade, K.OrderWindow + 1, "Line");
        }
        K.Spr(body, AISkinArt.Copy5, new Vector2(w * 0.5f - 15f, -h * 0.5f + 6f) * K.Dot, K.Grey, K.OrderWindow + 1, 1f, "Copy");
        K.Spr(body, K.Spinner(6), new Vector2(w * 0.5f - 7f, -h * 0.5f + 6f) * K.Dot, K.Grey, K.OrderWindow + 1, 1f, "Retry");
    }
}

// ───────── 落下物：コードのブロック（暗い板。上の帯に code、中に色つきの棒 4行）。着地で棒が飛び散る ─────────

public class AIFxCodeBlock : GenericFxFalling
{
    protected override Color PanelColor => K.WinDark;

    protected override void BuildContent(Transform body, int w, int h)
    {
        float bandY = h * 0.5f - 1f - 4.5f;
        K.BoxPx(body, 0f, bandY, w - 2f, 9f, K.WinDarkTitle, K.OrderWindow + 1, "Band");
        TextMesh tm = K.Text(body, "code", new Vector2(-w * 0.5f + 16f, bandY) * K.Dot, 0.4f, K.WinLightShade, K.OrderWindow + 2);
        tm.anchor = TextAnchor.MiddleCenter;
        K.Spr(body, AISkinArt.Copy5, new Vector2(w * 0.5f - 6f, bandY) * K.Dot, K.Grey, K.OrderWindow + 2, 1f, "Copy");
        int[] indent = { 0, 4, 4, 0 };
        float[] lens = { 0.55f, 0.7f, 0.45f, 0.3f };
        for (int i = 0; i < 4; i++)
        {
            float x0 = -w * 0.5f + 5f + indent[i];
            float len = (w - 12f - indent[i]) * lens[i];
            float y = bandY - 8.5f - i * 5f;
            K.BoxPx(body, x0 + len * 0.3f, y, len * 0.6f, 2f, AISkinArt.Code[i % 4], K.OrderWindow + 1, "Code");
            K.BoxPx(body, x0 + len * 0.6f + len * 0.25f + 1f, y, len * 0.4f, 2f, AISkinArt.Code[(i + 1) % 4], K.OrderWindow + 1, "Code");
        }
    }

    protected override void LandedBurst(Vector2 at)
    {
        K.Shards(at, Vector2.up, 8, i => null, i => AISkinArt.Code[i % 4], 80f, 1.2f);
    }
}

// ───────── 上の列：候補の札（角の丸い札）。着地で、きらめき ─────────

public class AIFxChip : GenericFxTopRow
{
    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（候補の札）なので、今の板のまま（2026-11-19）
    protected override Color PanelColor => AISkinArt.Dark;

    protected override void BuildContent(Transform body, int w, int h)
    {
        K.Spr(body, K.Sparkle(7), new Vector2(-w * 0.5f + 5f, 0f) * K.Dot, Main, K.OrderWindow + 1, 1f, "Spark");
        float x0 = -w * 0.5f + 10f, len = w - 14f;
        K.BoxPx(body, x0 + len * 0.4f, 0f, len * 0.8f, 1f, Color.white, K.OrderWindow + 1, "Word");
    }

    protected override void BuildBand(Transform root, float spacing)
    {
        K.Box(root, new Vector2(0f, Size.y * 0.5f + 0.22f), new Vector2(spacing * 0.8f, 0.28f), K.WithAlpha(Color.white, 0.22f), K.OrderTelegraph, "ChipRow");
    }

    protected override void LandedBurst(Vector2 at)
    {
        K.Shards(at, Vector2.up, 6, i => K.Sparkle(5), i => i % 2 == 0 ? Main : AISkinArt.SparkYellow, 80f);
    }
}
