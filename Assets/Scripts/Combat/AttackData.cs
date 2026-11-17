using UnityEngine;

/// <summary>
/// 攻撃1つ分のデータ。プレイヤーの能力・近接と、ボスの技で共通に使う。
/// 項目は docs/app-boss-and-ability-design.md の6章の表（MVPに必要な分）。
/// 種類（Kind）に関係のない項目は使われない。
/// 実行するのは AttackRunner（敵味方共通）。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Attack Data
/// </summary>
[CreateAssetMenu(fileName = "Attack_", menuName = "Team1/Attack Data")]
public class AttackData : ScriptableObject
{
    // ───────── 共通 ─────────
    [Header("共通")]
    [SerializeField] AttackKind _kind = AttackKind.Projectile;
    [Tooltip("予備動作（秒）。この間に構えを見せてから攻撃が出る。床に予告（影・赤丸）を出す技は、予告時間の方を使い、ここは0にする")]
    [SerializeField, Min(0f)] float _windup;
    [Tooltip("判定時間（秒）。指定位置の判定・近接で、攻撃判定を出している時間。出現型の落下物（ポップアップ出現）では、現れてから判定を出している時間")]
    [SerializeField, Min(0f)] float _activeTime = 0.3f;
    [Tooltip("間隔（クールタイム、秒）。プレイヤーの能力・近接で、次に使えるまでの時間")]
    [SerializeField, Min(0f)] float _cooldown;
    [Tooltip("ダメージ量。プレイヤー（ライフ制）は量に関係なく1減る。雑魚はこの分だけHPが減る")]
    [SerializeField, Min(0)] int _damage = 1;
    [Tooltip("ボスのウイルス解除ゲージを増やす量（プレイヤーの攻撃用）")]
    [SerializeField, Min(0)] int _gaugeGain;
    [Tooltip("吹き飛ばす速度。x は相手が攻撃から離れる向きに自動で反転する")]
    [SerializeField] Vector2 _knockback = new Vector2(6f, 5f);
    [Tooltip("相手がのけぞって操作できない秒数")]
    [SerializeField, Min(0f)] float _hitStun = 0.25f;
    [Tooltip("1回の攻撃で、同じものを何回出すか（2連射なら2、扇状を2回なら2）")]
    [SerializeField, Min(1)] int _repeatCount = 1;
    [Tooltip("繰り返すときの間隔（秒）")]
    [SerializeField, Min(0f)] float _repeatInterval;

    [Header("見た目（仮素材）")]
    [Tooltip("弾・判定・落下物の絵。空なら白い四角（円の判定は白い円）を使う")]
    [SerializeField] Sprite _sprite;
    [SerializeField] Color _color = Color.white;

    // ───────── 弾（Projectile） ─────────
    [Header("弾（Projectile のとき）")]
    [Tooltip("1回に撃つ数")]
    [SerializeField, Min(1)] int _projectileCount = 1;
    [Tooltip("扇の角度（度）。数が2以上のとき、この角度の中に等間隔で並べる")]
    [SerializeField, Range(0f, 360f)] float _spreadAngle;
    [Tooltip("速さ（マス/秒）")]
    [SerializeField, Min(0f)] float _speed = 7f;
    [Tooltip("重力（マス/秒²）。0なら真っすぐ飛ぶ")]
    [SerializeField] float _gravity;
    [Tooltip("寿命（秒）。過ぎたら消える")]
    [SerializeField, Min(0.01f)] float _lifetime = 3f;
    [Tooltip("判定の大きさ（マス）。弾は進む向きに回転する。x が進む向きの長さ")]
    [SerializeField] Vector2 _hitSize = new Vector2(0.5f, 0.5f);
    [SerializeField] AimMode _aim = AimMode.Target;
    [Tooltip("正面に撃つとき、上へ傾ける角度（度）。放物線で投げる弾（ボム）に使う")]
    [SerializeField, Range(0f, 85f)] float _launchAngle;
    [Tooltip("弾を出す位置のずれ（x は向きに合わせて左右を反転）。足元が原点の持ち主（プレイヤー）が、体の中ほどから投げるときに使う")]
    [SerializeField] Vector2 _spawnOffset;
    [Tooltip("地形（床・壁）に当たったら消えるか")]
    [SerializeField] bool _destroyOnTerrain = true;
    [Tooltip("相手に当たったら消えるか")]
    [SerializeField] bool _destroyOnHit = true;
    [Tooltip("地形に当たった位置に出す追撃（赤い波線など）。無ければ空")]
    [SerializeField] AttackData _onTerrainHit;
    [Tooltip("オン：相手に当たったときも、当たった位置に追撃を出す（ボムの爆発）")]
    [SerializeField] bool _followUpOnHit;

