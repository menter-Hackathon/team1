using UnityEngine;

/// <summary>
/// 【仮・見本】KitTimedEvent と KitMotion で書いた場面③の出来事（2026-11-19）。担当0 の物。
/// どのアプリにも登録していない（書き方の見本）。使うときは、キットのファイルで
///   StageEvents.Register("Slack", stage => new KitMotionSampleEvent());
/// のように登録する（[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] の中で）
/// </summary>
public class KitMotionSampleEvent : KitTimedEvent
{
    public KitMotionSampleEvent() : base("Sample") { }

    protected override void Build()
    {
        // 長さは 10秒（StagePacer は、ゲートのステージの出来事を 12秒で打ち切る。通知からの 0.8秒も入る）
        Every(0f, 1.4f, 3, i => KitMotion.Erupt(Owner, KitMotion.PlayerPos.x, 1.6f, 2.6f));                // 下から：プレイヤーの下に3回
        At(0.8f, () => KitMotion.EdgeSweep(Owner, KitSide.Right, 0.5f, speed: 7f, count: 2, gap: 0.9f));     // 横から：低い物（ジャンプ）
        At(3.5f, () => KitMotion.Orbit(Owner, View.center + Vector2.up * 2f, 2.2f, 100f, count: 3, duration: 4f)); // 回る
        At(4f, () => KitMotion.HitTarget(Owner, View.center + Vector2.up * 2f, 3f,
            onHit: () => { KitMotion.ClearHazards(); KitPlayer.RefillCooldowns(); },                         // 叩くと回る物が消える
            onMiss: () => KitMotion.FloorWave(Owner, KitSide.Both, speed: 6f, meet: true)));                 // 叩かないと床の波
        At(7.5f, () => KitMotion.HoldZone(Owner, View.center.x, 3f, 1.2f, () => KitPlayer.Heal(), life: 2.5f)); // 立つと回復
        EndAt(10f);
    }
}
