using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】タブ一覧（Safari のボスの技2。2026-11-19。docs/app-kits-2-design.md 4-4）。格子のタブが閉じる。
/// 部屋が 3列×2段の 6枚のタブになる（白い枠）。4枚に赤（1.4秒）→ 赤いタブが閉じる（判定 0.2秒）。
/// 下の段に、必ず1枚は安全なタブを残す（プレイヤーのいる列か、となりの列）。第2形態：2回続ける（安全なタブは毎回変わる）。
/// </summary>
[CreateAssetMenu(fileName = "Safari_TabGrid", menuName = "Team1/Kits/Safari/Tab Grid")]
public class SafariTabGridAttack : CustomAttack
{
    [Tooltip("【仮】何回か")]
    [SerializeField, Min(1)] int _repeat = 1;
    [Tooltip("【仮】予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.4f;
    [Tooltip("【仮】判定（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.2f;
    [Tooltip("【仮】次の回までの間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.35f;
    [Tooltip("【仮】赤くするタブの数（6枚のうち）")]
    [SerializeField, Range(1, 5)] int _red = 4;

    public float Telegraph => _telegraph;
    public float HitTime => _hitTime;
    public int Red => _red;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            float step = _telegraph + _hitTime + _gap;
            for (int i = 0; i < _repeat; i++) SafariTabTiles.Spawn(owner, data, this, i * step);
            length = _repeat * step - _gap + 0.1f;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
