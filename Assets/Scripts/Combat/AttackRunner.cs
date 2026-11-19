using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AttackData を実行する部分。プレイヤー・ボス・雑魚で共通に使う（攻撃する人に1つ付ける）。
/// ・Run(データ) で、予備動作 → 攻撃（弾を撃つ・判定を出す・落とす・瞬間移動・ロックオン）の順に進める
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
    [Tooltip("【仮】弾だけを止める物（盾ウィンドウ。レイヤー BulletBlocker）。敵の AttackRunner にだけ入れる。\n" +
             "止めた弾は追撃（赤い波線など）を出さずに消える。瞬間移動の壁止めには使わない（地形は上の「弾が当たって止まる地形」）")]
    [SerializeField] LayerMask _projectileBlockerMask;

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
    /// <summary>【仮】弾だけを止める物（盾ウィンドウ）のレイヤー。弾の判定にだけ使う</summary>
    public LayerMask ProjectileBlockerMask => _projectileBlockerMask;
    /// <summary>狙う相手</summary>
    public Transform Target { get => _target; set => _target = value; }
    /// <summary>向き（右 1／左 -1）。PlayerController があればそちらの向きを使う</summary>
    public int Facing
    {
        get => _controller != null ? _controller.FacingDirection : _facing;
        set => _facing = value >= 0 ? 1 : -1;
    }
    public bool IsRunning => _routines.Count > 0;

    // ───────── スキン（2026-11-18：スキルの見た目。docs/app-skill-visuals-design.md 1-1） ─────────
    /// <summary>この人が出す攻撃のスキン（どのアプリの見た目か）。ボスはステージのアプリ、プレイヤーは使った能力のアプリ</summary>
    public SkinId Skin { get; set; } = SkinId.None;
    /// <summary>スキンの絵に使うアプリの色（無ければ null）</summary>
    public AppTheme SkinTheme { get; set; }
    /// <summary>スキンの絵に使うアプリのアイコン（無ければ null）</summary>
    public Sprite SkinIcon { get; set; }
    /// <summary>実行中の攻撃。無ければ null</summary>
    public AttackData Current { get; private set; }

    /// <summary>攻撃を始めたとき（予備動作の開始）</summary>
    public event Action<AttackData> Started;
    /// <summary>攻撃が出た瞬間（弾を撃つ・予告を出す・落とす・消え始める）。繰り返す攻撃では、そのたびに呼ばれる</summary>
    public event Action<AttackData> Released;
    /// <summary>どれかの AttackRunner で Started・Released が出たとき（技の音。2026-11-19）。出す所は Started・Released と同じ</summary>
    public static event Action<AttackRunner, AttackData> AnyStarted, AnyReleased;
    /// <summary>どれかの AttackRunner が追撃（弾が当たった所の攻撃。ボムの爆発など）を出したとき（音だけに使う。2026-11-19）</summary>
    public static event Action<AttackRunner, AttackData> AnyFollowUp;
    /// <summary>攻撃が終わって、次の行動に移れるようになったとき。Cancel したときは呼ばれない</summary>
    public event Action<AttackData> Finished;
    /// <summary>弾・判定・落下物を出したとき（テスト・エフェクト用）</summary>
    public event Action<GameObject> Spawned;
    /// <summary>瞬間移動した瞬間（攻撃データ, 元の位置, 移った先）。エフェクト用</summary>
    public event Action<AttackData, Vector2, Vector2> Warped;

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
            Debug.LogWarning($"{name}: 近接（{data.name}）は AttackRunner では実行しません。PlayerMeleeAttack に設定してください", this);
            return false;
        }
        // 【仮】ロックオンは、画面に相手がいなければ使えない（false を返すので、能力のクールタイムも始まらない）
        if (data.Kind == AttackKind.LockOn && LockOnStrike.FindTargets(this).Count == 0) return false;
        // 専用の技は、使えるかどうかを技の中身に聞く（使えなければ false。能力ならクールタイムも始まらない）
        if (data.Kind == AttackKind.Custom && (data.Custom == null || !data.Custom.CanRun(this, data))) return false;
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
        EndWindupDress(); // 予備動作の絵（スキン）も消す
        EndWarpDress();
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
        // スキンが足した飾り（Target の子でない物。SkinDresser.Adopt）も、形態変化で消す（2026-11-18：スキルの見た目 14-5）
        foreach (var go in _spawned)
        {
            if (go != null && (go.TryGetComponent(out Projectile _) || go.TryGetComponent(out SkinFxMarker _))) Destroy(go);
        }
        _spawned.RemoveAll(g => g == null || g.TryGetComponent(out Projectile _) || g.TryGetComponent(out SkinFxMarker _));
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
        AnyStarted?.Invoke(this, data);
        if (data.Windup > 0f)
        {
            BeginWindupDress(data);
            yield return new WaitForSeconds(data.Windup);
            EndWindupDress();
        }

        yield return Execute(data);

        if (self[0] != null) _routines.Remove(self[0]);
        else _finishedImmediately = true; // StartCoroutine から戻る前に終わった
        if (_routines.Count == 0) Current = null;
        Finished?.Invoke(data);
    }

    /// <summary>
    /// 攻撃の本体だけを進める（予備動作・開始と終わりのイベントなし）。専用の技が、中で別の攻撃データを出すときに使う
    /// （例：エフェクトアイコン投げが、選んだアイコンの投てきを出す）。呼んだコルーチンの中で yield return する
    /// </summary>
    public IEnumerator Execute(AttackData data)
    {
        if (data == null) yield break;
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
            case AttackKind.Summon:
                yield return SummonRoutine(data);
                break;
            case AttackKind.Beam:
                yield return BeamRoutine(data);
                break;
            case AttackKind.Chat:
                yield return ChatRoutine(data);
                break;
            case AttackKind.Alarm:
                yield return AlarmRoutine(data);
                break;
            case AttackKind.Brush:
                yield return BrushRoutine(data);
                break;
            case AttackKind.Filter:
                yield return FilterRoutine(data);
                break;
            case AttackKind.Packet:
                yield return PacketRoutine(data);
                break;
            case AttackKind.BombSite:
                yield return BombSiteRoutine(data);
                break;
            case AttackKind.LockOn:
                yield return LockOnRoutine(data);
                break;
            case AttackKind.PhysicsObject:
                yield return PhysicsObjectRoutine(data);
                break;
            case AttackKind.Custom:
                if (data.Custom != null) yield return data.Custom.Run(this, data);
                break;
            default:
                Debug.LogWarning($"{name}: 攻撃の種類 {data.Kind} はまだ実行できません（{data.name}）", this);
                break;
        }
    }

    /// <summary>攻撃が出た瞬間を知らせる（専用の技が、弾を撃つ・予告を出すなどの瞬間に呼ぶ）</summary>
    public void NotifyReleased(AttackData data) => RaiseReleased(data);

    // 攻撃が出た瞬間の知らせ（この人の Released と、どれでも聞ける AnyReleased）
    void RaiseReleased(AttackData data)
    {
        Released?.Invoke(data);
        AnyReleased?.Invoke(this, data);
    }

    // ───────── 弾（A） ─────────

    IEnumerator ProjectileRoutine(AttackData data)
    {
        for (int r = 0; r < data.RepeatCount; r++)
        {
            if (r > 0 && data.RepeatInterval > 0f) yield return new WaitForSeconds(data.RepeatInterval);
            FireVolley(data, r);
        }
    }

    /// <summary>1回分（扇状にまとめて）撃つ。連射のたびに狙い直す。repeat：何回目の連射か（スキンの絵の番号に使う）</summary>
    void FireVolley(AttackData data, int repeat = 0)
    {
        Vector2 origin = OriginPoint + new Vector2(Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        Vector2 baseDir = new Vector2(Facing, 0f);
        if (data.Aim == AimMode.Forward && data.LaunchAngle > 0f)
        {
            // 正面の上へ傾けて投げる（放物線）
            float rad = data.LaunchAngle * Mathf.Deg2Rad;
            baseDir = new Vector2(Facing * Mathf.Cos(rad), Mathf.Sin(rad));
        }
        if (data.Aim == AimMode.Target && TargetPoint is Vector2 target)
        {
            Vector2 d = target - origin;
            if (d.sqrMagnitude > 0.0001f) baseDir = d.normalized;
        }

        int n = data.ProjectileCount;
        bool fullCircle = data.SpreadAngle >= 359.9f; // 全方位：端どうしが重ならないように、数で等分する
        for (int i = 0; i < n; i++)
        {
            float angle = n <= 1 ? 0f
                : fullCircle ? 360f * i / n
                : -data.SpreadAngle * 0.5f + data.SpreadAngle * i / (n - 1);
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            Projectile.Spawn(this, data, origin, dir, repeat * n + i);
        }
        RaiseReleased(data);
    }

    // ───────── 指定位置の判定（E）・残り続けるダメージ床（H） ─────────

    IEnumerator AreaRoutine(AttackData data)
    {
        // 出す位置のずれ（弾を出す位置のずれと同じ欄。x は向きに合わせて反転）。今までの攻撃データは 0 なので変わらない
        Vector2 offset = new Vector2(Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        Vector2 anchor = data.Anchor switch
        {
            AreaAnchor.TargetPosition => TargetPoint ?? OriginPoint,
            AreaAnchor.Forward => OriginPoint + new Vector2(Facing * data.ForwardDistance, 0f) + offset,
            AreaAnchor.ForwardAtTargetHeight => new Vector2(OriginPoint.x + Facing * data.ForwardDistance + offset.x,
                (TargetPoint ?? OriginPoint).y + data.SpawnOffset.y),
            _ => OriginPoint,
        };

        // 1つ目は狙った位置、2つ目は左、3つ目は右、4つ目はさらに左…の順に、ずらす時間をあけて出す
        // 出したあとは判定が自分で進む（攻撃を Cancel しても、出した分は最後まで残る）
        for (int i = 0; i < data.AreaCount; i++)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            Vector2 point = data.AreaForwardOrder
                ? anchor + new Vector2(Facing * i * data.AreaSpacing, 0f) // 前へ順に並べる
                : anchor + new Vector2(sign * step * data.AreaSpacing, 0f);
            AreaStrike.Spawn(this, data, point, Vector2.up, i * data.AreaStagger, i);
        }
        RaiseReleased(data);

        float total = (data.AreaCount - 1) * data.AreaStagger + data.TelegraphTime + data.HitDelay + data.HitWindow;
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
                AnyFollowUp?.Invoke(this, data);
                break;
            default:
                Debug.LogWarning($"{name}: 追撃に使えるのは Area だけです（{data.name}）", this);
                break;
        }
    }

    // ───────── ロックオン（2026-11-18：ギャラリービュー） ─────────

    IEnumerator LockOnRoutine(AttackData data)
    {
        // 予備動作の間に相手がいなくなっていたら、何もしない
        LockOnStrike strike = LockOnStrike.Spawn(this, data, LockOnStrike.FindTargets(this));
        if (strike == null) yield break;
        RaiseReleased(data);
        yield return new WaitForSeconds(strike.Duration);
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

        if (data.Placement == SpawnPlacement.AppearAroundTarget)
        {
            // 落とさない：出る場所に薄い枠（予告）を出し、予告時間のあとにその場へパッと出す
            float appearAt = now + data.TelegraphTime;
            int index = 0;
            foreach (Vector2 c in PickAppearPositions(data))
            {
                objects.Add(FallingObject.SpawnInPlace(this, data, c, appearAt, index++));
            }
            RaiseReleased(data);
        }
        else if (data.Placement == SpawnPlacement.AroundTarget)
        {
            // 影（予告）を出してから、画面の上から落とす。影は落ち終わるまで残す
            float targetX = Mathf.Clamp((TargetPoint ?? OriginPoint).x, minX, maxX);
            var xs = new List<float> { targetX };
            for (int i = 1; i < data.SpawnCount; i++) xs.Add(PickRandomX(targetX, xs, data, minX, maxX));
            for (int i = 0; i < xs.Count; i++)
            {
                float fallAt = now + data.TelegraphTime;
                objects.Add(FallingObject.Spawn(this, data, new Vector2(xs[i], hangY), fallAt, fallAt, true, false, i));
            }
            RaiseReleased(data);
        }
        else
        {
            // 画面の上端に、左から一定間隔で並べる → 並べ終えて少し待つ → 左から順に落とす
            // 【仮】並んでから落ち始めるまでの間は、クリックで消せる（DismissableTab。時間・間隔は変えない）
            int n = data.SpawnCount;
            float width = (n - 1) * data.PlaceSpacing;
            float startX = arena.center.x - width * 0.5f;
            float hangInside = arena.yMax - data.ObjectSize.y * 0.5f - 0.2f; // 画面の中に見えるように並べる
            float lastPlace = now + (n - 1) * data.PlaceInterval;
            for (int i = 0; i < n; i++)
            {
                float appearAt = now + i * data.PlaceInterval;
                float fallAt = lastPlace + data.DropHoldTime + i * data.DropInterval;
                objects.Add(FallingObject.Spawn(this, data, new Vector2(startX + i * data.PlaceSpacing, hangInside), appearAt, fallAt, false, dismissable: true, index: i));
            }
            RaiseReleased(data);
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

    // ───────── 物理オブジェクト（Unity の Cube・Sphere・Capsule） ─────────

    readonly Dictionary<AttackData, int> _primitiveTurn = new Dictionary<AttackData, int>();
    readonly Dictionary<AttackData, BoundedSpawnList<PhysicsPrimitive>> _alivePrimitives = new Dictionary<AttackData, BoundedSpawnList<PhysicsPrimitive>>();

    /// <summary>次に出す物理オブジェクトの番号（Cube → Sphere → Capsule の順）。進めない</summary>
    public int PeekPrimitiveIndex(AttackData data)
    {
        int n = data.Primitives != null ? data.Primitives.Length : 0;
        if (n == 0) return 0;
        _primitiveTurn.TryGetValue(data, out int turn);
        return turn % n;
    }

    /// <summary>この攻撃データで出して、まだ残っている物理オブジェクトの数（テスト用）</summary>
    public int AlivePrimitiveCount(AttackData data)
    {
        if (!_alivePrimitives.TryGetValue(data, out var list)) return 0;
        list.Prune(p => p == null || p.IsGone);
        return list.Count;
    }

    IEnumerator PhysicsObjectRoutine(AttackData data)
    {
        if (data.Primitives == null || data.Primitives.Length == 0)
        {
            Debug.LogWarning($"{name}: 物理オブジェクトの「出す物」が空です（{data.name}）", this);
            yield break;
        }
        if (!_alivePrimitives.TryGetValue(data, out var alive))
        {
            alive = new BoundedSpawnList<PhysicsPrimitive>(data.MaxAlive);
            _alivePrimitives[data] = alive;
        }
        alive.Capacity = data.MaxAlive;

        foreach (Vector2 pos in PickPrimitivePositions(data))
        {
            _primitiveTurn.TryGetValue(data, out int turn);
            _primitiveTurn[data] = turn + 1;
            var obj = PhysicsPrimitive.Spawn(this, data, pos, turn % data.Primitives.Length);
            alive.Prune(p => p == null || p.IsGone);
            foreach (var old in alive.Add(obj)) if (old != null) old.Despawn(); // 上限を超えたら古い物から消す
        }
        Released?.Invoke(data);

        // 本体が出るまで待つ（ボスはそのあと次の行動へ）
        float wait = (data.HasSpawnWarning ? data.TelegraphTime : 0f) + data.SpawnReleaseDelay;
        if (wait > 0f) yield return new WaitForSeconds(wait);
    }

    /// <summary>
    /// 物理オブジェクトを出す位置（中心）。
    /// ・ForwardAbove：前方やや上（弾を出す位置のずれ）
    /// ・AroundTarget：1つ目は狙う相手の頭上、残りは部屋の中のランダムな位置（相手から一定以上離す）
    /// </summary>
    List<Vector2> PickPrimitivePositions(AttackData data)
    {
        var result = new List<Vector2>();
        if (data.Placement == SpawnPlacement.AroundTarget)
        {
            Rect arena = BattleArena.GetBounds();
            float margin = 0.8f;
            float topY = arena.yMax - margin;
            Vector2 target = TargetPoint ?? OriginPoint;
            result.Add(new Vector2(Mathf.Clamp(target.x, arena.xMin + margin, arena.xMax - margin), Mathf.Min(target.y + data.DropHeight, topY)));
            for (int i = 1; i < data.SpawnCount; i++)
            {
                Vector2 best = result[0];
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var c = new Vector2(UnityEngine.Random.Range(arena.xMin + margin, arena.xMax - margin),
                        UnityEngine.Random.Range(Mathf.Min(arena.yMin + 3f, topY), topY));
                    if (Mathf.Abs(c.x - target.x) < data.MinDistanceFromTarget) continue;
                    bool near = false;
                    foreach (Vector2 t in result) if (Vector2.Distance(t, c) < 1.6f) { near = true; break; }
                    best = c;
                    if (!near) break;
                }
                result.Add(best);
            }
            return result;
        }

        Vector2 basePos = OriginPoint + new Vector2(Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        for (int i = 0; i < data.SpawnCount; i++) result.Add(basePos + new Vector2(Facing * i * 1.2f, 0f));
        return result;
    }

    // ───────── 予備動作の絵 ─────────

    /// <summary>予備動作の間に出す絵（円グラフ）。弾を出す位置に出し、最後まで再生して消える。出した物として登録する</summary>
    void PlayWindupEffect(AttackData data)
    {
        Sprite[] frames = data.WindupFrames;
        if (frames == null || frames.Length == 0 || frames[0] == null) return;
        Vector2 pos = OriginPoint + new Vector2(Facing * data.SpawnOffset.x, data.SpawnOffset.y);
        SpriteRenderer sr = Vfx.Play(frames, pos, data.WindupFrameRate, 19, null, Facing < 0);
        if (sr != null) Register(sr.gameObject);
    }

    // ───────── 出現型の置き方（ポップアップ出現） ─────────

    /// <summary>【仮】上の端に乗れるかを調べるとき、横にどこまでの床・足場を見るか（マス）。ジャンプの頂点までに横へ進める距離くらい</summary>
    const float ReachHorizontal = 2.5f;

    /// <summary>
    /// 出現型の位置（中心）を決める。
    /// ・1つ目：狙う相手の体に重なる位置。床にめり込むなら床の上まで上げる。ボスの体に重なるなら、相手に重なったまま横へずらす
    /// ・残り：狙う相手（体の中心）から一定以上離れた空中のランダムな位置。
    ///   高さは「上の端の高さの範囲」の中で、近くの床・足場からジャンプで上の端に乗れる所。地形・ボスの体には重ねない。
    ///   ほかの出現物とは、なるべく重ねない（場所が無ければ、重なりが一番少ない所）
    /// </summary>
    List<Vector2> PickAppearPositions(AttackData data)
    {
        var result = new List<Vector2>();
        Rect arena = BattleArena.GetBounds();
        Vector2 size = data.ObjectSize;
        Vector2 half = size * 0.5f;
        Bounds? boss = OwnBodyBounds();
        Vector2 target = TargetPoint ?? OriginPoint;
        float targetHalfWidth = TargetBodyBounds()?.extents.x ?? 0.35f;

        // 1つ目：相手の体に重ねる
        Vector2 first = target;
        first.x = Mathf.Clamp(first.x, arena.xMin + half.x, arena.xMax - half.x);
        TryFindGround(target.x, target.y, out float groundY);
        if (first.y - half.y < groundY) first.y = groundY + half.y; // 床にめり込むなら、床の上まで上げる
        first.y = Mathf.Min(first.y, arena.yMax - half.y);           // 天井（画面の上端）は越えない
        if (boss is Bounds b && Overlaps(first, size, b, 0f))
        {
            float dir = first.x >= b.center.x ? 1f : -1f;
            float need = dir > 0f ? b.max.x - (first.x - half.x) : (first.x + half.x) - b.min.x;
            float maxShift = half.x + targetHalfWidth - 0.05f; // ここまでなら相手の体に重なったまま
            first.x = Mathf.Clamp(first.x + dir * Mathf.Min(need + 0.05f, maxShift), arena.xMin + half.x, arena.xMax - half.x);
        }
        result.Add(first);

        for (int i = 1; i < data.SpawnCount; i++) result.Add(PickAppearRandom(data, arena, target, boss, result));
        return result;
    }

    Vector2 PickAppearRandom(AttackData data, Rect arena, Vector2 target, Bounds? boss, List<Vector2> taken)
    {
        Vector2 size = data.ObjectSize;
        Vector2 half = size * 0.5f;
        float topMin = arena.yMin + data.AppearTopRange.x;
        float topMax = Mathf.Min(arena.yMin + data.AppearTopRange.y, arena.yMax);
        Vector2? best = null;
        float bestOverlap = float.PositiveInfinity;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            float x = UnityEngine.Random.Range(arena.xMin + half.x, arena.xMax - half.x);
            float top = UnityEngine.Random.Range(topMin, topMax);
            var c = new Vector2(x, top - half.y);
            if (Vector2.Distance(c, target) < data.MinDistanceFromTarget) continue;
            if (boss is Bounds b && Overlaps(c, size, b, 0.2f)) continue;
            if (OverlapsTerrain(c, size)) continue;
            if (!CanReachTop(c, size, data.ReachHeight, arena)) continue;

            float overlap = 0f;
            foreach (Vector2 t in taken) overlap += OverlapArea(c, t, size);
            if (overlap <= 0f) return c;
            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                best = c;
            }
        }
        if (best is Vector2 found) return found;
        // 条件に合う場所が無いとき：相手から離れた低い位置に置く（ここに来ることは、ふつうは無い）
        float side = target.x <= arena.center.x ? 1f : -1f;
        float fx = Mathf.Clamp(target.x + side * (data.MinDistanceFromTarget + half.x), arena.xMin + half.x, arena.xMax - half.x);
        return new Vector2(fx, topMin - half.y);
    }

    /// <summary>近くの床・足場から、ジャンプで上の端に乗れるか</summary>
    bool CanReachTop(Vector2 center, Vector2 size, float reachHeight, Rect arena)
    {
        Vector2 half = size * 0.5f;
        float top = center.y + half.y, bottom = center.y - half.y;
        float highest = arena.yMin;
        for (float x = center.x - half.x - ReachHorizontal; x <= center.x + half.x + ReachHorizontal + 0.01f; x += 0.5f)
        {
            if (x < arena.xMin || x > arena.xMax) continue;
            bool under = x >= center.x - half.x && x <= center.x + half.x;
            // 真下は自分の下の端より下の床、横は上の端より下の床を探す
            if (TryFindGround(x, under ? bottom - 0.05f : top - 0.1f, out float y)) highest = Mathf.Max(highest, y);
        }
        return top - highest <= reachHeight;
    }

    bool OverlapsTerrain(Vector2 center, Vector2 size)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_groundMask | _solidMask);
        var results = new Collider2D[4];
        return Physics2D.OverlapBox(center, size - Vector2.one * 0.05f, 0f, filter, results) > 0;
    }

    static bool Overlaps(Vector2 center, Vector2 size, Bounds b, float margin)
    {
        Vector2 half = size * 0.5f;
        return center.x - half.x < b.max.x + margin && center.x + half.x > b.min.x - margin
            && center.y - half.y < b.max.y + margin && center.y + half.y > b.min.y - margin;
    }

    static float OverlapArea(Vector2 a, Vector2 b, Vector2 size)
    {
        float ox = Mathf.Max(0f, size.x - Mathf.Abs(a.x - b.x));
        float oy = Mathf.Max(0f, size.y - Mathf.Abs(a.y - b.y));
        return ox * oy;
    }

    /// <summary>自分（攻撃する人）の体の範囲。瞬間移動できる持ち主（ボス）だけ。無ければ null</summary>
    Bounds? OwnBodyBounds()
    {
        var w = GetComponent<IWarpable>();
        return w != null ? w.BodyBounds : (Bounds?)null;
    }

    /// <summary>狙う相手の体（移動用の当たり判定）の範囲。無ければ null</summary>
    Bounds? TargetBodyBounds()
    {
        if (_target == null) return null;
        foreach (var col in _target.GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds;
        }
        return null;
    }

    // ───────── Warp ─────────

    // ───────── 瞬間移動の絵（スキンのワープの枠。2026-11-18：スキルの見た目 1-1） ─────────
    // 消え始めた瞬間＝予告（物は元の場所。Direction は行き先への向き）→ 移った瞬間＝本体（物を行き先へ動かす）→ 現れきった＝消える。
    // 付けたスキンがあれば WarpDressed が true（プレイヤーの新しいタブは、今の小さいウィンドウを出さない）。
    // 現れる場所の予告（WarpTelegraph）を出す瞬間移動（ボス）は、予告の物がスキンに知らせるので、ここでは知らせない（二重にしない）

    GameObject _warpDress;
    AttackData _warpData;
    Vector2 _warpCenterOffset; // 体の中心の、足元からのずれ（被弾範囲を切る前に取る）
    Vector2 _warpSize;

    /// <summary>今の瞬間移動に、スキンが絵を付けたか（PlayerVfx が見る）</summary>
    public bool WarpDressed { get; private set; }

    Vector2 WarpBodyCenter(IWarpable warpable) =>
        warpable != null ? (Vector2)warpable.BodyBounds.center : (Vector2)transform.position + Vector2.up * 0.5f;

    void BeginWarpDress(AttackData data, IWarpable warpable, Vector2 from, Vector2 to)
    {
        EndWarpDress();
        WarpDressed = false;
        if (SkinTables.FrameOf(data) != AttackFrame.Warp) return;
        if (data.AppearTelegraph > 0f && warpable != null) return; // WarpTelegraph に任せる
        var go = new GameObject($"Warp_{data.name}");
        go.layer = gameObject.layer;
        Vector2 center = WarpBodyCenter(warpable);
        go.transform.position = center;
        go.AddComponent<SkinFxMarker>(); // 形態変化でも消える
        Vector2 size = warpable != null ? (Vector2)warpable.BodyBounds.size : new Vector2(0.7f, 1f);
        if (size.sqrMagnitude < 0.01f) size = new Vector2(0.7f, 1f);
        _warpCenterOffset = center - (Vector2)transform.position;
        _warpSize = size;
        Vector2 dir = (to - from).sqrMagnitude > 0.0001f ? (to - from).normalized : new Vector2(Facing, 0f);
        if (!SkinDresser.Dress(this, data, go, DressPhase.Telegraph, null, size, 0, dir, 1, data.VanishTime + data.AppearDelay))
        {
            Destroy(go);
            return;
        }
        Register(go);
        _warpDress = go;
        _warpData = data;
        WarpDressed = true;
    }

    void ActiveWarpDress(IWarpable warpable, Vector2 from, Vector2 to)
    {
        if (_warpDress == null) return;
        _warpDress.transform.position = (Vector2)transform.position + _warpCenterOffset; // 被弾範囲は切れているので、取っておいたずれを使う
        Vector2 size = _warpSize;
        Vector2 dir = (to - from).sqrMagnitude > 0.0001f ? (to - from).normalized : new Vector2(Facing, 0f);
        SkinDresser.Dress(this, _warpData, _warpDress, DressPhase.Active, null, size, 0, dir, 1, _warpData.AppearTime);
    }

    void EndWarpDress()
    {
        if (_warpDress == null) return;
        GameObject go = _warpDress;
        _warpDress = null;
        SkinDresser.Dress(this, _warpData, go, DressPhase.Vanish, null, Vector2.zero, 0, new Vector2(Facing, 0f));
        Destroy(go);
    }

    IEnumerator WarpRoutine(AttackData data)
    {
        var warpable = GetComponent<IWarpable>();
        var body = GetComponent<KinematicBody2D>();
        Vector2 from = transform.position;
        Vector2 to = ComputeWarpDestination(data, from, body);
        BeginWarpDress(data, warpable, from, to);
        RaiseReleased(data);

        bool hideContact = data.DisableContactUntilAppeared && warpable != null;
        // 体の範囲は、被弾範囲を切る前に取る（切ったあとは範囲が空になることがある。2026-11-19）
        Bounds bodyBounds = warpable != null ? warpable.BodyBounds : new Bounds(transform.position, Vector3.one);
        if (hideContact) warpable.SetContactEnabled(false);

        // 現れる場所の予告（現れ始める appearTelegraph 秒前から、現れきるまで）
        float appearStart = data.VanishTime + data.AppearDelay;
        if (data.AppearTelegraph > 0f && warpable != null)
        {
            WarpTelegraph.Spawn(this, bodyBounds, to - from,
                Mathf.Max(0f, appearStart - data.AppearTelegraph), appearStart + data.AppearTime);
        }

        yield return Fade(warpable, 1f, 0f, data.VanishTime);
        if (data.AppearDelay > 0f) yield return new WaitForSeconds(data.AppearDelay);

        if (warpable != null) warpable.Teleport(to);
        else if (body != null) body.Warp(to);
        else transform.position = to;
        ActiveWarpDress(warpable, from, (Vector2)transform.position);
        Warped?.Invoke(data, from, (Vector2)transform.position);

        yield return Fade(warpable, 0f, 1f, data.AppearTime);
        if (hideContact) warpable.SetContactEnabled(true);
        EndWarpDress();
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

    // ───────── 召喚（AI アシスタント） ─────────

    /// <summary>
    /// 召喚。Assistant：味方を出して、消えるまで待つ（消えたときに Finished が出る。能力のクールタイムはそこから数える）。
    /// Chaser：敵のミニロボットを少しずつ時間をずらして出し、出し終えたら終わる（ロボットは自分で動く）
    /// </summary>
    IEnumerator SummonRoutine(AttackData data)
    {
        if (data.SummonType == SummonType.Minion)
        {
            int n = data.SummonCount;
            for (int i = 0; i < n; i++)
            {
                if (data.SummonMarquee)
                {
                    // 選択ツール：カーソルが伸びてきて、狙う相手の近くの床を四角く選び、その中に出す（Photoshop のボス）
                    if (data.SummonMaxAlive > 0 && data.SummonPrefab != null && AliveMinions(data.SummonPrefab.name) >= data.SummonMaxAlive) break;
                    MarqueeSelect m = MarqueeSelect.Spawn(this, OriginPoint, PickMarqueeRect());
                    while (m != null && !m.IsDone) yield return null;
                    if (m != null) SpawnMinion(data, m.SpawnPoint);
                    Released?.Invoke(data);
                    continue;
                }
                float spread = n > 1 ? (i / (float)(n - 1) - 0.5f) * Mathf.Min(4f, 1.2f * (n - 1)) : 0f;
                SpawnMinion(data, OriginPoint + new Vector2(spread, -0.4f));
                Released?.Invoke(data);
                if (i < n - 1) yield return new WaitForSeconds(0.15f);
            }
            yield break;
        }
        if (data.SummonType == SummonType.Chaser)
        {
            int n = data.SummonCount;
            for (int i = 0; i < n; i++)
            {
                // 持ち主のまわりに、左右へ広げて出す（数が多いほど広く。最大で左右 2.5 マス）
                float width = Mathf.Min(5f, 0.6f * (n - 1));
                float spread = n > 1 ? (i / (float)(n - 1) - 0.5f) * width : 0f;
                ChaserBot.Spawn(this, data, OriginPoint + new Vector2(spread, -0.6f));
                Released?.Invoke(data);
                if (i < n - 1) yield return new WaitForSeconds(0.1f);
            }
            yield break;
        }

        AssistantDrone drone = AssistantDrone.Spawn(this, data);
        Released?.Invoke(data);
        while (drone != null) yield return null;
    }

    // ───────── チャットのメッセージ（Slack・Discord・LINE） ─────────

    /// <summary>
    /// 最初のメッセージを出して、メッセージを足せる間（止まっていて、最大数に届いていない間）待つ。
    /// 足せなくなったら終わる（Finished。能力のクールタイムはそこから数える）。メッセージが飛んでいくのは、そのあとでもよい
    /// </summary>
    IEnumerator ChatRoutine(AttackData data)
    {
        ChatVolley volley = ChatVolley.Start(this, data);
        Released?.Invoke(data);
        // 繰り返す回数が2以上なら、自分で続けて送る（チャットのボスの連投。プレイヤーは1回ずつボタンで足す）
        for (int r = 1; r < data.RepeatCount && volley != null && volley.IsOpen; r++)
        {
            if (data.RepeatInterval > 0f) yield return new WaitForSeconds(data.RepeatInterval);
            if (!ChatVolley.TryAppend(this, data)) break;
            Released?.Invoke(data);
        }
        while (volley != null && volley.IsOpen) yield return null;
    }

    // ───────── 筆（Photoshop） ─────────

    /// <summary>筆を振り終わるまで待つ（飛び出したインクは、そのあとも残る）</summary>
    IEnumerator BrushRoutine(AttackData data)
    {
        // 振る前に、部屋の端（狙う相手と反対側）へ寄る（Photoshop のボス。寄っている間と振っている間は、体当たり・被弾なし）
        var warpable = GetComponent<IWarpable>();
        bool moved = data.BrushMoveToEdge;
        if (moved)
        {
            if (warpable != null) warpable.SetContactEnabled(false);
            yield return MoveToFarEdge(data, warpable);
        }
        BrushSwing swing = BrushSwing.Spawn(this, data);
        Released?.Invoke(data);
        while (swing != null && !swing.IsDone) yield return null;
        if (moved && warpable != null) warpable.SetContactEnabled(true);
    }

    /// <summary>部屋の端のうち、狙う相手から遠い方へ飛んでいく（高さは部屋の真ん中より少し上）。着いたら相手の方を向く</summary>
    IEnumerator MoveToFarEdge(AttackData data, IWarpable warpable)
    {
        Rect arena = BattleArena.GetBounds();
        float halfW = warpable != null ? warpable.BodyBounds.extents.x : 1f;
        Vector2 target = TargetPoint ?? arena.center;
        bool goLeft = target.x >= arena.center.x;
        var to = new Vector2(goLeft ? arena.xMin + halfW + 0.3f : arena.xMax - halfW - 0.3f, arena.yMin + arena.height * 0.55f);
        Vector2 from = transform.position;
        float start = Time.time;
        while (Time.time - start < data.BrushMoveTime)
        {
            Vector2 p = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (Time.time - start) / data.BrushMoveTime));
            if (warpable != null) warpable.Teleport(p);
            else transform.position = p;
            yield return null;
        }
        if (warpable != null) warpable.Teleport(to);
        else transform.position = to;
        Facing = goLeft ? 1 : -1;
    }

    // ───────── パケット（ブラウザの能力） ─────────

    /// <summary>パケットを撃ち終わるまで待つ（追尾・応答のパケットは、そのあとも続く）</summary>
    IEnumerator PacketRoutine(AttackData data)
    {
        PacketVolley volley = PacketVolley.Spawn(this, data);
        Released?.Invoke(data);
        while (volley != null && !volley.IsFired) yield return null;
    }

    // ───────── 爆弾のサイト（ブラウザのボス） ─────────

    /// <summary>
    /// 爆弾のサイトのウィンドウを、少しずつずらして開く（1つ目は狙う相手の近く、ほかは部屋のあちこち。ウィンドウどうしは離す）。
    /// 開き終わったら終わる（読み込み・爆発は、ウィンドウが自分で進める）
    /// </summary>
    IEnumerator BombSiteRoutine(AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 half = data.ObjectSize * 0.5f + new Vector2(0.3f, 0.3f);
        Vector2 target = TargetPoint ?? arena.center;
        var placed = new List<Vector2>();
        for (int i = 0; i < data.SpawnCount; i++)
        {
            Vector2 best = arena.center;
            float bestScore = float.NegativeInfinity;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 c = i == 0
                    ? target + new Vector2(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(-0.5f, 1.5f))
                    : new Vector2(UnityEngine.Random.Range(arena.xMin + half.x, arena.xMax - half.x), UnityEngine.Random.Range(arena.yMin + half.y + 0.5f, arena.yMax - half.y));
                c.x = Mathf.Clamp(c.x, arena.xMin + half.x, arena.xMax - half.x);
                c.y = Mathf.Clamp(c.y, arena.yMin + half.y, arena.yMax - half.y);
                float score = 10f;
                foreach (var q in placed) score = Mathf.Min(score, Vector2.Distance(c, q));
                if (score > bestScore) { bestScore = score; best = c; }
                if (score >= data.AreaSize.x) break;
            }
            placed.Add(best);
            BombSiteWindow.Spawn(this, data, best);
            Released?.Invoke(data);
            if (i < data.SpawnCount - 1) yield return new WaitForSeconds(0.15f);
        }
        yield return new WaitForSeconds(0.3f);
    }

    // ───────── 画面のフィルター（Photoshop のボスの大技） ─────────

    /// <summary>フィルターをかけ終わるまで待つ（状態異常と画面の色は、そのあとも決まった秒数続く）</summary>
    IEnumerator FilterRoutine(AttackData data)
    {
        ScreenFilter filter = ScreenFilter.Spawn(this, data);
        Released?.Invoke(data);
        while (filter != null && !filter.IsCastDone) yield return null;
    }

    // ───────── 通知のアラーム（チャットのボスの大技） ─────────

    /// <summary>通知を置いてベルを鳴らし、回収されるか攻撃が終わるまで待つ（その間、持ち主は動かない）</summary>
    IEnumerator AlarmRoutine(AttackData data)
    {
        NotificationAlarm alarm = NotificationAlarm.Spawn(this, data);
        Released?.Invoke(data);
        while (alarm != null && !alarm.IsDone) yield return null;
    }

    /// <summary>
    /// 雑魚をプレハブから出す（チャットのボスのチャットbot。2026-11-19）。出した物として登録する（正常化のときに消える）。
    /// 「召喚した味方の絵」があれば、雑魚のコマ送りの絵をそれに差し替える（アプリの色に塗り替えた絵）
    /// </summary>
    void SpawnMinion(AttackData data, Vector2 position)
    {
        if (data.SummonPrefab == null) return;
        // 同時にいられる数がいっぱいなら出さない（倒れた雑魚は数えない）
        if (data.SummonMaxAlive > 0 && AliveMinions(data.SummonPrefab.name) >= data.SummonMaxAlive) return;
        Rect arena = BattleArena.GetBounds();
        position.x = Mathf.Clamp(position.x, arena.xMin + 0.6f, arena.xMax - 0.6f);
        GameObject go = Instantiate(data.SummonPrefab, position, Quaternion.identity);
        go.name = data.SummonPrefab.name;
        go.SetActive(true);
        Register(go);
        foreach (var s in go.GetComponents<ISummonedBy>()) s.SetSummoner(this); // 出した物がさらに雑魚を出すとき（ブラウザのボスのウィンドウ）
        Sprite[] frames = data.SummonFrames;
        if (frames.Length > 0 && frames[0] != null)
        {
            var anim = go.GetComponentInChildren<SpriteFrameAnimation>(true);
            if (anim != null) anim.Setup(anim.GetComponent<SpriteRenderer>(), frames, 7f, loop: true, destroyWhenDone: false);
        }
        Vfx.SmokeSmall(position + Vector2.up * 0.4f);
    }

    /// <summary>選択ツールで選ぶ四角：狙う相手から横に2〜4マス離れた床・足場の上（横2.2×高さ2.6）</summary>
    Rect PickMarqueeRect()
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 target = TargetPoint ?? arena.center;
        const float w = 2.2f, h = 2.6f;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float x = target.x + side * UnityEngine.Random.Range(2f, 4f);
        if (x < arena.xMin + w * 0.5f + 0.3f || x > arena.xMax - w * 0.5f - 0.3f) x = target.x - side * UnityEngine.Random.Range(2f, 4f);
        x = Mathf.Clamp(x, arena.xMin + w * 0.5f + 0.3f, arena.xMax - w * 0.5f - 0.3f);
        TryFindGround(x, target.y + 1f, out float groundY);
        return new Rect(x - w * 0.5f, groundY, w, h);
    }

    /// <summary>この人が出した、その名前の雑魚のうち、まだ倒れていない数</summary>
    int AliveMinions(string prefabName)
    {
        _spawned.RemoveAll(g => g == null);
        int n = 0;
        foreach (var go in _spawned)
        {
            if (go.name != prefabName || !go.activeInHierarchy) continue;
            if (go.TryGetComponent(out Health h) && h.IsDead) continue;
            n++;
        }
        return n;
    }

    // ───────── 直線のレーザー（AI チャットのボス） ─────────

    /// <summary>
    /// レーザーを撃つ。撃つ位置が ArenaLeft なら、先に部屋の左端へ寄る（寄っている間と撃っている間は、体当たり・被弾なし）。
    /// 撃ったレーザー（予告 → 太くなって細くなる）が消えるまで待つ
    /// </summary>
    IEnumerator BeamRoutine(AttackData data)
    {
        var warpable = GetComponent<IWarpable>();
        bool moved = data.BeamOrigin == BeamOrigin.ArenaLeft;
        if (moved)
        {
            if (warpable != null) warpable.SetContactEnabled(false);
            yield return MoveToArenaLeft(data, warpable);
        }

        float lastEnd = 0f;
        for (int r = 0; r < data.RepeatCount; r++)
        {
            if (r > 0 && data.RepeatInterval > 0f) yield return new WaitForSeconds(data.RepeatInterval);
            FireBeams(data);
            lastEnd = Time.time + data.TelegraphTime + data.ActiveTime;
        }
        while (Time.time < lastEnd) yield return null;
        if (moved && warpable != null) warpable.SetContactEnabled(true);
    }

    /// <summary>大技の前に、部屋の左端（高さは撃つ高さ）へ飛んでいく</summary>
    IEnumerator MoveToArenaLeft(AttackData data, IWarpable warpable)
    {
        Rect arena = BattleArena.GetBounds();
        float halfW = warpable != null ? warpable.BodyBounds.extents.x : 1f;
        Vector2 from = transform.position;
        var to = new Vector2(arena.xMin + halfW + 0.3f, BeamCenterY(data, arena));
        float start = Time.time;
        while (Time.time - start < data.BeamMoveTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, (Time.time - start) / data.BeamMoveTime);
            Vector2 p = Vector2.Lerp(from, to, t);
            if (warpable != null) warpable.Teleport(p);
            else transform.position = p;
            yield return null;
        }
        if (warpable != null) warpable.Teleport(to);
        else transform.position = to;
    }

    /// <summary>部屋の左端から撃つレーザーの高さ：狙う相手の高さ（レーザーが部屋からはみ出さないように寄せる）</summary>
    float BeamCenterY(AttackData data, Rect arena)
    {
        float y = (TargetPoint ?? OriginPoint).y;
        float half = Mathf.Min(data.BeamWidth * 0.5f, arena.height * 0.5f);
        return Mathf.Clamp(y, arena.yMin + half, arena.yMax - half);
    }

    void FireBeams(AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        if (data.BeamOrigin == BeamOrigin.ArenaLeft)
        {
            var origin = new Vector2(arena.xMin, BeamCenterY(data, arena));
            BeamStrike.Spawn(this, data, origin, Vector2.right, arena.width);
            Released?.Invoke(data);
            return;
        }

        Vector2 from = OriginPoint;
        Vector2 baseDir = new Vector2(Facing, 0f);
        if (data.Aim == AimMode.Target && TargetPoint is Vector2 target)
        {
            Vector2 d = target - from;
            if (d.sqrMagnitude > 0.0001f) baseDir = d.normalized;
        }
        int n = data.ProjectileCount;
        for (int i = 0; i < n; i++)
        {
            float angle = n > 1 ? -data.SpreadAngle * 0.5f + data.SpreadAngle * i / (n - 1) : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            BeamStrike.Spawn(this, data, from, dir, RayLengthInRect(from, dir, arena));
        }
        Released?.Invoke(data);
    }

    /// <summary>from から dir の向きに進んで、部屋の端に着くまでの長さ（レーザーは地形を通り抜け、部屋の端で止まる）</summary>
    static float RayLengthInRect(Vector2 from, Vector2 dir, Rect r)
    {
        float t = 100f;
        if (dir.x > 0.0001f) t = Mathf.Min(t, (r.xMax - from.x) / dir.x);
        else if (dir.x < -0.0001f) t = Mathf.Min(t, (r.xMin - from.x) / dir.x);
        if (dir.y > 0.0001f) t = Mathf.Min(t, (r.yMax - from.y) / dir.y);
        else if (dir.y < -0.0001f) t = Mathf.Min(t, (r.yMin - from.y) / dir.y);
        return Mathf.Max(0.5f, t);
    }

    // ───────── 共通の道具 ─────────

    /// <summary>x の位置で、fromY から真下にある床の上面を探す</summary>
    /// <param name="steadyOnly">2026-11-19：true なら、動く・消えるアプリの足場（AppStagePlatforms）は床にしない（置いた物・着地した物が宙に残らないように）</param>
    public bool TryFindGround(float x, float fromY, out float groundY, bool steadyOnly = false)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_groundMask);
        var hits = new RaycastHit2D[4];
        int count = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, filter, hits, 100f);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance <= 0f) continue; // 始点が地形の中
            if (steadyOnly && AppStagePlatforms.IsUnsteady(hits[i].collider)) continue;
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

    /// <summary>
    /// 当たり判定（Hitbox）を付ける。コライダーは呼んだ側で先に付けておく。
    /// 当たったときの見た目用に、攻撃のスキン・エラーの絵の種類・攻撃データも入れる（2026-11-18。種類を変えたいときは、返した Hitbox の SetSkin で上書きする）
    /// </summary>
    public Hitbox AddHitbox(GameObject go, AttackData data, Collider2D area, bool hitOncePerActivation = true)
    {
        var hitbox = go.AddComponent<Hitbox>();
        hitbox.SetArea(area);
        hitbox.SetOwner(_team, _targetLayers, hitOncePerActivation);
        hitbox.SetStatSource(transform); // 持ち主のバフ（Steam の実績解除）を、出した弾・判定にも効かせる
        hitbox.Setup(data);
        SkinDresser.Look look = SkinDresser.LookOf(this, data);
        hitbox.SetSkin(look.Skin, SkinTables.ErrorKindOf(data), data, look.Theme, look.Icon);
        hitbox.Deactivate();
        return hitbox;
    }

    // ───────── 予備動作の絵（スキン。2026-11-18：スキルの見た目 1-1） ─────────
    // 弾の技の予備動作の間だけ、攻撃する人の子に空の物を出して、スキンに「予告」の絵を付けてもらう（タブが扇に並ぶ・エラーの数のバッジなど）。
    // どのスキンも付けなければ、すぐ消す（今の見た目のまま）

    GameObject _windupDress;
    AttackData _windupData;

    void BeginWindupDress(AttackData data)
    {
        EndWindupDress();
        if (data.Kind != AttackKind.Projectile || SkinTables.FrameOf(data) == AttackFrame.None) return;
        var go = new GameObject($"Windup_{data.name}");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = OriginPoint;
        go.AddComponent<SkinWindupMarker>();
        go.AddComponent<SkinFxMarker>(); // 形態変化でも消える
        int count = Mathf.Max(1, data.ProjectileCount) * Mathf.Max(1, data.RepeatCount);
        if (!SkinDresser.Dress(this, data, go, DressPhase.Telegraph, null, data.HitSize, 0, new Vector2(Facing, 0f), count, data.Windup))
        {
            Destroy(go);
            return;
        }
        Register(go);
        _windupDress = go;
        _windupData = data;
    }

    void EndWindupDress()
    {
        if (_windupDress == null) return;
        GameObject go = _windupDress;
        _windupDress = null;
        SkinDresser.Dress(this, _windupData, go, DressPhase.Vanish, null, _windupData != null ? _windupData.HitSize : Vector2.zero, 0, new Vector2(Facing, 0f));
        Destroy(go);
    }

    /// <summary>
    /// 絵（子の SpriteRenderer）を付ける。
    /// ・縁（9スライスの境界）がある絵：縁はそのままの大きさで、間を並べて指定の大きさにする（柱・波線・点線の枠。ドットが伸びない）
    /// ・縁が無い絵：指定した大きさに合わせて伸び縮みさせる
    /// </summary>
    public static SpriteRenderer AddVisual(Transform parent, Sprite sprite, Color color, Vector2 size, int sortingOrder = 20, string objectName = "Visual")
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : PlaceholderSprites.Square;
        if (sr.sprite.border != Vector4.zero)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
        }
        else
        {
            Vector2 native = sr.sprite.bounds.size; // 絵の元の大きさに関係なく、指定した大きさに合わせる
            go.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
        }
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