    // ───────── 指定位置の判定（Area） ─────────
    [Header("指定位置の判定（Area のとき）")]
    [SerializeField] AreaAnchor _anchor = AreaAnchor.TargetPosition;
    [Tooltip("前方Nマス（出す場所が Forward のとき）")]
    [SerializeField] float _forwardDistance = 4f;
    [Tooltip("真下の床の上に出すか（柱・赤丸）。オフなら指定した位置にそのまま出す")]
    [SerializeField] bool _snapToGround = true;
    [Tooltip("弾が当たった面に沿わせるか（壁に当たったら縦向きにする）。出す場所が HitPoint のとき")]
    [SerializeField] bool _alignToSurface = true;
    [SerializeField] AreaShape _shape = AreaShape.Box;
    [Tooltip("形の大きさ（マス）。床の上に出すときは、下の端が床に付く。円は x が直径")]
    [SerializeField] Vector2 _areaSize = new Vector2(1.5f, 12f);
    [Tooltip("同時に出す数")]
    [SerializeField, Min(1)] int _areaCount = 1;
    [Tooltip("2つ目以降をずらす距離（マス）。2つ目は左、3つ目は右、4つ目はさらに左…の順")]
    [SerializeField, Min(0f)] float _areaSpacing = 4f;
    [Tooltip("2つ目以降をずらす時間（秒）")]
    [SerializeField, Min(0f)] float _areaStagger;
    [Tooltip("オン：2つ目以降を前へ順に並べる（棒グラフ）。オフ：左→右→さらに左…の順")]
    [SerializeField] bool _areaForwardOrder;

    [Header("予告（Area・Spawn のとき）")]
    [Tooltip("予告（赤丸・影・出現の枠）を出してから判定・落下・出現を始めるまでの秒数")]
    [SerializeField, Min(0f)] float _telegraphTime = 1f;

    [Header("持続（Area のとき。残り続けるダメージ床）")]
    [Tooltip("持続時間（秒）。0より大きいと、判定時間の代わりにこの時間だけ残る")]
    [SerializeField, Min(0f)] float _persistDuration;
    [Tooltip("ヒット間隔（秒）。持続中、この間隔で同じ相手にもう一度当たる")]
    [SerializeField, Min(0.05f)] float _tickInterval = 0.5f;

