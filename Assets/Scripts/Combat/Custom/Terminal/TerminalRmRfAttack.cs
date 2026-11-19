using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 【仮】sudo rm -rf /floor（2026-11-18：Terminal の技。docs/app-boss-skills-design.md 4-1）。
/// ボスがコマンドを打ちこむと、床が文字にくずれて「文字化けの床」になる。床に穴はあけない（床の当たり判定は消さない）。
/// ・床を4マスずつ5つの区画に分ける。区画ごとに「赤く点滅 → 文字にくずれる 0.3秒（まだ当たらない）→ 文字化けの床」
/// ・文字化けの床は、区画の幅 × 床から高さ 1.2マス。触れている間ライフ -1（無敵が切れたら、もう一度）。浮いた足場は消えない（逃げ場）
/// ・最後の区画が文字化けになってから 3秒で、全部いっしょに元の床に戻る（最後の 0.5秒は白く点滅）
/// 2026-11-19 まさぴ〜の感想で直した（docs/app-feedback-v2-design.md 6-1）：
/// ・第1形態：端から順番に3つ。1回目は左から（1→2→3）、2回目は右から（5→4→3）、と交互。点滅 0.8秒、0.6秒ずつずれる
/// ・第2形態：ランダムな3つ。3つ続けては選ばない（安全な区画が必ず2つ残る）。3つ同時に赤い点滅 1.5秒
/// 場に残る技（攻撃データの「場に残る技」をオン）。床が戻るまで、ほかの場に残る技と文字列の雨は選ばれない（TerminalRmRfEffect が知らせる）
/// </summary>
[CreateAssetMenu(fileName = "Terminal_RmRf", menuName = "Team1/Custom Attack/Terminal rm -rf")]
public class TerminalRmRfAttack : CustomAttack
{
    [Tooltip("【仮】コマンドの行に打つ文字（第1形態。プロンプトの % は行が足す）")]
    [SerializeField] string _command = "sudo rm -rf /floor";
    [Tooltip("【仮】床を分ける区画の数")]
    [SerializeField, Min(1)] int _sectionCount = 5;
    [Tooltip("【仮】消える区画の数（第1形態：端から順番）")]
    [SerializeField, Min(1)] int _erasedSections = 3;
    [Tooltip("【仮】オン：第2形態（ランダムな区画を同時に消す）。オフ：第1形態（端から順番。左からと右からを交互）")]
    [FormerlySerializedAs("_randomSide")]
    [SerializeField] bool _randomSections = false;
    [Tooltip("【仮】第1形態：オン＝左からと右からを交互（1回目は左から）。オフ＝いつも左から")]
    [SerializeField] bool _alternateSides = true;
    [Tooltip("【仮】打ち始めてから、1つ目の区画が点滅を始めるまで（秒）")]
    [SerializeField, Min(0f)] float _firstBlinkDelay = 0.2f;
    [Tooltip("【仮】となりの区画とのずれ（秒。第1形態）")]
    [SerializeField, Min(0f)] float _sectionStagger = 0.6f;
    [Tooltip("【仮】赤く点滅する秒数（第1形態）")]
    [SerializeField, Min(0f)] float _blinkTime = 0.8f;
    [Tooltip("【仮】文字にくずれる秒数（この間はまだ当たらない）")]
    [SerializeField, Min(0f)] float _crumbleTime = 0.3f;
    [Tooltip("【仮】最後の区画が文字化けになってから、床が戻るまで（秒）")]
    [SerializeField, Min(0f)] float _lingerTime = 3f;
    [Tooltip("【仮】戻る前に白く点滅する秒数（この間は当たらない）")]
    [SerializeField, Min(0f)] float _restoreBlinkTime = 0.5f;
    [Tooltip("【仮】文字化けの床の判定の高さ（床から、マス）")]
    [SerializeField, Min(0.1f)] float _bandHeight = 1.2f;

    [Header("第2形態（ランダム）")]
    [Tooltip("【仮】コマンドの行に打つ文字（第2形態。飾り。どこが消えるかは赤い点滅で伝える）")]
    [SerializeField] string _randomCommand = "sudo rm -rf /floor/*";
    [Tooltip("【仮】消える区画の数（第2形態）。続けて選ぶのは2つまで")]
    [SerializeField, Min(1)] int _randomCount = 3;
    [Tooltip("【仮】赤く点滅する秒数（第2形態。全部同時）")]
    [SerializeField, Min(0f)] float _randomBlinkTime = 1.5f;

    /// <summary>第2形態（ランダムな区画を同時に）か</summary>
    public bool RandomSections => _randomSections;
    public int SectionCount => _sectionCount;
    /// <summary>消える区画の数（形態で変わる。区画の数より多くはならない）</summary>
    public int ErasedSections => Mathf.Clamp(_randomSections ? _randomCount : _erasedSections, 1, Mathf.Max(1, _sectionCount));
    public float FirstBlinkDelay => _firstBlinkDelay;
    /// <summary>となりの区画とのずれ（第2形態は 0＝同時）</summary>
    public float SectionStagger => _randomSections ? 0f : _sectionStagger;
    /// <summary>赤く点滅する秒数（形態で変わる）</summary>
    public float BlinkTime => _randomSections ? _randomBlinkTime : _blinkTime;
    public float CrumbleTime => _crumbleTime;
    public float LingerTime => _lingerTime;
    public float RestoreBlinkTime => _restoreBlinkTime;
    public float BandHeight => _bandHeight;
    /// <summary>コマンドの行に打つ文字（形態で変わる）</summary>
    public string Command => _randomSections && !string.IsNullOrEmpty(_randomCommand) ? _randomCommand : _command;

