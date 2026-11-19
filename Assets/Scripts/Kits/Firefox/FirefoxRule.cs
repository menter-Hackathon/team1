using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Firefox のステージの決まり：アドオン（2026-11-19。docs/app-kits-2-design.md 4-6）。
/// ・右上にツールバー（パズルのピースの枠が 3つ）
/// ・雑魚を 5体倒すごと（ボス戦は 15秒おき）に、パズルのピースが来る（止まってから 6秒残る）。拾うとアドオンが1つ入る（そのステージの間ずっと。同じ物は出ない）
///   2026-11-19：上から落とすのをやめて、プレイヤーから遠い方の端の高さ 4 から、床で 2回跳ねながら入ってきて止まる（KitMotion.Catch。
///   docs/stage-gimmick-variety-b.md 11章）。入る端に紫の小さい矢印（0.5秒）
///   Speed：速さ ×1.1 ／ Power：与えるゲージ ×1.2 ／ Cooldown：クールタイムの進み ×1/0.85
/// ・ボスも 50％でアドオンを1つ入れる（紫のピース。待ち時間 ×0.85。10秒だけ）。ボスのゲージの下に紫のピースの印
/// ・Steam の実績は「条件で・8秒」、こちらは「拾って・ずっと」
/// </summary>
public class FirefoxRule : StageRule
{
    /// <summary>【仮】数</summary>
    public const int KillsPerPiece = 5;
    public const float BossPieceEvery = 15f, PieceLife = 6f, BossAddonSeconds = 10f;
    public const float SpeedMult = 1.1f, PowerMult = 1.2f, CoolRate = 1f / 0.85f, BossWait = 0.85f;
    /// <summary>【仮】ピースの入り方：入る高さ（床から）・跳ねる高さ（2回）・重さ・横の速さ</summary>
    const float PieceEnterHeight = 4f, PieceGravity = 25f, PieceSpeedMin = 3f, PieceSpeedMax = 5f;
    static readonly float[] PieceHops = { 1.6f, 0.7f };
    public const string KeyBoss = "firefox.boss";
    static readonly string[] Names = { "Speed", "Power", "Cooldown" };

    readonly List<int> _installed = new List<int>();
    int _kills;
    int _owed;
    float _nextBossPiece;
    bool _hooked;
    bool _bossAddonOn; // ボスのアドオンが切れた瞬間の音のため
    FirefoxToolbar _bar;

    public override void Begin(StageRuleContext ctx)
    {
        Health.AnyDied += OnAnyDied;
        _hooked = true;
        var go = new GameObject("FirefoxToolbar");
        _bar = go.AddComponent<FirefoxToolbar>();
        ctx.TrackDecor(go);
    }

    public override void OnBossAppeared(BossController boss) => _nextBossPiece = Time.time + BossPieceEvery;

    public override void OnBossPhase(int phase)
    {
        if (phase < 2) return;
        StatMods.Boss.Wait.SetFor(KeyBoss, BossWait, BossAddonSeconds);
        _bossAddonOn = true;
        GameAudio.Event("gm_firefox_boss_addon");
        KitPips.SetBoss(KeyBoss, FirefoxArt.BossAddonIcon, true); // ボスのゲージの下に紫のピースと上向きの矢印
        BossController b = Ctx.Boss;
        if (b != null) FxKit.Ring(b.BodyBounds.center, 3f, 0.3f, (Color)FirefoxArt.Purple);
    }

