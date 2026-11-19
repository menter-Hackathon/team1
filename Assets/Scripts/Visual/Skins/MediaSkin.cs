using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】Media のスキン（ミュージック・Spotify・QuickTime・TV・Podcast・VLC・IINA。2026-11-18。担当4。
// docs/app-skill-visuals-design.md 9章・1-7 の Media の行）。
// 何をまねるか：再生・一時停止・早送りのボタン、再生の位置のバー、音量の棒（イコライザー）、波形、音符、アルバムの四角。
// ワープは Generic に任せる（今の見た目のまま）
public class MediaSkin : AppSkin
{
    public override SkinId Id => SkinId.Media;
    public override string NotificationTitle => "Now playing";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        AttackFrame f = K.Resolve(frame, info.Data);
        switch (f)
        {
            case AttackFrame.SmallShot:
                if (K.IsVolleyTelegraph(info)) return phase != DressPhase.Telegraph || MediaSkinArt.StaffVolley(info);
                return GenericFrameFx.Apply<MediaFxNote>(f, phase, info);
            case AttackFrame.LongShot:
                if (K.IsVolleyTelegraph(info))
                {
                    if (phase != DressPhase.Telegraph) return true;
                    Color edge = info.IsEnemy ? K.Red : Color.white;
                    return GenericFrameVolley.Show(info, K.VolleyCount(info, false), 0.5f, false, (slot, i) =>
                    {
                        K.Spr(slot, MediaSkinArt.FastForward, Vector2.zero, Color.white, K.OrderDeco + 1, 1f, "FF");
                        return K.Spr(slot, K.RoundRect(12, 10, 2), Vector2.zero, edge, K.OrderDeco, 1f, "Edge");
                    }) != null;
                }
                return GenericFrameFx.Apply<MediaFxWave>(f, phase, info);
            case AttackFrame.Thrown:
                if (K.IsVolleyTelegraph(info)) return false;
                return GenericFrameFx.Apply<MediaFxRecord>(f, phase, info);
            case AttackFrame.Floor: return GenericFrameFx.Apply<MediaFxClipping>(f, phase, info);
            case AttackFrame.Pillar: return GenericFrameFx.Apply<MediaFxVolume>(f, phase, info);
            case AttackFrame.Paint: return GenericFrameFx.Apply<MediaFxEqualizer>(f, phase, info);
            case AttackFrame.Circle: return GenericFrameFx.Apply<MediaFxSpeaker>(f, phase, info);
            case AttackFrame.Window: return GenericFrameFx.Apply<MediaFxPlayer>(f, phase, info);
            case AttackFrame.Falling: return GenericFrameFx.Apply<MediaFxJacket>(f, phase, info);
            case AttackFrame.TopRow: return GenericFrameFx.Apply<MediaFxRow>(f, phase, info);
            default: return GenericFrameDresser.Dress(f, phase, info);
        }
    }

    // ───────── 当たった絵：音符 3つが上へ。大は、ハート（いいね）が「ポン」とふくらむ ─────────

    public override bool Hit(HitInfo info)
    {
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
        Color main = K.Main(info.Theme, info.AppIcon);
        if (info.Strength == HitStrength.Small)
        {
            K.Shards(info.Point, dir, 1, i => MediaSkinArt.NoteSmall, i => main);
            return true;
        }
        bool fin = info.Strength == HitStrength.Finisher;
        bool heavy = info.Strength == HitStrength.Heavy || fin;
        Vector2 at = info.Point + dir * 0.3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i; // 動きの中で使う番号（ループの変数をそのまま使わない）
            Transform n = K.Loose("Gen4_MediaNote", at + new Vector2((i - 1) * 0.3f, 0f));
            SpriteRenderer sr = K.Spr(n, MediaSkinArt.Note, Vector2.zero, main, K.OrderHit, fin ? 1.4f : 1f, "Note");
            var tw = GenericFrameTweens.Of(n.gameObject).Kill(0.4f);
            Vector3 p = n.position;
            tw.Each(0.38f, k =>
            {
                if (n == null) return;
                n.position = p + new Vector3(Mathf.Sin(k * 10f + idx * 2f) * 0.12f, k * (0.8f + idx * 0.15f), 0f);
            }, i * 0.04f).Alpha(sr, 1f, 0f, 0.15f, 0.25f);
        }
        if (heavy)
        {
            Transform h = K.Loose("Gen4_MediaHeart", at + Vector2.up * 0.3f);
            SpriteRenderer e = K.Spr(h, MediaSkinArt.HeartEdge, Vector2.zero, Color.white, K.OrderHit + 1, 1f, "Edge");
            SpriteRenderer f = K.Spr(h, MediaSkinArt.Heart, Vector2.zero, main, K.OrderHit + 2, 1f, "Heart");
            var tw = GenericFrameTweens.Of(h.gameObject).Kill(0.32f);
            tw.Scale(h, Vector3.zero, Vector3.one * (fin ? 1.8f : 1.2f), 0.12f, 0f, GenericFrameTweens.Ease.OutBack).Alpha(e, 1f, 0f, 0.12f, 0.2f).Alpha(f, 1f, 0f, 0.12f, 0.2f);
        }
        K.Shards(info.Point, dir, fin ? 12 : heavy ? 6 : 3, i => i % 2 == 0 ? MediaSkinArt.NoteSmall : null, i => i % 2 == 0 ? main : Color.white);
        return true;
    }

    // ───────── エラーの絵：スピーカーに赤い ×（消音）。ダウンは一時停止の印 ─────────

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 at = new Vector2(b.center.x, b.max.y + 0.6f);
        if (down) at = Vector2.Lerp(at, ScreenUi.ViewRect().center, 0.35f);
        float life = down ? 0.9f : 0.4f;
        Transform root = K.Loose("Gen4_MediaError", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        if (down)
        {
            K.Spr(pic, K.Disc(26), Vector2.zero, K.WithAlpha(MediaSkinArt.Dark, 0.9f), K.OrderHit, 1f, "Bg");
            K.Spr(pic, MediaSkinArt.Pause20, Vector2.zero, Color.white, K.OrderHit + 1, 1f, "Pause");
            tw.Pop(pic, Vector3.one * 1.5f, 0.12f);
        }
        else
        {
            K.Spr(pic, MediaSkinArt.Speaker, new Vector2(-3f, 0f) * K.Dot, Color.white, K.OrderHit, 1f, "Speaker");
            K.Spr(pic, K.Cross5, new Vector2(5f, 0f) * K.Dot, K.Red, K.OrderHit + 1, 1.4f, "Mute");
            tw.Pop(pic, Vector3.one, 0.1f);
        }
        tw.Move(root, at, at + Vector2.up * 0.4f, life);
        foreach (var sr in pic.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.12f, life - 0.12f);
        return true;
    }
}

