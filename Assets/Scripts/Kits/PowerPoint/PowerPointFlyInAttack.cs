using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】フライイン（PowerPoint のボスの技 P2。2026-11-19。docs/app-kits-1-design.md 11章）。数値はすべて【仮】。
/// 左右の壁に、大きい矢印の予告（0.8秒。通り道に赤い細い線）→ 矢印の図形（3×1）が、壁から壁へ横切る（毎秒 14マス）。
/// 左から低 → 右から高 → 左から低 → 右から低、の4本（間 0.6秒）。うしろに残像（アニメーションの軌跡）。
/// 避け方：低＝ジャンプ、高＝足場から降りて床にいる。
/// </summary>
[CreateAssetMenu(fileName = "PowerPoint_FlyIn", menuName = "Team1/Kits/PowerPoint Fly In")]
public class PowerPointFlyInAttack : CustomAttack
{
    [Tooltip("矢印の攻撃データ（弾として出す。速さ・大きさ・寿命）")]
    [SerializeField] AttackData _arrow;
    [Tooltip("どちらの壁から（+1＝左の壁から右へ、-1＝右の壁から左へ）")]
    [SerializeField] int[] _dirs = { 1, -1, 1, -1 };
    [Tooltip("高さ（0＝低、1＝中、2＝高）")]
    [SerializeField] int[] _heights = { 0, 2, 0, 0 };
    [Tooltip("1本ずつの間（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;
    [Tooltip("予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _arrow != null && _dirs != null && _dirs.Length > 0;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect area = OfficeKitTools.Area(owner, out float floor);
        int n = _dirs.Length;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            if (OfficeKitTools.OwnerStopped(owner, data)) yield break;
            int dir = _dirs[i] >= 0 ? 1 : -1;
            var h = (WallShotHeight)Mathf.Clamp(_heights != null && i < _heights.Length ? _heights[i] : 0, 0, 2);
            float y = floor + WallShot.HeightOf(h);
            // 壁の大きい矢印（ふちだけ）
            float wx = dir > 0 ? area.xMin + 1.7f : area.xMax - 1.7f;
            OfficeFx mark = OfficeFx.Sprite(owner, PowerPointArt.ArrowOutline, new Vector2(wx, y), new Vector2(3f, 1f), _telegraph, 21);
            if (dir < 0) mark.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            WallShot.Fire(owner, _arrow, h, dir, _telegraph, PowerPointArt.Arrow, p => PowerPointTrail.Attach(p.gameObject, PowerPointArt.Arrow, new Vector2(3f, 1f)));
        }
        yield return new WaitForSeconds(_telegraph + 0.2f);
    }
}

/// <summary>うしろに残像を出す（PowerPoint のアニメーションの軌跡）。付けた物が消えると止まる</summary>
public class PowerPointTrail : MonoBehaviour
{
    Sprite _sprite;
    Vector2 _size;
    float _next;
    int _order = 18;

    public static PowerPointTrail Attach(GameObject go, Sprite sprite, Vector2 size, int order = 18)
    {
        if (go == null) return null;
        var t = go.AddComponent<PowerPointTrail>();
        t._sprite = sprite;
        t._size = size;
        t._order = order;
        return t;
    }

    void Update()
    {
        if (Time.time < _next || _sprite == null) return;
        _next = Time.time + 0.04f;
        FxAnim fx = FxAnim.Create("PPT_Trail", transform.position, 0.18f);
        SpriteRenderer sr = AttackRunner.AddVisual(fx.transform, _sprite, new Color(1f, 1f, 1f, 0.35f), _size, _order, "Ghost");
        fx.transform.rotation = transform.rotation;
        fx.FadeFrom(0f).Refresh();
    }
}
