using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力「アーティファクト」（2026-11-19。Claude の能力。docs/app-kits-1-design.md 6章）。担当3 の物。
/// 正面 2.5マス先・高さ 2 に、パネル（横 3×縦 0.6）が開く。開く瞬間、重なった敵にゲージ +10（雑魚は倒れる）。
/// パネルは 4秒、足場として残る（乗れる。敵の弾は止めない）。浮いているボスに近接を届かせるための足場。
/// ・パネルは中の攻撃データ（落下物・出現型。FallingObject.SpawnInPlace）を、決めた位置に出す。絵はクリーム色のパネルに差し替える
/// ・クールタイムは外の攻撃データ（能力の攻撃データ）の欄
/// </summary>
[CreateAssetMenu(fileName = "Claude_Artifact", menuName = "Team1/Kits/Claude Artifact")]
public class ClaudeArtifactAttack : CustomAttack
{
    [Tooltip("パネル（落下物。大きさ・判定の秒数・残る秒数・足場・ゲージ・ダメージ）")]
    [SerializeField] AttackData _panel;
    [Tooltip("【仮】前へのずれ（マス）")]
    [SerializeField] float _forward = 2.5f;
    [Tooltip("【仮】足元からの高さ（パネルの真ん中まで。マス）")]
    [SerializeField] float _height = 2f;
    [Tooltip("【仮】開くまでの秒数（出る場所の枠が一瞬見える）")]
    [SerializeField, Min(0f)] float _openDelay = 0.08f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _panel != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_panel == null) yield break;
        Vector2 feet = owner.OriginPoint;
        Vector2 c = feet + new Vector2(owner.Facing * _forward, _height);
        Rect arena = BattleArena.GetBounds();
        Vector2 half = _panel.ObjectSize * 0.5f;
        if (arena.Contains(feet))
        {
            c.x = Mathf.Clamp(c.x, arena.xMin + half.x, arena.xMax - half.x);
            c.y = Mathf.Min(c.y, arena.yMax - half.y);
        }
        FallingObject f = FallingObject.SpawnInPlace(owner, _panel, c, Time.time + _openDelay);
        if (f != null) KitDress.Swap(f.gameObject, ClaudeArt.ArtifactPanel);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_openDelay + _panel.ActiveTime);
        // 開いた瞬間の光（横に開く Artifacts のパネル）
        FxAnim.Sprite(ClaudeArt.Spark(9), c + new Vector2(-half.x + 0.2f, half.y), 34, 0.3f, Color.white, 1f, "ClaudeArtifactOpen").Spin(360f).FadeFrom(0.4f);
    }
}