/// <summary>【仮】Media の色と絵</summary>
public static class MediaSkinArt
{
    public static readonly Color Dark = RuntimeSprites.Hex("121212");
    public static readonly Color Vinyl = RuntimeSprites.Hex("111114");

    static readonly string[] NoteRows =
    {
        ".....XX...",
        ".....XXX..",
        ".....X.XX.",
        ".....X..XX",
        ".....X...X",
        ".....X....",
        ".....X....",
        ".....X....",
        "..XXXX....",
        ".XXXXX....",
        ".XXXXX....",
        "..XXX.....",
    };

    static bool NoteOn(int x, int y) => y >= 0 && y < NoteRows.Length && x >= 0 && x < NoteRows[0].Length && NoteRows[y][x] == 'X';

    /// <summary>8分音符（10×12。ふちの分だけ大きい 12×14 の中のまん中）</summary>
    public static Sprite Note => K.Make("MdNote", 12, 14, (x, y) => NoteOn(x - 1, y - 1));
    /// <summary>8分音符のふち（1ドット太らせた形）</summary>
    public static Sprite NoteEdge => K.Make("MdNoteEdge", 12, 14, (x, y) =>
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (NoteOn(x - 1 + dx, y - 1 + dy)) return true;
            }
        }
        return false;
    });

    /// <summary>小さい音符（6ドット。かけら用）</summary>
    public static Sprite NoteSmall => K.Glyph("MdNoteSmall", new[] { "...XX", "...XX", "...X.", "...X.", "XXXX.", "XXX.." });

    public static Sprite FastForward => K.Glyph("MdFF", new[] { "X...X...", "XX..XX..", "XXX.XXX.", "XXXXXXXX", "XXX.XXX.", "XX..XX..", "X...X..." });
    public static Sprite Play => K.Glyph("MdPlay", new[] { "X...", "XX..", "XXX.", "XXXX", "XXX.", "XX..", "X..." });

    public static Sprite Speaker => K.Glyph("MdSpeaker", new[]
    {
        "....X....",
        "...XX....",
        "XXXXX....",
        "XXXXX....",
        "XXXXX....",
        "XXXXX....",
        "...XX....",
        "....X....",
    });

    public static Sprite Pause20 => K.Make("MdPause20", 16, 20, (x, y) => x < 6 || x >= 10);

    static readonly string[] HeartRows =
    {
        ".XXX...XXX.",
        "XXXXX.XXXXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        ".XXXXXXXXX.",
        "..XXXXXXX..",
        "...XXXXX...",
        "....XXX....",
        ".....X.....",
    };

    static bool HeartOn(int x, int y) => y >= 0 && y < HeartRows.Length && x >= 0 && x < HeartRows[0].Length && HeartRows[y][x] == 'X';

    public static Sprite Heart => K.Make("MdHeart", 13, 11, (x, y) => HeartOn(x - 1, y - 1));
    public static Sprite HeartEdge => K.Make("MdHeartEdge", 13, 11, (x, y) =>
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (HeartOn(x - 1 + dx, y - 1 + dy)) return true;
            }
        }
        return false;
    });

    /// <summary>予備動作：ボスの上に、短い五線（線 3本）と、発射する数の音符</summary>
    public static bool StaffVolley(DressInfo info)
    {
        Color main = K.Main(info.Theme, info.AppIcon);
        Color edge = info.IsEnemy ? K.Red : Color.white;
        return GenericFrameVolley.Show(info, K.VolleyCount(info, true), 0.7f, true, (slot, i) =>
        {
            for (int l = -1; l <= 1; l++) K.Box(slot, new Vector2(0f, l * 3f * K.Dot), new Vector2(0.72f, K.Dot), K.WithAlpha(Color.white, 0.6f), K.OrderDeco, "Staff");
            SpriteRenderer e = K.Spr(slot, NoteEdge, new Vector2(0f, 2f * K.Dot), edge, K.OrderDeco + 1, 0.8f, "Edge");
            K.Spr(slot, Note, new Vector2(0f, 2f * K.Dot), main, K.OrderDeco + 2, 0.8f, "Note");
            return e;
        }, K.VolleyRows(info)) != null;
    }

    /// <summary>レコード（黒い丸、まん中がアプリの色）を parent に置く。d は直径（ドット）</summary>
    public static void Record(Transform parent, Vector2 localPos, int d, Color label, int order)
    {
        K.Spr(parent, K.Disc(d), localPos, Vinyl, order, 1f, "Vinyl");
        K.Spr(parent, K.Ring(Mathf.Max(4, d - 4), 1), localPos, K.WithAlpha(Color.white, 0.18f), order + 1, 1f, "Groove");
        K.Spr(parent, K.Disc(Mathf.Max(3, d / 3)), localPos, label, order + 1, 1f, "Label");
        K.Spr(parent, K.Disc(2), localPos, Color.white, order + 2, 1f, "Hole");
    }
}

