using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力 Revert（GitHub Desktop を倒すと手に入る。2026-11-19。docs/app-kits-2-design.md 2-2）。2回押し。
/// ・1回目：今いる場所（空中なら真下の床・足場）にコミットの丸を置く（6秒残る）。クールタイムは始めない
/// ・2回目：丸の場所へ一瞬で戻る。戻る前の場所と丸を結ぶ線の上の敵に当たる（ダメージ・ゲージは攻撃データの共通の欄）。戻ってから 0.3秒は無敵
/// ・丸が消えたら、次の1回目からやり直し（クールタイムは始まっていない）
/// 状態はプレイヤーに実行時に足す部品（GitHubRevertHolder）が持つ。
/// </summary>
[CreateAssetMenu(fileName = "GitHub_Revert", menuName = "Team1/Kits/GitHub Desktop/Revert")]
public class GitHubRevertAttack : CustomAttack
{
    [Tooltip("【仮】丸が残る秒数")]
    [SerializeField, Min(0.5f)] float _keep = 6f;
    [Tooltip("【仮】線の判定の太さ（マス）")]
    [SerializeField, Min(0.2f)] float _width = 1.2f;
    [Tooltip("【仮】判定の時間（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;
    [Tooltip("【仮】戻ってからの無敵（秒）")]
    [SerializeField, Min(0f)] float _invincible = 0.3f;

    public float Keep => _keep;
    public float Width => _width;
    public float HitTime => _hitTime;
    public float Invincible => _invincible;

    public override bool CanRun(AttackRunner owner, AttackData data) => owner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        // 状態は最初の yield より前に変える（PlayerSkillExecutor が Run の直後に DefersCooldown を聞く）
        try
        {
            GitHubRevertHolder holder = GitHubRevertHolder.GetOrAdd(owner, this);
            if (holder.HasCommit) holder.Revert(data);
            else holder.Place();
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        yield break;
    }

    /// <summary>丸を置いたあとはクールタイムを始めない。戻ったときだけ始める</summary>
    public override bool DefersCooldown(AttackRunner owner)
    {
        GitHubRevertHolder h = owner != null ? owner.GetComponent<GitHubRevertHolder>() : null;
        if (h == null) return true;
        return h.LastRevertFrame != Time.frameCount;
    }

    /// <summary>丸を置いている間は、枠の絵を紫の丸にする</summary>
    public override Sprite DisplayIcon(AttackRunner owner)
    {
        GitHubRevertHolder h = owner != null ? owner.GetComponent<GitHubRevertHolder>() : null;
        return h != null && h.HasCommit ? GitHubDesktopArt.RevertIcon() : null;
    }

    /// <summary>丸を置いている間（もう一度押せる）は、枠のふちを光らせる</summary>
    public override bool IsCharged(AttackRunner owner)
    {
        GitHubRevertHolder h = owner != null ? owner.GetComponent<GitHubRevertHolder>() : null;
        return h != null && h.HasCommit;
    }

    public override Color ChargedColor => GitHubDesktopArt.Purple;
}
