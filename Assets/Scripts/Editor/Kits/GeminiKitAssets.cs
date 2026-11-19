using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Gemini のキットのアセット（2026-11-19。docs/app-kits-1-design.md 7章）。担当3 の物。
/// ・ボス（ワープ型）：第1形態＝3つの回答案・ふたご座。第2形態＝3つの回答案（2回）・ふたご座（低・高・低）・画像生成
/// ・能力：ふたご星（自分の位置と左右 2.5マスに星の柱 3本。1本 +6・クールタイム 2.5秒。絵は包み KitDressed で星の柱に差し替え）
/// ・どの攻撃データの名前にも、枠の語は入れない（絵はキットの物）
/// ・数値はここが正（メニューをかけ直すたびに入れ直す）。このファイルの中の Color は KitAssetBuilder の道具（16進を入れる）
/// </summary>
public class GeminiKitAssets : KitAssetBuilder
{
    public override string Label => GeminiKit.Label;
    public override string[] AppNames => GeminiKit.AppNames;
    public override string[] BundleIds => GeminiKit.BundleIds;
    public override bool HoverBoss => false;
    public override string SkillName => "ふたご星";
    public override SkillCategory Category => SkillCategory.AI;
    public override string SkillDescription => "自分の位置と左右に、星の柱を 3本立てる。はさまれたときに強い。";
    public override string[] SkillTags => new[] { "範囲", "前後" };
    public override SkinId SkillSkin => SkinId.AI;

    public override AttackData[] Phase1() => new[] { Drafts(1), Twin(1) };
    public override AttackData[] Phase2() => new[] { Drafts(2), Twin(2), ImageGen() };
    public override AttackData PlayerSkill() => TwinStar();

    // ───────── ボスの技 ─────────

    /// <summary>3つの回答案：床を 3つに分け、1つが青（安全）・2つが赤 → 赤い区画に星の光（高さ 5、0.5秒）。第2形態は 2回</summary>
    AttackData Drafts(int phase)
    {
        var custom = Custom<GeminiDraftsAttack>($"Gemini_Drafts_P{phase}", so =>
        {
            so.FindProperty("_rounds").intValue = phase >= 2 ? 2 : 1;
            so.FindProperty("_flicker").floatValue = 0.6f;
            so.FindProperty("_flickerNext").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_active").floatValue = 0.5f;
            so.FindProperty("_height").floatValue = 5f;
        });
        return CustomData($"Attack_Gemini_Drafts_P{phase}", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = false;
            Color(so, "_color", "FFFFFF");
        });
    }

    /// <summary>ふたご座：反対側にふたごの影 → ボスとふたごが交互に横の弾（毎秒 9マス）。第1形態 低・低、第2形態 低・高・低</summary>
    AttackData Twin(int phase)
    {
        var custom = Custom<GeminiTwinAttack>($"Gemini_Twin_P{phase}", so =>
        {
            var shots = so.FindProperty("_shots");
            int[] heights = phase >= 2
                ? new[] { (int)WallShotHeight.Low, (int)WallShotHeight.High, (int)WallShotHeight.Low }
                : new[] { (int)WallShotHeight.Low, (int)WallShotHeight.Low };
            shots.arraySize = heights.Length;
            for (int i = 0; i < heights.Length; i++) shots.GetArrayElementAtIndex(i).intValue = heights[i];
            so.FindProperty("_appearTime").floatValue = 0.8f;
            so.FindProperty("_gap").floatValue = 0.7f;
            so.FindProperty("_minApart").floatValue = 7f;
        });
        return CustomData($"Attack_Gemini_Twin_P{phase}", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_speed").floatValue = 9f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.8f, 0.5f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時に星にする
            so.FindProperty("_lingering").boolValue = false;
            Color(so, "_color", "9B72CB");
        });
    }

    /// <summary>画像生成（第2形態）：額ぶちの絵（4×3）が、プレイヤーの位置に 2枚（0.6秒おき。影の予告 0.8秒）→ 3秒、足場</summary>
    AttackData ImageGen()
    {
        AttackData frame = Data("Attack_Gemini_Image_Picture", AttackKind.Spawn, so =>
        {
            so.FindProperty("_windup").floatValue = 0f;
            BossHit(so);
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(4f, 3f);
            so.FindProperty("_fallSpeed").floatValue = 12f;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 3f;
            so.FindProperty("_becomesPlatform").boolValue = true;
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時に額ぶちにする
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<GeminiImageAttack>("Gemini_Image", so =>
        {
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_count").intValue = 2;
            so.FindProperty("_interval").floatValue = 0.6f;
        });
        return CustomData("Attack_Gemini_Image_P2", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = false;
        });
    }

    // ───────── 能力 ─────────

    /// <summary>ふたご星：自分の位置と左右 2.5マスに星の柱（幅 1.5×高さ 4、0.3秒）。1本 +6。クールタイム 2.5秒</summary>
    AttackData TwinStar()
    {
        AttackData inner = Data("Attack_App_Gemini_TwinStar_Inner", AttackKind.Area, so =>
        {
            so.FindProperty("_windup").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.3f;
            so.FindProperty("_cooldown").floatValue = 2.5f;
            PlayerHit(so, 2, 6);
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.Forward;
            so.FindProperty("_forwardDistance").floatValue = 0f;
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(1.5f, 4f);
            so.FindProperty("_areaCount").intValue = 3;
            so.FindProperty("_areaSpacing").floatValue = 2.5f;
            so.FindProperty("_areaStagger").floatValue = 0f;
            so.FindProperty("_areaForwardOrder").boolValue = false;
            so.FindProperty("_telegraphTime").floatValue = 0f;
            so.FindProperty("_persistDuration").floatValue = 0f;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "9B72CB");
        });
        return Dressed("Attack_App_Gemini_TwinStar", inner, GeminiKit.StarPillarArt, false, so =>
        {
            so.FindProperty("_cooldown").floatValue = 2.5f;
            PlayerHit(so, 2, 6);
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
