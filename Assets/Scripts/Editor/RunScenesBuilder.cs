using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ラン（タイトル → ステージ → リザルト）を通して遊ぶためのシーンとデータを作るエディタ拡張（M5）。
/// メニュー「Tools/Team1/ランのシーンを作成」から実行する。作るもの：
/// ・ボスのプレハブ（Assets/Prefabs/Bosses。非表示で保存。すでにあれば作らない）
/// ・対応アプリ表（Assets/ScriptableObjects/Run/AppCatalog。すでにあれば作らない）：Chrome・VS Code
/// ・サンプルデータ（Assets/Resources/SampleUsage.json。すでにあれば作らない）
/// ・シーン（Assets/Scenes/Run）：RunTitle（【仮】タイトル）、Stage_Desktop（【仮】ステージ）、RunResult（【仮】リザルト）
/// ・Build Settings にこの3つを登録する（ほかに登録されているシーンは後ろに残す）
/// 数値はすべて【仮】。データとプレハブは上書きしないので、直したいときはそのアセットを直接直す（作り直すなら消してから実行）。
/// </summary>
public static class RunScenesBuilder
{
    const string SceneFolder = "Assets/Scenes/Run";
    const string TitleScene = "RunTitle";
    const string StageScene = "Stage_Desktop";
    const string ResultScene = "RunResult";
    const string PrefabFolder = "Assets/Prefabs/Bosses";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string SamplePath = "Assets/Resources/SampleUsage.json";

