using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Discord のステージのギミック「ボイスチャンネル」（2026-11-19。docs/app-kits-1-design.md 3章）。StageRule。
/// ・画面の足場（すり抜け床）が「VC」。足場の上に、スピーカーの印と細い緑の線
/// ・プレイヤーが VC の上に立っている間：プレイヤーに緑のふち（通話中）。能力のクールタイムが 1.5倍の速さで進む（E2。HUD に印）
/// ・12秒おき（ボス戦は 16秒おき）に、どれかの VC が「切断」：足場が赤く 1.5秒点滅（予告）→ 足場の上だけに判定 0.5秒（高さ 2）→
///   足場は 3秒うすくなる（乗れるが、うまみは無い）。床はいつも安全（0-3：危ないのは足場1つぶんだけ）
/// ・危ない物は ctx.CanSpawnHazard のときだけ出す（ボスの場に残る技・技の途中・形態変化のあとは待つ）
/// </summary>
public class DiscordRule : StageRule
{
    // 【仮】数値
    const float Interval = 12f, BossInterval = 16f;
    const float Telegraph = 1.5f, Active = 0.5f, HitHeight = 2f, Dim = 3f;
    const float CooldownRate = 1.5f;
    const float RetryDelay = 1f;
    const int DecorOrder = 14, MarkOrder = 15, FrameOrder = 31;

    class Vc
    {
        public Bounds Box;
        public GameObject Decor;
        public SpriteRenderer Line;
        public SpriteRenderer Speaker;
        public float DimUntil;
        public float BusyUntil; // 切断の予告・判定の間
    }

    readonly List<Vc> _vcs = new List<Vc>();
    float _timer;
    bool _inVc;
    float _leftVcAt = -10f; // ボイスチャンネルから最後に離れた時刻（入る音の間をあける）
    GameObject _frameGo;
    SpriteRenderer _frame;
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
        PlayerController pc = CallKitUtil.Player;
        if (pc != null && CallKitUtil.TryBodyBounds(pc.transform, out Bounds p))
        {
            foreach (var v in _vcs)
            {
                if (Time.time < v.DimUntil) continue;
                if (CallKitUtil.StandingOn(v.Box, p, pc.IsGrounded)) { on = true; break; }
            }
            if (on) ShowFrame(p);
        }
        if (on == _inVc) { if (!on && _frame != null) _frame.enabled = false; return; }
        _inVc = on;
        if (on)
        {
            StatMods.Player.CooldownRate.Set(Key, CooldownRate, _icon);
            // 足場の上でジャンプすると出入りをくり返すので、1秒より長く離れていたときだけ鳴らす
            if (Time.time - _leftVcAt > 1f) GameAudio.Event("gm_discord_vc_join");
        }
        else { StatMods.Player.CooldownRate.Clear(Key); _leftVcAt = Time.time; }
        if (!on && _frame != null) _frame.enabled = false;
    }

    void ShowFrame(Bounds p)
    {
        if (_frameGo == null)
        {
            _frameGo = Ctx.TrackDecor(new GameObject("Discord_Speaking"));
            Vector2 size = (Vector2)p.size + new Vector2(0.3f, 0.3f);
            int wp = Mathf.Clamp(Mathf.RoundToInt(size.x * CallArt.Ppu), 6, 80);
            int hp = Mathf.Clamp(Mathf.RoundToInt(size.y * CallArt.Ppu), 6, 80);
            _frame = DiscordArt.Put(_frameGo.transform, DiscordArt.SpeakingFrame(wp, hp), Vector2.zero, size, DiscordArt.Green, FrameOrder, "Frame");
        }
        _frameGo.transform.position = p.center;
        if (_frame != null)
        {
            _frame.enabled = true;
            _frame.color = CallKitUtil.Alpha(DiscordArt.Green, 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
        }
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
