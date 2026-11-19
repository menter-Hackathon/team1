using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】棒グラフ（Excel のボスの技 P1-2。2026-11-19。docs/app-kits-1-design.md 10章）。数値はすべて【仮】。
/// ボスの真下から、プレイヤーのいる向きへ、棒（幅 1.5）が 2.5マスおきに 4本、0.25秒ずつ遅れて伸びる（予告 0.8秒・判定 0.5秒・高さ 4）。
/// 棒の上に #DIV/0!。第2形態は 6本。
/// ・データの「前へ順に並べる」だと、浮くボスの真下の足場に棒が立ってしまうので、床の高さで出す（棒の数値は「棒」の攻撃データ）
/// ・「棒」の攻撃データの名前に BarGraph を入れて、Office のスキンの赤い棒グラフの絵を付ける（わざと）
/// </summary>
[CreateAssetMenu(fileName = "Excel_BarGraph", menuName = "Team1/Kits/Excel Bar Graph")]
public class ExcelBarGraphAttack : CustomAttack
{
    [Tooltip("棒1本の攻撃データ（種類 Area。大きさ・予告・判定時間）")]
    [SerializeField] AttackData _bar;
    [SerializeField, Min(1)] int _count = 4;
    [Tooltip("棒と棒の間（中心どうし。マス）")]
    [SerializeField, Min(0.5f)] float _spacing = 2.5f;
    [Tooltip("1本ずつ遅らせる秒数")]
    [SerializeField, Min(0f)] float _stagger = 0.25f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _bar != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect area = OfficeKitTools.Area(owner, out float floor);
        float half = _bar.AreaSize.x * 0.5f;
        float x0 = Mathf.Clamp(owner.OriginPoint.x, area.xMin + half, area.xMax - half);
        int dir = owner.Facing;
        int made = 0;
        for (int i = 0; i < _count; i++)
        {
            float x = x0 + dir * i * _spacing;
            if (x < area.xMin + half - 0.01f || x > area.xMax - half + 0.01f) break;
            AreaStrike.Spawn(owner, _bar, new Vector2(x, floor + 0.1f), Vector2.up, i * _stagger, i);
            made++;
        }
        owner.NotifyReleased(data);

        // 判定が出る瞬間に、棒の上に #DIV/0!
        float start = Time.time;
        int labeled = 0;
        float total = Mathf.Max(0, made - 1) * _stagger + _bar.TelegraphTime + _bar.HitWindow;
        while (Time.time - start < total)
        {
            while (labeled < made && Time.time - start >= labeled * _stagger + _bar.TelegraphTime)
            {
                float x = x0 + dir * labeled * _spacing;
                FxKit.Text("#DIV/0!", new Vector2(x, floor + _bar.AreaSize.y + 0.35f), 0.3f, ExcelArt.ErrorRed, 22, _bar.HitWindow + 0.1f, Color.white).Pop();
                labeled++;
            }
            yield return null;
        }
    }
}
