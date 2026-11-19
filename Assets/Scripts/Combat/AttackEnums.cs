/// <summary>
/// 攻撃の種類（部品）。docs/app-boss-and-ability-design.md の6〜7章。
/// アセットには数値で保存されるので、値は明示的に書いておく。項目を消すときは欠番にして、ほかの値は変えないこと。
/// </summary>
public enum AttackKind
{
    /// <summary>弾（部品 A）</summary>
    Projectile = 0,
    /// <summary>指定位置の判定（部品 E）。持続時間を入れると、残り続けるダメージ床（部品 H）になる</summary>
    Area = 1,
    /// <summary>落下物・設置物（部品 F・G）</summary>
    Spawn = 2,
    /// <summary>瞬間移動（新しい種類）。ボスの移動と、能力「新しいタブ」で共用</summary>
    Warp = 3,
    /// <summary>プレイヤーの近接。持ち主に付けた Hitbox を使うので、AttackRunner では実行しない（PlayerMeleeAttack が使う）</summary>
    Melee = 4,
    /// <summary>
    /// ロックオン（2026-11-18：Zoom の能力「ギャラリービュー」）。画面に映っている相手に印を付けてから、それぞれへレーザーを撃つ。
    /// 画面に相手がいないときは使えない（クールタイムも始まらない）
    /// </summary>
    LockOn = 5,
    /// <summary>物理で動く物（Unity の Cube／Sphere／Capsule）。落ちる速さが一定以上のときだけ当たる。足場になる</summary>
    PhysicsObject = 5,
}

/// <summary>弾の狙い方</summary>
public enum AimMode
{
    /// <summary>狙う相手（ボスならプレイヤー）の方向</summary>
    Target = 0,
    /// <summary>持ち主の正面（向いている方向）</summary>
    Forward = 1,
}

/// <summary>指定位置の判定を出す場所</summary>
public enum AreaAnchor
{
    /// <summary>狙う相手（ボスならプレイヤー）の位置</summary>
    TargetPosition = 0,
    /// <summary>持ち主の前方Nマス</summary>
    Forward = 1,
    /// <summary>弾が地形に当たった位置（弾の追撃として出すとき）</summary>
    HitPoint = 2,
    /// <summary>持ち主の前方Nマスで、高さは狙う相手に合わせる（ボスの折れ線グラフ）</summary>
    ForwardAtTargetHeight = 3,
}

/// <summary>判定の形</summary>
public enum AreaShape
{
    Box = 0,
    Circle = 1,
    /// <summary>折れ線（EdgeCollider2D）。点は AttackData の「折れ線の点」</summary>
    Polyline = 2,
}

/// <summary>落下物・設置物の置き方</summary>
public enum SpawnPlacement
{
    /// <summary>1つ目は狙う相手の真上、残りは狙う相手から一定以上離れたランダムな位置から落とす（VS Code のエラーウィンドウ）</summary>
    AroundTarget = 0,
    /// <summary>画面の上端に、左から一定間隔で並べてから、順番に落とす（タブ増殖）</summary>
    TopRow = 1,
    /// <summary>
    /// 落とさずに、その場にパッと出す（ポップアップ出現）。予告は出る場所の薄い枠。
    /// 1つ目は狙う相手の体に重なる位置、残りは狙う相手から一定以上離れた空中のランダムな位置
    /// </summary>
    AppearAroundTarget = 2,
    /// <summary>持ち主の前方やや上に出す（弾を出す位置のずれ）。プレイヤーの Unity スキル</summary>
    ForwardAbove = 3,
}

/// <summary>物理オブジェクトの形（コライダーの種類）</summary>
public enum PrimitiveShape
{
    Box = 0,
    Circle = 1,
    Capsule = 2,
}

/// <summary>瞬間移動の行き先</summary>
public enum WarpDestination
{
    /// <summary>前方Nマス（能力「新しいタブ」）</summary>
    Forward = 0,
    /// <summary>戦う部屋の反対側（VS Code）</summary>
    Opposite = 1,
}

/// <summary>使用時間で強くする、攻撃データの数値（M5。docs 6章「使用時間で変えるつまみ」）</summary>
public enum AttackStat
{
    /// <summary>変えない</summary>
    None = 0,
    /// <summary>瞬間移動の距離（能力「新しいタブ」）</summary>
    WarpDistance = 1,
    /// <summary>ボスへのゲージ増加量（能力「ブレークポイント」）</summary>
    GaugeGain = 2,
    /// <summary>雑魚へのダメージ</summary>
    Damage = 3,
    /// <summary>指定位置の判定を同時に出す数（能力のレベル用）</summary>
    AreaCount = 4,
    /// <summary>残り続ける判定の秒数（能力のレベル用）</summary>
    PersistDuration = 5,
    /// <summary>間隔（クールタイム）の秒数（能力のレベル用）</summary>
    Cooldown = 6,
    /// <summary>指定位置の判定の大きさの倍率（1＝元の大きさ。その場で作る能力の違いに使う）</summary>
    AreaScale = 7,
    /// <summary>1回に撃つ弾の数</summary>
    ProjectileCount = 8,
}
