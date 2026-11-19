using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Tab 補完（Cursor のボスの技1。2026-11-19。docs/app-kits-2-design.md 3-3）。プレイヤーの行き先を先読みする。
/// プレイヤーの「0.8秒後にいそうな場所」（今の位置＋今の速さ×0.8秒。止まっていればその場）に、灰色のプレイヤーの形（0.3秒）→
/// 赤に変わる（0.8秒。場所は固定）→ 2×2マスが当たる。形の横に Tab のキーの印。
/// 第1形態：2回。第2形態：4回で、2回目と4回目は「今いる場所」。避け方：赤い形の所へ行かない（向きを変える・止まる）。
/// </summary>
[CreateAssetMenu(fileName = "Cursor_Tab", menuName = "Team1/Kits/Cursor/Tab")]
public class CursorTabAttack : CustomAttack
{
    [Tooltip("【仮】何回か")]
    [SerializeField, Min(1)] int _count = 2;
    [Tooltip("【仮】1回ごとの間（秒。次のゴーストが出るまで）")]
    [SerializeField, Min(0.3f)] float _interval = 0.9f;
    [Tooltip("【仮】灰色のゴースト（秒）")]
    [SerializeField, Min(0f)] float _ghost = 0.3f;
    [Tooltip("【仮】赤の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】判定（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;
    [Tooltip("【仮】判定の大きさ（マス）")]
    [SerializeField] Vector2 _size = new Vector2(2f, 2f);
    [Tooltip("【仮】先読みの秒数")]
    [SerializeField, Min(0f)] float _lookAhead = 0.8f;
    [Tooltip("【仮】オン：偶数回目（2回目・4回目）は今いる場所（第2形態）")]
    [SerializeField] bool _evenIsCurrent;

    public float Ghost => _ghost;
    public float Telegraph => _telegraph;
    public float HitTime => _hitTime;
    public Vector2 Size => _size;
    public float LookAhead => _lookAhead;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            for (int i = 0; i < _count; i++)
                CursorTabStrike.Spawn(owner, data, this, i * _interval, _evenIsCurrent && i % 2 == 1);
            length = (_count - 1) * _interval + _ghost + _telegraph + _hitTime + 0.1f;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
