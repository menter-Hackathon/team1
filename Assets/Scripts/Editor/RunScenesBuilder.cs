using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ラン（タイトル → 能力えらび → ステージ → リザルト）を通して遊ぶためのシーンとデータを作るエディタ拡張（M5）。
/// メニュー「Tools/Team1/ランのシーンを作成」から実行する。作るもの：
/// ・ボスのプレハブ（Assets/Prefabs/Bosses。非表示で保存。毎回作り直す。同じパスに上書きするので、対応アプリ表からの参照は切れない）
/// ・対応アプリ表（Assets/ScriptableObjects/Run/AppCatalog。すでにあれば作らない）：Chrome・VS Code
/// ・サンプルデータ（Assets/Resources/SampleUsage.json。すでにあれば作らない）
/// ・カテゴリの能力と能力えらびの表（AbilitiesBuilder。すでにあれば作らない）
/// ・シーン（Assets/Scenes/Run）：RunTitle（【仮】タイトル）、RunLoadout（【仮】能力えらび）、Stage_Desktop（【仮】1画面のステージ）、
///   Stage_Scroll（横スクロール）・Stage_Windows（ウィンドウ移動）（StageLayoutsBuilder）、RunResult（【仮】リザルト）
/// ・Build Settings にこの6つを登録する（ほかに登録されているシーンは後ろに残す）
/// ・AI チャットのボス（AiChatBossBuilder：プレハブ・Stage_AIChat・対応アプリ表の行。2026-11-19）
/// ・チャットのボス（ChatBossBuilder：プレハブ・チャットbot・対応アプリ表の行。2026-11-19）
/// 数値はすべて【仮】。データ（対応アプリ表・サンプル）は上書きしないので、直したいときはそのアセットを直接直す（作り直すなら消してから実行）。
/// 絵は ArtAssets（Tools/Team1/グラフィックを適用）が入れる。ボスのプレハブは絵ごと作り直す。
/// </summary>
public static class RunScenesBuilder
{
    const string SceneFolder = "Assets/Scenes/Run";
    const string TitleScene = "RunTitle";
    const string LoadoutScene = "RunLoadout";
    const string StageScene = "Stage_Desktop";
    const string ResultScene = "RunResult";
    const string PrefabFolder = "Assets/Prefabs/Bosses";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string SamplePath = "Assets/Resources/SampleUsage.json";

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

        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        StageTestSceneBuilder.GetOrCreateZakoShot();
        AbilitiesBuilder.GetOrCreate(); // カテゴリの能力と能力えらびの表（無ければ作る）
        ArtAssets.Apply(); // 絵の取り込み設定と、攻撃データ・能力・ボスへの割り当て

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（取り込みやシーンの切り替えで、前に読んだ物が使えなくなることがある）
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();
        BossController chromePrefab = BuildBossPrefab("Chrome", attacks.ChromeProfile, ChromeSpawn, ChromeBodyColor, square);
        BossController vscodePrefab = BuildBossPrefab("VSCode", attacks.VSCodeProfile, VSCodeSpawn, VSCodeBodyColor, square);
        AppCatalog catalog = GetOrCreateCatalog(chromePrefab, attacks.ChromeProfile, vscodePrefab, attacks.VSCodeProfile);
        ArtAssets.ApplyThemesToCatalog(); // 各アプリの色（新しく表を作ったときにも入れる）
        ApplyStageScenes(); // 各アプリのステージの種類（Chrome＝横スクロール、VS Code＝ウィンドウ移動）
        GetOrCreateSample();

        PlayerTestSceneBuilder.EnsureFolder(SceneFolder);
        string titlePath = BuildTitle(catalog);
        string loadoutPath = BuildLoadout();
        string stagePath = BuildStage(catalog, square, attacks, zakoShot);
        string scrollPath = StageLayoutsBuilder.BuildScroll(SceneFolder, catalog);
        string windowsPath = StageLayoutsBuilder.BuildWindows(SceneFolder, catalog);
        string resultPath = BuildResult();
        RegisterBuildScenes(titlePath, loadoutPath, stagePath, scrollPath, windowsPath, resultPath);
        FinalBossBuilder.BuildFinalBoss(); // ラスボス Terminal（無ければ作る。2026-11-18）
        AiChatBossBuilder.BuildForRun();   // AI チャットのボス（プレハブ・ステージ・対応アプリ表の行。2026-11-19）
        ChatBossBuilder.BuildForRun();     // チャットのボス（プレハブ・チャットbot・対応アプリ表の行。2026-11-19）

