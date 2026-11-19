using UnityEngine;

/// <summary>
/// 筆を振る（2026-11-19。Photoshop の能力。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Brush）から作る。
/// ・持ち主の前で、筆を下から上へ振る（振り始めの角度 → 振り終わりの角度。だんだんゆっくり）。持ち主が動いても付いてくる
/// ・筆にも判定がある（筆の長さ×太さ。1回振るあいだに、同じ相手には1回だけ当たる）
/// ・振る途中で、筆の先からインクが飛び出す（InkBlot。振る向きに少し飛んで、その場に残る）
/// ・振った跡に、筆の残像が少し残る
/// ・絵は筆の長さに合わせて大きさを変える（Photoshop のボスの大きな筆。2026-11-19）
/// ・「筆にも判定」がオフなら、筆は当たらない（インクだけで攻撃する）。「インクをまき散らす」がオンなら、筆のあちこちから前の方へ広く飛ばす
/// </summary>
public class BrushSwing : MonoBehaviour
{
    const float FadeTime = 0.08f;

    AttackRunner _owner;
    AttackData _data;
    SpriteRenderer _sr;
    BoxCollider2D _col;
    Hitbox _hitbox;
    float _start;
    int _facing;
    int _inksDone;
    Vector2 _lastTip;

    /// <summary>振り終わったら true（筆が消えるのは少しあと）</summary>
    public bool IsDone { get; private set; }

