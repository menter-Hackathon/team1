using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Steam のボスの技3（第2形態）：フレンドの通知（2026-11-19。docs/app-kits-2-design.md 2-3）。
/// ボスと反対の端のふちに、赤い線の予告が順に出る（各 0.8秒前。高さは 床・足場・その上）→ 横長の通知（3×1マス）が毎秒 7マスで横切る。0.7秒おき。
/// ・床の通知はジャンプ。足場の高さの通知は床にいる。その上の通知は、足場から跳ばなければ当たらない
/// ・横の弾は共通の WallShot（予告の赤い線つき）。絵だけ「PLAYER 2 ONLINE」の通知に差し替える
/// </summary>
[CreateAssetMenu(fileName = "Steam_Friends", menuName = "Team1/Kits/Steam Friends")]
public class SteamFriendOnline : CustomAttack
{
    [Tooltip("通知の弾（種類 Projectile。速さ 7・大きさ 3×1）")]
    [SerializeField] AttackData _card;
    [Tooltip("通る高さ（床からの、通知の真ん中の高さ。マス）")]
    [SerializeField] float[] _heights = { 0.6f, 3.6f, 5.6f };
    [Tooltip("1つずつの間（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.7f;
    [Tooltip("予告の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _card != null && _heights != null && _heights.Length > 0;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        // ボスと反対の端から、ボスの方へ流れる
        int dir = owner.transform.position.x >= arena.center.x ? 1 : -1;
        float half = _card.HitSize.x * 0.5f;
        float x = dir > 0 ? arena.xMin + half : arena.xMax - half;
        float floor = WallShot.FloorY(owner, WallShot.AreaFor(owner));
        WallShotTelegraph last = null;
        for (int i = 0; i < _heights.Length; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            last = WallShot.FireFrom(owner, _card, new Vector2(x, floor + _heights[i]), dir, _telegraph, SteamArt.FriendCard, p => Upright(p, dir));
        }
        owner.NotifyReleased(data);
        // 最後の通知が出るまで待つ（横切るのは自分で進む）
        float until = Time.time + _telegraph + 0.3f;
        while (Time.time < until) yield return null;
    }

    /// <summary>左へ飛ぶ弾は回って逆さになるので、絵だけ戻す</summary>
    static void Upright(Projectile p, int dir)
    {
        if (p == null || dir > 0) return;
        SpriteRenderer v = KitDress.FindVisual(p.transform);
        if (v != null) v.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }
}
