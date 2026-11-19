using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】近接の届く長さの倍率を、近接の判定に掛ける（2026-11-19：担当4 の頼み。Word の太字）。担当0 の物。
/// StatMods.Player.MeleeReach（→ GameBuffs.PlayerMeleeReach）が変わるたびに、プレイヤーの近接の判定（箱）の横幅を、元の幅×倍率にする。
/// 前（判定がある側）へだけ伸ばす。元の大きさは覚えておき、倍率 1 で必ず戻す。絵（振る見た目）は変えない
/// </summary>
public static class KitMeleeReach
{
    struct Base
    {
        public Vector2 Size;
        public Vector2 Offset;
        public float Forward; // 前の向き（+1／-1）
    }

    static readonly Dictionary<BoxCollider2D, Base> Bases = new Dictionary<BoxCollider2D, Base>();

    /// <summary>今の倍率を掛け直す（StatMods が呼ぶ）</summary>
    public static void Apply()
    {
        float k = Mathf.Max(0.1f, GameBuffs.PlayerMeleeReach);
        var melee = Object.FindObjectOfType<PlayerMeleeAttack>();
        // 消えた判定を忘れる
        var dead = new List<BoxCollider2D>();
        foreach (var c in Bases.Keys) if (c == null) dead.Add(c);
        foreach (var c in dead) Bases.Remove(c);
        if (melee == null) return;
        foreach (var hb in melee.GetComponentsInChildren<Hitbox>(true))
        {
            if (hb == null || hb.Team != Team.Player || !hb.TryGetComponent(out BoxCollider2D box)) continue;
            if (!Bases.TryGetValue(box, out Base b))
            {
                if (Mathf.Approximately(k, 1f)) continue; // まだ変えていない
                float side = box.transform.localPosition.x + box.offset.x;
                b = new Base { Size = box.size, Offset = box.offset, Forward = side < 0f ? -1f : 1f };
                Bases[box] = b;
            }
            float w = b.Size.x * k;
            box.size = new Vector2(w, b.Size.y);
            box.offset = new Vector2(b.Offset.x + b.Forward * (w - b.Size.x) * 0.5f, b.Offset.y);
            if (Mathf.Approximately(k, 1f)) Bases.Remove(box);
        }
    }
}