// ───────── 小弾：音符（アプリの色、赤いふち）。上下に 1ドットゆれながら飛ぶ ─────────

public class MediaFxNote : GenericFrameFx
{
    Transform _body;

    protected override void Build()
    {
        Upright = true;
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, MediaSkinArt.NoteEdge, Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        K.Spr(_body, MediaSkinArt.Note, Vector2.zero, Main, K.OrderCore + 1, 1f, "Note");
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void Tick(float t)
    {
        if (_body != null && Phase == DressPhase.Active) _body.localPosition = new Vector3(0f, Mathf.Round(Mathf.Sin(t * 22f + Info.Index)) * K.Dot, 0f);
    }

    void Burst() => GenericFrameBits.Pop(transform.position, null, 0, Edge, Main, 4);

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
}

// ───────── 長弾：波形（縦の棒が 8本並ぶ。まん中が高い） ─────────

public class MediaFxWave : GenericFrameFx
{
    Transform _body;
    readonly List<SpriteRenderer> _bars = new List<SpriteRenderer>();
    static readonly int[] Heights = { 2, 4, 6, 7, 6, 4, 2 };

    protected override void Build()
    {
        FlipWhenLeft = true;
        _body = K.Node(Root, "Body", Vector2.zero);
        // 敵の弾は判定より 3ドットまで（板 24×11）
        K.Plate(_body, Vector2.zero, 24, 11, 2, Edge, MediaSkinArt.Dark, K.OrderCore, out _, out _);
        for (int i = 0; i < Heights.Length; i++)
        {
            float x = -9f + i * 3f;
            _bars.Add(K.BoxPx(_body, x, 0f, 2f, Heights[i], Main, K.OrderCore + 1, "Bar"));
        }
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        for (int i = 0; i < _bars.Count; i++)
        {
            float h = Mathf.Max(1f, Heights[i] + Mathf.Round(Mathf.Sin(t * 30f + i * 1.3f) * 1.5f));
            K.SetSize(_bars[i], new Vector2(2f, Mathf.Min(9f, h)) * K.Dot);
        }
    }

