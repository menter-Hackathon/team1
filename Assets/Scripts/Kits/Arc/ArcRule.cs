using UnityEngine;

/// <summary>
/// 【仮】Arc のステージの決まり：Space（2026-11-19。docs/app-kits-2-design.md 4-5）。
/// ・2つの Space：自分の Space（ピンク）とウイルスの Space（青むらさき）。背景の色で分かる。左のふちにサイドバー（うすい板と Space の丸）
/// ・15秒おきに、勝手にウイルスの Space へスワイプする（色が横にすべる。0.4秒）：ボスの待ち時間 ×0.85・雑魚の移動 ×1.2。10秒で勝手に戻る
/// ・ウイルスの Space の間だけ、左のふちに切り替えのボタン（KitProp）。近接でたたくと、すぐ自分の Space へ戻る。ごほうびに 8秒、クールタイム ×0.8（進み方 ×1.25）
/// ・片づけ：ボタンは ctx.TrackPlaced（ボスの登場・50％で消えるので、ウイルスの Space のままなら出し直す）、背景・サイドバーは ctx.TrackDecor。倍率は 100％で全部外れる
/// </summary>
public class ArcRule : StageRule
{
    const float OwnTime = 15f, FirstDelay = 8f, VirusTime = 10f, SwipeTime = 0.4f, RewardTime = 8f;

    bool _virus;
    bool _warned; // ウイルスの Space の 1秒前の音を鳴らしたか
    float _timer;
    float _swipeStart = -10f;
    bool _swipeToVirus;
    GameObject _tint;
    SpriteRenderer _tintSr, _swipeSr;
    GameObject _sidebar;
    SpriteRenderer _sideDotOwn, _sideDotVirus;
    KitProp _button;

    static Sprite OwnSprite => ArcArt.Gradient(64, 36, 0, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, false);
    static Sprite VirusSprite => ArcArt.Gradient(64, 36, 0, ArcArt.Virus, ArcArt.VirusLight, ArcArt.Purple, false);
    Sprite _own, _vir;

    string ModKey => Ctx.Key("space");
    string RewardKey => Ctx.Key("space_reward");

    public override void Begin(StageRuleContext ctx)
    {
        _timer = FirstDelay;
        // 背景の色（画面いっぱい。背景の絵のすぐ上）
        _own = OwnSprite;
        _vir = VirusSprite;
        _tint = new GameObject("ArcSpaceTint");
        _tintSr = _tint.AddComponent<SpriteRenderer>();
        _tintSr.sprite = ArcArt.Gradient(64, 36, 0, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, false);
        _tintSr.sortingOrder = -94;
        var sw = new GameObject("Swipe");
        sw.transform.SetParent(_tint.transform, false);
        _swipeSr = sw.AddComponent<SpriteRenderer>();
        _swipeSr.sprite = ArcArt.Gradient(64, 36, 0, ArcArt.Virus, ArcArt.VirusLight, ArcArt.Purple, false);
        _swipeSr.sortingOrder = -93;
        _swipeSr.enabled = false;
        ctx.TrackDecor(_tint);

        // 左のサイドバー（うすい板と、Space の丸 2つ）
        _sidebar = new GameObject("ArcSidebar");
        var plate = RuntimeSprites.Show(_sidebar.transform, ArcArt.Gradient(14, 120, 6, Color.white, Color.white, Color.white, true, 0.18f), Vector2.zero, -30, 1f, "Plate");
        plate.color = Color.white;
        _sideDotOwn = RuntimeSprites.Show(_sidebar.transform, FxArt.Disc(6), new Vector2(0f, -2.4f), -29, 1f, "Own");
        _sideDotVirus = RuntimeSprites.Show(_sidebar.transform, FxArt.Disc(6), new Vector2(0f, -2.85f), -29, 1f, "Virus");
        ctx.TrackDecor(_sidebar);
        Follow();
        Paint();
    }

    public override void Tick(float dt)
    {
        Follow();
        _timer -= dt;
        // 場面③の出来事（スペースのスワイプ）の間は、ウイルスの Space を新しく始めない（色の切りかえが重ならないように。2026-11-19）
        if (!_virus && Ctx.Phase == PacePhase.Event && _timer < 1.5f) _timer = 1.5f;
        if (!_virus && !_warned && _timer <= 1f) { _warned = true; GameAudio.Event("gm_arc_virus_warn"); }
        if (!_virus && _timer <= 0f) Swipe(true);
        else if (_virus && _timer <= 0f) Swipe(false);
        if (_virus && _button == null && Ctx.CanPlace) SpawnButton();
        Paint();
    }

