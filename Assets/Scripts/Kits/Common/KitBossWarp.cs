using System.Collections;
using UnityEngine;

// 【仮】ボスを瞬間移動させる口（浮くボスにも使える。2026-11-19：担当C の頼み。ミュージックのシャッフル・Spotify のスキップ）。担当0 の物。
// ・行き先に赤い体の枠（予告。telegraph 秒、最後の 0.3秒は点滅）→ 消える（vanish 秒。体当たりと被弾なし）→ 移る → 現れる（appear 秒）→ 元どおり
// ・高さを渡さなければ、今の高さのまま（浮くボスは浮いた高さのまま）。部屋の外には出さない
// ・ボスの出した物（CustomEffect）なので、形態変化（50％）・正常化（100％）で止められる。止められたら、必ず見える状態に戻す
//   （体当たりは、まだ戦っているときだけ戻す。追い出しのあとに体当たりを戻さない）
// ・使い方（ボスの技の CustomAttack の中で）：yield return KitBossWarp.Run(owner, boss, new Vector2(x, boss.transform.position.y));

/// <summary>ボスの瞬間移動</summary>
public static class KitBossWarp
{
    /// <summary>瞬間移動を始める（待たない）。出した部品を返す（ボス・持ち主が無ければ null）</summary>
    public static KitBossWarpEffect Start(AttackRunner owner, BossController boss, Vector2 to, float telegraph = 0.8f, float vanish = 0.15f, float appear = 0.15f)
    {
        if (owner == null || boss == null) return null;
        Rect arena = BattleArena.GetBounds();
        Bounds body = boss.BodyBounds;
        Vector2 offset = (Vector2)body.center - (Vector2)boss.transform.position;
        float half = body.extents.x;
        to.x = arena.width > half * 2f ? Mathf.Clamp(to.x, arena.xMin + half - offset.x, arena.xMax - half - offset.x) : to.x;
        GameObject go = owner.CreateSpawnedObject("KitBossWarp", to + offset);
        var e = go.AddComponent<KitBossWarpEffect>();
        e.Setup(boss, to, body.size, Mathf.Max(0f, telegraph), Mathf.Max(0f, vanish), Mathf.Max(0f, appear));
        return e;
    }

    /// <summary>x だけ変える（高さは今のまま）</summary>
    public static KitBossWarpEffect StartX(AttackRunner owner, BossController boss, float x, float telegraph = 0.8f, float vanish = 0.15f, float appear = 0.15f)
        => boss != null ? Start(owner, boss, new Vector2(x, boss.transform.position.y), telegraph, vanish, appear) : null;

    /// <summary>瞬間移動して、終わるまで待つ（CustomAttack の Run の中で yield return する）</summary>
    public static IEnumerator Run(AttackRunner owner, BossController boss, Vector2 to, float telegraph = 0.8f, float vanish = 0.15f, float appear = 0.15f)
    {
        KitBossWarpEffect e = Start(owner, boss, to, telegraph, vanish, appear);
        while (e != null && !e.Done) yield return null;
    }
}

/// <summary>ボスの瞬間移動の部品（KitBossWarp が作る）。行き先の赤い枠を出し、消える → 移る → 現れる</summary>
public class KitBossWarpEffect : CustomEffect
{
    BossController _boss;
    Vector2 _to;
    float _start, _telegraph, _vanish, _appear;
    SpriteRenderer _frame;
    bool _hidden, _moved;

    /// <summary>終わったか（現れ終わった・止められた）</summary>
    public bool Done { get; private set; }

    internal void Setup(BossController boss, Vector2 to, Vector2 bodySize, float telegraph, float vanish, float appear)
    {
        _boss = boss;
        _to = to;
        _start = Time.time;
        _telegraph = telegraph;
        _vanish = vanish;
        _appear = appear;
        if (telegraph > 0f)
        {
            Color red = KitDress.Red;
            _frame = AttackRunner.AddVisual(transform, PlaceholderSprites.Frame, red, bodySize + new Vector2(0.2f, 0.2f), 17, "Frame");
        }
    }

    void Update()
    {
        if (_boss == null || _boss.State != BossState.Fighting) { Finish(); return; }
        float t = Time.time - _start;
        if (t < _telegraph)
        {
            if (_frame != null) _frame.enabled = _telegraph - t > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            return;
        }
        if (_frame != null) _frame.enabled = false;
        t -= _telegraph;
        if (!_hidden)
        {
            _hidden = true;
            _boss.SetContactEnabled(false);
        }
        if (t < _vanish)
        {
            _boss.SetVisibility(1f - t / Mathf.Max(0.0001f, _vanish));
            return;
        }
        if (!_moved)
        {
            _moved = true;
            _boss.SetVisibility(0f);
            _boss.Teleport(_to);
        }
        t -= _vanish;
        if (t < _appear)
        {
            _boss.SetVisibility(t / Mathf.Max(0.0001f, _appear));
            return;
        }
        Finish();
    }

    void Finish()
    {
        if (Done) return;
        Done = true;
        RestoreOnce();
        Destroy(gameObject);
    }

    // 止められても、必ず見える状態に戻す（体当たりは、まだ戦っているときだけ）
    protected override void Restore()
    {
        Done = true;
        if (_boss == null) return;
        _boss.SetVisibility(1f);
        if (_hidden && (_boss.State == BossState.Fighting || _boss.State == BossState.Transition)) _boss.SetContactEnabled(true);
    }
}
