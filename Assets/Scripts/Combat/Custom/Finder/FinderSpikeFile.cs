using UnityEngine;

/// <summary>
/// トゲ付きファイル（スマートフォルダマグネット。2026-11-18。docs/app-boss-skills-design.md 2-2）。
/// ・現れてからしばらくは、空中に止まっている（当たらない）。フォルダが開くと、ふるえる／ゆれる
/// ・Launch で、決まった位置へまっすぐ飛ぶ（追いかけない）。飛んでいる間だけ当たる（ライフ -1）。
///   相手に当たる・壁や床に当たる・部屋の外・時間切れで消える。足場（フォルダ）はすり抜ける
/// ・zip圧縮では吸えない（弾の部品も印も付けない）
/// FinderSmartFolderEffect の子として作る（技が消されると一緒に消える）
/// </summary>
public class FinderSpikeFile : MonoBehaviour
{
    const float AppearTime = 0.15f; // 現れるときに、小さい所から大きくなる

    AttackRunner _owner;
    Hitbox _hitbox;
    Transform _visual;
    Vector2 _size;
    Vector2 _velocity;
    float _flyEnd;
    float _spawnedAt;
    readonly Collider2D[] _overlap = new Collider2D[2];
    FinderLine _tail; // 飛んでいる間の、タグの色の点線の尾（2026-11-18。11-2）

    /// <summary>色（0＝赤、1＝青）</summary>
    public int Tag { get; private set; }
    /// <summary>飛んでいる間 true</summary>
    public bool IsFlying { get; private set; }

    public static FinderSpikeFile Spawn(Transform parent, AttackRunner owner, AttackData data, int tag, Vector2 position, Vector2 size)
    {
        Transform t = FinderArt.Child(parent, $"SpikeFile_{tag}", position);
        GameObject go = t.gameObject;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;

        var s = go.AddComponent<FinderSpikeFile>();
        s._owner = owner;
        s.Tag = tag;
        s._size = size;
        s._spawnedAt = Time.time;
        s._visual = FinderArt.Child(t, "Visual", position);
        // 絵は1マス（白い書類 14ドット＋まわりのトゲ）。判定は書類の大きさ
        AttackRunner.AddVisual(s._visual, FinderArt.SpikeFile(tag), Color.white, Vector2.one, 22, "Sprite");
        s._visual.localScale = Vector3.zero;
        s._hitbox = owner.AddHitbox(go, data, box); // 飛ぶまでは出さない
        s._hitbox.SetErrorKind(tag == 1 ? "SmartFolder_Blue" : "SmartFolder_Red"); // 当たった絵：その色のタグが貼られて落ちる（FinderSkin）
        s._hitbox.Hit += s.OnHit;
        return s;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnHit(IDamageable target, DamageInfo info) => Destroy(gameObject);

    /// <summary>その場でふるえる（タグが付いていて、狙っている間）。amount はマス</summary>
    public void Shake(float amount)
    {
        if (IsFlying) return;
        _visual.localPosition = new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), 0f);
        _visual.localRotation = Quaternion.identity;
    }

    /// <summary>「？」のように少しゆれる（タグが付いていないとき）。t は経過秒</summary>
    public void Wobble(float t)
    {
        if (IsFlying) return;
        _visual.localPosition = Vector3.zero;
        _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 12f);
    }

    /// <summary>ふるえ・ゆれを止める</summary>
    public void Still()
    {
        if (_visual == null) return;
        _visual.localPosition = Vector3.zero;
        _visual.localRotation = Quaternion.identity;
    }

    /// <summary>target へまっすぐ飛ぶ（target を通りすぎても、壁・床まで進む）</summary>
    public void Launch(Vector2 target, float speed, float maxTime)
    {
        if (IsFlying) return;
        Still();
        Vector2 d = target - (Vector2)transform.position;
        _velocity = (d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down) * speed;
        _flyEnd = Time.time + maxTime;
        IsFlying = true;
        _hitbox.Activate();
        _tail = FinderLine.Create(transform, FinderArt.TagColor(Tag), true, 1, 21, "Tail");
        _tail.Set(transform.position, transform.position);
    }

    void Update()
    {
        if (!IsFlying)
        {
            float k = Mathf.Clamp01((Time.time - _spawnedAt) / AppearTime);
            if (_visual != null) _visual.localScale = new Vector3(k, k, 1f);
            return;
        }
        if (_owner == null || Time.time >= _flyEnd)
        {
            Destroy(gameObject);
            return;
        }
        Vector2 next = (Vector2)transform.position + _velocity * Time.deltaTime;
        transform.position = next;
        if (_tail != null) _tail.Set(next - _velocity.normalized * 1.4f, next - _velocity.normalized * 0.45f);

        // 壁・床に当たったら消える
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.SolidMask);
        if (Physics2D.OverlapBox(next, _size * 0.7f, 0f, filter, _overlap) > 0)
        {
            Destroy(gameObject);
            return;
        }
        Rect arena = BattleArena.GetBounds();
        if (next.x < arena.xMin - 1f || next.x > arena.xMax + 1f || next.y < arena.yMin - 1f || next.y > arena.yMax + 1f) Destroy(gameObject);
    }
}
