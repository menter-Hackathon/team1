using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Discord のキットのアセット（2026-11-19。docs/app-kits-1-design.md 3章）。担当2。
/// ボス：ワープ（VS Code の体）。第1形態＝ボイス・サーバー一覧、第2形態＝ボイス（3連）・サーバー一覧（4つ）・スタンプ連打。
/// 能力：ミュート。実行時の登録は Kits/Discord/DiscordKit.cs（ラベル "Discord"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class DiscordKitAssets : KitAssetBuilder
{
    public override string Label => "Discord";
    public override string[] AppNames => new[] { "Discord" };
    public override string[] BundleIds => new[] { "com.hnc.Discord" };
    public override bool HoverBoss => false;

    public override AttackData[] Phase1() => new[] { Voice(1), ServerList(1) };
    public override AttackData[] Phase2() => new[] { Voice(2), ServerList(2), Stamps() };
    public override AttackData PlayerSkill() => Mute();

    public override string SkillName => "ミュート";
    public override SkillCategory Category => SkillCategory.SocialChat;
    public override string SkillDescription => "前に円を出す。中の敵の弾を消し、ボスを2秒だまらせる";
    public override string[] SkillTags => new[] { "弾消し", "止める" };

    // ───────── ボスの技 ─────────

    /// <summary>ボイス：床から 0.6 の高さの音の波（毎秒 8マス・1.2×0.8）。第1形態 2連・間 0.7秒、第2形態 3連・間 0.6秒</summary>
    AttackData Voice(int phase)
    {
        var custom = Custom<DiscordVoiceAttack>($"Discord_Voice_P{phase}", so =>
        {
            so.FindProperty("_shots").intValue = phase >= 2 ? 3 : 2;
            so.FindProperty("_interval").floatValue = phase >= 2 ? 0.6f : 0.7f;
            so.FindProperty("_height").floatValue = 0.6f;
        });
        return CustomData($"Attack_Discord_Voice_P{phase}", custom, so =>
        {
            Shot(so, 8f, new Vector2(1.2f, 0.8f));
            Color(so, "_color", "23A55A");
        });
    }

    /// <summary>サーバー一覧：遠い方の壁から、低・中・高のアイコンが光った順に飛ぶ（毎秒 10マス・直径 1.0）。第2形態は 4つ</summary>
    AttackData ServerList(int phase)
    {
        var custom = Custom<DiscordServerListAttack>($"Discord_ServerList_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 4 : 3;
            so.FindProperty("_lead").floatValue = 0.4f;
            so.FindProperty("_interval").floatValue = 0.6f;
        });
        return CustomData($"Attack_Discord_ServerList_P{phase}", custom, so =>
        {
            Shot(so, 10f, new Vector2(1.0f, 1.0f));
            Color(so, "_color", "5865F2");
        });
    }

    /// <summary>
    /// スタンプ連打（第2形態）：大きいスタンプ（3×2）が、プレイヤーの位置に 0.4秒おきに 3個（影の予告 0.8秒）。
    /// 中の落下物の名前に ErrorWindowFall を入れて、Chat のスキンの「大きいスタンプ」の絵を付ける（20-4：そのまま）
    /// </summary>
    AttackData Stamps()
    {
        AttackData inner = Data("Attack_Discord_Stamp_ErrorWindowFall_P2", AttackKind.Spawn, so =>
        {
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_fallSpeed").floatValue = 14f;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 0f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_minDistanceFromTarget").floatValue = 0f;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "5865F2");
        });
        var custom = Custom<CallDropRepeat>("Discord_StampRain_P2", so =>
        {
            so.FindProperty("_inner").objectReferenceValue = inner;
            so.FindProperty("_count").intValue = 3;
            so.FindProperty("_interval").floatValue = 0.4f;
            so.FindProperty("_art").stringValue = "";
            so.FindProperty("_exclaim").boolValue = true;
        });
        return CustomData("Attack_Discord_StampRain_P2", custom);
    }

    // ───────── 能力 ─────────

    /// <summary>ミュート：正面 2マス先に直径 4.5 の円。雑魚 5・ゲージ +8・クールタイム 5秒。ボスを 2秒止める</summary>
    AttackData Mute()
    {
        var custom = Custom<DiscordMuteSkill>("Discord_Mute", so =>
        {
            so.FindProperty("_forward").floatValue = 2f;
            so.FindProperty("_diameter").floatValue = 4.5f;
            so.FindProperty("_active").floatValue = 0.15f;
            so.FindProperty("_stun").floatValue = 2f;
        });
        return CustomData("Attack_App_Discord_Mute", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_damage").intValue = 5;
            so.FindProperty("_gaugeGain").intValue = 8;
            so.FindProperty("_knockback").vector2Value = new Vector2(4f, 3f);
            so.FindProperty("_hitStun").floatValue = 0.2f;
            Color(so, "_color", "5865F2");
        });
    }

    // ───────── 道具 ─────────

    /// <summary>弾の欄（横へ真っすぐ。地形で消える）</summary>
    static void Shot(SerializedObject so, float speed, Vector2 size)
    {
        so.FindProperty("_speed").floatValue = speed;
        so.FindProperty("_hitSize").vector2Value = size;
        so.FindProperty("_lifetime").floatValue = 3.5f;
        so.FindProperty("_gravity").floatValue = 0f;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_sprite").objectReferenceValue = null;
    }
}
