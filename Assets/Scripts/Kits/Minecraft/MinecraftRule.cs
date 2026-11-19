using UnityEngine;

/// <summary>
/// 【仮】Minecraft のステージの決まり「昼と夜」（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// ・空を四角い太陽と月が横切る（時計の代わり。MinecraftSky）。昼 20秒 → 夜 10秒をくり返す。変わる 2秒前から、空の色が変わり始める
/// ・夜：画面が青く暗くなる（40％）。雑魚の移動 ×1.3、ボスの待ち時間 ×0.85（HUD に月の印）
///   暗くする板は、背景・床・足場より手前、雑魚・プレイヤー・危ない物・予告より奥の並び順（10）に置く（明るいまま見える）
/// ・昼：元どおり。ボス戦も続く。100％で全部外れる
/// ・ボスが強くなるギミック（【要相談②】のかっこ内：入れる。上限は 1-2。予告は変えない）
/// </summary>
public class MinecraftRule : StageRule
{
    const float Day = 20f;
    const float Night = 10f;
    const float Fade = 2f;
    const float Darkness = 0.4f;
    const int OverlayOrder = 10;

    MinecraftSky _sky;
    SpriteRenderer _overlay;
    float _cycleStart;
    bool _nightOn;
    bool _duskCued; // 夕方の音を鳴らしたか（1日に1回）

    string Key => Ctx.Key("night");

    public override void Begin(StageRuleContext ctx)
    {
        var go = new GameObject("MinecraftSky");
        _sky = go.AddComponent<MinecraftSky>();
        ctx.TrackDecor(go);
        Transform cam = FxKit.CameraChild("MinecraftNight");
        if (cam != null)
        {
            Vector2 view = FxKit.ViewSize();
            _overlay = AttackRunner.AddVisual(cam, PlaceholderSprites.Square, new Color(MinecraftArt.Night.r, MinecraftArt.Night.g, MinecraftArt.Night.b, 0f), view + Vector2.one * 4f, OverlayOrder, "Dark");
            ctx.TrackDecor(cam.gameObject);
        }
        _cycleStart = ctx.Elapsed;
    }

    public override void Tick(float dt)
    {
        float t = (Ctx.Elapsed - _cycleStart) % (Day + Night);
        bool night = t >= Day;
        // 暗さ：昼の終わり 2秒で暗く、夜の終わり 2秒で明るく
        float amount;
        if (!night) amount = t < Day - Fade ? 0f : (t - (Day - Fade)) / Fade;
        else amount = t < Day + Night - Fade ? 1f : 1f - (t - (Day + Night - Fade)) / Fade;
        // 場面③の出来事（夜の襲撃）の間は夜に固定（2秒で暗く・終わったら 2秒で時計の暗さへ戻す）
        _hold = Mathf.MoveTowards(_hold, _holdNight ? 1f : 0f, dt / Fade);
        bool held = _hold >= 0.5f && !night;
        if (_hold > 0f) amount = Mathf.Max(amount, _hold);
        if (held) night = true;
        // 夜の2秒前（暗くなり始め）に1回だけ鳴らす
        if (!night && t < Day - Fade) _duskCued = false;
        else if (!night && !_duskCued)
        {
            _duskCued = true;
            GameAudio.Event("gm_minecraft_dusk");
        }
        if (_overlay != null)
        {
            Color c = _overlay.color;
            c.a = Darkness * Mathf.Clamp01(amount);
            _overlay.color = c;
        }
        if (_sky != null)
        {
            _sky.IsNight = night;
            _sky.Progress = held ? 0.5f : night ? (t - Day) / Night : t / Day; // 固定の夜は、月を空のまん中に
        }
        if (night != _nightOn) SetNight(night);
    }

    bool _holdNight;
    float _hold; // 0〜1（固定の夜の濃さ）

    /// <summary>
    /// 【仮】夜に固定する・戻す（2026-11-19：場面③の出来事 MinecraftNightRaidEvent の間だけ。docs/stage-gimmick-variety-c.md 3章）。
    /// 固定の間は、ふつうの夜と同じ（暗い・雑魚の移動 ×1.3・ボスの待ち時間 ×0.85）。戻すと、今の時計の昼と夜へ
    /// </summary>
    public void HoldNight(bool on) => _holdNight = on;

    void SetNight(bool on)
    {
        _nightOn = on;
        if (on)
        {
            Sprite icon = MinecraftArt.MoonIcon;
            StatMods.Zako.Speed.Set(Key, 1.3f, icon);
            StatMods.Boss.Wait.Set(Key, 0.85f, icon);
            GameAudio.Event("gm_minecraft_night");
        }
        else
        {
            StatMods.Clear(Key);
            _duskCued = false;
            GameAudio.Event("gm_minecraft_dawn");
        }
    }

    public override void End()
    {
        StatMods.Clear(Key);
        _nightOn = false;
        _duskCued = false;
        _holdNight = false;
        _hold = 0f;
        if (_sky != null) Object.Destroy(_sky.gameObject);
        if (_overlay != null) Object.Destroy(_overlay.transform.parent.gameObject);
        _sky = null;
        _overlay = null;
    }
}