    // ───────── 落下物・設置物（Spawn） ─────────
    [Header("落下物・設置物（Spawn のとき）")]
    [SerializeField] SpawnPlacement _placement = SpawnPlacement.AroundTarget;
    [Tooltip("出す数")]
    [SerializeField, Min(1)] int _spawnCount = 2;
    [Tooltip("2つ目以降を、狙う相手からどれだけ離すか（マス）。置き方が AroundTarget のとき")]
    [SerializeField, Min(0f)] float _minDistanceFromTarget = 3f;
    [Tooltip("落下物の大きさ（マス）。判定も同じ大きさ")]
    [SerializeField] Vector2 _objectSize = new Vector2(3f, 2f);
    [Tooltip("落下速度（マス/秒）。等速で落ちる")]
    [SerializeField, Min(0.1f)] float _fallSpeed = 12f;
    [Tooltip("着地後に残る秒数。0なら着地したらすぐ消える")]
    [SerializeField, Min(0f)] float _landRemainTime;
    [Tooltip("着地後、すり抜け床（足場）になるか")]
    [SerializeField] bool _becomesPlatform;
    [Tooltip("並べる間隔（秒）。置き方が TopRow のとき")]
    [SerializeField, Min(0f)] float _placeInterval = 0.3f;
    [Tooltip("並べる距離の間隔（マス）。置き方が TopRow のとき")]
    [SerializeField, Min(0f)] float _placeSpacing = 3f;
    [Tooltip("並べ終えてから落とし始めるまでの秒数。置き方が TopRow のとき")]
    [SerializeField, Min(0f)] float _dropHoldTime = 0.5f;
    [Tooltip("落とす間隔（秒）。置き方が TopRow のとき、左から順に落とす")]
    [SerializeField, Min(0f)] float _dropInterval = 0.25f;
    [Tooltip("【仮】出す高さの範囲。上の端の、部屋の床からの高さ（マス）。置き方が AppearAroundTarget のとき（1つ目を除く）")]
    [SerializeField] Vector2 _appearTopRange = new Vector2(2.5f, 6f);
    [Tooltip("【仮】上の端に乗れるとみなす高さ（マス）。近くの床・足場から上の端までがこれ以下の位置にだけ出す。ジャンプの高さ3.2より少し低くしてある。置き方が AppearAroundTarget のとき（1つ目を除く）")]
    [SerializeField, Min(0.5f)] float _reachHeight = 3f;

    // ───────── Warp ─────────
    [Header("Warp のとき")]
    [SerializeField] WarpDestination _warpDestination = WarpDestination.Forward;
    [Tooltip("前方Nマス（行き先が Forward のとき）")]
    [SerializeField, Min(0f)] float _warpDistance = 6f;
    [Tooltip("消えるのにかける秒数。0なら一瞬で消える")]
    [SerializeField, Min(0f)] float _vanishTime;
    [Tooltip("消えてから現れ始めるまでの秒数")]
    [SerializeField, Min(0f)] float _appearDelay;
    [Tooltip("現れる場所に、現れ始める何秒前から予告を出すか。0なら出さない")]
    [SerializeField, Min(0f)] float _appearTelegraph;
    [Tooltip("現れるのにかける秒数。0なら一瞬で現れる")]
    [SerializeField, Min(0f)] float _appearTime;
    [Tooltip("完全に現れるまで、体当たりの判定と被弾範囲を無効にするか")]
    [SerializeField] bool _disableContactUntilAppeared = true;

    // ───────── 読み取り用 ─────────
    public AttackKind Kind => _kind;
    public float Windup => _windup;
    public float ActiveTime => _activeTime;
    public float Cooldown => _cooldown;
    public int Damage => _damage;
    public int GaugeGain => _gaugeGain;
    public Vector2 Knockback => _knockback;
    public float HitStun => _hitStun;
    public int RepeatCount => _repeatCount;
    public float RepeatInterval => _repeatInterval;
    public Sprite Sprite => _sprite;
    public Color Color => _color;

    public int ProjectileCount => _projectileCount;
    public float SpreadAngle => _spreadAngle;
    public float Speed => _speed;
    public float Gravity => _gravity;
    public float Lifetime => _lifetime;
    public Vector2 HitSize => _hitSize;
    public AimMode Aim => _aim;
    public float LaunchAngle => _launchAngle;
    public Vector2 SpawnOffset => _spawnOffset;
    public bool DestroyOnTerrain => _destroyOnTerrain;
    public bool DestroyOnHit => _destroyOnHit;
    public AttackData OnTerrainHit => _onTerrainHit;
    public bool FollowUpOnHit => _followUpOnHit;

