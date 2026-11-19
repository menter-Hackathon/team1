using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Little Arc（Arc のボスの技2。2026-11-19。docs/app-kits-2-design.md 4-5）。
/// 2か所（高さ 4〜7マス）に赤い枠（0.8秒）→ 小さい窓（3×2.2マス。角の丸い Mac のウィンドウに、グラデーションの帯）が開く
/// → 窓からプレイヤーへ赤い線（0.8秒。向きは固定）→ 撃って閉じる。第2形態：3か所。2発ずつ。
/// 弾の数値（速さ・大きさ・寿命）は攻撃データの弾の欄
/// </summary>
[CreateAssetMenu(fileName = "Arc_LittleArc", menuName = "Team1/Kits/Arc Little Arc")]
public class ArcLittleArcAttack : CustomAttack
{
    [SerializeField, Min(1)] int _windows = 2;
    [SerializeField, Min(1)] int _shots = 1;
    [Tooltip("2発目までの秒数")]
    [SerializeField, Min(0.05f)] float _shotGap = 0.3f;
    [Tooltip("赤い枠の予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _frameTime = 0.8f;
    [Tooltip("赤い線の予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _aimTime = 0.8f;
    [SerializeField] Vector2 _size = new Vector2(3f, 2.2f);
    [Tooltip("窓の高さの範囲（床から）")]
    [SerializeField] Vector2 _heights = new Vector2(4f, 7f);
    [Tooltip("窓と窓をずらす秒数")]
    [SerializeField, Min(0f)] float _stagger = 0.25f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float px = owner.TargetPoint is Vector2 t ? t.x : arena.center.x;
        float bx = owner.OriginPoint.x;
        var spots = new List<Vector2>();
        for (int tries = 0; tries < 60 && spots.Count < _windows; tries++)
        {
            float x = Random.Range(arena.xMin + _size.x * 0.5f + 0.3f, arena.xMax - _size.x * 0.5f - 0.3f);
            if (Mathf.Abs(x - px) < 2.5f || Mathf.Abs(x - bx) < 3f) continue;
            float y = arena.yMin + Random.Range(_heights.x, _heights.y);
            bool near = false;
            foreach (var s in spots) if (Mathf.Abs(s.x - x) < _size.x + 0.8f) near = true;
            if (!near) spots.Add(new Vector2(x, y));
        }
        float total = 0f;
        for (int i = 0; i < spots.Count; i++)
        {
            GameObject go = owner.CreateSpawnedObject("Arc_LittleArc", spots[i]);
            var e = go.AddComponent<ArcLittleArcEffect>();
            e.Setup(owner, data, _size, i * _stagger, _frameTime, _aimTime, _shots, _shotGap);
            total = Mathf.Max(total, e.TotalTime);
        }
        yield return new WaitForSeconds(total);
    }
}
