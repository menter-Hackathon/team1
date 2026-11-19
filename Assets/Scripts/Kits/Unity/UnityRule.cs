using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity のステージの決まり「ランダムな座標に 3D の物体」（2026-11-19。docs/app-kits-2-design.md 2-1。まさぴ〜の依頼そのもの）。担当A。
/// ・ステージの間ずっと、7秒おき（強さ 3 は 5秒）に、ランダムな座標へ 3D の物体が1つ出て落ちる（技1 と同じ枠と「!」。予告 1.0秒）
/// ・落ちている間は当たる。雑魚にも当たる（倒れる。倒した数に入る）。止まったら 8秒残る（乗れる・壁）。同時に 4つまで
/// ・ボス戦も続く。ボスの弾は止まった物で止まる（かくれられる）
/// ・プレイヤーの 4マス以内には出さない。足場の上・部屋の両端 2マスには置かない。ボスの登場・50％・100％で全部消える（片づけの表）
/// ・飾り：右上に Scene ビューの軸の印、左上の Hierarchy に「Cube (3)」のような行が増える（UnityHud）
/// </summary>
public class UnityRule : StageRule
{
    const int MaxAlive = 4;
    const float Telegraph = 1.0f;
    const float Remain = 8f;
    const float NearPlayer = 4f;
    const float EdgeMargin = 2f;

    readonly List<Kit3DFaller> _alive = new List<Kit3DFaller>();
    UnityHud _hud;
    float _next;
    int _spawned;

    float Interval => Ctx.Strength >= 3 ? 5f : 7f;

    static AttackData _hazardData;

    /// <summary>ギミックの物のダメージ（ライフ1・ノックバックはボスの技と同じ）。名前は「Attack_Stage_」で始める（スキンの絵を付けない）</summary>
    static AttackData HazardData
    {
        get
        {
            if (_hazardData != null) return _hazardData;
            _hazardData = ScriptableObject.CreateInstance<AttackData>();
            _hazardData.name = "Attack_Stage_Unity_Object";
            _hazardData.hideFlags = HideFlags.DontSave;
            return _hazardData;
        }
    }

    public override void Begin(StageRuleContext ctx)
    {
        _hud = UnityHud.Ensure();
        _hud.ShowPanel = true;
        ctx.TrackDecor(_hud.gameObject);
        _next = StageRuleHost.StartQuiet + 1f;
    }

    public override void OnBossAppeared(BossController boss)
    {
        _alive.Clear();
        _next = Ctx.Elapsed + StageRuleHost.BossQuiet + 1f;
    }

    public override void OnBossPhase(int phase)
    {
        _alive.Clear();
        _next = Mathf.Max(_next, Ctx.Elapsed + StageRuleHost.TransitionQuiet + 1f);
    }

    public override void Tick(float dt)
    {
        _alive.RemoveAll(f => f == null);
        if (Ctx.Elapsed < _next) return;
        if (!Ctx.CanSpawnHazard || CountAlive() >= MaxAlive || Ctx.Hazard == null)
        {
            _next = Ctx.Elapsed + 0.5f; // 出せる時まで少しずつ待つ
            return;
        }
        if (TrySpawn()) _next = Ctx.Elapsed + Interval;
        else _next = Ctx.Elapsed + 0.5f;
    }

    int CountAlive()
    {
        int n = 0;
        foreach (var f in _alive) if (f != null) n++;
        return n;
    }

    bool TrySpawn()
    {
        Rect area = Ctx.PlayArea;
        Vector2 player = Ctx.Player != null ? (Vector2)Ctx.Player.position : area.center;
        Kit3DShape shape = (_spawned % 3) == 0 ? Kit3DShape.Cube : (_spawned % 3) == 1 ? Kit3DShape.Sphere : Kit3DShape.Capsule;
        Vector3 size = shape == Kit3DShape.Capsule ? new Vector3(1f, 2f, 1f) : Vector3.one * 1.6f;
        float half = size.x * 0.5f;
        for (int t = 0; t < 30; t++)
        {
            float x = Random.Range(area.xMin + EdgeMargin + half, area.xMax - EdgeMargin - half);
            if (Mathf.Abs(x - player.x) < NearPlayer + half) continue;
            float top = area.yMax - size.y * 0.5f - 0.6f;
            // 床（足場・置いた箱ではない）の上だけ
            if (!Kit3D.FindFloor(x, top, out float floor, false)) continue;
            // 横の幅ぶんも床か（端が足場にかからないか）
            if (!Kit3D.FindFloor(x - half * 0.9f, top, out float fl, false) || Mathf.Abs(fl - floor) > 0.05f) continue;
            if (!Kit3D.FindFloor(x + half * 0.9f, top, out float fr, false) || Mathf.Abs(fr - floor) > 0.05f) continue;
            float y = Mathf.Min(floor + Random.Range(5f, 8f), top);
            if (y - floor < size.y + 1.5f) continue;
            var landing = new Vector2(x, floor + size.y * 0.5f);
            if (Kit3D.BossOverlaps(landing, (Vector2)size + Vector2.one)) continue;
            if (OverlapsAlive(x, size.x)) continue;

            var opt = Kit3DFallOptions.Default;
            opt.Telegraph = Telegraph;
            opt.Remain = Remain;
            opt.HitZako = true;
            opt.LandOnPlatforms = false;
            opt.RollDistance = 0f; // 決まりの球は転がさない（置いた場所が分かるように）
            opt.Group = "unity.rule";
            opt.Cap = MaxAlive;
            Kit3DFaller f = Kit3D.Faller(Ctx.Hazard, HazardData, shape, new Vector2(x, y), size, UnityArt.Object, opt);
            if (f == null) return false;
            Ctx.TrackHazard(f.gameObject);
            _alive.Add(f);
            _hud.AddObject(shape == Kit3DShape.Cube ? "Cube" : shape == Kit3DShape.Sphere ? "Sphere" : "Capsule");
            GameAudio.Event("gm_unity_object_warn");
            _spawned++;
            return true;
        }
        return false;
    }

    bool OverlapsAlive(float x, float w)
    {
        foreach (var f in _alive)
        {
            if (f == null) continue;
            if (Mathf.Abs(f.transform.position.x - x) < (f.BoxSize.x + w) * 0.5f + 0.4f) return true;
        }
        return false;
    }

    public override void End()
    {
        foreach (var f in _alive) if (f != null) Object.Destroy(f.gameObject);
        _alive.Clear();
        if (_hud != null)
        {
            _hud.ShowPanel = false;
            Object.Destroy(_hud.gameObject);
            _hud = null;
        }
    }
}
