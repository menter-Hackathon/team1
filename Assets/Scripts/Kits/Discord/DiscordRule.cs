using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Discord のステージのギミック「ボイスチャンネル」（2026-11-19。docs/app-kits-1-design.md 3章）。StageRule。
/// ・画面の足場（すり抜け床）が「VC」。足場の上に、スピーカーの印と細い緑の線
/// ・プレイヤーが VC の上に立っている間：プレイヤーに緑のふち（通話中）。能力のクールタイムが 1.5倍の速さで進む（E2。HUD に印）
/// ・12秒おき（ボス戦は 16秒おき）に、どれかの VC が「切断」：足場が赤く 1.5秒点滅（予告）→ 足場の上だけに判定 0.5秒（高さ 2）→
///   足場は 3秒うすくなる（乗れるが、うまみは無い）。床はいつも安全（0-3：危ないのは足場1つぶんだけ）
/// ・危ない物は ctx.CanSpawnHazard のときだけ出す（ボスの場に残る技・技の途中・形態変化のあとは待つ）
/// 2026-11-19（遊んだ感想「マイクマークの意味がわからない」）：文字なしで分かるように
/// ・VC の足場：うすい緑の光の帯＋スピーカーの横で音の波（弧3本）が順に光る（Discord の通話の見た目）
/// ・プレイヤーが VC の上にいる間：プレイヤーに、ふくらむ緑の輪（Discord の「話している」印）と、左右に音の波。
///   入った瞬間に、足場とプレイヤーに緑の輪が広がる。HUD の能力の枠に、緑の上向きの矢印（PixelHud。クールタイムが速い間）
/// </summary>
public class DiscordRule : StageRule
{
    // 【仮】数値
    const float Interval = 12f, BossInterval = 16f;
    const float Telegraph = 1.5f, Active = 0.5f, HitHeight = 2f, Dim = 3f;
    const float CooldownRate = 1.5f;
    const float RetryDelay = 1f;
    const int DecorOrder = 14, MarkOrder = 15, FrameOrder = 31;
    const int GlowOrder = 3; // 足場の絵（2）の上、キャラクターより後ろ

    class Vc
    {
        public Bounds Box;
        public GameObject Decor;
        public SpriteRenderer Line;
        public SpriteRenderer Speaker;
        public SpriteRenderer Glow;
        public SpriteRenderer[] Arcs;
        public float DimUntil;
        public float BusyUntil; // 切断の予告・判定の間
    }

    readonly List<Vc> _vcs = new List<Vc>();
    float _timer;
    bool _inVc;
    float _leftVcAt = -10f; // ボイスチャンネルから最後に離れた時刻（入る音の間をあける）
    GameObject _frameGo;
    SpriteRenderer _frame;
    SpriteRenderer[] _talkArcs; // プレイヤーの左右の音の波（右3本・左3本）
    AttackData _hitData;
    Sprite _icon;
    Rect _scannedArea;

    string Key => Ctx.Key("vc");

    public override void Begin(StageRuleContext ctx)
    {
        _hitData = CallKitUtil.HazardData("Attack_Discord_Disconnect");
        _icon = ctx.Icon;
        _timer = Interval;
        Scan();
    }

    public override void OnBossAppeared(BossController boss)
    {
        _timer = BossInterval;
        Scan();
    }

    public override void Tick(float dt)
    {
        // 画面が変わったら（スクロールのステージ・ボス部屋）、足場を探し直す
        Rect area = Ctx.PlayArea;
        if (Mathf.Abs(area.center.x - _scannedArea.center.x) > 2f || Mathf.Abs(area.center.y - _scannedArea.center.y) > 2f) Scan();

        UpdateVoice();
        UpdateDecor();

        _timer -= dt;
        if (_timer > 0f) return;
        if (!Ctx.CanSpawnHazard || _vcs.Count == 0)
        {
            _timer = RetryDelay;
            return;
        }
        Disconnect(PickVc());
        _timer = Ctx.InBossFight ? BossInterval : Interval;
    }

    public override void OnBossPhase(int phase) => _timer = Mathf.Max(_timer, 4f);

    public override void End()
    {
        StatMods.Player.CooldownRate.Clear(Key);
        foreach (var v in _vcs) if (v.Decor != null) Object.Destroy(v.Decor);
        _vcs.Clear();
        if (_frameGo != null) Object.Destroy(_frameGo);
        _frameGo = null;
        if (_hitData != null) Object.Destroy(_hitData);
        _hitData = null;
    }

    // ───────── 足場 ─────────

