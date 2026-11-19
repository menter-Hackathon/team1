using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ボスの専用の技・アプリの能力のアセットを作る道具（2026-11-18。docs/app-boss-skills-design.md）。
/// ボスごとの中身は、同じクラスの別のファイル（FinderSkillAssets.cs・PremiereSkillAssets.cs・TerminalSkillAssets.cs）に書く。
/// まとめて作るのは AppBossSkillsBuilder（メニュー「Tools/Team1/ボスの専用の技を用意」）。
///
/// 決まり：アセットは「無ければ作る」。作ったときだけ数値を入れ、すでにあれば数値は触らない（手で直した値を残す）。
/// 数値を入れ直したいときは、そのアセットを消してから作り直す。
/// </summary>
public static partial class BossSkillAssets
{
    public const string FinderFolder = "Assets/ScriptableObjects/Attacks/Bosses/Finder";
    public const string PremiereFolder = "Assets/ScriptableObjects/Attacks/Bosses/PremierePro";
    public const string TerminalFolder = "Assets/ScriptableObjects/Attacks/Bosses/Terminal";
    public const string PlayerAppFolder = "Assets/ScriptableObjects/Attacks/Player/App";

    /// <summary>
    /// 専用の技の中身（CustomAttack を継いだアセット）を、無ければ作る。init は作ったときだけ呼ぶ（数値を入れる）
    /// </summary>
    public static T GetOrCreateCustom<T>(string path, Action<SerializedObject> init = null) where T : CustomAttack
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        if (init != null) PlayerTestSceneBuilder.SetProps(asset, init);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>
    /// 専用の技の攻撃データ（種類＝Custom）を、無ければ作る。中身（custom）はいつも入れ直す。init は作ったときだけ呼ぶ
    /// （共通の欄：ダメージ・ノックバック・色・場に残る技かどうか など）
    /// </summary>
    public static AttackData GetOrCreateAttack(string path, CustomAttack custom, Action<SerializedObject> init = null)
    {
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        bool created = false;
        if (data == null)
        {
            PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            data = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(data, path);
            created = true;
        }
        PlayerTestSceneBuilder.SetProps(data, so =>
        {
            so.FindProperty("_kind").intValue = (int)AttackKind.Custom;
            so.FindProperty("_custom").objectReferenceValue = custom;
            if (created)
            {
                so.FindProperty("_windup").floatValue = 0f;
                so.FindProperty("_cooldown").floatValue = 0f;
                so.FindProperty("_damage").intValue = 1;
                so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
                so.FindProperty("_hitStun").floatValue = 0.25f;
                init?.Invoke(so);
            }
        });
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>今ある攻撃データを複製する（無ければ）。init は複製したときだけ呼ぶ（色・数値を変える）</summary>
    public static AttackData CopyAttack(string sourcePath, string path, Action<SerializedObject> init = null)
    {
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (data != null) return data;
        PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        if (!AssetDatabase.CopyAsset(sourcePath, path))
        {
            Debug.LogWarning($"攻撃データを複製できません：{sourcePath} → {path}");
            return null;
        }
        data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (init != null) PlayerTestSceneBuilder.SetProps(data, init);
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>そのボスだけの仕組み（BossExtension を継いだアセット）を、無ければ作る</summary>
    public static T GetOrCreateExtension<T>(string path, Action<SerializedObject> init = null) where T : BossExtension
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        if (init != null) PlayerTestSceneBuilder.SetProps(asset, init);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>色を SerializedObject の欄に入れる（16進）</summary>
    public static void Color(SerializedObject so, string prop, string hex) =>
        so.FindProperty(prop).colorValue = (Color)RuntimeSprites.Hex(hex);
}
