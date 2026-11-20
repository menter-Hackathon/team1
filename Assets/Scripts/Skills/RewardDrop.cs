using System;
using UnityEngine;

/// <summary>
/// 【仮】ボスが落とすスキル（2026-11-20：docs/window-stage-and-pickup-design.md 2-2。担当 C）。
/// ・追い出したウイルスが倒れた所（無ければボスの体のまん中）から、正常化の 0.5秒 後に上へはねて、ボス部屋のまん中の方の床へ落ちる。
///   床から 0.7 の高さでふわふわ浮く
/// ・白い角の丸い板（ふちはアプリの色。赤は使わない）に、スキルのアイコン。右下に元のアプリの小さいアイコン。まわりに小さい光が上へ流れる
/// ・落ちてから 0.6秒 たつと、プレイヤーの体が 0.7 以内に来たら拾う → 入れ替えの画面（SkillSwapScreen）
/// ・出てから 6秒 拾わなければプレイヤーへ飛んで来る（地形は無視）。12秒 で必ず拾ったことにする
/// ・出てから画面を閉じるまで、プレイヤーはダメージを受けない（Health.RewardGrace）
/// 画面を閉じたら onDone を 1回だけ呼ぶ（StageDirector が CompleteStage）。Cancel では呼ばない。
/// 中で例外が出ても止まらない：空いた枠に入れて（無ければ入れない）onDone へ進む。数値はすべて【仮】
/// </summary>
public class RewardDrop : MonoBehaviour
{
    /// <summary>false にすると、今までどおり（正常化の瞬間に手に入り、少し待って次へ）。StageDirector が Awake で見る</summary>
    public static bool Enabled = true;

    const float AppearDelay = 0.5f;   // 正常化から出るまで
    const float BounceSpeed = 6f;     // 上へはねる速さ
    const float Gravity = 22f;
    const float TowardCenter = 1.5f;  // ボス部屋のまん中の方へ寄る
    const float ArenaMargin = 1.5f;   // ボス部屋の左右の端から内側へ
    const float RestHeight = 0.7f;    // 床からの高さ
    const float BobAmp = 0.12f;
    const float BobHz = 1.5f;
    const float PickDelay = 0.6f;     // 出てから拾えるまで
    const float PickRadius = 0.7f;
    const float FlyAfter = 6f;        // Spawn から数える
    const float AutoPickAt = 12f;     // Spawn から数える
    const float FlySpeed = 8f;
    const float PlatePx = 22f;        // 板の大きさ（ドット。1マス＝18ドット）
    const float IconPx = 16f;
    const float BadgePx = 10f;

    enum State { Waiting, Bouncing, Resting, Flying, Picked, Done }

    /// <summary>落ちている間だけ（拾うと null。画面を閉じるまでは、拾った物はかくれて残る）</summary>
    public static RewardDrop Current { get; private set; }
    static RewardDrop _live; // 拾った後も、画面を閉じるまでの物

    SkillDefinition _reward;
    PlayerSkills _player;
    Sprite _appIcon;
    Color _edge;
    Action _onDone;
    State _state;
    float _age;
    Vector2 _from;
    float _targetX, _restY, _vx, _vy;
    Transform _art;
    float _nextSpark;
    bool _logged;

