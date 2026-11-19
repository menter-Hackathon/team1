using System;
using UnityEditor;
using UnityEngine;

// Premiere Pro のボスの専用の技と、能力「エフェクトアイコン投げ」のアセットを作る（2026-11-18。docs/app-boss-skills-design.md 3章）。
// 決まりは BossSkillAssets と同じ：無ければ作り、作ったときだけ数値を入れる（手で直した値は残す）。
// 技の中身（CustomAttack）の数値は、クラスの初期値が第1形態の値。第2形態の違いだけを、作ったときに入れる。
// 中で使う攻撃データ（爆発・落雷・切る線・火・もや）への参照は、空のときだけ入れ直す。
// 欄の名前をまちがえても止まらないように、欄はすべて Premiere〇〇 の道具で入れる（見つからなければ警告だけ出す）。
public static partial class BossSkillAssets
{
    const string PremiereBossPrefix = PremiereFolder + "/";
    const string PremiereAppPrefix = PlayerAppFolder + "/";

    /// <summary>第1形態の技の並び：エフェクトアイコンの雨（P1）⇄ 再生ヘッドレーザー（P1）</summary>
    public static AttackData[] PremierePhase1() => new[]
    {
        PremiereRainAttack(1),
        PremierePlayheadAttack(1),
    };

    /// <summary>第2形態の技の並び：雨（P2）・再生ヘッド（P2）・レーザーツール・スピードデュレーション</summary>
    public static AttackData[] PremierePhase2() => new[]
    {
        PremiereRainAttack(2),
        PremierePlayheadAttack(2),
        PremiereRazorAttack(),
        PremiereSpeedAttack(),
    };

    /// <summary>Premiere Pro の能力（エフェクトアイコン投げ）の攻撃データ。種類 Custom、クールタイム 3</summary>
    public static AttackData PremiereSkill()
    {
        // 効果（3-5 の表）。判定の色は透明にし、絵は PremiereFx が出す
        AttackData fire = PremiereData(PremiereAppPrefix + "Attack_App_PremierePro_Fire.asset", so =>
            PremiereArea(so, true, new Vector2(3.5f, 3.5f), false, 0.25f, 0f, 3, 8, new Vector2(6f, 6f), 0.3f, "FF950000"));
        AttackData thunder = PremiereData(PremiereAppPrefix + "Attack_App_PremierePro_Thunder.asset", so =>
            PremiereArea(so, false, new Vector2(1.5f, 12f), true, 0.25f, 0f, 3, 8, new Vector2(3f, 6f), 0.3f, "FFD60A00"));
        AttackData dissolve = PremiereData(PremiereAppPrefix + "Attack_App_PremierePro_Dissolve.asset", so =>
            PremiereArea(so, false, new Vector2(3f, 3f), false, 0.3f, 1.5f, 1, 3, new Vector2(1f, 2f), 0.1f, "9999FF00"));

        var custom = GetOrCreateCustom<PremiereIconThrow>(PremiereAppPrefix + "Custom_App_PremierePro_IconThrow.asset");
        PremiereLink(custom, "_fire", fire);
        PremiereLink(custom, "_thunder", thunder);
        PremiereLink(custom, "_dissolve", dissolve);

        // 直撃（ダメージ 1・ゲージ +2）。前からある Attack_App_PremierePro（BarGraph の複製）は使わず、新しく作る
        return GetOrCreateAttack(PremiereAppPrefix + "Attack_App_PremierePro_IconThrow.asset", custom, so =>
        {
            PremiereFloat(so, "_cooldown", 3f);
            PremiereInt(so, "_damage", 1);
            PremiereInt(so, "_gaugeGain", 2);
            PremiereVec2(so, "_knockback", new Vector2(3f, 3f));
            PremiereFloat(so, "_hitStun", 0.15f);
        });
    }

    // ───────── 3-1 エフェクトアイコンの雨 ─────────

