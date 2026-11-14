/// <summary>
/// ダメージを受けられるもの（プレイヤー・敵・壊せる物など）。
/// </summary>
public interface IDamageable
{
    Team Team { get; }

    /// <summary>
    /// ダメージを与える。実際に当たったら true。
    /// 無敵中・同じ陣営・すでに倒れている場合は false（弾の消滅やヒットSEの判定に使う）。
    /// </summary>
    bool TryTakeDamage(in DamageInfo info);
}
