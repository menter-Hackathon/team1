using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Claude のキットのアセット（2026-11-19。docs/app-kits-1-design.md 6章）。担当3 の物。
/// ・ボス（浮く型）：第1形態＝スパーク（2回）・Artifacts。第2形態＝じっくり考える・スパーク（3回）・Artifacts
/// ・能力：アーティファクト（正面 2.5マス先・高さ 2 にパネル。ゲージ +10・足場 4秒・クールタイム 4秒）
/// ・Artifacts は Chrome のポップアップ出現の複製（名前に PopupAppear ＝ AI のスキンの窓の枠「回答のカード」。20-4 の「そのまま」）
/// ・数値はここが正（メニューをかけ直すたびに入れ直す）。このファイルの中の Color は KitAssetBuilder の道具（16進を入れる）
/// </summary>
public class ClaudeKitAssets : KitAssetBuilder
{
    const string ChromePopup = "Assets/ScriptableObjects/Attacks/Chrome/Attack_Chrome_PopupAppear_P1.asset";

    public override string Label => ClaudeKit.Label;
    public override string[] AppNames => ClaudeKit.AppNames;
    public override string[] BundleIds => ClaudeKit.BundleIds;
    public override bool HoverBoss => true;
    public override string SkillName => "アーティファクト";
    public override SkillCategory Category => SkillCategory.AI;
    public override string SkillDescription => "前方の空中にパネルを開く。開く瞬間に当たり、しばらく足場になる。";
    public override string[] SkillTags => new[] { "足場", "対空" };
    public override SkinId SkillSkin => SkinId.AI;

    public override AttackData[] Phase1() => new[] { Spark(1), Artifacts() };
    public override AttackData[] Phase2() => new[] { Thinking(), Spark(2), Artifacts() };
    public override AttackData PlayerSkill() => Artifact();

    // ───────── ボスの技 ─────────

    /// <summary>スパーク：8方向へ赤い線 → 火花の弾 8発（毎秒 8マス）→ 22.5度ずらしてもう 1回（第2形態は 3回）</summary>
    AttackData Spark(int phase)
    {
        var custom = Custom<ClaudeSparkAttack>($"Claude_Spark_P{phase}", so =>
        {
            so.FindProperty("_volleys").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_count").intValue = 8;
            so.FindProperty("_firstOffset").floatValue = 22.5f;
            so.FindProperty("_step").floatValue = 22.5f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
        });
        return CustomData($"Attack_Claude_Spark_P{phase}", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_speed").floatValue = 8f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.4f, 0.4f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時に火花にする
            so.FindProperty("_lingering").boolValue = false;
            Color(so, "_color", "D97757");
        });
    }

    /// <summary>Artifacts：パネル（4×3）がプレイヤーの近くに 2つ開く（出現の枠 0.8秒。出る瞬間だけ当たる）→ 足場として 4秒残る</summary>
    AttackData Artifacts()
    {
        System.Action<SerializedObject> init = so =>
        {
            so.FindProperty("_kind").intValue = (int)AttackKind.Spawn;
            so.FindProperty("_windup").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.1f;
            BossHit(so);
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AppearAroundTarget;
            so.FindProperty("_spawnCount").intValue = 2;
            so.FindProperty("_minDistanceFromTarget").floatValue = 3f;
            so.FindProperty("_objectSize").vector2Value = new Vector2(4f, 3f);
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 4f;
            so.FindProperty("_becomesPlatform").boolValue = true;
            so.FindProperty("_appearTopRange").vector2Value = new Vector2(2.5f, 6f);
            so.FindProperty("_reachHeight").floatValue = 3f;
        };
        AttackData d = AssetDatabase.LoadAssetAtPath<AttackData>(ChromePopup) != null ? Copy(ChromePopup, "Attack_Claude_PopupAppear_P1", init) : null;
        return d != null ? d : Data("Attack_Claude_PopupAppear_P1", AttackKind.Spawn, init);
    }

    /// <summary>じっくり考える（第2形態）：火花が回る 2秒 → 足元に印 1・2・3 → 順に柱（幅 2×高さ 6）</summary>
    AttackData Thinking()
    {
        var custom = Custom<ClaudeThinkingAttack>("Claude_Thinking", so =>
        {
            so.FindProperty("_marks").intValue = 3;
            so.FindProperty("_markInterval").floatValue = 0.6f;
            so.FindProperty("_thinkTime").floatValue = 2f;
            so.FindProperty("_pillarInterval").floatValue = 0.4f;
            so.FindProperty("_pillarSize").vector2Value = new Vector2(2f, 6f);
            so.FindProperty("_active").floatValue = 0.4f;
        });
        return CustomData("Attack_Claude_Thinking_P2", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = false;
            Color(so, "_color", "FFFFFF");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>アーティファクト：正面 2.5マス先・高さ 2 にパネル（3×0.6）。開く瞬間ゲージ +10。足場 4秒。クールタイム 4秒</summary>
    AttackData Artifact()
    {
        AttackData panel = Data("Attack_App_Claude_Artifact_Panel", AttackKind.Spawn, so =>
        {
            so.FindProperty("_windup").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.15f;
            so.FindProperty("_cooldown").floatValue = 0f;
            PlayerHit(so, 3, 10);
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AppearAroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(3f, 0.6f);
            so.FindProperty("_telegraphTime").floatValue = 0f;
            so.FindProperty("_landRemainTime").floatValue = 4f;
            so.FindProperty("_becomesPlatform").boolValue = true;
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時にパネルにする
            Color(so, "_color", "F0EEE6");
        });
        var custom = Custom<ClaudeArtifactAttack>("Claude_Artifact", so =>
        {
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_forward").floatValue = 2.5f;
            so.FindProperty("_height").floatValue = 2f;
            so.FindProperty("_openDelay").floatValue = 0.08f;
        });
        return CustomData("Attack_App_Claude_Artifact", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            PlayerHit(so, 3, 10);
        });
    }

    // ───────── 道具 ─────────

    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_gaugeGain").intValue = 0;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }

    static void PlayerHit(SerializedObject so, int damage, int gauge)
    {
        so.FindProperty("_damage").intValue = damage;
        so.FindProperty("_gaugeGain").intValue = gauge;
        so.FindProperty("_knockback").vector2Value = new Vector2(4f, 3f);
        so.FindProperty("_hitStun").floatValue = 0.2f;
    }
}
