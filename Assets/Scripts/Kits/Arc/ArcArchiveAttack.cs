using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】自動アーカイブ（Arc の第2形態の技。2026-11-19。docs/app-kits-2-design.md 4-5）。
/// 床が 5つのタブ（幅 4マス）に分かれる。プレイヤーのいるタブがうすくなっていき、赤く点滅（1.2秒）→ アーカイブ（判定 0.2秒。Archived）。
/// これを 4回（毎回、その時プレイヤーがいるタブ）。となりのタブへ動き続ければ当たらない。足場の上は、下のタブと同じ扱い（判定は床から 5マス）。
/// 判定は PathStrokeRegion
/// </summary>
[CreateAssetMenu(fileName = "Arc_Archive", menuName = "Team1/Kits/Arc Archive")]
public class ArcArchiveAttack : CustomAttack
{
    [SerializeField, Min(2)] int _tabs = 5;
    [SerializeField, Min(1)] int _times = 4;
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [SerializeField, Min(0.05f)] float _active = 0.2f;
    [Tooltip("判定の高さ（床から）")]
    [SerializeField, Min(1f)] float _height = 5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float tabW = arena.width / _tabs;
        float total = _times * (_telegraph + _active) + 0.4f;

        // 床のタブ（飾り。技の間ずっと）
        Transform deco = PathStroke.Decor(owner, new Vector2(arena.center.x, arena.yMin), total, "ArcTabs");
        var tabs = new List<SpriteRenderer>();
        for (int i = 0; i < _tabs; i++)
        {
            float x0 = arena.xMin + i * tabW;
            var sr = RuntimeSprites.Show(deco, ArcArt.Gradient(Mathf.RoundToInt(tabW * 18f) - 4, 7, 3, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, false, 0.75f),
                new Vector2(x0 + tabW * 0.5f - arena.center.x, 0.2f), 35, 1f, "Tab");
            tabs.Add(sr);
        }

        for (int k = 0; k < _times; k++)
        {
            float px = owner.TargetPoint is Vector2 t ? t.x : arena.center.x;
            int idx = Mathf.Clamp(Mathf.FloorToInt((px - arena.xMin) / tabW), 0, _tabs - 1);
            float cx = arena.xMin + (idx + 0.5f) * tabW;
            var c = new Vector2(cx, arena.yMin + _height * 0.5f);
            var style = new PathStrokeRegion.Style
            {
                Telegraph = _telegraph,
                Active = _active,
                Linger = 0.25f,
                Fill = new Color(ArcArt.Purple.r, ArcArt.Purple.g, ArcArt.Purple.b, 0.8f),
                TelegraphFillAlpha = 0.14f,
                OutlinePx = 2f,
                Order = 36,
            };
            PathStrokeRegion r = PathStrokeRegion.Spawn(owner, data, PathStrokeRegion.RectShape(c, new Vector2(tabW - 0.1f, _height)), c, style);
            SpriteRenderer tab = tabs[idx];
            if (r != null)
            {
                r.StepChanged += (reg, step) =>
                {
                    if (step == PathStrokeRegion.Step.Active)
                        FxKit.Text("Archived", reg.Center + new Vector2(0f, _height * 0.5f + 0.4f), 0.34f, Color.white, 47, 0.5f, ArcArt.Virus);
                };
            }
            // タブがうすくなっていく
            float end = Time.time + _telegraph;
            while (Time.time < end)
            {
                if (tab != null) tab.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, (end - Time.time) / _telegraph));
                yield return null;
            }
            yield return new WaitForSeconds(_active);
            if (tab != null) tab.color = Color.white;
        }
        yield return new WaitForSeconds(0.25f);
    }
}