    public override void Tick(float dt)
    {
        if (!StatMods.Boss.Wait.Has(KeyBoss)) // 10秒で消す
        {
            KitPips.ClearBoss(KeyBoss);
            if (_bossAddonOn) { _bossAddonOn = false; GameAudio.Event("gm_firefox_boss_addon_end"); } // 毎フレーム通るので、旗で1回だけ
        }
        if (_installed.Count >= Names.Length) return;
        if (Ctx.InBossFight && Time.time >= _nextBossPiece)
        {
            _nextBossPiece = Time.time + BossPieceEvery;
            _owed = Mathf.Min(_owed + 1, 1);
        }
        if (_owed > 0 && Ctx.CanPlace && Ctx.PlacedCount == 0)
        {
            Drop();
            _owed--;
        }
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _owed = 0;
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            _hooked = false;
        }
        StatMods.ClearPrefix("firefox.");
        KitPips.ClearBoss(KeyBoss);
        _bossAddonOn = false;
    }

    /// <summary>
    /// 【仮】ピースを 1つ来させる（場面③の出来事「しっぽの炎」で、追跡者の的を叩いたとき。2026-11-19）。
    /// アドオンが全部入っている・もう 2つ待っていれば false
    /// </summary>
    public bool GrantPiece()
    {
        if (_installed.Count >= Names.Length || _owed >= 2) return false;
        _owed++;
        return true;
    }

    void OnAnyDied(Health h)
    {
        if (Ctx.InBossFight || !RewardKit.IsZako(h)) return;
        _kills++;
        if (_kills % KillsPerPiece == 0) _owed = Mathf.Min(_owed + 1, 2);
    }

    void Drop()
    {
        var left = new List<int>();
        for (int i = 0; i < Names.Length; i++) if (!_installed.Contains(i)) left.Add(i);
        if (left.Count == 0) return;
        int kind = left[Random.Range(0, left.Count)];
        Rect area = Ctx.PlayArea;
        Transform p = Ctx.Player;
        float px = p != null ? p.position.x : area.center.x;
        // プレイヤーから遠い方の端の外（高さ 4）から、床で 2回跳ねて止まる。止まってから 6秒残る（最後の 1秒は点滅）
        bool fromLeft = px >= area.center.x;
        float floor = KitMotion.FloorY();
        const float size = 14f / 18f; // ピースの絵の大きさ
        float edge = fromLeft ? area.xMin - 0.5f : area.xMax + 0.5f;
        float vx = (fromLeft ? 1f : -1f) * Random.Range(PieceSpeedMin, PieceSpeedMax);
        KitPath hop = GroupBMotion.HopPath(new Vector2(edge, floor + PieceEnterHeight), vx, PieceGravity, PieceHops, floor, size * 0.5f, area);
        // 拾う広さは前と同じくらい（触る判定を広く。絵はピースの大きさに戻す）
        KitMotionCatch c = KitMotion.Catch(KitOwner.Of(go => Ctx.TrackPlaced(go)), hop.Then(PieceLife), () => Install(kind),
            FirefoxArt.Piece, FirefoxArt.AddonColor(kind), 1.4f);
        if (c != null && c.Visual != null)
        {
            c.Visual.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, c.Visual.sprite.bounds.size.x));
            RuntimeSprites.Show(c.Body, FirefoxArt.PieceMark(kind), new Vector2(-0.03f, -0.03f), KitMotionItem.OrderBody + 1, 1f, "Mark");
        }
        KitMotionMark.Create(null, KitMotionArt.Arrow, new Vector2(fromLeft ? area.xMin + 0.55f : area.xMax - 0.55f, floor + PieceEnterHeight),
            new Vector2(0.75f, 0.75f), fromLeft ? 0f : 180f, (Color)FirefoxArt.Purple, KitMotionItem.OrderMark, 0.5f, "FirefoxPieceArrow");
        GameAudio.Event("gm_firefox_piece_drop");
    }

    void Install(int kind)
    {
        if (_installed.Contains(kind)) return;
        _installed.Add(kind);
        GameAudio.Event("gm_firefox_addon_install", 1f + 0.12f * (_installed.Count - 1)); // 入れるたびに高く
        Sprite icon = FirefoxArt.AddonIcon(kind);
        switch (kind)
        {
            case 0: StatMods.Player.Speed.Set("firefox.speed", SpeedMult, icon); break;
            case 1: StatMods.Player.GaugeGain.Set("firefox.power", PowerMult, icon); break;
            default: StatMods.Player.CooldownRate.Set("firefox.cool", CoolRate, icon); break;
        }
        if (_bar != null) _bar.Fill(_installed.Count - 1, kind);
        KitToast.Show($"{Names[kind]} added", null, FirefoxArt.AddonColor(kind));
    }
}

