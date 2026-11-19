using UnityEngine;

/// <summary>
/// 【仮】能力「スタンプ」の場に出る物（2026-11-19）。スタンプの絵が上から落ちてきて「ドン」→ 判定 → 少し残って消える。
/// プレイヤーの出した物（CustomEffect）
/// </summary>
public class LineStampPress : CustomEffect
{
    const int Order = 24;
    const float FallHeight = 2.5f;

    Hitbox _hitbox;
    Transform _art;
    Vector2 _center;
    float _start, _pressAt, _endAt, _goneAt;
    bool _pressed, _hitDone;

    internal void Setup(AttackRunner owner, AttackData data, Vector2 size, float drop, float active, int index)
    {
        _center = transform.position;
        _start = Time.time;
        _pressAt = _start + drop;
        _endAt = _pressAt + active;
        _goneAt = _endAt + 0.25f;
        var box = TalkKitUtil.AddTrigger<BoxCollider2D>(gameObject);
        box.size = size;
        _hitbox = owner.AddHitbox(gameObject, data, box);
        _art = TalkArt.Child(transform, "Art", _center + Vector2.up * FallHeight);
        TalkArt.Show(_art, LineArt.Stamp(index), Vector2.zero, size, Color.white, Order, "Stamp");
    }

    void Update()
    {
        float now = Time.time;
        if (!_pressed)
        {
            float t = Mathf.Clamp01((now - _start) / Mathf.Max(0.01f, _pressAt - _start));
            _art.position = _center + Vector2.up * FallHeight * (1f - t * t);
            if (now < _pressAt) return;
            _pressed = true;
            _art.position = _center;
            _hitbox.Activate();
            StageCamera.Shake(0.08f, 0.1f);
            LineStampLanding.Hearts(_center + Vector2.down * 0.6f, 4);
            FxAnim.Sprite(TalkArt.Ring(40, 2), _center + Vector2.down * 0.8f, Order - 1, 0.3f, new Color(1f, 1f, 1f, 0.7f), 1.2f, "Dust").Grow(1.8f).FadeFrom(0f);
        }
        if (!_hitDone && now >= _endAt)
        {
            _hitDone = true;
            _hitbox.Deactivate();
        }
        if (now >= _goneAt) Destroy(gameObject);
        else if (_hitDone)
        {
            // つぶれて消える
            float k = Mathf.Clamp01((_goneAt - now) / 0.25f);
            _art.localScale = new Vector3(1f + (1f - k) * 0.2f, k, 1f);
        }
        else
        {
            float k = Mathf.Clamp01((now - _pressAt) / 0.08f);
            _art.localScale = new Vector3(1f + 0.15f * (1f - k), 1f - 0.15f * (1f - k), 1f);
        }
    }
}
