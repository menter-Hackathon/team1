using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FigmaDemoBuilder
{
    const string ScenePath = "Assets/Scenes/Test/FigmaTest.unity";

    [MenuItem("Tools/Team1/Figma/技を試す")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("Playを停止してからFigmaのテストを開いてください。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            EditorSceneManager.OpenScene(ScenePath);
        else if (!Build()) return;
        EditorApplication.isPlaying = true;
    }

    static bool Build()
    {
        int ground = LayerMask.NameToLayer("Ground"), enemy = LayerMask.NameToLayer("Enemy");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (ground < 0 || enemy < 0 || oneWay < 0 || LayerMask.NameToLayer("Player") < 0)
        {
            Debug.LogError("技テストに必要なGround / Enemy / Player / OneWayPlatformレイヤーがありません。");
            return false;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var terrain = new GameObject("Figma terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0, -0.5f), new Vector2(22, 1), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Left wall", new Vector2(-10.5f, 5), new Vector2(1, 12), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Right wall", new Vector2(10.5f, 5), new Vector2(1, 12), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Left folder", new Vector2(-3.5f, 2.8f), new Vector2(3, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        PlayerTestSceneBuilder.CreateBlock("Right folder", new Vector2(3, 2.8f), new Vector2(3, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var melee = AssetDatabase.LoadAssetAtPath<AttackData>("Assets/ScriptableObjects/Attacks/Player/Attack_Player_Melee.asset");
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-5, 0.1f, 0), melee, new SkillDefinition[0]);
        var boss = new GameObject("Boss Figma") { layer = enemy };
        boss.transform.position = new Vector3(6, 1.25f, 0);
        boss.transform.localScale = new Vector3(0.7f, 0.7f, 1);
        var gauge = boss.AddComponent<VirusGauge>();
        var body = boss.AddComponent<BoxCollider2D>();
        body.size = new Vector2(4, 3);
        body.isTrigger = true;
        boss.AddComponent<Hurtbox>();
        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraGo.transform.position = new Vector3(0, 5.5f, -10);
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraGo.AddComponent<AudioListener>();
        ArtLib.CreateBackground(camera);
        ArtLib.CreateHud(camera, player, null);
        var demo = new GameObject("Figma skill demo").AddComponent<FigmaSkillDemo>();
        PlayerTestSceneBuilder.SetProps(demo, so =>
        {
            so.FindProperty("_player").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("_boss").objectReferenceValue = gauge;
            so.FindProperty("_bossBody").objectReferenceValue = body;
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_bossBodySheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Body_Stand.png");
            so.FindProperty("_bossFaceSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Face.png");
            so.FindProperty("_bossHandsSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Hands.png");
        });
        PlayerTestSceneBuilder.EnsureFolder("Assets/Scenes/Test");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (saved) Debug.Log("Figma技テストを作成しました。O: 付箋 / P: 選択ハンドル / I: ワープ / 1: 付箋トラップ / F1: 操作パネル");
        return saved;
    }
}
