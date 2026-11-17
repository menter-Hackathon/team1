using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ステージの種類（2026-11-17 まさぴ〜決定）のシーンを作る。ランのシーンの作成ツール（RunScenesBuilder）から呼ぶ。
/// ・Stage_Scroll（横スクロール、Chrome）：壁紙の上を右へ進む。穴・開いたり閉じたりするウィンドウ・降ってくる通知
/// ・Stage_Windows（ウィンドウ移動、VS Code）：エディタのウィンドウ（1画面ずつ）を渡り歩く。開いたり閉じたりするウィンドウ・ファイアウォール
/// どちらも、最後にいつものボス部屋（横20×高さ11.5、フォルダ2枚）があり、入ると入口が閉じてボスが出る（道中の雑魚は倒さなくてよい）。
/// 配置・数値はすべて【仮】。
/// </summary>
public static class StageLayoutsBuilder
{
    public const string ScrollScene = "Stage_Scroll";
    public const string WindowsScene = "Stage_Windows";
    const float RoomHeight = 11.5f;

    /// <summary>横スクロールのステージ（ボス部屋は x 65〜85）</summary>
    public static string BuildScroll(string folder, AppCatalog catalog)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ctx = new Context(catalog);
        var arena = new Rect(65f, 0f, 20f, RoomHeight);
        var terrain = new GameObject("Terrain").transform;

        // 床（穴は 6〜9、22〜28、44〜47）と、左右の壁
        Ground(terrain, "Ground_1", -10f, 6f);
        Ground(terrain, "Ground_2", 9f, 22f);
        Ground(terrain, "Ground_3", 28f, 44f);
        Ground(terrain, "Ground_4", 47f, 85f);
        Wall(terrain, "Wall_Left", -10.5f);
        Wall(terrain, "Wall_Right", arena.xMax + 0.5f);
        // フォルダの足場
        foreach (var (x, top) in new[] { (2f, 3f), (14f, 3f), (17.5f, 5.5f), (33f, 3f), (36.5f, 5.5f), (52f, 3f) })
            Folder(terrain, x, top, 2.4f);
        // 開いたり閉じたりするウィンドウ（穴の上の橋と、上の道）
        BlinkingWindow(terrain, "Blink_Pit", 25f, 1.2f, 3f, 0f);
        BlinkingWindow(terrain, "Blink_High", 40.5f, 6.5f, 2.4f, 1.1f);
        BlinkingWindow(terrain, "Blink_Pit3", 45.5f, 1.4f, 2.4f, 0.6f);

        ctx.CreatePlayer(new Vector3(-7.5f, 0.05f, 0f));
        var pit = ctx.Player.AddComponent<PitRecovery>();
        PlayerTestSceneBuilder.SetProps(pit, so =>
        {
            so.FindProperty("_killY").floatValue = -3f;
            so.FindProperty("_safeGround").intValue = 1 << LayerMask.NameToLayer("Ground");
        });

