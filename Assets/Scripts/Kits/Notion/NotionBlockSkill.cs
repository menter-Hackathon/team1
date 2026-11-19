using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Notion の能力「ブロック」（2026-11-19。docs/app-kits-1-design.md 8章。担当0）。
/// ・正面 1.5マス先の床に、白いブロック（2×2）を置く。出る瞬間、重なった敵に当たる（ゲージ・ダメージは攻撃データの共通の欄）
/// ・ブロックは 6秒残る壁：自分は通れない・上に乗れる（地形のレイヤー）。敵の弾を止める（弾だけを止める物のレイヤー。追撃を出さずに消える）
/// ・空中で使ったら、真下の床に置く。最後の 1秒は点滅して消える
/// </summary>
[CreateAssetMenu(fileName = "NotionBlockSkill", menuName = "Team1/Kits/Notion/Block Skill")]
public class NotionBlockSkill : CustomAttack
{
    [Tooltip("置く所（正面の何マス先。ブロックの真ん中）")]
    [SerializeField, Min(0f)] float _forward = 1.5f;
    [Tooltip("ブロックの大きさ（マス）")]
    [SerializeField] Vector2 _size = new Vector2(2f, 2f);
    [Tooltip("残る秒数")]
    [SerializeField, Min(0.1f)] float _life = 6f;
    [Tooltip("出る瞬間の判定の秒数")]
    [SerializeField, Min(0.02f)] float _hitTime = 0.15f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 o = owner.OriginPoint;
        float x = o.x + owner.Facing * _forward;
        Rect view = ScreenUi.ViewRect();
        Rect arena = BattleArena.GetBounds();
        Rect area = arena.Contains(o) ? arena : view;
        x = Mathf.Clamp(x, area.xMin + _size.x * 0.5f, area.xMax - _size.x * 0.5f);
        owner.TryFindGround(x, o.y + 1f, out float groundY);
        var center = new Vector2(x, groundY + _size.y * 0.5f);

        GameObject go = owner.CreateSpawnedObject("NotionBlock", center);
        var wall = go.AddComponent<NotionBlockWall>();
        wall.Setup(owner, data, _size, _life, _hitTime);
        owner.NotifyReleased(data);
        yield break;
    }
}

/// <summary>能力「ブロック」が置いた壁（NotionBlockSkill が作る）。時間が来たら消える</summary>
public class NotionBlockWall : MonoBehaviour
{
    SpriteRenderer _visual;
    Hitbox _hitbox;
    float _hitEnd;
    float _dieAt;

    internal void Setup(AttackRunner owner, AttackData data, Vector2 size, float life, float hitTime)
    {
        // 出る瞬間の判定（重なった敵に1回）
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var hit = gameObject.AddComponent<BoxCollider2D>();
        hit.isTrigger = true;
        hit.size = size;
        _hitbox = owner.AddHitbox(gameObject, data, hit);
        _hitbox.Activate();
        _hitEnd = Time.time + hitTime;
        _dieAt = Time.time + life;

        // 乗れる・通れない壁（地形）
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0)
        {
            var solid = new GameObject("Solid");
            solid.layer = ground;
            solid.transform.SetParent(transform, false);
            solid.AddComponent<BoxCollider2D>().size = size;
        }
        // 敵の弾を止める（地形より少し大きくして、先に当たるように。追撃は出ない）
        int blocker = LayerMask.NameToLayer("BulletBlocker");
        if (blocker >= 0)
        {
            var b = new GameObject("BulletBlock");
            b.layer = blocker;
            b.transform.SetParent(transform, false);
            b.AddComponent<BoxCollider2D>().size = size + new Vector2(0.15f, 0.15f);
        }
        _visual = AttackRunner.AddVisual(transform, NotionArt.SkillBlock(), Color.white, size, 18);
        FxKit.Ring(transform.position, Mathf.Max(size.x, size.y) * 1.2f, 0.25f, NotionArt.Blue);
    }

    void Update()
    {
        float now = Time.time;
        if (_hitbox != null && _hitbox.IsActive && now >= _hitEnd) _hitbox.Deactivate();
        if (now >= _dieAt) { Destroy(gameObject); return; }
        if (_visual != null) _visual.enabled = _dieAt - now > 1f || Mathf.FloorToInt(now / 0.1f) % 2 == 0;
    }
}
