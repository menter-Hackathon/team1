using UnityEngine;

/// <summary>
/// タグの弾（スマートフォルダマグネット。2026-11-18。docs/app-boss-skills-design.md 2-2）。ダメージなし。
/// ・まっすぐ飛び、プレイヤーの被弾範囲に重なったら、その色のタグを付けて消える（Hitbox は使わず、自分で重なりを調べる）
/// ・壁・床（撃った人の「弾が当たって止まる地形」）・部屋の外・寿命で消える。足場（フォルダ）はすり抜ける
/// ・zip圧縮で吸いこめる（FinderZippable。吸うときに見つかるように、トリガーのコライダーを持つ）
/// FinderSmartFolderEffect の子として作る（技が消されると一緒に消える）
/// </summary>
public class FinderTagBullet : MonoBehaviour
{
    FinderSmartFolderEffect _effect;
    AttackRunner _owner;
    Vector2 _velocity;
    float _dieAt;
    float _radius;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[2];

    /// <summary>色（0＝赤、1＝青）</summary>
    public int Tag { get; private set; }

    public static FinderTagBullet Spawn(FinderSmartFolderEffect effect, AttackRunner owner, int tag, Vector2 position, Vector2 direction,
        float speed, float diameter, float lifetime)
    {
        Transform t = FinderArt.Child(effect.transform, $"TagBullet_{tag}", position);
        GameObject go = t.gameObject;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = diameter * 0.5f;
        go.AddComponent<FinderZippable>();
        AttackRunner.AddVisual(t, FinderArt.TagBall(9, tag), Color.white, Vector2.one * diameter, 21, "Visual");

        var b = go.AddComponent<FinderTagBullet>();
        b._effect = effect;
        b._owner = owner;
        b.Tag = tag;
        b._velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down) * speed;
        b._dieAt = Time.time + lifetime;
        b._radius = diameter * 0.5f;
        return b;
    }

    void Update()
    {
        if (_owner == null || _effect == null || Time.time >= _dieAt)
        {
            Destroy(gameObject);
            return;
        }
        Vector2 pos = transform.position;
        Vector2 delta = _velocity * Time.deltaTime;
        float dist = delta.magnitude;
        if (dist > 0f)
        {
            // 壁・床に当たったら消える
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(_owner.SolidMask);
            if (Physics2D.CircleCast(pos, _radius * 0.6f, delta / dist, filter, _hits, dist) > 0)
            {
                Destroy(gameObject);
                return;
            }
        }
        Vector2 next = pos + delta;
        transform.position = next;

        // プレイヤーに当たったら、タグを付けて消える
        if (FinderArt.TouchesTarget(_owner, next, _radius))
        {
            _effect.SetTag(Tag);
            Destroy(gameObject);
            return;
        }

        // 部屋の外へ出たら消える
        Rect arena = BattleArena.GetBounds();
        if (next.x < arena.xMin - 1f || next.x > arena.xMax + 1f || next.y < arena.yMin - 1f || next.y > arena.yMax + 1f) Destroy(gameObject);
    }
}
