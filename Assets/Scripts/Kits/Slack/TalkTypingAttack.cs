using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】入力の欄に文字を打ちながら、中の技を出す（2026-11-19。担当1 のキットで使い回す）。
/// 例：Slack の @channel（欄に @channel と打つ 0.8秒＝中の柱の予告）。文字は UI をまねた英語の短い言葉だけ（docs 0-2）。
/// 中の技は同時に始める（中の予告の秒数に、打つ時間を合わせる）
/// </summary>
[CreateAssetMenu(fileName = "Talk_Typing", menuName = "Team1/Kits/Talk Typing")]
public class TalkTypingAttack : CustomAttack
{
    [Tooltip("中の攻撃データ（予告つきの技）")]
    [SerializeField] AttackData _inner;
    [Tooltip("欄に打つ文字（英語の短い言葉）")]
    [SerializeField] string _text = "@channel";
    [Tooltip("打ち終わるまでの秒数（0 なら中の予告の秒数）")]
    [SerializeField, Min(0f)] float _typeTime;
    [Tooltip("送信で光る色")]
    [SerializeField] Color _accent = new Color(0.29f, 0.08f, 0.29f, 1f);

    public override bool CanRun(AttackRunner owner, AttackData data) => _inner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_inner == null) yield break;
        float t = _typeTime > 0f ? _typeTime : Mathf.Max(0.5f, _inner.TelegraphTime);
        TalkTypingBox.Spawn(owner, _text, t, _accent);
        yield return owner.Execute(_inner);
    }
}
