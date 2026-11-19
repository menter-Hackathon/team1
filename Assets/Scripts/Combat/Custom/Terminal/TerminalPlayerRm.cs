using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力 rm -rf（2026-11-19：Terminal を倒すと手に入る能力。docs/app-feedback-v2-design.md 6-3）。
/// ボスが使っていた rm -rf を、今度は自分が使って、床の上の敵を消す。1回押し。
/// ・押すと、頭の上に小さいコマンドの行（% rm -rf ./virus）が 0.2秒で打たれる（飾り。待たない）
/// ・同時に、足元から文字化けの波が、向いている方へ床の上を走る（TerminalPlayerRmWave）。
///   触れた雑魚にダメージ、ボスにゲージ（1回だけ）。触れた敵の弾は消える。貫通
/// ・空中で押したら、真下の床から走る。壁で止まる
/// ダメージ・ゲージ・クールタイムは攻撃データの共通の欄（雑魚 3・ゲージ +14・5秒）。赤は使わない（赤い文字化けはボスの物）
/// </summary>
[CreateAssetMenu(fileName = "Terminal_PlayerRm", menuName = "Team1/Custom Attack/Terminal rm -rf (player)")]
public class TerminalPlayerRm : CustomAttack
{
    [Tooltip("【仮】頭の上に打つコマンド（飾り）")]
    [SerializeField] string _command = "% rm -rf ./virus";
    [Tooltip("【仮】コマンドを打ち終わるまで（秒。待たない）")]
    [SerializeField, Min(0.01f)] float _typeTime = 0.2f;
    [Tooltip("【仮】波の大きさ（横×高さ、マス）")]
    [SerializeField] Vector2 _waveSize = new Vector2(2f, 3.5f);
    [Tooltip("【仮】波の速さ（毎秒マス）")]
    [SerializeField, Min(0.1f)] float _speed = 14f;
    [Tooltip("【仮】波が走る距離（マス）")]
    [SerializeField, Min(0.1f)] float _distance = 10f;
    [Tooltip("【仮】通った床に残る緑の字の秒数（飾り。当たらない）")]
    [SerializeField, Min(0f)] float _trailLife = 0.5f;

    public Vector2 WaveSize => _waveSize;
    public float Speed => _speed;
    public float Distance => _distance;
    public float TrailLife => _trailLife;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Vector2 feet = owner.transform.position;
        int dir = owner.Facing >= 0 ? 1 : -1;

        // 真下の床（空中でも）。体の少し上から探す
        // 床が見つからない（穴の上など）ときは、足元の高さから走る
        if (!owner.TryFindGround(feet.x, feet.y + 0.3f, out float groundY) || groundY > feet.y + 0.3f) groundY = feet.y;

        TypeCommand(owner);
        TerminalPlayerRmWave.Spawn(owner, data, this, new Vector2(feet.x, groundY), dir);
        owner.NotifyReleased(data);
    }

    /// <summary>頭の上に、小さいコマンドの行を打つ（飾り。0.2秒で打ち終わり、少し残って消える）</summary>
    void TypeCommand(AttackRunner owner)
    {
        const float h = 0.4f;
        Vector2 head = FxKit.HeadOf(owner.gameObject, (Vector2)owner.transform.position + Vector2.up);
        float half = FxKit.TextWidth(_command, h) * 0.5f;
        Vector2 at = FxKit.ClampToView(head + new Vector2(0f, 0.45f), half + 0.2f);
        FxAnim a = FxAnim.Create("TerminalPlayerRmCommand", at, 0.75f);
        FxKit.AddText(a.transform, _command, new Vector2(-half, 0f), h, TerminalArt.Green, 52, TerminalArt.Black, true, TextAnchor.MiddleLeft);
        SpriteRenderer cursor = FxKit.Box(a.transform, new Vector2(-half, 0f), new Vector2(h * 0.55f, h), TerminalArt.White, 52, "Cursor");
        a.gameObject.AddComponent<TerminalTyping>().Setup(_command, _typeTime, -half, h, cursor);
        a.Refresh().FadeFrom(0.6f);
    }

    /// <summary>
    /// HUD の枠の絵（黒い地に緑の rm）。能力のアセットに絵のファイルが無いので、コードの中のドット絵を出す【仮】。
    /// カード・リザルトの絵は、能力のアセットの絵（担当0 に頼む）
    /// </summary>
    public override Sprite DisplayIcon(AttackRunner owner) => TerminalArt.RmIcon;
}
