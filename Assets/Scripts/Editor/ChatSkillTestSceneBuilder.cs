using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// チャットアプリの能力（Slack・Discord・LINE）と、チャットのボスを試すテストシーン（2026-11-19 にボス戦へ書き換え）。
/// メニュー「Tools/Team1/チャットアプリの能力のテストシーンを作成」から実行する。ほかのテストシーンは変えない。
/// ・部屋は AI チャットのボスのステージと同じ形（すり抜け床の多い1画面。AiChatBossBuilder.BuildPlatforms）
/// ・プレイヤーは最初から 1：LINE、2：Slack、3：Discord を持っている
/// ・ボスはチャットのボス。見た目は C キーで Slack → Discord → LINE と切り替わる（Mac に入っていればそのアイコン。メッセージの絵も変わる）
/// ・R：やり直し、N：ゲージ +10（BossTestDirector）
/// </summary>
public static class ChatSkillTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/ChatSkillTest.unity";

    [MenuItem("Tools/Team1/チャットアプリの能力のテストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod ChatSkillTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (ground < 0 || oneWay < 0 || LayerMask.NameToLayer("Player") < 0 || LayerMask.NameToLayer("Enemy") < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }

        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        StageTestSceneBuilder.GetOrCreateZakoShot();
        AbilitiesBuilder.EnsureChatSkills();
        ArtAssets.Apply(); // 絵の取り込み設定（先に入れておくと、技に絵が入る）
        ChatBossBuilder.GetOrCreateProfile();
        ArtAssets.Apply(); // メッセージの絵・エフェクト・アイコンの割り当て
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        var skills = new[]
        {
            AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Line.asset"),
            AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Slack.asset"),
            AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Discord.asset"),
        };
        Rect arenaBounds = AiChatBossBuilder.ArenaBounds;

        // --- 部屋（床の上面が y=0）。すり抜け床の多い形 ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        AiChatBossBuilder.BuildPlatforms(terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = arenaBounds);

        // --- プレイヤー（最初から LINE・Slack・Discord を持つ） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8f, 0.05f, 0f), attacks.PlayerMelee, skills);
        PlayerTestSceneBuilder.SetProps(player.GetComponent<SkillDebugGiver>(), so => so.FindProperty("_giveOnStart").boolValue = true);

        // --- ボス（チャット） ---
        BossController boss = ChatBossBuilder.CreateBoss(new Vector2(arenaBounds.center.x, arenaBounds.yMin) + AiChatBossBuilder.SpawnPosition, player.transform);
        var director = new GameObject("BossTestDirector").AddComponent<BossTestDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            var arr = so.FindProperty("_bosses");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            so.FindProperty("_player").objectReferenceValue = player.transform;
            so.FindProperty("_showUpdateBar").boolValue = false; // AI ではないので、「アップデート中…」のような表示は出さない
        });
        var cycler = director.gameObject.AddComponent<BossAppLookCycler>();
        PlayerTestSceneBuilder.SetProps(cycler, so =>
        {
            so.FindProperty("_boss").objectReferenceValue = boss;
            Strings(so.FindProperty("_names"), "Slack", "Discord", "LINE");
            Strings(so.FindProperty("_bundleIds"), "com.tinyspeck.slackmacgap", "com.hnc.Discord", "jp.naver.line.mac");
        });

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

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log($"チャットアプリの能力（チャットのボス戦）のテストシーンを作成しました: {ScenePath}（1：LINE　2：Slack　3：Discord　C：ボスのアプリを切り替え　R：やり直し　N：ゲージ+10）");
    }

    static void Strings(SerializedProperty arr, params string[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