/// <summary>パズルのピース（落ちて床で止まり、6秒残る。触ると拾う）</summary>
public class FirefoxPiece : MonoBehaviour
{
    const float FallSpeed = 9f, PickRadius = 0.9f;
    Transform _player;
    float _dieAt, _groundY;
    bool _landed;
    System.Action _onPick;
    SpriteRenderer _sr;

    public void Setup(Vector2 from, Transform player, int kind, float life, System.Action onPick)
    {
        transform.position = from;
        _player = player;
        _dieAt = Time.time + life + 1f;
        _onPick = onPick;
        _sr = RuntimeSprites.Show(transform, FirefoxArt.Piece, Vector2.zero, 30, 1f, "Piece");
        _sr.color = FirefoxArt.AddonColor(kind);
        RuntimeSprites.Show(_sr.transform, FirefoxArt.PieceMark(kind), new Vector2(-0.03f, -0.03f), 31, 1f, "Mark");
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Ground", "OneWayPlatform"));
        var hits = new RaycastHit2D[4];
        int n = Physics2D.Raycast(from, Vector2.down, filter, hits, 40f);
        _groundY = BattleArena.GetBounds().yMin + 0.5f;
        for (int i = 0; i < n; i++) if (hits[i].distance > 0f && !AppStagePlatforms.IsUnsteady(hits[i].collider)) { _groundY = hits[i].point.y + 0.5f; break; } // 動く・消えるアプリの足場の上には落とさない（2026-11-19）
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
            FxKit.Ring(transform.position, 2f, 0.25f, (Color)FirefoxArt.Orange);
            try { _onPick?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
            Destroy(gameObject);
        }
    }
}

/// <summary>右上のツールバー（ピースの枠 3つ）。カメラの子。飾り</summary>
public class FirefoxToolbar : MonoBehaviour
{
    const float TopOffset = 4.1f, Margin = 0.35f;
    Transform _root;
    readonly SpriteRenderer[] _slots = new SpriteRenderer[3];

    void Awake()
    {
        _root = FxKit.CameraChild("FirefoxToolbarRoot");
        if (_root == null) return;
        Vector2 view = FxKit.ViewSize();
        float w = 3f * 1.0f + 0.3f;
        _root.localPosition = new Vector3(view.x * 0.5f - Margin - w * 0.5f, view.y * 0.5f - TopOffset, _root.localPosition.z);
        FxKit.Box(_root, Vector2.zero, new Vector2(w, 1.0f), new Color(0.17f, 0.16f, 0.2f, 0.85f), FxKit.OrderToast - 3, "Back");
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = new Vector2((i - 1) * 1.0f, 0f);
            FxKit.Box(_root, at, new Vector2(0.86f, 0.86f), new Color(1f, 1f, 1f, 0.12f), FxKit.OrderToast - 2, "Slot" + i);
            SpriteRenderer ghost = RuntimeSprites.Show(_root, FirefoxArt.Piece, at, FxKit.OrderToast - 1, 0.9f, "Ghost" + i);
            ghost.color = new Color(1f, 1f, 1f, 0.18f);
            _slots[i] = ghost;
        }
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    public void Fill(int slot, int kind)
    {
        if (slot < 0 || slot >= _slots.Length || _slots[slot] == null) return;
        _slots[slot].color = FirefoxArt.AddonColor(kind);
        RuntimeSprites.Show(_slots[slot].transform, FirefoxArt.PieceMark(kind), new Vector2(-0.03f, -0.03f), FxKit.OrderToast, 1f, "Mark");
        FxKit.Ring(_slots[slot].transform.position, 1.4f, 0.25f, Color.white);
    }
}