    protected override void OnLanded()
    {
        K.Shards(transform.position, Vector2.up, 4, i => null, i => Main, 70f, 0.6f);
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

// ───────── 投げ物（1-7）：小さいレコード（黒い丸、まん中がアプリの色）。回る ─────────

public class MediaFxRecord : GenericFrameFx
{
    Transform _body;
    float _spin;

    protected override void Build()
    {
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, K.Disc(14), Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        MediaSkinArt.Record(_body, Vector2.zero, 12, Main, K.OrderCore + 1);
        _spin = Info.Direction.x < 0f ? 720f : -720f;
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    void Burst() => K.Shards(transform.position, Vector2.up, 4, i => MediaSkinArt.NoteSmall, i => i % 2 == 0 ? Main : Color.white, 80f);

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

// ───────── 床：音が割れたイコライザー（赤い縦の棒が 6〜8本、ばらばらに上下する） ─────────

public class MediaFxClipping : GenericFrameFx
{
    GenericFrameCanvas _cv;
    int _last = -1;
    Color32 _col, _hot;

    protected override void Build()
    {
        _cv = new GenericFrameCanvas(Px(Size.x), Px(Size.y), "Clipping");
        _cv.Show(Root, Vector2.zero, K.OrderFloor + 1, "Clipping");
        Color c = Info.IsEnemy ? K.Red : Main;
        _col = c;
        _hot = Color.Lerp(c, Color.white, 0.5f);
        Draw(0f);
    }

    void Draw(float t)
    {
        _cv.Clear();
        int w = _cv.W, h = _cv.H;
        int maxH = Mathf.Min(h - 1, h >= 12 ? 14 : 7); // トラップ弾の床（1マス）は2段ぶんの高さ
        int bars = Mathf.Clamp(w / 5, 6, 12);
        float step = w / (float)bars;
        AttackData d = Info.Data;
        bool flash = d != null && d.PersistDuration > 0f && d.TickInterval > 0f && (t % d.TickInterval) < 0.06f;
        for (int i = 0; i < bars; i++)
        {
            float n = Mathf.PerlinNoise(i * 1.7f, t * 9f);
            int bh = Mathf.Clamp(Mathf.RoundToInt(2f + n * (maxH - 2)), 2, maxH);
            int x = Mathf.RoundToInt(i * step + step * 0.5f) - 1;
            for (int y = 0; y < bh; y++)
            {
                Color32 c = flash ? (Color32)Color.white : (y >= bh - 1 ? _hot : _col);
                _cv.Set(x, h - 1 - y, c);
                _cv.Set(x + 1, h - 1 - y, c);
            }
        }
        _cv.Apply();
    }

    protected override void Tick(float t)
    {
        int key = Mathf.FloorToInt(t / 0.05f);
        if (key == _last) return;
        _last = key;
        Draw(t);
    }

    protected override void OnVanish() => FadeAway(0.12f);
}

// ───────── 柱：音量が最大（床に再生の位置の丸と横のバー → 縦の棒 3本が下から上へ一気に伸びる。下は白、上は赤） ─────────

public class MediaFxVolume : GenericFrameFx
{
    float _wu, _hu;
    Transform _tele, _knob;
    readonly List<Transform> _bars = new List<Transform>();
    readonly List<float> _barH = new List<float>();

    protected override void Build()
    {
        Vector2 s = Size;
        _wu = s.x;
        _hu = s.y;
    }

    float Bottom => -_hu * 0.5f;

    protected override void OnTelegraph()
    {
        _tele = K.Node(Root, "Telegraph", new Vector2(0f, Bottom + 3f * K.Dot));
        K.Box(_tele, Vector2.zero, new Vector2(_wu, 2f * K.Dot), K.WithAlpha(Color.white, 0.5f), K.OrderTelegraph, "Bar");
        K.Box(_tele, new Vector2(-_wu * 0.25f, 0f), new Vector2(_wu * 0.5f, 2f * K.Dot), Edge, K.OrderTelegraph + 1, "Played");
        _knob = K.Spr(_tele, K.Disc(9), new Vector2(0f, 0f), Edge, K.OrderTelegraph + 2, 1f, "Knob").transform;
    }

    protected override void OnActive()
    {
        if (_tele != null) Destroy(_tele.gameObject);
        Color top = K.Step(Tint, Info.Index);
        float bw = (_wu - 4f * K.Dot) / 3f;
        for (int i = 0; i < 3; i++)
        {
            Transform bar = K.Node(Root, "Bar", new Vector2(-_wu * 0.5f + 2f * K.Dot + bw * (i + 0.5f), Bottom));
            // 下は白、上は赤（アプリの色）。根元を下に置いて、上へ伸ばす
            K.Box(bar, new Vector2(0f, _hu * 0.3f), new Vector2(bw - 2f * K.Dot, _hu * 0.6f), K.WithAlpha(Color.white, 0.6f), K.OrderPillar, "Low");
            K.Box(bar, new Vector2(0f, _hu * 0.8f), new Vector2(bw - 2f * K.Dot, _hu * 0.4f), K.WithAlpha(top, 0.75f), K.OrderPillar, "High");
            K.Box(bar, new Vector2(0f, _hu - K.Dot), new Vector2(bw - 2f * K.Dot, 2f * K.Dot), Color.white, K.OrderPillar + 1, "Peak");
            bar.localScale = new Vector3(1f, 0f, 1f);
            _bars.Add(bar);
            _barH.Add(0.85f + 0.15f * ((i * 7) % 3) / 2f);
        }
        K.Box(Root, new Vector2(-_wu * 0.5f + 0.5f * K.Dot, 0f), new Vector2(K.Dot, _hu), Edge, K.OrderPillar + 2, "EdgeL");
        K.Box(Root, new Vector2(_wu * 0.5f - 0.5f * K.Dot, 0f), new Vector2(K.Dot, _hu), Edge, K.OrderPillar + 2, "EdgeR");
    }

    protected override void Tick(float t)
    {
        if (Phase == DressPhase.Telegraph)
        {
            if (_knob != null) _knob.localPosition = new Vector3(Mathf.Lerp(-_wu * 0.5f, 0f, Mathf.Clamp01(t / Mathf.Max(0.05f, TelegraphTime))), 0f, 0f);
            if (_tele != null) _tele.gameObject.SetActive(K.BlinkOn(TelegraphTime - t));
            return;
        }
        if (Phase != DressPhase.Active) return;
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.08f), 2f);
        for (int i = 0; i < _bars.Count; i++)
        {
            float jitter = rise >= 1f ? Mathf.Sin(t * 40f + i * 2f) * 0.05f : 0f;
            _bars[i].localScale = new Vector3(1f, Mathf.Clamp01(rise * _barH[i] + jitter + (rise >= 1f ? 0.1f : 0f)), 1f);
        }
    }

    protected override void OnVanish()
    {
        var bars = new List<Transform>(_bars);
        Transform r = Detach(0.15f);
        if (r == null) return;
        var tw = GenericFrameTweens.Of(r.gameObject);
        foreach (var b in bars) if (b != null) tw.Scale(b, b.localScale, new Vector3(1f, 0f, 1f), 0.12f, 0f, GenericFrameTweens.Ease.InQuad);
        foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>()) if (sr.name.StartsWith("Edge")) tw.Alpha(sr, 1f, 0f, 0.1f);
    }
}

// ───────── 塗り（1-7）：イコライザーの棒（幅 6ドット）が横にすき間なく並び、ばらばらに上下する ─────────

public class MediaFxEqualizer : GenericFrameFx
{
    GenericFrameCanvas _cv;
    int _last = -1;

