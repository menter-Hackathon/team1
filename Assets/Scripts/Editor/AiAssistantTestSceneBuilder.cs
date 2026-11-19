using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// AI の能力「AIアシスタント」と、AI チャットのボスを試すテストシーン（2026-11-19 にボス戦へ書き換え）。
/// メニュー「Tools/Team1/AIアシスタントのテストシーンを作成」から実行する。ほかのテストシーンは変えない。
/// ・部屋は AI チャットのボスのステージと同じ形（すり抜け床の多い1画面。AiChatBossBuilder.BuildPlatforms）
/// ・プレイヤーは最初から AIアシスタントを1枠目に持っている（1 キーで使う。G キーでもう一度もらえる）
/// ・ボスは AI チャットのボス。見た目は C キーで ChatGPT → Claude → Perplexity → Codex と切り替わる（Mac に入っていればそのアイコン）
/// ・R：やり直し、N：ゲージ +10（BossTestDirector）
/// 数値はすべて【仮】。
/// </summary>
public static class AiAssistantTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/AiAssistantTest.unity";

    [MenuItem("Tools/Team1/AIアシスタントのテストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod AiAssistantTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (ground < 0 || oneWay < 0 || playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }

        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        AbilitiesBuilder.EnsureAiCategory(AbilitiesBuilder.GetOrCreateAiAssistant());
        AiChatBossBuilder.GetOrCreateProfile();
        ArtAssets.Apply(); // 絵の取り込み設定と、ロボット・レーザー・ボスの技の絵の割り当て
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（取り込みやシーンの切り替えで、前に読んだ物が使えなくなることがある）
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        SkillDefinition ai = AbilitiesBuilder.GetOrCreateAiAssistant();
        Rect arenaBounds = AiChatBossBuilder.ArenaBounds;

        // --- 部屋（床の上面が y=0）。すり抜け床の多い形 ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        AiChatBossBuilder.BuildPlatforms(terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = arenaBounds);

        // --- プレイヤー（最初から AIアシスタントを持つ） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8f, 0.05f, 0f), attacks.PlayerMelee, new[] { ai });
        PlayerTestSceneBuilder.SetProps(player.GetComponent<SkillDebugGiver>(), so => so.FindProperty("_giveOnStart").boolValue = true);

        // --- ボス（AI チャット） ---
        BossController boss = AiChatBossBuilder.CreateBoss(new Vector2(arenaBounds.center.x, arenaBounds.yMin) + AiChatBossBuilder.SpawnPosition, player.transform);
        var director = new GameObject("BossTestDirector").AddComponent<BossTestDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            var arr = so.FindProperty("_bosses");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            so.FindProperty("_player").objectReferenceValue = player.transform;
        });
        var cycler = director.gameObject.AddComponent<BossAppLookCycler>();
        PlayerTestSceneBuilder.SetProps(cycler, so => so.FindProperty("_boss").objectReferenceValue = boss);

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
        Debug.Log($"AIアシスタント（AI チャットのボス戦）のテストシーンを作成しました: {ScenePath}（1：AIアシスタント　C：ボスのアプリを切り替え　R：やり直し　N：ゲージ+10）");
    }
}
