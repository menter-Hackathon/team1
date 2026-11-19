using UnityEngine;

/// <summary>
/// 【仮】ウソの言葉の弾（2026-11-19。ChatGPT のハルシネーション。docs/app-kits-1-design.md 5章）。担当3 の物。
/// 見た目だけの弾。判定は無い（重なっても当たらない）。灰色で、0.15秒ごとに文字化けする。地形に当たると消える。
/// CustomEffect なので、形態変化（50％）・正常化で消える
/// </summary>
public class ChatGptFakeBullet : CustomEffect
{
    const int Order = 20;

    SpriteRenderer _sr;
    Vector2 _dir;
    float _speed;
    float _dieAt;
    float _nextFrame;
    int _frame;
    LayerMask _solid;

    public static ChatGptFakeBullet Spawn(AttackRunner owner, Vector2 from, Vector2 dir, float speed, float lifetime, Vector2 size)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("ChatGPTFakeBullet", from);
        var b = go.AddComponent<ChatGptFakeBullet>();
        b._dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        b._speed = Mathf.Max(0.1f, speed);
        b._dieAt = Time.time + Mathf.Max(0.1f, lifetime);
        b._solid = owner.SolidMask;
        Sprite s = ChatGptArt.FakeToken(0);
        b._sr = AttackRunner.AddVisual(go.transform, s, new Color(1f, 1f, 1f, 0.85f), new Vector2(Mathf.Max(0.3f, size.x), Mathf.Max(0.2f, size.y)), Order, "Visual");
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b._dir.y, b._dir.x) * Mathf.Rad2Deg);
        return b;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _dieAt)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
        if (_solid.value != 0 && Physics2D.OverlapPoint(transform.position, _solid) != null)
        {
            ChatGptArt.Glitch(new Rect((Vector2)transform.position - Vector2.one * 0.3f, Vector2.one * 0.6f), 3, 32);
            Destroy(gameObject);
            return;
        }
        if (now >= _nextFrame && _sr != null)
        {
            _nextFrame = now + 0.15f;
            _frame++;
            _sr.sprite = ChatGptArt.FakeToken(_frame);
            _sr.color = new Color(1f, 1f, 1f, _frame % 2 == 0 ? 0.85f : 0.55f);
        }
    }
}
