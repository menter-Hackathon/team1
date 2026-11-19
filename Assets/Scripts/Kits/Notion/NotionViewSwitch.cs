using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Notion のボスの技「ビューの切り替え」（第2形態。2026-11-19。docs/app-kits-1-design.md 8章 P2。担当0）。
/// 画面の上のタブが Table → Board に変わる（0.8秒）→ 床を 4列（幅 5）に分け、左から右へ 0.45秒おきに、列ごとにカードの束が落ちる
/// （予告 0.8秒・判定 0.4秒・高さ 6）→ 次は右から左へ。通り過ぎた列へ入れば当たらない。区画は ZoneStrike（担当3）
/// ダメージ・ノックバックは、この技の攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "NotionViewSwitch", menuName = "Team1/Kits/Notion/View Switch")]
public class NotionViewSwitch : CustomAttack
{
    [SerializeField, Min(1)] int _columns = 4;
    [SerializeField, Min(0.5f)] float _height = 6f;
    [Tooltip("タブが変わる間（秒）")]
    [SerializeField, Min(0f)] float _tabTime = 0.8f;
    [Tooltip("列ごとの間（秒）")]
    [SerializeField, Min(0f)] float _stagger = 0.45f;
    [Tooltip("予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(0.05f)] float _active = 0.4f;
    [Tooltip("1回目と2回目（右から左）の間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.3f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect area = ZoneStrike.AreaFor(owner);
        float floor = ZoneStrike.FloorY(owner);
        // タブ（Table → Board）。飾りの英語の短い言葉
        var tab = new Vector2(area.center.x, area.yMax - 0.8f);
        FxKit.Text("Table", tab, 0.5f, NotionArt.Black, 30, _tabTime * 0.5f, NotionArt.White);
        yield return new WaitForSeconds(_tabTime * 0.5f);
        FxKit.Text("Board", tab, 0.5f, NotionArt.Black, 30, _tabTime * 0.5f + 0.4f, NotionArt.White);
        yield return new WaitForSeconds(_tabTime * 0.5f);

        Rect[] cols = ZoneStrike.Columns(area, _columns, floor, _height);
        Sprite fill = NotionArt.Cards();
        ZoneStrike.StrikeAll(owner, data, cols, 0f, _stagger, _telegraph, _active, fill, Color.white);
        owner.NotifyReleased(data);
        float first = (_columns - 1) * _stagger + _telegraph + _active + _gap;
        yield return new WaitForSeconds(first);

        System.Array.Reverse(cols); // 次は右から左へ
        ZoneStrike.StrikeAll(owner, data, cols, 0f, _stagger, _telegraph, _active, fill, Color.white);
        yield return new WaitForSeconds((_columns - 1) * _stagger + _telegraph + _active);
    }
}
