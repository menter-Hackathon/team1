using UnityEngine;

/// <summary>
/// 【仮】ラスボス Terminal に足す仕組み：sudo reboot（形態変化の演出）とカーネル・パニック（第2形態の背景）（2026-11-18。docs/app-boss-skills-design.md 4-4）。
/// Boss_Terminal の「ボスに足す仕組み」に入れると、ボスに TerminalReboot の部品が1つ足される（2回は足さない）。
/// 形態変化の仕組み（ゲージ50%で形態変化、その間は無敵）はそのまま使い、見せ方だけを変える
/// </summary>
[CreateAssetMenu(fileName = "Terminal_Reboot", menuName = "Team1/Boss Extension/Terminal Reboot")]
public class TerminalRebootExtension : BossExtension
{
    public override void Attach(BossController boss)
    {
        if (boss == null) return;
        if (!boss.TryGetComponent(out TerminalReboot _)) boss.gameObject.AddComponent<TerminalReboot>();
    }
}