    static AttackData PremiereRainAttack(int phase)
    {
        AttackData landingFire = null;
        if (phase >= 2)
        {
            // 炎のアイコンが着地した所の火（横1.5×縦0.8、1.5秒）。火の場所が見えるように、判定の色も少し残す
            landingFire = PremiereData(PremiereBossPrefix + "Attack_Premiere_IconRain_Fire.asset", so =>
                PremiereArea(so, false, new Vector2(1.5f, 0.8f), true, 0.3f, 1.5f, 1, 0, new Vector2(6f, 5f), 0.25f, "FF950066"));
        }

        var custom = GetOrCreateCustom<PremiereIconRain>(PremiereBossPrefix + $"Custom_Premiere_IconRain_P{phase}.asset", so =>
        {
            if (phase < 2) return;
            PremiereInt(so, "_count", 6);
            PremiereFloat(so, "_spacing", 3f);
        });
        if (landingFire != null) PremiereLink(custom, "_landingFire", landingFire);

        // 落下物の欄（大きさ 1×1、毎秒10マス、着地したら消える）を、この攻撃データに持たせる
        return GetOrCreateAttack(PremiereBossPrefix + $"Attack_Premiere_IconRain_P{phase}.asset", custom, so =>
        {
            PremiereVec2(so, "_objectSize", new Vector2(1f, 1f));
            PremiereFloat(so, "_fallSpeed", 10f);
            PremiereFloat(so, "_landRemainTime", 0f);
            PremiereBool(so, "_becomesPlatform", false);
        });
    }

    // ───────── 3-2 再生ヘッドレーザー ─────────

    static AttackData PremierePlayheadAttack(int phase)
    {
        AttackData explosion = PremiereData(PremiereBossPrefix + "Attack_Premiere_Playhead_Explosion.asset", so =>
            PremiereArea(so, true, new Vector2(3f, 3f), false, 0.3f, 0f, 1, 0, new Vector2(6f, 5f), 0.25f, "FF950000"));
        AttackData bolt = PremiereData(PremiereBossPrefix + "Attack_Premiere_Playhead_Bolt.asset", so =>
            PremiereArea(so, false, new Vector2(1.5f, 12f), true, 0.3f, 0f, 1, 0, new Vector2(6f, 5f), 0.25f, "FFD60A00"));

        var custom = GetOrCreateCustom<PremierePlayhead>(PremiereBossPrefix + $"Custom_Premiere_Playhead_P{phase}.asset", so =>
        {
            if (phase < 2) return;
            PremiereInt(so, "_markerCount", 5);
            PremiereInt(so, "_fireCount", 3);
            PremiereBool(so, "_roundTrip", true);
        });
        PremiereLink(custom, "_explosion", explosion);
        PremiereLink(custom, "_bolt", bolt);

        return GetOrCreateAttack(PremiereBossPrefix + $"Attack_Premiere_Playhead_P{phase}.asset", custom);
    }

    // ───────── 3-3 レーザーツール（場に残る技） ─────────

    static AttackData PremiereRazorAttack()
    {
        AttackData line = PremiereData(PremiereBossPrefix + "Attack_Premiere_Razor_Line.asset", so =>
            PremiereArea(so, false, new Vector2(0.6f, 12f), true, 0.15f, 0f, 1, 0, new Vector2(6f, 5f), 0.25f, "FFFFFF00"));
        var custom = GetOrCreateCustom<PremiereRazor>(PremiereBossPrefix + "Custom_Premiere_Razor.asset");
        PremiereLink(custom, "_line", line);
        AttackData data = GetOrCreateAttack(PremiereBossPrefix + "Attack_Premiere_Razor.asset", custom);
        PremiereLingering(data);
        return data;
    }

    // ───────── 3-4 スピードデュレーション（場に残る技） ─────────

    static AttackData PremiereSpeedAttack()
    {
        var custom = GetOrCreateCustom<PremiereSpeedDuration>(PremiereBossPrefix + "Custom_Premiere_SpeedDuration.asset");
        AttackData data = GetOrCreateAttack(PremiereBossPrefix + "Attack_Premiere_SpeedDuration.asset", custom, so =>
            PremiereInt(so, "_damage", 0)); // この技のダメージはなし
        PremiereLingering(data);
        return data;
    }