    public SkillDefinition Skill => _reward;
    /// <summary>拾える（出てから 0.6秒 たった）</summary>
    public bool CanPick => (_state == State.Bouncing || _state == State.Resting || _state == State.Flying) && _age >= AppearDelay + PickDelay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
        _live = null;
        Enabled = true;
        Health.RewardGrace = false;
    }

    /// <summary>
    /// スキルを落とす（ボスが正常化した瞬間に呼ぶ）。出る位置は、ここで覚える（このあとウイルスは消えるかもしれない）。
    /// reward が無ければ null（落とさない）
    /// </summary>
    public static RewardDrop Spawn(BossController boss, PlayerSkills player, SkillDefinition reward, Sprite appIcon, AppTheme theme, Action onDone)
    {
        if (reward == null) return null;
        if (_live != null) Cancel();

        Vector2 from;
        Health virus = boss != null ? boss.EjectedVirus : null;
        if (virus != null) from = (Vector2)virus.transform.position + Vector2.up * 0.3f;
        else if (boss != null) from = boss.BodyBounds.center;
        else from = player != null ? (Vector2)player.transform.position + Vector2.up : Vector2.zero;

        var go = new GameObject("RewardDrop");
        go.transform.position = from;
        var drop = go.AddComponent<RewardDrop>();
        drop._reward = reward;
        drop._player = player;
        drop._appIcon = appIcon;
        drop._edge = EdgeColor(theme, reward);
        drop._onDone = onDone;
        drop._from = from;
        drop._state = State.Waiting;
        Current = drop;
        _live = drop;
        Health.RewardGrace = true; // 拾って画面を閉じるまで、プレイヤーはダメージを受けない
        return drop;
    }

    /// <summary>【確認用】触ったことにする（画面が開く）。拾えるまでの待ちは無視</summary>
    public void PickNow()
    {
        if (_state == State.Picked || _state == State.Done) return;
        try { Pick(); }
        catch (Exception e) { Fail(e); }
    }

    /// <summary>落ちたスキルを消す（ライフ 0 のとき）。開いている画面も閉じる。onDone は呼ばない</summary>
    public static void Cancel()
    {
        Health.RewardGrace = false;
        if (SkillSwapScreen.Current != null) SkillSwapScreen.Current.Abort();
        RewardDrop d = _live != null ? _live : Current;
        _live = null;
        Current = null;
        if (d != null)
        {
            d._state = State.Done;
            d._onDone = null;
            Destroy(d.gameObject);
        }
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (_live == this) _live = null;
        if (_live == null) Health.RewardGrace = false;
    }

    // ───────── 動き ─────────

    void Update()
    {
        if (_state == State.Picked && SkillSwapScreen.Current == null)
        {
            // 画面が onClosed を呼ばずに消えた（外から Abort など）：止まったままにしない
            SkillSwapScreen.ApplyFallback(_player != null ? _player.Loadout : null, _reward);
            OnScreenClosed();
            return;
        }
        if (_state == State.Picked || _state == State.Done) return;
        try { Tick(Time.deltaTime); }
        catch (Exception e) { Fail(e); }
    }

    void Tick(float dt)
    {
        _age += dt;
        if (_state == State.Waiting)
        {
            if (_age < AppearDelay) return;
            Appear();
        }
        if (_age >= AutoPickAt)
        {
            Pick();
            return;
        }
        if (_state != State.Flying && _age >= FlyAfter) _state = State.Flying;

        Vector3 pos = transform.position;
        switch (_state)
        {
            case State.Bouncing:
                _vy -= Gravity * dt;
                pos.y += _vy * dt;
                pos.x = Mathf.MoveTowards(pos.x, _targetX, _vx * dt);
                if (_vy < 0f && pos.y <= _restY)
                {
                    pos = new Vector3(_targetX, _restY, 0f);
                    _state = State.Resting;
                    FxKit.Fragments((Vector2)pos + Vector2.down * 0.5f, Vector2.up, 3, new[] { FxArt.Square(2) }, new[] { Color.white, _edge });
                }
                transform.position = pos;
                SetBob(0f);
                break;
            case State.Resting:
                SetBob(Mathf.Sin((_age - AppearDelay) * Mathf.PI * 2f * BobHz) * BobAmp);
                break;
            case State.Flying:
                SetBob(0f);
                if (TryPlayer(out Transform player))
                {
                    Vector2 target = RewardKit.BodyCenter(player);
                    transform.position = Vector2.MoveTowards(pos, target, FlySpeed * dt);
                }
                break;
        }

        Sparkle();
        if (CanPick && Touching()) Pick();
    }

    void Appear()
    {
        BuildArt();
        FindLanding(out _targetX, out _restY);
        float y0 = _from.y;
        float disc = BounceSpeed * BounceSpeed + 2f * Gravity * (y0 - _restY);
        if (disc < 0f) { y0 = _restY; disc = BounceSpeed * BounceSpeed; } // 床より下から出た：床の高さからはねる
        float airTime = (BounceSpeed + Mathf.Sqrt(disc)) / Gravity;
        _vx = Mathf.Abs(_targetX - _from.x) / Mathf.Max(0.1f, airTime);
        _vy = BounceSpeed;
        transform.position = new Vector3(_from.x, y0, 0f);
        _state = State.Bouncing;
        _nextSpark = _age;
    }

    // ボス部屋のまん中の方へ 1.5 寄った所の、床・動かない足場の上
    void FindLanding(out float x, out float restY)
    {
        BattleArena arena = BattleArena.Current != null ? BattleArena.Current : FindObjectOfType<BattleArena>();
        x = _from.x;
        if (arena != null)
        {
            Rect b = arena.Bounds;
            x = Mathf.MoveTowards(_from.x, b.center.x, TowardCenter);
            if (b.width > ArenaMargin * 2f) x = Mathf.Clamp(x, b.xMin + ArenaMargin, b.xMax - ArenaMargin);
        }
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        float[] starts = { 0.3f, 1.2f, 2.4f };
        foreach (float up in starts)
        {
            RaycastHit2D hit = AppStagePlatforms.RaycastSteady(new Vector2(x, _from.y + up), Vector2.down, 40f, mask);
            if (hit.collider != null && hit.distance > 0.01f)
            {
                restY = hit.point.y + RestHeight;
                return;
            }
        }
        // 床が見つからない：ボス部屋の床、無ければプレイヤーの足もと
        if (arena != null) restY = arena.Bounds.yMin + RestHeight;
        else if (TryPlayer(out Transform p)) { x = p.position.x; restY = p.position.y + RestHeight; }
        else restY = _from.y;
    }

    void SetBob(float offset)
    {
        if (_art != null) _art.localPosition = new Vector3(0f, Mathf.Round(offset * 18f) / 18f, 0f);
    }

    // 白とアプリの色の小さい光が、上へ流れる
    void Sparkle()
    {
        if (_art == null || _age < _nextSpark) return;
        _nextSpark = _age + 0.12f;
        Vector2 at = (Vector2)_art.position + new Vector2(UnityEngine.Random.Range(-0.55f, 0.55f), UnityEngine.Random.Range(-0.5f, 0.1f));
        Color c = UnityEngine.Random.value < 0.5f ? Color.white : _edge;
        int order = UnityEngine.Random.value < 0.5f ? RewardKit.OrderPickup - 1 : RewardKit.OrderPickup + 4;
        FxAnim.Sprite(FxArt.Square(UnityEngine.Random.value < 0.7f ? 1 : 2), at, order, 0.7f, c, 1f, "RewardSpark").Rise(0.7f).FadeFrom(0.4f);
    }

    // ───────── 拾う ─────────

    bool TryPlayer(out Transform player)
    {
        player = _player != null ? _player.transform : null;
        return player != null;
    }

    bool PlayerDead()
    {
        if (!TryPlayer(out Transform p)) return false;
        Health h = p.GetComponent<Health>();
        return h != null && h.IsDead;
    }

    bool Touching()
    {
        if (!TryPlayer(out Transform player) || _art == null) return false;
        Vector2 c = _art.position;
        Collider2D body = RewardKit.BodyOf(player);
        if (body != null)
        {
            Bounds b = body.bounds;
            return b.SqrDistance(new Vector3(c.x, c.y, b.center.z)) <= PickRadius * PickRadius;
        }
        return Vector2.Distance(c, RewardKit.BodyCenter(player)) <= PickRadius + 0.5f;
    }

    void Pick()
    {
        if (_state == State.Picked || _state == State.Done) return;
        if (PlayerDead()) return; // 倒れた：StageDirector が Cancel する（止まった画面を出さない）
        if (_art == null) BuildArt();
        _state = State.Picked;
        if (Current == this) Current = null;
        GameAudio.Event("skill_pickup");
        if (_art != null) _art.gameObject.SetActive(false); // 画面を閉じるまで、かくれて残る（ダメージを受けない間）

        SkillSwapScreen screen = null;
        try { screen = SkillSwapScreen.Open(_reward, _player, _appIcon, OnScreenClosed); }
        catch (Exception e) { Debug.LogException(e); }
        if (screen == null && _state != State.Done)
        {
            // 画面を開けなかった：空いた枠に入れて（無ければ入れない）先へ進む
            SkillSwapScreen.ApplyFallback(_player != null ? _player.Loadout : null, _reward);
            OnScreenClosed();
        }
    }

    void OnScreenClosed()
    {
        if (_state == State.Done) return;
        _state = State.Done;
        if (Current == this) Current = null;
        if (_live == this) _live = null;
        Health.RewardGrace = false;
        Action done = _onDone;
        _onDone = null;
        try { done?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        Destroy(gameObject);
    }

    void Fail(Exception e)
    {
        if (!_logged)
        {
            _logged = true;
            Debug.LogError($"落ちたスキルで例外。画面を開いて先へ進みます：{e}");
        }
        if (_state == State.Picked || _state == State.Done) return;
        if (PlayerDead()) return;
        try { Pick(); }
        catch (Exception e2) { Debug.LogException(e2); }
        if (_state != State.Picked && _state != State.Done)
        {
            SkillSwapScreen.ApplyFallback(_player != null ? _player.Loadout : null, _reward);
            OnScreenClosed();
        }
    }

    // ───────── 見た目 ─────────

    void BuildArt()
    {
        if (_art != null) return;
        var art = new GameObject("Art");
        art.transform.SetParent(transform, false);
        _art = art.transform;
        int order = RewardKit.OrderPickup;
        SpriteRenderer edge = RuntimeSprites.Show(_art, MacUi.RoundRectSprite((int)PlatePx, (int)PlatePx, 3), Vector2.zero, order, 1f, "Edge");
        edge.color = _edge;
        RuntimeSprites.Show(_art, MacUi.RoundRectSprite((int)PlatePx - 2, (int)PlatePx - 2, 2), Vector2.zero, order + 1, 1f, "Plate");
        Sprite icon = _reward.Icon != null ? _reward.Icon : _appIcon;
        if (icon != null) Fit(RuntimeSprites.Show(_art, icon, Vector2.zero, order + 2, 1f, "Icon"), IconPx);
        if (_reward.Icon != null && _appIcon != null)
        {
            var at = new Vector2((PlatePx * 0.5f - 2f) / 18f, -(PlatePx * 0.5f - 2f) / 18f);
            SpriteRenderer badge = RuntimeSprites.Show(_art, MacUi.RoundRectSprite((int)BadgePx, (int)BadgePx, 2), at, order + 3, 1f, "BadgePlate");
            badge.color = Color.white;
            Fit(RuntimeSprites.Show(_art, _appIcon, at, order + 4, 1f, "Badge"), BadgePx - 2f);
        }
    }

    // 絵を px ドット（1マス＝18ドット）の大きさにそろえる（アイコンの絵の大きさ・PPU はまちまち）
    static void Fit(SpriteRenderer sr, float px)
    {
        if (sr == null || sr.sprite == null) return;
        Vector3 size = sr.sprite.bounds.size;
        float m = Mathf.Max(size.x, size.y);
        if (m <= 0f) return;
        float s = px / 18f / m;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    // ふちの色：アプリの色。赤っぽいとき（赤＝危ない）は、ほかの色か青
    static Color EdgeColor(AppTheme theme, SkillDefinition reward)
    {
        AppTheme t = theme != null ? theme : reward != null ? reward.Theme : null;
        if (t != null)
        {
            if (!IsReddish(t.Main)) return t.Main;
            foreach (Color c in t.Accents)
                if (!IsReddish(c)) return c;
        }
        return MacDesktopGui.Blue;
    }

    static bool IsReddish(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return s > 0.45f && v > 0.3f && (h < 0.05f || h > 0.93f);
    }
}
