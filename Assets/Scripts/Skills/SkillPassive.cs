using System;
using UnityEngine;

/// <summary>
/// 装備している間ずっと効く効果（スキルの「常に効く」部分）。SkillDefinition の「装備中ずっと効く効果」に入れる。
/// ・プレイヤー：PlayerSkillPassives が、枠に入ったときに Attach し、枠から外れたら戻り値を Dispose する
/// ・ボス：BossController が、戦闘を始めたときに Attach し、正常化・やり直しで Dispose する（BossProfile の「ボスの常に効く効果」）
/// 作るのは派生クラス（例：AchievementPassive＝Steam の実績解除）。
/// </summary>
public abstract class SkillPassive : ScriptableObject
{
    /// <summary>owner に効果を付ける。外すときは戻り値を Dispose する</summary>
    public abstract IDisposable Attach(GameObject owner);
}
