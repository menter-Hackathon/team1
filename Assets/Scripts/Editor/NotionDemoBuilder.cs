using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>既存シーンを上書きせず、ToolsからNotion専用テストを起動する。</summary>
public static class NotionDemoBuilder
{
    const string ScenePath = "Assets/Scenes/Test/NotionTest.unity";

    [MenuItem("Tools/Team1/Notion/技を試す")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("Playを停止してからNotionのテストを開いてください。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            var existing = Object.FindObjectOfType<NotionSkillDemo>();
            if (existing != null)
            {
                SetBossLook(existing);
                SetSharedLayout(existing);
                EditorSceneManager.SaveScene(existing.gameObject.scene);
            }
        }
        else if (!Build()) return;
        EditorApplication.isPlaying = true;
    }

    static bool Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int enemy = LayerMask.NameToLayer("Enemy");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (ground < 0 || enemy < 0 || oneWay < 0 || LayerMask.NameToLayer("Player") < 0)
        {
            Debug.LogError("Notionテストに必要なGround / Enemy / Player / OneWayPlatformレイヤーがありません。");
            return false;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var terrain = new GameObject("Notion page terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0, -0.5f), new Vector2(22, 1), ground, new Color32(60, 57, 64, 255), square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Left wall", new Vector2(-10.5f, 5), new Vector2(1, 12), ground, Color.gray, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Right wall", new Vector2(10.5f, 5), new Vector2(1, 12), ground, Color.gray, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Page block left", new Vector2(-3.5f, 2.8f), new Vector2(3, 0.3f), oneWay, Color.gray, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Page block right", new Vector2(3, 2.8f), new Vector2(3, 0.3f), oneWay, Color.gray, square, terrain);
        var melee = AssetDatabase.LoadAssetAtPath<AttackData>("Assets/ScriptableObjects/Attacks/Player/Attack_Player_Melee.asset");
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-5, 0.1f, 0), melee, new SkillDefinition[0]);

        var boss = new GameObject("Notion - infected page");
        boss.layer = enemy;
        boss.transform.position = new Vector3(6, 1.25f, 0);
        boss.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        var gauge = boss.AddComponent<VirusGauge>();
        var body = boss.AddComponent<BoxCollider2D>();
        body.size = new Vector2(4f, 3f);
        body.isTrigger = true;
        boss.AddComponent<Hurtbox>();

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0, 3.8f, -10);
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7.5f;
        camera.backgroundColor = new Color32(216, 213, 222, 255);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraGo.AddComponent<AudioListener>();

        var demo = new GameObject("Notion skill demo").AddComponent<NotionSkillDemo>();
        PlayerTestSceneBuilder.SetProps(demo, so =>
        {
            so.FindProperty("_player").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("_boss").objectReferenceValue = gauge;
            so.FindProperty("_bossBody").objectReferenceValue = body;
            so.FindProperty("_camera").objectReferenceValue = camera;
        });
        SetBossLook(demo);
        SetSharedLayout(demo);
        PlayerTestSceneBuilder.EnsureFolder("Assets/Scenes/Test");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (saved) Debug.Log("Notion技テストを作成しました。T: ToDo / Y: トグル / 1: 設置・発射 / R: リセット");
        return saved;
    }

    static void SetBossLook(NotionSkillDemo demo)
    {
        PlayerTestSceneBuilder.SetProps(demo, so =>
        {
            so.FindProperty("_bossBodySheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Body_Stand.png");
            so.FindProperty("_bossFaceSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Face.png");
            so.FindProperty("_bossHandsSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Generated/Bosses/BossB_Hands.png");
        });
    }

    static void SetSharedLayout(NotionSkillDemo demo)
    {
        var so = new SerializedObject(demo);
        var camera = so.FindProperty("_camera").objectReferenceValue as Camera;
        var player = so.FindProperty("_player").objectReferenceValue as PlayerController;
        var bossBody = so.FindProperty("_bossBody").objectReferenceValue as BoxCollider2D;
        if (bossBody != null)
        {
            bossBody.size = new Vector2(4f, 3f);
            bossBody.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            Vector3 position = bossBody.transform.position;
            bossBody.transform.position = new Vector3(position.x, 1.25f, position.z);
        }
        if (camera == null || player == null) return;
        // BossTestと同じ固定カメラ・壁紙・フォルダ足場・HUDを呼び出す。
        camera.transform.position = new Vector3(0f, 5.5f, -10f);
        camera.orthographicSize = 6f;
        if (camera.transform.Find("Background") == null) ArtLib.CreateBackground(camera);
        if (camera.GetComponentInChildren<PixelHud>() == null) ArtLib.CreateHud(camera, player.gameObject, null);
        Transform terrain = GameObject.Find("Notion page terrain")?.transform;
        if (terrain == null) return;
        foreach (Transform block in terrain)
        {
            if (block.Find("Art") != null) continue;
            ArtLib.BlockKind kind = block.name == "Floor" ? ArtLib.BlockKind.Ground
                : block.name.Contains("wall") ? ArtLib.BlockKind.Wall : ArtLib.BlockKind.Platform;
            var size = new Vector2(Mathf.Abs(block.localScale.x), Mathf.Abs(block.localScale.y));
            ArtLib.DecorateBlock(block.gameObject, size, kind);
        }
    }
}
