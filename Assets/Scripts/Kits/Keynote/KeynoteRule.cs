using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Keynote のステージのギミック「拍手」（2026-11-19。docs/app-kits-1-design.md 13章）。数値はすべて【仮】。
/// ・画面の下の端に、観客の頭の影（飾り）。HUD のライフの下に細いメーター（KitMeter）
/// ・攻撃を当てるたびにメーターが増える（1回 1/10）。2.5秒当てないと、減り始める
/// ・満タンで「スタンディングオベーション」：観客の手が上がり、紙吹雪。6秒、与えるゲージ ×1.5（StatMods。HUD に印）。
///   終わるとメーターは 0 に戻る。ダメージを受けても減らない
/// </summary>
public class KeynoteRule : StageRule
{
    public const float PerHit = 0.1f;
    public const float Idle = 2.5f;
    public const float DecayPerSecond = 0.15f;
    public const float OvationSeconds = 6f;
    public const float OvationRate = 1.5f;
    const int HeadCount = 16;

    float _meter;
    float _lastHit = -99f;
    float _ovationUntil = -1f;
    float _nextConfetti;
    bool _hooked;
    string _key;
    Transform _crowd;
    readonly List<SpriteRenderer> _heads = new List<SpriteRenderer>();

    /// <summary>メーター（0〜1。テスト用）</summary>
    public float Meter => _meter;
    /// <summary>スタンディングオベーションの間か</summary>
    public bool InOvation => _ovationUntil > Time.time;

    public override void Begin(StageRuleContext ctx)
    {
        _key = ctx.Key("applause");
        Hitbox.AnyHit += OnAnyHit;
        _hooked = true;
        // 観客（カメラについていく飾り）
        var go = new GameObject("Keynote_Crowd");
        ctx.TrackDecor(go);
        _crowd = go.transform;
        Vector2 view = FxKit.ViewSize();
        for (int i = 0; i < HeadCount; i++)
        {
            SpriteRenderer sr = AttackRunner.AddVisual(_crowd, KeynoteArt.Audience(false), new Color(0f, 0f, 0f, 0.55f), new Vector2(14f / 18f, 12f / 18f) * 1.3f, 3, "Head");
            float x = -view.x * 0.5f + (i + 0.5f) * view.x / HeadCount + (i % 2 == 0 ? 0.1f : -0.1f);
            sr.transform.localPosition = new Vector3(x, -view.y * 0.5f + 0.42f + (i % 3) * 0.06f, 0f);
            _heads.Add(sr);
        }
        Place();
        Show();
    }

    public override void Tick(float dt)
    {
        Place();
        float now = Time.time;
        if (_ovationUntil > 0f)
        {
            // 拍手の間：メーターが減っていって、終わったら 0
            float left = _ovationUntil - now;
            _meter = Mathf.Clamp01(left / OvationSeconds);
            if (now >= _nextConfetti)
            {
                _nextConfetti = now + 0.08f;
                Confetti();
            }
            if (left <= 0f) EndOvation();
            Show();
            return;
        }
        if (now - _lastHit > Idle && _meter > 0f)
        {
            _meter = Mathf.Max(0f, _meter - DecayPerSecond * dt);
            Show();
        }
    }

    void OnAnyHit(Hitbox hitbox, IDamageable target, DamageInfo info, bool first)
    {
        if (hitbox == null || hitbox.Team != Team.Player || target == null || target.Team != Team.Enemy) return;
        if (Ctx == null || _ovationUntil > 0f) return;
        _lastHit = Time.time;
        _meter = Mathf.Min(1f, _meter + PerHit);
        if (_meter >= 0.999f) StartOvation();
        else GameAudio.Event("gm_keynote_clap", 0.9f + 0.4f * _meter);
        Show();
    }

    void StartOvation()
    {
        _ovationUntil = Time.time + OvationSeconds;
        GameAudio.Event("gm_keynote_ovation");
        StatMods.Player.GaugeGain.SetFor(_key, OvationRate, OvationSeconds, Ctx != null ? Ctx.Icon : null);
        foreach (var h in _heads) if (h != null) h.sprite = KeynoteArt.Audience(true);
        for (int i = 0; i < 24; i++) Confetti();
        FxKit.Ring(Ctx != null && Ctx.Player != null ? (Vector2)Ctx.Player.position + Vector2.up : Vector2.zero, 3f, 0.3f, KeynoteArt.Spot);
    }

    void EndOvation()
    {
        _ovationUntil = -1f;
        GameAudio.Event("gm_keynote_ovation_end");
        _meter = 0f;
        _lastHit = -99f;
        StatMods.Player.GaugeGain.Clear(_key);
        foreach (var h in _heads) if (h != null) h.sprite = KeynoteArt.Audience(false);
    }

    void Confetti()
    {
        if (_crowd == null) return;
        Vector2 view = FxKit.ViewSize();
        Vector2 at = (Vector2)_crowd.position + new Vector2(Random.Range(-view.x * 0.5f, view.x * 0.5f), -view.y * 0.5f + 0.6f);
        Color c = KeynoteArt.Confetti[Random.Range(0, KeynoteArt.Confetti.Length)];
        FxAnim.Sprite(FxArt.Square(Random.Range(2, 4)), at, 30, Random.Range(0.8f, 1.3f), c)
            .Velocity(new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(7f, 11f))).Gravity(9f).Spin(Random.Range(-400f, 400f)).FadeFrom(0.6f);
    }

    void Place()
    {
        Camera cam = Camera.main;
        if (_crowd != null && cam != null) _crowd.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        // 手が上がっている間は、少しはねる
        if (_ovationUntil > 0f)
            for (int i = 0; i < _heads.Count; i++)
                if (_heads[i] != null)
                {
                    Vector3 p = _heads[i].transform.localPosition;
                    Vector2 view = FxKit.ViewSize();
                    p.y = -view.y * 0.5f + 0.42f + (i % 3) * 0.06f + Mathf.Abs(Mathf.Sin(Time.time * 10f + i)) * 0.12f;
                    _heads[i].transform.localPosition = p;
                }
    }

    void Show()
    {
        if (_key == null) return;
        bool ovation = _ovationUntil > 0f;
        KitMeter.Set(_key, _meter, ovation ? KeynoteArt.Spot : KeynoteArt.Blue, Ctx != null ? Ctx.Icon : null);
    }

    public override void End()
    {
        if (_hooked)
        {
            Hitbox.AnyHit -= OnAnyHit;
            _hooked = false;
        }
        if (_key != null)
        {
            StatMods.Player.GaugeGain.Clear(_key);
            KitMeter.Clear(_key);
        }
        _ovationUntil = -1f;
        _meter = 0f;
    }
}
