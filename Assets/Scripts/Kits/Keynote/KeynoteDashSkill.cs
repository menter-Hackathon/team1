using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力「マジックムーブ」（Keynote。2026-11-19。docs/app-kits-1-design.md 13章）。数値はすべて【仮】。
/// 正面へ 5マス、なめらかに動く（0.2秒。その間は無敵）。通り道の敵に1回ずつ当たる（ゲージ +10・雑魚は倒れる）。壁の手前で止まる。
/// クールタイム 3.5秒。攻めと逃げの両方に使える。新しいタブ（瞬間移動）とのちがいは、通り道に当たること。
/// ・動かし方：プレイヤーの体（KinematicBody2D）を少しずつ動かす（壁・床に当たれば止まる）。動いている間は速さを横だけにする
/// ・当たり：判定（1.2×1.2）を、動いている間だけプレイヤーの位置に重ねて出す（プレイヤーの子にはしない）
/// </summary>
[CreateAssetMenu(fileName = "Keynote_DashSkill", menuName = "Team1/Kits/Keynote Dash Skill")]
public class KeynoteDashSkill : CustomAttack
{
    [SerializeField, Min(0.5f)] float _distance = 5f;
    [SerializeField, Min(0.05f)] float _time = 0.2f;
    [Tooltip("当たりの大きさ（マス）")]
    [SerializeField] Vector2 _hitSize = new Vector2(1.2f, 1.2f);
    [Tooltip("動いたあとに残す、横の速さ（マス/秒）")]
    [SerializeField, Min(0f)] float _exitSpeed = 6f;

    public override bool CanRun(AttackRunner owner, AttackData data) =>
        owner != null && owner.GetComponent<KinematicBody2D>() != null && owner.GetComponent<KeynoteDashMotion>() == null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var m = owner.gameObject.AddComponent<KeynoteDashMotion>();
        m.Setup(owner, data, owner.Facing >= 0 ? 1 : -1, _distance, _time, _hitSize, _exitSpeed);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_time);
    }
}

/// <summary>マジックムーブの動き（KeynoteDashSkill がプレイヤーに付ける）。終わったら自分で外れる</summary>
public class KeynoteDashMotion : MonoBehaviour
{
    AttackRunner _owner;
    KinematicBody2D _body;
    PlayerController _pc;
    Health _health;
    GameObject _hitGo;
    Rigidbody2D _hitRb;
    Vector2 _hitOffset;
    int _dir;
    float _speed, _endAt, _exitSpeed;
    float _nextGhost;
    SpriteRenderer _main;

    internal void Setup(AttackRunner owner, AttackData data, int dir, float distance, float time, Vector2 hitSize, float exitSpeed)
    {
        _owner = owner;
        _body = GetComponent<KinematicBody2D>();
        _pc = GetComponent<PlayerController>();
        _health = GetComponent<Health>();
        _dir = dir;
        _speed = distance / Mathf.Max(0.01f, time);
        _endAt = Time.time + time;
        _exitSpeed = exitSpeed;
        if (_health != null) _health.SetInvincible(time + 0.05f);
        _main = OfficeKitTools.PlayerSprite(transform);

        // 通り道の当たり（動いている間だけ）。プレイヤーの子にはしない（近接の判定と見分けられるように）。毎回プレイヤーの位置へ動かす
        _hitOffset = new Vector2(0f, hitSize.y * 0.5f);
        _hitGo = owner.CreateSpawnedObject("Keynote_DashHit", (Vector2)transform.position + _hitOffset);
        var rb = _hitGo.AddComponent<Rigidbody2D>();
        _hitRb = rb;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = _hitGo.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = hitSize;
        Hitbox hb = owner.AddHitbox(_hitGo, data, col);
        hb.Activate();

        FxKit.Ring((Vector2)transform.position + Vector2.up * 0.5f, 1.6f, 0.15f, KeynoteArt.Blue);
    }

    void FixedUpdate()
    {
        if (_body == null) { Destroy(this); return; }
        if (Time.time >= _endAt)
        {
            if (_pc != null) _pc.SetVelocity(new Vector2(_dir * _exitSpeed, 0f));
            Destroy(this);
            return;
        }
        // なめらかに：はじめ速く、終わりに少しゆるむ
        float left = (_endAt - Time.time);
        float k = Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(left / 0.2f));
        _body.Move(new Vector2(_dir * _speed * k * Time.fixedDeltaTime, 0f));
        if (_hitRb != null) _hitRb.position = (Vector2)transform.position + _hitOffset + new Vector2(_dir * _speed * k * Time.fixedDeltaTime, 0f);
        if (_pc != null) _pc.SetVelocity(Vector2.zero); // 重力をためない
        if ((_dir > 0 && _body.HitWallRight) || (_dir < 0 && _body.HitWallLeft)) _endAt = Mathf.Min(_endAt, Time.time + 0.02f);
    }

    void Update()
    {
        if (_main == null || Time.time < _nextGhost) return;
        _nextGhost = Time.time + 0.045f;
        // 残像（青から紫へ）
        float t = Mathf.Clamp01(1f - (_endAt - Time.time) / 0.2f);
        Vector3 ls = _main.transform.lossyScale;
        FxAnim fx = FxAnim.Sprite(_main.sprite, _main.transform.position, _main.sortingOrder - 1, 0.22f,
            OfficeKitTools.Fade(Color.Lerp(KeynoteArt.Blue, KeynoteArt.Purple, t), 0.55f), Mathf.Abs(ls.y), "Keynote_DashGhost");
        // 向き：見た目の親の大きさ（左右の反転）と flipX の両方を写す
        if (fx.Main != null) fx.Main.flipX = (ls.x < 0f) != _main.flipX;
        fx.FadeFrom(0f);
    }

    void OnDestroy()
    {
        if (_hitGo != null) Destroy(_hitGo);
    }
}
