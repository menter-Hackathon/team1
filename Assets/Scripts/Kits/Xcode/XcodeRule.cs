using UnityEngine;

/// <summary>
/// 【仮】Xcode のステージの決まり「Build」（2026-11-19。docs/app-kits-2-design.md 3-2）。
/// 上のまん中に Xcode の進みの表示（角丸の小さい帯）。3つをくり返す：
///   1. Indexing...（8秒。何も起きない）。雑魚がいない場面（③ 出来事・④ ひと休み・雑魚が0）は、ここで待つ
///   2. Building...（5秒。バーが進む。この間に雑魚を 3体倒す／ボス戦ではボスに 3回当てる。数は帯の右に丸 3つ）
///   3. 結果（6秒）。できた：緑の Build Succeeded・クールタイム ×0.7（待つ時間）。
///      できなかった：赤の Build Failed・8秒、雑魚の移動 ×1.25・ボスの待ち時間 ×0.8（罰でライフは減らさない）
/// </summary>
public class XcodeRule : StageRule
{
    const float IndexTime = 8f;
    const float BuildTime = 5f;
    const float ResultTime = 6f;
    const float FailTime = 8f;
    const int Need = 3;

    enum Step { Indexing, Building, Result }

    Step _step = Step.Indexing;
    float _t;
    int _count;
    bool _success;
    bool _hooked;
    XcodeBuildStrip _strip;
    float _nextZakoCheck;
    bool _zakoSeen;
    float _failEndAt = -1f; // Build Failed の倍率が切れる時刻（終わりの音用。StatMods と同じ Time.time）

    public override void Begin(StageRuleContext ctx)
    {
        Transform cam = FxKit.CameraChild("XcodeBuildStrip");
        if (cam != null)
        {
            _strip = cam.gameObject.AddComponent<XcodeBuildStrip>();
            ctx.TrackDecor(cam.gameObject);
        }
        Health.AnyDied += OnAnyDied;
        Hitbox.AnyHit += OnAnyHit;
        _hooked = true;
        Show();
    }

    public override void Tick(float dt)
    {
        if (_failEndAt >= 0f && Time.time >= _failEndAt)
        {
            _failEndAt = -1f;
            GameAudio.Event("gm_xcode_failed_end");
        }
        switch (_step)
        {
            case Step.Indexing:
                if (_t < IndexTime) _t += dt;
                if (_t >= IndexTime && CanBuild())
                {
                    Enter(Step.Building);
                    GameAudio.Event("gm_xcode_building");
                }
                break;
            case Step.Building:
                if (!Ctx.InBossFight && (Ctx.Phase == PacePhase.Event || Ctx.Phase == PacePhase.Rest)) break; // 雑魚が出ない間は止める
                _t += dt;
                if (_count >= Need) Finish(true);
                else if (_t >= BuildTime) Finish(false);
                break;
            case Step.Result:
                _t += dt;
                if (_t >= ResultTime) Enter(Step.Indexing);
                break;
        }
        Show();
    }

    public override void OnPhase(PacePhase phase)
    {
        // ボスの登場：作りかけの Build はやめて、Indexing からやり直す（結果の倍率はそのまま）
        if (phase == PacePhase.Done && _step == Step.Building) Enter(Step.Indexing);
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            Hitbox.AnyHit -= OnAnyHit;
        }
        _hooked = false;
        _failEndAt = -1f;
        StatMods.ClearPrefix("xcode.");
        if (_strip != null) Object.Destroy(_strip.gameObject);
        _strip = null;
    }

    void Enter(Step s)
    {
        _step = s;
        _t = 0f;
        _count = 0;
    }

    /// <summary>Building を始めてよいか：ボス戦（戦っている間）か、雑魚がいる場面</summary>
    bool CanBuild()
    {
        if (Ctx.InBossFight) return KitBoss.IsFighting(Ctx.Boss);
        if (Ctx.Phase == PacePhase.Event || Ctx.Phase == PacePhase.Rest || Ctx.Phase == PacePhase.Done) return false;
        if (Time.time >= _nextZakoCheck)
        {
            _nextZakoCheck = Time.time + 0.5f;
            Rect a = Ctx.PlayArea;
            _zakoSeen = RewardKit.ZakoInRadius(a.center, Mathf.Max(a.width, a.height)).Count > 0;
        }
        return _zakoSeen;
    }

    void Finish(bool ok)
    {
        _success = ok;
        Enter(Step.Result);
        Sprite icon = Ctx.Icon;
        if (ok)
        {
            StatMods.Player.CooldownRate.SetFor(Ctx.Key("build"), 1f / 0.7f, ResultTime, icon);
            KitToast.Show("Build Succeeded", null, XcodeArt.Green);
            GameAudio.Event("gm_xcode_succeeded");
        }
        else
        {
            StatMods.Zako.Speed.SetFor(Ctx.Key("failed"), 1.25f, FailTime, icon);
            StatMods.Boss.Wait.SetFor(Ctx.Key("failed"), 0.8f, FailTime, icon);
            KitToast.Show("Build Failed", null, XcodeArt.Red);
            GameAudio.Event("gm_xcode_failed");
            _failEndAt = Time.time + FailTime;
        }
    }

    void OnAnyDied(Health h)
    {
        if (_step != Step.Building || Ctx.InBossFight || h == null || !RewardKit.IsZako(h)) return;
        _count++;
        if (_count <= Need) GameAudio.Event("gm_xcode_count", 1f + 0.12f * (_count - 1));
    }

    void OnAnyHit(Hitbox hb, IDamageable target, DamageInfo info, bool first)
    {
        if (_step != Step.Building || !Ctx.InBossFight || info.SourceTeam != Team.Player) return;
        if (!(target is VirusGauge g) || Ctx.Boss == null || g != Ctx.Boss.Gauge) return;
        if (Time.time - _lastHitAt < 0.2f) return; // 同じ攻撃の続けての当たりは1回に数える
        _lastHitAt = Time.time;
        _count++;
        if (_count <= Need) GameAudio.Event("gm_xcode_count", 1f + 0.12f * (_count - 1));
    }

    float _lastHitAt = -1f;

    void Show()
    {
        if (_strip == null) return;
        switch (_step)
        {
            case Step.Indexing: _strip.Set("Indexing...", Mathf.Clamp01(_t / IndexTime), 0, false, XcodeBuildStrip.Mode.Busy); break;
            case Step.Building: _strip.Set("Building...", Mathf.Clamp01(_t / BuildTime), _count, true, XcodeBuildStrip.Mode.Busy); break;
            case Step.Result:
                _strip.Set(_success ? "Build Succeeded" : "Build Failed", 1f, _success ? Need : _count, false,
                    _success ? XcodeBuildStrip.Mode.Succeeded : XcodeBuildStrip.Mode.Failed);
                break;
        }
    }
}

