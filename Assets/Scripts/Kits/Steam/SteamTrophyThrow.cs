using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Steam の能力：トロフィー（2026-11-19。docs/app-kits-2-design.md 2-3）。クールタイム 6秒・ゲージ 10。
/// 金のトロフィーを山なりに投げる。敵に当たると、右下に Achievement unlocked の通知が出て、6秒間 与えるゲージ ×1.3（HUD に印）。
/// 外れたら何も付かない。ステージの決まりに頼らない（ほかのステージ・Terminal でも同じに動く）
/// </summary>
[CreateAssetMenu(fileName = "Steam_Trophy", menuName = "Team1/Kits/Steam Trophy")]
public class SteamTrophyThrow : CustomAttack
{
    public const string BuffKey = "steam.trophy";

    [Tooltip("トロフィーの弾（種類 Projectile。重力つき。ダメージ・ゲージはこちら）")]
    [SerializeField] AttackData _shot;
    [Tooltip("投げる角度（度）")]
    [SerializeField, Range(0f, 85f)] float _angle = 40f;
    [Tooltip("当たったときの、与えるゲージの倍率")]
    [SerializeField, Min(1f)] float _gaugeMult = 1.3f;
    [Tooltip("倍率の秒数")]
    [SerializeField, Min(0.5f)] float _buffSeconds = 6f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _shot != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float rad = _angle * Mathf.Deg2Rad;
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * _shot.SpawnOffset.x, _shot.SpawnOffset.y);
        Vector2 dir = new Vector2(owner.Facing * Mathf.Cos(rad), Mathf.Sin(rad));
        Projectile p = Projectile.Spawn(owner, _shot, origin, dir);
        if (p == null) yield break;
        KitDress.Swap(p.gameObject, SteamArt.Trophy);
        Hitbox hb = p.GetComponent<Hitbox>();
        if (hb != null)
        {
            float mult = _gaugeMult, secs = _buffSeconds;
            bool done = false;
            hb.Hit += (target, info) =>
            {
                if (done) return;
                done = true;
                Unlock(mult, secs, info.HitPoint);
            };
        }
        owner.NotifyReleased(data);
        yield break;
    }

    static void Unlock(float mult, float seconds, Vector2 at)
    {
        StatMods.Player.GaugeGain.SetFor(BuffKey, mult, seconds, SteamArt.TrophyIcon);
        int pct = Mathf.RoundToInt((mult - 1f) * 100f);
        SteamToast.Show("Trophy Hunter", false, new (Sprite, string)[] { (SteamArt.Sword, $"+{pct}%") });
        FxKit.Ring(at, 2.2f, 0.3f, (Color)SteamArt.Gold);
    }
}
