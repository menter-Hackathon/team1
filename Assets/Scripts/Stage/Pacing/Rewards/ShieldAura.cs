using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】シールド（2026-11-19。docs/stage-pacing-design.md 4-3。担当3）。拾える物（Pickup）の盾で始まる。
/// ・5秒、無敵（Health.SetInvincible）。止められない入口なので、0.2秒ずつ足し続ける（ボスの登場で、すぐ終わるように）
/// ・体に触れた雑魚が倒れる（ダメージ 1。強い雑魚は 1減るだけ。同じ相手には 0.5秒に1回）。ボス・分身には効かない
/// ・体のまわりに白い輪（少し青い）。最後の1秒は点滅する。無敵の間の体の点滅（DamageBlink）は止めて、体はいつも見せる
/// ・ボスの登場が始まったら（KillChain.RewardsOpen が false）、すぐ消える
/// 数値はすべて【仮】
/// </summary>
public class ShieldAura : MonoBehaviour
{
    const float Slice = 0.2f;
    const float ContactCooldown = 0.5f;
    const int RingPx = 17;

    static ShieldAura _current;

    Transform _player;
    Health _health;
    SpriteRenderer _ring;
    SpriteRenderer _fill;
    SpriteRenderer _playerArt;
    float _until;
    float _born;
    readonly Dictionary<Health, float> _lastHit = new Dictionary<Health, float>();
    readonly List<Collider2D> _overlap = new List<Collider2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _current = null;

    /// <summary>シールドを始める（もう出ていれば、長くする）</summary>
    public static void Apply(Transform player, float seconds)
    {
        if (player == null) return;
        Health h = player.GetComponent<Health>();
        if (h == null || h.IsDead) return;
        if (_current != null && _current._player == player)
        {
            _current._until = Mathf.Max(_current._until, Time.time + seconds);
            FxKit.Ring(RewardKit.BodyCenter(player), 2.5f, 0.2f, Color.white);
            return;
        }
        if (_current != null) _current.End(false);
        var go = new GameObject("ShieldAura");
        var s = go.AddComponent<ShieldAura>();
        s._player = player;
        s._health = h;
        s._until = Time.time + seconds;
        s._born = Time.time;
        s.Build();
        _current = s;
    }

    /// <summary>シールドを消す（ボスの登場）</summary>
    public static void EndAll()
    {
        if (_current != null) _current.End(true);
    }

    void Build()
    {
        transform.position = RewardKit.BodyCenter(_player);
        Color tint = RuntimeSprites.Hex("CFE8FF");
        _fill = RuntimeSprites.Show(transform, FxArt.Disc(RingPx * 2 - 2), Vector2.zero, RewardKit.OrderShield - 1, 1f, "Fill");
        _fill.color = new Color(0.39f, 0.71f, 1f, 0.14f);
        _ring = RuntimeSprites.Show(transform, RewardKit.RingSprite(RingPx, 2f), Vector2.zero, RewardKit.OrderShield, 1f, "Ring");
        _ring.color = tint;
        Transform art = _player.Find("Visual/Art");
        _playerArt = art != null ? art.GetComponent<SpriteRenderer>() : null;
        FxKit.Ring(transform.position, 2.5f, 0.2f, Color.white);
        Vfx.Sparkle(transform.position);
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    void Update()
    {
        try
        {
            if (!KillChain.RewardsOpen || _player == null || _health == null || _health.IsDead)
            {
                End(true);
                return;
            }
            float left = _until - Time.time;
            if (left <= 0f)
            {
                End(true);
                return;
            }
            _health.SetInvincible(Mathf.Min(Slice, left));
            ContactKill();

            // 輪：少しふくらんだり、しぼんだり。最後の1秒は点滅
            float age = Time.time - _born;
            float s = 1f + Mathf.Sin(age * Mathf.PI * 2f * 2f) * 0.05f;
            transform.localScale = new Vector3(s, s, 1f);
            bool on = left > 1f || Mathf.FloorToInt(left / 0.1f) % 2 == 0;
            if (_ring != null) _ring.enabled = on;
            if (_fill != null) _fill.enabled = on;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ShieldAura.Update", $"シールドで例外：{e}");
        }
    }

    void LateUpdate()
    {
        if (_player == null) return;
        Vector2 c = RewardKit.BodyCenter(_player);
        transform.position = new Vector3(Mathf.Round(c.x * 18f) / 18f, Mathf.Round(c.y * 18f) / 18f, 0f);
        // 無敵の間の点滅を止める（体はいつも見せる。輪があるので、守られているのが分かる）
        if (_playerArt != null && _health != null && !_health.IsDead) _playerArt.enabled = true;
    }

    /// <summary>体に触れた雑魚にダメージ</summary>
    void ContactKill()
    {
        Collider2D body = RewardKit.BodyOf(_player);
        Vector2 center = RewardKit.BodyCenter(_player);
        Vector2 size = body != null ? (Vector2)body.bounds.size + new Vector2(0.3f, 0.2f) : new Vector2(1f, 1.2f);
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Enemy"));
        _overlap.Clear();
        Physics2D.OverlapBox(center, size, 0f, filter, _overlap);
        if (_overlap.Count == 0) return;

        var seen = new HashSet<Health>();
        StagePacer pacer = StagePacer.Current;
        foreach (var col in _overlap)
        {
            if (col == null) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h == null || !seen.Add(h) || h.IsDead || !RewardKit.IsZako(h)) continue;
            if (_lastHit.TryGetValue(h, out float last) && Time.time - last < ContactCooldown) continue;
            _lastHit[h] = Time.time;
            RewardKit.Strike(h, 1, center, _player.gameObject, SkinId.Generic, HitStrength.Light, RewardKit.ThemeOf(pacer), RewardKit.IconOf(pacer));
        }
    }

    void End(bool burst)
    {
        if (this == null) return;
        if (burst && _ring != null)
        {
            // 輪がはじけて消える
            FxAnim.Sprite(_ring.sprite, transform.position, RewardKit.OrderShield, 0.2f, _ring.color, 1f, "ShieldPop").Grow(1.5f).FadeFrom(0f);
        }
        if (_current == this) _current = null;
        Destroy(gameObject);
    }
}