    protected override void Build()
    {
        _cv = new GenericFrameCanvas(Px(Size.x), Px(Size.y), "Equalizer");
        _cv.Show(Root, Vector2.zero, K.OrderPillar, "Equalizer");
    }

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        int key = Mathf.FloorToInt(t / 0.05f);
        if (key == _last) return;
        _last = key;
        int w = _cv.W, h = _cv.H;
        float remain = HitWindow - t;
        float fade = remain < 0.5f ? Mathf.Clamp01(remain / 0.5f) : 1f;
        float grow = Mathf.Clamp01(t / 0.12f);
        AttackData d = Info.Data;
        bool pulse = d != null && d.TickInterval > 0f && (t % d.TickInterval) < 0.06f;
        Color32 lo = K.WithAlpha(Main, 0.62f * fade), hi = K.WithAlpha(Color.Lerp(Main, Color.white, pulse ? 0.7f : 0.4f), 0.85f * fade);
        Color32 edge = K.WithAlpha(Edge, fade);
        _cv.Clear();
        int bars = Mathf.Max(1, w / 7);
        for (int i = 0; i < bars; i++)
        {
            float n = Mathf.PerlinNoise(i * 0.9f + 3f, t * 6f);
            int bh = Mathf.RoundToInt(h * grow * Mathf.Lerp(0.35f, 1f, n));
            if (i % 3 == 1) bh = Mathf.RoundToInt(h * grow); // 何本かは上の端（範囲の高さ）まで
            int x0 = i * 7 + 1;
            for (int y = 0; y < bh; y++)
            {
                bool seg = (y % 3) == 2; // セグメントの切れ目
                Color32 c = y > h * 0.7f ? hi : lo;
                if (seg) c.a = (byte)(c.a / 3);
                for (int x = x0; x < x0 + 6 && x < w; x++) _cv.Set(x, h - 1 - y, c);
            }
        }
        for (int x = 0; x < w; x++) _cv.Set(x, h - 1, edge);
        _cv.Apply();
    }

