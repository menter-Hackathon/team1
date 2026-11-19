using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ブラシ（Photoshop のボスの技2。2026-11-19。docs/app-kits-2-design.md 3-1）。
/// ボスからの細い赤い点線が、弧を描いて反対の壁まで出る（0.9秒）→ 太い水色のブラシの線が、0.5秒で同じ道をなぞる（先に丸いブラシのカーソル）→ 線は 0.8秒残って消える（残る間は当たらない）。
/// 道は 2種類：「谷」＝部屋のまん中で床に着く（両端と足場が安全。足場の下は、線が床から離れている）／「山」＝両端で床に着く（まん中が安全）。
/// 第2形態：谷と山を続けて（間 0.7秒）。判定は PathStroke（同じ相手には 1回）
/// </summary>
[CreateAssetMenu(fileName = "Photoshop_Brush", menuName = "Team1/Kits/Photoshop Brush")]
public class PhotoshopBrushAttack : CustomAttack
{
    [Tooltip("道の並び（0＝谷・1＝山）。第1形態はどちらか1つ（ランダム）、第2形態は 谷→山")]
    [SerializeField] bool _both;
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(0.05f)] float _drawTime = 0.5f;
    [Tooltip("ブラシの太さ（マス）")]
    [SerializeField, Min(0.2f)] float _width = 1.4f;
    [Tooltip("なぞり終えてから当たる秒数")]
    [SerializeField, Min(0.02f)] float _active = 0.12f;
    [Tooltip("線が残って見える秒数（当たらない）")]
    [SerializeField, Min(0f)] float _linger = 0.8f;
    [Tooltip("続けるときの間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.7f;

    // 道の元の点（部屋のまん中を 0、床を 0。左から右）【仮】
    // 谷：足場の下（|x| 2.3〜4.7）は高さ 2.0（太さ 1.4 で 1.3〜2.7。床のプレイヤー 1.0 にも、足場の上 3.0 にも当たらない）。
    // 点を細かく置いて、なめらかにしたときの沈みこみをおさえる（2026-11-19 にスクリプトで確かめた）
    static readonly Vector2[] Valley =
    {
        new Vector2(-9.5f, 7f), new Vector2(-7.2f, 5.2f), new Vector2(-5.7f, 2.8f), new Vector2(-4.7f, 2.05f), new Vector2(-3.5f, 2f),
        new Vector2(-2.3f, 2f), new Vector2(-1.1f, 1.2f), new Vector2(0f, 0.75f), new Vector2(1.1f, 1.2f), new Vector2(2.3f, 2f),
        new Vector2(3.5f, 2f), new Vector2(4.7f, 2.05f), new Vector2(5.7f, 2.8f), new Vector2(7.2f, 5.2f), new Vector2(9.5f, 7f),
    };
    static readonly Vector2[] Mountain =
    {
        new Vector2(-9.5f, 0.75f), new Vector2(-7f, 0.75f), new Vector2(-4.5f, 2.6f), new Vector2(-2f, 5f), new Vector2(0f, 5.6f),
        new Vector2(2f, 5f), new Vector2(4.5f, 2.6f), new Vector2(7f, 0.75f), new Vector2(9.5f, 0.75f),
    };

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var order = new List<bool>(); // true＝谷
        if (_both) { order.Add(true); order.Add(false); }
        else order.Add(Random.value < 0.5f);
        PathStroke last = null;
        for (int i = 0; i < order.Count; i++)
        {
            last = PathStroke.Spawn(owner, data, BuildPath(owner, order[i]), Style());
            if (last == null) yield break;
            if (i < order.Count - 1) yield return new WaitForSeconds(_telegraph + _drawTime + _active + _gap);
        }
        yield return new WaitForSeconds(_telegraph + _drawTime + _active);
    }

    PathStroke.Style Style() => new PathStroke.Style
    {
        Telegraph = _telegraph,
        Dashed = true,
        TelegraphPx = 2f,
        Width = _width,
        DrawTime = _drawTime,
        Active = _active,
        Linger = _linger,
        Color = PhotoshopArt.Blue,
        Core = PhotoshopArt.BlueLight,
        Tip = PhotoshopArt.BrushRing(Mathf.RoundToInt(_width * 18f) + 6),
        Order = 36,
    };

    /// <summary>ボスから始まり、ボスのいない側の壁まで行く道</summary>
    static List<Vector2> BuildPath(AttackRunner owner, bool valley)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 o = owner.OriginPoint;
        float cx = arena.center.x, floor = arena.yMin;
        float bx = o.x - cx;
        int dir;
        if (bx < -1f) dir = 1;
        else if (bx > 1f) dir = -1;
        else dir = owner.TargetPoint is Vector2 t && t.x < o.x ? -1 : 1;

        var src = valley ? Valley : Mountain;
        var pts = new List<Vector2> { o };
        for (int k = 0; k < src.Length; k++)
        {
            Vector2 s = dir > 0 ? src[k] : new Vector2(-src[k].x, src[k].y); // 左右反転（形は左右対称）
            float ahead = (s.x - bx) * dir;
            if (!(ahead >= 0.5f || (Mathf.Abs(s.x) < 0.01f && ahead >= -0.01f))) continue; // ボスの後ろの点は使わない（まん中の点は残す）
            pts.Add(new Vector2(cx + s.x, floor + s.y));
        }
        if (pts.Count < 2) pts.Add(new Vector2(cx + dir * 9.5f, floor + 0.75f));
        return PathStroke.Smooth(pts, 3);
    }
}
