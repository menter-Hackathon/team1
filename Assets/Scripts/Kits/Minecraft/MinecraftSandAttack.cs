using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft の技2「落ちる砂」（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// 流れ：床に赤い影 N列（0.8秒。1列は 1マス幅）→ 砂のブロック（3D）が落ちて積もる。6秒残る（乗れる・壁になる）。
/// ・避け方：影から出る。積もった砂は近接1回で1つこわせる（上の砂は落ちてくる）。当たるのは落ちている間だけ
/// ・第2形態：6列。高さ 3つ（ジャンプで越えられる高さまで）
/// ・列の間は 1マス以上あける（両側をふさがれても、こわすかジャンプで出られる）
/// </summary>
[CreateAssetMenu(fileName = "MinecraftSand_", menuName = "Team1/Kits/Minecraft/Sand")]
public class MinecraftSandAttack : CustomAttack
{
    [SerializeField, Min(1)] int _columns = 4;
    [SerializeField, Min(1)] int _stack = 2;
    [Tooltip("赤い影の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(0f)] float _remain = 6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 target = owner.TargetPoint ?? arena.center;
        var xs = new List<float> { Snap(target.x, arena) };
        for (int i = 1; i < _columns; i++)
        {
            float best = Snap(Random.Range(arena.xMin + 1f, arena.xMax - 1f), arena);
            for (int t = 0; t < 40; t++)
            {
                float x = Snap(Random.Range(arena.xMin + 1f, arena.xMax - 1f), arena);
                bool ok = true;
                foreach (float o in xs) if (Mathf.Abs(o - x) < 2f) { ok = false; break; }
                best = x;
                if (ok) break;
            }
            xs.Add(best);
        }
        float top = arena.yMin + 6.5f;
        foreach (float x in xs)
        {
            for (int k = 0; k < _stack; k++)
            {
                var opt = Kit3DFallOptions.Default;
                opt.Telegraph = _telegraph + k * 0.18f;
                opt.Remain = _remain;
                opt.BreakHits = 1;
                opt.NoBracket = true;
                opt.Bang = false;
                opt.ShadowColor = new Color(1f, 0.27f, 0.23f, 0.55f);
                opt.Group = "minecraft.sand";
                opt.Cap = 24;
                opt.LandedShade = 0.92f;
                opt.Gravity = 34f;
                Kit3DFaller f = Kit3D.Faller(owner, data, Kit3DShape.Cube, new Vector2(x, top + k * 1.1f), Vector3.one, MinecraftArt.Sand, opt);
                f.Broken += b => MinecraftCrumbs.Spawn(b.transform.position, MinecraftArt.Sand);
            }
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + _stack * 0.18f + 0.6f);
    }

    // 1マスの格子に合わせる（部屋の左の端から）
    static float Snap(float x, Rect arena)
    {
        float k = Mathf.Floor(x - arena.xMin);
        return Mathf.Clamp(arena.xMin + k + 0.5f, arena.xMin + 0.5f, arena.xMax - 0.5f);
    }
}