    void Scan()
    {
        foreach (var v in _vcs) if (v.Decor != null) Object.Destroy(v.Decor);
        _vcs.Clear();
        _scannedArea = Ctx.PlayArea;
        List<Bounds> found = CallKitUtil.FindPlatforms(_scannedArea);
        // 多すぎるとき（スクロールの画面）は、画面のまん中に近い 3つまで
        found.Sort((a, b) => Mathf.Abs(a.center.x - _scannedArea.center.x).CompareTo(Mathf.Abs(b.center.x - _scannedArea.center.x)));
        if (found.Count > 3) found.RemoveRange(3, found.Count - 3);
        foreach (Bounds b in found)
        {
            var v = new Vc { Box = b };
            var go = new GameObject("Discord_VC");
            go.transform.position = new Vector3(b.center.x, b.max.y, 0f);
            v.Decor = Ctx.TrackDecor(go);
            v.Line = DiscordArt.Put(go.transform, PlaceholderSprites.Square, new Vector2(0f, CallArt.Px), new Vector2(b.size.x, 2f * CallArt.Px), DiscordArt.Green, DecorOrder, "Line");
            v.Speaker = RuntimeSprites.Show(go.transform, DiscordArt.Speaker, new Vector2(-b.size.x * 0.5f + 0.4f, 0.45f), MarkOrder, 1f, "Speaker");
            // うすい緑の光の帯（足場の上）と、スピーカーの横の音の波
            v.Glow = DiscordArt.Put(go.transform, GlowSprite, new Vector2(0f, 0.35f), new Vector2(b.size.x, 0.7f), DiscordArt.Green, GlowOrder, "Glow");
            v.Arcs = MakeArcs(go.transform, new Vector2(-b.size.x * 0.5f + 1.05f, 0.45f), 0.6f, false, MarkOrder);
            _vcs.Add(v);
        }
    }

    Vc PickVc()
    {
        // プレイヤーが乗っている VC があれば、それを少し選びやすく
        var candidates = new List<Vc>();
        foreach (var v in _vcs) if (Time.time >= v.BusyUntil && Time.time >= v.DimUntil) candidates.Add(v);
        if (candidates.Count == 0) return null;
        if (CallKitUtil.TryPlayerBounds(out Bounds p) && Random.value < 0.6f)
        {
            Vc nearest = null;
            float best = float.MaxValue;
            foreach (var v in candidates)
            {
                float d = Mathf.Abs(v.Box.center.x - p.center.x);
                if (d < best) { best = d; nearest = v; }
            }
            if (nearest != null) return nearest;
        }
        return candidates[Random.Range(0, candidates.Count)];
    }

    // ───────── 通話中 ─────────

    void UpdateVoice()
    {
        bool on = false;
        Vc standing = null;
        Bounds p = default;
        PlayerController pc = CallKitUtil.Player;
        if (pc != null && CallKitUtil.TryBodyBounds(pc.transform, out p))
        {
            foreach (var v in _vcs)
            {
                if (Time.time < v.DimUntil) continue;
                if (CallKitUtil.StandingOn(v.Box, p, pc.IsGrounded)) { on = true; standing = v; break; }
            }
            if (on) ShowFrame(p);
        }
        if (on == _inVc) { if (!on) HideFrame(); return; }
        _inVc = on;
        if (on)
        {
            StatMods.Player.CooldownRate.Set(Key, CooldownRate, _icon);
            // 足場の上でジャンプすると出入りをくり返すので、1秒より長く離れていたときだけ鳴らす（入った印の緑の輪も）
            if (Time.time - _leftVcAt > 1f)
            {
                GameAudio.Event("gm_discord_vc_join");
                if (standing != null) FxKit.Ring(new Vector2(standing.Box.center.x, standing.Box.max.y), standing.Box.size.x + 0.6f, 0.4f, DiscordArt.Green);
                FxKit.Ring(p.center, Mathf.Max(p.size.x, p.size.y) + 1.2f, 0.35f, DiscordArt.Green);
            }
        }
        else { StatMods.Player.CooldownRate.Clear(Key); _leftVcAt = Time.time; }
        if (!on) HideFrame();
    }

    void HideFrame()
    {
        if (_frame != null) _frame.enabled = false;
        if (_talkArcs != null) foreach (var a in _talkArcs) if (a != null) a.enabled = false;
    }

