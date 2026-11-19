using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 能力を試すテストシーンを作る（2026-11-19。能力ごとのテストシーンで共通に使う）。
/// ・部屋は雑魚戦テストシーンと同じ形（床・左右の壁・高さ3の足場2枚）
/// ・プレイヤーは最初から、渡した能力を 1・2・3 の枠に持っている（G キーでもう一度もらえる）
/// ・雑魚（HP の仮表示つき）が出続ける。ボスは出ない
/// </summary>
public static class SkillTestSceneBuilder
{
    static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);

    /// <summary>
    /// テストシーンを作って保存する（呼ぶ前に、能力のアセットと絵を用意しておく）。
    /// startLife が1以上なら、プレイヤーはそのライフから始まる（H キーで戻せる。回復を確かめるとき用）
    /// </summary>
    public static void Build(string scenePath, SkillDefinition[] skills, string label, int startLife = 0)
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (ground < 0 || oneWay < 0 || LayerMask.NameToLayer("Player") < 0 || LayerMask.NameToLayer("Enemy") < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }
        // 能力のアセットのパスを覚えておく（新しいシーンを開くと、前に読んだ物が使えなくなることがある）
        var skillPaths = new string[skills.Length];
        for (int i = 0; i < skills.Length; i++) skillPaths[i] = skills[i] != null ? AssetDatabase.GetAssetPath(skills[i]) : null;

        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        StageTestSceneBuilder.GetOrCreateZakoShot();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();
        var loaded = new SkillDefinition[skillPaths.Length];
        for (int i = 0; i < skillPaths.Length; i++) loaded[i] = string.IsNullOrEmpty(skillPaths[i]) ? null : AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPaths[i]);

        // --- 部屋（雑魚戦テストシーンと同じ。床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // --- プレイヤー（最初から能力を持つ） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-7f, 0.05f, 0f), attacks.PlayerMelee, loaded);
        PlayerTestSceneBuilder.SetProps(player.GetComponent<SkillDebugGiver>(), so => so.FindProperty("_giveOnStart").boolValue = true);
        if (startLife > 0)
        {
            var life = player.AddComponent<DebugStartLife>();
            PlayerTestSceneBuilder.SetProps(life, so => so.FindProperty("_life").intValue = startLife);
        }

        // --- 雑魚ウイルス（HP の仮表示つき）。そのあとも出し続ける ---
        var enemies = new GameObject("Enemies").transform;
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Floor", new Vector2(2f, 0f), -1, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Folder", new Vector2(3.5f, 3f), 1, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Floor", new Vector2(8.5f, 0f), zakoShot, player.transform, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateSpawner(player.transform, zakoShot, square, null, 0, new Vector2(2.5f, 4f), new Vector2Int(2, 4), 20f, ArenaBounds, false);

        // --- カメラ（固定） ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 5.5f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        ArtLib.CreateBackground(cam);
        ArtLib.CreateHud(cam, player, null);

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, scenePath);
        Selection.activeGameObject = player;
        Debug.Log($"{label}のテストシーンを作成しました: {scenePath}");
    }
}

