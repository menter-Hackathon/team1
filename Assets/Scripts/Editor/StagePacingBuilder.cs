using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】ステージの山と谷（出来事）のアセットを作るエディタ拡張（2026-11-19。docs/stage-pacing-design.md 10-2）。担当2 の物。
/// メニュー「Tools/Team1/ステージの山と谷を用意」から実行する（コマンドラインなら -executeMethod StagePacingBuilder.Build）。
/// 作る物（あれば値を上書きする。GUID は変えない）
/// ・Assets/ScriptableObjects/Attacks/Pace/ の攻撃データ。名前でスキンの枠が決まる（SkinTables.FrameOf。3-1 の決まり：Attack_Pace_〇〇）
///   - Attack_Pace_ErrorWindowFall（雨。Attack_Stage_Notification の値が元）・_Small（Chat）・_Big（AI）→ 落下物の枠
///   - Attack_Pace_Breakpoint（低い柱。VS Code のブレークポイントが元）・_Rhythm（Media。予告と判定が短い）→ 柱の枠
///   - Attack_Pace_RedWave（床の帯。VS Code の赤い波線が元）・_Reboot（Terminal。予告が長い）→ 床の枠
///   柱・床の判定は低い（1.2〜1.6マス。フォルダ＝高さ3 の上は当たらない）。予告は 0.8秒以上、ダメージ1。幅2マスを並べて使う（SweepEvent）
/// ・Assets/Resources/StagePacing/HazardRunner.prefab：出来事用の AttackRunner（陣営＝敵・当てる相手＝プレイヤー。BossTestSceneBuilder.AddAttackRunner）
/// ・Assets/Resources/StagePacing/PaceAssets.asset：攻撃データとプレハブの組（実行時に Resources.Load で読む）
/// 今あるビルダー・シーンは変えない。
/// </summary>
public static class StagePacingBuilder
{
    const string AttackFolder = "Assets/ScriptableObjects/Attacks/Pace";
    const string ResourceFolder = "Assets/Resources/StagePacing";
    const string RunnerPath = ResourceFolder + "/HazardRunner.prefab";
    const string AssetsPath = ResourceFolder + "/PaceAssets.asset";

    [MenuItem("Tools/Team1/ステージの山と谷を用意")]
    public static void BuildFromMenu() => Build();

