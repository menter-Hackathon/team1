using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Merge conflict（GitHub Desktop のボスの第2形態の技。2026-11-19。docs/app-kits-2-design.md 2-2）。場に残る技。
/// 左の壁に &lt;&lt;&lt;&lt;&lt;&lt;&lt; HEAD、右の壁に &gt;&gt;&gt;&gt;&gt;&gt;&gt; virus。床に閉じる向きの赤い矢印。安全な幅 3マスに緑の =======（1.0秒。場所は x＝-5・0・5 のどれか）→
/// 両側から赤い壁が閉じる（2つの壁は動き出して 2.0秒で同時に着く。速さは毎秒 7マス未満）→ 緑の所だけ残して 0.5秒止まる → 開く。
/// 壁は床から天井まで当たる（足場の上も）。避け方：緑の ======= の所へ行く。
/// </summary>
[CreateAssetMenu(fileName = "GitHub_MergeConflict", menuName = "Team1/Kits/GitHub Desktop/Merge Conflict")]
public class GitHubMergeConflictAttack : CustomAttack
{
    [Tooltip("【仮】予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.0f;
    [Tooltip("【仮】壁が閉じきるまで（秒）。いちばん遠い壁でも毎秒 7マス未満になるように")]
    [SerializeField, Min(0.5f)] float _close = 2.0f;
    [Tooltip("【仮】閉じて止まる（秒）")]
    [SerializeField, Min(0f)] float _hold = 0.5f;
    [Tooltip("【仮】開く（秒。当たらない）")]
    [SerializeField, Min(0.05f)] float _open = 0.5f;
    [Tooltip("【仮】安全な幅（マス）")]
    [SerializeField, Min(1f)] float _safeWidth = 3f;
    [Tooltip("【仮】安全な所の真ん中（部屋の真ん中からのマス）。どれかを選ぶ")]
    [SerializeField] float[] _safeCenters = { -5f, 0f, 5f };

    public float Telegraph => _telegraph;
    public float Close => _close;
    public float Hold => _hold;
    public float Open => _open;
    public float SafeWidth => _safeWidth;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            Rect arena = BattleArena.GetBounds();
            float off = _safeCenters != null && _safeCenters.Length > 0 ? _safeCenters[Random.Range(0, _safeCenters.Length)] : 0f;
            float cx = Mathf.Clamp(arena.center.x + off, arena.xMin + _safeWidth, arena.xMax - _safeWidth);
            if (GitHubMergeWalls.Spawn(owner, data, this, arena, cx) != null) length = _telegraph + _close + _hold + _open;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
