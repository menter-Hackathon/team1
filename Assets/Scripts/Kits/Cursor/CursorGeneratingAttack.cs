using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Generating...（Cursor のボスの第2形態の技。2026-11-19。docs/app-kits-2-design.md 3-3）。コードの行が、灰色から本物になる。
/// 3つの高さ（床・足場・その上）に順に：灰色の文字の行が横いっぱいに出る（0.3秒）→ 赤に変わる（0.8秒）→
/// 左から右へ毎秒 20マスで「打たれて」本物になる（高さ 1マス。打たれた所が 0.3秒当たる）。行と行は 0.6秒おき。頭の上に Generating...。
/// 避け方：赤い行の高さにいない（床の行はジャンプか足場、足場の行は床）。
/// </summary>
[CreateAssetMenu(fileName = "Cursor_Generating", menuName = "Team1/Kits/Cursor/Generating")]
public class CursorGeneratingAttack : CustomAttack
{
    [Tooltip("【仮】行の高さ（床からの、行の真ん中。マス）。この順に出す")]
    [SerializeField] float[] _rows = { 0.5f, 3.5f, 6.0f };
    [Tooltip("【仮】行と行の間（秒）")]
    [SerializeField, Min(0f)] float _rowGap = 0.6f;
    [Tooltip("【仮】灰色の行（秒）")]
    [SerializeField, Min(0f)] float _ghost = 0.3f;
    [Tooltip("【仮】赤の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】打つ速さ（マス/秒）")]
    [SerializeField, Min(1f)] float _typeSpeed = 20f;
    [Tooltip("【仮】打たれた所が当たる秒数")]
    [SerializeField, Min(0.05f)] float _linger = 0.3f;
    [Tooltip("【仮】行の高さ（マス）")]
    [SerializeField, Min(0.3f)] float _height = 1f;

    public float Ghost => _ghost;
    public float Telegraph => _telegraph;
    public float TypeSpeed => _typeSpeed;
    public float Linger => _linger;
    public float Height => _height;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            Rect arena = BattleArena.GetBounds();
            int n = _rows != null ? _rows.Length : 0;
            for (int i = 0; i < n; i++) CursorCodeRow.Spawn(owner, data, this, arena.yMin + _rows[i], i * _rowGap, i);
            float one = _ghost + _telegraph + arena.width / _typeSpeed + _linger;
            length = Mathf.Max(0, n - 1) * _rowGap + one + 0.1f;
            CursorGeneratingBadge.Spawn(owner, length);
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