    /// <summary>コマンドラインから実行する用（-executeMethod StagePacingBuilder.Build）</summary>
    public static void Build()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0 || LayerMask.NameToLayer("Ground") < 0 || LayerMask.NameToLayer("OneWayPlatform") < 0)
        {
            Debug.LogError("レイヤー Player / Ground / OneWayPlatform が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }
        PlayerTestSceneBuilder.EnsureFolder(AttackFolder);
        PlayerTestSceneBuilder.EnsureFolder(ResourceFolder);

        // 絵は、スキンが無いとき（スキンも Generic も断ったとき）の予備。色は白（絵の色のまま）
        Sprite notification = ArtAssets.LoadSprite("Generated/Hazards/Hazard_Notification.png");
        Sprite pillar = ArtAssets.LoadSprite("Generated/Attacks/Fx_Pillar.png");
        Sprite redWave = ArtAssets.LoadSprite("Generated/Attacks/Fx_RedWave.png");

        // 雨：降ってくる通知（Attack_Stage_Notification）と同じ値が元。影（予告）0.8秒、着地したら消える
        AttackData rain = Attack("Attack_Pace_ErrorWindowFall", so => Falling(so, new Vector2(2.0f, 0.9f), 9f, 0.8f, notification));
        AttackData rainSmall = Attack("Attack_Pace_ErrorWindowFall_Small", so => Falling(so, new Vector2(1.4f, 0.7f), 10f, 0.8f, notification));
        AttackData rainBig = Attack("Attack_Pace_ErrorWindowFall_Big", so => Falling(so, new Vector2(3.0f, 1.6f), 6f, 1.0f, notification));
        // 柱・床：VS Code の技が元。予告を長く（ボスの技の 1.0秒以上）、判定は低く
        AttackData pillarData = Attack("Attack_Pace_Breakpoint", so => Area(so, new Vector2(2f, 1.6f), 1.0f, 0.6f, pillar));
        AttackData pillarRhythm = Attack("Attack_Pace_Breakpoint_Rhythm", so => Area(so, new Vector2(2f, 1.6f), 0.8f, 0.4f, pillar));
        AttackData floor = Attack("Attack_Pace_RedWave", so => Area(so, new Vector2(2f, 1.2f), 1.0f, 0.6f, redWave));
        AttackData floorReboot = Attack("Attack_Pace_RedWave_Reboot", so => Area(so, new Vector2(2f, 1.2f), 2.5f, 0.6f, redWave));

        AttackRunner runner = BuildRunner(playerLayer);

        var assets = AssetDatabase.LoadAssetAtPath<PaceAssets>(AssetsPath);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<PaceAssets>();
            AssetDatabase.CreateAsset(assets, AssetsPath);
        }
        assets.Rain = rain;
        assets.RainSmall = rainSmall;
        assets.RainBig = rainBig;
        assets.Pillar = pillarData;
        assets.PillarRhythm = pillarRhythm;
        assets.Floor = floor;
        assets.FloorReboot = floorReboot;
        assets.HazardRunner = runner;
        EditorUtility.SetDirty(assets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ステージの山と谷：攻撃データ 7つ（{AttackFolder}）と {RunnerPath}・{AssetsPath} を用意しました。");
    }

    // ───────── 出来事用の AttackRunner ─────────

    static AttackRunner BuildRunner(int playerLayer)
    {
        var go = new GameObject("HazardRunner");
        try
        {
            // 陣営＝敵・当てる相手＝プレイヤー・床（Ground とすり抜け床）。狙う相手は実行時に入れる（出来事が StagePacer.Player を入れる）
            BossTestSceneBuilder.AddAttackRunner(go, Team.Enemy, 1 << playerLayer, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, RunnerPath);
            return prefab != null ? prefab.GetComponent<AttackRunner>() : null;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ───────── 攻撃データ ─────────

    /// <summary>攻撃データを作る（あれば、それに値を上書きする）</summary>
    static AttackData Attack(string name, System.Action<SerializedObject> init)
    {
        string path = $"{AttackFolder}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(data, path);
        }
        PlayerTestSceneBuilder.SetProps(data, so =>
        {
            Common(so);
            init(so);
        });
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>敵の攻撃の共通値（ボスの技と同じ：ダメージ1、ノックバック (6,5)、のけぞり0.25秒）</summary>
    static void Common(SerializedObject so)
    {
        I(so, "_damage", 1);
        I(so, "_gaugeGain", 0);
        V(so, "_knockback", new Vector2(6f, 5f));
        F(so, "_hitStun", 0.25f);
        F(so, "_windup", 0f);
        I(so, "_repeatCount", 1);
        so.FindProperty("_color").colorValue = Color.white;
    }

    /// <summary>落下物（雨）：1つずつ出す。影（予告）→ 落ちる → 着地したら消える。足場にならない</summary>
    static void Falling(SerializedObject so, Vector2 size, float fallSpeed, float telegraph, Sprite sprite)
    {
        I(so, "_kind", (int)AttackKind.Spawn);
        I(so, "_placement", (int)SpawnPlacement.AroundTarget);
        I(so, "_spawnCount", 1);
        F(so, "_minDistanceFromTarget", 0f);
        V(so, "_objectSize", size);
        F(so, "_fallSpeed", fallSpeed);
        F(so, "_telegraphTime", Mathf.Max(0.8f, telegraph));
        F(so, "_landRemainTime", 0f);
        B(so, "_becomesPlatform", false);
        R(so, "_sprite", sprite);
    }

    /// <summary>柱・床：床の上に出す箱（幅2マス）。予告 → 判定 → 消える。持続はしない</summary>
    static void Area(SerializedObject so, Vector2 size, float telegraph, float active, Sprite sprite)
    {
        I(so, "_kind", (int)AttackKind.Area);
        I(so, "_anchor", (int)AreaAnchor.TargetPosition);
        B(so, "_snapToGround", true);
        B(so, "_alignToSurface", false);
        I(so, "_shape", (int)AreaShape.Box);
        V(so, "_areaSize", size);
        I(so, "_areaCount", 1);
        F(so, "_areaStagger", 0f);
        F(so, "_telegraphTime", Mathf.Max(0.8f, telegraph));
        F(so, "_activeTime", active);
        F(so, "_persistDuration", 0f);
        R(so, "_sprite", sprite);
    }

    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;
    static void I(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void V(SerializedObject so, string prop, Vector2 value) => so.FindProperty(prop).vector2Value = value;
    static void B(SerializedObject so, string prop, bool value) => so.FindProperty(prop).boolValue = value;
    static void R(SerializedObject so, string prop, Object value) => so.FindProperty(prop).objectReferenceValue = value;
}
