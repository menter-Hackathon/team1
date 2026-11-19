using UnityEngine;

/// <summary>
/// 1回の攻撃で相手に渡す情報。
/// 値は Hitbox が持っている（AttackData から Hitbox.Setup で設定する）。
/// </summary>
public struct DamageInfo
{
    /// <summary>ダメージ量。プレイヤー（ライフ制）は量に関係なく1減る</summary>
    public int Amount;
    /// <summary>ボスのウイルス解除ゲージを増やす量（HPを持たないボスは Amount ではなくこちらを見る）</summary>
    public int GaugeGain;
    /// <summary>攻撃した側の陣営</summary>
    public Team SourceTeam;
    /// <summary>吹き飛ばす速度（向きを含む）。ボスなど吹き飛ばない相手は無視する</summary>
    public Vector2 Knockback;
    /// <summary>のけぞって操作できない秒数</summary>
    public float HitStun;
    /// <summary>当たった位置（エフェクトを出す場所）</summary>
    public Vector2 HitPoint;
    /// <summary>攻撃した本人（弾なら撃った人）。無い場合は null</summary>
    public GameObject Source;
    /// <summary>攻撃のスキン（どのアプリの見た目か。2026-11-18：当たったときのエフェクト）。入っていなければ None</summary>
    public SkinId Skin;
    /// <summary>エラーの絵の種類（短い名前。空でよい。Terminal の技ごとの字など）</summary>
    public string ErrorKind;
    /// <summary>攻撃データ（当たった絵を、能力・技ごとに変えるとき用。無い場合は null）</summary>
    public AttackData Data;
}
