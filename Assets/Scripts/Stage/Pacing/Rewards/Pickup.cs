using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>拾える物の種類（4-3）</summary>
public enum PickupKind { Heal, Shield }

/// <summary>
/// 【仮】拾える物（2026-11-19。docs/stage-pacing-design.md 4-3。担当3）。強い雑魚が倒れたときに1つ落とす（ZakoElite）。
/// ・アプリのアイコンの形の小さい物（16ドット。白いふち）。回復＝緑の電池、シールド＝青い盾。赤は使わない（赤＝危ない）
/// ・床の上に落ちる（穴の上なら、プレイヤーの側の手前の床）。ふわふわ浮く。7秒から点滅して、10秒で消える
/// ・触れると拾う（プレイヤーの体の当たり判定と重なったら。Hitbox と同じく、物理のトリガー通知は使わない）
///   回復：ライフ +1（Health.Heal）。満タンなら、シールドとして効く／シールド：5秒（ShieldAura）
/// ・ボスの登場が始まったら（KillChain.RewardsOpen が false）、落ちている物は消える
/// 数値はすべて【仮】
/// </summary>
public class Pickup : MonoBehaviour
{
    const float Life = 10f;
    const float BlinkFrom = 7f;
    const float Radius = 0.45f;
    const float Gravity = 30f;
    const float RestHeight = 0.55f;
    public const float ShieldSeconds = 5f;

    static readonly List<Pickup> All = new List<Pickup>();

    PickupKind _kind;
    SpriteRenderer _sr;
    float _age;
    float _restY;
    float _vy;
    bool _landed;
    float _phase;
    float _nextTwinkle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    /// <summary>from から床へ落とす。towardX：床が無い（穴）とき、この x の方へ床を探す（プレイヤーの側）</summary>
    public static Pickup Drop(Vector2 from, PickupKind kind, float towardX)
    {
        Vector2 floor;
        if (!FindFloor(from, towardX, out floor)) floor = new Vector2(from.x, from.y - 0.6f);
        var go = new GameObject("Pickup_" + kind);
        go.transform.position = new Vector3(floor.x, Mathf.Max(from.y, floor.y + RestHeight), 0f);
        var p = go.AddComponent<Pickup>();
        p._kind = kind;
        p._restY = floor.y + RestHeight;
        p._vy = 4f; // 少し上へはねてから落ちる
        p._phase = UnityEngine.Random.value * 2f;
        p._sr = RuntimeSprites.Show(go.transform, RewardKit.PickupSprite(kind), Vector2.zero, RewardKit.OrderPickup, 1f, "Art");
        All.Add(p);
        return p;
    }