    // 話している印：プレイヤーのまわりで、ふくらんだり戻ったりする緑の輪（Discord の話している人のふち）と、左右の音の波
    void ShowFrame(Bounds p)
    {
        float d = Mathf.Max(p.size.x, p.size.y) + 0.55f; // 輪の直径（マス）
        if (_frameGo == null)
        {
            _frameGo = Ctx.TrackDecor(new GameObject("Discord_Speaking"));
            int dp = Mathf.Clamp(Mathf.RoundToInt(d * CallArt.Ppu), 12, 64);
            _frame = DiscordArt.Put(_frameGo.transform, CallArt.Ring(dp, 2), Vector2.zero, new Vector2(d, d), DiscordArt.Green, FrameOrder, "Ring");
            var right = MakeArcs(_frameGo.transform, new Vector2(d * 0.5f + 0.35f, 0.15f), 0.65f, false, FrameOrder);
            var left = MakeArcs(_frameGo.transform, new Vector2(-d * 0.5f - 0.35f, 0.15f), 0.65f, true, FrameOrder);
            _talkArcs = new SpriteRenderer[right.Length + left.Length];
            right.CopyTo(_talkArcs, 0);
            left.CopyTo(_talkArcs, right.Length);
        }
        _frameGo.transform.position = p.center;
        if (_frame != null)
        {
            _frame.enabled = true;
            float k = Mathf.Abs(Mathf.Sin(Time.time * 6f));
            _frame.color = CallKitUtil.Alpha(DiscordArt.Green, 0.75f + 0.25f * k);
            float s = 1f + 0.12f * k;
            _frame.transform.localScale = Vector3.one; // 下の大きさの元に戻してから掛ける
            Vector2 native = _frame.sprite.bounds.size;
            _frame.transform.localScale = new Vector3(d * s / Mathf.Max(0.01f, native.x), d * s / Mathf.Max(0.01f, native.y), 1f);
        }
        if (_talkArcs != null) AnimateArcs(_talkArcs, DiscordArt.Green, 1f, 2.4f);
    }

    void UpdateDecor()
    {
        foreach (var v in _vcs)
        {
            bool dim = Time.time < v.DimUntil;
            bool busy = Time.time < v.BusyUntil;
            Color c = busy ? (Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0 ? DiscordArt.Red : CallKitUtil.Alpha(DiscordArt.Red, 0.3f))
                : dim ? CallKitUtil.Alpha(DiscordArt.Green, 0.25f) : DiscordArt.Green;
            if (v.Line != null) v.Line.color = c;
            if (v.Speaker != null) v.Speaker.color = busy ? DiscordArt.Red : dim ? CallKitUtil.Alpha(Color.white, 0.3f) : Color.white;
            // 光の帯：ふだんはゆっくり明るさがゆれる緑。切断の予告は赤、切れたあとはうすい
            if (v.Glow != null)
            {
                float a = busy ? 0.75f : dim ? 0.15f : 0.55f + 0.25f * Mathf.Sin(Time.time * 3f);
                v.Glow.color = CallKitUtil.Alpha(busy ? DiscordArt.Red : DiscordArt.Green, a);
            }
            if (v.Arcs != null) AnimateArcs(v.Arcs, busy ? DiscordArt.Red : DiscordArt.Green, dim ? 0.2f : 1f, busy ? 4f : 1.6f);
        }
    }

    // ───────── 音の波（絵） ─────────

    // 下から上へうすくなる光（白。色は sr.color）
    static Sprite GlowSprite => RuntimeSprites.FromAscii("Discord_VcGlow", new[] { "4", "4", "3", "3", "2", "2", "1", "1" },
        new Dictionary<char, Color32>
        {
            { '1', new Color32(255, 255, 255, 150) }, { '2', new Color32(255, 255, 255, 95) },
            { '3', new Color32(255, 255, 255, 50) }, { '4', new Color32(255, 255, 255, 20) },
        });

    static readonly int[][] ArcColumns = { new[] { 6, 10 }, new[] { 13, 17 }, new[] { 19, 21 } };

    // DiscordArt.Wave（弧3本）を、弧1本ずつの絵に分けたもの（白。同じ大きさなので、同じ場所に重ねると元の絵になる）
    static Sprite ArcSprite(int i)
    {
        string[] rows =
        {
            "......GG.....GG....GG.",
            ".......GG.....GG....GG",
            "........GL.....GL....G",
            "........GL.....GL....G",
            ".........GL.....GL...G",
            ".........GL.....GL...G",
            ".........GL.....GL...G",
            ".........GL.....GL...G",
            ".........GL.....GL...G",
            ".........GL.....GL...G",
            "........GL.....GL....G",
            "........GL.....GL....G",
            ".......GG.....GG....GG",
            "......GG.....GG....GG.",
        };
        int from = ArcColumns[i][0], to = ArcColumns[i][1];
        var only = new string[rows.Length];
        for (int y = 0; y < rows.Length; y++)
        {
            var line = rows[y].ToCharArray();
            for (int x = 0; x < line.Length; x++) if (x < from || x > to) line[x] = '.';
            only[y] = new string(line);
        }
        return RuntimeSprites.FromAscii($"Discord_Arc{i}", only, new Dictionary<char, Color32>
        {
            { 'G', new Color32(255, 255, 255, 255) }, { 'L', new Color32(255, 255, 255, 170) },
        });
    }

