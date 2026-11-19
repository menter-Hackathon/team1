using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】ミュージック（Apple Music）のキットのアセット（2026-11-19。docs/app-kits-2-design.md 4-1）。担当C。
/// ボス：浮く（Chrome の体）。第1形態＝イコライザー・音の波、第2形態＝イコライザー（波つき）・音の波（5発）・シャッフル。
/// 能力：リピート。ステージの決まり：ビート（Kits/Music/MusicRule.cs）。実行時の登録は Kits/Music/MusicKit.cs（ラベル "Music"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class MusicKitAssets : KitAssetBuilder
{
    public override string Label => "Music";
    public override string[] AppNames => new[] { "Music", "ミュージック", "Apple Music" };
    public override string[] BundleIds => new[] { "com.apple.Music" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Equalizer(1), Wave(1) };
    public override AttackData[] Phase2() => new[] { Equalizer(2), Wave(2), Shuffle() };
    public override AttackData PlayerSkill() => Repeat();

    public override string SkillName => "リピート";
    public override SkillCategory Category => SkillCategory.Media;
    public override string SkillDescription => "最後に使ったほかの能力を、もう1回出す。まだなら音符を3発";
    public override string[] SkillTags => new[] { "もう1回", "音符" };

    /// <summary>イコライザー：8列。奇数 → 0.6秒あと偶数（予告 0.8秒・高さ 4〜7）。第2形態は先に左から右への波</summary>
    AttackData Equalizer(int phase)
    {
        var custom = Custom<MusicEqualizer>($"Music_Equalizer_P{phase}", so =>
        {
            so.FindProperty("_columns").intValue = 8;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_gap").floatValue = 0.6f;
            so.FindProperty("_height").vector2Value = new Vector2(4f, 7f);
            so.FindProperty("_sweep").boolValue = phase >= 2;
            so.FindProperty("_sweepStep").floatValue = 0.2f;
        });
        return CustomData($"Attack_Music_Equalizer_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FA243C");
        });
    }

    /// <summary>音の波：低・高・低（第2形態は 低・高・低・低・高）。0.6秒おき・毎秒 8マス・予告 0.8秒</summary>
    AttackData Wave(int phase)
    {
        AttackData wave = Data("Attack_Music_WaveShot", AttackKind.Projectile, so =>
        {
            Shot(so, 8f, new Vector2(0.45f, 1.5f), 3f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = false;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<MusicSoundWave>($"Music_SoundWave_P{phase}", so =>
        {
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_pattern").stringValue = phase >= 2 ? "LHLLH" : "LHL";
            so.FindProperty("_interval").floatValue = 0.6f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
        });
        return CustomData($"Attack_Music_SoundWave_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FB5C74");
        });
    }

    /// <summary>シャッフル（第2形態）：3か所へ瞬間移動（予告 0.8秒）。着くたびに音符を扇に 3発（毎秒 6マス・30°）</summary>
    AttackData Shuffle()
    {
        AttackData notes = Data("Attack_Music_NoteShot", AttackKind.Projectile, so =>
        {
            Shot(so, 6f, new Vector2(0.55f, 0.55f), 3.5f);
            so.FindProperty("_projectileCount").intValue = 3;
            so.FindProperty("_spreadAngle").floatValue = 30f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<MusicShuffle>("Music_Shuffle_P2", so =>
        {
            so.FindProperty("_notes").objectReferenceValue = notes;
            so.FindProperty("_hops").intValue = 3;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_aimDelay").floatValue = 0.25f;
        });
        return CustomData("Attack_Music_Shuffle_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FA243C");
        });
    }

    /// <summary>リピート：最後に使った能力をもう1回（ゲージ 0.7倍）。無ければ音符を扇に 3発（雑魚 1・ゲージ 4）。クールタイム 5秒</summary>
    AttackData Repeat()
    {
        AttackData notes = Data("Attack_App_Music_RepeatNote", AttackKind.Projectile, so =>
        {
            Shot(so, 12f, new Vector2(0.55f, 0.55f), 0.8f);
            so.FindProperty("_projectileCount").intValue = 3;
            so.FindProperty("_spreadAngle").floatValue = 24f;
            so.FindProperty("_aim").intValue = (int)AimMode.Forward;
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.4f, 0.6f);
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_gaugeGain").intValue = 4;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<MusicRepeat>("Music_Repeat", so =>
        {
            so.FindProperty("_notes").objectReferenceValue = notes;
            so.FindProperty("_gaugeRate").floatValue = 0.7f;
        });
        return CustomData("Attack_App_Music_Repeat", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_gaugeGain").intValue = 12;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FA243C");
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
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
