using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】fork() &amp; の進行（2026-11-18。docs/app-boss-skills-design.md 4-3）。TerminalForkAttack が出す。
/// 出る場所の予告（うすい枠）→ 分身（TerminalGhost、子）を出す → 左から順に光って撃つ（2周）→ 残った分身が文字になって消える。
/// 分身を消されると、ボスのゲージを増やす。形態変化・正常化でこの GameObject ごと消えると、分身も消える（弾は形態変化の弾の片づけで消える）。
/// 出ている間：場に残る技の効果（Lingers）
/// </summary>
public class TerminalForkEffect : CustomEffect
{
    const int FrameOrder = 20;

    public override bool Lingers => true;

    AttackRunner _owner;
    AttackData _data;
    TerminalForkAttack _cfg;
    float _start;
    float _firstShotAt;
    int _shotsFired;
    int _totalShots;
    bool _appeared;
    readonly List<Vector2> _points = new List<Vector2>();
    readonly List<SpriteRenderer> _frames = new List<SpriteRenderer>();
    readonly List<TerminalGhost> _ghosts = new List<TerminalGhost>();

    public static TerminalForkEffect Spawn(AttackRunner owner, AttackData data, TerminalForkAttack cfg)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalFork", Vector2.zero);
        var fx = go.AddComponent<TerminalForkEffect>();
        fx._owner = owner;
        fx._data = data;
        fx._cfg = cfg;
        fx._start = Time.time;
        fx._points.AddRange(fx.PickPoints());
        // 出る場所の予告：分身の大きさの、うすい緑の枠
        Vector2 size = TerminalGhost.Size;
        foreach (Vector2 p in fx._points)
        {
            SpriteRenderer frame = AttackRunner.AddVisual(go.transform, PlaceholderSprites.Frame, TerminalArt.WithAlpha(TerminalArt.Green, 0.5f), size, FrameOrder, "Frame");
            frame.transform.position = p;
            fx._frames.Add(frame);
        }
        return fx;
    }

    /// <summary>
    /// 分身の場所を選ぶ。決まった x（左から交互に高さ 5.5・7）のうち、プレイヤー・ボスから離れている所。
    /// 使えなければ、もう1つの高さ、代わりの x の順に試す。左から順に並べて返す
    /// </summary>
    List<Vector2> PickPoints()
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 size = TerminalGhost.Size;
        Vector2? player = _owner.TargetPoint;
        Bounds? boss = null;
        if (_owner.TryGetComponent(out BossController bc)) boss = bc.BodyBounds;
        int want = _cfg.GhostXs != null ? _cfg.GhostXs.Length : 0;

        var candidates = new List<float>();
        if (_cfg.GhostXs != null) candidates.AddRange(_cfg.GhostXs);
        if (_cfg.FallbackXs != null) candidates.AddRange(_cfg.FallbackXs);

        var result = new List<Vector2>();
        foreach (float cx in candidates)
        {
            if (result.Count >= want) break;
            float x = Mathf.Clamp(arena.center.x + cx, arena.xMin + size.x * 0.5f, arena.xMax - size.x * 0.5f);
            bool firstLow = result.Count % 2 == 0;
            float h1 = firstLow ? _cfg.Heights.x : _cfg.Heights.y;
            float h2 = firstLow ? _cfg.Heights.y : _cfg.Heights.x;
            foreach (float h in new[] { h1, h2 })
            {
                var p = new Vector2(x, arena.yMin + h);
                if (!IsFree(p, player, boss, result, size)) continue;
                result.Add(p);
                break;
            }
        }
        result.Sort((a, b) => a.x.CompareTo(b.x));
        return result;
    }

    bool IsFree(Vector2 p, Vector2? player, Bounds? boss, List<Vector2> taken, Vector2 size)
    {
        float r = _cfg.AvoidRadius;
        if (player is Vector2 pl && Vector2.Distance(p, pl) < r) return false;
        if (boss is Bounds b && Vector2.Distance(b.ClosestPoint(p), p) < r) return false;
        foreach (Vector2 t in taken)
        {
            if (Mathf.Abs(t.x - p.x) < size.x + 0.2f && Mathf.Abs(t.y - p.y) < size.y + 0.2f) return false;
        }
        return true;
    }

    void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }
        float now = Time.time;
        float t = now - _start;

        if (!_appeared)
        {
            bool on = Mathf.FloorToInt(now / 0.1f) % 2 == 0 || _cfg.TelegraphTime - t > 0.4f;
            foreach (var f in _frames)
            {
                if (f != null) f.enabled = on;
            }
            if (t < _cfg.TelegraphTime) return;
            Appear();
        }

        if (_ghosts.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        // 撃つ（1フレームに何発分も過ぎていたら、まとめて）
        while (_shotsFired < _totalShots && now >= ShotTime(_shotsFired))
        {
            Fire(_shotsFired);
            _shotsFired++;
        }
        // これから撃つ分身を、撃つ前の 0.5秒、白く光らせる（撃つ間隔より長いので、2体が同時に光ることもある）
        for (int k = _shotsFired; k < _totalShots && now >= ShotTime(k) - _cfg.FlashTime; k++)
        {
            TerminalGhost next = _ghosts[k % _ghosts.Count];
            if (next != null && next.IsAlive) next.Flash(ShotTime(k));
        }

        bool anyAlive = false;
        foreach (var g in _ghosts)
        {
            if (g != null && g.IsAlive) anyAlive = true;
        }
        bool finished = _shotsFired >= _totalShots && now >= ShotTime(_totalShots - 1) + _cfg.VanishDelay;
        if (finished || !anyAlive)
        {
            foreach (var g in _ghosts)
            {
                if (g != null && g.IsAlive) Crumble(g);
            }
            Destroy(gameObject);
        }
    }

    float ShotTime(int k) => _firstShotAt + k * _cfg.ShotInterval;

    void Appear()
    {
        _appeared = true;
        foreach (var f in _frames)
        {
            if (f != null) Destroy(f.gameObject);
        }
        _frames.Clear();

        int layer = LayerMask.NameToLayer("Enemy");
        if (layer < 0) layer = gameObject.layer;
        foreach (Vector2 p in _points)
        {
            TerminalGhost ghost = TerminalGhost.Create(transform, p, layer);
            if (ghost == null) continue;
            TerminalGhost captured = ghost;
            ghost.Health.Died += () => OnGhostDied(captured);
            _ghosts.Add(ghost);
        }
        _firstShotAt = Time.time + _cfg.FirstShotDelay;
        _totalShots = _ghosts.Count * Mathf.Max(1, _cfg.Rounds);
    }

    /// <summary>k 発目を撃つ（左の分身から順に。消された分身の番は飛ばす）</summary>
    void Fire(int k)
    {
        if (_ghosts.Count == 0) return;
        TerminalGhost g = _ghosts[k % _ghosts.Count];
        if (g == null || !g.IsAlive) return;
        Vector2 from = g.transform.position;
        Vector2 dir = Vector2.down;
        if (_owner.TargetPoint is Vector2 target)
        {
            Vector2 d = target - from;
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }
        Projectile p = Projectile.Spawn(_owner, _data, from, dir);
        if (p == null) return;
        // 絵を、緑の & の字に（回さない）
        foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.sprite = TerminalArt.AmpBullet;
            sr.color = Color.white;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.transform.localScale = Vector3.one;
            sr.transform.rotation = Quaternion.identity;
        }
        _owner.NotifyReleased(_data);
    }

    void OnGhostDied(TerminalGhost g)
    {
        if (g == null) return;
        Crumble(g);
        // 消すとボスのゲージが増える（形態変化・正常化の途中は増やさない）
        BossController boss = _owner != null ? _owner.GetComponent<BossController>() : null;
        if (boss != null && boss.State == BossState.Fighting && boss.Gauge != null && !boss.Gauge.Invincible)
        {
            boss.Gauge.Add(_cfg.GaugeOnKill);
        }
    }

    /// <summary>分身を、緑の文字（0・1）にくずして消す</summary>
    void Crumble(TerminalGhost g)
    {
        if (g == null) return;
        TerminalGlyphBurst.Spawn(_owner, g.transform.position, TerminalGhost.Size, 8, TerminalArt.Green, TerminalArt.BinaryGlyphs, 0.33f, 0.6f, 30);
        Destroy(g.gameObject);
    }
}
