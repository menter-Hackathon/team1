using System.Collections;
using UnityEngine;

/// <summary>
/// 指定位置の判定（部品 E）と、残り続けるダメージ床（部品 H）。AttackRunner が AttackData から作る。
/// 流れ：（ずらす時間）→ 予告（床の赤丸）→ 判定を出す（判定時間、または持続時間のあいだ）→ 消える。
/// 出したあとは自分で進むので、攻撃した人が攻撃を止めても最後まで残る。
///
/// 置き方
/// ・床の上に出す（柱・赤丸）：真下の床の上面に下の端を付ける。高さが部屋の高さ以上なら、部屋の床から画面の上端までを覆う
/// ・弾が当たった位置に出す（赤い波線）：当たった面に下の端を付け、面に沿わせる（壁なら縦向き）
/// </summary>
public class AreaStrike : MonoBehaviour
{
    static readonly Color TelegraphColor = new Color(1f, 0.15f, 0.15f, 0.55f);

    AttackRunner _owner;
    AttackData _data;
    Hitbox _hitbox;
    SpriteRenderer _visual;
    SpriteRenderer _telegraph;
    int _index;       // 何個目か（スキンの絵の番号。2026-11-18）
    Vector2 _size;    // 判定の大きさ（円は x が直径）
    Sprite[] _frames;

    public AttackData Data => _data;
    /// <summary>判定を出している間 true</summary>
    public bool IsHitActive { get; private set; }
    /// <summary>判定の中心（テスト用）</summary>
    public Vector2 HitCenter => _hitbox != null ? (Vector2)_hitbox.transform.position : (Vector2)transform.position;

