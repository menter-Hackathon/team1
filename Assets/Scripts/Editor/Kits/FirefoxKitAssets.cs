using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Firefox のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-6）。担当C。
/// ボス：浮く（Chrome の体。技は Chrome の物を使わない）。第1形態＝しっぽの炎・コンテナタブ、第2形態＝しっぽの炎（5つ）・コンテナタブ（2色×3回）・ピクチャー・イン・ピクチャー。
/// 能力：広告ブロック。ステージの決まり：アドオン（Kits/Firefox/FirefoxRule.cs）。実行時の登録は Kits/Firefox/FirefoxKit.cs（ラベル "Firefox"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class FirefoxKitAssets : KitAssetBuilder
{
    public override string Label => "Firefox";
    public override string[] AppNames => new[] { "Firefox", "Firefox Developer Edition", "Firefox Nightly" };
    public override string[] BundleIds => new[] { "org.mozilla.firefox" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { TailFlame(1), Containers(1) };
    public override AttackData[] Phase2() => new[] { TailFlame(2), Containers(2), Pip() };
    public override AttackData PlayerSkill() => AdBlock();

    public override string SkillName => "広告ブロック";
    public override SkillCategory Category => SkillCategory.Browser;
    public override string SkillDescription => "2.5秒の盾。敵の弾を消し、最初に防いだ弾は相手へ打ち返す";
    public override string[] SkillTags => new[] { "盾", "反撃" };

    /// <summary>しっぽの炎：火の玉 3つ（影 0.8秒）→ 炎 1.5秒（幅 1.5・高さ 1）。第2形態は 5つ・炎 2.5秒</summary>
    AttackData TailFlame(int phase)
    {
        AttackData flame = Data($"Attack_Firefox_TailFire_P{phase}", AttackKind.Area, so =>
        {
            so.FindProperty("_shape").intValue = (int)AreaShape.Box;
            so.FindProperty("_areaSize").vector2Value = new Vector2(1.5f, 1f);
            so.FindProperty("_areaCount").intValue = 1;
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_telegraphTime").floatValue = 0f;
            so.FindProperty("_activeTime").floatValue = 0.2f;
            so.FindProperty("_persistDuration").floatValue = phase >= 2 ? 2.5f : 1.5f;
            so.FindProperty("_tickInterval").floatValue = 0.5f;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FF7139");
        });
        var custom = Custom<FirefoxTailFlame>($"Firefox_TailFlame_P{phase}", so =>
        {
            so.FindProperty("_flame").objectReferenceValue = flame;
            so.FindProperty("_count").intValue = phase >= 2 ? 5 : 3;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_stagger").floatValue = 0.15f;
            so.FindProperty("_spacing").floatValue = 2.6f;
        });
        return CustomData($"Attack_Firefox_TailFlame_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FF7139");
        });
    }

    /// <summary>コンテナタブ：4色の区画。タブの色の区画に赤い枠 1.2秒 → 炎の柱。第1形態 1色×2回、第2形態 2色×3回（並びが毎回変わる）</summary>
    AttackData Containers(int phase)
    {
        var custom = Custom<FirefoxContainers>($"Firefox_Containers_P{phase}", so =>
        {
            so.FindProperty("_rounds").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_colors").intValue = phase >= 2 ? 2 : 1;
            so.FindProperty("_telegraph").floatValue = 1.2f;
            so.FindProperty("_lead").floatValue = 0.35f;
            so.FindProperty("_reshuffle").boolValue = phase >= 2;
        });
        return CustomData($"Attack_Firefox_Containers_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "7542E5");
        });
    }

    /// <summary>ピクチャー・イン・ピクチャー（第2形態）：窓がはがれ（0.8秒）、毎秒 3.5マスで 4秒追う → 点滅 0.8秒 → 半径 1.5 ではじける</summary>
    AttackData Pip()
    {
        var custom = Custom<FirefoxPip>("Firefox_Pip_P2", so =>
        {
            so.FindProperty("_peel").floatValue = 0.8f;
            so.FindProperty("_chaseSpeed").floatValue = 3.5f;
            so.FindProperty("_chaseTime").floatValue = 4f;
            so.FindProperty("_blink").floatValue = 0.8f;
            so.FindProperty("_radius").floatValue = 1.5f;
            so.FindProperty("_hitWindow").floatValue = 0.15f;
            so.FindProperty("_size").vector2Value = new Vector2(2f, 1.5f);
        });
        return CustomData("Attack_Firefox_Pip_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FF453A");
        });
    }

    /// <summary>広告ブロック：2.5秒の盾（半径 1.25）。最初に防いだ弾を打ち返す（雑魚 1・ゲージ 14）。クールタイム 7秒</summary>
    AttackData AdBlock()
    {
        AttackData counter = Data("Attack_App_Firefox_Counter", AttackKind.Projectile, so =>
        {
            so.FindProperty("_speed").floatValue = 15f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.7f, 0.7f);
            so.FindProperty("_lifetime").floatValue = 1.6f;
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_projectileCount").intValue = 1;
            so.FindProperty("_repeatCount").intValue = 1;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_gaugeGain").intValue = 14;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<FirefoxAdBlock>("Firefox_AdBlock", so =>
        {
            so.FindProperty("_counter").objectReferenceValue = counter;
            so.FindProperty("_seconds").floatValue = 2.5f;
            so.FindProperty("_radius").floatValue = 1.25f;
        });
        return CustomData("Attack_App_Firefox_AdBlock", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 7f;
            so.FindProperty("_gaugeGain").intValue = 14;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "7542E5");
        });
    }
}
