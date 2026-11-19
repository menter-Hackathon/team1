using UnityEngine;

/// <summary>
/// 【仮】Spotify のステージの決まり：広告と Premium（2026-11-19。docs/app-kits-2-design.md 4-2）。
/// ・ふだんは無料プラン。22秒おきに広告：上に Advertisement 0:04 の帯（4秒）。その間、クールタイムの進みが ×1/1.5（遅い）
/// ・雑魚を 6体倒すごと（ボス戦は 18秒おき）に、緑の Premium の札が左右の端から交互に横に流れてくる（2026-11-19：上から落ちるのをやめた。
///   床から 3.5・波打つ・3.5マス/秒＝約6秒で横切る。ボス戦は 3.0。KitMotion.Catch）。拾うと 10秒 Premium：
///   広告が来ない（出ている広告も消える）・クールタイムの進み ×1/0.6（速い）。帯は緑の Premium 0:10
/// ・不利になるギミックなので、効きは弱く（【要相談⑧】の「弱く入れる」）
/// ・札は置いた物（ボスの登場・50％・100％で消える）
/// </summary>
public class SpotifyRule : StageRule
{
    /// <summary>【仮】広告の間・長さ・クールタイムの進み</summary>
    public const float AdEvery = 22f, AdSeconds = 4f, AdRate = 1f / 1.5f;
    /// <summary>【仮】Premium の札</summary>
    public const int KillsPerTicket = 6;
    public const float BossTicketEvery = 18f, TicketLife = 6f, PremiumSeconds = 10f, PremiumRate = 1f / 0.6f;
    public const string KeyAd = "spotify.ad", KeyPremium = "spotify.premium";

    float _nextAd;
    float _adUntil;
    float _premiumUntil;
    bool _adWarned; // 広告の 1秒前の音を鳴らしたか
    int _kills;
    int _ticketsOwed;
    float _nextBossTicket;
    bool _hooked;
    SpotifyBand _band;

    public override void Begin(StageRuleContext ctx)
    {
        _nextAd = Time.time + AdEvery;
        Health.AnyDied += OnAnyDied;
        _hooked = true;
        var go = new GameObject("SpotifyBand");
        _band = go.AddComponent<SpotifyBand>();
        ctx.TrackDecor(go);
    }

    public override void OnBossAppeared(BossController boss) => _nextBossTicket = Time.time + BossTicketEvery;

