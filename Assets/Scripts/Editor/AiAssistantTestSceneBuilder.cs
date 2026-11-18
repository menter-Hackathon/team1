using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// AI の能力「AIアシスタント」を試すテストシーンを作るエディタ拡張（2026-11-18）。
/// メニュー「Tools/Team1/AIアシスタントのテストシーンを作成」から実行する。ほかのテストシーンは変えない。
/// ・部屋は雑魚戦テストシーンと同じ形（床・左右の壁・高さ3の足場2枚）に、壁越しの敵を狙わないかを見る仕切り（x=5、高さ3）を足した
/// ・プレイヤーは最初から AIアシスタントを1枠目に持っている（1 キーで使う。G キーでもう一度もらえる）
/// ・雑魚：歩く×2（床・右の足場）、撃つ×1（左の足場）、仕切りの向こうに歩く×1。そのあとも雑魚を出し続ける（ボスは出ない）
/// 数値はすべて【仮】。
/// </summary>
public static class AiAssistantTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/AiAssistantTest.unity";
    static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);

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
        StageTestSceneBuilder.GetOrCreateZakoShot();
        AbilitiesBuilder.EnsureAiCategory(AbilitiesBuilder.GetOrCreateAiAssistant());
        ArtAssets.Apply(); // 絵の取り込み設定と、ロボット・レーザーの絵の割り当て
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（取り込みやシーンの切り替えで、前に読んだ物が使えなくなることがある）
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();
        SkillDefinition ai = AbilitiesBuilder.GetOrCreateAiAssistant();

        // --- 部屋（雑魚戦テストシーンと同じ。床の上面が y=0）＋仕切り ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        // 仕切り：この向こうの雑魚は、レーザーの通り道が壁にさえぎられるあいだは狙わない
        PlayerTestSceneBuilder.CreateBlock("Wall_Divider", new Vector2(5.5f, 1.5f), new Vector2(0.6f, 3f), ground, ArtLib.BlockKind.Wall, terrain);

        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // --- プレイヤー（最初から AIアシスタントを持つ） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-6f, 0.05f, 0f), attacks.PlayerMelee, new[] { ai });
        PlayerTestSceneBuilder.SetProps(player.GetComponent<SkillDebugGiver>(), so => so.FindProperty("_giveOnStart").boolValue = true);

        // --- 雑魚ウイルス（HP の仮表示つき） ---
        var enemies = new GameObject("Enemies").transform;
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Floor", new Vector2(1f, 0f), -1, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateWalker("Zako_Walker_Folder", new Vector2(3.5f, 3f), 1, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateShooter("Zako_Shooter_Folder", new Vector2(-3.5f, 3f), zakoShot, player.transform, enemies, square, debugLabel: true);
        StageTestSceneBuilder.CreateWalker("Zako_Walker_BehindWall", new Vector2(8f, 0f), 1, enemies, square, debugLabel: true);
        // 【仮】雑魚を出し続ける（ノルマなし。同時に2〜4体）
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

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log($"AIアシスタントのテストシーンを作成しました: {ScenePath}（1 キーで AIアシスタントを使う）");
    }
}
