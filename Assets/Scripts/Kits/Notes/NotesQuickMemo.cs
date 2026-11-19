using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メモのボスの技「クイックメモ」（2026-11-19。docs/app-kits-1-design.md 14章 P1-2。担当0）。
/// プレイヤーに近い方の、部屋の下の隅から、メモの窓（5×4マス）がせり出す（出現の枠の予告 0.8秒。出る瞬間だけ当たる）→ 足場として 3秒残る。
/// 隅に逃げ続けるプレイヤーを、まん中へ戻す技。今の部品（出現）は「狙う相手に重なる位置」にしか出せないので、隅の位置をここで決める（Custom 小）
/// </summary>
[CreateAssetMenu(fileName = "NotesQuickMemo", menuName = "Team1/Kits/Notes/Quick Memo")]
public class NotesQuickMemo : CustomAttack
{
    [Tooltip("窓の攻撃データ（種類＝落下物。大きさ・予告・判定時間・残る秒数・足場になるか）")]
    [SerializeField] AttackData _window;
    [Tooltip("窓の絵の名前（KitDress）")]
    [SerializeField] string _art = NotesKit.ArtQuick;

    public override bool CanRun(AttackRunner owner, AttackData data) => _window != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_window == null) yield break;
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _window.ObjectSize;
        float px = (owner.TargetPoint ?? owner.OriginPoint).x;
        bool left = px < arena.center.x;
        float x = left ? arena.xMin + size.x * 0.5f : arena.xMax - size.x * 0.5f;
        var center = new Vector2(x, arena.yMin + size.y * 0.5f);
        FallingObject f = FallingObject.SpawnInPlace(owner, _window, center, Time.time + _window.TelegraphTime);
        if (f != null) KitDress.Swap(f.gameObject, KitDress.Art(_art));
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_window.TelegraphTime + _window.ActiveTime);
    }
}