    public override void Tick(float dt)
    {
        float now = Time.time;
        bool premium = now < _premiumUntil;
        // 広告の 1秒前に1回だけ鳴らす（止まっている間に 1秒前を過ぎたときは、始まりの音だけ）
        float adAt = Mathf.Max(_nextAd, _premiumUntil);
        if (!_adWarned && _adUntil <= 0f && now >= adAt - 1f && now < adAt)
        {
            _adWarned = true;
            GameAudio.Event("gm_spotify_ad_warn");
        }
        // 広告
        if (!premium && _adUntil <= 0f && now >= _nextAd)
        {
            _adUntil = now + AdSeconds;
            _adWarned = false;
            StatMods.Player.CooldownRate.SetFor(KeyAd, AdRate, AdSeconds, SpotifyArt.AdIcon);
            GameAudio.Event("gm_spotify_ad_start");
        }
        if (_adUntil > 0f && (now >= _adUntil || premium))
        {
            _adUntil = 0f;
            _nextAd = now + AdEvery;
            StatMods.Player.CooldownRate.Clear(KeyAd);
            if (!premium) GameAudio.Event("gm_spotify_ad_end"); // Premium で消えたときは gm_spotify_premium が鳴る
        }
        if (_band != null)
        {
            if (premium) _band.Show(false, _premiumUntil - now);
            else if (_adUntil > 0f) _band.Show(true, _adUntil - now);
            else _band.Hide();
        }
        // Premium の札
        if (Ctx.InBossFight && now >= _nextBossTicket)
        {
            _nextBossTicket = now + BossTicketEvery;
            _ticketsOwed++;
        }
        if (_ticketsOwed > 0 && Ctx.CanPlace && Ctx.PlacedCount == 0)
        {
            if (DropTicket()) _ticketsOwed--;
        }
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _ticketsOwed = 0;
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            _hooked = false;
        }
        StatMods.ClearPrefix("spotify.");
    }

    void OnAnyDied(Health h)
    {
        if (Ctx.InBossFight || !RewardKit.IsZako(h)) return;
        _kills++;
        if (_kills % KillsPerTicket == 0) _ticketsOwed = Mathf.Min(_ticketsOwed + 1, 2);
    }

    /// <summary>【仮】札の流れ方（2026-11-19：docs/stage-gimmick-variety-c.md 9章。上から落とすのをやめて、左右の端から横に流す）</summary>
    public const float TicketHeight = 3.5f, TicketWave = 1f, TicketSpeed = 3.5f, TicketSpeedBoss = 3f, TicketWarn = 0.8f, TicketSize = 1f;

    bool _ticketFromRight;

    bool DropTicket()
    {
        KitSide side = _ticketFromRight ? KitSide.Right : KitSide.Left; // 左右は交互
        _ticketFromRight = !_ticketFromRight;
        SendTicket(side, Ctx.InBossFight ? TicketSpeedBoss : TicketSpeed, KitOwner.Of(go => Ctx.TrackPlaced(go)));
        GameAudio.Event("gm_spotify_ticket_drop");
        return true;
    }

    /// <summary>
    /// 【仮】Premium の札を1枚、side の端から横に流す（KitMotion.Catch。床から 3.5 の高さ・上下に 1.0 波打つ）。
    /// 出る 0.8秒前に、出る側の端に緑の矢印（良い物なので赤にしない）。触ると今の効き（OnPicked）。
    /// owner：片づけ先（決まりは置いた物 ctx.TrackPlaced。場面③の出来事 SpotifyShuffleEvent も同じ物を使う）
    /// </summary>
    public KitMotionCatch SendTicket(KitSide side, float speed, KitOwner owner)
    {
        Rect area = KitMotion.Area;
        float y = KitMotion.FloorY() + TicketHeight;
        bool fromLeft = side != KitSide.Right;
        KitMotionMark.Create(null, KitMotionArt.Arrow, new Vector2(fromLeft ? area.xMin + 0.55f : area.xMax - 0.55f, y), new Vector2(0.75f, 0.75f),
            fromLeft ? 0f : 180f, (Color)SpotifyArt.Green, KitMotionItem.OrderMark, TicketWarn, "SpotifyTicketArrow");
        // 矢印の間は画面の外で待ってから、波打って横切る
        KitPath sweep = KitPath.Sweep(fromLeft ? KitSide.Left : KitSide.Right, y, Mathf.Max(0.5f, speed), TicketWave, 4f, TicketSize, area);
        var path = new KitPath(t => sweep.At(t - TicketWarn), TicketWarn + sweep.Duration);
        return KitMotion.Catch(owner, path, () => OnPicked(), SpotifyArt.Premium, null, TicketSize);
    }

    void OnPicked()
    {
        _premiumUntil = Time.time + PremiumSeconds;
        _adWarned = false; // 広告が後ろへずれるので、1秒前の音を鳴らし直す
        StatMods.Player.CooldownRate.SetFor(KeyPremium, PremiumRate, PremiumSeconds, SpotifyArt.PremiumIcon);
        GameAudio.Event("gm_spotify_premium");
    }
}

/// <summary>Premium の札（落ちて床で止まり、6秒残る。触ると拾う）。2026-11-19 から決まりは使っていない（SendTicket の横に流れる札にした）</summary>
public class SpotifyTicket : MonoBehaviour
{
    const float FallSpeed = 9f, PickRadius = 0.9f;
    Transform _player;
    float _dieAt;
    float _groundY;
    bool _landed;
    System.Action _onPick;
    SpriteRenderer _sr;