    protected override void OnVanish() => FadeAway(0.1f);
}

// ───────── 円（1-7）：スピーカーの音の輪（弧が3重。アプリの色 → 白）が外へ ─────────

public class MediaFxSpeaker : GenericFrameFx
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
        int dpx = Px(d);
        var tw = GenericFrameTweens.Of(gameObject);
        Color[] cols = { Main, Color.Lerp(Main, Color.white, 0.5f), Color.white };
        for (int i = 0; i < 3; i++)
        {
            SpriteRenderer ring = K.Spr(Root, K.Ring(dpx, 2), Vector2.zero, cols[i], K.OrderSpark + i, 1f, "Wave");
            float delay = i * life * 0.15f;
            tw.Scale(ring.transform, Vector3.one * 0.1f, Vector3.one * (i == 2 ? 1f : 0.85f - i * 0.1f + 0.1f), life * 0.6f, delay).Alpha(ring, 1f, 0f, life * 0.35f, delay + life * 0.5f);
        }
        SpriteRenderer fill = K.SprSized(Root, PlaceholderSprites.Circle, Vector2.zero, Vector2.one * d, K.WithAlpha(Main, 0.2f), K.OrderSpark - 1, "Fill");
        Vector3 fs = fill.transform.localScale;
        tw.Scale(fill.transform, fs * 0.15f, fs, life * 0.6f).Alpha(fill, 0.25f, 0f, life * 0.4f, life * 0.6f);
        Transform spk = K.Node(Root, "Speaker", Vector2.zero);
        K.Spr(spk, K.Disc(22), Vector2.zero, MediaSkinArt.Dark, K.OrderSpark + 3, 1f, "Bg");
        K.Spr(spk, K.Ring(22, 1), Vector2.zero, Edge, K.OrderSpark + 4, 1f, "Edge");
        K.Spr(spk, MediaSkinArt.Speaker, new Vector2(-1f, 0f) * K.Dot, Main, K.OrderSpark + 4, 1.4f, "Icon");
        tw.Scale(spk, Vector3.zero, Vector3.one, 0.12f, 0f, GenericFrameTweens.Ease.OutBack);
        foreach (var sr in spk.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, life * 0.4f, life * 0.6f);
    }

    protected override void OnVanish() => FadeAway(0.1f, 1.1f);
}

