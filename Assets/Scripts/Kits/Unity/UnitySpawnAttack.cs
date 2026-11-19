using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity の技1「GameObject &gt; 3D Object」（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// 流れ：赤い角の枠と「!」が N か所に 0.9秒（高さ 5〜9マス。x はランダム、1つはプレイヤーの真上）→
///       立方体・球・カプセルが出て落ちる → 床で止まり、4秒残る（乗れる・壁になる）→ 点滅して縮んで消える。
/// ・当たるのは落ちている間だけ（速さが毎秒2マス以上）。球は落ちたあと 2マス転がる（転がっている間は当たる）
/// ・第2形態：5か所。0.3秒ずつずらす（攻撃データの欄で）
/// ・出した物はボスの出した物（50％・100％で消える）
/// </summary>
[CreateAssetMenu(fileName = "UnitySpawn_", menuName = "Team1/Kits/Unity/3D Object")]
public class UnitySpawnAttack : CustomAttack
{
    [Tooltip("出す数（第1形態 3・第2形態 5）")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("1つずつずらす秒数（第2形態 0.3）")]
    [SerializeField, Min(0f)] float _stagger;
    [Tooltip("予告の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [Tooltip("出る高さ（床から。マス）")]
    [SerializeField] Vector2 _height = new Vector2(5f, 9f);
    [Tooltip("止まってから残る秒数")]
    [SerializeField, Min(0f)] float _remain = 4f;
    [Tooltip("球が転がるマス")]
    [SerializeField, Min(0f)] float _roll = 2f;
    [Tooltip("立方体・球の大きさ（マス）")]
    [SerializeField, Min(0.5f)] float _size = 1.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float floor = arena.yMin;
        Vector2 player = owner.TargetPoint ?? (Vector2)arena.center;
        var xs = PickXs(owner, arena, player.x);
        var fallers = new List<Kit3DFaller>();
        for (int i = 0; i < xs.Count; i++)
        {
            Kit3DShape shape = ShapeAt(i);
            Vector3 size = SizeOf(shape);
            float y = floor + Random.Range(_height.x, _height.y);
            if (i == 0) y = Mathf.Max(y, player.y + 3.5f); // プレイヤーの真上（足場の上にいても上から）
            y = Mathf.Min(y, arena.yMax - size.y * 0.5f - 0.4f);
            var opt = Kit3DFallOptions.Default;
            opt.Telegraph = _telegraph;
            opt.Remain = _remain;
            opt.RollDistance = shape == Kit3DShape.Sphere ? _roll : 0f;
            opt.Group = "unity.boss";
            opt.Cap = 8;
            fallers.Add(Kit3D.Faller(owner, data, shape, new Vector2(xs[i], y), size, UnityArt.Object, opt));
            owner.NotifyReleased(data);
            if (_stagger > 0f && i < xs.Count - 1) yield return new WaitForSeconds(_stagger);
        }
        // 全部が止まるまで待つ（技の長さ）
        float timeout = Time.time + _telegraph + 3f;
        while (Time.time < timeout && fallers.Exists(f => f != null && !f.HasLanded)) yield return null;
    }

    Kit3DShape ShapeAt(int i)
    {
        switch (i % 3)
        {
            case 0: return Kit3DShape.Cube;
            case 1: return Kit3DShape.Sphere;
            default: return Kit3DShape.Capsule;
        }
    }

    Vector3 SizeOf(Kit3DShape shape)
    {
        if (shape == Kit3DShape.Capsule) return new Vector3(_size * 0.6f, _size * 1.25f, _size * 0.6f);
        return new Vector3(_size, _size, _size);
    }

    // 1つ目はプレイヤーの真上、ほかは重ならない・ボスに重ならないランダムな x
    List<float> PickXs(AttackRunner owner, Rect arena, float playerX)
    {
        float margin = _size * 0.75f;
        float minX = arena.xMin + margin, maxX = arena.xMax - margin;
        var xs = new List<float> { Mathf.Clamp(playerX, minX, maxX) };
        float bossX = owner.transform.position.x;
        for (int i = 1; i < _count; i++)
        {
            float best = Random.Range(minX, maxX);
            for (int t = 0; t < 40; t++)
            {
                float x = Random.Range(minX, maxX);
                if (Mathf.Abs(x - bossX) < 1.8f) continue;
                bool ok = true;
                foreach (float o in xs) if (Mathf.Abs(o - x) < _size + 0.6f) { ok = false; break; }
                best = x;
                if (ok) break;
            }
            xs.Add(best);
        }
        return xs;
    }
}