    // 弧3本を置く（flip で左向き）。scale は元の絵（22×14 ドット）に掛ける
    static SpriteRenderer[] MakeArcs(Transform parent, Vector2 localPos, float scale, bool flip, int order)
    {
        var arcs = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            arcs[i] = RuntimeSprites.Show(parent, ArcSprite(i), localPos, order, scale, "Arc");
            arcs[i].flipX = flip;
        }
        return arcs;
    }

    // 弧を内側から順に光らせる（音が広がって見える）。左右の組は 3本ずつ
    static void AnimateArcs(SpriteRenderer[] arcs, Color color, float alpha, float speed)
    {
        float t = Time.time * speed;
        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] == null) continue;
            int k = i % 3;
            float ph = Mathf.Repeat(t - k * 0.22f, 1f);
            float a = ph < 0.55f ? 1f - ph / 0.55f * 0.7f : 0.15f;
            arcs[i].enabled = true;
            arcs[i].color = CallKitUtil.Alpha(color, a * alpha);
        }
    }

    // ───────── 切断 ─────────

    void Disconnect(Vc v)
    {
        if (v == null) return;
        AttackRunner runner = Ctx.Hazard;
        if (runner == null) return;
        v.BusyUntil = Time.time + Telegraph + Active;
        GameAudio.Event("gm_discord_disconnect_warn");
        v.DimUntil = v.BusyUntil + Dim;
        Vector2 size = new Vector2(v.Box.size.x, HitHeight);
        Vector2 center = new Vector2(v.Box.center.x, v.Box.max.y + HitHeight * 0.5f);
        // 予告：足場の上の赤い帯（点滅）
        GameObject mark = runner.CreateSpawnedObject("Discord_DisconnectMark", new Vector2(center.x, v.Box.max.y));
        Ctx.TrackHazard(mark);
        mark.AddComponent<DiscordDisconnectMark>().Setup(v.Box.size.x, HitHeight, Telegraph, Active);
        CallTimedHit hit = CallKitUtil.TimedHit(runner, _hitData, center, size, false, Telegraph, Active, "Disconnect");
        if (hit != null) Ctx.TrackHazard(hit.gameObject);
    }
}

/// <summary>切断の予告（足場の上のうすい赤と赤い線。点滅）と判定の間の赤</summary>
public class DiscordDisconnectMark : MonoBehaviour
{
    const int Order = 16;
    SpriteRenderer _fill, _edge;
    float _born, _telegraph, _active;
    bool _cut; // 判定が出た音を1回だけ鳴らす

    public void Setup(float width, float height, float telegraph, float active)
    {
        _born = Time.time;
        _telegraph = telegraph;
        _active = active;
        _fill = FxKit.Box(transform, new Vector2(0f, height * 0.5f), new Vector2(width, height), CallKitUtil.Alpha(KitDress.Red, 0.2f), Order, "Fill");
        _edge = FxKit.Box(transform, new Vector2(0f, CallArt.Px), new Vector2(width, 2f * CallArt.Px), KitDress.Red, Order + 1, "Edge");
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t >= _telegraph + _active)
        {
            Destroy(gameObject);
            return;
        }
        if (t < _telegraph)
        {
            float left = _telegraph - t;
            bool on = left > 0.3f ? Mathf.FloorToInt(t / 0.15f) % 2 == 0 : Mathf.FloorToInt(t / 0.06f) % 2 == 0;
            if (_fill != null) _fill.color = CallKitUtil.Alpha(KitDress.Red, on ? 0.28f : 0.12f);
            if (_edge != null) _edge.enabled = on;
        }
        else
        {
            if (!_cut) { _cut = true; GameAudio.Event("gm_discord_disconnect_cut"); }
            if (_fill != null) _fill.color = CallKitUtil.Alpha(KitDress.Red, 0.5f);
            if (_edge != null) _edge.enabled = true;
        }
    }
}