    // ───────── 道具（名前は Premiere で始める。ほかの担当の道具とぶつからないように） ─────────

    /// <summary>種類が Custom ではない攻撃データ（爆発・落雷など）を、無ければ作る。init は作ったときだけ呼ぶ</summary>
    static AttackData PremiereData(string path, Action<SerializedObject> init)
    {
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (data != null) return data;
        PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        data = ScriptableObject.CreateInstance<AttackData>();
        AssetDatabase.CreateAsset(data, path);
        if (init != null) PlayerTestSceneBuilder.SetProps(data, init);
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>
    /// 指定位置の判定（Area）の欄をまとめて入れる。出す場所は TargetPosition（面に沿わせない）、予告 0。
    /// snap：真下の床の上に置くか（落雷・切る線・火）。persist が 0 より大きいと、0.5秒ごとに当たり直す持続
    /// </summary>
    static void PremiereArea(SerializedObject so, bool circle, Vector2 size, bool snap, float active, float persist,
        int damage, int gauge, Vector2 knockback, float stun, string colorHex)
    {
        PremiereInt(so, "_kind", (int)AttackKind.Area);
        PremiereInt(so, "_anchor", (int)AreaAnchor.TargetPosition);
        PremiereBool(so, "_snapToGround", snap);
        PremiereBool(so, "_alignToSurface", false);
        PremiereInt(so, "_shape", (int)(circle ? AreaShape.Circle : AreaShape.Box));
        PremiereVec2(so, "_areaSize", size);
        PremiereInt(so, "_areaCount", 1);
        PremiereFloat(so, "_windup", 0f);
        PremiereFloat(so, "_telegraphTime", 0f);
        PremiereFloat(so, "_activeTime", active);
        PremiereFloat(so, "_persistDuration", persist);
        PremiereFloat(so, "_tickInterval", 0.5f);
        PremiereInt(so, "_damage", damage);
        PremiereInt(so, "_gaugeGain", gauge);
        PremiereVec2(so, "_knockback", knockback);
        PremiereFloat(so, "_hitStun", stun);
        PremiereColor(so, "_color", colorHex);
    }

    /// <summary>場に残る技にする（いつも入れる。技の作りに関わる値なので、手で直す物ではない）</summary>
    static void PremiereLingering(AttackData data)
    {
        if (data == null) return;
        PlayerTestSceneBuilder.SetProps(data, so => PremiereBool(so, "_lingering", true));
        EditorUtility.SetDirty(data);
    }

    /// <summary>技の中身から、中で使う攻撃データへの参照を入れる（空のときだけ）</summary>
    static void PremiereLink(UnityEngine.Object target, string prop, UnityEngine.Object value)
    {
        if (target == null || value == null) return;
        PlayerTestSceneBuilder.SetProps(target, so =>
        {
            SerializedProperty p = PremiereProp(so, prop);
            if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
        });
        EditorUtility.SetDirty(target);
    }

    static SerializedProperty PremiereProp(SerializedObject so, string name)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning($"Premiere のアセット：欄 {name} が見つかりません（{so.targetObject.name}）");
        return p;
    }

    static void PremiereInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = PremiereProp(so, name);
        if (p != null) p.intValue = value;
    }

    static void PremiereFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = PremiereProp(so, name);
        if (p != null) p.floatValue = value;
    }

    static void PremiereBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = PremiereProp(so, name);
        if (p != null) p.boolValue = value;
    }

    static void PremiereVec2(SerializedObject so, string name, Vector2 value)
    {
        SerializedProperty p = PremiereProp(so, name);
        if (p != null) p.vector2Value = value;
    }

    static void PremiereColor(SerializedObject so, string name, string hex)
    {
        SerializedProperty p = PremiereProp(so, name);
        if (p != null) p.colorValue = (UnityEngine.Color)RuntimeSprites.Hex(hex);
    }
}