    public void Setup(Vector2 from, Transform player, float life, System.Action onPick)
    {
        transform.position = from;
        _player = player;
        _dieAt = Time.time + life + 1f;
        _onPick = onPick;
        _sr = RuntimeSprites.Show(transform, SpotifyArt.Premium, Vector2.zero, 30, 1f, "Ticket");
        _groundY = FindGround(from);
    }

    static float FindGround(Vector2 from)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Ground", "OneWayPlatform"));
        var hits = new RaycastHit2D[4];
        int n = Physics2D.Raycast(from, Vector2.down, filter, hits, 40f);
        for (int i = 0; i < n; i++) if (hits[i].distance > 0f) return hits[i].point.y + 0.5f;
        return BattleArena.GetBounds().yMin + 0.5f;
    }

    void Update()
    {
        Vector3 p = transform.position;
        if (!_landed)
        {
            p.y -= FallSpeed * Time.deltaTime;
            if (p.y <= _groundY) { p.y = _groundY; _landed = true; }
            transform.position = p;
        }
        float left = _dieAt - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        if (_sr != null)
        {
            _sr.enabled = left > 1.5f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            _sr.transform.localPosition = new Vector3(0f, _landed ? Mathf.Round(Mathf.Sin(Time.time * 4f) * 2f) / 18f : 0f, 0f);
        }
        if (_player != null && ((Vector2)_player.position + Vector2.up * 0.5f - (Vector2)transform.position).sqrMagnitude <= PickRadius * PickRadius)
        {
            FxKit.Ring(transform.position, 2f, 0.25f, (Color)SpotifyArt.Green);
            try { _onPick?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
            Destroy(gameObject);
        }
    }
}

/// <summary>上の帯（広告：Advertisement 0:04 ／ Premium：Premium 0:10）。カメラの子。飾り</summary>
public class SpotifyBand : MonoBehaviour
{
    const float TopOffset = 2.3f;
    Transform _root;
    SpriteRenderer _back, _icon;
    TextMesh _text;
    bool _ad;

    void Awake()
    {
        _root = FxKit.CameraChild("SpotifyBandRoot");
        if (_root == null) return;
        _back = AttackRunner.AddVisual(_root, MacUi.RoundRectSprite(144, 16, 3), Color.white, new Vector2(8f, 0.9f), FxKit.OrderToast - 2, "Back");
        _icon = RuntimeSprites.Show(_root, SpotifyArt.AdIcon, new Vector2(-3.55f, 0f), FxKit.OrderToast - 1, 1f, "Icon");
        _text = WorldText.Create(_root, "", 0.4f, Color.white, FxKit.OrderToast - 1, true, TextAnchor.MiddleCenter, "Text");
        _text.transform.localPosition = new Vector3(0.25f, 0f, -0.001f);
        Hide();
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    public void Show(bool ad, float left)
    {
        if (_root == null) return;
        _root.gameObject.SetActive(true);
        Vector2 view = FxKit.ViewSize();
        _root.localPosition = new Vector3(0f, view.y * 0.5f - TopOffset, _root.localPosition.z);
        int s = Mathf.Max(0, Mathf.CeilToInt(left));
        string time = $"0:{s:00}";
        _text.text = ad ? $"Advertisement {time}" : $"Premium {time}";
        if (ad != _ad || _back.color.a == 0f)
        {
            _ad = ad;
            _back.color = ad ? new Color(0.16f, 0.16f, 0.16f, 0.95f) : (Color)SpotifyArt.Green;
            _icon.sprite = ad ? SpotifyArt.AdIcon : SpotifyArt.PremiumIcon;
            WorldText.SetColor(_text, ad ? (Color)SpotifyArt.Gray : (Color)SpotifyArt.Black);
        }
    }

    public void Hide()
    {
        if (_root != null) _root.gameObject.SetActive(false);
        if (_back != null) _back.color = new Color(1f, 1f, 1f, 0f);
    }
}