    public static BrushSwing Spawn(AttackRunner owner, AttackData data)
    {
        GameObject go = owner.CreateSpawnedObject($"Brush_{data.name}", owner.transform.position);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var s = go.AddComponent<BrushSwing>();
        s._owner = owner;
        s._data = data;
        s._start = Time.time;
        s._facing = owner.Facing;

        // 筆の絵（原点＝持ち手の端。右へ伸びる）
        var art = new GameObject("Art");
        art.layer = go.layer;
        art.transform.SetParent(go.transform, false);
        s._sr = art.AddComponent<SpriteRenderer>();
        s._sr.sortingOrder = 17; // プレイヤー（15）の手前
        if (data.Sprite != null)
        {
            s._sr.sprite = data.Sprite;
            // 絵の長さを筆の長さにそろえる（同じ長さなら、そのまま）
            float native = data.Sprite.bounds.size.x;
            if (native > 0.01f) art.transform.localScale = Vector3.one * (data.BrushLength / native);
        }
        else
        {
            s._sr.sprite = PlaceholderSprites.Square;
            art.transform.localPosition = new Vector3(data.BrushLength * 0.5f, 0f, 0f);
            art.transform.localScale = new Vector3(data.BrushLength, data.BrushWidth, 1f);
            s._sr.color = data.Color;
        }
        s._sr.flipY = s._facing < 0; // 左向きのときは、筆が上下さかさまにならないように

        // 筆の判定（筆の長さ×太さ）
        s._col = go.AddComponent<BoxCollider2D>();
        s._col.isTrigger = true;
        s._col.size = new Vector2(data.BrushLength, data.BrushWidth);
        s._col.offset = new Vector2(data.BrushLength * 0.5f, 0f);
        s._hitbox = owner.AddHitbox(go, data, s._col);
        s._hitbox.Hit += s.OnHit;
        if (data.BrushHits) s._hitbox.Activate();

        s.Pose(0f);
        s._lastTip = s.Tip;
        // プレイヤーの筆：押した瞬間に、筆が通る弧の上へインクを全部出す（出た瞬間から当たる。2026-11-19）。
        // ボスの筆（まき散らす）は、今までどおり振る途中で出す
        if (!data.BrushInkScatter) s.SpawnAllInksNow();
        return s;
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnHit(IDamageable target, DamageInfo info) => Vfx.HitSpark(info.HitPoint);

    /// <summary>筆の先（インクが飛び出す所）</summary>
    Vector2 Tip => transform.TransformPoint(new Vector3(_data.BrushLength * 0.9f, 0f, 0f));

    /// <summary>振る進み具合 t（0〜1）の位置と角度にする。持ち主に付いていく</summary>
    void Pose(float t)
    {
        if (_owner == null) return;
        Vector2 pivot = _data.BrushPivot;
        transform.position = (Vector2)_owner.transform.position + new Vector2(_facing * pivot.x, pivot.y);
        float k = 1f - (1f - t) * (1f - t); // だんだんゆっくり
        float angle = Mathf.Lerp(_data.BrushAngles.x, _data.BrushAngles.y, k);
        // 左向きのときは、左右を反転した角度
        transform.rotation = Quaternion.Euler(0f, 0f, _facing >= 0 ? angle : 180f - angle);
    }

    void Update()
    {
        float elapsed = Time.time - _start;
        float swing = Mathf.Max(0.02f, _data.BrushSwingTime);
        if (!IsDone)
        {
            float t = Mathf.Clamp01(elapsed / swing);
            Pose(t);
            Trail();
            SpawnInks(t);
            if (t >= 1f)
            {
                IsDone = true;
                _hitbox.Deactivate();
            }
            return;
        }

        // 振り終わり：さっと消える
        Pose(1f);
        float f = (elapsed - swing) / FadeTime;
        if (f >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Color c = _sr.color;
        c.a = 1f - f;
        _sr.color = c;
    }

    /// <summary>
    /// インクを全部、今すぐ出す：振る途中の均等な所（振り始めから 1/(n+1)・2/(n+1)…）での筆の先から、そのときの振る向きへ飛ばす
    /// </summary>
    void SpawnAllInksNow()
    {
        if (_data.BrushInk == null || _owner == null) return;
        int n = _data.BrushInkCount;
        Vector2 pivot = (Vector2)_owner.transform.position + new Vector2(_facing * _data.BrushPivot.x, _data.BrushPivot.y);
        for (int i = 0; i < n; i++)
        {
            float t = (i + 1f) / (n + 1f);
            float k = 1f - (1f - t) * (1f - t);
            float deg = Mathf.Lerp(_data.BrushAngles.x, _data.BrushAngles.y, k);
            if (_facing < 0) deg = 180f - deg;
            float a = deg * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 tip = pivot + along * (_data.BrushLength * 0.9f);
            // 振る向き（弧の接線）：右向きは角度が増える向き、左向きは減る向き
            Vector2 tangent = _facing >= 0 ? new Vector2(-along.y, along.x) : new Vector2(along.y, -along.x);
            float dist = Random.Range(Mathf.Min(_data.BrushInkFlingMin, _data.BrushInkFling), _data.BrushInkFling);
            InkBlot.Spawn(_owner, _data.BrushInk, tip, tangent * dist);
        }
        _inksDone = n;
    }

    /// <summary>振る途中で、決まった数のインクを、均等な間隔で飛ばす（筆の先から、振る向きへ）</summary>
    void SpawnInks(float t)
    {
        Vector2 tip = Tip;
        int n = _data.BrushInkCount;
        while (_data.BrushInk != null && _inksDone < n && t >= (_inksDone + 1f) / (n + 1f))
        {
            float dist = Random.Range(Mathf.Min(_data.BrushInkFlingMin, _data.BrushInkFling), _data.BrushInkFling);
            if (_data.BrushInkScatter)
            {
                // 筆のあちこちから、前の方へ広くまき散らす（部屋の外には出さない）
                Vector2 from = transform.TransformPoint(new Vector3(_data.BrushLength * Random.Range(0.3f, 0.9f), 0f, 0f));
                float a = Random.Range(-50f, 45f) * Mathf.Deg2Rad;
                var dir = new Vector2(_facing * Mathf.Cos(a), Mathf.Sin(a));
                Rect arena = BattleArena.GetBounds();
                Vector2 to = from + dir * dist;
                to.x = Mathf.Clamp(to.x, arena.xMin + 0.8f, arena.xMax - 0.8f);
                to.y = Mathf.Clamp(to.y, arena.yMin + 0.8f, arena.yMax - 0.8f);
                InkBlot.Spawn(_owner, _data.BrushInk, from, to - from);
            }
            else
            {
                Vector2 dir = tip - _lastTip;
                if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(_facing, 1f);
                InkBlot.Spawn(_owner, _data.BrushInk, tip, dir.normalized * dist);
            }
            _inksDone++;
        }
        _lastTip = tip;
    }

    /// <summary>振った跡に、筆の残像を残す</summary>
    void Trail()
    {
        if (_sr.sprite == null) return;
        var go = new GameObject("BrushGhost");
        go.transform.SetPositionAndRotation(_sr.transform.position, _sr.transform.rotation);
        go.transform.localScale = _sr.transform.lossyScale;
        var g = go.AddComponent<SpriteRenderer>();
        g.sprite = _sr.sprite;
        g.flipY = _sr.flipY;
        g.sortingOrder = _sr.sortingOrder - 1;
        g.color = new Color(0.55f, 0.82f, 1f, 0.45f);
        go.AddComponent<FadeAndDestroy>().Setup(0.12f);
    }
}
