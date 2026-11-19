using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Spotify のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-2）。担当C。
/// ボス：浮く（Chrome の体）。第1形態＝Wrapped・スキップ、第2形態＝Wrapped（まん中だけ安全つき）・スキップ（3回）・再生キュー。
/// 能力：再生キュー。ステージの決まり：広告と Premium（Kits/Spotify/SpotifyRule.cs）。実行時の登録は Kits/Spotify/SpotifyKit.cs（ラベル "Spotify"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class SpotifyKitAssets : KitAssetBuilder
{
    public override string Label => "Spotify";
    public override string[] AppNames => new[] { "Spotify" };
    public override string[] BundleIds => new[] { "com.spotify.client" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Wrapped(1), Skip(1) };
    public override AttackData[] Phase2() => new[] { Wrapped(2), Skip(2), Queue() };
    public override AttackData PlayerSkill() => NoteQueue();

    public override string SkillName => "再生キュー";
    public override SkillCategory Category => SkillCategory.Media;
    public override string SkillDescription => "頭の上に音符を3つ並べる。0.5秒おきに近い敵へ勝手に飛ぶ";
    public override string[] SkillTags => new[] { "自動", "音符" };

    /// <summary>Wrapped：5列。1・3・5 → 0.8秒あと 2・4（予告 0.9秒）。第2形態は最後に「まん中だけ安全」（予告 1.5秒）</summary>
    AttackData Wrapped(int phase)
    {
        var custom = Custom<SpotifyWrapped>($"Spotify_Wrapped_P{phase}", so =>
        {
            so.FindProperty("_columns").intValue = 5;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_gap").floatValue = 0.8f;
            so.FindProperty("_finale").boolValue = phase >= 2;
            so.FindProperty("_finaleTelegraph").floatValue = 1.5f;
        });
        return CustomData($"Attack_Spotify_Wrapped_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "F037A5");
        });
    }

    /// <summary>スキップ：プレイヤーの後ろ 2マスへ飛び、半径 2.5マスの輪（予告 0.8秒・判定 0.15秒）。第2形態は 3回</summary>
    AttackData Skip(int phase)
    {
        var custom = Custom<SpotifySkip>($"Spotify_Skip_P{phase}", so =>
        {
            so.FindProperty("_skips").intValue = phase >= 2 ? 3 : 1;
            so.FindProperty("_behind").floatValue = 2f;
            so.FindProperty("_radius").floatValue = 2.5f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_hitWindow").floatValue = 0.15f;
            so.FindProperty("_gap").floatValue = 0.35f;
        });
        return CustomData($"Attack_Spotify_Skip_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "1DB954");
        });
    }

    /// <summary>再生キュー（第2形態）：5つのアイコン → シャッフル 0.5秒 → 1.0秒見せる → 0.5秒おきに撃つ（予告 0.8秒）</summary>
    AttackData Queue()
    {
        AttackData lane = Data("Attack_Spotify_QueueLane", AttackKind.Projectile, so =>
        {
            Shot(so, 8f, new Vector2(0.6f, 0.6f), 3f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            Color(so, "_color", "FFFFFF");
        });
        AttackData aimed = Data("Attack_Spotify_QueueAim", AttackKind.Projectile, so =>
        {
            Shot(so, 9f, new Vector2(0.6f, 0.6f), 3f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<SpotifyQueue>("Spotify_Queue_P2", so =>
        {
            so.FindProperty("_lane").objectReferenceValue = lane;
            so.FindProperty("_aimed").objectReferenceValue = aimed;
            so.FindProperty("_count").intValue = 5;
            so.FindProperty("_shuffleTime").floatValue = 0.5f;
            so.FindProperty("_showTime").floatValue = 1.0f;
            so.FindProperty("_interval").floatValue = 0.5f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
        });
        return CustomData("Attack_Spotify_Queue_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "1DB954");
        });
    }

    /// <summary>能力 再生キュー：音符 3つ（雑魚 1・ゲージ 5）を 0.5秒おきに近い敵へ。クールタイム 4秒</summary>
    AttackData NoteQueue()
    {
        AttackData note = Data("Attack_App_Spotify_Note", AttackKind.Projectile, so =>
        {
            Shot(so, 14f, new Vector2(0.55f, 0.55f), 1.2f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_gaugeGain").intValue = 5;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<SpotifyNoteQueue>("Spotify_NoteQueue", so =>
        {
            so.FindProperty("_note").objectReferenceValue = note;
            so.FindProperty("_count").intValue = 3;
            so.FindProperty("_interval").floatValue = 0.5f;
        });
        return CustomData("Attack_App_Spotify_NoteQueue", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_gaugeGain").intValue = 15;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "1DB954");
        });
    }

    static void Shot(SerializedObject so, float speed, Vector2 size, float lifetime)
    {
        so.FindProperty("_speed").floatValue = speed;
        so.FindProperty("_hitSize").vector2Value = size;
        so.FindProperty("_lifetime").floatValue = lifetime;
        so.FindProperty("_gravity").floatValue = 0f;
        so.FindProperty("_projectileCount").intValue = 1;
        so.FindProperty("_spreadAngle").floatValue = 0f;
        so.FindProperty("_repeatCount").intValue = 1;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
