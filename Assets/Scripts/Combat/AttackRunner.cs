using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AttackData を実行する部分。プレイヤー・ボス・雑魚で共通に使う（攻撃する人に1つ付ける）。
/// ・Run(データ) で、予備動作 → 攻撃（弾を撃つ・判定を出す・落とす・瞬間移動）の順に進める
/// ・出した弾・判定・落下物は覚えておき、ClearSpawned でまとめて消せる（形態変化・正常化のとき）
/// ・同時に実行できる攻撃は1つ。実行中に Run しても false を返して何もしない（「同時に実行できる」をオンにしたときを除く）
///
/// 時間はすべて Time.time（スケールあり）で数える。各段階でイベントを出すので、
/// アニメーション・SE・テストはそれを見ればよい。
/// </summary>
public class AttackRunner : MonoBehaviour
{
    [Header("陣営")]
    [SerializeField] Team _team = Team.Enemy;
    [Tooltip("当てる相手のレイヤー（プレイヤーの攻撃なら Enemy、敵の攻撃なら Player）")]
    [SerializeField] LayerMask _targetLayers;

    [Header("地形")]
    [Tooltip("弾が当たって止まる地形（床・壁）。すり抜け床は含めない")]
    [SerializeField] LayerMask _solidMask;
    [Tooltip("上に乗れる地形（床・すり抜け床）。予告や柱を置く床、落下物が着地する床")]
    [SerializeField] LayerMask _groundMask;

    [Header("位置")]
    [Tooltip("弾を撃つ位置。空ならこの GameObject の位置")]
    [SerializeField] Transform _origin;
    [Tooltip("狙う相手（ボスならプレイヤー）。空のときは、狙う技も正面に撃つ")]
    [SerializeField] Transform _target;

    [Header("実行のしかた")]
    [Tooltip("オン：実行中でも別の攻撃を始められる（プレイヤーの能力用。ブレークポイントの予告中に新しいタブを使える）。\nオフ：同時に1つだけ（ボス用）")]
    [SerializeField] bool _allowConcurrent;

    readonly List<GameObject> _spawned = new List<GameObject>();
    PlayerController _controller; // あれば向きをここから読む
    readonly List<Coroutine> _routines = new List<Coroutine>();
    int _facing = 1;

    public Team Team => _team;
    public LayerMask TargetLayers => _targetLayers;
    public LayerMask SolidMask => _solidMask;
    public LayerMask GroundMask => _groundMask;
    /// <summary>狙う相手</summary>
    public Transform Target { get => _target; set => _target = value; }
    /// <summary>向き（右 1／左 -1）。PlayerController があればそちらの向きを使う</summary>
    public int Facing
    {
        get => _controller != null ? _controller.FacingDirection : _facing;
        set => _facing = value >= 0 ? 1 : -1;
    }
    public bool IsRunning => _routines.Count > 0;
    /// <summary>実行中の攻撃。無ければ null</summary>
    public AttackData Current { get; private set; }

