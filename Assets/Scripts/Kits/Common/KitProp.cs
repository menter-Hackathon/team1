using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】叩ける置物（2026-11-19。docs/app-kits-1-design.md E6）。担当0 の物。
/// ・場に置く物。プレイヤーの近接か能力（プレイヤーの攻撃判定 Hitbox）が当たると、残りの回数が減り、0 で壊れて呼び出し元に知らせる
/// ・体当たりのダメージは無い。被弾範囲（Hurtbox）を持たないので、ロックオンの相手・雑魚の数・ボスのゲージには数えない
///   （当たったかは、置物が自分でプレイヤーの攻撃判定と重なっているかを見る）
/// ・StageRule から置くときは ctx.TrackPlaced(prop.gameObject) で覚えさせる（片づけの表で消える）
/// ・ボスの技から置くときは、owner（ボスの AttackRunner）を渡す形の Spawn を使う（ボスの出した物になり、50％・100％で消える）
/// ・乗れる置物にしたいときは、KitBlock（担当D）と組み合わせる
/// </summary>
public class KitProp : MonoBehaviour
{
    /// <summary>同じ攻撃判定が、続けて当たったと数えない秒数【仮】</summary>
    const float RehitDelay = 0.3f;

    /// <summary>壊れたとき（1回だけ）</summary>
    public event Action<KitProp> Broken;
    /// <summary>当たったが、まだ壊れていないとき（残りの回数）</summary>
    public event Action<KitProp, int> Hit;

    /// <summary>壊れるまでの残りの回数</summary>
    public int HitsLeft { get; private set; } = 1;
    public bool IsBroken { get; private set; }
    /// <summary>本体の絵</summary>
    public SpriteRenderer Visual { get; private set; }
    /// <summary>大きさ（マス）</summary>
    public Vector2 Size { get; private set; }
    /// <summary>壊れたら消すか（false なら、呼び出し元が消す）</summary>
    public bool DestroyOnBreak { get; set; } = true;

    BoxCollider2D _area;
    float _flashUntil;
    Color _baseColor = Color.white;
    float _dieAt = float.PositiveInfinity;
    readonly Collider2D[] _results = new Collider2D[16];
    readonly Dictionary<Hitbox, float> _lastHit = new Dictionary<Hitbox, float>();

    /// <summary>
    /// 置物を1つ置く（ボスの技から：owner の出した物になり、50％・100％・ボスの登場のやり直しで消える）。
    /// ほかの引数は持ち主なしの Spawn と同じ
    /// </summary>
    public static KitProp Spawn(AttackRunner owner, Vector2 position, Sprite sprite, Vector2 size, Action<KitProp> onBroken = null, int hits = 1, float lifetime = 0f, int sortingOrder = 15)
    {
        KitProp prop = Spawn(position, sprite, size, onBroken, hits, lifetime, sortingOrder);
        if (owner != null)
        {
            SceneManagerMove(prop.gameObject, owner.gameObject);
            owner.Register(prop.gameObject); // 出した物として登録（50％の形態変化でも消えるように、印も付ける）
            prop.gameObject.AddComponent<KitPropBossMark>();
        }
        return prop;
    }

    static void SceneManagerMove(GameObject go, GameObject like)
    {
        if (go.scene != like.scene && like.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, like.scene);
    }

    /// <summary>
    /// 置物を1つ置く（持ち主なし：StageRule から置くとき）。position は真ん中。sprite が null なら白い四角。hits：壊れるまでの回数。
    /// lifetime：秒数で消す（0 ならずっと）。onBroken：壊れたとき
    /// </summary>
    public static KitProp Spawn(Vector2 position, Sprite sprite, Vector2 size, Action<KitProp> onBroken = null, int hits = 1, float lifetime = 0f, int sortingOrder = 15)
    {
        var go = new GameObject("KitProp");
        go.transform.position = position;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        var prop = go.AddComponent<KitProp>();
        prop._area = col;
        prop.Size = size;
        prop.HitsLeft = Mathf.Max(1, hits);
        prop.Visual = AttackRunner.AddVisual(go.transform, sprite != null ? sprite : PlaceholderSprites.Square, Color.white, size, sortingOrder);
        prop._baseColor = prop.Visual.color;
        if (lifetime > 0f) prop._dieAt = Time.time + lifetime;
        if (onBroken != null) prop.Broken += onBroken;
        return prop;
    }

    /// <summary>絵を変える（大きさはそのまま）</summary>
    public void SetSprite(Sprite sprite, Color? color = null)
    {
        if (Visual == null || sprite == null) return;
        Vector2 native = sprite.bounds.size;
        Visual.drawMode = SpriteDrawMode.Simple;
        Visual.sprite = sprite;
        Visual.transform.localScale = new Vector3(Size.x / Mathf.Max(native.x, 0.001f), Size.y / Mathf.Max(native.y, 0.001f), 1f);
        if (color.HasValue) Visual.color = _baseColor = color.Value;
    }

    /// <summary>壊す（攻撃が当たらなくても）。壊れたら Broken を出す</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        HitsLeft = 0;
        if (_area != null) _area.enabled = false;
        GameAudio.Event("prop_break");
        try { Broken?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
        if (DestroyOnBreak) Destroy(gameObject);
    }

    void Update()
    {
        if (Time.time >= _dieAt) { Destroy(gameObject); return; }
        if (Visual != null) Visual.color = Time.time < _flashUntil ? Color.Lerp(_baseColor, Color.white, 0.7f) : _baseColor;
    }

    void FixedUpdate()
    {
        if (IsBroken || _area == null || !_area.enabled) return;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.NoFilter();
        int n = _area.OverlapCollider(filter, _results);
        for (int i = 0; i < n && !IsBroken; i++)
        {
            Collider2D c = _results[i];
            if (c == null) continue;
            Hitbox h = c.GetComponent<Hitbox>();
            if (h == null) h = c.GetComponentInParent<Hitbox>();
            if (h == null || h.Team != Team.Player || !h.IsActive) continue;
            if (_lastHit.TryGetValue(h, out float t) && Time.time - t < RehitDelay) continue;
            _lastHit[h] = Time.time;
            TakeHit();
        }
    }

    void TakeHit()
    {
        HitsLeft--;
        _flashUntil = Time.time + 0.1f;
        if (HitsLeft <= 0) { Break(); return; }
        GameAudio.Event("prop_hit");
        try { Hit?.Invoke(this, HitsLeft); }
        catch (Exception e) { Debug.LogException(e); }
    }
}

/// <summary>ボスの技が置いた置物の印。形態変化（50％）で、ボスが CustomEffect の付いた物を消すときに一緒に消える</summary>
public class KitPropBossMark : CustomEffect
{
}
