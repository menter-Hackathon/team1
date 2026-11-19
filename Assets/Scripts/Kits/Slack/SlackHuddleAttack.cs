using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Slack のボスの技「ハドル」（2026-11-19。docs/app-kits-1-design.md 2章 P2）。
/// ボスの頭の上にヘッドホンの印 → ボスを中心に輪（直径 8）の予告 1.0秒（ふちが赤く点滅・中は赤 2割）→ 緑の音の輪が 2回（0.4秒ずつ、間 0.8秒）。
/// 2回目の予告は、1回目の判定の直後から続けて出す（まだ危ないことが見える）。避け方：ボスから 4マス離れる
/// </summary>
[CreateAssetMenu(fileName = "Slack_Huddle", menuName = "Team1/Kits/Slack Huddle")]
public class SlackHuddleAttack : CustomAttack
{
    [SerializeField, Min(1f)] float _diameter = 8f;
    [Tooltip("1回目の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1f;
    [Tooltip("音の輪の判定（秒）")]
    [SerializeField, Min(0.05f)] float _pulse = 0.4f;
    [Tooltip("輪と輪の間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.8f;
    [SerializeField, Min(1)] int _pulses = 2;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Bounds body = TalkKitUtil.OwnerBody(owner);
        Vector2 center = body.center;
        float step = _pulse + _gap;
        float total = _telegraph + (_pulses - 1) * step + _pulse;
        SlackHeadphoneMark.Spawn(owner, total + 0.2f);
        Color green = (Color)SlackArt.Green;
        for (int i = 0; i < _pulses; i++)
        {
            // 2回目から：前の判定が始まった直後から予告を出す（予告の長さは 1回目と同じ以上）
            float delay = i == 0 ? 0f : _telegraph + (i - 1) * step + 0.05f;
            float hitAt = _telegraph + i * step;
            TalkRing.Spawn(owner, data, center, _diameter, delay, hitAt - delay, _pulse, TalkBurst.Sound, green, owner.transform);
        }
        float end = Time.time + total + 0.05f;
        while (Time.time < end) yield return null;
    }
}
