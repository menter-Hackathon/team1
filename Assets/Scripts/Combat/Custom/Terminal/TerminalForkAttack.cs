using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】fork() &amp;（2026-11-18：Terminal の第2形態だけの技。docs/app-boss-skills-design.md 4-3）。
/// 1. コマンドを打つ（0.5秒）。同時に、空中の4か所に、分身のうすい枠（出る場所の予告 0.8秒）
/// 2. 分身（ゴーストプロセス、TerminalGhost）が4体出る。動かない。ボスは 1.5秒から次の技へ
/// 3. 左の分身から順に、0.35秒おきに1発ずつ、プレイヤーを狙って撃つ。撃つ前の 0.5秒、その分身が白く光る。これを2周
/// 4. 2周撃ったら、残った分身は文字になって消える（約5秒）
/// ・分身は1回当てれば消え、ボスのゲージが +2。触ってもダメージはない
/// ・分身の弾はボスの AttackRunner で出す（弾の速さ・大きさ・寿命は攻撃データの「弾」の欄）。形態変化（50%）で消える
/// 場に残る技（攻撃データの「場に残る技」をオン）。分身がいる間、rm -rf と chmod は選ばれない
/// </summary>
[CreateAssetMenu(fileName = "Terminal_Fork", menuName = "Team1/Custom Attack/Terminal fork")]
public class TerminalForkAttack : CustomAttack
{
    [Tooltip("【仮】コマンドの行に打つ文字")]
    [SerializeField] string _command = "fork() &";
    [Tooltip("【仮】分身の出る x（部屋の真ん中から）。この数だけ出す")]
    [SerializeField] float[] _ghostXs = { -8f, -4f, 4f, 8f };
    [Tooltip("【仮】上の場所がプレイヤーやボスに近くて使えないときの、代わりの x")]
    [SerializeField] float[] _fallbackXs = { -6f, 6f, -2f, 2f, 0f, -9f, 9f };
    [Tooltip("【仮】分身の高さ（床から、真ん中まで）。左から交互に使う")]
    [SerializeField] Vector2 _heights = new Vector2(5.5f, 7f);
    [Tooltip("【仮】プレイヤーとボスから、この距離（マス）以内には出さない")]
    [SerializeField, Min(0f)] float _avoidRadius = 2.5f;
    [Tooltip("【仮】出る場所の予告（秒）")]
    [SerializeField, Min(0f)] float _telegraphTime = 0.8f;
    [Tooltip("【仮】技の長さ（秒）。ボスはこのあと次の技へ（分身は残る）")]
    [SerializeField, Min(0f)] float _techniqueTime = 1.5f;
    [Tooltip("【仮】分身が出てから、1発目を撃つまで（秒）")]
    [SerializeField, Min(0f)] float _firstShotDelay = 1f;
    [Tooltip("【仮】撃つ間隔（秒）")]
    [SerializeField, Min(0.05f)] float _shotInterval = 0.35f;
    [Tooltip("【仮】撃つ周回の数（1体につき、この数だけ撃つ）")]
    [SerializeField, Min(1)] int _rounds = 2;
    [Tooltip("【仮】撃つ前に白く光る秒数")]
    [SerializeField, Min(0f)] float _flashTime = 0.5f;
    [Tooltip("【仮】撃ち終わってから、分身が消えるまで（秒）")]
    [SerializeField, Min(0f)] float _vanishDelay = 0.5f;
    [Tooltip("【仮】分身を1体消したときに増えるボスのゲージ")]
    [SerializeField, Min(0)] int _gaugeOnKill = 2;

    public float[] GhostXs => _ghostXs;
    public float[] FallbackXs => _fallbackXs;
    public Vector2 Heights => _heights;
    public float AvoidRadius => _avoidRadius;
    public float TelegraphTime => _telegraphTime;
    public float FirstShotDelay => _firstShotDelay;
    public float ShotInterval => _shotInterval;
    public int Rounds => _rounds;
    public float FlashTime => _flashTime;
    public float VanishDelay => _vanishDelay;
    public int GaugeOnKill => _gaugeOnKill;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        TerminalCommandLine line = TerminalCommandLine.Spawn(owner, _command);
        TerminalForkEffect.Spawn(owner, data, this);
        float typing = Mathf.Min(TerminalCommandLine.TypingTime(_command), _techniqueTime);
        yield return new WaitForSeconds(typing);
        owner.NotifyReleased(data);
        float rest = _techniqueTime - typing;
        if (rest > 0f) yield return new WaitForSeconds(rest);
        if (line != null) line.Close();
    }
}
