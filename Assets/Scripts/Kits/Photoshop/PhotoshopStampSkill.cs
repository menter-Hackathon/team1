using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】コピースタンプ（Photoshop の能力。2026-11-19。docs/app-kits-2-design.md 3-1）。
/// 今いる場所に、自分の分身（水色の半透明）を 4秒置く。分身は 0.6秒おきに、向いていた方へ近接くらいの範囲をふる（1回 ゲージ 3・最大 6回）。
/// ボスの前に置いて、自分は逃げる・別の所から攻める、という使い方。ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "Photoshop_Stamp", menuName = "Team1/Kits/Photoshop Stamp")]
public class PhotoshopStampSkill : CustomAttack
{
    [Tooltip("分身が残る秒数")]
    [SerializeField, Min(0.5f)] float _life = 4f;
    [Tooltip("ふる間隔（秒）")]
    [SerializeField, Min(0.1f)] float _swingInterval = 0.6f;
    [Tooltip("最初にふるまで（秒）")]
    [SerializeField, Min(0f)] float _firstSwing = 0.25f;
    [Tooltip("ふる範囲（マス）と、体の中心からの前のずれ")]
    [SerializeField] Vector2 _swingSize = new Vector2(1.8f, 1.4f);
    [SerializeField] float _swingForward = 0.95f;
    [Tooltip("判定を出している秒数")]
    [SerializeField, Min(0.02f)] float _swingActive = 0.12f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        int facing = owner.Facing >= 0 ? 1 : -1;
        Vector2 feet = owner.transform.position;
        GameObject go = owner.CreateSpawnedObject("Photoshop_StampClone", feet);
        var clone = go.AddComponent<PhotoshopStampClone>();
        clone.Setup(owner, data, facing, _life, _swingInterval, _firstSwing, _swingSize, _swingForward, _swingActive);
        FxKit.Ring(feet + new Vector2(0f, 0.5f), 1.6f, 0.25f, PhotoshopArt.Blue);
        yield return null;
    }
}

/// <summary>コピースタンプの分身（プレイヤーの能力が出す）。時間で消える</summary>
public class PhotoshopStampClone : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    int _facing;
    float _dieAt, _nextSwing, _interval, _active, _forward;
    Vector2 _swingSize;
    SpriteRenderer _body;
    int _frame;

    internal void Setup(AttackRunner owner, AttackData data, int facing, float life, float interval, float first, Vector2 swingSize, float forward, float active)
    {
        _owner = owner;
        _data = data;
        _facing = facing;
        _dieAt = Time.time + life;
        _interval = interval;
        _nextSwing = Time.time + first;
        _swingSize = swingSize;
        _forward = forward;
        _active = active;
        Sprite s = PhotoshopArt.PlayerSprite(0);
        _body = RuntimeSprites.Show(transform, s, Vector2.zero, 9, 1f, "Clone");
        // 足元が原点になるように、絵の下の端を合わせる
        float bottom = s.bounds.min.y;
        _body.transform.localPosition = new Vector3(0f, -bottom, 0f);
        _body.flipX = facing < 0;
        _body.color = new Color(PhotoshopArt.Blue.r, PhotoshopArt.Blue.g, PhotoshopArt.Blue.b, 0.55f);
        var reticle = RuntimeSprites.Show(transform, PhotoshopArt.Reticle, new Vector2(0f, s.bounds.size.y + 0.5f), 10, 1f, "Stamp");
        reticle.color = new Color(1f, 1f, 1f, 0.8f);
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _dieAt) { Destroy(gameObject); return; }
        float left = _dieAt - now;
        if (_body != null)
        {
            Color c = _body.color;
            c.a = 0.55f * (left < 0.4f ? left / 0.4f : 1f) * (0.85f + 0.15f * Mathf.Sin(now * 10f));
            _body.color = c;
        }
        if (now >= _nextSwing)
        {
            _nextSwing += _interval;
            Swing();
        }
    }

    void Swing()
    {
        if (_owner == null) return;
        Vector2 center = (Vector2)transform.position + new Vector2(_facing * _forward, 0.55f);
        var go = new GameObject("StampSwing");
        go.layer = _owner.gameObject.layer;
        go.transform.position = center;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = _swingSize;
        Hitbox h = _owner.AddHitbox(go, _data, box);
        h.Activate();
        Destroy(go, _active);
        // 見た目：水色の三日月
        var a = FxAnim.Sprite(PhotoshopArt.Slash(14, 26), center, 31, 0.16f, PhotoshopArt.BlueLight);
        if (_facing < 0 && a.TryGetComponent(out SpriteRenderer sr)) sr.flipX = true;
        _frame++;
        if (_body != null) _body.sprite = PhotoshopArt.PlayerSprite(_frame);
    }
}
