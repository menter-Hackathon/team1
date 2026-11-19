using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】マジックムーブ（Keynote のボスの技 P1-1。Custom 中。2026-11-19。docs/app-kits-1-design.md 13章）。数値はすべて【仮】。
/// 大きい丸（直径 2.5）がボスの横に出る → 行き先（今のプレイヤーの位置）に赤い点線の輪、通り道に赤い点線（0.8秒）
/// → 丸が 0.35秒で、なめらかに行き先へ動く（通り道と行き先が当たる）→ 着いた所から、もう一度（合わせて3回。第2形態は5回）。
/// 避け方：通り道の線から、横か上へ 1.5マス出る。丸とプレイヤーの間に立たない。
/// 丸は青から紫へ色が変わる。動くときに残像 4つ。
/// </summary>
[CreateAssetMenu(fileName = "Keynote_MagicMove", menuName = "Team1/Kits/Keynote Magic Move")]
public class KeynoteMagicMoveAttack : CustomAttack
{
    [SerializeField, Min(1)] int _hops = 3;
    [SerializeField, Min(0.5f)] float _diameter = 2.5f;
    [Tooltip("点線の予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("動く時間（秒）")]
    [SerializeField, Min(0.05f)] float _moveTime = 0.35f;
    [Tooltip("着いてから判定を残す時間（秒）")]
    [SerializeField, Min(0f)] float _arriveHit = 0.1f;
    [Tooltip("ボスの真ん中から、丸が出る位置までの横の距離（マス）")]
    [SerializeField] float _side = 2.2f;

    public int Hops => _hops;
    public float Diameter => _diameter;
    public float Telegraph => _telegraph;
    public float MoveTime => _moveTime;
    public float ArriveHit => _arriveHit;
    public float Side => _side;

    public float TotalTime => 0.25f + _hops * (_telegraph + _moveTime + _arriveHit) + 0.3f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        KeynoteMagicMoveEffect e = KeynoteMagicMoveEffect.Spawn(owner, data, this);
        if (e == null) yield break;
        float timeout = Time.time + TotalTime + 1f;
        while (e != null && Time.time < timeout) yield return null;
    }
}

/// <summary>マジックムーブの丸（KeynoteMagicMoveAttack が出す）。自分で進む。形態変化・正常化で消える</summary>
public class KeynoteMagicMoveEffect : CustomEffect
{
    const int BallOrder = 21, MarkOrder = 17;

    AttackRunner _owner;
    AttackData _data;
    KeynoteMagicMoveAttack _a;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    SpriteRenderer _ball;
    Rect _area;
    float _floor;
    readonly List<GameObject> _marks = new List<GameObject>();

    public bool IsHitActive => _hitbox != null && _hitbox.IsActive;

    public static KeynoteMagicMoveEffect Spawn(AttackRunner owner, AttackData data, KeynoteMagicMoveAttack a)
    {
        if (owner == null || data == null || a == null) return null;
        Rect area = OfficeKitTools.Area(owner, out float floor);
        float r = a.Diameter * 0.5f;
        Vector2 start = owner.OriginPoint + new Vector2(owner.Facing * a.Side, 0f);
        start = ClampIn(start, area, floor, r);
        GameObject go = owner.CreateSpawnedObject($"Keynote_MagicMove_{data.name}", start);
        var e = go.AddComponent<KeynoteMagicMoveEffect>();
        e._owner = owner;
        e._data = data;
        e._a = a;
        e._area = area;
        e._floor = floor;
        e._rb = go.AddComponent<Rigidbody2D>();
        e._rb.bodyType = RigidbodyType2D.Kinematic;
        e._rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = r * 0.92f; // 見た目より少しだけ小さく（ふちのかすりで当てない）
        e._hitbox = owner.AddHitbox(go, data, col);
        e._ball = AttackRunner.AddVisual(go.transform, KeynoteArt.Ball(Mathf.RoundToInt(a.Diameter * 18f)), KeynoteArt.Blue, Vector2.one * a.Diameter, BallOrder, "Ball");
        e.StartCoroutine(e.Run());
        return e;
    }

    static Vector2 ClampIn(Vector2 p, Rect area, float floor, float r)
    {
        p.x = Mathf.Clamp(p.x, area.xMin + r, area.xMax - r);
        p.y = Mathf.Clamp(p.y, floor + r, Mathf.Max(floor + r, area.yMax - r));
        return p;
    }

    Color ColorAt(int hop) => Color.Lerp(KeynoteArt.Blue, KeynoteArt.Purple, _a.Hops > 1 ? (float)hop / (_a.Hops - 1) : 1f);