/// <summary>
/// ボス戦のテストシーンを作る（2026-11-19。アプリのアイコンのボスのテストシーンで共通に使う）。
/// ・部屋は AI チャットのボスのステージと同じ形（すり抜け床の多い1画面）
/// ・プレイヤーは最初から、渡した能力を 1・2・3 の枠に持っている
/// ・ボスは createBoss で作る。C キーで見た目のアプリを切り替える（names・bundleIds）。R：やり直し、N：ゲージ +10
/// </summary>
public static class BossBattleTestSceneBuilder
{
    public static void Build(string scenePath, string[] skillPaths, System.Func<Vector2, Transform, BossController> createBoss,
        string[] names, string[] bundleIds, bool showUpdateBar, string label, Vector2? spawn = null)
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (ground < 0 || oneWay < 0 || LayerMask.NameToLayer("Player") < 0 || LayerMask.NameToLayer("Enemy") < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }
        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        var skills = new SkillDefinition[skillPaths.Length];
        for (int i = 0; i < skillPaths.Length; i++) skills[i] = AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPaths[i]);
        Rect arenaBounds = AiChatBossBuilder.ArenaBounds;

        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        AiChatBossBuilder.BuildPlatforms(terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = arenaBounds);

        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8f, 0.05f, 0f), attacks.PlayerMelee, skills);
        PlayerTestSceneBuilder.SetProps(player.GetComponent<SkillDebugGiver>(), so => so.FindProperty("_giveOnStart").boolValue = true);

        BossController boss = createBoss(new Vector2(arenaBounds.center.x, arenaBounds.yMin) + (spawn ?? AiChatBossBuilder.SpawnPosition), player.transform);
        var director = new GameObject("BossTestDirector").AddComponent<BossTestDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            var arr = so.FindProperty("_bosses");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            so.FindProperty("_player").objectReferenceValue = player.transform;
            so.FindProperty("_showUpdateBar").boolValue = showUpdateBar;
        });
        var cycler = director.gameObject.AddComponent<BossAppLookCycler>();
        PlayerTestSceneBuilder.SetProps(cycler, so =>
        {
            so.FindProperty("_boss").objectReferenceValue = boss;
            var n = so.FindProperty("_names");
            n.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) n.GetArrayElementAtIndex(i).stringValue = names[i];
            var b = so.FindProperty("_bundleIds");
            b.arraySize = bundleIds.Length;
            for (int i = 0; i < bundleIds.Length; i++) b.GetArrayElementAtIndex(i).stringValue = bundleIds[i];
        });

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 5.5f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        ArtLib.CreateBackground(cam);
        ArtLib.CreateHud(cam, player, null);

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, scenePath);
        Selection.activeGameObject = player;
        Debug.Log($"{label}のテストシーンを作成しました: {scenePath}");
    }
}

/// <summary>
/// Photoshop の能力と、Photoshop のボスを試すテストシーン（2026-11-19。ボス戦）。
/// メニュー「Tools/Team1/Photoshopの能力のテストシーンを作成」から実行する。
/// プレイヤーは最初から 1：Photoshop を持っている。ボスは Photoshop のボス（C キーで Photoshop → Figma → Illustrator と見た目を切り替え）
/// </summary>
public static class PhotoshopTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/PhotoshopTest.unity";

    [MenuItem("Tools/Team1/Photoshopの能力のテストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod PhotoshopTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        AbilitiesBuilder.EnsurePhotoshopSkill();
        ArtAssets.Apply(); // 絵の取り込み設定（先に入れておくと、技に絵が入る）
        PhotoshopBossBuilder.GetOrCreateProfile();
        ArtAssets.Apply(); // 筆・インク・アイコン・状態異常の印の割り当て
        BossBattleTestSceneBuilder.Build(ScenePath, new[] { "Assets/ScriptableObjects/Skills/Skill_App_Photoshop.asset" },
            (pos, player) => PhotoshopBossBuilder.CreateBoss(pos, player),
            new[] { "Photoshop", "Figma", "Illustrator" }, new[] { "com.adobe.Photoshop", "com.figma.Desktop", "com.adobe.illustrator" },
            false, "Photoshop の能力（Photoshop のボス戦。1：Photoshop　C：ボスのアプリを切り替え　R：やり直し　N：ゲージ+10）");
    }
}

/// <summary>
/// ブラウザの能力「パケット」を試すテストシーン（2026-11-19）。メニュー「Tools/Team1/パケットの能力のテストシーンを作成」から実行する。
/// プレイヤーは最初から 1：パケット を持ち、ライフ1から始まる（倒すと応答のパケットで回復するのを見るため。H キーでライフ1に戻す）。雑魚が出続ける
/// </summary>
public static class PacketTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/PacketTest.unity";

    [MenuItem("Tools/Team1/パケットの能力のテストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod PacketTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        AbilitiesBuilder.EnsurePacketSkill();
        ArtAssets.Apply(); // パケットの絵・アイコンの割り当て
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Packet.asset");
        SkillTestSceneBuilder.Build(ScenePath, new[] { skill }, "パケットの能力（1：パケット　H：ライフを1に戻す）", startLife: 1);
    }
}