/// <summary>【仮】Xcode の進みの表示（上のまん中の角丸の帯。2026-11-19）。カメラの子</summary>
public class XcodeBuildStrip : MonoBehaviour
{
    public enum Mode { Busy, Succeeded, Failed }

    const int Order = 60;
    const float W = 7.4f;
    const float H = 0.9f;

    TextMesh _text;
    SpriteRenderer _bar;
    SpriteRenderer _barBg;
    SpriteRenderer _icon;
    readonly SpriteRenderer[] _dots = new SpriteRenderer[3];
    Transform _root;
    string _last;

    void Start()
    {
        Vector2 view = FxKit.ViewSize();
        _root = new GameObject("Strip").transform;
        _root.gameObject.layer = gameObject.layer;
        _root.SetParent(transform, false);
        _root.localPosition = new Vector3(0f, view.y * 0.5f - 2.5f, 0f);
        Color bg = MacUi.Light1;
        // 角丸の帯（ふち・影）
        XcodeArt.Box(_root, new Vector2(0f, -2f * XcodeArt.Px), new Vector2(W, H), new Color(0f, 0f, 0f, 0.3f), Order - 1, "Shadow");
        XcodeArt.Box(_root, Vector2.zero, new Vector2(W, H), MacUi.Light4, Order, "Edge");
        XcodeArt.Box(_root, Vector2.zero, new Vector2(W - 2f * XcodeArt.Px, H - 2f * XcodeArt.Px), bg, Order + 1, "Bg");
        _icon = RuntimeSprites.Show(_root, XcodeArt.HammerIcon(), new Vector2(-W * 0.5f + 0.5f, 0.05f), Order + 2, 0.9f, "Icon");
        _text = XcodeArt.Text(_root, "", new Vector2(-W * 0.5f + 1.0f, 0.12f), 0.4f, MacUi.Gray6, Order + 2, TextAnchor.MiddleLeft);
        _barBg = XcodeArt.Box(_root, new Vector2(0.1f, -0.28f), new Vector2(W - 2.8f, 2f * XcodeArt.Px), MacUi.Light3, Order + 2, "BarBg");
        _bar = XcodeArt.Box(_root, new Vector2(0.1f, -0.28f), new Vector2(0f, 2f * XcodeArt.Px), XcodeArt.Blue, Order + 3, "Bar");
        for (int i = 0; i < 3; i++)
        {
            _dots[i] = RuntimeSprites.Show(_root, FxArt.Disc(7), new Vector2(W * 0.5f - 1.15f + i * 0.4f, 0f), Order + 2, 1f, "Dot");
        }
    }

    public void Set(string text, float progress, int count, bool showDots, Mode mode)
    {
        if (_text == null) return;
        if (_last != text)
        {
            _last = text;
            _text.text = text;
        }
        Color ink = mode == Mode.Succeeded ? XcodeArt.Green : mode == Mode.Failed ? XcodeArt.Red : MacUi.Gray6;
        WorldText.SetColor(_text, ink);
        float bw = (W - 2.8f) * Mathf.Clamp01(progress);
        XcodeArt.SetBox(_bar, new Vector2(0.1f - (W - 2.8f) * 0.5f + bw * 0.5f, -0.28f), new Vector2(bw, 2f * XcodeArt.Px));
        _bar.color = mode == Mode.Succeeded ? XcodeArt.Green : mode == Mode.Failed ? XcodeArt.Red : XcodeArt.Blue;
        _barBg.enabled = mode == Mode.Busy;
        _bar.enabled = mode == Mode.Busy;
        for (int i = 0; i < 3; i++)
        {
            if (_dots[i] == null) continue;
            _dots[i].enabled = showDots && mode == Mode.Busy; // 結果の字（Build Succeeded）と重ならないように、結果では隠す
            _dots[i].color = i < count ? (mode == Mode.Failed ? XcodeArt.Red : XcodeArt.Green) : MacUi.Light4;
        }
        if (_icon != null) _icon.color = mode == Mode.Busy && showDots && Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
    }
}
