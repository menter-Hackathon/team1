using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メッセージのボスの技「レーザー」（2026-11-19。docs/app-kits-1-design.md 16章 P2。画面いっぱいのエフェクト「レーザー」）。
/// ボスの上から、床の片方の端（プレイヤーから遠い方）へ、細い赤い線（0.8秒）と進む向きの矢印 → 光の帯が、床をなぞって反対の端まで 1.6秒で振れる。
/// 当たるのは「床の上の光の点」（高さ 0.8）と「帯が足場の上を通る所」。避け方：光の点が来る瞬間にジャンプ。足場の上は帯が通るので当たる。
/// 場に出る物は MessagesLaserEffect（自分で進む）
/// </summary>
[CreateAssetMenu(fileName = "Messages_Laser", menuName = "Team1/Kits/Messages Laser")]
public class MessagesLaserAttack : CustomAttack
{
    [Tooltip("予告（赤い線と矢印）の秒数。短くしない")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("端から端まで振れる秒数")]
    [SerializeField, Min(0.3f)] float _sweep = 1.6f;
    [Tooltip("床の上の光の点の判定（幅×高さ。マス）")]
    [SerializeField] Vector2 _spot = new Vector2(1.1f, 0.8f);
    [Tooltip("帯が足場の上を通る所の判定の高さ（マス）")]
    [SerializeField, Min(0.2f)] float _platformHit = 1f;
    [Tooltip("帯の見た目の太さ（マス）")]
    [SerializeField, Min(0.05f)] float _beamWidth = 0.35f;

    public float Telegraph => _telegraph;
    public float Sweep => _sweep;
    public Vector2 Spot => _spot;
    public float PlatformHit => _platformHit;
    public float BeamWidth => _beamWidth;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        MessagesLaserEffect e = MessagesLaserEffect.Spawn(owner, data, this);
        if (e == null) yield break;
        float timeout = Time.time + _telegraph + _sweep + 1f;
        while (e != null && Time.time < timeout) yield return null;
    }
}
