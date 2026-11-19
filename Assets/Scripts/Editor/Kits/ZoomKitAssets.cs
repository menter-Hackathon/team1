using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Zoom のキットのアセット（2026-11-19。docs/app-kits-1-design.md 18章）。担当2。ボスだけ。
/// ボス：ワープ（VS Code の体）。第1形態＝ギャラリービュー・退出ボタン、第2形態＝ギャラリービュー（4タイル）・退出ボタン・リアクションの嵐。
/// 能力：作らない（PlayerSkill＝null）。AbilityCatalog の Zoom の行（ギャラリービュー）はそのまま残る。
/// 実行時の登録は Kits/Zoom/ZoomKit.cs（ラベル "Zoom"）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class ZoomKitAssets : KitAssetBuilder
{
    public override string Label => "Zoom";
    public override string[] AppNames => new[] { "Zoom", "zoom.us" };
    public override string[] BundleIds => new[] { "us.zoom.xos" };
    public override bool HoverBoss => false;

    public override AttackData[] Phase1() => new[] { Gallery(1), Leave(1) };
    public override AttackData[] Phase2() => new[] { Gallery(2), Leave(2), Claps() };
    // 能力はギャラリービューのまま（AbilityCatalog の Zoom の行を変えない）
    public override AttackData PlayerSkill() => null;

    // ───────── ボスの技 ─────────

    /// <summary>ギャラリービュー：6タイルのうち 3つ（第2形態 4つ）。緑の枠 0.5秒おき → 赤い予告 0.8秒 → 判定 0.4秒</summary>
    AttackData Gallery(int phase)
    {
        var custom = Custom<ZoomGalleryAttack>($"Zoom_Gallery_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 4 : 3;
            so.FindProperty("_interval").floatValue = 0.5f;
            so.FindProperty("_frameLead").floatValue = 0.5f;
            so.FindProperty("_bottomTop").floatValue = 2.8f;
            so.FindProperty("_top").floatValue = 8f;
        });
        return CustomData($"Attack_Zoom_Gallery_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "E02828");
        });
    }

    /// <summary>退出ボタン：赤い退出のボタン（3×1.5）が、プレイヤーの位置に 0.5秒おきに 2個（第2形態 3個）。影の予告 0.8秒</summary>
    AttackData Leave(int phase)
    {
        AttackData inner = Data("Attack_Zoom_Leave_Drop", AttackKind.Spawn, so =>
        {
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(3f, 1.5f);
            so.FindProperty("_fallSpeed").floatValue = 13f;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 0f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_minDistanceFromTarget").floatValue = 0f;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "E02828");
        });
        var custom = Custom<CallDropRepeat>($"Zoom_Leave_P{phase}", so =>
        {
            so.FindProperty("_inner").objectReferenceValue = inner;
            so.FindProperty("_count").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_interval").floatValue = 0.5f;
            so.FindProperty("_art").stringValue = "zoom.leave";
            so.FindProperty("_exclaim").boolValue = true;
        });
        return CustomData($"Attack_Zoom_Leave_P{phase}", custom);
    }

    /// <summary>リアクションの嵐（第2形態）：拍手 5つ（1.4×1.4・毎秒 9マス）。1つ目はプレイヤーの真上、残りは散らばる</summary>
    AttackData Claps()
    {
        var custom = Custom<ZoomClapStorm>("Zoom_ClapStorm_P2", so =>
        {
            so.FindProperty("_count").intValue = 5;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_spread").floatValue = 2.5f;
        });
        return CustomData("Attack_Zoom_ClapStorm_P2", custom, so =>
        {
            so.FindProperty("_objectSize").vector2Value = new Vector2(1.4f, 1.4f);
            so.FindProperty("_fallSpeed").floatValue = 9f;
            so.FindProperty("_landRemainTime").floatValue = 0f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "FFCC4D");
        });
    }
}
