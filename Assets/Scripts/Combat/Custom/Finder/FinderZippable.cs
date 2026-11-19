using UnityEngine;

/// <summary>
/// 【仮】zip圧縮（Finder の能力。2026-11-18。docs/app-boss-skills-design.md 2-3）で吸いこめる物の印。
/// 敵の弾（Projectile）と雑魚（Health）は印が無くても吸える。弾の部品を使わない「ダメージのない弾」など
/// （スマートフォルダのタグの弾。ほかの技の、Projectile でない弾も）に付ける。吸われると、この GameObject ごと消える。
/// 吸う範囲は Physics2D.OverlapBox で探すので、同じ GameObject か子に Collider2D（トリガーでよい）が要る。コライダーが無いと見つからない
/// </summary>
public class FinderZippable : MonoBehaviour
{
}
