using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】オフライン（2026-11-19：本物の Chrome のボスの第2形態の技。docs/app-feedback-v2-design.md 2-3）。代用ボスには渡さない。
/// ネットが切れたときの恐竜ゲーム。プレイヤーが恐竜の役になり、床を走ってくるサボテンと翼竜を跳ぶ（または跳ばずにやりすごす）。
/// ・予告 1.0秒：ボスのウィンドウの中身がオフラインのページ（灰色の恐竜の顔）に、画面に白っぽい幕（3割）、床に地面の線、出てくる側の壁ぎわに赤い「！」
/// ・出てくる側：技が始まった瞬間に、プレイヤーから遠い方の壁。反対の壁へ、床の上を毎秒 8マスで進み、壁に着いたら消える
/// ・並び：サボテン小 → 大 → 翼竜（低）→ サボテン小 → 翼竜（高）→ サボテン大。間は 0.7秒。サボテンの次が低い翼竜のときだけ 1.0秒
///   （ジャンプの滞空 約0.68秒＋0.3秒。PlayerController の 3.2・0.38・落下×1.7 から計算）
/// ・1つずつ、壁から出る 0.8秒前に、出る高さの壁ぎわに赤い「！」（高さで、跳ぶか・跳ばないかが分かる）
/// ・当たるとライフ -1。当たった物は消えない（貫通）
/// 技の間、ボスはほかの技を出さない（Run が最後まで待つ）。場に残る技（攻撃データの「場に残る技」をオン）
/// </summary>
[CreateAssetMenu(fileName = "Chrome_Offline", menuName = "Team1/Custom Attack/Chrome Offline")]
public class ChromeOffline : CustomAttack
{
    [SerializeField] ChromeOfflineSettings _settings = new ChromeOfflineSettings();
    [Tooltip("【仮】並び（S＝サボテン小、B＝サボテン大、L＝翼竜（低）、H＝翼竜（高））")]
    [SerializeField] string _sequence = "SBLSHB";

    public ChromeOfflineSettings Settings => _settings;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        // 出てくる側：プレイヤーから遠い方の壁
        Vector2 player = owner.TargetPoint ?? arena.center;
        bool fromRight = player.x < arena.center.x;
        List<ChromeOfflineEffect.Planned> plan = ChromeOfflineEffect.PlanFromSequence(_sequence, _settings);
        ChromeOfflineEffect fx = ChromeOfflineEffect.CreateInRoom(owner, data, _settings, plan, arena, fromRight);
        owner.NotifyReleased(data);
        float deadline = Time.time + ChromeOfflineEffect.RoomDuration(plan, _settings, arena.width) + 0.5f;
        while (fx != null && Time.time < deadline) yield return null;
    }
}
