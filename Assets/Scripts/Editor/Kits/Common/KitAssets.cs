using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】キットの攻撃データを作る道具（2026-11-19。担当0）。KitAssetBuilder から使う。
/// 今ある BossSkillAssets（「無ければ作る」。作ったときだけ数値を入れる）と違い、作るたびに init を入れ直す
/// （キットの数値はコードが正。キットの担当は Unity を動かさないので、コードを直してビルダーをかけ直せば反映される）。
/// </summary>
public static class KitAssets
{
    /// <summary>キットのアセットの置き場所</summary>
    public const string Root = "Assets/ScriptableObjects/Kits";

    /// <summary>データの技を作る（無ければ作る）。種類はいつも入れ直す。init は毎回呼ぶ</summary>
    public static AttackData GetOrCreateData(string path, AttackKind kind, Action<SerializedObject> init = null)
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
            so.FindProperty("_kind").intValue = (int)kind;
            if (created)
            {
                // ボスの技の基本（docs/app-kits-1-design.md 0-2）
                so.FindProperty("_damage").intValue = 1;
                so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
                so.FindProperty("_hitStun").floatValue = 0.25f;
            }
            init?.Invoke(so);
        });
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>専用の技の中身を作る（無ければ作る）。init は毎回呼ぶ</summary>
    public static T GetOrCreateCustom<T>(string path, Action<SerializedObject> init = null) where T : CustomAttack
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        if (init != null) PlayerTestSceneBuilder.SetProps(asset, init);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    /// <summary>専用の技の攻撃データ（種類＝Custom）を作る（無ければ作る）。中身はいつも入れ直す。init は毎回呼ぶ</summary>
    public static AttackData GetOrCreateCustomData(string path, CustomAttack custom, Action<SerializedObject> init = null)
    {
        return GetOrCreateData(path, AttackKind.Custom, so =>
        {
            so.FindProperty("_custom").objectReferenceValue = custom;
            so.FindProperty("_windup").floatValue = 0f;
            init?.Invoke(so);
        });
    }

    /// <summary>包み（KitDressed）と、その攻撃データを作る。中身（中の攻撃データ・絵の名前・予告の線）はいつも入れ直す</summary>
    public static AttackData Dressed(string path, AttackData inner, string artKey, bool line, Action<SerializedObject> init = null)
    {
        string dressPath = path.EndsWith(".asset") ? path.Substring(0, path.Length - 6) + "_Dress.asset" : path + "_Dress.asset";
        var dress = GetOrCreateCustom<KitDressed>(dressPath, so =>
        {
            so.FindProperty("_inner").objectReferenceValue = inner;
            so.FindProperty("_art").stringValue = artKey ?? "";
            so.FindProperty("_line").boolValue = line;
        });
        return GetOrCreateCustomData(path, dress, so =>
        {
            // 予告の秒数・判定の時間は中の攻撃データのもの。ゲージ・クールタイムは包みの欄（能力のとき）
            if (inner != null)
            {
                so.FindProperty("_cooldown").floatValue = inner.Cooldown;
                so.FindProperty("_gaugeGain").intValue = inner.GaugeGain;
                so.FindProperty("_lingering").boolValue = inner.Lingering;
            }
            init?.Invoke(so);
        });
    }

    /// <summary>今ある攻撃データを複製する（無ければ）。init は毎回呼ぶ</summary>
    public static AttackData Copy(string sourcePath, string path, Action<SerializedObject> init = null)
    {
        AttackData data = BossSkillAssets.CopyAttack(sourcePath, path);
        if (data == null) return null;
        if (init != null) PlayerTestSceneBuilder.SetProps(data, init);
        EditorUtility.SetDirty(data);
        return data;
    }
}
