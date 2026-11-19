using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】sudo rm -rf /floor（2026-11-18：Terminal の技。docs/app-boss-skills-design.md 4-1）。
/// ボスがコマンドを打ちこむと、床が左から順に、文字にくずれて「文字化けの床」になる。床に穴はあけない（床の当たり判定は消さない）。
/// ・床を4マスずつ5つの区画に分ける。区画ごとに「赤く点滅 0.8秒 → 文字にくずれる 0.3秒（まだ当たらない）→ 文字化けの床」。0.6秒ずつずれる
/// ・文字化けの床は、区画の幅 × 床から高さ 1.2マス。触れている間ライフ -1（無敵が切れたら、もう一度）。浮いた足場は消えない（逃げ場）
/// ・最後の区画が文字化けになってから 3秒で、全部いっしょに元の床に戻る（最後の 0.5秒は白く点滅）
/// ・第1形態：左から3つ。第2形態：4つ。左からか右からかはランダム（点滅が始まる側で分かる）
/// 場に残る技（攻撃データの「場に残る技」をオン）。床が戻るまで、ほかの場に残る技と文字列の雨は選ばれない（TerminalRmRfEffect が知らせる）
/// </summary>
[CreateAssetMenu(fileName = "Terminal_RmRf", menuName = "Team1/Custom Attack/Terminal rm -rf")]
public class TerminalRmRfAttack : CustomAttack
{
    [Tooltip("【仮】コマンドの行に打つ文字")]
    [SerializeField] string _command = "sudo rm -rf /floor";
    [Tooltip("【仮】床を分ける区画の数")]
    [SerializeField, Min(1)] int _sectionCount = 5;
    [Tooltip("【仮】消える区画の数（第1形態 3・第2形態 4）")]
    [SerializeField, Min(1)] int _erasedSections = 3;
    [Tooltip("【仮】オン：左からか右からかをランダムにする（第2形態）。オフ：いつも左から")]
    [SerializeField] bool _randomSide = false;
    [Tooltip("【仮】打ち始めてから、1つ目の区画が点滅を始めるまで（秒）")]
    [SerializeField, Min(0f)] float _firstBlinkDelay = 0.2f;
    [Tooltip("【仮】となりの区画とのずれ（秒）")]
    [SerializeField, Min(0f)] float _sectionStagger = 0.6f;
    [Tooltip("【仮】赤く点滅する秒数")]
    [SerializeField, Min(0f)] float _blinkTime = 0.8f;
    [Tooltip("【仮】文字にくずれる秒数（この間はまだ当たらない）")]
    [SerializeField, Min(0f)] float _crumbleTime = 0.3f;
    [Tooltip("【仮】最後の区画が文字化けになってから、床が戻るまで（秒）")]
    [SerializeField, Min(0f)] float _lingerTime = 3f;
    [Tooltip("【仮】戻る前に白く点滅する秒数（この間は当たらない）")]
    [SerializeField, Min(0f)] float _restoreBlinkTime = 0.5f;
    [Tooltip("【仮】文字化けの床の判定の高さ（床から、マス）")]
    [SerializeField, Min(0.1f)] float _bandHeight = 1.2f;

    public int SectionCount => _sectionCount;
    public int ErasedSections => Mathf.Clamp(_erasedSections, 1, Mathf.Max(1, _sectionCount));
    public float FirstBlinkDelay => _firstBlinkDelay;
    public float SectionStagger => _sectionStagger;
    public float BlinkTime => _blinkTime;
    public float CrumbleTime => _crumbleTime;
    public float LingerTime => _lingerTime;
    public float RestoreBlinkTime => _restoreBlinkTime;
    public float BandHeight => _bandHeight;

    /// <summary>k 番目（0 から）の区画が点滅を始める時刻（技の始まりから）</summary>
    public float BlinkAt(int k) => _firstBlinkDelay + _sectionStagger * k;
    /// <summary>k 番目の区画が文字にくずれ始める時刻</summary>
    public float CrumbleAt(int k) => BlinkAt(k) + _blinkTime;
    /// <summary>k 番目の区画が文字化けの床になる（当たり始める）時刻</summary>
    public float CorruptAt(int k) => CrumbleAt(k) + _crumbleTime;
    /// <summary>床が全部戻る時刻</summary>
    public float RestoreAt => CorruptAt(ErasedSections - 1) + _lingerTime;
    /// <summary>ボスが次の技へ移る時刻（最後の区画が文字化けになった少しあと。第1形態で 2.6秒）</summary>
    public float TechniqueTime => CorruptAt(ErasedSections - 1) + 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        TerminalCommandLine line = TerminalCommandLine.Spawn(owner, _command);
        bool fromRight = _randomSide && Random.value < 0.5f;
        TerminalRmRfEffect.Spawn(owner, data, this, fromRight);

        float typing = TerminalCommandLine.TypingTime(_command);
        float wait = Mathf.Min(typing, TechniqueTime);
        yield return new WaitForSeconds(wait);
        owner.NotifyReleased(data);
        float rest = TechniqueTime - wait;
        if (rest > 0f) yield return new WaitForSeconds(rest);
        if (line != null) line.Close();
    }
}
