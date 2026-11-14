using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// プレイヤー操作を試すための仮ステージを自動で作るエディタ拡張。
/// メニュー「Tools/Team1/プレイヤー操作テストシーンを作成」から実行する。
/// 絵は白い四角の仮素材。デザインが来たら Player の Visual の下を差し替える。
/// </summary>
public static class PlayerTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/PlayerTest.unity";
    const string SpritePath = "Assets/Sprites/Placeholder/Square.png";

    static readonly Color GroundColor = new Color(0.36f, 0.30f, 0.62f);
    static readonly Color OneWayColor = new Color(0.30f, 0.80f, 0.90f);
    static readonly Color PlayerColor = new Color(1.00f, 0.55f, 0.80f);
    static readonly Color BackgroundColor = new Color(0.10f, 0.08f, 0.20f);

    [MenuItem("Tools/Team1/プレイヤー操作テストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod PlayerTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        int playerLayer = LayerMask.NameToLayer("Player");
        if (ground < 0 || oneWay < 0 || playerLayer < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }

        Sprite square = GetOrCreateSquareSprite();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 地形（床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(44f, 2f), ground, GroundColor, square, terrain);
        CreateBlock("Wall_Left", new Vector2(-22.5f, 5f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        CreateBlock("Wall_Right", new Vector2(22.5f, 5f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        CreateBlock("Step_Low", new Vector2(-8f, 0.5f), new Vector2(4f, 1f), ground, GroundColor, square, terrain);
        CreateBlock("Step_High", new Vector2(-13f, 2.5f), new Vector2(4f, 1f), ground, GroundColor, square, terrain);
        CreateBlock("Pillar", new Vector2(8f, 1.5f), new Vector2(1f, 3f), ground, GroundColor, square, terrain);
        CreateBlock("Ceiling", new Vector2(15f, 3.5f), new Vector2(6f, 1f), ground, GroundColor, square, terrain);
        CreateBlock("OneWay_1", new Vector2(0f, 2.5f), new Vector2(4f, 0.3f), oneWay, OneWayColor, square, terrain);
        CreateBlock("OneWay_2", new Vector2(4f, 5f), new Vector2(3f, 0.3f), oneWay, OneWayColor, square, terrain);

        // --- プレイヤー（原点＝足元） ---
        var player = new GameObject("Player");
        player.layer = playerLayer;
        player.transform.position = new Vector3(-3f, 0.5f, 0f);

        var rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var box = player.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.7f, 1.0f);
        box.offset = new Vector2(0f, 0.5f);

        var body = player.AddComponent<KinematicBody2D>();
        var bodySo = new SerializedObject(body);
        bodySo.FindProperty("_solidMask").intValue = 1 << ground;
        bodySo.FindProperty("_oneWayMask").intValue = 1 << oneWay;
        bodySo.ApplyModifiedPropertiesWithoutUndo();

        player.AddComponent<PlayerInputReader>();
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerAnimationBridge>();

        // 見た目（左右反転するのはこの Visual だけ）
        var visual = new GameObject("Visual").transform;
        visual.SetParent(player.transform, false);
        var bodySprite = CreateSprite("Body", visual, new Vector2(0f, 0.5f), new Vector2(0.7f, 1.0f), PlayerColor, square, 0);
        bodySprite.gameObject.layer = playerLayer;
        var eye = CreateSprite("Eye", visual, new Vector2(0.18f, 0.72f), new Vector2(0.15f, 0.2f), Color.white, square, 1);
        eye.gameObject.layer = playerLayer;

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("_visualRoot").objectReferenceValue = visual;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        // --- カメラ ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 2f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<CameraFollow2D>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("_target").objectReferenceValue = player.transform;
        followSo.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log($"プレイヤー操作テストシーンを作成しました: {ScenePath}");
    }

    static void CreateBlock(string name, Vector2 center, Vector2 size, int layer, Color color, Sprite sprite, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
    }

    static SpriteRenderer CreateSprite(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 10 + order;
        return sr;
    }

    static Sprite GetOrCreateSquareSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        EnsureFolder(Path.GetDirectoryName(SpritePath).Replace('\\', '/'));
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f; // 16px = 1ユニット
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