    IEnumerator Run()
    {
        float r = _a.Diameter * 0.5f;
        // 出てくる（ふくらむ）
        float popEnd = Time.time + 0.25f;
        while (Time.time < popEnd)
        {
            float k = 1f - (popEnd - Time.time) / 0.25f;
            _ball.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, k) * BallScale();
            yield return null;
        }
        _ball.transform.localScale = Vector3.one * BallScale();

        for (int hop = 0; hop < _a.Hops; hop++)
        {
            Vector2 from = _rb.position;
            Vector2 to = ClampIn(_owner.TargetPoint ?? from, _area, _floor, r);
            _ball.color = ColorAt(hop);

            // 予告：点線と輪
            ShowMarks(from, to);
            float end = Time.time + _a.Telegraph;
            while (Time.time < end)
            {
                if (OfficeKitTools.OwnerStopped(_owner, _data)) { Destroy(gameObject); yield break; }
                bool on = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
                foreach (var m in _marks) if (m != null) m.SetActive(on);
                yield return null;
            }
            ClearMarks();

            // 動く（なめらかに）。動いている間と、着いてから少しだけ判定
            _owner.NotifyReleased(_data);
            _hitbox.Activate();
            float start = Time.time;
            int ghosts = 0;
            while (true)
            {
                float t = Mathf.Clamp01((Time.time - start) / _a.MoveTime);
                float s = t * t * (3f - 2f * t);
                Vector2 p = Vector2.Lerp(from, to, s);
                _rb.position = p;
                transform.position = p;
                if (ghosts < 4 && t >= ghosts / 4f)
                {
                    Ghost(p, ColorAt(hop));
                    ghosts++;
                }
                if (t >= 1f) break;
                yield return new WaitForFixedUpdate();
            }
            float arriveEnd = Time.time + _a.ArriveHit;
            while (Time.time < arriveEnd) yield return null;
            _hitbox.Deactivate();
        }

        // 消える（しぼむ）
        float fadeEnd = Time.time + 0.25f;
        while (Time.time < fadeEnd)
        {
            float k = (fadeEnd - Time.time) / 0.25f;
            _ball.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, k) * BallScale();
            yield return null;
        }
        Destroy(gameObject);
    }

    float _ballScale = -1f;
    float BallScale()
    {
        if (_ballScale < 0f)
        {
            Vector2 native = _ball.sprite != null ? (Vector2)_ball.sprite.bounds.size : Vector2.one;
            _ballScale = _a.Diameter / Mathf.Max(0.01f, native.x);
        }
        return _ballScale;
    }

    void ShowMarks(Vector2 from, Vector2 to)
    {
        ClearMarks();
        // 通り道の点線（両側のふち：丸の直径の幅を示す 2本）＋ まん中の点線
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len > 0.1f)
        {
            Vector2 dir = d / len, n = new Vector2(-dir.y, dir.x);
            float r = _a.Diameter * 0.5f;
            foreach (float side in new[] { -r, 0f, r })
            {
                for (float s = 0.4f; s < len; s += 0.5f)
                {
                    Vector2 p = from + dir * s + n * side;
                    var dash = FxKit.Box(transform, Vector2.zero, new Vector2(0.28f, side == 0f ? 3f / 18f : 2f / 18f), side == 0f ? KitDress.Red : OfficeKitTools.Fade(KitDress.Red, 0.6f), MarkOrder, "Dash");
                    dash.transform.position = p;
                    dash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    _marks.Add(dash.gameObject);
                }
            }
        }
        // 行き先の点線の輪（ゲームの世界に置く。丸が動いても、その場に残す）
        var ring = AttackRunner.AddVisual(transform, KeynoteArt.DottedRing(Mathf.RoundToInt(_a.Diameter * 18f)), Color.white, Vector2.one * _a.Diameter, MarkOrder, "Ring");
        ring.transform.position = to;
        _marks.Add(ring.gameObject);
    }

    void ClearMarks()
    {
        foreach (var m in _marks) if (m != null) Destroy(m);
        _marks.Clear();
    }

    void Ghost(Vector2 p, Color c)
    {
        FxAnim fx = FxAnim.Create("Keynote_Ghost", p, 0.25f);
        AttackRunner.AddVisual(fx.transform, _ball.sprite, OfficeKitTools.Fade(c, 0.35f), Vector2.one * _a.Diameter, BallOrder - 1, "Ghost");
        fx.FadeFrom(0f).Refresh();
    }
}