    public AreaAnchor Anchor => _anchor;
    public float ForwardDistance => _forwardDistance;
    public bool SnapToGround => _snapToGround;
    public bool AlignToSurface => _alignToSurface;
    public AreaShape Shape => _shape;
    public Vector2 AreaSize => _areaSize;
    public int AreaCount => _areaCount;
    public float AreaSpacing => _areaSpacing;
    public float AreaStagger => _areaStagger;
    public bool AreaForwardOrder => _areaForwardOrder;
    public float TelegraphTime => _telegraphTime;
    public float PersistDuration => _persistDuration;
    public float TickInterval => _tickInterval;

    public SpawnPlacement Placement => _placement;
    public int SpawnCount => _spawnCount;
    public float MinDistanceFromTarget => _minDistanceFromTarget;
    public Vector2 ObjectSize => _objectSize;
    public float FallSpeed => _fallSpeed;
    public float LandRemainTime => _landRemainTime;
    public bool BecomesPlatform => _becomesPlatform;
    public float PlaceInterval => _placeInterval;
    public float PlaceSpacing => _placeSpacing;
    public float DropHoldTime => _dropHoldTime;
    public float DropInterval => _dropInterval;
    public Vector2 AppearTopRange => _appearTopRange;
    public float ReachHeight => _reachHeight;

    public WarpDestination WarpDestination => _warpDestination;
    public float WarpDistance => _warpDistance;
    public float VanishTime => _vanishTime;
    public float AppearDelay => _appearDelay;
    public float AppearTelegraph => _appearTelegraph;
    public float AppearTime => _appearTime;
    public bool DisableContactUntilAppeared => _disableContactUntilAppeared;

    /// <summary>判定を出し続ける時間（持続があれば持続時間）</summary>
    public float HitWindow => _persistDuration > 0f ? _persistDuration : _activeTime;

    /// <summary>
    /// 数値を1つだけ変えた複製を作る（使用時間による強化用）。アセットそのものは書き換えない
    /// </summary>
    public AttackData CreateScaledCopy(AttackStat stat, float value)
    {
        var copy = Instantiate(this);
        copy.name = name;
        switch (stat)
        {
            case AttackStat.WarpDistance: copy._warpDistance = Mathf.Max(0f, value); break;
            case AttackStat.GaugeGain: copy._gaugeGain = Mathf.Max(0, Mathf.RoundToInt(value)); break;
            case AttackStat.Damage: copy._damage = Mathf.Max(0, Mathf.RoundToInt(value)); break;
            case AttackStat.AreaCount: copy._areaCount = Mathf.Max(1, Mathf.RoundToInt(value)); break;
            case AttackStat.PersistDuration: copy._persistDuration = Mathf.Max(0f, value); break;
            case AttackStat.Cooldown: copy._cooldown = Mathf.Max(0f, value); break;
            case AttackStat.AreaScale: copy._areaSize = _areaSize * Mathf.Max(0.1f, value); break;
            case AttackStat.ProjectileCount: copy._projectileCount = Mathf.Max(1, Mathf.RoundToInt(value)); break;
        }
        return copy;
    }

    /// <summary>
    /// 絵を塗り替えた複製を作る（その場で作る能力を、アプリの色にするとき用）。追撃も同じように塗り替える。
    /// 色（掛ける色）は白にして、塗り替えた絵の色をそのまま出す。アセットそのものは書き換えない
    /// </summary>
    public AttackData CreateRecoloredCopy(System.Func<Sprite, Sprite> recolor)
    {
        var copy = Instantiate(this);
        copy.name = name;
        if (recolor != null && _sprite != null) copy._sprite = recolor(_sprite);
        copy._color = new Color(1f, 1f, 1f, _color.a);
        if (_onTerrainHit != null) copy._onTerrainHit = _onTerrainHit.CreateRecoloredCopy(recolor);
        return copy;
    }
}
