using UnityEngine;

/// <summary>
/// 【仮】メールのステージのギミック「新着メール」（2026-11-19。docs/app-kits-1-design.md 15章）。StageRule。
/// ・15秒おき（ボス戦は 20秒おき）に、右上に通知（New Mail）。封筒が 1通、プレイヤーから 3〜5マスの所へ届く（痛くない。床に 6秒残る）
///   2026-11-19（docs/stage-gimmick-variety-a.md Mail）：上から降りるのをやめて、右上の通知の所から山なりに飛んで（0.9秒）、床か足場の上に着く。
///   飛んでいる間は取れない（着いてから）。動く足場（swipe）・落ちる足場（spam）には着かせない（AppStagePlatforms.RaycastSteady）
/// ・触ると開く。中身は封筒の見た目で分かる
///   - ふつうのメール（白い封筒に青い帯。7割）：ごほうび。クールタイムを全部回復（E5）
///   - 迷惑メール（紫の「！」・文字化けの帯。3割）：開けると、赤く 0.8秒点滅して直径 3 の爆発。開けてから離れれば当たらない。開けなければ何も起きない
/// ・置くのは ctx.CanPlace のとき。迷惑メールにするのは ctx.CanSpawnHazard のときだけ（ボスの場に残る技の間などは、ふつうのメールだけ）
/// </summary>
public class MailRule : StageRule
{
    // 【仮】数値
    const float Interval = 15f, BossInterval = 20f, RetryDelay = 1f;
    const float SpamChance = 0.3f;
    const float Life = 6f, Fuse = 0.8f, Blast = 3f, BlastActive = 0.15f;
    const float FlyTime = 0.9f, FlyArc = 2f; // 【仮】右上から山なりに飛ぶ秒数・ふくらみ

    float _timer;
    AttackData _blastData;

    public override void Begin(StageRuleContext ctx)
    {
        _timer = Interval * 0.6f;
        _blastData = CallKitUtil.HazardData("Attack_Mail_SpamOpen");
    }

    public override void OnBossAppeared(BossController boss) => _timer = BossInterval * 0.6f;

    public override void Tick(float dt)
    {
        _timer -= dt;
        if (_timer > 0f) return;
        if (!Ctx.CanPlace || Ctx.Phase == PacePhase.Event)
        {
            _timer = RetryDelay;
            return;
        }
        Deliver();
        _timer = Ctx.InBossFight ? BossInterval : Interval;
    }

    public override void End()
    {
        if (_blastData != null) Object.Destroy(_blastData);
        _blastData = null;
    }

    void Deliver()
    {
        Rect area = Ctx.PlayArea;
        if (!CallKitUtil.TryPlayerBounds(out Bounds p)) return;
        // プレイヤーから 3〜5マス（画面の中で、行ける方）
        float side = Random.value < 0.5f ? -1f : 1f;
        float x = p.center.x + side * Random.Range(3f, 5f);
        if (x < area.xMin + 1f || x > area.xMax - 1f) x = p.center.x - side * Random.Range(3f, 5f);
        x = Mathf.Clamp(x, area.xMin + 1f, area.xMax - 1f);
        bool spam = Ctx.CanSpawnHazard && Random.value < SpamChance;
        var go = new GameObject("Mail_NewMail");
        go.transform.position = new Vector3(area.xMax - 1.2f, area.yMax - 1.2f, 0f); // 右上の通知（New Mail）の所から
        Ctx.TrackPlaced(go);
        go.AddComponent<MailNewEnvelope>().Setup(this, spam, x, area.yMax - 0.5f);
        KitToast.Show("New Mail", null, MailArt.Blue);
        GameAudio.Event("gm_mail_new");
    }

    /// <summary>迷惑メールを開けた：赤く点滅してから爆発（ギミックの危ない物として覚える）</summary>
    internal void OpenSpam(Vector2 c)
    {
        AttackRunner runner = Ctx.Hazard;
        if (runner == null) return;
        GameAudio.Event("gm_mail_spam_open");
        GameObject warn = runner.CreateSpawnedObject("Mail_SpamWarn", c);
        Ctx.TrackHazard(warn);
        warn.AddComponent<MailSpamWarn>().Setup(Blast, Fuse);
        CallTimedHit hit = CallKitUtil.TimedHit(runner, _blastData, c, Vector2.one * Blast, true, Fuse, BlastActive, "Spam");
        if (hit != null) Ctx.TrackHazard(hit.gameObject);
    }

    internal static float Fly => FlyTime;
    internal static float Arc => FlyArc;
    internal static float EnvelopeLife => Life;
}

/// <summary>
/// 新着メールの封筒：右上から山なりに飛んで（0.9秒）床か足場に着き、6秒残る。着いてからプレイヤーが触ると開く（ふつう：クールタイム回復／迷惑：爆発の予告）。
/// 2026-11-19：上からゆっくり降りる → 右上から飛んで来る（飛んでいる間は取れない）
/// </summary>
public class MailNewEnvelope : MonoBehaviour
{
    const int Order = 23;
    MailRule _rule;
    bool _spam, _landed, _opened;
    float _landedAt, _born;
    Vector2 _from, _to;
    SpriteRenderer _art;
    readonly Vector2 _size = new Vector2(1.0f, 0.69f);

