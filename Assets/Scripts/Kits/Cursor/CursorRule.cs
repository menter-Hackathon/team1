using UnityEngine;

/// <summary>
/// 【仮】Cursor のステージの決まり「Tab の候補」（2026-11-19。docs/app-kits-2-design.md 3-3）。
/// ・14秒おきに、床か足場の上に、灰色の点線の箱と Tab のキーの印が出る（5秒。プレイヤーから 5マス以上。部屋の両端 2マスには出さない）
/// ・触ると Accept：全部の能力のクールタイムが 0 になり、白い光と Accepted
/// ・ボス戦：5秒のうちに触れないと、ボスが Accept する（箱が紫になってボスへ飛ぶ）→ ボスの次の待ち時間が半分（1回だけ。StatMods の下限 0.7 で止まる）
/// ・雑魚の時間は、触れなくても何も起きない
/// </summary>
public class CursorRule : StageRule
{
    const float Interval = 14f;
    const float FirstDelay = 6f;
    const float MinDistance = 5f;
    const float EdgeMargin = 2f;

    float _timer = FirstDelay;
    CursorTabPickup _pickup;
    BossController _hookedBoss;
    int _clearAfterStarts;

    public override void Tick(float dt)
    {
        if (_pickup != null) return;
        _timer -= dt;
        if (_timer > 0f) return;
        if (!Ctx.CanPlace) return; // 置けるまで待つ
        if (Ctx.Phase == PacePhase.Event) return; // 場面③の出来事（ゴーストテキスト）の間は置かない（良い物が多くなりすぎないように。2026-11-19）
        if (Place()) _timer = Interval;
    }

    public override void OnBossAppeared(BossController boss)
    {
        _timer = Mathf.Min(_timer, 8f);
        if (boss != null && _hookedBoss == null)
        {
            _hookedBoss = boss;
            boss.MoveStarted += OnMoveStarted;
        }
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) _pickup = null; // 置いた物は StageRuleHost が消す
    }

    public override void End()
    {
        if (_hookedBoss != null) _hookedBoss.MoveStarted -= OnMoveStarted;
        _hookedBoss = null;
        StatMods.ClearPrefix("cursor.");
        if (_pickup != null) Object.Destroy(_pickup.gameObject);
        _pickup = null;
    }

    bool Place()
    {
        AttackRunner probe = Ctx.Hazard;
        Transform player = Ctx.Player;
        if (player == null) return false;
        Rect area = Ctx.PlayArea;
        float px = player.position.x;
        for (int t = 0; t < 20; t++)
        {
            float x = Random.Range(area.xMin + EdgeMargin, area.xMax - EdgeMargin);
            if (Mathf.Abs(x - px) < MinDistance) continue;
            float gy = area.yMin;
            if (probe != null)
            {
                // 上から下へ探して、いちばん上の床・足場（足場の上に出ることもある）
                if (!probe.TryFindGround(x, area.yMax - 0.5f, out gy, steadyOnly: true)) continue; // 動く・消えるアプリの足場の上には置かない（2026-11-19）
            }
            Vector2 pos = new Vector2(x, gy + 0.75f);
            _pickup = CursorTabPickup.Spawn(pos, player, OnAccepted, OnExpired);
            Ctx.TrackPlaced(_pickup.gameObject);
            GameAudio.Event("gm_cursor_tab_appear");
            return true;
        }
        return false;
    }

    void OnAccepted(CursorTabPickup p)
    {
        KitPlayer.RefillCooldowns();
        _pickup = null;
    }

    void OnExpired(CursorTabPickup p)
    {
        _pickup = null;
        if (!Ctx.InBossFight || Ctx.Boss == null || !KitBoss.IsFighting(Ctx.Boss)) return;
        // ボスが Accept する：箱が紫になってボスへ飛ぶ → 次の待ち時間が半分（1回だけ）
        BossController boss = Ctx.Boss;
        GameAudio.Event("gm_cursor_boss_take");
        p.FlyTo(boss, () =>
        {
            if (boss == null) return;
            StatMods.Boss.Wait.Set(Ctx.Key("accept"), 0.5f, Ctx.Icon);
            GameAudio.Event("gm_cursor_boss_accept");
            _clearAfterStarts = boss.InMove ? 1 : 2; // 今の技のあとの待ち時間か、次の技のあとの待ち時間を短くする
            FxKit.Text("Accepted", (Vector2)boss.BodyBounds.center + Vector2.up * (boss.BodyBounds.extents.y + 0.8f), 0.45f, Color.white, 40, 0.8f, CursorArt.Purple).Rise(0.4f);
        });
    }

    void OnMoveStarted(AttackData move)
    {
        if (_clearAfterStarts <= 0) return;
        _clearAfterStarts--;
        if (_clearAfterStarts == 0) StatMods.Boss.Wait.Clear(Ctx.Key("accept"));
    }
}

