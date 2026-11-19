using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Microsoft Teams のステージのギミック「ブレイクアウトルーム」（場面③の出来事。2026-11-19。docs/app-kits-1-design.md 17章・E4）。
/// ・始まり：右上の通知 Breakout rooms。部屋のまん中に赤い点線（1.0秒）→ 紫の壁（幅 0.6×高さ 4）が床から上がる。
///   プレイヤーが重なっていたら、近い側へ押し出す
/// ・部屋が左右 2つに分かれる。雑魚は両方の部屋に出る（出来事が自分で出す）。壁は足場からジャンプすれば越えられる（床からは越えられない）
/// ・12秒で Rooms closing：壁が 1.5秒点滅して、下がって消える
/// ・壁は地形なので、終わり（End・ボスの登場）で必ず消す（Track）。ボス戦にはかからない
/// </summary>
public class TeamsBreakoutEvent : PaceEvent
{
    const float WallWidth = 0.6f;
    const float WallHeight = 4f;
    const float MarkTime = 1f;
    const float RiseTime = 0.35f;
    const float OpenTime = 12f;
    const float BlinkTime = 1.5f;
    const float LowerTime = 0.4f;
    const float SpawnStep = 0.6f;
    const float MinFromPlayer = 4f;

    enum Step { Mark, Rise, Open, Blink, Lower, Done }

    Step _step = Step.Mark;
    float _clock, _stepAt;
    float _wallX, _floorY;
    TeamsWall _wall;
    readonly List<Health> _mine = new List<Health>();
    int _spawned;
    float _nextSpawn;
    int _side = 1;
    bool _exhausted;

    public TeamsBreakoutEvent()
    {
        Toast = "Breakout rooms";
    }

    public override bool SpawnsOwnZako => true;
    protected override bool NeedsRunner => false;
    public override float MaxSeconds => MarkTime + RiseTime + OpenTime + BlinkTime + LowerTime + 1.5f;

    protected override void OnBegin()
    {
        Rect a = Area;
        _wallX = a.center.x;
        _floorY = FloorY(a);
        _wall = TeamsWall.Create(new Vector2(_wallX, _floorY), new Vector2(WallWidth, WallHeight));
        Track(_wall.gameObject);
        GameAudio.Event("gm_teams_breakout_mark");
        _stepAt = 0f;
    }

    protected override bool OnTick(float dt)
    {
        _clock += dt;
        float t = _clock - _stepAt;
        switch (_step)
        {
            case Step.Mark:
                if (t >= MarkTime) Next(Step.Rise);
                break;
            case Step.Rise:
                PushPlayerOut(); // 上がっている間、重なったら押し出す（壁の中に埋まらない）
                _wall.SetHeight(Mathf.Clamp01(t / RiseTime) * WallHeight);
                if (t >= RiseTime) { _wall.SetHeight(WallHeight); Next(Step.Open); }
                break;
            case Step.Open:
                SpawnZako();
                if (t >= OpenTime)
                {
                    Next(Step.Blink);
                    KitToast.Show("Rooms closing", TeamsArt.Icon, (Color)TeamsArt.Purple);
                    GameAudio.Event("gm_teams_closing");
                }
                break;
            case Step.Blink:
                _wall.Blink(true);
                if (t >= BlinkTime) { _wall.Blink(false); Next(Step.Lower); GameAudio.Event("gm_teams_wall_down"); }
                break;
            case Step.Lower:
                _wall.SetHeight((1f - Mathf.Clamp01(t / LowerTime)) * WallHeight);
                if (t >= LowerTime)
                {
                    _wall.SetHeight(0f);
                    Next(Step.Done);
                }
                break;
            case Step.Done:
                return false;
        }
        return true;
    }

    void Next(Step s)
    {
        _step = s;
        _stepAt = _clock;
        if (s == Step.Rise)
        {
            _wall.ShowMark(false);
            GameAudio.Event("gm_teams_wall_up");
        }
    }

    /// <summary>壁が上がる所にプレイヤーが重なっていたら、近い側へ押し出す</summary>
    void PushPlayerOut()
    {
        Transform p = Pacer != null ? Pacer.Player : null;
        if (p == null || !TalkKitUtil.PlayerBody(p, out Bounds b)) return;
        float half = WallWidth * 0.5f;
        if (b.max.x < _wallX - half || b.min.x > _wallX + half) return;
        if (b.min.y > _floorY + WallHeight) return;
        float side = b.center.x >= _wallX ? 1f : -1f;
        float dx = side > 0 ? (_wallX + half + 0.05f) - b.min.x : (_wallX - half - 0.05f) - b.max.x;
        Vector2 to = (Vector2)p.position + new Vector2(dx, 0f);
        if (p.TryGetComponent(out KinematicBody2D body)) body.Warp(to);
        else p.position = to;
    }

    /// <summary>両方の部屋に、交互に雑魚を出す（プレイヤーから 4マス以上離す）</summary>
    void SpawnZako()
    {
        _mine.RemoveAll(h => h == null || h.IsDead);
        if (_exhausted || _spawned >= Budget || _clock < _nextSpawn) return;
        ZakoSpawner spawner = Pacer != null ? Pacer.Spawner : null;
        if (spawner == null) { _exhausted = true; return; }
        Rect a = Area;
        Vector2 player = PlayerPos;
        for (int tries = 0; tries < 2; tries++)
        {
            int side = tries == 0 ? _side : -_side;
            float xMin = side > 0 ? _wallX + 1.2f : a.xMin + 1.2f;
            float xMax = side > 0 ? a.xMax - 1.2f : _wallX - 1.2f;
            for (int i = 0; i < 6; i++)
            {
                float x = Random.Range(xMin, xMax);
                if (Mathf.Abs(x - player.x) < MinFromPlayer) continue;
                int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, a.yMax - 0.5f), Vector2.down, a.height, mask);
                if (hit.collider == null || hit.distance < 0.2f) continue;
                if (_wall != null && hit.collider.gameObject == _wall.gameObject) continue;
                Health h = spawner.SpawnNow(0, hit.point);
                if (h == null) { _exhausted = true; return; }
                _mine.Add(h);
                _spawned++;
                _side = -side;
                _nextSpawn = _clock + SpawnStep;
                return;
            }
        }
        _nextSpawn = _clock + 0.2f; // 出せる所が無い：少し待つ
    }
}