    static readonly Color GroundColor = new Color(0.36f, 0.30f, 0.62f);
    static readonly Color OneWayColor = new Color(0.30f, 0.80f, 0.90f);
    static readonly Color BackgroundColor = new Color(0.10f, 0.08f, 0.20f);
    static readonly Color ChromeBodyColor = new Color(0.92f, 0.92f, 0.95f);
    static readonly Color VSCodeBodyColor = new Color(0.16f, 0.42f, 0.78f);
    static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);
    static readonly Vector2 ChromeSpawn = new Vector2(0f, 2.5f + 1.5f);
    static readonly Vector2 VSCodeSpawn = new Vector2(7f, 1.5f);

    [MenuItem("Tools/Team1/ランのシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod RunScenesBuilder.Build）</summary>
    public static void Build()
    {
        if (LayerMask.NameToLayer("Ground") < 0 || LayerMask.NameToLayer("OneWayPlatform") < 0 ||
            LayerMask.NameToLayer("Player") < 0 || LayerMask.NameToLayer("Enemy") < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }

        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BossController chromePrefab = GetOrCreateBossPrefab("Chrome", attacks.ChromeProfile, ChromeSpawn, ChromeBodyColor, square);
        BossController vscodePrefab = GetOrCreateBossPrefab("VSCode", attacks.VSCodeProfile, VSCodeSpawn, VSCodeBodyColor, square);
        AppCatalog catalog = GetOrCreateCatalog(chromePrefab, attacks.ChromeProfile, vscodePrefab, attacks.VSCodeProfile);
        GetOrCreateSample();

        PlayerTestSceneBuilder.EnsureFolder(SceneFolder);
        string titlePath = BuildTitle(catalog);
        string stagePath = BuildStage(catalog, square, attacks, zakoShot);
        string resultPath = BuildResult();
        RegisterBuildScenes(titlePath, stagePath, resultPath);

        AssetDatabase.SaveAssets();
        Debug.Log($"ランのシーンを作成しました: {titlePath}, {stagePath}, {resultPath}");
    }

    // ───────── ボスのプレハブ・対応アプリ表・サンプル ─────────

    static BossController GetOrCreateBossPrefab(string bossName, BossProfile profile, Vector2 position, Color color, Sprite square)
    {
        string path = $"{PrefabFolder}/Boss_{bossName}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<BossController>(path);
        if (existing != null) return existing;

        PlayerTestSceneBuilder.EnsureFolder(PrefabFolder);
        // 狙う相手（プレイヤー）は、ステージで作ったときに StageDirector が入れる
        BossController boss = BossTestSceneBuilder.CreateBoss(bossName, profile, position, new Vector2(4f, 3f), color, square, null);
        boss.gameObject.SetActive(false); // 雑魚を倒すまで出さないので、非表示で保存する
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, path);
        Object.DestroyImmediate(boss.gameObject);
        return AssetDatabase.LoadAssetAtPath<BossController>(path);
    }

    static AppCatalog GetOrCreateCatalog(BossController chromePrefab, BossProfile chromeProfile, BossController vscodePrefab, BossProfile vscodeProfile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        if (catalog != null) return catalog;

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
        catalog = ScriptableObject.CreateInstance<AppCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            so.FindProperty("_fullStrengthHours").floatValue = 20f;
            var apps = so.FindProperty("_apps");
            apps.arraySize = 2;
            // Chrome：手に入る能力は「新しいタブ」。使用時間で瞬間移動の距離 5〜8マス
            SetEntry(apps.GetArrayElementAtIndex(0), "Chrome", "Google Chrome", "com.google.Chrome", SkillCategory.Browser, chromePrefab, chromeProfile, ChromeSpawn,
                AttackStat.WarpDistance, 5f, 8f);
            // VS Code：手に入る能力は「ブレークポイント」。使用時間でゲージ増加量 10〜16
            SetEntry(apps.GetArrayElementAtIndex(1), "VS Code", "Visual Studio Code", "com.microsoft.VSCode", SkillCategory.Dev, vscodePrefab, vscodeProfile, VSCodeSpawn,
                AttackStat.GaugeGain, 10f, 16f);
        });
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    static void SetEntry(SerializedProperty e, string displayName, string screenTimeName, string bundleId, SkillCategory category, BossController prefab, BossProfile profile,
        Vector2 spawn, AttackStat stat, float abilityMin, float abilityMax)
    {
        e.FindPropertyRelative("DisplayName").stringValue = displayName;
        var ids = e.FindPropertyRelative("BundleIdPrefixes");
        ids.arraySize = 1;
        ids.GetArrayElementAtIndex(0).stringValue = bundleId;
        var names = e.FindPropertyRelative("ScreenTimeNames");
        names.arraySize = 1;
        names.GetArrayElementAtIndex(0).stringValue = screenTimeName;
        e.FindPropertyRelative("Category").intValue = (int)category; // 値を明示した enum なので値で入れる
        e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("Profile").objectReferenceValue = profile;
        e.FindPropertyRelative("SpawnPosition").vector2Value = spawn;
        e.FindPropertyRelative("StageScene").stringValue = StageScene;
        e.FindPropertyRelative("GaugeMax").FindPropertyRelative("Min").intValue = 80;
        e.FindPropertyRelative("GaugeMax").FindPropertyRelative("Max").intValue = 140;
        e.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Min").floatValue = 0.8f;
        e.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Max").floatValue = 0.6f;
        e.FindPropertyRelative("AbilityStat").intValue = (int)stat;
        e.FindPropertyRelative("AbilityValue").FindPropertyRelative("Min").floatValue = abilityMin;
        e.FindPropertyRelative("AbilityValue").FindPropertyRelative("Max").floatValue = abilityMax;
    }

    static void GetOrCreateSample()
    {
        if (File.Exists(SamplePath)) return;
        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(SamplePath).Replace('\\', '/'));
        // 【仮】Chrome 8時間・VS Code 25時間（最大）。Finder は対応外なので登場しない
        var sample = new[]
        {
            new AppUsage("com.google.Chrome", "Google Chrome", 8 * 3600),
            new AppUsage("com.microsoft.VSCode", "Visual Studio Code", 25 * 3600),
            new AppUsage("com.apple.finder", "Finder", 10 * 3600),
        };
        File.WriteAllText(SamplePath, AppUsageJson.ToJson(sample));
        AssetDatabase.ImportAsset(SamplePath);
    }

    // ───────── シーン ─────────

    static string BuildTitle(AppCatalog catalog)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 0f, -10f));
        var session = new GameObject("GameSession").AddComponent<GameSession>();
        PlayerTestSceneBuilder.SetProps(session, so =>
        {
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_titleScene").stringValue = TitleScene;
            so.FindProperty("_resultScene").stringValue = ResultScene;
        });
        var menu = new GameObject("RunTitleMenu").AddComponent<RunTitleMenu>();
        PlayerTestSceneBuilder.SetProps(menu, so => so.FindProperty("_catalog").objectReferenceValue = catalog);
        string path = $"{SceneFolder}/{TitleScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static string BuildStage(AppCatalog catalog, Sprite square, BossTestSceneBuilder.Attacks attacks, AttackData zakoShot)
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 部屋（雑魚戦テストシーンと同じ。床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // --- プレイヤー（能力は G キーではもらえない。ボスを正常化して手に入れる） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8.5f, 0.05f, 0f), attacks.PlayerMelee, null);
        Object.DestroyImmediate(player.GetComponent<SkillDebugGiver>());
        PlayerTestSceneBuilder.SetProps(player.GetComponent<HealthDebugLabel>(), so => so.FindProperty("_showGameOver").boolValue = false);

        // --- 雑魚ウイルス ---
        var wave = new GameObject("EnemyWave").AddComponent<EnemyWave>();
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Floor", new Vector2(2f, 0f), -1, wave.transform, square);
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Folder", new Vector2(3.5f, 3f), 1, wave.transform, square);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Folder", new Vector2(-3.5f, 3f), zakoShot, player.transform, wave.transform, square);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Floor", new Vector2(8.5f, 0f), zakoShot, player.transform, wave.transform, square);

        // --- ボスの登場と、ステージの進行（どのボスが出るかは実行時に決まる） ---
        var entrance = new GameObject("BossEntrance").AddComponent<BossEntrance>();
        PlayerTestSceneBuilder.SetProps(entrance, so => so.FindProperty("_wave").objectReferenceValue = wave);
        var director = new GameObject("StageDirector").AddComponent<StageDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_entrance").objectReferenceValue = entrance;
            so.FindProperty("_player").objectReferenceValue = player.GetComponent<PlayerSkills>();
        });
        var hud = new GameObject("RunHud").AddComponent<RunHud>();
        PlayerTestSceneBuilder.SetProps(hud, so => so.FindProperty("_director").objectReferenceValue = director);

        CreateCamera(new Vector3(0f, 5.5f, -10f));
        string path = $"{SceneFolder}/{StageScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static string BuildResult()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 0f, -10f));
        new GameObject("RunResultScreen").AddComponent<RunResultScreen>();
        string path = $"{SceneFolder}/{ResultScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static void CreateCamera(Vector3 position)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = position;
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        camGo.AddComponent<AudioListener>();
    }

    /// <summary>Build Settings の先頭に、タイトル・ステージ・リザルトを登録する（ほかのシーンは後ろに残す）</summary>
    static void RegisterBuildScenes(params string[] paths)
    {
        var scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!paths.Contains(s.path)) scenes.Add(s);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