    /// <summary>k 番目（0 から）の区画が点滅を始める時刻（技の始まりから）</summary>
    public float BlinkAt(int k) => _firstBlinkDelay + SectionStagger * k;
    /// <summary>k 番目の区画が文字にくずれ始める時刻</summary>
    public float CrumbleAt(int k) => BlinkAt(k) + BlinkTime;
    /// <summary>k 番目の区画が文字化けの床になる（当たり始める）時刻</summary>
    public float CorruptAt(int k) => CrumbleAt(k) + _crumbleTime;
    /// <summary>count 区画を消すとき、床が全部戻る時刻</summary>
    public float RestoreAtFor(int count) => CorruptAt(Mathf.Max(1, count) - 1) + _lingerTime;
    /// <summary>床が全部戻る時刻</summary>
    public float RestoreAt => RestoreAtFor(ErasedSections);
    /// <summary>ボスが次の技へ移る時刻（最後の区画が文字化けになった少しあと。第1形態で 2.6秒、第2形態で 2.1秒）</summary>
    public float TechniqueTime => CorruptAt(ErasedSections - 1) + 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        string command = Command;
        TerminalCommandLine line = TerminalCommandLine.Spawn(owner, command);
        TerminalRmRfEffect.Spawn(owner, data, this, PickSections(owner));

        float typing = TerminalCommandLine.TypingTime(command);
        float wait = Mathf.Min(typing, TechniqueTime);
        yield return new WaitForSeconds(wait);
        owner.NotifyReleased(data);
        float rest = TechniqueTime - wait;
        if (rest > 0f) yield return new WaitForSeconds(rest);
        if (line != null) line.Close();
    }

    /// <summary>消す区画の番号（0＝左の端）を、消える順に返す</summary>
    int[] PickSections(AttackRunner owner)
    {
        int count = Mathf.Max(1, _sectionCount);
        int erased = ErasedSections;
        if (_randomSections) return PickRandom(count, erased);

        // 第1形態：端から順番。左からと右からを交互（ボスごとに覚える。ボスが変わると左から）
        bool fromRight = false;
        if (_alternateSides && owner != null)
        {
            var side = owner.GetComponent<TerminalRmRfSide>();
            if (side == null) side = owner.gameObject.AddComponent<TerminalRmRfSide>();
            fromRight = side.NextFromRight;
            side.NextFromRight = !fromRight;
        }
        var list = new int[erased];
        for (int k = 0; k < erased; k++) list[k] = fromRight ? count - 1 - k : k;
        return list;
    }

    /// <summary>
    /// ランダムに erased 個を選ぶ。3つ以上続けて並ぶ選び方は出さない（1・2・3、2・3・4、3・4・5 は出ない）。
    /// 順番もばらばら（同時に消えるので、見た目は同じ）。選べないとき（区画が少ない）は、続いてもよい
    /// </summary>
    public static int[] PickRandom(int count, int erased)
    {
        erased = Mathf.Clamp(erased, 1, count);
        var all = new List<int>();
        for (int i = 0; i < count; i++) all.Add(i);
        for (int tries = 0; tries < 60; tries++)
        {
            Shuffle(all);
            var pick = all.GetRange(0, erased);
            if (!HasRunOfThree(pick, count)) return pick.ToArray();
        }
        // 念のため（まず起きない）：1つおきに選ぶ（0・2・4 …）。足りなければ残りから
        var spaced = new List<int>();
        for (int i = 0; i < count && spaced.Count < erased; i += 2) spaced.Add(i);
        for (int i = 1; i < count && spaced.Count < erased; i += 2) spaced.Add(i);
        return spaced.ToArray();
    }

    /// <summary>3つ以上続けて並ぶ区画が入っているか</summary>
    static bool HasRunOfThree(List<int> pick, int count)
    {
        var on = new bool[count];
        foreach (int i in pick) if (i >= 0 && i < count) on[i] = true;
        int run = 0;
        for (int i = 0; i < count; i++)
        {
            run = on[i] ? run + 1 : 0;
            if (run >= 3) return true;
        }
        return false;
    }

    static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// ステージのギミック用に、その場で作る（2026-11-19：TerminalKit の「床が1区画ずつ消える」。アセットにしない）。
    /// 区画1つ、点滅 blink 秒 → くずれる crumble 秒 → 文字化けの床 linger 秒 → 戻る
    /// </summary>
    public static TerminalRmRfAttack CreateRuntime(float blink, float crumble, float linger, int sectionCount = 5)
    {
        var cfg = CreateInstance<TerminalRmRfAttack>();
        cfg.name = "Terminal_RmRf_Stage";
        cfg.hideFlags = HideFlags.DontSave;
        cfg._sectionCount = Mathf.Max(1, sectionCount);
        cfg._erasedSections = 1;
        cfg._randomSections = false;
        cfg._alternateSides = false;
        cfg._firstBlinkDelay = 0.2f;
        cfg._blinkTime = Mathf.Max(0f, blink);
        cfg._crumbleTime = Mathf.Max(0f, crumble);
        cfg._lingerTime = Mathf.Max(0f, linger);
        return cfg;
    }
}

/// <summary>【仮】第1形態の rm -rf が、次に右から消すか（ボスに付ける。2026-11-19）</summary>
public class TerminalRmRfSide : MonoBehaviour
{
    public bool NextFromRight;
}
