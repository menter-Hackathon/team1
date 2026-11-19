using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Frame（Figma のボスの技2。2026-11-19。docs/app-kits-2-design.md 2-4）。
/// プレイヤーの位置に、青い選択の枠（4×3マス。角に白い四角、左上に Frame 1）が広がる（0.3秒）→ 赤に変わる（0.9秒）→ 中が消える（判定 0.15秒。Delete）。
/// 第2形態（Auto layout）：枠が 3つ、間 2.5マスで並んで同時に出る。間が安全（間にピンクの間隔の数字 45）。
/// 判定は PathStrokeRegion（囲んだ範囲）。ダメージは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Figma_Frame", menuName = "Team1/Kits/Figma Frame")]
public class FigmaFrameAttack : CustomAttack
{
    [Tooltip("枠の数（第1形態 1・第2形態 3）")]
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("枠の大きさ（マス）")]
    [SerializeField] Vector2 _size = new Vector2(4f, 3f);
    [Tooltip("枠と枠の間（マス。安全な所）")]
    [SerializeField, Min(0f)] float _gap = 2.5f;
    [Tooltip("青い枠が広がる秒数（まだ安全）")]
    [SerializeField, Min(0f)] float _appear = 0.3f;
    [Tooltip("赤い予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [SerializeField, Min(0.05f)] float _active = 0.15f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 p = owner.TargetPoint ?? arena.center;
        int n = Mathf.Max(1, _count);
        float step = _size.x + _gap;
        // まん中の枠をプレイヤーに。部屋からはみ出さないように、全体をずらす
        float total = n * _size.x + (n - 1) * _gap;
        float left = Mathf.Clamp(p.x - total * 0.5f, arena.xMin + 0.2f, arena.xMax - 0.2f - total);
        float cy = Mathf.Max(p.y, arena.yMin + _size.y * 0.5f);
        PathStrokeRegion last = null;
        for (int i = 0; i < n; i++)
        {
            var c = new Vector2(left + _size.x * 0.5f + i * step, cy);
            last = SpawnFrame(owner, data, c, _size, _appear, _telegraph, _active, $"Frame {i + 1}");
            // 間隔の数字（Auto layout のピンクの印）
            if (i > 0 && last != null)
            {
                float gx = c.x - step * 0.5f;
                Transform root = PathStroke.Decor(owner, new Vector2(gx, cy), _appear + _telegraph, "FigmaGap");
                PathStroke.Bar(root, new Vector2(gx - _gap * 0.5f + 0.1f, cy), new Vector2(gx + _gap * 0.5f - 0.1f, cy), 1.5f * KitDress.Px, FigmaArt.Pink, 44, "Gap");
                var tag = RuntimeSprites.Show(root, FigmaArt.RoundRect(16, 9), new Vector2(0f, 0.34f), 45, 1f, "GapTag");
                tag.color = FigmaArt.Pink;
                TextMesh t = WorldText.Create(root, Mathf.RoundToInt(_gap * 18f).ToString(), 0.3f, Color.white, 46, true, TextAnchor.MiddleCenter, "GapNum");
                t.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            }
        }
        if (last == null) yield break;
        yield return new WaitForSeconds(last.TotalTime);
    }

    /// <summary>
    /// 選択の枠を1つ出す（青い枠が広がる → 赤 → 中が消える）。Figma の技で使い回す（カーソルの Client の枠も）
    /// </summary>
    public static PathStrokeRegion SpawnFrame(AttackRunner owner, AttackData data, Vector2 center, Vector2 size, float appear, float telegraph, float active, string label)
    {
        var style = new PathStrokeRegion.Style
        {
            Appear = appear,
            AppearColor = FigmaArt.Blue,
            Grow = Mathf.Max(0.05f, appear),
            Telegraph = telegraph,
            Active = active,
            Linger = 0.3f,
            Fill = new Color(1f, 1f, 1f, 0.85f),
            OutlinePx = 1.5f,
            Order = 40,
        };
        PathStrokeRegion r = PathStrokeRegion.Spawn(owner, data, PathStrokeRegion.RectShape(center, size), center, style);
        if (r == null) return null;
        // 角の四角と、左上の名前
        Vector2 h = size * 0.5f;
        var handles = new SpriteRenderer[4];
        int k = 0;
        for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
                handles[k++] = RuntimeSprites.Show(r.Root, FigmaArt.Handle, new Vector2(ix * h.x, iy * h.y), 43, 1f, "Handle");
        TextMesh name = WorldText.Create(r.Root, label, 0.3f, FigmaArt.Blue, 43, true, TextAnchor.LowerLeft, "Label");
        name.transform.localPosition = new Vector3(-h.x, h.y + 0.12f, 0f);
        r.StepChanged += (reg, step) =>
        {
            if (step == PathStrokeRegion.Step.Telegraph)
            {
                foreach (var hd in handles) if (hd != null) hd.sprite = FigmaArt.HandleRed;
                WorldText.SetColor(name, KitDress.Red);
            }
            else if (step == PathStrokeRegion.Step.Active)
            {
                foreach (var hd in handles) if (hd != null) hd.enabled = false;
                if (name != null) name.gameObject.SetActive(false);
                FxKit.Text("Delete", reg.Center, 0.4f, KitDress.Red, 47, 0.5f, Color.white);
            }
        };
        return r;
    }
}