        AssetDatabase.SaveAssets();
        Debug.Log($"ランのシーンを作成しました: {titlePath}, {loadoutPath}, {stagePath}, {scrollPath}, {windowsPath}, {resultPath}");
    }

    // ───────── ボスのプレハブ・対応アプリ表・サンプル ─────────

    /// <summary>ボスのプレハブを作る（あれば同じパスに上書きする。GUID は変わらない）</summary>
    static BossController BuildBossPrefab(string bossName, BossProfile profile, Vector2 position, Color color, Sprite square)
    {
        string path = $"{PrefabFolder}/Boss_{bossName}.prefab";
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

    /// <summary>
    /// 各アプリのステージの種類（2026-11-17 決定）。Chrome＝横スクロール、VS Code＝ウィンドウ移動、それ以外＝1画面（Stage_Desktop）。
    /// 対応アプリ表がすでにあっても、この欄だけ入れる
    /// </summary>
    static void ApplyStageScenes()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        if (catalog == null) return;
        var so = new SerializedObject(catalog);
        var apps = so.FindProperty("_apps");
        for (int i = 0; i < apps.arraySize; i++)
        {
            var e = apps.GetArrayElementAtIndex(i);
            string name = e.FindPropertyRelative("DisplayName").stringValue;
            string scene = name == "Chrome" ? StageLayoutsBuilder.ScrollScene : name == "VS Code" ? StageLayoutsBuilder.WindowsScene : StageScene;
            e.FindPropertyRelative("StageScene").stringValue = scene;
        }
        if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(catalog);
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
        // 【仮】Chrome 8時間・VS Code 25時間（最大）。Finder・Slack はボスにも能力にもならない。
        // Figma・Excel・Steam は能力えらびのカードだけに出る（ボスはまだいない）
        var sample = new[]
        {
            new AppUsage("com.google.Chrome", "Google Chrome", 8 * 3600),
            new AppUsage("com.microsoft.VSCode", "Visual Studio Code", 25 * 3600),
            new AppUsage("com.apple.finder", "Finder", 10 * 3600),
            new AppUsage("com.tinyspeck.slackmacgap", "Slack", 15 * 3600),
            new AppUsage("com.figma.Desktop", "Figma", 12 * 3600),
            new AppUsage("com.microsoft.Excel", "Microsoft Excel", 6 * 3600),
            new AppUsage("com.valvesoftware.steam", "Steam", 4 * 3600),
        };
        File.WriteAllText(SamplePath, AppUsageJson.ToJson(sample));
        AssetDatabase.ImportAsset(SamplePath);
    }

    // ───────── シーン ─────────

    static string BuildTitle(AppCatalog catalog)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath); // 新しいシーンを開いたので読み込み直す
        CreateCamera(new Vector3(0f, 0f, -10f));
        var abilities = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilitiesBuilder.CatalogPath);
        var session = new GameObject("GameSession").AddComponent<GameSession>();
        PlayerTestSceneBuilder.SetProps(session, so =>
        {
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_abilities").objectReferenceValue = abilities;
            so.FindProperty("_loadoutScene").stringValue = LoadoutScene;
            so.FindProperty("_titleScene").stringValue = TitleScene;
            so.FindProperty("_resultScene").stringValue = ResultScene;
        });
        var menu = new GameObject("RunTitleMenu").AddComponent<RunTitleMenu>();
        PlayerTestSceneBuilder.SetProps(menu, so => so.FindProperty("_catalog").objectReferenceValue = catalog);
        // アプリ図鑑（右上の「図鑑」ボタンで開く。タイトル画面とは別の部品として重ねる。2026-11-17）
        new GameObject("AppDex").AddComponent<AppDexMenu>();
        string path = $"{SceneFolder}/{TitleScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static string BuildLoadout()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 0f, -10f));
        var menu = new GameObject("RunLoadoutMenu").AddComponent<RunLoadoutMenu>();
        PlayerTestSceneBuilder.SetProps(menu, so =>
        {
            so.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
            so.FindProperty("_abilities").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilitiesBuilder.CatalogPath);
        });
        string path = $"{SceneFolder}/{LoadoutScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static string BuildStage(AppCatalog catalog, Sprite square, BossTestSceneBuilder.Attacks attacks, AttackData zakoShot)
    {
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        return BuildOneScreenStage(StageScene, terrain =>
        {
            PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
            PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        }, new Vector2(3.5f, 3f), new Vector2(-3.5f, 3f));
    }

    /// <summary>
    /// 1画面のステージ（雑魚を倒すとボスが出る）を作って保存する。床と左右の壁は共通で、足場は platforms で置く（2026-11-19：AI チャットのボスのステージでも使う）。
    /// walkerOnPlatform・shooterOnPlatform：足場の上に置く雑魚の足元（足場の上面に合わせる）
    /// </summary>
    internal static string BuildOneScreenStage(string sceneName, System.Action<Transform> platforms, Vector2 walkerOnPlatform, Vector2 shooterOnPlatform)
    {
        int ground = LayerMask.NameToLayer("Ground");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 新しいシーンを開いたので、アセットを読み込み直す
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        var abilities = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilitiesBuilder.CatalogPath);

        // --- 部屋（雑魚戦テストシーンと同じ。床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        platforms?.Invoke(terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // --- プレイヤー（能力は G キーではもらえない。能力えらびで3つ持ち、ボスを正常化して増やす） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8.5f, 0.05f, 0f), attacks.PlayerMelee, null);
        Object.DestroyImmediate(player.GetComponent<SkillDebugGiver>());

        // --- 雑魚ウイルス ---
        var wave = new GameObject("EnemyWave").AddComponent<EnemyWave>();
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Floor", new Vector2(2f, 0f), -1, wave.transform, square);
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Folder", walkerOnPlatform, 1, wave.transform, square);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Folder", shooterOnPlatform, zakoShot, player.transform, wave.transform, square);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Floor", new Vector2(8.5f, 0f), zakoShot, player.transform, wave.transform, square);
        // 【仮】雑魚は近接1回で倒れるので、部屋の中に出し続ける。8体出し終えて、全員倒すとボスが出る（最初の4体と合わせて12体。2026-11-17）
        StageTestSceneBuilder.CreateSpawner(player.transform, zakoShot, square, wave, 8, new Vector2(2.2f, 1f), new Vector2Int(3, 6), 45f, ArenaBounds, false);

        // --- ボスの登場と、ステージの進行（どのボスが出るかは実行時に決まる） ---
        var entrance = new GameObject("BossEntrance").AddComponent<BossEntrance>();
        PlayerTestSceneBuilder.SetProps(entrance, so => so.FindProperty("_wave").objectReferenceValue = wave);
        var director = new GameObject("StageDirector").AddComponent<StageDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_abilities").objectReferenceValue = abilities;
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_entrance").objectReferenceValue = entrance;
            so.FindProperty("_player").objectReferenceValue = player.GetComponent<PlayerSkills>();
        });
        // ステージの色をアプリの色にする（StageDirector が Apply を呼ぶ）。元の絵を覚えさせる
        var stageTheme = director.gameObject.AddComponent<StageTheme>();
        PlayerTestSceneBuilder.SetProps(stageTheme, so =>
        {
            so.FindProperty("_wallpaper").objectReferenceValue = ArtAssets.LoadSprite("Generated/Background/Bg_Desktop.png");
            so.FindProperty("_floor").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Tile_Floor.png");
            so.FindProperty("_wall").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Tile_Wall.png");
            so.FindProperty("_folder").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Platform_Folder.png");
        });
        // 画面表示：ハート・能力の枠・ボスのゲージ（文字なし）。背景はデスクトップの壁紙
        Camera cam = CreateCamera(new Vector3(0f, 5.5f, -10f));
        ArtLib.CreateBackground(cam);
        ArtLib.CreateHud(cam, player, director);
        string path = $"{SceneFolder}/{sceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    /// <summary>Build Settings に、シーンを1つ足す（すでにあれば何もしない。2026-11-19：AI チャットのボスのステージ）</summary>
    internal static void AddBuildScene(string path)
    {
        if (EditorBuildSettings.scenes.Any(s => s.path == path)) return;
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();
    }

    internal const string RunSceneFolder = SceneFolder;

    static string BuildResult()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 0f, -10f));
        new GameObject("RunResultScreen").AddComponent<RunResultScreen>();
        string path = $"{SceneFolder}/{ResultScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static Camera CreateCamera(Vector3 position)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = position;
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = ArtLib.Sky;
        camGo.AddComponent<AudioListener>();
        return cam;
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
