using UnityEngine;

/// <summary>
/// 瞬間移動（Warp）のときに、消える・現れる・体当たりを止める動きをする持ち主（ボスなど）。
/// 付いていない持ち主（プレイヤー）は、見た目を変えずに一瞬で移動する。
/// </summary>
public interface IWarpable
{
    /// <summary>体の範囲（出現の予告の大きさに使う）</summary>
    Bounds BodyBounds { get; }

    /// <summary>見た目の濃さ（0＝見えない、1＝ふつう）</summary>
    void SetVisibility(float alpha);

    /// <summary>体当たりの判定と被弾範囲のオン・オフ</summary>
    void SetContactEnabled(bool enabled);

    /// <summary>指定した位置へ移動する</summary>
    void Teleport(Vector2 position);
}
