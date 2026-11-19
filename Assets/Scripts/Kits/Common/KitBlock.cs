using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】置く物（2026-11-19。docs/app-kits-2-design.md X4）。担当D の物。Figma の四角・Blender の Default Cube が使う。
/// ・乗れる・壁になる・弾を止める（レイヤー Ground のトリガーでない箱）。N秒で消える。当たらない（ダメージなし）
/// ・絵は無くてもよい（3D の見た目を子に付ける物も、Attach で箱だけ付けられる）
/// ・置く前に CanPlace で調べる（プレイヤー・ボス・地形・足場・ほかの置いた物に重ならない。部屋の両端にかからない）。
///   予告のあいだに入られることがあるので、固くなる直前にもう一度調べる（Place は、だめなら null を返す）
/// ・置いたあとも毎フレーム見張る：
///   雑魚が中にいたら上へ押し出す／プレイヤーが中にいたら、出るまで箱をすり抜けにする／ボスの体に重なったら消える（ワープの行き先）
/// ・叩ける（KitProp）と組み合わせるときは MakeBreakable
/// ・ステージの決まりから置くときは ctx.TrackPlaced(block.gameObject)、ボスの技から置くときは owner を渡す（50％・100％で消える）
/// </summary>
public class KitBlock : MonoBehaviour
{
    /// <summary>上に空けておく高さ（マス）。プレイヤー（1.0）が上に立てるように</summary>
    public const float HeadRoom = 1.6f;
    /// <summary>消えるときにうすくなる秒数</summary>
    public const float FadeTime = 0.35f;
    /// <summary>見張るときに内側へ縮める幅（マス。ふれているだけの物を数えない）</summary>
    const float Inset = 0.06f;

    static readonly List<KitBlock> Alive = new List<KitBlock>();
    static readonly Collider2D[] Probe = new Collider2D[8];
    static BossController _boss;
    static float _bossFindAt = -1f;

    /// <summary>今のボス（1秒ごとに探し直す）</summary>
    static BossController Boss
    {
        get
        {
            if ((_boss == null || !_boss.isActiveAndEnabled) && Time.time >= _bossFindAt)
            {
                _bossFindAt = Time.time + 1f;
                _boss = KitBoss.Current;
            }
            return _boss;
        }
    }

    /// <summary>今ある置く物</summary>
    public static IReadOnlyList<KitBlock> All { get { Alive.RemoveAll(b => b == null); return Alive; } }

    /// <summary>消えたとき（時間切れ・Vanish・ボスに重なった・壊れた）。1回だけ</summary>
    public event Action<KitBlock> Vanished;

    /// <summary>大きさ（マス）</summary>
    public Vector2 Size { get; private set; }
    /// <summary>本体の絵（無ければ null）</summary>
    public SpriteRenderer Visual { get; private set; }
    /// <summary>固い箱</summary>
    public BoxCollider2D Solid { get; private set; }
    /// <summary>叩ける置物（MakeBreakable のあと）</summary>
    public KitProp Prop { get; private set; }
    /// <summary>消えている途中か</summary>
    public bool IsVanishing => _vanishAt >= 0f;
    /// <summary>残りの秒数（ずっとなら +∞）</summary>
    public float Remaining => _dieAt < float.PositiveInfinity ? Mathf.Max(0f, _dieAt - Time.time) : float.PositiveInfinity;

    float _dieAt = float.PositiveInfinity;
    float _vanishAt = -1f;
    bool _vanishedSent;
    Color _baseColor = Color.white;
    readonly Collider2D[] _buf = new Collider2D[16];
    PlayerController _player;
    BoxCollider2D _playerBox;

    // ───────── 置く ─────────