// ───────── 窓：小さいプレーヤー（アルバムの四角・▶ と棒。下の再生のバーが、足場の残り時間に合わせて進む） ─────────

public class MediaFxPlayer : GenericFxWindow
{
    SpriteRenderer _progress;
    float _barStart, _barLen, _start;

    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（プレーヤー）なので、今の板のまま（2026-11-19）
    protected override Color PanelColor => K.WithAlpha(MediaSkinArt.Dark, 0.97f);

    protected override void BuildContent(Transform body, int w, int h)
    {
        int art = Mathf.Min(20, h - 12);
        float ax = -w * 0.5f + 4f + art * 0.5f, ay = 3f;
        K.Spr(body, K.RoundRect(art, art, 1), new Vector2(ax, ay) * K.Dot, Main, K.OrderWindow + 1, 1f, "Album");
        MediaSkinArt.Record(body, new Vector2(ax, ay) * K.Dot, Mathf.Max(6, art - 8), Color.Lerp(Main, Color.white, 0.4f), K.OrderWindow + 2);
        float x0 = ax + art * 0.5f + 4f, x1 = w * 0.5f - 4f;
        K.Spr(body, MediaSkinArt.Play, new Vector2(x0 + 2f, ay + 5f) * K.Dot, Color.white, K.OrderWindow + 1, 1f, "Play");
        K.BoxPx(body, (x0 + 7f + x1) * 0.5f, ay + 6f, x1 - x0 - 7f, 2f, Color.white, K.OrderWindow + 1, "Title");
        K.BoxPx(body, x0 + (x1 - x0) * 0.35f, ay + 1f, (x1 - x0) * 0.7f, 1f, K.Grey, K.OrderWindow + 1, "Artist");
        // 下の再生のバー
        _barStart = -w * 0.5f + 4f;
        _barLen = w - 8f;
        K.BoxPx(body, 0f, -h * 0.5f + 4f, _barLen, 1f, K.WithAlpha(Color.white, 0.3f), K.OrderWindow + 1, "Track");
        _progress = K.BoxPx(body, _barStart, -h * 0.5f + 4f, 0.01f, 1f, Main, K.OrderWindow + 2, "Progress");
    }

    protected override void OnActive()
    {
        base.OnActive();
        if (_start <= 0f) _start = Time.time;
    }

    protected override void Tick(float t)
    {
        base.Tick(t);
        if (_progress == null || _start <= 0f) return;
        AttackData d = Info.Data;
        float total = d != null && d.LandRemainTime > 0f ? d.LandRemainTime : 4f;
        float k = Mathf.Clamp01((Time.time - _start) / total);
        float len = Mathf.Max(0.01f, _barLen * k);
        K.SetSize(_progress, new Vector2(len, 1f) * K.Dot);
        Vector3 p = _progress.transform.localPosition;
        _progress.transform.localPosition = new Vector3((_barStart + len * 0.5f) * K.Dot, p.y, p.z);
    }
}

