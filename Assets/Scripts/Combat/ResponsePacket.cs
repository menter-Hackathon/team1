using UnityEngine;

/// <summary>
/// 応答のパケット（2026-11-19。ブラウザの能力）。ロックオンした敵が倒れたとき、PacketVolley が作る。
/// ・敵のいた所から、持ち主（プレイヤー）へ曲がりながら飛んでくる（地形は通り抜ける。必ず届く）
/// ・届いたら、持ち主のライフを回復する（攻撃データの「回復する量」）。きらめきが出る
/// </summary>
public class ResponsePacket : MonoBehaviour
{
    const float Speed = 9f;
    const float TurnRate = 720f;
    const float MaxLife = 4f;

    AttackRunner _owner;
    int _heal;
    SpriteRenderer _sr;
    Vector2 _velocity;
    float _bornAt;
    float _nextGhost;

    public static ResponsePacket Spawn(AttackRunner owner, AttackData data, Vector2 from)
    {
        var go = new GameObject("ResponsePacket");
        go.transform.position = from;
        var r = go.AddComponent<ResponsePacket>();
        r._owner = owner;
        r._heal = data.PacketHeal;
        r._bornAt = Time.time;
        Sprite s = data.PacketResponseSprite != null ? data.PacketResponseSprite : data.Sprite;
        r._sr = AttackRunner.AddVisual(go.transform, s, data.PacketResponseSprite != null ? Color.white : new Color(0.3f, 1f, 0.45f), data.HitSize, 22, "Art");
        r._velocity = Vector2.up * Speed; // 最初は上へはね上がる
        Vfx.Sparkle(from);
        return r;
    }

    void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }
        Vector2 target = OwnerCenter();
        Vector2 want = target - (Vector2)transform.position;
        // 届いたら（または時間がかかりすぎたら）回復する
        if (want.magnitude < 0.4f || Time.time - _bornAt > MaxLife)
        {
            Arrive();
            return;
        }
        float maxTurn = TurnRate * Time.deltaTime;
        float angle = Vector2.SignedAngle(_velocity, want);
        _velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -maxTurn, maxTurn)) * _velocity;
        float speed = Speed + (Time.time - _bornAt) * 6f; // だんだん速く（必ず届くように）
        _velocity = _velocity.normalized * speed;
        transform.position += (Vector3)(_velocity * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg);

        if (Time.time >= _nextGhost && _sr != null)
        {
            var go = new GameObject("ResponseGhost");
            go.transform.SetPositionAndRotation(_sr.transform.position, _sr.transform.rotation);
            go.transform.localScale = _sr.transform.lossyScale;
            var g = go.AddComponent<SpriteRenderer>();
            g.sprite = _sr.sprite;
            g.sortingOrder = _sr.sortingOrder - 1;
            g.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, 0.35f);
            go.AddComponent<FadeAndDestroy>().Setup(0.12f);
            _nextGhost = Time.time + 0.03f;
        }
    }

    Vector2 OwnerCenter()
    {
        foreach (var col in _owner.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds.center;
        }
        return _owner.transform.position;
    }

    void Arrive()
    {
        if (_owner.TryGetComponent(out Health health)) health.Heal(_heal);
        Vfx.Sparkle(OwnerCenter() + Vector2.up * 0.3f);
        Destroy(gameObject);
    }
}
