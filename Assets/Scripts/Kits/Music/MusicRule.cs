using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックのステージの決まり：ビート（2026-11-19。docs/app-kits-2-design.md 4-1）。
/// 0.6秒ごとのビート（音は読まない。決まった秒数）。画面の下の棒がはね、プレイヤーの足元の輪が縮んでビートの瞬間に重なる。
/// ・ビートの前後 0.12秒は、与えるゲージ ×1.5（StatMods をビートの窓の間だけ入れる）
/// ・その窓の中で近接を当てると Perfect（ピンクの字）。雑魚を倒すと音符の輪が広がり、となりの雑魚（1.5マス）も倒す
/// ・外しても何も起きない（得だけ）。ボス戦も同じ。危ない物は出さない
/// </summary>
public class MusicRule : StageRule
{
    /// <summary>【仮】ビートの間と、窓の半分の幅（秒）</summary>
    public const float Beat = 0.6f, Window = 0.12f;
    /// <summary>【仮】窓の中の与えるゲージの倍率・となりを倒す半径</summary>
    public const float PerfectGauge = 1.5f, ChainRadius = 1.5f;
    const string Key = "music.beat";

    float _t0;
    bool _inWindow;
    bool _hooked;
    MusicBeatView _view;
    readonly HashSet<Health> _chained = new HashSet<Health>();

    /// <summary>今のビートの位置（0〜1。0 がビートの瞬間）</summary>
    public static float Phase01 { get; private set; }

    /// <summary>【仮】音符を取ったあとの広い窓の半分の幅（秒。2026-11-19：場面③の出来事 MusicBeatWaveEvent。docs/stage-gimmick-variety-c.md 8章）</summary>
    public const float WideWindow = 0.2f;
    static float _wideUntil = -1f;

    /// <summary>今の窓の半分の幅（ふだんは Window。WidenWindow の間は WideWindow）</summary>
    public static float CurrentWindow => Time.time < _wideUntil ? WideWindow : Window;

    /// <summary>【仮】seconds 秒、Perfect の窓を広げる（重ねたら長い方）</summary>
    public static void WidenWindow(float seconds) => _wideUntil = Mathf.Max(_wideUntil, Time.time + Mathf.Max(0f, seconds));

    /// <summary>
    /// ビートの時計（秒）。ミュージックのステージの曲が流れていれば、その再生位置（音とビートがずれない。2026-11-19：音）。
    /// 曲が無いとき・流れていないときは、今までどおり Time.time から数える
    /// </summary>
    public static float Clock(float t0) => GameAudio.TryGetBgmTime(GameAudio.MusicStageBgm, out float s) ? s : Time.time - t0;

    public override void Begin(StageRuleContext ctx)
    {
        _t0 = Time.time;
        _wideUntil = -1f;
        GameAudio.PlayBgm(GameAudio.MusicStageBgm, 0f, true); // 曲の頭をビートの始まりに合わせる
        Hitbox.AnyHit += OnAnyHit;
        _hooked = true;
        var go = new GameObject("MusicBeat");
        _view = go.AddComponent<MusicBeatView>();
        _view.Setup(ctx.Player, _t0);
        ctx.TrackDecor(go);
    }

    public override void Tick(float dt)
    {
        float t = Clock(_t0);
        float ph = Mathf.Repeat(t, Beat);
        Phase01 = ph / Beat;
        float win = CurrentWindow;
        bool inWin = ph <= win || Beat - ph <= win;
        if (inWin == _inWindow) return;
        _inWindow = inWin;
        if (inWin) StatMods.Player.GaugeGain.Set(Key, PerfectGauge);
        else StatMods.Player.GaugeGain.Clear(Key);
    }

    public override void End()
    {
        if (_hooked)
        {
            Hitbox.AnyHit -= OnAnyHit;
            _hooked = false;
        }
        StatMods.ClearPrefix("music.");
    }

    bool InWindow()
    {
        float ph = Mathf.Repeat(Clock(_t0), Beat);
        float win = CurrentWindow;
        return ph <= win || Beat - ph <= win;
    }

