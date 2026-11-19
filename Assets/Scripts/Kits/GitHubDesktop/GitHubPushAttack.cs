using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Push origin（GitHub Desktop のボスの技2。2026-11-19。docs/app-kits-2-design.md 2-2）。
/// ボスの頭の上に ↑3 のバッジ、細い赤い線がプレイヤーへ（0.8秒。向きは出た時に決める）→
/// コミットの丸 3つが 0.2秒おきに、同じ線の上を飛ぶ（毎秒 8マス。弾の数値は _bullet）。
/// 第2形態（_branch）：途中（2マス進んだ所）で、上・まっすぐ・下の 3本に枝分かれ（各 2つ）。予告の線も同じ形。
/// </summary>
[CreateAssetMenu(fileName = "GitHub_Push", menuName = "Team1/Kits/GitHub Desktop/Push")]
public class GitHubPushAttack : CustomAttack
{
    [Tooltip("コミットの丸の弾（弾の攻撃データ。名前に枠の語を入れない）")]
    [SerializeField] AttackData _bullet;
    [Tooltip("【仮】丸の数（枝分かれのときは、1本あたりの数）")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("【仮】丸と丸の間（秒）")]
    [SerializeField, Min(0.05f)] float _interval = 0.2f;
    [Tooltip("【仮】予告の線（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】オン：途中で 3本に枝分かれ（第2形態）")]
    [SerializeField] bool _branch;
    [Tooltip("【仮】枝分かれする所（撃つ所からのマス）")]
    [SerializeField, Min(0.5f)] float _branchAt = 2f;
    [Tooltip("【仮】枝の角度（度）")]
    [SerializeField, Range(5f, 60f)] float _branchAngle = 22f;

    public AttackData Bullet => _bullet;
    public int Count => _count;
    public float Interval => _interval;
    public float Telegraph => _telegraph;
    public bool Branch => _branch;
    public float BranchAt => _branchAt;
    public float BranchAngle => _branchAngle;

    public override bool CanRun(AttackRunner owner, AttackData data) => _bullet != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            GitHubPushVolley v = GitHubPushVolley.Spawn(owner, this);
            if (v != null) length = v.Length;
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }
}
