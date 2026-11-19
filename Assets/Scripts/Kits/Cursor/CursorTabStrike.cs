using UnityEngine;

/// <summary>
/// 【仮】Tab 補完の1回分（2026-11-19）。CursorTabAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 先読みの場所に灰色のゴースト（安全）→ 赤に変わる（予告。場所は固定）→ 2×2 の判定 → 消える。
/// </summary>
public class CursorTabStrike : CustomEffect
{
    const int Order = 18;

    AttackRunner _owner;
    AttackData _data;
    CursorTabAttack _cfg;
    float _start;
    bool _current;
    bool _built;
    bool _red;
    bool _fired;
    SpriteRenderer _ghost;
    SpriteRenderer[] _frame;
    SpriteRenderer _fill;
    Hitbox _hit;

    public static CursorTabStrike Spawn(AttackRunner owner, AttackData data, CursorTabAttack cfg, float delay, bool current)
    {
        if (owner == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("CursorTab", owner.OriginPoint);
        var s = go.AddComponent<CursorTabStrike>();
        s._owner = owner;
        s._data = data;
        s._cfg = cfg;
        s._current = current;
        s._start = Time.time + Mathf.Max(0f, delay);
        return s;
    }

    void Build()
    {
        _built = true;
        Transform player = _owner.Target;
        Vector2 center = _owner.TargetPoint ?? (Vector2)transform.position;
        if (!_current && player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) center.x += pc.Velocity.x * _cfg.LookAhead; // 横の速さだけ（ジャンプの上下は読まない。割り切り）
        }
        Rect arena = BattleArena.GetBounds();
        Vector2 half = _cfg.Size * 0.5f;
        center.x = Mathf.Clamp(center.x, arena.xMin + half.x, arena.xMax - half.x);
        center.y = Mathf.Clamp(center.y, arena.yMin + 0.5f, arena.yMax - half.y);
        transform.position = center;
        _fill = CursorArt.Box(transform, Vector2.zero, _cfg.Size, CursorArt.A(CursorArt.Ghost, 0.12f), Order - 1, "Fill");
        _frame = CursorArt.DottedBox(transform, Vector2.zero, _cfg.Size, CursorArt.A(CursorArt.Ghost, 0.9f), Order);
        _ghost = CursorArt.PlayerGhost(transform, player, Vector2.zero, CursorArt.A(CursorArt.Ghost, 0.55f), Order + 1);
        CursorArt.TabKey(transform, new Vector2(half.x + 0.95f, half.y - 0.35f), Order + 2);
        _hit = CursorArt.AddHit(_owner, _data, transform, Vector2.zero, _cfg.Size);
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        if (e < _cfg.Ghost) return;
        float r = e - _cfg.Ghost;
        if (r < _cfg.Telegraph)
        {
            if (!_red)
            {
                _red = true;
                if (_ghost != null) _ghost.color = CursorArt.A(CursorArt.Red, 0.75f);
                if (_fill != null) _fill.color = CursorArt.A(CursorArt.Red, 0.2f);
                foreach (var d in _frame) if (d != null) d.color = CursorArt.Red;
            }
            bool on = CursorArt.Blink(_cfg.Telegraph - r);
            foreach (var d in _frame) if (d != null) d.enabled = on;
            return;
        }
        float h = r - _cfg.Telegraph;
        if (!_fired)
        {
            _fired = true;
            if (_hit != null) _hit.Activate();
            if (_fill != null) _fill.color = CursorArt.A(CursorArt.Red, 0.65f);
            if (_ghost != null) _ghost.color = Color.white;
            foreach (var d in _frame) if (d != null) { d.enabled = true; d.color = Color.white; }
        }
        if (h >= _cfg.HitTime) Destroy(gameObject);
    }
}
