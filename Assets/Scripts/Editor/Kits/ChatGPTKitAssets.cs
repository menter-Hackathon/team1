using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】ChatGPT のキットのアセット（2026-11-19。docs/app-kits-1-design.md 5章）。担当3 の物。
/// ・ボス（ワープ型）：第1形態＝長文回答・ハルシネーション。第2形態＝Regenerate・ハルシネーション（ウソ 3体）・長文回答
/// ・能力：プロンプト（考え中 0.5秒 → 前方 3マスに太い柱。ゲージ +18・クールタイム 4.5秒）
/// ・長文回答・プロンプトの柱は、名前に Breakpoint を入れて AI のスキンの柱の枠（入力の欄 → 長い回答）を付ける（20-4 の「そのまま」）
/// ・数値はここが正（メニューをかけ直すたびに入れ直す）。このファイルの中の Color は KitAssetBuilder の道具（16進を入れる）
/// </summary>
public class ChatGPTKitAssets : KitAssetBuilder
{
    const string VSCodeBreakpoint = "Assets/ScriptableObjects/Attacks/VSCode/Attack_VSCode_Breakpoint_P1.asset";
    const string AbilityBreakpoint = "Assets/ScriptableObjects/Attacks/Player/Attack_Ability_Breakpoint.asset";

    public override string Label => ChatGPTKit.Label;
    public override string[] AppNames => ChatGPTKit.AppNames;
    public override string[] BundleIds => ChatGPTKit.BundleIds;
    public override bool HoverBoss => false;
    public override string SkillName => "プロンプト";
    public override SkillCategory Category => SkillCategory.AI;
    public override string SkillDescription => "考えてから、前方に太い柱を立てる。出るのは遅いが、とても強い。";
    public override string[] SkillTags => new[] { "ため", "大ダメージ" };
    public override SkinId SkillSkin => SkinId.AI;

    public override AttackData[] Phase1() => new[] { LongAnswer(), Hallucination(1) };
    public override AttackData[] Phase2() => new[] { Regenerate(), Hallucination(2), LongAnswer() };
    public override AttackData PlayerSkill() => Prompt();

    // ───────── ボスの技 ─────────

    /// <summary>長文回答：プレイヤーの足元に入力の欄（予告 0.8秒）→ 幅 3・天井までの柱 0.6秒。欄から 1.9マス動けば当たらない</summary>
    AttackData LongAnswer() => CopyOrData(VSCodeBreakpoint, "Attack_ChatGPT_LongAnswer_Breakpoint_P1", so =>
    {
        so.FindProperty("_kind").intValue = (int)AttackKind.Area;
        so.FindProperty("_windup").floatValue = 0f;
        so.FindProperty("_activeTime").floatValue = 0.6f;
        BossHit(so);
        so.FindProperty("_anchor").intValue = (int)AreaAnchor.TargetPosition;
        so.FindProperty("_snapToGround").boolValue = true;
        so.FindProperty("_shape").intValue = (int)AreaShape.Box;
        so.FindProperty("_areaSize").vector2Value = new Vector2(3f, 11.5f);
        so.FindProperty("_areaCount").intValue = 1;
        so.FindProperty("_areaStagger").floatValue = 0f;
        so.FindProperty("_telegraphTime").floatValue = 0.8f;
        so.FindProperty("_persistDuration").floatValue = 0f;
        so.FindProperty("_lingering").boolValue = false;
    });

    /// <summary>ハルシネーション：ウソ 2体（第2形態 3体）と同時に、言葉の弾 3発ずつ。本物の弾だけが当たる</summary>
    AttackData Hallucination(int phase)
    {
        var custom = Custom<ChatGptHallucinationAttack>($"ChatGPT_Hallucination_P{phase}", so =>
        {
            so.FindProperty("_fakeCount").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_appearTime").floatValue = 0.6f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_shots").intValue = 3;
            so.FindProperty("_spread").floatValue = 30f;
            so.FindProperty("_vanishDelay").floatValue = 0.35f;
            so.FindProperty("_fakeAlpha").floatValue = 0.7f;
        });
        return CustomData($"Attack_ChatGPT_Hallucination_P{phase}", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_speed").floatValue = 7f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.5f, 0.35f);
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_spawnOffset").vector2Value = Vector2.zero;
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時に言葉の札にする
            so.FindProperty("_lingering").boolValue = false;
            Color(so, "_color", "FFFFFF");
        });
    }

    /// <summary>Regenerate（第2形態）：回る矢印 → 長文回答の柱を、そのときのプレイヤーの位置に 3回（0.9秒おき）</summary>
    AttackData Regenerate()
    {
        AttackData pillar = LongAnswer();
        var custom = Custom<ChatGptRegenerateAttack>("ChatGPT_Regenerate", so =>
        {
            so.FindProperty("_pillar").objectReferenceValue = pillar;
            so.FindProperty("_count").intValue = 3;
            so.FindProperty("_interval").floatValue = 0.9f;
            so.FindProperty("_lead").floatValue = 0.4f;
        });
        return CustomData("Attack_ChatGPT_Regenerate_P2", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = false;
        });
    }

    // ───────── 能力 ─────────

    /// <summary>プロンプト：考え中 0.5秒 → 正面 3マス先に、幅 2×高さ 6 の柱（0.4秒）。ゲージ +18・クールタイム 4.5秒</summary>
    AttackData Prompt()
    {
        AttackData pillar = CopyOrData(AbilityBreakpoint, "Attack_App_ChatGPT_Prompt_Breakpoint", so =>
        {
            so.FindProperty("_kind").intValue = (int)AttackKind.Area;
            so.FindProperty("_windup").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.4f;
            so.FindProperty("_cooldown").floatValue = 0f;
            PlayerHit(so, 3, 18);
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.Forward;
            so.FindProperty("_forwardDistance").floatValue = 3f;
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(2f, 6f);
            so.FindProperty("_areaCount").intValue = 1;
            so.FindProperty("_areaStagger").floatValue = 0f;
            so.FindProperty("_telegraphTime").floatValue = 0f; // 予告は頭の上の「考え中」
            so.FindProperty("_persistDuration").floatValue = 0f;
        });
        var custom = Custom<ChatGptPromptAttack>("ChatGPT_Prompt", so =>
        {
            so.FindProperty("_pillar").objectReferenceValue = pillar;
            so.FindProperty("_thinkTime").floatValue = 0.5f;
        });
        return CustomData("Attack_App_ChatGPT_Prompt", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4.5f;
            PlayerHit(so, 3, 18);
        });
    }

    // ───────── 道具 ─────────

    /// <summary>今ある攻撃データを複製する（無ければ新しく作る）。init は毎回入れ直す</summary>
    AttackData CopyOrData(string source, string name, System.Action<SerializedObject> init)
    {
        AttackData d = AssetDatabase.LoadAssetAtPath<AttackData>(source) != null ? Copy(source, name, init) : null;
        return d != null ? d : Data(name, AttackKind.Area, init);
    }

    /// <summary>ボスの技の基本（ライフ -1・ノックバック (6,5)・のけぞり 0.25秒）</summary>
    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_gaugeGain").intValue = 0;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }

    /// <summary>能力の当たり（雑魚へのダメージ・ゲージ・ノックバック・のけぞり）</summary>
    static void PlayerHit(SerializedObject so, int damage, int gauge)
    {
        so.FindProperty("_damage").intValue = damage;
        so.FindProperty("_gaugeGain").intValue = gauge;
        so.FindProperty("_knockback").vector2Value = new Vector2(4f, 3f);
        so.FindProperty("_hitStun").floatValue = 0.2f;
    }
}
