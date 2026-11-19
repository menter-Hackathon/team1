using UnityEngine;

/// <summary>
/// 【仮】PowerPoint のステージのギミック「レーザーポインター」（2026-11-19。docs/app-kits-1-design.md 11章）。数値はすべて【仮】。
/// ・赤い点（直径 1.2 の輪と、まん中の点）が、いつも画面にいて、プレイヤーをゆっくり追う（毎秒 2.5マス）。点そのものは痛くない
/// ・12秒おき（ボス戦は 18秒おき）に「指される」：点が止まり、輪が縮む（予告 0.8秒）→ その場所にスライドが1枚落ちる（3×2）
/// ・歩いていれば、点は追いつけない。立ち止まって攻撃し続けると、指される
/// ・ボス戦で落ちるのは1枚だけ。ボスの技の途中・場に残る技の間（ctx.CanSpawnHazard が false）は、止まらずに追うだけ
/// </summary>
public class PowerPointRule : StageRule
{
    public const float FollowSpeed = 2.5f;
    public const float Interval = 12f;
    public const float BossInterval = 18f;
    public const float Telegraph = 0.8f;
    public const float RingCells = 1.2f;

    Transform _dot;
    SpriteRenderer _ring;
    SpriteRenderer _glow;
    float _timer;
    float _pointUntil = -1f;
    Vector2 _pos;
    bool _placed;

    /// <summary>点の位置（テスト用）</summary>
    public Vector2 DotPosition => _pos;

    public override void Begin(StageRuleContext ctx)
    {
        var go = new GameObject("PPT_LaserPointer");
        ctx.TrackDecor(go);
        _dot = go.transform;
        _glow = AttackRunner.AddVisual(_dot, FxArt.Disc(9), OfficeKitTools.Fade(PowerPointArt.Pointer, 0.35f), Vector2.one * 0.5f, 58, "Glow");
        _ring = AttackRunner.AddVisual(_dot, PowerPointArt.PointerRing(22), Color.white, Vector2.one * RingCells, 59, "Ring");
        _pos = ctx.Player != null ? (Vector2)ctx.Player.position + new Vector2(-3f, 2f) : ctx.PlayArea.center;
        _dot.position = _pos;
        _placed = true;
        _timer = Interval;
    }

    public override void OnBossAppeared(BossController boss)
    {
        _timer = BossInterval;
        _pointUntil = -1f;
    }

    public override void OnBossPhase(int phase)
    {
        _pointUntil = -1f;
        _timer = Mathf.Max(_timer, 4f);
    }

    public override void Tick(float dt)
    {
        if (!_placed || _dot == null || Ctx == null) return;
        Vector2 target = Ctx.Player != null ? (Vector2)Ctx.Player.position + new Vector2(0f, 0.5f) : _pos;

        if (_pointUntil > 0f)
        {
            // 指している：止まって、輪が縮む
            float left = _pointUntil - Time.time;
            float k = Mathf.Clamp01(left / Telegraph);
            _ring.transform.localScale = RingScale(0.45f + 0.55f * k);
            _ring.enabled = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            if (left <= 0f)
            {
                _pointUntil = -1f;
                _ring.transform.localScale = RingScale(1f);
                _ring.enabled = true;
                GameAudio.Event("gm_powerpoint_click");
                DropSlide();
                _timer = Ctx.InBossFight ? BossInterval : Interval;
            }
            return;
        }

        _pos = Vector2.MoveTowards(_pos, target, FollowSpeed * dt);
        Rect view = Ctx.PlayArea;
        _pos.x = Mathf.Clamp(_pos.x, view.xMin + 0.6f, view.xMax - 0.6f);
        _dot.position = _pos;
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 8f);
        _ring.transform.localScale = RingScale(pulse);

        _timer -= dt;
        if (_timer > 0f) return;
        if (!Ctx.CanSpawnHazard || (Ctx.InBossFight && Ctx.HazardCount > 0)) { _timer = 0.5f; return; } // 出せないときは、追うだけ
        _pointUntil = Time.time + Telegraph;
        GameAudio.Event("gm_powerpoint_point");
    }

    Vector3 RingScale(float s)
    {
        Sprite sp = _ring != null ? _ring.sprite : null;
        float native = sp != null ? sp.bounds.size.x : 1f;
        float k = RingCells / Mathf.Max(0.01f, native) * s;
        return new Vector3(k, k, 1f);
    }

    void DropSlide()
    {
        AttackRunner owner = Ctx.Hazard;
        if (owner == null) return;
        AttackData slide = SlideData();
        owner.TryFindGround(_pos.x, _pos.y + 0.5f, out float ground);
        Rect view = Ctx.PlayArea;
        float x = Mathf.Clamp(_pos.x, view.xMin + 1.5f, view.xMax - 1.5f);
        float hang = Mathf.Min(view.yMax + 1f, ground + 5f);
        float now = Time.time;
        // 影の予告（指していた 0.8秒のあとに、短い距離を速く落ちる）
        FallingObject.Spawn(owner, slide, new Vector2(x, hang), now, now + 0.25f, true);
        owner.NotifyReleased(slide);
    }

    /// <summary>落ちるスライド（3×2。着地後 2秒、足場として残る）。名前の ErrorWindowFall で Office のスキンのスライドの絵</summary>
    static AttackData SlideData() => OfficeKitTools.RuntimeData("Attack_PowerPoint_Pointer_ErrorWindowFall",
        "{\"_kind\":2,\"_damage\":1,\"_knockback\":{\"x\":6,\"y\":5},\"_hitStun\":0.25,\"_activeTime\":0.1," +
        "\"_placement\":0,\"_spawnCount\":1,\"_objectSize\":{\"x\":3,\"y\":2},\"_fallSpeed\":18,\"_landRemainTime\":2," +
        "\"_becomesPlatform\":true,\"_telegraphTime\":0.8,\"_color\":{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}");

    public override void End()
    {
        _placed = false;
        if (_dot != null) Object.Destroy(_dot.gameObject);
        _dot = null;
    }
}
