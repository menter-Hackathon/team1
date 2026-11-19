using UnityEngine;

/// <summary>
/// 【仮】Blender のステージの決まり「Default Cube」（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// ・ステージの最初から、画面のまん中（ボス戦は部屋のまん中 x＝0）の床の上に 3D の立方体（2×2マス。乗れる・壁になる）がある
/// ・近接（能力も）3回で消せる。`Delete` の小さいメニューが出て消える。消すと見返り：雑魚の時間は拾える物（盾か回復）、
///   ボス戦は能力のクールタイムが全部戻る（チャージ）（拾える物はボス戦では消える決まりなので）
/// ・20秒で、また現れる（プレイヤーが重なっていたら、どくまで待つ。オレンジの点線の四角が 1.0秒で広がる → 奥から大きくなって出る（2026-11-19：Pop の出方）。当たらない）
/// ・ボス戦：あるままだと、ボスの弾を止める壁になる。消すと見返り。どちらが得かを選ぶ
/// ・飾り：右上に軸の印（BlenderHud）。床の格子は割り切って出さない
/// </summary>
public class BlenderRule : StageRule
{
    const float Size = 2f;
    const float Respawn = 20f;
    const float FrameTime = 1f;

    Kit3DFaller _cube;
    GameObject _frame;
    BlenderHud _hud;
    float _respawnAt;
    float _frameEnd = -1f;
    Vector2 _spot;

    public override void Begin(StageRuleContext ctx)
    {
        _hud = BlenderHud.Ensure();
        _hud.ShowAxes = true;
        ctx.TrackDecor(_hud.gameObject);
        _respawnAt = 0f; // 最初から（置いてよくなったらすぐ）
    }

    public override void OnBossAppeared(BossController boss)
    {
        _cube = null;
        _frame = null;
        _frameEnd = -1f;
        _respawnAt = Ctx.Elapsed; // 置いてよくなったらすぐ
    }

    public override void OnBossPhase(int phase)
    {
        _cube = null;
        _frame = null;
        _frameEnd = -1f;
        _respawnAt = Ctx.Elapsed;
    }

    public override void Tick(float dt)
    {
        if (_cube != null) return;
        float now = Ctx.Elapsed;
        if (_frameEnd >= 0f)
        {
            if (now < _frameEnd) return;
            _frameEnd = -1f;
            if (_frame != null) Object.Destroy(_frame);
            if (Kit3D.BodyOverlaps(_spot, Vector2.one * Size)) { _respawnAt = now + 0.3f; return; }
            SpawnCube();
            return;
        }
        if (now < _respawnAt || !Ctx.CanPlace) return;
        if (!PickSpot(out _spot)) { _respawnAt = now + 1f; return; }
        if (Kit3D.BodyOverlaps(_spot, Vector2.one * Size)) { _respawnAt = now + 0.3f; return; }
        // 出る場所にオレンジの点線の四角（当たらないので赤にしない）。小さい四角が、立方体の大きさへ広がる（2026-11-19：Pop の出方。S で大きくする見た目）
        _frame = BlenderGrowFrame.Create(null, _spot, 0.5f, Size + 0.3f, FrameTime, BlenderArt.Select).gameObject;
        Ctx.TrackPlaced(_frame);
        _frameEnd = now + FrameTime;
        GameAudio.Event("gm_blender_frame");
    }

    bool PickSpot(out Vector2 spot)
    {
        Rect area = Ctx.PlayArea;
        float cx = Ctx.InBossFight ? Ctx.Arena.center.x : area.center.x;
        for (int i = 0; i < 9; i++)
        {
            float x = cx + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 1.5f;
            if (x < area.xMin + 2f + Size * 0.5f || x > area.xMax - 2f - Size * 0.5f) continue;
            float top = area.yMax - 1f;
            if (!Kit3D.FindFloor(x, top, out float floor, false)) continue;
            if (!Kit3D.FindFloor(x - Size * 0.45f, top, out float fl, false) || Mathf.Abs(fl - floor) > 0.05f) continue;
            if (!Kit3D.FindFloor(x + Size * 0.45f, top, out float fr, false) || Mathf.Abs(fr - floor) > 0.05f) continue;
            spot = new Vector2(x, floor + Size * 0.5f);
            if (Kit3D.BossOverlaps(spot, Vector2.one * (Size + 0.5f))) continue;
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    void SpawnCube()
    {
        var opt = Kit3DFallOptions.Default;
        opt.Telegraph = 0f;
        opt.Remain = 100000f;
        opt.BreakHits = 3;
        opt.LandOnPlatforms = false;
        opt.Group = "blender.rule";
        opt.Cap = 2;
        opt.LandedShade = 1f;
        _cube = Kit3D.Faller(null, null, Kit3DShape.Cube, _spot + new Vector2(0f, 0.05f), Vector3.one * Size, BlenderArt.Object, opt);
        _cube.Visual.AddOutline(BlenderArt.Select, 0.1f);
        BlenderPopIn.Attach(_cube); // 奥から大きくなって出る（見た目だけ。当たり・置く場所は今のまま）
        _cube.Broken += OnBroken;
        Ctx.TrackPlaced(_cube.gameObject);
        GameAudio.Event("gm_blender_cube");
    }

    void OnBroken(Kit3DFaller f)
    {
        Vector2 at = f.transform.position;
        BlenderHud.DeleteMenu(at + new Vector2(0f, Size * 0.5f + 0.4f), 0.7f);
        GameAudio.Event("gm_blender_delete", Ctx.InBossFight ? 1.12f : 1f); // ボス戦は少し高く
        if (Ctx.InBossFight) KitPlayer.RefillCooldowns();
        else
        {
            float px = Ctx.Player != null ? Ctx.Player.position.x : at.x;
            Pickup.Drop(at + Vector2.up * 0.5f, Random.value < 0.5f ? PickupKind.Shield : PickupKind.Heal, px);
        }
        _cube = null;
        _respawnAt = Ctx.Elapsed + Respawn;
    }

    public override void End()
    {
        if (_cube != null) Object.Destroy(_cube.gameObject);
        if (_frame != null) Object.Destroy(_frame);
        _cube = null;
        _frame = null;
        if (_hud != null)
        {
            _hud.ShowAxes = false;
            Object.Destroy(_hud.gameObject);
            _hud = null;
        }
    }
}
