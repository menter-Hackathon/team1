using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力「プロンプト」（2026-11-19。ChatGPT の能力。docs/app-kits-1-design.md 5章）。担当3 の物。
/// 押すと、プレイヤーの頭の上に「考え中」の点 3つ（0.5秒。動ける）→ 正面 3マス先に、長文の柱（幅 2×高さ 6、0.4秒）。
/// ・柱は中の攻撃データ（指定位置の判定。名前に Breakpoint ＝ AI のスキンの「入力の欄 → 長い回答」）。ゲージ +18
/// ・出るのが遅いので、止まっている相手（ギミック「考え中…」のボス）に当てる能力
/// ・クールタイムは外の攻撃データ（能力の攻撃データ）の欄
/// </summary>
[CreateAssetMenu(fileName = "ChatGPT_Prompt", menuName = "Team1/Kits/ChatGPT Prompt")]
public class ChatGptPromptAttack : CustomAttack
{
    [Tooltip("柱（指定位置の判定。出す場所は前方 3マス）")]
    [SerializeField] AttackData _pillar;
    [Tooltip("【仮】考え中の秒数（この間は動ける）")]
    [SerializeField, Min(0f)] float _thinkTime = 0.5f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _pillar != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_pillar == null) yield break;
        // 考えている間にほかの能力を押しても、柱の見た目（AI のスキン）が変わらないように、この能力の見た目を覚えさせる
        SkinDresser.Look look = SkinDresser.LookOf(owner, data);
        if (look.Skin != SkinId.None) SkinDresser.Remember(_pillar, look.Skin, look.Theme, look.Icon);
        if (_thinkTime > 0f)
        {
            ChatGptThinkingDots.Attach(owner.transform, new Vector2(0f, 1.55f), _thinkTime, 0.75f);
            yield return new WaitForSeconds(_thinkTime);
        }
        owner.NotifyReleased(data);
        yield return owner.Execute(_pillar);
    }
}