// ───────── 落下物：アルバムのジャケット（アプリの色の地に、黒いレコードが右へ半分のぞく）。着地でレコードが転がり出る ─────────

public class MediaFxJacket : GenericFxFalling
{
    protected override Color PanelColor => K.WithAlpha(MediaSkinArt.Dark, 0.35f);

    protected override void BuildContent(Transform body, int w, int h)
    {
        int jacket = h - 2;
        float jx = -w * 0.5f + 1f + jacket * 0.5f;
        // レコードは、ジャケットの後ろから右へのぞく
        MediaSkinArt.Record(body, new Vector2(jx + jacket * 0.45f, 0f) * K.Dot, jacket - 4, Color.Lerp(Main, Color.white, 0.3f), K.OrderWindow + 1);
        K.Spr(body, K.RoundRect(jacket, jacket, 1), new Vector2(jx, 0f) * K.Dot, Main, K.OrderWindow + 3, 1f, "Jacket");
        // ジャケットの絵（大きい円と線）
        K.Spr(body, K.Ring(jacket - 12, 2), new Vector2(jx, 2f) * K.Dot, K.WithAlpha(Color.white, 0.55f), K.OrderWindow + 4, 1f, "Art");
        K.BoxPx(body, jx - jacket * 0.2f, -jacket * 0.5f + 5f, jacket * 0.45f, 2f, Color.white, K.OrderWindow + 4, "Title");
    }

    protected override void LandedBurst(Vector2 at)
    {
        // レコードが転がり出て消え、音符が飛ぶ
        Transform rec = K.Loose("Gen4_MediaRecordRoll", at + new Vector2(0.3f, -Size.y * 0.5f + 0.5f));
        MediaSkinArt.Record(rec, Vector2.zero, 16, Main, K.OrderWindow + 2);
        var tw = GenericFrameTweens.Of(rec.gameObject).Kill(0.5f);
        tw.Move(rec, rec.position, rec.position + Vector3.right * 2.5f, 0.5f).Rotate(rec, 0f, -720f, 0.5f);
        foreach (var sr in rec.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.15f, 0.35f);
        K.Shards(at, Vector2.up, 6, i => MediaSkinArt.NoteSmall, i => i % 2 == 0 ? Main : Color.white, 70f, 1.2f);
    }
}

// ───────── 上の列：プレイリストの行（左に小さい四角、右に棒）。着地で、音符 ─────────

public class MediaFxRow : GenericFxTopRow
{
    protected override GenericPlateLook Look => GenericPlateLook.Panel; // アプリの中身（プレイリストの行）なので、今の板のまま（2026-11-19）
    protected override Color PanelColor => MediaSkinArt.Dark;

    protected override void BuildContent(Transform body, int w, int h)
    {
        int art = Mathf.Clamp(h - 4, 4, 9);
        K.Spr(body, K.RoundRect(art, art, 0), new Vector2(-w * 0.5f + 2f + art * 0.5f, 0f) * K.Dot, Main, K.OrderWindow + 1, 1f, "Art");
        float x0 = -w * 0.5f + 4f + art, len = w - 7f - art;
        K.BoxPx(body, x0 + len * 0.4f, 1.5f, len * 0.8f, 1f, Color.white, K.OrderWindow + 1, "Song");
        K.BoxPx(body, x0 + len * 0.25f, -1.5f, len * 0.5f, 1f, K.Grey, K.OrderWindow + 1, "Artist");
    }

    protected override void BuildBand(Transform root, float spacing)
    {
        K.Box(root, new Vector2(0f, Size.y * 0.5f + 0.22f), new Vector2(spacing * 0.9f, 0.28f), K.WithAlpha(Color.white, 0.18f), K.OrderTelegraph, "Playlist");
    }

    protected override void LandedBurst(Vector2 at)
    {
        K.Shards(at, Vector2.up, 5, i => MediaSkinArt.NoteSmall, i => i % 2 == 0 ? Main : Color.white, 80f);
    }
}