    void OnAnyHit(Hitbox hb, IDamageable target, DamageInfo info, bool first)
    {
        if (hb == null || hb.Team != Team.Player || !first) return;
        if (hb.GetComponentInParent<PlayerMeleeAttack>() == null) return; // 近接だけ
        if (!InWindow()) return;
        Vector2 at = info.HitPoint;
        FxKit.Text("Perfect", at + Vector2.up * 0.9f, 0.45f, (Color)MusicArt.Pink, FxKit.OrderToast - 1, 0.6f, Color.black);
        GameAudio.Event("gm_music_perfect");
        if (_view != null) _view.Flash();
        // 倒した雑魚から、音符の輪がとなりへ
        var h = target as Health;
        if (h == null || !h.IsDead || !RewardKit.IsZako(h) || _chained.Contains(h)) return;
        Vector2 c = HitFx.CenterOf(h.gameObject, h.transform.position);
        FxKit.Ring(c, ChainRadius * 2f, 0.25f, (Color)MusicArt.Pink);
        GameAudio.Event("gm_music_chain");
        FxAnim.Sprite(MusicArt.Note(0), c + Vector2.up * 0.6f, FxKit.OrderToast - 2, 0.4f, (Color)MusicArt.Pink);
        foreach (Health z in RewardKit.ZakoInRadius(c, ChainRadius))
        {
            if (z == h) continue;
            _chained.Add(z);
            RewardKit.Strike(z, z.GetComponent<ZakoElite>() != null ? 1 : 99, c, hb.gameObject, Ctx.Skin, HitStrength.Light, Ctx.Theme, Ctx.Icon); // 強い雑魚は 1 だけ
        }
        if (_chained.Count > 64) _chained.Clear();
    }
}

/// <summary>ビートの見た目（画面の下の棒・足元の縮む輪）。飾り。MusicRule が置く</summary>
public class MusicBeatView : MonoBehaviour
{
    const int Bars = 16;
    Transform _player;
    float _t0;
    Transform _bars;
    SpriteRenderer[] _bar;
    float[] _barH;
    SpriteRenderer _ring;
    int _lastBeat = -1;
    float _flashUntil;

    public void Setup(Transform player, float t0)
    {
        _player = player;
        _t0 = t0;
        _bars = FxKit.CameraChild("MusicBeatBars");
        if (_bars != null)
        {
            _bar = new SpriteRenderer[Bars];
            _barH = new float[Bars];
            Vector2 view = FxKit.ViewSize();
            float w = view.x / Bars;
            for (int i = 0; i < Bars; i++)
            {
                _bar[i] = FxKit.Box(_bars, new Vector2(-view.x * 0.5f + w * (i + 0.5f), -view.y * 0.5f), new Vector2(w - 0.15f, 1f), MusicArt.BarColor(i / (float)Bars), 2, "Bar" + i);
                _barH[i] = Random.Range(0.4f, 1.6f);
            }
        }
        var ring = new GameObject("BeatRing");
        ring.transform.SetParent(transform, false);
        _ring = RuntimeSprites.Show(ring.transform, MusicArt.Ring(24), Vector2.zero, 14, 1f, "Ring");
        _ring.color = new Color(1f, 0.36f, 0.45f, 0.8f);
    }

    public void Flash() => _flashUntil = Time.time + 0.15f;

    void OnDestroy()
    {
        if (_bars != null) Destroy(_bars.gameObject);
    }

    void LateUpdate()
    {
        float t = MusicRule.Clock(_t0);
        int beat = Mathf.FloorToInt(t / MusicRule.Beat);
        float ph = Mathf.Repeat(t, MusicRule.Beat) / MusicRule.Beat; // 0 がビートの瞬間
        if (beat != _lastBeat)
        {
            _lastBeat = beat;
            if (_barH != null) for (int i = 0; i < _barH.Length; i++) _barH[i] = Random.Range(0.4f, 1.8f);
        }
        if (_bar != null)
        {
            Vector2 view = FxKit.ViewSize();
            float k = 1f - ph;
            for (int i = 0; i < _bar.Length; i++)
            {
                float h = Mathf.Max(0.12f, _barH[i] * (0.35f + 0.65f * k * k));
                Transform tr = _bar[i].transform;
                Vector3 s = tr.localScale;
                tr.localScale = new Vector3(s.x, h, 1f);
                tr.localPosition = new Vector3(tr.localPosition.x, -view.y * 0.5f + h * 0.5f, 0f);
                _bar[i].color = new Color(_bar[i].color.r, _bar[i].color.g, _bar[i].color.b, 0.22f);
            }
        }
        if (_ring != null)
        {
            if (_player == null || !_player.gameObject.activeInHierarchy) { _ring.enabled = false; return; }
            _ring.enabled = true;
            // 次のビートへ向かって縮む（ビートの瞬間に足元の大きさ）
            float toNext = 1f - ph;
            float scale = 0.55f + 0.9f * toNext;
            _ring.transform.parent.position = _player.position + new Vector3(0f, 0.5f, 0f);
            _ring.transform.localScale = new Vector3(scale, scale * 0.45f, 1f);
            float win = MusicRule.CurrentWindow / MusicRule.Beat; // ふだんは 0.12秒（音符を取ったあとは広い窓）
            bool hit = ph < win || ph > 1f - win;
            Color c = Time.time < _flashUntil ? Color.white : hit ? new Color(1f, 0.5f, 0.6f, 0.95f) : new Color(1f, 0.36f, 0.45f, 0.55f);
            _ring.color = c;
        }
    }
}