    /// <summary>落ちている物を全部消す（ボスの登場）</summary>
    public static void ClearAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i] != null) All[i].Vanish();
        }
        All.Clear();
    }

    static bool FindFloor(Vector2 from, float towardX, out Vector2 floor)
    {
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        Rect view = ScreenUi.ViewRect();
        for (int i = 0; i <= 16; i++)
        {
            float x = Mathf.MoveTowards(from.x, towardX, i * 0.5f);
            x = Mathf.Clamp(x, view.xMin + 0.6f, view.xMax - 0.6f);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, from.y + 0.3f), Vector2.down, 30f, mask);
            if (hit.collider != null && hit.distance > 0.01f && hit.point.y > view.yMin - 1f)
            {
                floor = hit.point;
                return true;
            }
            if (Mathf.Approximately(x, towardX)) break;
        }
        floor = from;
        return false;
    }

    void OnDestroy() => All.Remove(this);

    void Update()
    {
        try
        {
            if (!KillChain.RewardsOpen)
            {
                Vanish();
                return;
            }
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= Life)
            {
                Vanish();
                return;
            }

            Vector3 pos = transform.position;
            if (!_landed)
            {
                _vy -= Gravity * dt;
                pos.y += _vy * dt;
                if (_vy < 0f && pos.y <= _restY)
                {
                    pos.y = _restY;
                    _landed = true;
                    FxKit.Fragments(pos + Vector3.down * 0.4f, Vector2.up, 2, new[] { FxArt.Square(2) }, new[] { Color.white });
                }
                transform.position = pos;
            }
            else if (_sr != null)
            {
                // ふわふわ浮く（絵だけ。拾える位置は動かさない）
                float bob = Mathf.Sin((_age + _phase) * Mathf.PI * 2f / 1.2f) * 0.08f;
                _sr.transform.localPosition = new Vector3(0f, Mathf.Round(bob * 18f) / 18f, 0f);
            }

            if (_sr != null) _sr.enabled = _age < BlinkFrom || Mathf.FloorToInt(_age / 0.1f) % 2 == 0;
            if (_landed && _age >= _nextTwinkle && _age < BlinkFrom)
            {
                _nextTwinkle = _age + 0.9f;
                Vector2 off = new Vector2(UnityEngine.Random.Range(-0.45f, 0.45f), UnityEngine.Random.Range(0.1f, 0.5f));
                FxAnim.Sprite(FxArt.Twinkle, (Vector2)transform.position + off, RewardKit.OrderPickup + 1, 0.3f, Color.white, 1f, "PickupTwinkle").Pop().FadeFrom(0.3f);
            }

            TryCollect();
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("Pickup.Update", $"拾える物で例外：{e}");
        }
    }

    void TryCollect()
    {
        Transform player = RewardKit.PlayerOf(StagePacer.Current);
        if (player == null) return;
        Health h = player.GetComponent<Health>();
        if (h == null || h.IsDead) return;
        Collider2D body = RewardKit.BodyOf(player);
        Vector2 c = _sr != null ? (Vector2)_sr.transform.position : (Vector2)transform.position;
        bool touch;
        if (body != null)
        {
            Bounds b = body.bounds;
            touch = b.SqrDistance(new Vector3(c.x, c.y, b.center.z)) <= Radius * Radius;
        }
        else touch = Vector2.Distance(c, RewardKit.BodyCenter(player)) <= Radius + 0.5f;
        if (touch) Collect(player, h, c);
    }

    void Collect(Transform player, Health h, Vector2 at)
    {
        PickupKind kind = _kind;
        if (kind == PickupKind.Heal && h.Current >= h.Max) kind = PickupKind.Shield; // 満タンなら、シールドとして効く

        // 拾った物が、大きくなって消える
        FxAnim.Sprite(RewardKit.PickupSprite(_kind), at, RewardKit.OrderPickup + 1, 0.25f, Color.white, 1f, "PickupGot").Grow(1.8f).FadeFrom(0f);
        Vfx.Sparkle(at);

        if (kind == PickupKind.Heal)
        {
            h.Heal(1);
            Vector2 head = FxKit.HeadOf(player.gameObject, (Vector2)player.position + Vector2.up) + Vector2.up * 0.4f;
            FxAnim.Sprite(RewardKit.PlusSprite, head, RewardKit.OrderBadge - 1, 0.7f, Color.white, 1.5f, "HealPlus").Pop().Rise(0.6f).FadeFrom(0.6f);
            Color green = RuntimeSprites.Hex("30D158");
            FxKit.Fragments(RewardKit.BodyCenter(player), Vector2.up, 6, new[] { FxArt.Square(2), FxArt.Square(3) }, new[] { green, Color.white });
        }
        else
        {
            ShieldAura.Apply(player, ShieldSeconds);
        }
        All.Remove(this);
        Destroy(gameObject);
    }

    void Vanish()
    {
        if (this == null) return;
        if (_sr != null && _sr.enabled)
            FxAnim.Sprite(RewardKit.PickupSprite(_kind), _sr.transform.position, RewardKit.OrderPickup, 0.18f, new Color(1f, 1f, 1f, 0.8f), 1f, "PickupGone").Grow(0.2f).FadeFrom(0f);
        All.Remove(this);
        Destroy(gameObject);
    }
}