        // 雑魚（道中）
        var wave = new GameObject("Zako").AddComponent<EnemyWave>();
        StageTestSceneBuilder.CreateWalker("Walker_1", new Vector2(13f, 0f), -1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateShooter("Shooter_1", new Vector2(19.5f, 0f), ctx.ZakoShot, ctx.Player.transform, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateWalker("Walker_2", new Vector2(33f, 3f), 1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateWalker("Walker_3", new Vector2(40f, 0f), -1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateShooter("Shooter_2", new Vector2(52f, 3f), ctx.ZakoShot, ctx.Player.transform, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateWalker("Walker_4", new Vector2(57f, 0f), -1, wave.transform, ctx.Square);

        // 降ってくる通知（x 28〜62）
        NotificationDropper(ctx, 28f, 62f);

        ctx.BossRoom(terrain, arena, wave, StageCamera.Mode.FollowX, -10f + 10.67f, arena.xMax - 10.67f, null,
            gateX: arena.xMin - 0.5f, gateBottom: 0f, gateTop: 14f, triggerX: arena.xMin + 1.5f);

        string path = $"{folder}/{ScrollScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    /// <summary>ウィンドウ移動のステージ（部屋の真ん中 x＝0・21・42、ボス部屋は 63）</summary>
    public static string BuildWindows(string folder, AppCatalog catalog)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ctx = new Context(catalog);
        float[] rooms = { 0f, 21f, 42f, 63f };
        var arena = new Rect(53f, 0f, 20f, RoomHeight);
        var terrain = new GameObject("Terrain").transform;

        Ground(terrain, "Ground", -10f, arena.xMax);
        Wall(terrain, "Wall_Left", -10.5f);
        Wall(terrain, "Wall_Right", arena.xMax + 0.5f);
        // 部屋の間の壁。1→2 は床の高さの出入り口（高さ 3.5）、2→3 は高い出入り口（5.5〜8.5）、3→ボス部屋は床の高さ（入ると閉じる）
        WallPiece(terrain, "Wall_1_2", 10.5f, 3.5f, 14f);
        WallPiece(terrain, "Wall_2_3_Low", 31.5f, 0f, 5.5f);
        WallPiece(terrain, "Wall_2_3_High", 31.5f, 8.5f, 14f);
        WallPiece(terrain, "Wall_3_Boss", 52.5f, 3.5f, 14f);
        // 部屋の見た目（エディタの中身とタイトルバー）
        foreach (float cx in rooms) RoomLook(terrain, cx);

        // 部屋1：開いたり閉じたりするウィンドウを順番に渡る
        BlinkingWindow(terrain, "Blink_1a", -4f, 2.5f, 2.4f, 0f);
        BlinkingWindow(terrain, "Blink_1b", 0f, 4.5f, 2.4f, 0.8f);
        BlinkingWindow(terrain, "Blink_1c", 4f, 2.5f, 2.4f, 1.6f);
        // 部屋2：まん中にファイアウォール。右の壁の高い出入り口へは足場を登る
        Firewall(ctx, terrain, "Firewall_2", 18f, 0f);
        Folder(terrain, 24.5f, 3f, 2.4f);
        Folder(terrain, 28.5f, 5.5f, 2.4f);
        // 部屋3：ファイアウォール2つ（ずらして交互に）と、消える足場
        Firewall(ctx, terrain, "Firewall_3a", 38f, 0f);
        Firewall(ctx, terrain, "Firewall_3b", 46f, 1.7f);
        BlinkingWindow(terrain, "Blink_3", 42f, 3.5f, 2.4f, 0.4f);

        ctx.CreatePlayer(new Vector3(-7.5f, 0.05f, 0f));
        var wave = new GameObject("Zako").AddComponent<EnemyWave>();
        StageTestSceneBuilder.CreateWalker("Walker_1", new Vector2(6f, 0f), -1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateShooter("Shooter_1", new Vector2(8.5f, 0f), ctx.ZakoShot, ctx.Player.transform, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateWalker("Walker_2", new Vector2(25f, 0f), 1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateShooter("Shooter_2", new Vector2(24.5f, 3f), ctx.ZakoShot, ctx.Player.transform, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateWalker("Walker_3", new Vector2(42f, 0f), -1, wave.transform, ctx.Square);
        StageTestSceneBuilder.CreateShooter("Shooter_3", new Vector2(49.5f, 0f), ctx.ZakoShot, ctx.Player.transform, wave.transform, ctx.Square);

        ctx.BossRoom(terrain, arena, wave, StageCamera.Mode.Rooms, 0f, 0f, rooms,
            gateX: 52.5f, gateBottom: 0f, gateTop: 3.5f, triggerX: arena.xMin + 1.5f);

        string path = $"{folder}/{WindowsScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    // ───────── 部品 ─────────

    static int Layer(string name) => LayerMask.NameToLayer(name);

    static void Ground(Transform parent, string name, float x0, float x1) =>
        PlayerTestSceneBuilder.CreateBlock(name, new Vector2((x0 + x1) * 0.5f, -1f), new Vector2(x1 - x0, 2f), Layer("Ground"), ArtLib.BlockKind.Ground, parent);

    static void Wall(Transform parent, string name, float x) =>
        PlayerTestSceneBuilder.CreateBlock(name, new Vector2(x, 6f), new Vector2(1f, 14f), Layer("Ground"), ArtLib.BlockKind.Wall, parent);

    static GameObject WallPiece(Transform parent, string name, float x, float y0, float y1) =>
        PlayerTestSceneBuilder.CreateBlock(name, new Vector2(x, (y0 + y1) * 0.5f), new Vector2(1f, y1 - y0), Layer("Ground"), ArtLib.BlockKind.Wall, parent);

    static void Folder(Transform parent, float x, float top, float width) =>
        PlayerTestSceneBuilder.CreateBlock($"Folder_{x:0.#}", new Vector2(x, top - 0.15f), new Vector2(width, 0.3f), Layer("OneWayPlatform"), ArtLib.BlockKind.Platform, parent);

    /// <summary>開いたり閉じたりするウィンドウの足場（top＝乗る面の高さ）</summary>
    static void BlinkingWindow(Transform parent, string name, float x, float top, float width, float phase)
    {
        var block = PlayerTestSceneBuilder.CreateBlock(name, new Vector2(x, top - 0.15f), new Vector2(width, 0.3f), Layer("OneWayPlatform"), ArtLib.BlockKind.WindowPlatform, parent);
        var blink = block.AddComponent<BlinkingPlatform>();
        var renderers = block.GetComponentsInChildren<SpriteRenderer>(true);
        PlayerTestSceneBuilder.SetProps(blink, so =>
        {
            so.FindProperty("_collider").objectReferenceValue = block.GetComponent<BoxCollider2D>();
            var arr = so.FindProperty("_renderers");
            arr.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.FindProperty("_phase").floatValue = phase;
        });
    }

    /// <summary>ファイアウォール（床から画面の上端まで）</summary>
    static void Firewall(Context ctx, Transform parent, string name, float x, float phase)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector2(x, RoomHeight * 0.5f);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, RoomHeight);
        col.isTrigger = true;
        var hitbox = PlayerTestSceneBuilder.AddHitbox(go, Team.Enemy, 1 << Layer("Player"), damage: 1,
            knockback: new Vector2(6f, 4f), hitStun: 0.25f, hitOnce: false, activeOnStart: false, area: col);
        Sprite[] flames = ArtAssets.LoadFrames("Generated/Hazards/Fx_Firewall.png");
        var flame = ArtLib.AddSized(go.transform, "Flame", flames.Length > 0 ? flames[0] : null, Vector3.zero, new Vector2(1f, RoomHeight), 18, 0);
        ArtLib.Animate(flame, flames, 10f);
        Sprite emitter = ArtAssets.LoadSprite("Generated/Hazards/Firewall_Emitter.png");
        ArtLib.AddArt(go.transform, "Emitter_Bottom", emitter, new Vector3(0f, -RoomHeight * 0.5f + 0.22f, 0f), 19, 0);
        ArtLib.AddArt(go.transform, "Emitter_Top", emitter, new Vector3(0f, RoomHeight * 0.5f - 0.22f, 0f), 19, 0);
        var fw = go.AddComponent<global::Firewall>();
        PlayerTestSceneBuilder.SetProps(fw, so =>
        {
            so.FindProperty("_hitbox").objectReferenceValue = hitbox;
            so.FindProperty("_flame").objectReferenceValue = flame;
            so.FindProperty("_phase").floatValue = phase;
        });
    }

    /// <summary>エディタのウィンドウの部屋の見た目（中身の色と、上のタイトルバー）。判定はない</summary>
    static void RoomLook(Transform parent, float cx)
    {
        var room = new GameObject($"Room_{cx:0}");
        room.transform.SetParent(parent, false);
        // 中身は壁の所まで広げる（部屋の間の出入り口から、外の壁紙が見えないように）
        ArtLib.AddSized(room.transform, "Inside", ArtAssets.LoadSprite("Generated/Stage/Room_Editor.png"), new Vector3(cx, 5.5f, 0f), new Vector2(21f, 12f), -50, 0);
        float barH = 6f / ArtLib.Ppu;
        ArtLib.AddSized(room.transform, "TitleBar", ArtAssets.LoadSprite("Generated/Stage/Room_TitleBar.png"), new Vector3(cx, RoomHeight - barH * 0.5f, 0f), new Vector2(20f, barH), -40, 0);
        ArtLib.AddArt(room.transform, "Close", ArtAssets.LoadSprite("Generated/UI/Btn_Close.png"), new Vector3(cx + 10f - 0.5f, RoomHeight - 0.32f, 0f), -39, 0);
    }

    static void NotificationDropper(Context ctx, float minX, float maxX)
    {
        var data = BossTestSceneBuilder.GetOrCreateAttack("Stage/Attack_Stage_Notification", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Spawn, Color.white);
            so.FindProperty("_objectSize").vector2Value = new Vector2(2.0f, 0.9f);
            so.FindProperty("_fallSpeed").floatValue = 9f;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_landRemainTime").floatValue = 0f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Hazards/Hazard_Notification.png");
        });
        var go = new GameObject("NotificationDropper");
        var runner = BossTestSceneBuilder.AddAttackRunner(go, Team.Enemy, 1 << Layer("Player"), ctx.Player.transform);
        var dropper = go.AddComponent<global::NotificationDropper>();
        PlayerTestSceneBuilder.SetProps(dropper, so =>
        {
            so.FindProperty("_attack").objectReferenceValue = data;
            so.FindProperty("_minX").floatValue = minX;
            so.FindProperty("_maxX").floatValue = maxX;
        });
    }

    /// <summary>ステージで共通に使うもの（プレイヤー・ボス部屋・カメラ・画面表示・ステージの進行）</summary>
    class Context
    {
        public readonly AppCatalog Catalog;
        public readonly Sprite Square;
        public readonly BossTestSceneBuilder.Attacks Attacks;
        public readonly AttackData ZakoShot;
        public GameObject Player;

        public Context(AppCatalog catalog)
        {
            // 新しいシーンを開いたあとに読み込む（前のシーンで読んだ物は使えなくなることがある）
            Catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>("Assets/ScriptableObjects/Run/AppCatalog.asset");
            Square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
            Attacks = BossTestSceneBuilder.GetOrCreateAttacks();
            ZakoShot = StageTestSceneBuilder.GetOrCreateZakoShot();
        }

        public void CreatePlayer(Vector3 position)
        {
            Player = PlayerTestSceneBuilder.CreatePlayer(Square, position, Attacks.PlayerMelee, null);
            Object.DestroyImmediate(Player.GetComponent<SkillDebugGiver>());
        }

        /// <summary>ボス部屋（いつもの部屋）・入口・ステージの進行・カメラ・画面表示を作る</summary>
        public void BossRoom(Transform terrain, Rect arena, EnemyWave wave, StageCamera.Mode mode, float minX, float maxX, float[] rooms,
            float gateX, float gateBottom, float gateTop, float triggerX)
        {
            float cx = arena.center.x;
            Folder(terrain, cx - 3.5f, 3f, 2.4f);
            Folder(terrain, cx + 3.5f, 3f, 2.4f);
            var arenaGo = new GameObject("BattleArena").AddComponent<BattleArena>();
            PlayerTestSceneBuilder.SetProps(arenaGo, so => so.FindProperty("_bounds").rectValue = arena);

            // ボスの登場（入口から呼ばれるまで待つ）とステージの進行
            var entrance = new GameObject("BossEntrance").AddComponent<BossEntrance>();
            PlayerTestSceneBuilder.SetProps(entrance, so => so.FindProperty("_waitForGate").boolValue = true);
            var director = new GameObject("StageDirector").AddComponent<StageDirector>();
            PlayerTestSceneBuilder.SetProps(director, so =>
            {
                so.FindProperty("_catalog").objectReferenceValue = Catalog;
                so.FindProperty("_wave").objectReferenceValue = wave;
                so.FindProperty("_entrance").objectReferenceValue = entrance;
                so.FindProperty("_player").objectReferenceValue = Player.GetComponent<PlayerSkills>();
            });
            var theme = director.gameObject.AddComponent<StageTheme>();
            PlayerTestSceneBuilder.SetProps(theme, so =>
            {
                so.FindProperty("_wallpaper").objectReferenceValue = ArtAssets.LoadSprite("Generated/Background/Bg_Desktop.png");
                so.FindProperty("_floor").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Tile_Floor.png");
                so.FindProperty("_wall").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Tile_Wall.png");
                so.FindProperty("_folder").objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Platform_Folder.png");
                var bars = so.FindProperty("_titleBars");
                bars.arraySize = 2;
                bars.GetArrayElementAtIndex(0).objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Platform_Window.png");
                bars.GetArrayElementAtIndex(1).objectReferenceValue = ArtAssets.LoadSprite("Generated/Stage/Room_TitleBar.png");
            });

            // カメラ
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(Player.transform.position.x, 5.5f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            var stageCam = camGo.AddComponent<StageCamera>();
            PlayerTestSceneBuilder.SetProps(stageCam, so =>
            {
                so.FindProperty("_mode").enumValueIndex = (int)mode;
                so.FindProperty("_target").objectReferenceValue = Player.transform;
                so.FindProperty("_minX").floatValue = minX;
                so.FindProperty("_maxX").floatValue = maxX;
                var arr = so.FindProperty("_roomCentersX");
                arr.arraySize = rooms != null ? rooms.Length : 0;
                for (int i = 0; i < arr.arraySize; i++) arr.GetArrayElementAtIndex(i).floatValue = rooms[i];
            });
            ArtLib.CreateBackground(cam);
            ArtLib.CreateHud(cam, Player, director);

            // 入口：入ったら閉じる壁（最初は非表示）
            var wall = WallPiece(terrain, "Gate", gateX, gateBottom, gateTop);
            wall.SetActive(false);
            var gate = new GameObject("BossRoomGate").AddComponent<BossRoomGate>();
            PlayerTestSceneBuilder.SetProps(gate, so =>
            {
                so.FindProperty("_entrance").objectReferenceValue = entrance;
                so.FindProperty("_camera").objectReferenceValue = stageCam;
                so.FindProperty("_wall").objectReferenceValue = wall;
                so.FindProperty("_triggerX").floatValue = triggerX;
                so.FindProperty("_lockCameraX").floatValue = cx;
            });
        }
    }
}
