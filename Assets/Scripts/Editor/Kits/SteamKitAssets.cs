using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Steam のキットのアセット（2026-11-19。docs/app-kits-2-design.md 2-3）。担当C。
/// ボス：浮く（Chrome の体）。第1形態＝SALE の値札・ダウンロード、第2形態＝値札（6つ＋-95%）・ダウンロード（2回。70％で止まる）・フレンドの通知。
/// 能力：トロフィー。ステージの決まり：実績解除（Kits/Steam/SteamRule.cs）。実行時の登録は Kits/Steam/SteamKit.cs（ラベル "Steam"）。
/// 攻撃データの名前に枠の語は入れない（キットの絵に差し替える）。数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class SteamKitAssets : KitAssetBuilder
{
    public override string Label => "Steam";
    public override string[] AppNames => new[] { "Steam" };
    public override string[] BundleIds => new[] { "com.valvesoftware.steam" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Sale(1), Download(1) };
    public override AttackData[] Phase2() => new[] { Sale(2), Download(2), Friends() };
    public override AttackData PlayerSkill() => Trophy();

    public override string SkillName => "トロフィー";
    public override SkillCategory Category => SkillCategory.Game;
    public override string SkillDescription => "トロフィーを山なりに投げる。当たると実績解除で、6秒ゲージが1.3倍";
    public override string[] SkillTags => new[] { "投げる", "強化" };

    // ───────── ボスの技 ─────────

    /// <summary>SALE の値札：影 0.8秒 → 0.25秒おきに落ちる（2.8マスおきの枠）。第1形態 4つ、第2形態 6つ＋大きい -95%（左右に低い衝撃）</summary>
    AttackData Sale(int phase)
    {
        AttackData tag = Data("Attack_Steam_SaleTag", AttackKind.Spawn, so =>
        {
            so.FindProperty("_objectSize").vector2Value = new Vector2(2.2f, 0.9f); // 値札の絵（40×16 ドット）に合わせる
            so.FindProperty("_fallSpeed").floatValue = 22f;
            so.FindProperty("_landRemainTime").floatValue = 0.25f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFFFFF");
        });
        AttackData big = null, shock = null;
        if (phase >= 2)
        {
            big = Data("Attack_Steam_SaleTagBig", AttackKind.Spawn, so =>
            {
                so.FindProperty("_objectSize").vector2Value = new Vector2(3.5f, 1.5f); // 絵（64×27 ドット）に合わせる
                so.FindProperty("_fallSpeed").floatValue = 20f;
                so.FindProperty("_landRemainTime").floatValue = 0.4f;
                so.FindProperty("_becomesPlatform").boolValue = false;
                so.FindProperty("_spawnCount").intValue = 1;
                so.FindProperty("_damage").intValue = 1;
                Color(so, "_color", "FFFFFF");
            });
            shock = Data("Attack_Steam_SaleShock", AttackKind.Projectile, so =>
            {
                Shot(so, 8f, new Vector2(0.9f, 0.5f), 0.28f); // 左右 2マスほど
                so.FindProperty("_destroyOnTerrain").boolValue = false;
                so.FindProperty("_destroyOnHit").boolValue = false;
                Color(so, "_color", "BEEE11");
            });
        }
        var custom = Custom<SteamSaleDrop>($"Steam_Sale_P{phase}", so =>
        {
            so.FindProperty("_tag").objectReferenceValue = tag;
            so.FindProperty("_bigTag").objectReferenceValue = big;
            so.FindProperty("_shock").objectReferenceValue = shock;
            so.FindProperty("_count").intValue = phase >= 2 ? 6 : 4;
            so.FindProperty("_slot").floatValue = 2.8f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_interval").floatValue = 0.25f;
            so.FindProperty("_bigTelegraph").floatValue = 1.0f;
        });
        return CustomData($"Attack_Steam_Sale_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "4C6B22");
        });
    }

    /// <summary>ダウンロード：1.6秒でのびる床のバー。50％から赤く点滅、100％で 0.15秒当たる。第2形態は 2回（2回目は 70％で 0.6秒止まる）</summary>
    AttackData Download(int phase)
    {
        var custom = Custom<SteamDownload>($"Steam_Download_P{phase}", so =>
        {
            so.FindProperty("_runs").intValue = phase >= 2 ? 2 : 1;
            so.FindProperty("_fillTime").floatValue = 1.6f;
            so.FindProperty("_pauseAt").floatValue = 0.7f;
            so.FindProperty("_pauseTime").floatValue = 0.6f;
            so.FindProperty("_rushTime").floatValue = 0.25f;
            so.FindProperty("_hitWindow").floatValue = 0.15f;
            so.FindProperty("_barHeight").floatValue = 0.8f;
            so.FindProperty("_gap").floatValue = 0.5f;
        });
        return CustomData($"Attack_Steam_Download_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
            so.FindProperty("_hitStun").floatValue = 0.25f;
            Color(so, "_color", "66C0F4");
        });
    }

    /// <summary>フレンドの通知（第2形態）：床・足場・その上の 3つの高さを、0.7秒おきに毎秒 7マスで横切る（予告の線 0.8秒）</summary>
    AttackData Friends()
    {
        AttackData card = Data("Attack_Steam_FriendCard", AttackKind.Projectile, so =>
        {
            Shot(so, 7f, new Vector2(3f, 1f), 3.6f);
            so.FindProperty("_destroyOnTerrain").boolValue = false;
            so.FindProperty("_destroyOnHit").boolValue = false;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<SteamFriendOnline>("Steam_Friends_P2", so =>
        {
            so.FindProperty("_card").objectReferenceValue = card;
            so.FindProperty("_interval").floatValue = 0.7f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
        });
        return CustomData("Attack_Steam_Friends_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "90BA3C");
        });
    }

    // ───────── 能力 ─────────

    /// <summary>トロフィー：山なりの弾（雑魚 1・ゲージ 10）。当たると 6秒 与えるゲージ ×1.3。クールタイム 6秒</summary>
    AttackData Trophy()
    {
        AttackData shot = Data("Attack_App_Steam_TrophyShot", AttackKind.Projectile, so =>
        {
            Shot(so, 11f, new Vector2(0.8f, 0.8f), 2f);
            so.FindProperty("_gravity").floatValue = 22f;
            so.FindProperty("_aim").intValue = (int)AimMode.Forward;
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.4f, 0.7f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_gaugeGain").intValue = 10;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 3f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            Color(so, "_color", "FFFFFF");
        });
        var custom = Custom<SteamTrophyThrow>("Steam_Trophy", so =>
        {
            so.FindProperty("_shot").objectReferenceValue = shot;
            so.FindProperty("_angle").floatValue = 40f;
            so.FindProperty("_gaugeMult").floatValue = 1.3f;
            so.FindProperty("_buffSeconds").floatValue = 6f;
        });
        return CustomData("Attack_App_Steam_Trophy", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 6f;
            so.FindProperty("_gaugeGain").intValue = 10;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFD24A");
        });
    }

    // ───────── 道具 ─────────

    static void Shot(SerializedObject so, float speed, Vector2 size, float lifetime)
    {
        so.FindProperty("_speed").floatValue = speed;
        so.FindProperty("_hitSize").vector2Value = size;
        so.FindProperty("_lifetime").floatValue = lifetime;
        so.FindProperty("_gravity").floatValue = 0f;
        so.FindProperty("_projectileCount").intValue = 1;
        so.FindProperty("_repeatCount").intValue = 1;
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}