    public override void OnBossAppeared(BossController boss)
    {
        _button = null; // 片づけの表で消えた。ウイルスの Space のままなら Tick で出し直す
        Follow();
    }

    public override void End()
    {
        _virus = false;
        if (_button != null) Object.Destroy(_button.gameObject);
        _button = null;
    }

    void Swipe(bool toVirus)
    {
        _warned = false;
        _virus = toVirus;
        _swipeStart = Time.time;
        _swipeToVirus = toVirus;
        Sprite icon = Ctx.Icon;
        if (toVirus)
        {
            _timer = VirusTime;
            StatMods.Boss.Wait.Set(ModKey, 0.85f, icon);
            StatMods.Zako.Speed.Set(ModKey, 1.2f, icon);
            GameAudio.Event("gm_arc_space_virus");
        }
        else
        {
            _timer = OwnTime;
            StatMods.Boss.Wait.Clear(ModKey);
            StatMods.Zako.Speed.Clear(ModKey);
            GameAudio.Event("gm_arc_space_home");
            if (_button != null) Object.Destroy(_button.gameObject);
            _button = null;
        }
    }

    void SpawnButton()
    {
        Rect a = Ctx.PlayArea;
        float x = a.xMin + 0.75f;
        KitBlock.TryFindFloor(x, a, out float floor);
        _button = KitProp.Spawn(new Vector2(x, floor + 1.35f), ArcArt.SwitchButton(true), new Vector2(1f, 2f), OnButton, 1, 0f, 20);
        Ctx.TrackPlaced(_button.gameObject);
        GameAudio.Event("gm_arc_button");
    }

    void OnButton(KitProp p)
    {
        _button = null;
        if (!_virus) return;
        Swipe(false);
        StatMods.Player.CooldownRate.SetFor(RewardKey, 1f / 0.8f, RewardTime, Ctx.Icon);
        FxKit.Ring(p.transform.position, 2f, 0.3f, ArcArt.Pink);
        KitToast.Show("Space: Personal", Ctx.Icon, ArcArt.Pink);
    }

    /// <summary>背景・サイドバー・ボタンを、今の画面に合わせる</summary>
    void Follow()
    {
        Rect v = ScreenUi.ViewRect();
        if (_tint != null)
        {
            _tint.transform.position = new Vector3(v.center.x, v.center.y, 0f);
            Vector2 native = _tintSr.sprite.bounds.size;
            _tint.transform.localScale = new Vector3((v.width + 2f) / native.x, (v.height + 2f) / native.y, 1f);
        }
        if (_sidebar != null) _sidebar.transform.position = new Vector3(v.xMin + 0.45f, v.center.y, 0f);
        if (_button != null && !Ctx.InBossFight)
        {
            float x = v.xMin + 0.75f;
            KitBlock.TryFindFloor(x, Ctx.PlayArea, out float floor);
            _button.transform.position = new Vector2(x, floor + 1.35f);
        }
    }

    /// <summary>色（スワイプの間は、新しい色が右から横にすべってくる）</summary>
    void Paint()
    {
        if (_tintSr == null) return;
        float k = Mathf.Clamp01((Time.time - _swipeStart) / SwipeTime);
        bool swiping = k < 1f;
        // 下の色＝前の Space、上の色（スワイプ）＝次の Space
        bool showVirusBase = swiping ? !_swipeToVirus : _virus;
        _tintSr.sprite = showVirusBase ? _vir : _own;
        _tintSr.color = new Color(1f, 1f, 1f, showVirusBase ? 0.4f : 0.2f);
        _swipeSr.enabled = swiping;
        if (swiping)
        {
            _swipeSr.sprite = _swipeToVirus ? _vir : _own;
            _swipeSr.color = new Color(1f, 1f, 1f, _swipeToVirus ? 0.4f : 0.2f);
            float e = 1f - (1f - k) * (1f - k);
            // 親の大きさは画面いっぱい。子は右から左へすべる（親の中の位置で）
            float w = _tintSr.sprite.bounds.size.x;
            _swipeSr.transform.localPosition = new Vector3((1f - e) * w, 0f, 0f);
        }
        if (_sideDotOwn != null) _sideDotOwn.color = !_virus ? ArcArt.Pink : new Color(1f, 1f, 1f, 0.35f);
        if (_sideDotVirus != null) _sideDotVirus.color = _virus ? ArcArt.VirusLight : new Color(1f, 1f, 1f, 0.35f);
    }
}
