using UnityEditor;
using UnityEngine;

// Finder のボスの専用の技と、Finder の能力（zip圧縮）のアセットを作る（2026-11-18。docs/app-boss-skills-design.md 2章）。
// ・技の中身（FinderSpotlightAttack・FinderSmartFolderAttack・FinderZipAttack）と、ボスの技の攻撃データは FinderFolder に置く。P1・P2 は別のアセット
// ・zip圧縮の攻撃データは PlayerAppFolder（Attack_App_Finder_Zip。今ある Attack_App_Finder は、前の【仮】の複製なので使わない）
// ・決まり（BossSkillAssets）：無ければ作る。すでにあれば数値は触らない。中身の数値の初めの値は、各クラスの欄の初めの値（docs の【仮】の値）
// ・このファイルの中では Color は BossSkillAssets.Color（16進を入れる道具）を指す。色の型は UnityEngine.Color と書く
public static partial class BossSkillAssets
{
    const string FinderChromeAttacks = "Assets/ScriptableObjects/Attacks/Chrome";
    /// <summary>【仮】Chrome の技の複製に掛ける色（色だけ Finder にする。うすい青）</summary>
    const string FinderTintHex = "A8D8FF";

    /// <summary>
    /// スマートフォルダマグネット（優先度 B）を入れるか。false にすると、第1形態は Chrome のタブ手裏剣の複製、第2形態はスマートフォルダ抜きに戻る
    /// （遊んでみて具合が悪いときの逃げ道）
    /// </summary>
    static readonly bool FinderUseSmartFolder = true;

    /// <summary>第1形態の技の並び：Spotlight・サーチ（P1）⇄ スマートフォルダマグネット（P1）。交互</summary>
    public static AttackData[] FinderPhase1()
    {
        AttackData spotlight = FinderSpotlight(1);
        AttackData second = FinderUseSmartFolder ? FinderSmartFolder(1) : null;
        if (second == null)
        {
            second = CopyAttack($"{FinderChromeAttacks}/Attack_Chrome_TabShuriken_P1.asset",
                $"{FinderFolder}/Attack_Finder_TabShuriken_P1.asset", FinderTint);
        }
        return new[] { spotlight, second };
    }

    /// <summary>第2形態の技の並び：Spotlight・サーチ（P2）、スマートフォルダマグネット（P2）、ウィンドウ出現（Chrome のポップアップ出現 P2 の複製）</summary>
    public static AttackData[] FinderPhase2()
    {
        AttackData spotlight = FinderSpotlight(2);
        AttackData smart = FinderUseSmartFolder ? FinderSmartFolder(2) : null;
        AttackData popup = CopyAttack($"{FinderChromeAttacks}/Attack_Chrome_PopupAppear_P2.asset",
            $"{FinderFolder}/Attack_Finder_PopupAppear_P2.asset", FinderTint);
        return smart != null ? new[] { spotlight, smart, popup } : new[] { spotlight, popup };
    }

    /// <summary>
    /// Finder の能力（zip圧縮）の攻撃データ。種類 Custom、クールタイム 3.5秒。散弾・弾幕の1発の値は弾の欄に入れる。
    /// 2026-11-19（docs/app-feedback-v2-design.md 4-2）：発射は吸いこんだ数で4段。0個の1発（ゲージ +4）・レーザー（雑魚 3・ゲージ +24）の数値は
    /// FinderZipAttack の欄（クラスの初めの値）にあり、実行時に攻撃データの写しを作って使う。だから、今あるアセットのままで新しい値になる
    /// </summary>
    public static AttackData FinderSkill()
    {
        var custom = GetOrCreateCustom<FinderZipAttack>($"{FinderFolder}/Finder_Zip.asset");
        return GetOrCreateAttack($"{PlayerAppFolder}/Attack_App_Finder_Zip.asset", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 3.5f;      // 発射してから
            so.FindProperty("_damage").intValue = 1;             // 散弾1発：雑魚にダメージ 1
            so.FindProperty("_gaugeGain").intValue = 2;          // 散弾・弾幕1発：ボスのゲージ +2（1個で 4発＝+8、3個で 10発＝+20）
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            so.FindProperty("_speed").floatValue = 12f;          // 毎秒12マス
            so.FindProperty("_lifetime").floatValue = 0.6f;      // 0.6秒で消える（届く距離 約7マス）
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.3f, 0.3f); // 直径 0.3
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.45f, 0.55f); // 体の中ほどから
            so.FindProperty("_sprite").objectReferenceValue = null; // 絵は実行時に小さい書類にする
            Color(so, "_color", "FFFFFF");
        });
    }

    // ───────── 技ごと ─────────

    /// <summary>Spotlight・サーチ。第2形態は、追加で2回撃つ</summary>
    static AttackData FinderSpotlight(int phase)
    {
        string key = $"Finder_Spotlight_P{phase}";
        var custom = GetOrCreateCustom<FinderSpotlightAttack>($"{FinderFolder}/{key}.asset", so =>
        {
            so.FindProperty("_extraShots").intValue = phase >= 2 ? 2 : 0;
        });
        return GetOrCreateAttack($"{FinderFolder}/Attack_{key}.asset", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
            so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
            so.FindProperty("_hitStun").floatValue = 0.25f;
            Color(so, "_color", "FFF4B0"); // 光の帯の色（見た目は技の中で描く）
        });
    }

    /// <summary>スマートフォルダマグネット。第2形態は赤 → 青の2色</summary>
    static AttackData FinderSmartFolder(int phase)
    {
        string key = $"Finder_SmartFolder_P{phase}";
        var custom = GetOrCreateCustom<FinderSmartFolderAttack>($"{FinderFolder}/{key}.asset", so =>
        {
            so.FindProperty("_twoColors").boolValue = phase >= 2;
        });
        return GetOrCreateAttack($"{FinderFolder}/Attack_{key}.asset", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;             // 飛んだトゲ：ライフ -1
            so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
            so.FindProperty("_hitStun").floatValue = 0.25f;
            Color(so, "_color", "FF453A");
        });
    }

    /// <summary>Chrome の技の複製の色だけを Finder にする</summary>
    static void FinderTint(SerializedObject so) => Color(so, "_color", FinderTintHex);
}
