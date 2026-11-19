using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メッセージの能力「花火」（2026-11-19。docs/app-kits-1-design.md 16章）。
/// 正面 4マス先・高さ 3 を中心に、直径 4 の花火が 3発（0.2秒おき、少しずつ位置をずらす）。1発ごとにゲージ（攻撃データの共通の欄。+4）。
/// 雑魚は倒れる。空中に出るので、浮いているボスに床から届く。プレイヤーの物なので予告（赤）は出さない
/// </summary>
[CreateAssetMenu(fileName = "Messages_FireworkSkill", menuName = "Team1/Kits/Messages Firework Skill")]
public class MessagesFireworkSkill : CustomAttack
{
    [SerializeField] float _forward = 4f;
    [SerializeField] float _height = 3f;
    [SerializeField, Min(0.5f)] float _diameter = 4f;
    [SerializeField, Min(1)] int _count = 3;
    [SerializeField, Min(0f)] float _interval = 0.2f;
    [SerializeField, Min(0.05f)] float _active = 0.25f;

    static readonly Vector2[] Offsets = { Vector2.zero, new Vector2(0.7f, 0.6f), new Vector2(-0.5f, -0.5f), new Vector2(0.4f, -0.7f), new Vector2(-0.7f, 0.5f) };

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 center = owner.OriginPoint + new Vector2(owner.Facing * _forward, _height);
        for (int i = 0; i < _count; i++)
        {
            Vector2 o = Offsets[i % Offsets.Length];
            Vector2 p = center + new Vector2(o.x * owner.Facing, o.y);
            Color col = (Color)MessagesArt.FireworkColors[i % MessagesArt.FireworkColors.Length];
            TalkRing.Spawn(owner, data, p, _diameter, i * _interval, 0f, _active, TalkBurst.Firework, col);
        }
        owner.NotifyReleased(data);
        yield break;
    }
}