    /// <summary>
    /// 置く物を1つ置く。center は真ん中。sprite が null なら絵なし（color の四角も出さない）。lifetime：秒数で消える（0 ならずっと）。
    /// owner を渡すとボスの出した物になる（50％・100％で消える）。置けなければ null
    /// </summary>
    public static KitBlock Place(Vector2 center, Vector2 size, Sprite sprite = null, float lifetime = 0f, AttackRunner owner = null, int sortingOrder = 6, bool check = true)
    {
        if (check && !CanPlace(center, size)) return null;
        var go = new GameObject("KitBlock");
        go.transform.position = center;
        if (owner != null)
        {
            if (go.scene != owner.gameObject.scene && owner.gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            owner.Register(go);
            go.AddComponent<KitBlockBossMark>();
        }
        KitBlock b = Attach(go, size, lifetime);
        if (sprite != null)
        {
            b.Visual = AttackRunner.AddVisual(go.transform, sprite, Color.white, size, sortingOrder);
            b._baseColor = b.Visual.color;
        }
        return b;
    }

    /// <summary>今ある GameObject に、乗れる箱だけを付ける（3D の見た目など、絵は呼び出し元が持つ）。レイヤーは Ground になる</summary>
    public static KitBlock Attach(GameObject go, Vector2 size, float lifetime = 0f)
    {
        if (go == null) return null;
        go.layer = GroundLayer;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = size;
        var b = go.AddComponent<KitBlock>();
        b.Solid = col;
        b.Size = size;
        if (lifetime > 0f) b._dieAt = Time.time + lifetime;
        return b;
    }

    /// <summary>床の上に置く（下の端が床）。置けなければ null</summary>
    public static KitBlock PlaceOnFloor(float x, float floorY, Vector2 size, Sprite sprite = null, float lifetime = 0f, AttackRunner owner = null, int sortingOrder = 6)
        => Place(new Vector2(x, floorY + size.y * 0.5f), size, sprite, lifetime, owner, sortingOrder);

    // ───────── 置けるか ─────────

    public static int GroundLayer => LayerMask.NameToLayer("Ground") >= 0 ? LayerMask.NameToLayer("Ground") : 0;
    static int TerrainMask => LayerMask.GetMask("Ground", "OneWayPlatform");

    /// <summary>
    /// その場所に置けるか：地形・足場・ほかの置いた物（上に HeadRoom の空きも見る）・プレイヤー（gap マス空ける）・ボスの体に重ならない
    /// </summary>
    public static bool CanPlace(Vector2 center, Vector2 size, float playerGap = 0.3f)
    {
        Vector2 half = size * 0.5f;
        // 地形・足場（箱と、その上の空き）。床にふれているだけは数えない
        Vector2 colMin = new Vector2(center.x - half.x + Inset, center.y - half.y + Inset);
        Vector2 colMax = new Vector2(center.x + half.x - Inset, center.y + half.y + HeadRoom);
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(TerrainMask);
        if (Physics2D.OverlapArea(colMin, colMax, filter, Probe) > 0) return false;
        // プレイヤー
        var box = new Bounds(center, new Vector3(size.x + playerGap * 2f, size.y + playerGap * 2f, 10f));
        PlayerController pc = KitPlayer.Controller;
        if (pc != null && pc.TryGetComponent(out BoxCollider2D pb) && Overlaps(box, pb.bounds)) return false;
        // ボス
        foreach (var boss in UnityEngine.Object.FindObjectsOfType<BossController>())
        {
            if (!boss.isActiveAndEnabled || boss.State == BossState.Normalized) continue;
            if (Overlaps(box, boss.BodyBounds)) return false;
        }
        return true;
    }

    /// <summary>
    /// 床の上面（Ground。すり抜け床は数えない）。x の真下を、area の下の方から調べる（PaceEvent.FloorY と同じ考え方）。
    /// 見つからなければ false（y は area の下の端）
    /// </summary>
    public static bool TryFindFloor(float x, Rect area, out float y)
    {
        int ground = LayerMask.GetMask("Ground");
        var hits = new RaycastHit2D[8];
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(ground);
        int n = Physics2D.Raycast(new Vector2(x, area.yMin + 2.4f), Vector2.down, filter, hits, 6f);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0.01f) continue;
            if (hits[i].collider != null && hits[i].collider.GetComponent<KitBlock>() != null) continue; // 置いた物の上は床にしない
            y = hits[i].point.y;
            return true;
        }
        y = Mathf.Max(area.yMin, BattleArena.GetBounds().yMin);
        return false;
    }

    /// <summary>
    /// 床の上に置ける x を探す（area の中・両端 edge マスを除く・プレイヤーから minFromPlayer マス以上・足場の下を除く）。
    /// tries 回ためして、見つかれば true（pos は箱の真ん中）
    /// </summary>
    public static bool TryFindSpot(Rect area, Vector2 size, float minFromPlayer, out Vector2 pos, float edge = 2f, int tries = 16)
    {
        pos = Vector2.zero;
        Transform player = KitPlayer.Transform;
        float xMin = area.xMin + edge + size.x * 0.5f, xMax = area.xMax - edge - size.x * 0.5f;
        if (xMax <= xMin) return false;
        for (int i = 0; i < tries; i++)
        {
            float x = UnityEngine.Random.Range(xMin, xMax);
            if (player != null && Mathf.Abs(x - player.position.x) < minFromPlayer + size.x * 0.5f) continue;
            if (!TryFindFloor(x, area, out float floor)) continue;
            var c = new Vector2(x, floor + size.y * 0.5f);
            if (!CanPlace(c, size)) continue;
            pos = c;
            return true;
        }
        return false;
    }

    static bool Overlaps(Bounds a, Bounds b) =>
        a.min.x < b.max.x && a.max.x > b.min.x && a.min.y < b.max.y && a.max.y > b.min.y;

    // ───────── 叩ける ─────────

    /// <summary>叩ける置物（KitProp）を重ねる（近接・能力が hits 回当たると壊れる）。onBroken のあと箱も消える</summary>
    public KitProp MakeBreakable(int hits, Action<KitBlock> onBroken = null)
    {
        if (Prop != null) return Prop;
        Prop = KitProp.Spawn(transform.position, null, Size + new Vector2(0.15f, 0.15f), null, hits);
        if (Prop.Visual != null) Prop.Visual.enabled = false; // 絵は箱の物を使う
        Prop.transform.SetParent(transform, true);
        Prop.DestroyOnBreak = false;
        Prop.Hit += (p, left) => Flash();
        Prop.Broken += p =>
        {
            try { onBroken?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
            Vanish(0.12f);
        };
        return Prop;
    }

    float _flashUntil;
    void Flash() => _flashUntil = Time.time + 0.1f;

    // ───────── 消す ─────────

    /// <summary>消す（fade 秒でうすくなってから。すぐ通れるようになる）</summary>
    public void Vanish(float fade = FadeTime)
    {
        if (IsVanishing) return;
        _vanishAt = Time.time + Mathf.Max(0f, fade);
        if (Solid != null) Solid.enabled = false;
        if (Prop != null) Prop.gameObject.SetActive(false);
    }

    /// <summary>本体の絵の色を変える（Visual.color を直接変えると、毎フレーム戻される）</summary>
    public void SetColor(Color c)
    {
        _baseColor = c;
        if (Visual != null) Visual.color = c;
    }

    /// <summary>残りの時間を変える（0 ならずっと）</summary>
    public void SetLifetime(float seconds) => _dieAt = seconds > 0f ? Time.time + seconds : float.PositiveInfinity;

    // ───────── 毎フレーム ─────────

    void OnEnable() { if (!Alive.Contains(this)) Alive.Add(this); }
    void OnDisable() => Alive.Remove(this);

    void OnDestroy()
    {
        Alive.Remove(this);
        SendVanished();
    }

    void SendVanished()
    {
        if (_vanishedSent) return;
        _vanishedSent = true;
        GameAudio.Event("block_vanish");
        try { Vanished?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
    }

    void Update()
    {
        float now = Time.time;
        if (!IsVanishing && now >= _dieAt) Vanish();
        float a = 1f;
        if (IsVanishing)
        {
            float left = _vanishAt - now;
            if (left <= 0f) { SendVanished(); Destroy(gameObject); return; }
            a = Mathf.Clamp01(left / FadeTime);
        }
        else if (_dieAt - now < FadeTime) a = Mathf.Clamp01((_dieAt - now) / FadeTime);
        if (Visual != null)
        {
            Color c = now < _flashUntil ? Color.Lerp(_baseColor, Color.white, 0.6f) : _baseColor;
            c.a = _baseColor.a * a;
            Visual.color = c;
        }
    }

    void FixedUpdate()
    {
        if (IsVanishing || Solid == null) return;
        // 箱の範囲は位置と大きさから作る（すり抜けにした箱は bounds が空になるため）
        Vector3 ls = transform.lossyScale;
        Vector2 sz = new Vector2(Size.x * Mathf.Abs(ls.x), Size.y * Mathf.Abs(ls.y));
        var me = new Bounds((Vector2)transform.TransformPoint(Solid.offset), new Vector3(Mathf.Max(0.01f, sz.x - Inset * 2f), Mathf.Max(0.01f, sz.y - Inset * 2f), 10f));

        // ボスの体に重なった（ワープの行き先・浮くボスが下りてきた）：消える
        BossController boss = Boss;
        if (boss != null && boss.isActiveAndEnabled && boss.State != BossState.Normalized && boss.Visibility >= 0.5f)
        {
            Bounds bb = boss.BodyBounds;
            bb.Expand(new Vector3(-0.2f, -0.2f, 0f));
            if (Overlaps(me, bb)) { Vanish(0.15f); return; }
        }

        // プレイヤー：中にいる間は、すり抜けにする（閉じこめない）
        if (_player == null) { _player = KitPlayer.Controller; _playerBox = _player != null ? _player.GetComponent<BoxCollider2D>() : null; }
        bool playerInside = _playerBox != null && Overlaps(me, _playerBox.bounds);
        Solid.enabled = !playerInside;

        // 雑魚：中にいたら上へ押し出す
        var zf = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        zf.SetLayerMask(LayerMask.GetMask("Enemy"));
        int n = Physics2D.OverlapBox(me.center, me.size, 0f, zf, _buf);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _buf[i];
            if (c == null || c.isTrigger) continue;
            KinematicBody2D body = c.GetComponent<KinematicBody2D>();
            if (body == null || body.GetComponent<BossController>() != null) continue;
            Bounds zb = c.bounds;
            float lift = me.max.y + Inset * 2f - zb.min.y + 0.02f;
            if (lift > 0f && lift < Size.y + 2f) body.Warp((Vector2)body.transform.position + new Vector2(0f, lift));
        }
    }
}

/// <summary>ボスの技が置いた箱の印。形態変化（50％）で、CustomEffect の付いた物と一緒に消える</summary>
public class KitBlockBossMark : CustomEffect
{
}