    /// <summary>攻撃を始めたとき（予備動作の開始）</summary>
    public event Action<AttackData> Started;
    /// <summary>攻撃が出た瞬間（弾を撃つ・予告を出す・落とす・消え始める）。繰り返す攻撃では、そのたびに呼ばれる</summary>
    public event Action<AttackData> Released;
    /// <summary>攻撃が終わって、次の行動に移れるようになったとき。Cancel したときは呼ばれない</summary>
    public event Action<AttackData> Finished;
    /// <summary>弾・判定・落下物を出したとき（テスト・エフェクト用）</summary>
    public event Action<GameObject> Spawned;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
    }

    void OnDisable() => Cancel();

    /// <summary>攻撃を始める。実行中なら false</summary>
    public bool Run(AttackData data)
    {
        if (data == null || !isActiveAndEnabled) return false;
        if (IsRunning && !_allowConcurrent) return false;
        if (data.Kind == AttackKind.Melee)
        {
            Debug.LogWarning($"{name}: 近接（{data.name}）は AttackRunner では実行しません。PlayerDebugAttack に設定してください", this);
            return false;
        }
        Current = data;
        var holder = new Coroutine[1];
        holder[0] = StartCoroutine(RunRoutine(data, holder));
        if (holder[0] != null && !_finishedImmediately) _routines.Add(holder[0]);
        _finishedImmediately = false;
        return true;
    }

    bool _finishedImmediately; // 1フレームも待たずに終わった攻撃（一瞬の Warp など）は、実行中の一覧に入れない

    /// <summary>実行中の攻撃を止める（出した弾などは残る。消すときは ClearSpawned）</summary>
    public void Cancel()
    {
        // 攻撃の中の段階（連射・瞬間移動のフェードなど）も入れ子のコルーチンなので、まとめて止める。
        // このコンポーネントが動かすコルーチンは攻撃だけ（弾・判定・落下物は自分の GameObject で動く）
        StopAllCoroutines();
        _routines.Clear();
        Current = null;
    }

    /// <summary>この人が出した弾・判定・落下物・予告をすべて消す（正常化のとき）</summary>
    public void ClearSpawned()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Destroy(go);
        }
        _spawned.Clear();
    }

    /// <summary>この人が出した弾だけを消す（形態変化のとき）。落下物・判定・ダメージ床は残る</summary>
    public void ClearProjectiles()
    {
        foreach (var go in _spawned)
        {
            if (go != null && go.TryGetComponent(out Projectile _)) Destroy(go);
        }
        _spawned.RemoveAll(g => g == null || g.TryGetComponent(out Projectile _));
    }

    /// <summary>この人が出した物のうち、指定した部品が付いている物だけを消す（瞬間移動の予告など）</summary>
    public void ClearSpawnedWith<T>() where T : Component
    {
        foreach (var go in _spawned)
        {
            if (go != null && go.TryGetComponent(out T _)) Destroy(go);
        }
        _spawned.RemoveAll(g => g == null || g.TryGetComponent(out T _));
    }

    /// <summary>出した物として登録する（ClearSpawned で消えるようになる）</summary>
    public void Register(GameObject go)
    {
        _spawned.RemoveAll(g => g == null);
        _spawned.Add(go);
        Spawned?.Invoke(go);
    }

    /// <summary>今出ている物の数（テスト用）</summary>
    public int SpawnedCount
    {
        get
        {
            _spawned.RemoveAll(g => g == null);
            return _spawned.Count;
        }
    }

    /// <summary>弾を撃つ位置</summary>
    public Vector2 OriginPoint => _origin != null ? (Vector2)_origin.position : (Vector2)transform.position;

    /// <summary>狙う相手の体の中心。相手がいなければ null</summary>
    public Vector2? TargetPoint
    {
        get
        {
            if (_target == null || !_target.gameObject.activeInHierarchy) return null;
            // 移動用の当たり判定（トリガーでないコライダー）の中心を狙う。足元が原点のプレイヤーでも体の真ん中に飛ぶ
            foreach (var col in _target.GetComponents<Collider2D>())
            {
                if (!col.isTrigger && col.enabled) return col.bounds.center;
            }
            return _target.position;
        }
    }

    IEnumerator RunRoutine(AttackData data, Coroutine[] self)
    {
        Started?.Invoke(data);
        if (data.Windup > 0f) yield return new WaitForSeconds(data.Windup);

        switch (data.Kind)
        {
            case AttackKind.Projectile:
                yield return ProjectileRoutine(data);
                break;
            case AttackKind.Area:
                yield return AreaRoutine(data);
                break;
            case AttackKind.Spawn:
                yield return SpawnRoutine(data);
                break;
            case AttackKind.Warp:
                yield return WarpRoutine(data);
                break;
            default:
                Debug.LogWarning($"{name}: 攻撃の種類 {data.Kind} はまだ実行できません（{data.name}）", this);
                break;
        }

        if (self[0] != null) _routines.Remove(self[0]);
        else _finishedImmediately = true; // StartCoroutine から戻る前に終わった
        if (_routines.Count == 0) Current = null;
        Finished?.Invoke(data);
    }

    // ───────── 弾（A） ─────────

    IEnumerator ProjectileRoutine(AttackData data)
    {
        for (int r = 0; r < data.RepeatCount; r++)
        {
            if (r > 0 && data.RepeatInterval > 0f) yield return new WaitForSeconds(data.RepeatInterval);
            FireVolley(data);
        }
    }

    /// <summary>1回分（扇状にまとめて）撃つ。連射のたびに狙い直す</summary>
    void FireVolley(AttackData data)
    {
        Vector2 origin = OriginPoint;
        Vector2 baseDir = new Vector2(Facing, 0f);
        if (data.Aim == AimMode.Target && TargetPoint is Vector2 target)
        {
            Vector2 d = target - origin;
            if (d.sqrMagnitude > 0.0001f) baseDir = d.normalized;
        }

        int n = data.ProjectileCount;
        for (int i = 0; i < n; i++)
        {
            float angle = n > 1 ? -data.SpreadAngle * 0.5f + data.SpreadAngle * i / (n - 1) : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            Projectile.Spawn(this, data, origin, dir);
        }
        Released?.Invoke(data);
    }

    // ───────── 指定位置の判定（E）・残り続けるダメージ床（H） ─────────

    IEnumerator AreaRoutine(AttackData data)
    {
        Vector2 anchor = data.Anchor switch
        {
            AreaAnchor.TargetPosition => TargetPoint ?? OriginPoint,
            AreaAnchor.Forward => OriginPoint + new Vector2(Facing * data.ForwardDistance, 0f),
            _ => OriginPoint,
        };

        // 1つ目は狙った位置、2つ目は左、3つ目は右、4つ目はさらに左…の順に、ずらす時間をあけて出す
        // 出したあとは判定が自分で進む（攻撃を Cancel しても、出した分は最後まで残る）
        for (int i = 0; i < data.AreaCount; i++)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            Vector2 point = anchor + new Vector2(sign * step * data.AreaSpacing, 0f);
            AreaStrike.Spawn(this, data, point, Vector2.up, i * data.AreaStagger);
        }
        Released?.Invoke(data);

        float total = (data.AreaCount - 1) * data.AreaStagger + data.TelegraphTime + data.HitWindow;
        yield return new WaitForSeconds(total);
    }

    /// <summary>弾が地形に当たった位置に、追撃の攻撃を出す（赤い波線など）。normal は当たった面の向き</summary>
    public void SpawnFollowUp(AttackData data, Vector2 point, Vector2 normal)
    {
        if (data == null) return;
        switch (data.Kind)
        {
            case AttackKind.Area:
                AreaStrike.Spawn(this, data, point, normal, 0f);
                break;
            default:
                Debug.LogWarning($"{name}: 追撃に使えるのは Area だけです（{data.name}）", this);
                break;
        }
    }

    // ───────── 落下物（F）・設置物（G） ─────────

    IEnumerator SpawnRoutine(AttackData data)
    {
        var objects = new List<FallingObject>();
        Rect arena = BattleArena.GetBounds();
        float half = data.ObjectSize.x * 0.5f;
        float minX = arena.xMin + half, maxX = arena.xMax - half;
        float hangY = arena.yMax + data.ObjectSize.y * 0.5f; // 画面の上端のすぐ外から落とす
        float now = Time.time;

        if (data.Placement == SpawnPlacement.AroundTarget)
        {
            // 影（予告）を出してから、画面の上から落とす。影は落ち終わるまで残す
            float targetX = Mathf.Clamp((TargetPoint ?? OriginPoint).x, minX, maxX);
            var xs = new List<float> { targetX };
            for (int i = 1; i < data.SpawnCount; i++) xs.Add(PickRandomX(targetX, xs, data, minX, maxX));
            foreach (float x in xs)
            {
                float fallAt = now + data.TelegraphTime;
                objects.Add(FallingObject.Spawn(this, data, new Vector2(x, hangY), fallAt, fallAt, true));
            }
            Released?.Invoke(data);
        }
        else
        {
            // 画面の上端に、左から一定間隔で並べる → 並べ終えて少し待つ → 左から順に落とす
            int n = data.SpawnCount;
            float width = (n - 1) * data.PlaceSpacing;
            float startX = arena.center.x - width * 0.5f;
            float hangInside = arena.yMax - data.ObjectSize.y * 0.5f - 0.2f; // 画面の中に見えるように並べる
            float lastPlace = now + (n - 1) * data.PlaceInterval;
            for (int i = 0; i < n; i++)
            {
                float appearAt = now + i * data.PlaceInterval;
                float fallAt = lastPlace + data.DropHoldTime + i * data.DropInterval;
                objects.Add(FallingObject.Spawn(this, data, new Vector2(startX + i * data.PlaceSpacing, hangInside), appearAt, fallAt, false));
            }
            Released?.Invoke(data);
        }

        // 全部が着地する（または消える）まで待つ
        float timeout = Time.time + 30f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }

    /// <summary>狙う相手から一定以上離れ、ほかの落下物ともなるべく重ならない x を選ぶ</summary>
    float PickRandomX(float targetX, List<float> taken, AttackData data, float minX, float maxX)
    {
        float best = UnityEngine.Random.Range(minX, maxX);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            if (Mathf.Abs(x - targetX) < data.MinDistanceFromTarget) continue;
            best = x;
            bool overlaps = false;
            foreach (float t in taken)
            {
                if (Mathf.Abs(x - t) < data.ObjectSize.x) { overlaps = true; break; }
            }
            if (!overlaps) return x;
        }
        // 重ならない場所が見つからなければ、離れていることだけを守る
        if (Mathf.Abs(best - targetX) < data.MinDistanceFromTarget)
        {
            float left = targetX - data.MinDistanceFromTarget, right = targetX + data.MinDistanceFromTarget;
            best = left >= minX ? left : Mathf.Min(right, maxX);
        }
        return best;
    }

    // ───────── Warp ─────────

    IEnumerator WarpRoutine(AttackData data)
    {
        var warpable = GetComponent<IWarpable>();
        var body = GetComponent<KinematicBody2D>();
        Vector2 from = transform.position;
        Vector2 to = ComputeWarpDestination(data, from, body);
        Released?.Invoke(data);

        bool hideContact = data.DisableContactUntilAppeared && warpable != null;
        if (hideContact) warpable.SetContactEnabled(false);

        // 現れる場所の予告（現れ始める appearTelegraph 秒前から、現れきるまで）
        float appearStart = data.VanishTime + data.AppearDelay;
        if (data.AppearTelegraph > 0f && warpable != null)
        {
            WarpTelegraph.Spawn(this, warpable.BodyBounds, to - from,
                Mathf.Max(0f, appearStart - data.AppearTelegraph), appearStart + data.AppearTime);
        }

        yield return Fade(warpable, 1f, 0f, data.VanishTime);
        if (data.AppearDelay > 0f) yield return new WaitForSeconds(data.AppearDelay);

        if (warpable != null) warpable.Teleport(to);
        else if (body != null) body.Warp(to);
        else transform.position = to;

        yield return Fade(warpable, 0f, 1f, data.AppearTime);
        if (hideContact) warpable.SetContactEnabled(true);
    }

    IEnumerator Fade(IWarpable warpable, float from, float to, float duration)
    {
        if (warpable == null) yield break;
        if (duration <= 0f)
        {
            warpable.SetVisibility(to);
            yield break;
        }
        float start = Time.time;
        while (Time.time - start < duration)
        {
            warpable.SetVisibility(Mathf.Lerp(from, to, (Time.time - start) / duration));
            yield return null;
        }
        warpable.SetVisibility(to);
    }

    /// <summary>瞬間移動の行き先</summary>
    public Vector2 ComputeWarpDestination(AttackData data, Vector2 from, KinematicBody2D body)
    {
        Rect arena = BattleArena.GetBounds();
        if (data.WarpDestination == WarpDestination.Opposite)
        {
            return new Vector2(arena.center.x * 2f - from.x, from.y); // 部屋の真ん中で左右反転した位置
        }

        float dir = Facing;
        float distance = data.WarpDistance;
        if (body != null)
        {
            // 体の形のまま前へ動かしてみて、壁や床にぶつかるなら、その手前で止める
            var box = body.GetComponent<BoxCollider2D>();
            Vector2 scale = body.transform.lossyScale;
            Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) - Vector2.one * 0.04f;
            Vector2 center = from + Vector2.Scale(box.offset, scale);
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(_solidMask);
            var hits = new RaycastHit2D[4];
            int count = Physics2D.BoxCast(center, size, 0f, new Vector2(dir, 0f), filter, hits, distance);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.attachedRigidbody != null && hits[i].collider.attachedRigidbody.gameObject == body.gameObject) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.02f));
            }
        }
        return from + new Vector2(dir * distance, 0f);
    }

    // ───────── 共通の道具 ─────────

    /// <summary>x の位置で、fromY から真下にある床の上面を探す</summary>
    public bool TryFindGround(float x, float fromY, out float groundY)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_groundMask);
        var hits = new RaycastHit2D[4];
        int count = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, filter, hits, 100f);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance <= 0f) continue; // 始点が地形の中
            groundY = hits[i].point.y;
            return true;
        }
        groundY = BattleArena.GetBounds().yMin;
        return false;
    }

    /// <summary>実行時に作る物（弾・判定など）の土台。Register 済みで返す</summary>
    public GameObject CreateSpawnedObject(string objectName, Vector2 position)
    {
        var go = new GameObject($"{objectName}");
        go.layer = gameObject.layer; // 持ち主と同じレイヤー（地形のレイヤーにしない）
        go.transform.position = position;
        Register(go);
        return go;
    }

    /// <summary>当たり判定（Hitbox）を付ける。コライダーは呼んだ側で先に付けておく</summary>
    public Hitbox AddHitbox(GameObject go, AttackData data, Collider2D area, bool hitOncePerActivation = true)
    {
        var hitbox = go.AddComponent<Hitbox>();
        hitbox.SetArea(area);
        hitbox.SetOwner(_team, _targetLayers, hitOncePerActivation);
        hitbox.Setup(data);
        hitbox.Deactivate();
        return hitbox;
    }

    /// <summary>仮の絵（子の SpriteRenderer）を付ける</summary>
    public static SpriteRenderer AddVisual(Transform parent, Sprite sprite, Color color, Vector2 size, int sortingOrder = 20, string objectName = "Visual")
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : PlaceholderSprites.Square;
        Vector2 native = sr.sprite.bounds.size; // 絵の元の大きさに関係なく、指定した大きさに合わせる
        go.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
