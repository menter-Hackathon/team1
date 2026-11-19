using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】ふたご座（2026-11-19。Gemini の第1形態の技。docs/app-kits-1-design.md 7章 P1-2）。担当3 の物。
/// 部屋の反対側に、ボスの「ふたご」（左右反転した青紫のうすい影）が現れる（0.8秒）→ ボスが低い弾を 1発 → 0.7秒後、ふたごが低い弾を 1発。
/// 第2形態は 低・高・低 の 3発（ボス・ふたご・ボスの順）。弾は毎秒 9マス。通り道に赤い細い線（WallShot。0.8秒）。
/// ・避け方：ジャンプを 2回（高い弾は、床にいれば当たらない）
/// ・弾の速さ・大きさ・寿命・ダメージは攻撃データの「弾」の欄
/// </summary>
[CreateAssetMenu(fileName = "Gemini_Twin", menuName = "Team1/Kits/Gemini Twin")]
public class GeminiTwinAttack : CustomAttack
{
    [Tooltip("【仮】弾の高さの並び（ボス・ふたご・ボス…の順に撃つ）")]
    [SerializeField] WallShotHeight[] _shots = { WallShotHeight.Low, WallShotHeight.Low };
    [Tooltip("【仮】ふたごが現れるまで（1発目が出るまで。秒）")]
    [SerializeField, Min(0.8f)] float _appearTime = 0.8f;
    [Tooltip("【仮】弾の間隔（秒）")]
    [SerializeField, Min(0.3f)] float _gap = 0.7f;
    [Tooltip("【仮】ボスとふたごの、最低の離れ方（マス）")]
    [SerializeField, Min(2f)] float _minApart = 7f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        int n = _shots != null && _shots.Length > 0 ? _shots.Length : 0;
        if (n == 0) yield break;
        Rect area = WallShot.AreaFor(owner);
        float floor = WallShot.FloorY(owner, area);
        Bounds body = GeminiArt.BodyOf(owner);
        float half = body.extents.x;
        // ふたごの x：部屋の真ん中で折り返した所。近すぎれば、ボスと反対の端
        float bx = body.center.x;
        float tx = area.center.x - (bx - area.center.x);
        if (Mathf.Abs(tx - bx) < _minApart) tx = bx >= area.center.x ? area.xMin + half + 0.6f : area.xMax - half - 0.6f;
        tx = Mathf.Clamp(tx, area.xMin + half + 0.3f, area.xMax - half - 0.3f);
        float duration = _appearTime + (n - 1) * _gap + 0.4f;
        var twinRoot = new Vector2(owner.transform.position.x + (tx - bx), owner.transform.position.y);
        GeminiTwinShadow.Spawn(owner, twinRoot, duration);

        float start = Time.time;
        for (int k = 0; k < n; k++)
        {
            float fireAt = start + _appearTime + k * _gap;
            float callAt = fireAt - WallShot.DefaultTelegraph;
            float wait = callAt - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            bool fromBoss = k % 2 == 0;
            float sx = fromBoss ? bx : tx;
            float ox = fromBoss ? tx : bx;
            int dir = ox >= sx ? 1 : -1;
            var from = new Vector2(sx + dir * (half * 0.6f), floor + WallShot.HeightOf(_shots[k]));
            WallShot.FireFrom(owner, data, from, dir, WallShot.DefaultTelegraph, GeminiArt.StarBullet);
            owner.NotifyReleased(data);
        }
        float rest = start + duration - Time.time;
        if (rest > 0f) yield return new WaitForSeconds(rest);
    }
}