    /// <summary>
    /// 判定を1つ出す。point は狙った位置（床の上に出すときは、その真下の床に置く）。
    /// normal は面の向き（弾が当たった位置に出すとき）。delay 秒たってから予告を始める。
    /// index：同時に出す物の何個目か（スキンの絵の番号。2026-11-18）
    /// </summary>
    public static AreaStrike Spawn(AttackRunner owner, AttackData data, Vector2 point, Vector2 normal, float delay, int index = 0)
    {
        Vector2 size = data.AreaSize;
        Vector2 up = Vector2.up;
        Vector2 surface = point; // 予告を出す位置（面の上）
        Vector2 center;

        if (data.Anchor == AreaAnchor.HitPoint)
        {
            if (data.AlignToSurface && normal.sqrMagnitude > 0.0001f) up = normal.normalized;
            center = point + up * (size.y * 0.5f);
        }
        else if (data.SnapToGround)
        {
            owner.TryFindGround(point.x, point.y + 0.5f, out float groundY);
            surface = new Vector2(point.x, groundY);
            Rect arena = BattleArena.GetBounds();
            float bottom = size.y >= arena.height ? arena.yMin : groundY; // 画面の高さの柱は、部屋の床から覆う
            center = new Vector2(point.x, bottom + size.y * 0.5f);
        }
        else
        {
            center = point;
            surface = point;
        }

        GameObject go = owner.CreateSpawnedObject($"Area_{data.name}", center);
        float angle = Vector2.SignedAngle(Vector2.up, up);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        Collider2D col;
        int facing = owner.Facing;
        if (data.Shape == AreaShape.Polyline)
        {
            // 折れ線：点は右向きの値。左向きでは左右を反転する
            var e = go.AddComponent<EdgeCollider2D>();
            Vector2[] src = data.PolylinePoints;
            if (src == null || src.Length < 2) src = new[] { new Vector2(-size.x * 0.5f, 0f), new Vector2(size.x * 0.5f, 0f) };
            var pts = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++) pts[i] = new Vector2(src[i].x * facing, src[i].y);
            e.points = pts;
            e.edgeRadius = data.PolylineRadius;
            col = e;
        }
        else if (data.Shape == AreaShape.Circle)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = size.x * 0.5f;
            col = c;
        }
        else
        {
            var b = go.AddComponent<BoxCollider2D>();
            b.size = size;
            col = b;
        }
        col.isTrigger = true;

        var strike = go.AddComponent<AreaStrike>();
        strike._owner = owner;
        strike._data = data;
        strike._index = index;
        strike._size = size;
        Sprite sprite = data.Sprite != null ? data.Sprite : (data.Shape == AreaShape.Circle ? PlaceholderSprites.Circle : PlaceholderSprites.Square);
        Vector2 visualSize = data.Shape == AreaShape.Circle ? new Vector2(size.x, size.x) : size;
        Color color = data.Color;
        color.a *= 0.75f;
        strike._visual = AttackRunner.AddVisual(go.transform, sprite, color, visualSize, 18);
        strike._visual.enabled = false;
        strike._hitbox = owner.AddHitbox(go, data, col);

        strike.CreateTelegraph(surface, up, size);
        strike.StartCoroutine(strike.Run(delay));
        return strike;
    }

    /// <summary>予告：床の上に平たい赤丸（面の上に出す）。予告時間が 0 なら出さない</summary>
    void CreateTelegraph(Vector2 surface, Vector2 up, Vector2 size)
    {
        if (_data.TelegraphTime <= 0f) return;
        var tg = new GameObject("Telegraph");
        tg.layer = gameObject.layer;
        tg.transform.SetParent(transform, false);
        tg.transform.position = surface + up * 0.15f;
        tg.transform.rotation = transform.rotation;
        // 絵があれば（GameArt）その色のまま、無ければ仮の赤丸
        Sprite mark = GameArt.Instance != null ? GameArt.Instance.BreakpointMark : null;
        _telegraph = mark != null
            ? AttackRunner.AddVisual(tg.transform, mark, new Color(1f, 1f, 1f, 0.9f), new Vector2(size.x, 0.5f), 17, "Circle")
            : AttackRunner.AddVisual(tg.transform, PlaceholderSprites.Circle, TelegraphColor, new Vector2(size.x, 0.5f), 17, "Circle");
        _telegraph.enabled = false;
    }

    /// <summary>予告を点滅させる（予告が無ければすぐ終わる）</summary>
    IEnumerator BlinkTelegraph()
    {
        if (_telegraph == null) yield break;
        float end = Time.time + _data.TelegraphTime;
        while (Time.time < end)
        {
            _telegraph.enabled = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 || end - Time.time > 0.4f;
            yield return null;
        }
        _telegraph.enabled = false;
    }

    IEnumerator Run(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 予告（点滅させる）
        if (_telegraph != null)
        {
            NotifySkin(DressPhase.Telegraph, _telegraph, _data.TelegraphTime);
            float end = Time.time + _data.TelegraphTime;
            while (Time.time < end)
            {
                _telegraph.enabled = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 || end - Time.time > 0.4f;
                yield return null;
            }
            _telegraph.enabled = false;
        }

        // 判定
        IsHitActive = true;
        _visual.enabled = true;
        _hitbox.Activate();
        NotifySkin(DressPhase.Active, _visual, _data.HitWindow);
        float hitEnd = Time.time + _data.HitWindow;
        if (_data.PersistDuration > 0f)
        {
            // 持続：ヒット間隔ごとに「当てた相手」の記録を消して、もう一度当たるようにする
            float nextTick = Time.time + _data.TickInterval;
            while (Time.time < hitEnd)
            {
                if (Time.time >= nextTick)
                {
                    _hitbox.Activate();
                    nextTick += _data.TickInterval;
                }
                yield return null;
            }
        }
        else
        {
            while (Time.time < hitEnd) yield return null;
        }

        IsHitActive = false;
        _hitbox.Deactivate();
        NotifySkin(DressPhase.Vanish, null, 0f);
        Destroy(gameObject);
    }

    /// <summary>スキンに場面を知らせる（2026-11-18：スキルの見た目 1-1）。付けたら今の絵をかくす</summary>
    void NotifySkin(DressPhase phase, SpriteRenderer current, float duration)
    {
        if (_data == null) return;
        SkinDresser.Dress(_owner, _data, gameObject, phase, current, _size, _index, transform.up, Mathf.Max(1, _data.AreaCount), duration);
    }
}
