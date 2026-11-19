using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】なげなわ選択（Photoshop のボスの技1。2026-11-19。docs/app-kits-2-design.md 3-1）。
/// プレイヤーの位置に、少しゆがんだだ円の選択範囲（横 4.5×縦 3.5マス。白黒の点線が流れる）が出る → 点線が赤になる（0.9秒）
/// → 中が透明の市松もようになって消える（判定 0.15秒。もようは 0.4秒見える）。
/// 第2形態：3回続ける（0.6秒ずつずらす。毎回プレイヤーの位置）。3回目は横 7マスの大きい選択。判定は PathStrokeRegion
/// </summary>
[CreateAssetMenu(fileName = "Photoshop_Lasso", menuName = "Team1/Kits/Photoshop Lasso")]
public class PhotoshopLassoAttack : CustomAttack
{
    [Tooltip("回数（第1形態 1・第2形態 3）")]
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("ずらす秒数")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;
    [SerializeField] Vector2 _size = new Vector2(4.5f, 3.5f);
    [Tooltip("最後の大きい選択（回数が 2 以上のとき）")]
    [SerializeField] Vector2 _lastSize = new Vector2(7f, 3.5f);
    [Tooltip("白黒の点線で見せる秒数（まだ安全）")]
    [SerializeField, Min(0f)] float _appear = 0.3f;
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(0.05f)] float _active = 0.15f;
    [Tooltip("市松もようが見えている秒数")]
    [SerializeField, Min(0f)] float _linger = 0.4f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        PathStrokeRegion last = null;
        int n = Mathf.Max(1, _count);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            Vector2 size = n > 1 && i == n - 1 ? _lastSize : _size;
            last = Spawn(owner, data, size, i);
        }
        if (last != null) yield return new WaitForSeconds(last.TotalTime);
    }

    PathStrokeRegion Spawn(AttackRunner owner, AttackData data, Vector2 size, int index)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 p = owner.TargetPoint ?? arena.center;
        p.x = Mathf.Clamp(p.x, arena.xMin + size.x * 0.35f, arena.xMax - size.x * 0.35f);
        p.y = Mathf.Max(p.y, arena.yMin + size.y * 0.35f);
        var style = new PathStrokeRegion.Style
        {
            Appear = _appear,
            Ants = true,
            Telegraph = _telegraph,
            Active = _active,
            Linger = _linger,
            Fill = Color.white,
            Checker = PhotoshopArt.CheckGray,
            CheckerCell = 4,
            OutlinePx = 1.5f,
            Order = 40,
        };
        PathStrokeRegion r = PathStrokeRegion.Spawn(owner, data, Lasso(p, size, index), p, style);
        if (r == null) return null;
        r.StepChanged += (reg, step) =>
        {
            if (step == PathStrokeRegion.Step.Active) FxKit.Text("Delete", reg.Center + new Vector2(0f, size.y * 0.5f + 0.4f), 0.34f, Color.white, 47, 0.45f, PhotoshopArt.Navy);
        };
        return r;
    }

    /// <summary>なげなわの形（だ円を少しゆがめる。手で囲んだ感じ）</summary>
    static System.Collections.Generic.List<Vector2> Lasso(Vector2 c, Vector2 size, int seed)
    {
        var o = new System.Collections.Generic.List<Vector2>(32);
        float ph = seed * 1.7f + Time.time;
        const int n = 30;
        for (int i = 0; i < n; i++)
        {
            float t = i * Mathf.PI * 2f / n;
            float k = 1f + 0.05f * Mathf.Sin(3f * t + ph) + 0.035f * Mathf.Sin(5f * t + ph * 1.3f);
            o.Add(c + new Vector2(Mathf.Cos(t) * size.x * 0.5f, Mathf.Sin(t) * size.y * 0.5f) * k);
        }
        return o;
    }
}
