using UnityEngine;

/// <summary>
/// 【仮】Terminal の技が出した、ふつうの部品の物（アクセス拒否の柱＝AreaStrike など）に付ける目印（2026-11-18）。
/// CustomEffect が付いた物は、ボスの形態変化（50%）でも消える（AttackRunner.ClearSpawnedWith&lt;CustomEffect&gt;）。
/// 再起動の演出の 3.5秒の間に、柱が当たらないようにするため（docs/app-boss-skills-design.md 4-4 の OK の条件）
/// </summary>
public class TerminalClearMarker : CustomEffect
{
}
