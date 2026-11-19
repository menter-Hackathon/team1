using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メールの能力「送信」（2026-11-19。docs/app-kits-1-design.md 15章）。Custom 小。
/// 紙飛行機を正面に投げる（毎秒 14マス）。敵を貫通して 8マス先まで飛び、戻ってくる（返信）。
/// ・行きで +5、帰りで +5（同じ相手に 2回当たる。合わせて +10）。雑魚は、通り道の全部が倒れる
/// ・戻ってくる紙飛行機を自分で受け取ると、クールタイムが 0.5秒短くなる（飾りのごほうび。全部の枠を進める）
/// ダメージ・ゲージ（1回ぶん）・クールタイム 2.5秒は攻撃データの共通の欄。大きさは「弾」の欄の大きさ
/// </summary>
[CreateAssetMenu(fileName = "Mail_SendSkill", menuName = "Team1/Kits/Mail Send (player)")]
public class MailSendSkill : CustomAttack
{
    [Tooltip("行きの距離（マス）")]
    [SerializeField, Min(1f)] float _distance = 8f;
    [Tooltip("速さ（毎秒マス）")]
    [SerializeField, Min(1f)] float _speed = 14f;
    [Tooltip("受け取ったときに進めるクールタイム（秒）")]
    [SerializeField, Min(0f)] float _catchBonus = 0.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        CallKitUtil.TryBodyBounds(owner.transform, out Bounds body);
        int dir = owner.Facing >= 0 ? 1 : -1;
        Vector2 from = new Vector2(body.center.x + dir * 0.5f, body.center.y + 0.1f);
        MailBoomerang.Spawn(owner, data, from, dir, _distance, _speed, _catchBonus);
        owner.NotifyReleased(data);
        yield return null;
    }

    /// <summary>HUD の枠の絵（青い地に白い紙飛行機）</summary>
    public override Sprite DisplayIcon(AttackRunner owner) => MailArt.SkillIcon;
}

/// <summary>行って戻る紙飛行機（プレイヤーの攻撃判定）。帰りに判定を出し直すので、同じ相手に 2回当たる</summary>
public class MailBoomerang : MonoBehaviour
{
    const int Order = 26;
    AttackRunner _owner;
    Hitbox _hitbox;
    Rigidbody2D _rb;
    Transform _art;
    SpriteRenderer _planeSr;
    int _dir;
    float _distance, _speed, _bonus, _born, _traveled;
    bool _returning;
    Vector2 _velocity;

    public static MailBoomerang Spawn(AttackRunner owner, AttackData data, Vector2 from, int dir, float distance, float speed, float bonus)
    {
        GameObject go = owner.CreateSpawnedObject("Mail_Boomerang", from);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(Mathf.Max(0.4f, data.HitSize.x), Mathf.Max(0.3f, data.HitSize.y));
        var b = go.AddComponent<MailBoomerang>();
        b._owner = owner;
        b._rb = rb;
        b._dir = dir;
        b._distance = distance;
        b._speed = speed;
        b._bonus = bonus;
        b._born = Time.time;
        b._velocity = new Vector2(dir * speed, 0f);
        b._hitbox = owner.AddHitbox(go, data, col);
        b._hitbox.SetErrorKind("MailSend");
        b._hitbox.Activate();
        var art = new GameObject("Art");
        art.transform.SetParent(go.transform, false);
        b._planeSr = MailArt.Put(art.transform, MailArt.Plane, Vector2.zero, new Vector2(1f, 0.67f), Color.white, Order, "Plane");
        b._art = art.transform;
        b.Face();
        return b;
    }

    void Face()
    {
        if (_art == null || _velocity.sqrMagnitude < 0.01f) return;
        _art.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg);
        MailArt.Upright(_planeSr, _velocity);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 pos = _rb.position;
        if (!_returning)
        {
            float step = _speed * dt;
            _traveled += step;
            Rect view = ScreenUi.ViewRect();
            bool edge = pos.x < view.xMin + 0.3f || pos.x > view.xMax - 0.3f;
            if (_traveled >= _distance || edge) Turn();
        }
        else
        {
            // 帰り：持ち主へ向かう（少しずつ曲がる）
            Vector2 home = HomePoint();
            Vector2 want = (home - pos).normalized * _speed * 1.1f;
            _velocity = Vector2.MoveTowards(_velocity, want, 60f * dt);
            if ((home - pos).sqrMagnitude < 0.5f * 0.5f)
            {
                Catch();
                return;
            }
        }
        if (Time.time - _born > 3f)
        {
            Destroy(gameObject);
            return;
        }
        _rb.MovePosition(pos + _velocity * dt);
        Face();
    }

    void Turn()
    {
        _returning = true;
        _velocity = new Vector2(-_dir * _speed * 0.4f, 0f);
        if (_hitbox != null) _hitbox.Activate(); // 帰りは、もう一度当たる
        FxAnim.Sprite(FxArt.Twinkle, _rb.position, Order + 1, 0.2f, Color.white, 1f, "Mail_Turn").Pop();
    }

    Vector2 HomePoint()
    {
        if (_owner == null) return _rb.position;
        CallKitUtil.TryBodyBounds(_owner.transform, out Bounds b);
        return b.center;
    }

    void Catch()
    {
        if (_bonus > 0f) KitPlayer.AdvanceCooldowns(_bonus);
        FxAnim.Sprite(FxArt.Star4, HomePoint() + Vector2.up * 0.8f, Order + 1, 0.35f, MailArt.Blue, 1f, "Mail_Catch").Pop().Rise(0.4f);
        Destroy(gameObject);
    }
}
