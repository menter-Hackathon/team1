using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】スパーク（2026-11-19。Claude の第1形態の技。docs/app-kits-1-design.md 6章 P1-1）。担当3 の物。
/// ボスを中心に、8方向へ赤い細い線（0.8秒）→ 火花の弾 8発（毎秒 8マス）→ 22.5度ずらして、もう 1回（第2形態は 3回）。
/// ・2回目の線は 1回目の線のちょうど間。1回目の線の上が、2回目の安全な所
/// ・データの弾（扇 360度）は最初と最後が重なり、包み（KitDressed）の線は回をずらせないので、ここで撃つ
/// ・どの回も予告の線は 0.8秒（仕様書の「0.6秒後」は、予告を短くしない決まりに合わせて 0.8秒にした【仮】）
/// ・弾の速さ・大きさ・寿命・ダメージは攻撃データの「弾」の欄
/// </summary>
[CreateAssetMenu(fileName = "Claude_Spark", menuName = "Team1/Kits/Claude Spark")]
public class ClaudeSparkAttack : CustomAttack
{
    [Tooltip("【仮】撃つ回数（第1形態 2・第2形態 3）")]
    [SerializeField, Min(1)] int _volleys = 2;
    [Tooltip("【仮】1回の弾の数（全方向に等間隔）")]
    [SerializeField, Min(1)] int _count = 8;
    [Tooltip("【仮】1回目の向きのずれ（度。0 なら真横・真下を含む）")]
    [SerializeField] float _firstOffset = 22.5f;
    [Tooltip("【仮】回ごとに回す角度（度）")]
    [SerializeField] float _step = 22.5f;
    [Tooltip("【仮】予告の線の秒数（0.8 が基本。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Bounds body = BodyOf(owner);
        Vector2 origin = body.center;
        Rect arena = BattleArena.GetBounds();
        var dirs = new List<Vector2>();
        for (int v = 0; v < _volleys; v++)
        {
            dirs.Clear();
            float off = _firstOffset + v * _step;
            for (int i = 0; i < _count; i++)
            {
                float ang = (off + 360f * i / _count) * Mathf.Deg2Rad;
                dirs.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)));
            }
            foreach (var d in dirs) KitDress.Line(owner, origin, origin + d * RayLength(arena, origin, d), _telegraph);
            // ボスの真ん中で火花が光る（ため）
            FxAnim.Sprite(ClaudeArt.Spark(13), origin, 33, _telegraph, Color.white, 1.2f, "ClaudeSparkCharge").Spin(180f).Grow(1.8f).FadeFrom(0.7f);
            yield return new WaitForSeconds(_telegraph);
            for (int i = 0; i < dirs.Count; i++)
            {
                Projectile p = Projectile.Spawn(owner, data, origin, dirs[i], v * _count + i);
                if (p != null) KitDress.Swap(p.gameObject, ClaudeArt.SparkBullet);
            }
            owner.NotifyReleased(data);
        }
        yield return new WaitForSeconds(0.3f);
    }

    static Bounds BodyOf(AttackRunner owner)
    {
        if (owner != null && owner.TryGetComponent(out BossController boss)) return boss.BodyBounds;
        return new Bounds(owner != null ? (Vector3)owner.OriginPoint : Vector3.zero, Vector3.one);
    }

    /// <summary>origin から dir へ、部屋の端までの長さ（最大 30）</summary>
    static float RayLength(Rect r, Vector2 o, Vector2 d)
    {
        float t = 30f;
        if (d.x > 0.0001f) t = Mathf.Min(t, (r.xMax - o.x) / d.x);
        if (d.x < -0.0001f) t = Mathf.Min(t, (r.xMin - o.x) / d.x);
        if (d.y > 0.0001f) t = Mathf.Min(t, (r.yMax - o.y) / d.y);
        if (d.y < -0.0001f) t = Mathf.Min(t, (r.yMin - o.y) / d.y);
        return Mathf.Max(1f, t);
    }
}
