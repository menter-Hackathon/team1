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
    /// 召喚（部品 I・K。2026-11-18）。持ち主のそばに味方（AssistantDrone）を出し、味方が自分で敵を攻撃する。
    /// 能力「AIアシスタント」：AI ロボットが一定時間、範囲内の敵にレーザーを撃つ
    /// </summary>
    Summon = 5,
    /// <summary>
    /// 直線のレーザー（2026-11-19。AI チャットのボス）。通り道に予告（電流・赤い危険エリア）を出してから、
    /// その場所にレーザーを撃つ。レーザーはだんだん太くなってから細くなる。位置は変わらない（BeamStrike）
    /// </summary>
    Beam = 6,
    /// <summary>
    /// チャットのメッセージ（2026-11-19。Slack・Discord・LINE の能力）。止まっている間は、続けて押すとメッセージが積み上がり（最大数まで）、
    /// 最後に足してから決まった秒数で一斉に飛んでいく（ChatVolley）。動くか最大数まで出したら、そこからクールタイム
    /// </summary>
    Chat = 7,
    /// <summary>
    /// 通知のアラーム（2026-11-19。チャットのボスの大技）。部屋に通知を置き、ベルが決まった回数鳴るまでに全部回収しないと、
    /// 部屋全体に攻撃が来る（NotificationAlarm）。出している間、持ち主は動かない
    /// </summary>
    Alarm = 8,
    /// <summary>
    /// 筆（2026-11-19。Photoshop の能力）。持ち主の前で、筆を下から上へ振る（筆にも判定）。振る途中でインクが飛び出し、
    /// その場にしばらく残って当たり続ける（BrushSwing・InkBlot）
    /// </summary>
    Brush = 9,
    /// <summary>
    /// 画面のフィルター（2026-11-19。Photoshop のボスの大技）。画面の色を変え、狙う相手（プレイヤー）にランダムに状態異常をかける。
    /// しばらくすると元に戻る（ScreenFilter・PlayerStatusEffects）
    /// </summary>
    Filter = 10,
    /// <summary>
    /// パケット（2026-11-19。ブラウザの能力）。近くの敵を1体ロックオンし（頭の上に IP アドレス）、棒状のパケットを何発か撃って追尾させる。
    /// ロックオンした敵が倒れると、応答のパケットが持ち主へ飛んできて回復する（PacketVolley）
    /// </summary>
    Packet = 11,
    /// <summary>
    /// 爆弾のサイト（2026-11-19。ブラウザのボス）。ブラウザのウィンドウ（爆弾のマークのサイト）をあちこちに開き、
    /// 読み込みバーがたまり終わったら、その場で爆発する。ウィンドウそのものには判定がない（BombSiteWindow）
    /// </summary>
    BombSite = 12,
}

/// <summary>レーザーの見せ方（Beam のとき）</summary>
public enum BeamStyle
{
    /// <summary>ふつうのレーザー。予告は通り道を流れる電流</summary>
    Laser = 0,
    /// <summary>大技：大量の文字列が流れる巨大なレーザー。予告は赤い半透明の危険エリア</summary>
    CodeStream = 1,
}

/// <summary>レーザーを撃つ位置（Beam のとき）</summary>
public enum BeamOrigin
{
    /// <summary>持ち主の位置から（狙い方に合わせた向き）</summary>
    Owner = 0,
    /// <summary>持ち主が部屋の左端へ寄ってから、右向きに撃つ（高さは狙う相手に合わせる）</summary>
    ArenaLeft = 1,
}

/// <summary>召喚する物の種類（Summon のとき）</summary>
public enum SummonType
{
    /// <summary>味方の AI アシスタント（AssistantDrone）。消えるまで攻撃が終わらない</summary>
    Assistant = 0,
    /// <summary>敵のミニロボット（ChaserBot）。狙う相手をゆっくり追いかけ、触れるとダメージ。出したらすぐ次の行動へ</summary>
    Chaser = 1,
    /// <summary>敵の雑魚（プレハブ）を出す（2026-11-19：チャットのボスのチャットbot）。出したらすぐ次の行動へ</summary>
    Minion = 2,
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
}

/// <summary>判定の形</summary>
public enum AreaShape
{
    Box = 0,
    Circle = 1,
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
