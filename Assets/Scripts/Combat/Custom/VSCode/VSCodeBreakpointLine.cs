using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ブレークポイント（行）（2026-11-19：本物の VS Code のボスの技。docs/app-feedback-v2-design.md 3-1）。代用ボスは今の柱のまま。
/// たての柱をやめて、横の「行」にする。部屋に行を2つ決める：床の行（床から 1.0マスまで）と、足場の行（床から 3.0〜4.2マス）。どちらも部屋の横いっぱい。
/// ・予告 1.0秒：壁ぎわ（ボスがいない側）にその行の高さで赤丸が「ポン」。行の全部にうすい赤の帯と、上下のふちの赤い点線、灰色の行番号。
///   黄色い矢印 ▶ が反対の端から赤丸へ 1.0秒かけて進み、着いた瞬間が判定。最後の 0.3秒は帯が点滅
/// ・判定 0.2秒：行の全部が止まった行の黄色（6割）に光る。上下のふちは赤。部屋の上のまん中にデバッグの操作バー（0.3秒）
/// ・終わり 0.3秒：帯が左右から閉じる。赤丸が消える
/// 第1形態：技が始まった瞬間にプレイヤーがいる行。第2形態：その行 → もう一方 → その行 を 0.7秒ずつずらして（予告は短くしない）。
/// 避け方：床の行は跳ぶか足場に乗る。足場の行は足場から下りる。当たるとライフ -1（頭の上に ⏸ は DevSkin.Error。攻撃データの名前に Breakpoint）
/// </summary>
[CreateAssetMenu(fileName = "VSCode_BreakpointLine", menuName = "Team1/Custom Attack/VS Code Breakpoint Line")]
public class VSCodeBreakpointLine : CustomAttack
{
    [SerializeField] VSCodeLineSettings _settings = new VSCodeLineSettings();
    [Tooltip("【仮】行の数（第1形態 1・第2形態 3）")]
    [SerializeField, Min(1)] int _lines = 1;
    [Tooltip("【仮】床の行の上の端（床から、マス）。むずかしければ 0.8 に下げる（【要相談⑥】）")]
    [SerializeField, Min(0.2f)] float _floorRowTop = 1.0f;
    [Tooltip("【仮】足場の行の下の端・上の端（床から、マス）")]
    [SerializeField] Vector2 _platformRow = new Vector2(3.0f, 4.2f);
    [Tooltip("【仮】足場の上にいると見なす高さ（床から、マス。足元がこれより上で、床に立っていれば足場の行）")]
    [SerializeField, Min(0.5f)] float _onPlatformAbove = 2.0f;

    public VSCodeLineSettings Settings => _settings;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float floorY = arena.yMin;
        bool startOnPlatform = PlayerOnPlatform(owner, floorY);
        var rows = new List<VSCodeBreakpointLineEffect.Row>();
        for (int i = 0; i < _lines; i++)
        {
            bool platform = i % 2 == 0 ? startOnPlatform : !startOnPlatform;
            float bottom = platform ? _platformRow.x : 0f;
            float top = platform ? _platformRow.y : _floorRowTop;
            rows.Add(new VSCodeBreakpointLineEffect.Row
            {
                Bottom = floorY + bottom,
                Height = Mathf.Max(0.2f, top - bottom),
                HitAt = _settings.Telegraph + _settings.Stagger * i,
                LineNo = platform ? 15 : 12,
            });
        }
        // 赤丸は、ボスがいない側の壁ぎわ（ボスの体にかくれないように）
        Vector2 boss = BrowserDevFx.OwnerBounds(owner).center;
        bool dotLeft = boss.x >= arena.center.x;
        VSCodeBreakpointLineEffect fx = VSCodeBreakpointLineEffect.Create(owner, data, _settings, arena, rows, dotLeft, true);
        owner.NotifyReleased(data);
        float total = rows[rows.Count - 1].HitAt + _settings.HitTime + _settings.CloseTime;
        float end = Time.time + total;
        while (fx != null && Time.time < end) yield return null;
    }

    /// <summary>プレイヤーが足場の上にいるか（床に立っていて、足元が床から onPlatformAbove より上）</summary>
    bool PlayerOnPlatform(AttackRunner owner, float floorY)
    {
        Transform t = owner.Target;
        if (t == null) return false;
        float feet = t.position.y;
        foreach (var col in t.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) { feet = col.bounds.min.y; break; }
        }
        bool grounded = !t.TryGetComponent(out PlayerController pc) || pc.IsGrounded;
        return grounded && feet - floorY >= _onPlatformAbove;
    }
}