/// <summary>【仮】Tab の候補（灰色の点線の箱と Tab のキー。2026-11-19）。触ると Accept。時間が来たら消える（ボス戦ではボスへ飛ぶ）</summary>
public class CursorTabPickup : MonoBehaviour
{
    const int Order = 19;
    const float Life = 5f;
    const float FlyTime = 0.4f;
    static readonly Vector2 Size = new Vector2(1.2f, 1.2f);

    Transform _player;
    System.Action<CursorTabPickup> _onAccept;
    System.Action<CursorTabPickup> _onExpire;
    float _born;
    Vector2 _home;
    SpriteRenderer[] _srs;
    bool _done;
    BossController _flyTo;
    System.Action _arrived;
    float _flyStart;
    Vector2 _flyFrom;
    bool _hurryCued; // 消える1秒前の音を鳴らしたか

    public static CursorTabPickup Spawn(Vector2 pos, Transform player, System.Action<CursorTabPickup> onAccept, System.Action<CursorTabPickup> onExpire)
    {
        var go = new GameObject("CursorTabPickup");
        go.transform.position = pos;
        var p = go.AddComponent<CursorTabPickup>();
        p._player = player;
        p._onAccept = onAccept;
        p._onExpire = onExpire;
        p._born = Time.time;
        p._home = pos;
        CursorArt.Box(go.transform, Vector2.zero, Size, CursorArt.A(CursorArt.Ghost, 0.2f), Order, "Fill");
        CursorArt.DottedBox(go.transform, Vector2.zero, Size, CursorArt.A(CursorArt.Ghost, 0.95f), Order + 1);
        CursorArt.TabKey(go.transform, new Vector2(0f, Size.y * 0.5f + 0.5f), Order + 2);
        CursorArt.Text(go.transform, "{}", Vector2.zero, 0.45f, CursorArt.A(Color.white, 0.8f), Order + 2);
        p._srs = go.GetComponentsInChildren<SpriteRenderer>();
        FxKit.Ring(pos, 1.6f, 0.15f, CursorArt.Ghost);
        return p;
    }

    /// <summary>ボスへ飛ぶ（紫）。着いたら arrived</summary>
    public void FlyTo(BossController boss, System.Action arrived)
    {
        _flyTo = boss;
        _arrived = arrived;
        _flyStart = Time.time;
        _flyFrom = transform.position;
        foreach (var sr in _srs) if (sr != null) sr.color = new Color(CursorArt.Purple.r, CursorArt.Purple.g, CursorArt.Purple.b, sr.color.a);
    }

    void Update()
    {
        if (_flyTo != null)
        {
            float k = (Time.time - _flyStart) / FlyTime;
            Vector2 to = _flyTo.BodyBounds.center;
            if (k >= 1f)
            {
                FxKit.Ring(to, 2f, 0.2f, CursorArt.Purple);
                try { _arrived?.Invoke(); }
                catch (System.Exception ex) { Debug.LogException(ex); }
                Destroy(gameObject);
                return;
            }
            transform.position = Vector2.Lerp(_flyFrom, to, k * k);
            return;
        }
        if (_done) return;
        float e = Time.time - _born;
        transform.position = _home + Vector2.up * (Mathf.Sin(e * 3f) * 0.08f);
        float left = Life - e;
        bool on = left > 1f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        if (left <= 1f && !_hurryCued)
        {
            _hurryCued = true;
            GameAudio.Event("gm_cursor_tab_hurry");
        }
        foreach (var sr in _srs) if (sr != null) sr.enabled = on;
        if (Touching())
        {
            _done = true;
            Vector2 c = transform.position;
            FxKit.Ring(c, 2.4f, 0.2f, Color.white);
            FxKit.Text("Accepted", c + Vector2.up * 1.6f, 0.45f, Color.white, 40, 0.8f, CursorArt.Black).Rise(0.4f);
            GameAudio.Event("gm_cursor_accept");
            try { _onAccept?.Invoke(this); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            Destroy(gameObject);
            return;
        }
        if (left <= 0f)
        {
            _done = true;
            foreach (var sr in _srs) if (sr != null) sr.enabled = true;
            try { _onExpire?.Invoke(this); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            if (_flyTo == null) Destroy(gameObject);
        }
    }

    bool Touching()
    {
        if (_player == null) return false;
        Collider2D body = RewardKit.BodyOf(_player);
        Bounds me = new Bounds(transform.position, Size);
        if (body != null) return body.bounds.Intersects(me);
        return me.Contains(_player.position);
    }
}
