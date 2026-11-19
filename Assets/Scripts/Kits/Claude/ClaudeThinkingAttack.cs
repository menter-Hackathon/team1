using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】じっくり考える（2026-11-19。Claude の第2形態の技。docs/app-kits-1-design.md 6章 P2）。担当3 の物。
/// ボスの頭の上で火花が回る（2.0秒）。その間、0.6秒おきに 3回、そのときのプレイヤーの足元に赤い印（1・2・3。付いたら動かない）
/// → 考え終わると 1 → 2 → 3 の順に 0.4秒おきで柱（幅 2×高さ 6、0.4秒）。
/// ・印そのものが予告（付いた瞬間から赤い。一番短い 3 でも 1.0秒ある）。柱は ZoneStrike（形態変化で消える）
/// ・避け方：印の無い所へ。考えている間は小さく動く（動き回ると印が散らばる）
/// ・ダメージ・ノックバックは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Claude_Thinking", menuName = "Team1/Kits/Claude Thinking")]
public class ClaudeThinkingAttack : CustomAttack
{
    [Tooltip("【仮】印の数")]
    [SerializeField, Min(1)] int _marks = 3;
    [Tooltip("【仮】印を付ける間隔（秒。1つ目もこの秒数のあと）")]
    [SerializeField, Min(0.1f)] float _markInterval = 0.6f;
    [Tooltip("【仮】考える秒数（このあと柱が出始める）")]
    [SerializeField, Min(0.5f)] float _thinkTime = 2f;
    [Tooltip("【仮】柱の間隔（秒）")]
    [SerializeField, Min(0.1f)] float _pillarInterval = 0.4f;
    [Tooltip("【仮】柱の大きさ（マス。幅×高さ）")]
    [SerializeField] Vector2 _pillarSize = new Vector2(2f, 6f);
    [Tooltip("【仮】柱の判定の秒数")]
    [SerializeField, Min(0.05f)] float _active = 0.4f;

    public int Marks => _marks;
    public float MarkInterval => _markInterval;
    public float ThinkTime => _thinkTime;
    public float PillarInterval => _pillarInterval;
    public Vector2 PillarSize => _pillarSize;
    public float Active => _active;

    /// <summary>技の長さ（最後の柱が消えるまで）</summary>
    public float Duration => Mathf.Max(_thinkTime, _markInterval * _marks) + (_marks - 1) * _pillarInterval + _active + 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        ClaudeThinkingEffect.Spawn(owner, data, this);
        yield return new WaitForSeconds(_thinkTime);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(Mathf.Max(0f, Duration - _thinkTime));
    }
}
