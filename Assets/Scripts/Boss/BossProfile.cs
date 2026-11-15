using System;
using UnityEngine;

/// <summary>ボスの移動のしかた</summary>
public enum BossMovement
{
    /// <summary>動かない</summary>
    None = 0,
    /// <summary>決まった数か所を、一定の速さで飛んで移動する（Chrome）</summary>
    Hover = 1,
    /// <summary>瞬間移動の攻撃データ（Warp）で移動する（VS Code）</summary>
    Warp = 2,
}

/// <summary>形態ごとの設定</summary>
[Serializable]
public class BossPhaseSettings
{
    [Tooltip("この形態で出す技。同じ技は2回続けない（2つなら交互になる）")]
    public AttackData[] Moves;
    [Tooltip("攻撃が終わってから、次の攻撃を始めるまでの待ち時間（秒）")]
    [Min(0f)] public float WaitAfterAttack = 1.2f;
    [Tooltip("何回攻撃するごとに移動するか。0なら移動しない")]
    [Min(0)] public int AttacksPerMove = 2;
}

/// <summary>
/// アプリボス1体分の設定（docs 3〜5章）。BossController が読む。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Boss Profile
/// </summary>
[CreateAssetMenu(fileName = "Boss_", menuName = "Team1/Boss Profile")]
public class BossProfile : ScriptableObject
{
    [SerializeField] string _displayName;

    [Header("ウイルス解除ゲージ（docs 3章）")]
    [SerializeField, Min(1)] int _gaugeMax = 100;
    [Tooltip("第2形態になるゲージの割合")]
    [SerializeField, Range(0.05f, 0.95f)] float _phase2Threshold = 0.5f;
    [Tooltip("形態変化の演出（アップデート中…）の秒数。この間は無敵")]
    [SerializeField, Min(0f)] float _transitionTime = 1.5f;

    [Header("行動")]
    [Tooltip("戦闘開始から最初の攻撃までの秒数【仮】")]
    [SerializeField, Min(0f)] float _startDelay = 1f;
    [SerializeField] BossPhaseSettings _phase1 = new BossPhaseSettings { WaitAfterAttack = 1.2f };
    [SerializeField] BossPhaseSettings _phase2 = new BossPhaseSettings { WaitAfterAttack = 0.8f };

    [Header("移動")]
    [SerializeField] BossMovement _movement = BossMovement.None;
    [Tooltip("移動に使う瞬間移動の攻撃データ（移動が Warp のとき）")]
    [SerializeField] AttackData _warpAttack;
    [Tooltip("飛んで移動する速さ（マス/秒、移動が Hover のとき）")]
    [SerializeField, Min(0.1f)] float _hoverSpeed = 3f;
    [Tooltip("飛んで移動する先の x（部屋の真ん中からの距離、移動が Hover のとき）")]
    [SerializeField] float[] _hoverPointsX = { -7f, 0f, 7f };

    [Header("正常化したときに手に入る能力")]
    [SerializeField] SkillDefinition _reward;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public int GaugeMax => _gaugeMax;
    public float Phase2Threshold => _phase2Threshold;
    public float TransitionTime => _transitionTime;
    public float StartDelay => _startDelay;
    public BossPhaseSettings Phase1 => _phase1;
    public BossPhaseSettings Phase2 => _phase2;
    public BossMovement Movement => _movement;
    public AttackData WarpAttack => _warpAttack;
    public float HoverSpeed => _hoverSpeed;
    public float[] HoverPointsX => _hoverPointsX;
    public SkillDefinition Reward => _reward;

    /// <summary>形態の設定（1 か 2）</summary>
    public BossPhaseSettings GetPhase(int phase) => phase >= 2 ? _phase2 : _phase1;
}