    /// <summary>x：着く所の x。fromY：そこから下へ着く面を探す高さ</summary>
    public void Setup(MailRule rule, bool spam, float x, float fromY)
    {
        _rule = rule;
        _spam = spam;
        _born = Time.time;
        _art = MailArt.Put(transform, spam ? MailArt.SpamEnvelope : MailArt.Envelope, Vector2.zero, _size, Color.white, Order, "Art");
        // 着く所：床か、動かない足場の上（動く・落ちる UI の足場は飛ばす）
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        RaycastHit2D hit = AppStagePlatforms.RaycastSteady(new Vector2(x, fromY), Vector2.down, 40f, mask);
        float groundY = hit.collider != null ? hit.point.y : BattleArena.GetBounds().yMin;
        _from = transform.position;
        _to = new Vector2(x, groundY + _size.y * 0.5f);
    }

    void Update()
    {
        if (_opened) return;
        if (!_landed)
        {
            // 山なりに飛ぶ（まわりながら）。飛んでいる間は取れない
            float k = Mathf.Clamp01((Time.time - _born) / Mathf.Max(0.05f, MailRule.Fly));
            Vector2 p = Vector2.Lerp(_from, _to, k) + Vector2.up * (4f * MailRule.Arc * k * (1f - k));
            transform.position = p;
            if (_art != null) _art.transform.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 360f);
            if (k >= 1f)
            {
                _landed = true;
                _landedAt = Time.time;
                if (_art != null) _art.transform.localRotation = Quaternion.identity;
                FxKit.Ring(_to, 0.9f, 0.2f, Color.white);
            }
            return;
        }
        float left = MailRule.EnvelopeLife - (Time.time - _landedAt);
        if (left <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (_art != null) _art.enabled = left > 1.5f || Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
        // 触ると開く（着いてから）
        if (CallKitUtil.TryPlayerBounds(out Bounds pb))
        {
            var mine = new Bounds(transform.position, new Vector3(_size.x + 0.2f, _size.y + 0.2f, 1f));
            pb.extents = new Vector3(pb.extents.x, pb.extents.y, 1f);
            if (mine.Intersects(pb)) Open();
        }
    }

    void Open()
    {
        _opened = true;
        Vector2 c = transform.position;
        if (_spam)
        {
            _rule?.OpenSpam(c);
        }
        else
        {
            KitPlayer.RefillCooldowns();
            GameAudio.Event("gm_mail_open_good");
            FxAnim.Sprite(FxArt.Check, c + Vector2.up * 0.7f, 60, 0.6f, MailArt.Blue, 2f, "Mail_Open").Pop().Rise(0.5f);
            FxKit.Fragments(c, Vector2.up, 6, null, new[] { MailArt.Blue, MailArt.Paper }, 1f);
        }
        // 開いた封筒：ふたが開いて消える
        FxAnim.Sprite(_spam ? MailArt.SpamEnvelope : MailArt.Envelope, c, Order, 0.3f, Color.white, 1.1f, "Mail_Opened").Rise(0.3f).FadeFrom(0.2f);
        Destroy(gameObject);
    }
}

/// <summary>迷惑メールの爆発の予告（赤い円のふちが点滅）。時間が来たら、白い輪とかけら</summary>
public class MailSpamWarn : MonoBehaviour
{
    const int Order = 18;
    float _born, _fuse, _diameter;
    SpriteRenderer _ring, _fill;

    public void Setup(float diameter, float fuse)
    {
        _born = Time.time;
        _fuse = fuse;
        _diameter = diameter;
        int d = Mathf.Clamp(Mathf.RoundToInt(diameter * CallArt.Ppu), 8, 96);
        _fill = MailArt.Put(transform, CallArt.Disc(Mathf.Min(48, d)), Vector2.zero, Vector2.one * diameter, CallKitUtil.Alpha(KitDress.Red, 0.2f), Order, "Fill");
        _ring = MailArt.Put(transform, CallArt.Ring(d, 2), Vector2.zero, Vector2.one * diameter, KitDress.Red, Order + 1, "Ring");
        RuntimeSprites.Show(transform, CallArt.Exclaim, Vector2.up * 0.9f, Order + 2, 1f, "Exclaim");
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t >= _fuse)
        {
            MailBlastFx.Show(transform.position, _diameter);
            GameAudio.Event("gm_mail_spam_boom");
            Destroy(gameObject);
            return;
        }
        bool on = Mathf.FloorToInt(t / (_fuse - t < 0.3f ? 0.05f : 0.1f)) % 2 == 0;
        if (_ring != null) _ring.enabled = on;
        if (_fill != null) _fill.color = CallKitUtil.Alpha(KitDress.Red, on ? 0.3f : 0.12f);
    }
}
