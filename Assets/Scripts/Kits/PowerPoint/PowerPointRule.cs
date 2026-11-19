using UnityEngine;

/// <summary>
/// 【仮】PowerPoint のステージのギミック「レーザーポインター」（2026-11-19。docs/app-kits-1-design.md 11章）。数値はすべて【仮】。
/// ・赤い点（直径 1.2 の輪と、まん中の点）が、いつも画面にいて、プレイヤーをゆっくり追う（毎秒 2.5マス）。点そのものは痛くない
/// ・12秒おき（ボス戦は 18秒おき）に「指される」：点が止まり、輪が縮む（予告 0.8秒）→ 点の高さに、スライド（3×2）が
///   プレイヤーから遠い方の端から横に「スライドイン」して、反対の端へ抜ける（2026-11-19：上から落とすのをやめた。
///   docs/stage-gimmick-variety-b.md 7章。入る端に矢印と赤い線。足場にはならない）
/// ・歩いていれば、点は追いつけない。立ち止まって攻撃し続けると、指される（点が重なったら、段を変えてよける）
/// ・ボス戦で出るのは1枚だけ。ボスの技の途中・場に残る技の間（ctx.CanSpawnHazard が false）は、止まらずに追うだけ
/// </summary>
public class PowerPointRule : StageRule
{
    public const float FollowSpeed = 2.5f;
    public const float Interval = 12f;
    public const float BossInterval = 18f;
    public const float Telegraph = 0.8f;
    public const float RingCells = 1.2f;
    /// <summary>スライドインの速さ（マス/秒）と大きさ【仮】</summary>
    public const float SlideSpeed = 14f;
    public static readonly Vector2 SlideSize = new Vector2(3f, 2f);

    Transform _dot;
    SpriteRenderer _ring;
    SpriteRenderer _glow;
    float _timer;
    float _pointUntil = -1f;
    Vector2 _pos;
    bool _placed;
    KitMotionMover _slide; // 来ているスライド（指すのをやめたら消す）

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
        CancelSlide();
    }

    public override void OnBossPhase(int phase)
    {
        _pointUntil = -1f;
        _timer = Mathf.Max(_timer, 4f);
        CancelSlide();
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
                GameAudio.Event("gm_powerpoint_click"); // スライドは指し始めに出してある（輪が縮みきると入ってくる）
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
        SlideIn();
    }

    Vector3 RingScale(float s)
    {
        Sprite sp = _ring != null ? _ring.sprite : null;
        float native = sp != null ? sp.bounds.size.x : 1f;
        float k = RingCells / Mathf.Max(0.01f, native) * s;
        return new Vector3(k, k, 1f);
    }

    /// <summary>
    /// スライドイン：点の高さ（床・足場の上なら、その上に乗る高さ）に、プレイヤーから遠い方の端からスライドが横切る。
    /// 予告は指している 0.8秒（輪）と同じ長さ（入る端に矢印と赤い線）なので、輪が縮みきった時に入ってくる
    /// </summary>
    void SlideIn()
    {
        AttackRunner owner = Ctx.Hazard;
        if (owner == null) return;
        Rect view = Ctx.PlayArea;
        float px = Ctx.Player != null ? Ctx.Player.position.x : _pos.x;
        KitSide side = px < view.center.x ? KitSide.Right : KitSide.Left;
        float y = _pos.y;
        if (owner.TryFindGround(_pos.x, _pos.y + 0.5f, out float ground, steadyOnly: true)) y = Mathf.Max(y, ground + SlideSize.y * 0.5f);
        y = Mathf.Clamp(y, view.yMin + SlideSize.y * 0.5f, view.yMax - SlideSize.y * 0.5f);
        _slide = GroupBMotion.SweepRect(Ctx, side, y, SlideSpeed, SlideSize, OfficeArt.Slide(Ctx.Theme), Color.white, Telegraph, objectName: "PPT_SlideIn");
    }

    void CancelSlide()
    {
        if (_slide != null) _slide.Cancel();
        _slide = null;
    }

    public override void End()
    {
        _placed = false;
        CancelSlide();
        if (_dot != null) Object.Destroy(_dot.gameObject);
        _dot = null;
    }
}
