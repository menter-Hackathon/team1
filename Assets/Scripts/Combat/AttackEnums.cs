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
