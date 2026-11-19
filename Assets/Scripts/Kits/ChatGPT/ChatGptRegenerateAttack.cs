using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Regenerate（2026-11-19。ChatGPT の第2形態の技。docs/app-kits-1-design.md 5章 P2）。担当3 の物。
/// ボスの頭の上に回る矢印 → 長文回答の柱が、そのときのプレイヤーの位置に 3回（0.9秒おき。毎回、予告 0.8秒）。
/// ・柱は中の攻撃データ（長文回答。名前に Breakpoint ＝ AI のスキンの「入力の欄 → 長い回答」）を、そのまま AreaStrike で出す
///   （AttackRunner の指定位置の判定は、くり返しで狙い直さないので、ここで狙い直す）
/// ・避け方：1回ごとに 2マス動く。同じ向きに歩き続ければよい
/// </summary>
[CreateAssetMenu(fileName = "ChatGPT_Regenerate", menuName = "Team1/Kits/ChatGPT Regenerate")]
public class ChatGptRegenerateAttack : CustomAttack
{
    [Tooltip("柱（長文回答の攻撃データ。指定位置の判定）")]
    [SerializeField] AttackData _pillar;
    [Tooltip("【仮】柱の数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("【仮】柱の間隔（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.9f;
    [Tooltip("【仮】回る矢印を見せてから、1本目を出すまで（秒）")]
    [SerializeField, Min(0f)] float _lead = 0.4f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _pillar != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_pillar == null) yield break;
        float total = _lead + (_count - 1) * _interval + _pillar.TelegraphTime + _pillar.HitWindow;
        // 柱は矢印の子にする（矢印は CustomEffect なので、形態変化で柱もいっしょに消える）
        GameObject holder = ShowArrow(owner, total + 0.1f);
        if (_lead > 0f) yield return new WaitForSeconds(_lead);
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            Vector2 at = owner.TargetPoint ?? owner.OriginPoint;
            AreaStrike s = AreaStrike.Spawn(owner, _pillar, at, Vector2.up, 0f, i);
            if (s != null && holder != null) s.transform.SetParent(holder.transform, true);
            owner.NotifyReleased(data);
        }
        yield return new WaitForSeconds(_pillar.TelegraphTime + _pillar.HitWindow);
    }

    /// <summary>ボスの頭の上で、回る矢印（形態変化で消えるように、出した物として登録）</summary>
    static GameObject ShowArrow(AttackRunner owner, float seconds)
    {
        Bounds body = ChatGptArt.BodyOf(owner);
        GameObject go = owner.CreateSpawnedObject("ChatGPTRegenArrow", new Vector2(body.center.x, body.max.y + 0.7f));
        go.AddComponent<ChatGptSpinner>().Setup(ChatGptArt.RegenArrow, seconds, -360f, 1.6f);
        return go;
    }
}
