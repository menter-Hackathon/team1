using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Accept / Reject の1回分（2026-11-19）。CursorAcceptRejectAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 区画を緑（+）と赤（-）に塗る（予告）→ 赤い区画に判定（床から天井まで）→ 消える。
/// </summary>
public class CursorDiffZones : CustomEffect
{
    const int Order = 16;

    class Zone
    {
        public Rect Area;
        public bool Red;
        public SpriteRenderer Fill;
        public readonly List<SpriteRenderer> Edges = new List<SpriteRenderer>();
        public Hitbox Hit;
    }

    AttackRunner _owner;
    AttackData _data;
    CursorAcceptRejectAttack _cfg;
    float _start;
    bool _buttons;
    bool _built;
    bool _fired;
    Rect _arena;
    readonly List<Zone> _zones = new List<Zone>();
    SpriteRenderer _rejectBg;

    public static CursorDiffZones Spawn(AttackRunner owner, AttackData data, CursorAcceptRejectAttack cfg, float delay, bool buttons)
    {
        if (owner == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("CursorDiff", arena.center);
        var z = go.AddComponent<CursorDiffZones>();
        z._owner = owner;
        z._data = data;
        z._cfg = cfg;
        z._arena = arena;
        z._buttons = buttons;
        z._start = Time.time + Mathf.Max(0f, delay);
        return z;
    }

    void Build()
    {
        _built = true;
        int n = Mathf.Clamp(_cfg.Sections, 2, 3);
        float w = _arena.width / n;
        float px = _owner.TargetPoint is Vector2 t ? t.x : _arena.center.x;
        int p = Mathf.Clamp(Mathf.FloorToInt((px - _arena.xMin) / w), 0, n - 1);
        int green;
        if (n == 2) green = 1 - p; // プレイヤーのいる側が赤
        else
        {
            var choices = new List<int>();
            for (int i = p - 1; i <= p + 1; i++) if (i >= 0 && i < n) choices.Add(i);
            green = choices[Random.Range(0, choices.Count)];
        }
        Color red = CursorArt.Red, grn = CursorArt.Green;
        for (int i = 0; i < n; i++)
        {
            var z = new Zone { Area = new Rect(_arena.xMin + i * w, _arena.yMin, w, _arena.height), Red = i != green };
            Vector2 c = z.Area.center - _arena.center;
            Color col = z.Red ? red : grn;
            z.Fill = CursorArt.Box(transform, c, z.Area.size, CursorArt.A(col, z.Red ? 0.22f : 0.14f), Order, "Fill");
            float th = 2f * CursorArt.Px;
            if (i > 0) z.Edges.Add(CursorArt.Box(transform, new Vector2(c.x - w * 0.5f + th * 0.5f, c.y), new Vector2(th, _arena.height), col, Order + 1, "Edge"));
            if (i < n - 1) z.Edges.Add(CursorArt.Box(transform, new Vector2(c.x + w * 0.5f - th * 0.5f, c.y), new Vector2(th, _arena.height), col, Order + 1, "Edge"));
            // + と - の列（diff の行の頭）
            for (int k = 0; k < 4; k++)
            {
                float y = _arena.yMin + 1.2f + k * 2.4f - _arena.center.y;
                CursorArt.Text(transform, z.Red ? "-" : "+", new Vector2(c.x - w * 0.5f + 0.5f, y), 0.6f, col, Order + 2);
            }
            if (z.Red) z.Hit = CursorArt.AddHit(_owner, _data, transform, c, z.Area.size);
            _zones.Add(z);
        }
        if (_buttons) Buttons();
    }

    /// <summary>ボスの頭の上の Accept と Reject のボタン</summary>
    void Buttons()
    {
        Vector2 head = CursorArt.HeadOf(_owner) - _arena.center;
        var root = new GameObject("Buttons").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        root.localPosition = head + new Vector2(0f, 0.2f);
        CursorArt.Box(root, new Vector2(-1.55f, 0f), new Vector2(2.9f, 0.7f), CursorArt.Green, 40, "AcceptBg");
        CursorArt.Text(root, "Accept", new Vector2(-1.55f, 0.02f), 0.4f, Color.white, 41);
        _rejectBg = CursorArt.Box(root, new Vector2(1.55f, 0f), new Vector2(2.9f, 0.7f), MacUi.Gray4, 40, "RejectBg");
        CursorArt.Text(root, "Reject", new Vector2(1.55f, 0.02f), 0.4f, CursorArt.Red, 41);
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        float tele = _cfg.Telegraph;
        if (e < tele)
        {
            bool on = CursorArt.Blink(tele - e, 0.16f, 0.05f);
            foreach (var z in _zones)
            {
                if (!z.Red) continue;
                if (z.Fill != null) z.Fill.enabled = on;
            }
            return;
        }
        if (!_fired)
        {
            _fired = true;
            foreach (var z in _zones)
            {
                if (!z.Red) continue;
                if (z.Hit != null) z.Hit.Activate();
                if (z.Fill != null) { z.Fill.enabled = true; z.Fill.color = CursorArt.A(CursorArt.Red, 0.65f); }
            }
            if (_rejectBg != null) _rejectBg.color = CursorArt.Red;
        }
        if (e - tele >= _cfg.HitTime) Destroy(gameObject);
    }
}
