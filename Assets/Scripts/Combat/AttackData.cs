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
    [Tooltip("オン：ボスの技の絵を、アプリの色に塗り替えない（通知の赤いマークなど。2026-11-19）")]
    [SerializeField] bool _fixedColors;

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

    // ───────── 召喚（Summon） ─────────
    [Header("召喚（Summon のとき。AI アシスタント）")]
    [Tooltip("召喚した味方の絵（コマ送り。羽ばたき）。レーザーの絵は上の「見た目」の絵と色を使う")]
    [SerializeField] Sprite[] _summonFrames;
    [Tooltip("コマ送りの速さ（コマ/秒）")]
    [SerializeField, Min(0.1f)] float _summonFps = 10f;
    [Tooltip("味方がいられる最大の秒数。過ぎたら消える")]
    [SerializeField, Min(0.1f)] float _summonLifetime = 30f;
    [Tooltip("レーザーを撃てる合計の秒数。撃ち切ったら消える")]
    [SerializeField, Min(0.1f)] float _summonFireTime = 7f;
    [Tooltip("敵を探す範囲（持ち主の体の中心からの半径、マス）")]
    [SerializeField, Min(0.5f)] float _summonRange = 6f;
    [Tooltip("持ち主から見た、浮かぶ位置（x は持ち主の向きの後ろ側を正、y は足元からの高さ）")]
    [SerializeField] Vector2 _summonHoverOffset = new Vector2(0.9f, 1.9f);
    [Tooltip("持ち主に付いていく速さ（大きいほど、すぐ追いつく）")]
    [SerializeField, Min(0.1f)] float _summonFollowSharpness = 6f;
    [Tooltip("レーザーの太さ（マス）。判定も同じ太さ。Beam のときは、いちばん太いときの太さ")]
    [SerializeField, Min(0.05f)] float _beamWidth = 0.3f;
    [Tooltip("召喚する物の種類。Assistant：味方の AI アシスタント、Chaser：敵のミニロボット")]
    [SerializeField] SummonType _summonType = SummonType.Assistant;
    [Tooltip("一度に出す数（Chaser のとき）")]
    [SerializeField, Min(1)] int _summonCount = 1;
    [Tooltip("動く速さ（マス/秒。Chaser のとき）")]
    [SerializeField, Min(0f)] float _summonSpeed = 2.5f;
    [Tooltip("1秒に曲がれる角度（度。Chaser のとき）。小さいほど、追いかけ方がゆるい")]
    [SerializeField, Min(0f)] float _summonTurnRate = 90f;
    [Tooltip("出す雑魚のプレハブ（Minion のとき）。「召喚した味方の絵」があれば、雑魚の絵をそれに差し替える")]
    [SerializeField] GameObject _summonPrefab;
    [Tooltip("この技で出した雑魚が、同時にいられる数（Minion のとき。0なら制限なし）。いっぱいなら出さない")]
    [SerializeField, Min(0)] int _summonMaxAlive;
    [Tooltip("オン：出す前に、マウスカーソルが伸びてきて選択ツールで四角く選び、その中に出す（Minion のとき。Photoshop のボス）")]
    [SerializeField] bool _summonMarquee;

    // ───────── 直線のレーザー（Beam） ─────────
    [Header("直線のレーザー（Beam のとき。AI チャットのボス）")]
    [Tooltip("見せ方。Laser：ふつうのレーザー（予告は電流）、CodeStream：大技（予告は赤い危険エリア、文字列が流れる）")]
    [SerializeField] BeamStyle _beamStyle = BeamStyle.Laser;
    [Tooltip("撃つ位置。Owner：持ち主から（数・扇の角度・狙い方は「弾」の欄を使う）、ArenaLeft：部屋の左端へ寄ってから右向きに")]
    [SerializeField] BeamOrigin _beamOrigin = BeamOrigin.Owner;
    [Tooltip("部屋の左端へ寄るのにかける秒数（撃つ位置が ArenaLeft のとき）")]
    [SerializeField, Min(0f)] float _beamMoveTime = 0.8f;
    [Tooltip("いちばん太いときの何割より太い間、判定を出すか（細い間は当たらない）")]
    [SerializeField, Range(0f, 1f)] float _beamHitThreshold = 0.5f;
    [Tooltip("予告のコマ（電流。横に並べて通り道に流す）。空なら細い線で予告する")]
    [SerializeField] Sprite[] _telegraphFrames;

    // ───────── チャット（Chat） ─────────
    [Header("チャット（Chat のとき。Slack・Discord・LINE）")]
    [Tooltip("2つ目以降のメッセージの絵（続けて送ったメッセージ）。1つ目は「見た目」の絵。空なら1つ目と同じ絵")]
    [SerializeField] Sprite _chatBodySprite;
    [Tooltip("メッセージの間のすき間（マス）。マイナスなら重ねて、1つにつながって見える（Slack・Discord）")]
    [SerializeField] float _chatGap = 0.1f;
    [Tooltip("止まっている間に、クールタイムなしで出せるメッセージの数")]
    [SerializeField, Min(1)] int _chatMaxMessages = 3;
    [Tooltip("最後にメッセージを足してから、一斉に飛んでいくまでの秒数（足すたびに数え直す）")]
    [SerializeField, Min(0f)] float _chatFireDelay = 1f;
    [Tooltip("いちばん新しいメッセージ（いちばん下）の真ん中を出す位置。持ち主の足元から（x は向きの前を正）。メッセージの幅の半分より大きくすると、体に重ならない")]
    [SerializeField] Vector2 _chatAnchor = new Vector2(2.1f, 1.1f);
    [Tooltip("メッセージが当たったときのエフェクト（コマ）")]
    [SerializeField] Sprite[] _chatHitFrames;

    // ───────── 筆（Brush） ─────────
    [Header("筆（Brush のとき。Photoshop）")]
    [Tooltip("筆の長さ（マス）。判定も同じ長さ。絵は「見た目」の絵（右向き。原点は持ち手の端）")]
    [SerializeField, Min(0.2f)] float _brushLength = 2.4f;
    [Tooltip("筆の太さ（判定の太さ、マス）")]
    [SerializeField, Min(0.05f)] float _brushWidth = 0.45f;
    [Tooltip("振るのにかける秒数")]
    [SerializeField, Min(0.02f)] float _brushSwingTime = 0.2f;
    [Tooltip("振り始めと振り終わりの角度（度。右向きのとき、0＝前、マイナス＝下、プラス＝上）")]
    [SerializeField] Vector2 _brushAngles = new Vector2(-75f, 80f);
    [Tooltip("筆を持つ位置（持ち主の足元から。x は向きの前を正）")]
    [SerializeField] Vector2 _brushPivot = new Vector2(0.25f, 0.55f);
    [Tooltip("飛び出すインクの攻撃データ（ダメージ・ゲージ・大きさ「形の大きさ」・残る秒数「持続時間」・当たり直す間隔「ヒット間隔」・絵）。空ならインクは出ない")]
    [SerializeField] AttackData _brushInk;
    [Tooltip("1回振るあいだに飛び出すインクの数")]
    [SerializeField, Min(0)] int _brushInkCount = 3;
    [Tooltip("インクが飛ぶ距離（マス）。飛んだ先にとどまる")]
    [SerializeField, Min(0f)] float _brushInkFling = 0.8f;
    [Tooltip("インクが飛ぶいちばん短い距離（マス）。上の距離との間でばらける（同じなら、いつも同じ距離）")]
    [SerializeField, Min(0f)] float _brushInkFlingMin = 0.8f;
    [Tooltip("オン：インクを前の方へ広くまき散らす（Photoshop のボス）。オフ：振る向きへ飛ばす")]
    [SerializeField] bool _brushInkScatter;
    [Tooltip("筆そのものにも判定があるか（Photoshop のボスはオフ。インクだけで攻撃する）")]
    [SerializeField] bool _brushHits = true;
    [Tooltip("オン：振る前に、部屋の端（狙う相手と反対側）へ寄る（寄っている間と振っている間は、体当たり・被弾なし）")]
    [SerializeField] bool _brushMoveToEdge;
    [Tooltip("部屋の端へ寄るのにかける秒数")]
    [SerializeField, Min(0f)] float _brushMoveTime = 0.8f;

    // ───────── 画面のフィルター（Filter） ─────────
    [Header("画面のフィルター（Filter のとき。Photoshop のボスの大技）")]
    [Tooltip("状態異常が続く秒数（そのあと画面の色も元に戻る）")]
    [SerializeField, Min(1f)] float _filterDuration = 30f;
    [Tooltip("一度にかける状態異常の数（速い・遅い・クールタイムが長い・ジャンプが高い から、ランダムに。速いと遅いは同時にかけない）")]
    [SerializeField, Range(1, 3)] int _filterEffectCount = 2;
    [Tooltip("画面の色（ランダムに1つ。半透明で重ねる）")]
    [SerializeField] Color[] _filterTints;

    // ───────── パケット（Packet） ─────────
    [Header("パケット（Packet のとき。ブラウザの能力）")]
    [Tooltip("ロックオンする敵を探す範囲（持ち主からの半径、マス）。いなければ、前へまっすぐ撃つ")]
    [SerializeField, Min(0.5f)] float _packetRange = 12f;
    [Tooltip("パケットを1発ずつ撃つ間隔（秒）。数は「弾」の「1回に撃つ数」、最初に広がる角度は「扇の角度」")]
    [SerializeField, Min(0f)] float _packetInterval = 0.06f;
    [Tooltip("1秒に曲がれる角度（度）。大きいほど、よく追いかける")]
    [SerializeField, Min(0f)] float _packetTurnRate = 540f;
    [Tooltip("ロックオンした敵が倒れたとき、応答のパケットが届いて回復する量（ライフ）。0なら回復しない")]
    [SerializeField, Min(0)] int _packetHeal = 1;
    [Tooltip("応答のパケットの絵")]
    [SerializeField] Sprite _packetResponseSprite;
    [Tooltip("ロックオンして IP アドレスを出してから、撃ち始めるまでの秒数（ブラウザのボス。プレイヤーは0）")]
    [SerializeField, Min(0f)] float _packetLockTime;
    [Tooltip("オン：パケットを攻撃で壊せる（ブラウザのボスのパケット）")]
    [SerializeField] bool _packetDestructible;

    // ───────── 爆弾のサイト（BombSite） ─────────
    [Header("爆弾のサイト（BombSite のとき。ブラウザのボス）")]
    [Tooltip("読み込みバーが、最初にゆっくりたまる割合（0〜1）。ここまでを「予告時間」かけてため、残りを下の秒数で一気にためる")]
    [SerializeField, Range(0f, 1f)] float _bombSlowPortion = 0.2f;
    [Tooltip("残りを一気にためる秒数")]
    [SerializeField, Min(0.02f)] float _bombFillTime = 0.25f;
    [Tooltip("爆発の絵（大きさは「形の大きさ」の x＝直径）。ウィンドウの絵は「見た目」の絵、大きさは「落下物の大きさ」、数は「出す数」")]
    [SerializeField] Sprite _bombBlastSprite;

    // ───────── 通知のアラーム（Alarm） ─────────
    [Header("通知のアラーム（Alarm のとき。チャットのボスの大技）")]
    [Tooltip("ベルが鳴る回数。最後に鳴ったときに、通知のベルが残っていれば部屋全体に攻撃（ベルの数は「出す数」）")]
    [SerializeField, Min(1)] int _alarmRings = 3;
    [Tooltip("始まってから、1回目に鳴るまでの秒数")]
    [SerializeField, Min(0f)] float _alarmFirstRing = 1.6f;
    [Tooltip("鳴る間隔（秒）")]
    [SerializeField, Min(0.2f)] float _alarmRingInterval = 2.2f;
    [Tooltip("回収する通知のベルのコマ（0：止まっている、1〜：揺れる）。空なら「見た目」の絵")]
    [SerializeField] Sprite[] _alarmBellFrames;
    [Tooltip("ベルの右上の、残りのベルの数のバッジ（0 番目＝1、1 番目＝2…）")]
    [SerializeField] Sprite[] _alarmBadgeFrames;

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

    public Sprite[] SummonFrames => _summonFrames ?? System.Array.Empty<Sprite>();
    public float SummonFps => _summonFps;
    public float SummonLifetime => _summonLifetime;
    public float SummonFireTime => _summonFireTime;
    public float SummonRange => _summonRange;
    public Vector2 SummonHoverOffset => _summonHoverOffset;
    public float SummonFollowSharpness => _summonFollowSharpness;
    public float BeamWidth => _beamWidth;
    public SummonType SummonType => _summonType;
    public int SummonCount => _summonCount;
    public float SummonSpeed => _summonSpeed;
    public float SummonTurnRate => _summonTurnRate;
    public BeamStyle BeamStyle => _beamStyle;
    public BeamOrigin BeamOrigin => _beamOrigin;
    public float BeamMoveTime => _beamMoveTime;
    public float BeamHitThreshold => _beamHitThreshold;
    public Sprite[] TelegraphFrames => _telegraphFrames ?? System.Array.Empty<Sprite>();
    public Sprite ChatBodySprite => _chatBodySprite != null ? _chatBodySprite : _sprite;
    public float ChatGap => _chatGap;
    public int ChatMaxMessages => _chatMaxMessages;
    public float ChatFireDelay => _chatFireDelay;
    public Vector2 ChatAnchor => _chatAnchor;
    public Sprite[] ChatHitFrames => _chatHitFrames ?? System.Array.Empty<Sprite>();
    public bool FixedColors => _fixedColors;
    public float PacketRange => _packetRange;
    public float PacketInterval => _packetInterval;
    public float PacketTurnRate => _packetTurnRate;
    public int PacketHeal => _packetHeal;
    public Sprite PacketResponseSprite => _packetResponseSprite;
    public float PacketLockTime => _packetLockTime;
    public bool PacketDestructible => _packetDestructible;
    public float BombSlowPortion => _bombSlowPortion;
    public float BombFillTime => _bombFillTime;
    public Sprite BombBlastSprite => _bombBlastSprite;
    public float BrushLength => _brushLength;
    public float BrushWidth => _brushWidth;
    public float BrushSwingTime => _brushSwingTime;
    public Vector2 BrushAngles => _brushAngles;
    public Vector2 BrushPivot => _brushPivot;
    public AttackData BrushInk => _brushInk;
    public int BrushInkCount => _brushInkCount;
    public float BrushInkFling => _brushInkFling;
    public float BrushInkFlingMin => _brushInkFlingMin;
    public bool BrushInkScatter => _brushInkScatter;
    public bool BrushHits => _brushHits;
    public bool BrushMoveToEdge => _brushMoveToEdge;
    public float BrushMoveTime => _brushMoveTime;
    public bool SummonMarquee => _summonMarquee;
    public float FilterDuration => _filterDuration;
    public int FilterEffectCount => _filterEffectCount;
    public Color[] FilterTints => _filterTints ?? System.Array.Empty<Color>();
    public GameObject SummonPrefab => _summonPrefab;
    public int SummonMaxAlive => _summonMaxAlive;
    public int AlarmRings => _alarmRings;
    public float AlarmFirstRing => _alarmFirstRing;
    public float AlarmRingInterval => _alarmRingInterval;
    public Sprite[] AlarmBellFrames => _alarmBellFrames ?? System.Array.Empty<Sprite>();
    public Sprite[] AlarmBadgeFrames => _alarmBadgeFrames ?? System.Array.Empty<Sprite>();

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
        if (_fixedColors) return this; // 塗り替えない技（通知の赤など）
        var copy = Instantiate(this);
        copy.name = name;
        if (recolor != null && _sprite != null) copy._sprite = recolor(_sprite);
        copy._color = new Color(1f, 1f, 1f, _color.a);
        if (_onTerrainHit != null) copy._onTerrainHit = _onTerrainHit.CreateRecoloredCopy(recolor);
        if (recolor != null)
        {
            // 召喚する物の絵・予告のコマも塗り替える（AI チャットのボスのミニロボット・電流。2026-11-19）
            copy._summonFrames = RecolorAll(_summonFrames, recolor);
            copy._telegraphFrames = RecolorAll(_telegraphFrames, recolor);
            // チャットのメッセージ（2つ目以降の絵・当たったときのエフェクト。チャットのボス）
            if (_chatBodySprite != null) copy._chatBodySprite = recolor(_chatBodySprite);
            copy._chatHitFrames = RecolorAll(_chatHitFrames, recolor);
            // 筆から飛ぶインク（Photoshop のボス）
            if (_brushInk != null) copy._brushInk = _brushInk.CreateRecoloredCopy(recolor);
        }
        return copy;
    }

    /// <summary>
    /// チャットのメッセージの間のすき間を変える（CreateRecoloredCopy で作った複製にだけ使う。チャットのボスで、アプリごとに変える。2026-11-19）
    /// </summary>
    public void SetChatGap(float gap) => _chatGap = gap;

    static Sprite[] RecolorAll(Sprite[] frames, System.Func<Sprite, Sprite> recolor)
    {
        if (frames == null) return null;
        var result = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++) result[i] = frames[i] != null ? recolor(frames[i]) : null;
        return result;
    }
}
