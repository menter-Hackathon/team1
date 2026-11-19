using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ペンツール（Illustrator のボスの技1。2026-11-19。docs/app-kits-2-design.md 3-4）。
/// アンカーポイントが 3つ、順に置かれる（0.2秒ずつ。ハンドルつき）→ 点を通る赤い細い曲線（0.9秒）→ 太さ 1.2マスのオレンジの線になる（判定 0.3秒）。
/// 点：ボスの前・部屋のまん中（床か高さ 4）・反対の壁（高さ 0.5 か 5）。曲線が高い所は、下をくぐれる。
/// 第2形態：2本。2本目は 0.5秒あと、まん中の高さが逆。線は PathStroke（同じ相手には 1回）
/// </summary>
[CreateAssetMenu(fileName = "Illustrator_Pen", menuName = "Team1/Kits/Illustrator Pen")]
public class IllustratorPenAttack : CustomAttack
{
    [Tooltip("本数（第1形態 1・第2形態 2）")]
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("2本目までの秒数")]
    [SerializeField, Min(0f)] float _nextDelay = 0.5f;
    [Tooltip("点を1つ置く秒数")]
    [SerializeField, Min(0f)] float _anchorTime = 0.2f;
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(0.2f)] float _width = 1.2f;
    [SerializeField, Min(0.05f)] float _active = 0.3f;
    [Tooltip("まん中の点の高さ（低い・高い。床から）")]
    [SerializeField] Vector2 _midHeights = new Vector2(0.6f, 4f);
    [Tooltip("壁の点の高さ（低い・高い。床から）")]
    [SerializeField] Vector2 _wallHeights = new Vector2(0.5f, 5f);

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        bool lowMid = Random.value < 0.5f;
        float longest = 0f;
        for (int i = 0; i < Mathf.Max(1, _count); i++)
        {
            if (i > 0) yield return new WaitForSeconds(_nextDelay);
            bool low = i % 2 == 0 ? lowMid : !lowMid;
            owner.StartCoroutine(OneCurve(owner, data, low));
            longest = 3f * _anchorTime + _telegraph + _active;
        }
        yield return new WaitForSeconds(longest);
    }

    IEnumerator OneCurve(AttackRunner owner, AttackData data, bool lowMid)
    {
        Rect arena = BattleArena.GetBounds();
        float floor = arena.yMin;
        Vector2 o = owner.OriginPoint;
        int dir = o.x <= arena.center.x ? 1 : -1;
        var anchors = new List<Vector2>
        {
            new Vector2(Mathf.Clamp(o.x + dir * 1.6f, arena.xMin + 0.5f, arena.xMax - 0.5f), Mathf.Clamp(o.y, floor + 1f, arena.yMax - 1f)),
            new Vector2(arena.center.x, floor + (lowMid ? _midHeights.x : _midHeights.y)),
            new Vector2(dir > 0 ? arena.xMax - 0.3f : arena.xMin + 0.3f, floor + (lowMid ? _wallHeights.y : _wallHeights.x)),
        };
        List<Vector2> curve = PathStroke.Smooth(anchors, 5);

        // 点を順に置く（ハンドルつき。まだ安全）
        float total = 3f * _anchorTime + _telegraph + _active + 0.3f;
        Transform deco = PathStroke.Decor(owner, Vector2.zero, total, "AiPenAnchors");
        for (int i = 0; i < anchors.Count; i++)
        {
            Vector2 a = i > 0 ? anchors[i - 1] : anchors[i];
            Vector2 b = i < anchors.Count - 1 ? anchors[i + 1] : anchors[i];
            IllustratorArt.AnchorWithHandles(deco, anchors[i], b - a, 0.8f, 44);
            if (i > 0) PathStroke.Bar(deco, anchors[i - 1], anchors[i], KitDress.Px, new Color(1f, 0.6f, 0f, 0.5f), 43, "Guide");
            if (_anchorTime > 0f) yield return new WaitForSeconds(_anchorTime);
        }

        var style = new PathStroke.Style
        {
            Telegraph = _telegraph,
            TelegraphPx = 2f,
            Width = _width,
            DrawTime = 0f,
            Active = _active,
            Linger = 0.25f,
            Color = IllustratorArt.Orange,
            Core = IllustratorArt.OrangeLight,
            Order = 38,
        };
        if (owner != null && owner.isActiveAndEnabled) PathStroke.Spawn(owner, data, curve, style);
    }
}
