using System;
using UnityEngine;

/// <summary>
/// 【仮】盾ウィンドウ。マウスの左ボタンでつかんで動かせる、敵の弾だけを止めるウィンドウ。
/// 2026-11-16 にまさぴ〜が選んだ形。チームの合意はまだ（数値もすべて【仮】）。
///
/// ・部屋に最初から1枚だけ置く。部屋の範囲（BattleArena）の外には出せない
/// ・足場にしない：レイヤー BulletBlocker に置く。プレイヤー・雑魚・ボスの移動（KinematicBody2D）は Ground と OneWayPlatform しか見ないので、すり抜ける
/// ・止めるのは敵の弾（Projectile）だけ。敵の AttackRunner の「弾だけを止める物」に BulletBlocker を入れておく。
///   体当たり・落下物・Area（柱・赤い波線）は止めない。止めた弾は追撃（赤い波線）を出さずに消える（Projectile 側の処理）
/// ・プレイヤーの「新しいタブ」は止めない（瞬間移動の壁止めは AttackRunner の地形だけを見る）
/// ・強くなりすぎないように：弾が N 発当たると割れる → M 秒たつと最初の位置に戻る。ドラッグ中に割れたら、つかみを離す
/// ・マウスが無くてもクリアできること（マウスは、あると楽になる追加の操作）
///
/// 見た目は仮（四角を並べた枠・タイトルバー・耐久の棒）。絵はデザイン担当があとで差し替える。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class ShieldWindow : MonoBehaviour
{
    [Header("耐久【仮】")]
    [Tooltip("弾がこの数だけ当たると割れて消える")]
    [SerializeField, Min(1)] int _maxHits = 5;
    [Tooltip("割れてから、最初の位置に戻るまでの秒数")]
    [SerializeField, Min(0f)] float _respawnDelay = 5f;

    [Header("見た目（仮）")]
    [Tooltip("光らせたり薄くしたりする絵。空なら子のスプライトすべて")]
    [SerializeField] SpriteRenderer[] _renderers;
    [Tooltip("残りの耐久を表す棒（左端を基準に、横の長さを変える）。割れている間は、戻るまでの残り時間を表す。無くてもよい")]
    [SerializeField] SpriteRenderer _durabilityBar;
    [Tooltip("弾が当たったときに光らせる色")]
    [SerializeField] Color _hitFlashColor = Color.white;
    [SerializeField, Min(0f)] float _hitFlashTime = 0.08f;
    [Tooltip("割れている間、最初の位置に薄く出す濃さ（戻ってくる場所の目印）。0なら何も出さない（仕様どおり「割れて消える」）。\n" +
             "【仮】0.2 くらいにすると、薄い枠と、戻るまでの残り時間（タイトルバーの棒）が見える")]
    [SerializeField, Range(0f, 1f)] float _ghostAlpha;

    Rigidbody2D _rb;
    BoxCollider2D _box;
    Vector2 _home;
    int _hits;
    float _respawnAt;
    float _flashUntil;
    bool _dragging;
    Vector2 _grabOffset;
    Vector2 _dragTarget;
    Color[] _baseColors;
    Vector3 _barScale;
    Vector3 _barLocalPos;

    /// <summary>割れている（弾を止めない・つかめない）間 true</summary>
    public bool IsBroken { get; private set; }
    /// <summary>マウスでつかんでいる間 true</summary>
    public bool IsDragging => _dragging;
    /// <summary>今までに止めた弾の数（割れて戻ると0から）</summary>
    public int HitCount => _hits;
    public int MaxHits => _maxHits;
    public float RespawnDelay => _respawnDelay;
    /// <summary>最初の位置（割れたあとに戻る場所）</summary>
    public Vector2 HomePosition => _home;
    /// <summary>今の位置（コライダーの中心）</summary>
    public Vector2 Center => _rb.position + Vector2.Scale(_box.offset, AbsScale);
    /// <summary>ウィンドウの大きさ（ワールド座標）</summary>
    public Vector2 WorldSize => Vector2.Scale(_box.size, AbsScale);

    /// <summary>弾を止めたとき（SE・エフェクト用）</summary>
    public event Action<Projectile> Blocked;
    /// <summary>割れたとき</summary>
    public event Action Broken;
    /// <summary>最初の位置に戻ったとき</summary>
    public event Action Restored;

    Vector2 AbsScale
    {
        get
        {
            Vector3 s = transform.lossyScale;
            return new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
        }
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _box = GetComponent<BoxCollider2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = false; // 何かを押したり、何かに押されたりしない
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _box.isTrigger = false; // 弾の地形判定（トリガーを含めない）に掛かるように
        _home = transform.position;

        if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        if (_durabilityBar != null)
        {
            _barScale = _durabilityBar.transform.localScale;
            _barLocalPos = _durabilityBar.transform.localPosition;
        }
    }

    void OnEnable() => MousePointer.ShowCursor();

    void OnDisable() => EndDrag();

    void Update()
    {
        if (IsBroken)
        {
            if (Time.time >= _respawnAt) Restore();
            else SetBar(1f - (_respawnAt - Time.time) / Mathf.Max(_respawnDelay, 0.0001f));
            return;
        }
        ApplyColors(Time.time < _flashUntil ? 1f : 0f, 1f);
    }

    // タブ（DismissableTab）は Update でクリックを受けるので、重なっていたらタブを先に閉じる
    void LateUpdate()
    {
        if (IsBroken) return;
        if (!_dragging)
        {
            if (MousePointer.PressedThisFrame && MousePointer.TryGetWorldPosition(out Vector2 m) && ContainsPoint(m))
            {
                MousePointer.Consume();
                BeginDrag(m);
            }
            return;
        }
        if (!MousePointer.Held)
        {
            EndDrag();
            return;
        }
        if (MousePointer.TryGetWorldPosition(out Vector2 p)) DragTo(p);
    }

    void FixedUpdate()
    {
        if (_dragging) _rb.MovePosition(_dragTarget);
    }

    /// <summary>その点がウィンドウの上か（割れている間は false）</summary>
    public bool ContainsPoint(Vector2 world)
    {
        if (IsBroken || !_box.enabled) return false;
        Vector2 half = WorldSize * 0.5f, c = Center;
        return Mathf.Abs(world.x - c.x) <= half.x && Mathf.Abs(world.y - c.y) <= half.y;
    }

    /// <summary>つかむ。world はつかんだ点（ワールド座標）。つかんだ点とウィンドウの位置関係を保ったまま動く</summary>
    public void BeginDrag(Vector2 world)
    {
        if (IsBroken) return;
        _dragging = true;
        _grabOffset = _rb.position - world;
        DragTo(world);
    }

    /// <summary>つかんでいる点を world に動かす（部屋の外には出ない）。動くのは次の物理フレーム</summary>
    public void DragTo(Vector2 world)
    {
        if (!_dragging) return;
        _dragTarget = ClampToArena(world + _grabOffset);
    }

    /// <summary>つかみを離す</summary>
    public void EndDrag() => _dragging = false;

    /// <summary>ウィンドウ全体が部屋の範囲に収まる位置にする</summary>
    Vector2 ClampToArena(Vector2 position)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 half = WorldSize * 0.5f;
        Vector2 offset = Vector2.Scale(_box.offset, AbsScale);
        Vector2 c = position + offset;
        c.x = arena.width >= half.x * 2f ? Mathf.Clamp(c.x, arena.xMin + half.x, arena.xMax - half.x) : arena.center.x;
        c.y = arena.height >= half.y * 2f ? Mathf.Clamp(c.y, arena.yMin + half.y, arena.yMax - half.y) : arena.center.y;
        return c - offset;
    }

    /// <summary>
    /// 弾が当たったときに Projectile から呼ばれる。止めたら true（弾は消える）。割れていたら false（弾は通り抜ける）。
    /// N 発目で割れる
    /// </summary>
    public bool TryBlock(Projectile projectile)
    {
        if (IsBroken || !isActiveAndEnabled) return false;
        _hits++;
        _flashUntil = Time.time + _hitFlashTime;
        SetBar(1f - (float)_hits / _maxHits);
        Blocked?.Invoke(projectile);
        GameAudio.Event("shield_block");
        if (_hits >= _maxHits) Break();
        return true;
    }

    void Break()
    {
        IsBroken = true;
        EndDrag();
        _box.enabled = false; // 弾を止めない・つかめない
        _respawnAt = Time.time + _respawnDelay;
        // 見えなくして、戻ってくる場所へ移しておく（判定は無い）。_ghostAlpha が 0 より大きければ、そこに薄く出る
        Teleport(_home);
        ApplyColors(0f, _ghostAlpha);
        SetBar(0f);
        Broken?.Invoke();
        GameAudio.Event("shield_break");
    }

    void Restore()
    {
        IsBroken = false;
        _hits = 0;
        Teleport(_home);
        _box.enabled = true;
        ApplyColors(0f, 1f);
        SetBar(1f);
        Restored?.Invoke();
        GameAudio.Event("shield_restore");
    }

    /// <summary>最初の状態に戻す（テストシーンのやり直し用）</summary>
    public void ResetWindow()
    {
        EndDrag();
        Restore();
    }

    void Teleport(Vector2 position)
    {
        _rb.position = position;
        transform.position = position;
    }

    /// <summary>絵の色を決める。flash：光らせる量（0〜1）、alpha：元の濃さに掛ける値</summary>
    void ApplyColors(float flash, float alpha)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = Color.Lerp(_baseColors[i], _hitFlashColor, flash * 0.7f);
            c.a = _baseColors[i].a * alpha;
            _renderers[i].color = c;
        }
    }

    /// <summary>耐久の棒の長さ（0〜1）。左端は動かさない</summary>
    void SetBar(float ratio)
    {
        if (_durabilityBar == null) return;
        ratio = Mathf.Clamp01(ratio);
        Transform t = _durabilityBar.transform;
        float spriteWidth = _durabilityBar.sprite != null ? _durabilityBar.sprite.bounds.size.x : 1f;
        t.localScale = new Vector3(_barScale.x * ratio, _barScale.y, _barScale.z);
        t.localPosition = _barLocalPos - new Vector3(_barScale.x * (1f - ratio) * spriteWidth * 0.5f, 0f, 0f);
    }
}
