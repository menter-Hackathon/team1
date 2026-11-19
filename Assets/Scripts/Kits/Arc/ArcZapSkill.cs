using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Zap（Arc の能力。2026-11-19。docs/app-kits-2-design.md 4-5）。
/// いちばん近い敵（6マス以内）に稲妻。先に、自分のまわり 2マス以内の敵の弾をまとめて消す（KitBullets）。
/// 敵がいなければ、正面へ 4マスの稲妻（その先に当たれば当たる）。ゲージ・ダメージは攻撃データの共通の欄。ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "Arc_Zap", menuName = "Team1/Kits/Arc Zap")]
public class ArcZapSkill : CustomAttack
{
    [SerializeField, Min(1f)] float _range = 6f;
    [Tooltip("弾を消す半径（マス）")]
    [SerializeField, Min(0f)] float _clearRadius = 2f;
    [Tooltip("敵がいないときの長さ（マス）")]
    [SerializeField, Min(1f)] float _fizzle = 4f;
    [Tooltip("当たりの大きさ（マス）")]
    [SerializeField, Min(0.2f)] float _hitSize = 1.2f;
    [SerializeField, Min(0.02f)] float _active = 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 from = owner.OriginPoint + new Vector2(owner.Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        int cleared = _clearRadius > 0f ? KitBullets.ClearEnemyProjectiles(from, _clearRadius) : 0;
        if (cleared > 0) FxKit.Ring(from, _clearRadius * 2f, 0.25f, ArcArt.Pink);

        Vector2 to = from + new Vector2(owner.Facing * _fizzle, 0f);
        foreach (Hurtbox h in LockOnStrike.FindTargets(owner))
        {
            Vector2 c = h.Collider.bounds.center;
            if (Vector2.Distance(from, c) > _range) continue;
            to = c;
            break;
        }

        // 稲妻（見た目）と当たり
        Transform deco = PathStroke.Decor(owner, Vector2.zero, 0.18f, "ArcZap");
        ArcArt.Lightning(deco, from, to, 32);
        var go = new GameObject("ZapHit");
        go.layer = owner.gameObject.layer;
        go.transform.position = to;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(_hitSize, _hitSize);
        Hitbox hit = owner.AddHitbox(go, data, box);
        hit.Activate();
        Destroy(go, _active);
        FxKit.Ring(to, 1.4f, 0.2f, ArcArt.Purple);
        FxKit.Text("Zap", to + new Vector2(0f, 0.9f), 0.3f, Color.white, 47, 0.4f, ArcArt.Purple);
        owner.NotifyReleased(data);
        yield return null;
    }
}
