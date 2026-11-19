using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのキットのアセット（2026-11-19。docs/app-kits-1-design.md 9章）。担当2。
/// ボス：浮く（Chrome の体）。第1形態＝予定ブロック・締め切りの線、第2形態＝予定ブロック（5列・ダブルブッキング）・締め切りの線・毎週の予定。
/// 能力：予定を入れる。実行時の登録は Kits/Calendar/CalendarKit.cs（ラベル "Calendar"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class CalendarKitAssets : KitAssetBuilder
{
    public override string Label => "Calendar";
    public override string[] AppNames => new[] { "Calendar", "カレンダー" };
    public override string[] BundleIds => new[] { "com.apple.iCal" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Blocks(1), Deadline() };
    public override AttackData[] Phase2() => new[] { Blocks(2), Deadline(), Weekly() };
    public override AttackData PlayerSkill() => PlanEvent();

    public override string SkillName => "予定を入れる";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "足元に予定を置く。3秒後、その場所に大きい一撃";
    public override string[] SkillTags => new[] { "設置", "大ダメージ" };

    // ───────── ボスの技 ─────────

    /// <summary>予定ブロック：7列のうち 4列（第2形態は 5列＋ダブルブッキング）。ブロック 2.5×1.5・毎秒 12マス</summary>
    AttackData Blocks(int phase)
    {
        var custom = Custom<CalendarBlocksAttack>($"Calendar_Blocks_P{phase}", so =>
        {
            so.FindProperty("_filled").intValue = phase >= 2 ? 5 : 4;
            so.FindProperty("_placeInterval").floatValue = 0.2f;
            so.FindProperty("_hold").floatValue = 0.6f;
            so.FindProperty("_dropInterval").floatValue = 0.3f;
            so.FindProperty("_doubleBook").boolValue = phase >= 2;
            so.FindProperty("_doubleDelay").floatValue = 0.45f;
        });
        return CustomData($"Attack_Calendar_Blocks_P{phase}", custom, so =>
        {
            so.FindProperty("_objectSize").vector2Value = new Vector2(2.5f, 1.5f);
            so.FindProperty("_fallSpeed").floatValue = 12f;
            so.FindProperty("_landRemainTime").floatValue = 0f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "0A84FF");
        });
    }

    /// <summary>締め切りの線：床の全部に予告 2.5秒 → 高さ 2.5 まで判定 0.8秒。場に残る技</summary>
    AttackData Deadline()
    {
        var custom = Custom<CalendarDeadlineAttack>("Calendar_Deadline", so =>
        {
            so.FindProperty("_telegraph").floatValue = 2.5f;
            so.FindProperty("_active").floatValue = 0.8f;
            so.FindProperty("_height").floatValue = 2.5f;
        });
        return CustomData("Attack_Calendar_Deadline", custom, so =>
        {
            so.FindProperty("_lingering").boolValue = true;
            so.FindProperty("_damage").intValue = 1;
        });
    }

    /// <summary>毎週の予定（第2形態）：柱 2×5、予告 0.8秒・判定 0.4秒。0.9秒おきに 1本ずつ増える（3回）</summary>
    AttackData Weekly()
    {
        var custom = Custom<CalendarWeeklyAttack>("Calendar_Weekly_P2", so =>
        {
            so.FindProperty("_waves").intValue = 3;
            so.FindProperty("_interval").floatValue = 0.9f;
            so.FindProperty("_sameSpot").floatValue = 0.6f;
        });
        return CustomData("Attack_Calendar_Weekly_P2", custom, so =>
        {
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.TargetPosition;
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(2f, 5f);
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_activeTime").floatValue = 0.4f;
            so.FindProperty("_persistDuration").floatValue = 0f;
            so.FindProperty("_areaCount").intValue = 1;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "FF3B30");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>予定を入れる：置いて 3秒後に 5×4 の判定（0.4秒）。ゲージ +20・雑魚 5・クールタイム 5秒。赤い予告は出さない</summary>
    AttackData PlanEvent()
    {
        var custom = Custom<CalendarEventSkill>("Calendar_Event", so =>
        {
            so.FindProperty("_delay").floatValue = 3f;
        });
        return CustomData("Attack_App_Calendar_Event", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_damage").intValue = 5;
            so.FindProperty("_gaugeGain").intValue = 20;
            so.FindProperty("_knockback").vector2Value = new Vector2(4f, 6f);
            so.FindProperty("_hitStun").floatValue = 0.25f;
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.Forward;
            so.FindProperty("_forwardDistance").floatValue = 0f;
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(5f, 4f);
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_telegraphTime").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.4f;
            so.FindProperty("_persistDuration").floatValue = 0f;
            so.FindProperty("_sprite").objectReferenceValue = null;
            so.FindProperty("_color").colorValue = new UnityEngine.Color(0.04f, 0.52f, 1f, 0.45f);
        });
    }
}
