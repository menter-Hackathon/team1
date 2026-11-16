using UnityEngine;

/// <summary>
/// 地形（床・壁・すり抜け床）とだけ衝突する自前の移動処理。プレイヤー・敵の共通部品。
/// 物理演算には任せず、BoxCastで「どこまで動けるか」を調べてから動かす。
/// 使い方：FixedUpdate の中で 1物理フレームにつき1回だけ Move(移動量) を呼ぶ。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class KinematicBody2D : MonoBehaviour
{
    [Tooltip("上下左右すべてでぶつかる地形（床・壁）")]
    [SerializeField] LayerMask _solidMask;
    [Tooltip("上から乗れて、下や横からはすり抜けられる床")]
    [SerializeField] LayerMask _oneWayMask;
    [Tooltip("地形との間に空ける薄い隙間。めり込み防止用")]
    [SerializeField, Min(0.001f)] float _skinWidth = 0.015f;

    Rigidbody2D _rb;
    BoxCollider2D _box;
    ContactFilter2D _filter;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

    /// <summary>直前の Move で床に立っていたか</summary>
    public bool IsGrounded { get; private set; }
    /// <summary>直前の Move で天井にぶつかったか</summary>
    public bool HitCeiling { get; private set; }
    public bool HitWallLeft { get; private set; }
    public bool HitWallRight { get; private set; }
    /// <summary>true の間はすり抜け床に乗らない（下＋ジャンプで降りる処理などに使う）</summary>
    public bool IgnoreOneWay { get; set; }

    void Reset()
    {
        _solidMask = LayerMask.GetMask("Ground");
        _oneWayMask = LayerMask.GetMask("OneWayPlatform");
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _box = GetComponent<BoxCollider2D>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true; // 後で当たり判定(トリガー)を使えるように
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // トリガー（攻撃判定など）には当たらないようにする
        _filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
    }

    /// <summary>
    /// 指定した量だけ動かす。地形に当たったらその手前で止まり、各フラグが更新される。
    /// </summary>
    public void Move(Vector2 delta)
    {
        Vector2 pos = _rb.position;
        IsGrounded = HitCeiling = HitWallLeft = HitWallRight = false;

        // 縦 → 横 の順に解決する。
        // 縦は移動量が0でも下向きに調べて、接地しているかを判定する。
        bool goingUp = delta.y > 0f;
        Vector2 vDir = goingUp ? Vector2.up : Vector2.down;
        bool allowOneWay = !goingUp && !IgnoreOneWay;
        float vMove = CastAxis(pos, vDir, Mathf.Abs(delta.y), allowOneWay, out bool vHit);
        pos += vDir * vMove;
        if (vHit)
        {
            if (goingUp) HitCeiling = true;
            else IsGrounded = true;
        }

        if (delta.x != 0f)
        {
            Vector2 hDir = delta.x > 0f ? Vector2.right : Vector2.left;
            float hMove = CastAxis(pos, hDir, Mathf.Abs(delta.x), false, out bool hHit);
            pos += hDir * hMove;
            if (hHit)
            {
                if (delta.x > 0f) HitWallRight = true;
                else HitWallLeft = true;
            }
        }

        _rb.MovePosition(pos);
    }

    /// <summary>当たり判定を無視して瞬間移動する（ステージ開始位置への配置など）</summary>
    public void Warp(Vector2 position)
    {
        _rb.position = position;
        transform.position = position;
    }

    /// <summary>pos から dir 方向に distance だけ動けるか調べ、実際に動ける距離を返す</summary>
    float CastAxis(Vector2 pos, Vector2 dir, float distance, bool allowOneWay, out bool hit)
    {
        hit = false;

        Vector2 scale = transform.lossyScale;
        scale = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Vector2 center = pos + Vector2.Scale(_box.offset, scale);
        Vector2 size = Vector2.Scale(_box.size, scale) - Vector2.one * (_skinWidth * 2f);
        size = Vector2.Max(size, Vector2.one * 0.01f);

        int mask = _solidMask.value | (allowOneWay ? _oneWayMask.value : 0);
        _filter.SetLayerMask(mask);

        int count = Physics2D.BoxCast(center, size, 0f, dir, _filter, _hits, distance + _skinWidth);

        float allowed = distance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D h = _hits[i];
            if (h.collider.attachedRigidbody == _rb) continue; // 自分自身

            bool isOneWay = (_oneWayMask.value & (1 << h.collider.gameObject.layer)) != 0;
            if (isOneWay)
            {
                // 上から着地するときだけ床として扱う（めり込んだ状態からは乗らない）
                if (h.distance <= 0f || h.normal.y < 0.5f) continue;
            }

            float d = Mathf.Max(0f, h.distance - _skinWidth);
            if (d <= allowed)
            {
                allowed = d;
                hit = true;
            }
        }
        return allowed;
    }
}
