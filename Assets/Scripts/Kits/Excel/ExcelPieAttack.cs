using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】円グラフ（Excel のボスの技 P2。2026-11-19。docs/app-kits-1-design.md 10章）。数値はすべて【仮】。
/// ボスの上に円グラフ → 6つに切り分けられて（0.8秒。切れ目の向きに赤い細い線）、6方向へ飛ぶ（毎秒 8マス）
/// → 30度ずらしてもう1回（2回目も 0.8秒の線を見せる。仕様の 0.7秒より、予告 0.8秒の決まりを先にした）。
/// ・切れ端の攻撃データの名前に TabShuriken を入れて、Office のスキンの「円グラフの切れ端」の絵を付ける
/// </summary>
[CreateAssetMenu(fileName = "Excel_Pie", menuName = "Team1/Kits/Excel Pie")]
public class ExcelPieAttack : CustomAttack
{
    [Tooltip("切れ端1つの攻撃データ（種類 Projectile。速さ・大きさ・寿命）")]
    [SerializeField] AttackData _slice;
    [SerializeField, Min(2)] int _count = 6;
    [SerializeField, Min(1)] int _volleys = 2;
    [Tooltip("2回目からずらす角度（度）")]
    [SerializeField] float _rotate = 30f;
    [Tooltip("予告（赤い線・切り分け）の秒数。短くしない")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("円グラフを出す高さ（ボスの真ん中から上へ。マス）")]
    [SerializeField] float _height = 1.8f;
    [Tooltip("円グラフの直径（マス）")]
    [SerializeField, Min(0.5f)] float _diameter = 2.2f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _slice != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 origin = owner.OriginPoint + new Vector2(0f, _height);
        Rect area = OfficeKitTools.Area(owner, out _);
        origin.y = Mathf.Min(origin.y, area.yMax - _diameter * 0.5f);
        int dots = Mathf.Max(12, Mathf.RoundToInt(_diameter * 18f));
        for (int v = 0; v < _volleys; v++)
        {
            float offset = 90f + v * _rotate;
            var dirs = OfficeKitTools.RadialDirs(_count, offset);
            // 円グラフ（切れ目に白い線）→ 予告の線
            OfficeFx pie = OfficeFx.Sprite(owner, OfficeArt.Pie(dots, _count, _count, KitDress.Red, owner.SkinTheme), origin, Vector2.one * _diameter, _telegraph + 0.05f, 21, null, 0f);
            pie.transform.rotation = Quaternion.Euler(0f, 0f, v * _rotate); // 切れ目を線の向きに合わせる（反時計回り）
            OfficeKitTools.RadialLines(owner, origin, dirs, 24f, _telegraph, _diameter * 0.5f);
            float end = Time.time + _telegraph;
            while (Time.time < end)
            {
                if (OfficeKitTools.OwnerStopped(owner, data)) yield break;
                // 切り分け：最後の 0.3秒は、ふるえる
                if (pie != null && end - Time.time < 0.3f)
                    pie.transform.position = origin + Random.insideUnitCircle * 0.06f;
                yield return null;
            }
            if (pie != null) pie.End();
            FxKit.Ring(origin, _diameter * 1.4f, 0.18f, Color.white);
            OfficeKitTools.RadialFire(owner, _slice, origin, dirs, null, v * _count);
        }
        yield return new WaitForSeconds(0.3f);
    }
}
