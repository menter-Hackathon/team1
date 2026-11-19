using UnityEngine;

/// <summary>
/// 【仮】Safari のステージの決まり「トラッカーをブロック」（2026-11-19。docs/app-kits-2-design.md 4-4）。
/// ・HUD のライフの下に、盾の印と細いメーター（KitMeter。次の盾までの数）。雑魚を倒すたびに 1 増える（ボス戦は、近接を当てるたび）。
///   増えるたび、倒した所に小さい盾と数字が出る
/// ・5 たまるごとに、1回だけ当たりを防ぐ盾が付く（SafariShield。重ねない。付いている間は数えない）
/// ・割り切り：仕様書は今ある ShieldAura を使う形だが、ShieldAura は 5秒の無敵で、ボス戦では付かないので、KitPlayer.Guard で1回だけ止める盾にした
/// </summary>
public class SafariRule : StageRule
{
    const int Every = 5;

    int _count;
    int _total;
    bool _hooked;
    float _lastMeleeAt = -1f;
    SafariShield _shield;

    string MeterKey => Ctx.Key("trackers");

    public override void Begin(StageRuleContext ctx)
    {
        Health.AnyDied += OnAnyDied;
        Hitbox.AnyHit += OnAnyHit;
        _hooked = true;
        UpdateMeter();
    }

    public override void Tick(float dt)
    {
        if (_shield == null && _count >= Every)
        {
            _count = 0;
            Give();
        }
        UpdateMeter();
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            Hitbox.AnyHit -= OnAnyHit;
        }
        _hooked = false;
        KitMeter.Clear(MeterKey);
        if (_shield != null) Object.Destroy(_shield.gameObject);
        _shield = null;
    }

    void OnAnyDied(Health h)
    {
        if (Ctx.InBossFight || h == null || !RewardKit.IsZako(h)) return;
        Count(HitFx.CenterOf(h.gameObject, h.transform.position));
    }

    void OnAnyHit(Hitbox hb, IDamageable target, DamageInfo info, bool first)
    {
        if (!Ctx.InBossFight || hb == null || info.SourceTeam != Team.Player) return;
        if (!(target is VirusGauge g) || Ctx.Boss == null || g != Ctx.Boss.Gauge) return;
        if (hb.GetComponentInParent<PlayerMeleeAttack>() == null) return; // 近接だけ
        if (Time.time - _lastMeleeAt < 0.2f) return;
        _lastMeleeAt = Time.time;
        Count(info.HitPoint);
    }

    void Count(Vector2 at)
    {
        if (_shield != null) return; // 付いている間は数えない
        _count++;
        _total++;
        GameAudio.Event("gm_safari_count", 1f + 0.06f * (_count - 1));
        // 倒した所に小さい盾と数字
        FxAnim a = FxAnim.Sprite(SafariArt.Shield(), at + new Vector2(-0.35f, 0.9f), 40, 0.8f).Rise(0.6f).Pop();
        a.Refresh();
        FxKit.Text(_total.ToString(), at + new Vector2(0.3f, 0.9f), 0.45f, Color.white, 41, 0.8f, SafariArt.Blue).Rise(0.6f);
    }

    void Give()
    {
        Transform player = Ctx.Player;
        if (player == null) return;
        _shield = SafariShield.Apply(player);
        if (_shield != null) Ctx.TrackDecor(_shield.gameObject);
        KitToast.Show("Trackers Blocked", null, SafariArt.Blue);
        if (_shield != null) GameAudio.Event("gm_safari_shield_on");
    }

    void UpdateMeter()
    {
        float r = _shield != null ? 1f : Mathf.Clamp01((float)_count / Every);
        KitMeter.Set(MeterKey, r, SafariArt.Blue, SafariArt.Shield(), _shield != null);
    }
}

/// <summary>
/// 【仮】1回だけ当たりを防ぐ盾（Safari のステージの決まり。2026-11-19）。プレイヤーについていく。
/// 担当0 の「当たりを止める口」（KitPlayer.Guard）で、敵の当たりを1回だけ止める（無敵にはしない）。
/// 止めたら盾が割れて 1秒の無敵（被弾と同じ）。当てた物が弾なら消す。シーン変更・100％・ギミックの終わりで必ず外れる。
/// </summary>
public class SafariShield : MonoBehaviour
{
    const float HoldSeconds = 3600f; // 割れるまで（シーン変更・100％で外れる）
    const float AfterBreak = 1f;
    const int Order = 17;

    Transform _player;
    Health _health;
    SpriteRenderer _ring;
    KitGuard _guard;
    bool _broken;

    public static SafariShield Apply(Transform player)
    {
        if (player == null) return null;
        Health h = player.GetComponent<Health>();
        if (h == null || h.IsDead) return null;
        var go = new GameObject("SafariShield");
        var s = go.AddComponent<SafariShield>();
        s._player = player;
        s._health = h;
        s._guard = KitPlayer.Guard(HoldSeconds, s.OnBlocked, AfterBreak);
        if (s._guard == null)
        {
            Destroy(go);
            return null;
        }
        s._ring = RuntimeSprites.Show(go.transform, RewardKit.RingSprite(16, 2f), Vector2.zero, Order, 1f, "Ring");
        s._ring.color = SafariArt.A(SafariArt.Cyan, 0.9f);
        RuntimeSprites.Show(go.transform, SafariArt.Shield(), new Vector2(0f, 1.1f), Order + 1, 1f, "Icon");
        go.transform.position = RewardKit.BodyCenter(player);
        FxKit.Ring(go.transform.position, 2.2f, 0.2f, SafariArt.Cyan);
        return s;
    }

    /// <summary>敵の当たりが来た：1回だけ止める</summary>
    bool OnBlocked(DamageInfo info)
    {
        if (_broken) return false;
        _broken = true;
        if (_guard != null) _guard.Release();
        // 当てた物が弾なら消す
        if (info.Source != null && info.Source.GetComponent<Projectile>() != null) Destroy(info.Source);
        Vector2 c = transform.position;
        FxKit.Ring(c, 2.6f, 0.2f, Color.white);
        FxKit.Text("Blocked", c + Vector2.up * 1.4f, 0.45f, Color.white, 40, 0.7f, SafariArt.Blue).Rise(0.4f);
        GameAudio.Event("gm_safari_shield_block");
        Destroy(gameObject);
        return true;
    }

    void Update()
    {
        if (_player == null || _health == null || _health.IsDead || _guard == null || !_guard.Active)
        {
            Destroy(gameObject);
            return;
        }
        float s = 1f + Mathf.Sin(Time.time * 8f) * 0.05f;
        if (_ring != null) _ring.transform.localScale = new Vector3(s, s, 1f);
    }

    void LateUpdate()
    {
        if (_player == null) return;
        Vector2 c = RewardKit.BodyCenter(_player);
        transform.position = new Vector3(Mathf.Round(c.x * 18f) / 18f, Mathf.Round(c.y * 18f) / 18f, 0f);
    }

    void OnDestroy()
    {
        if (_guard != null) _guard.Release();
        _guard = null;
    }
}
