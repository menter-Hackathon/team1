using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 雑魚戦 → ボス登場 → 正常化 の流れを1画面で試すテストシーンを作るエディタ拡張（M4）。
/// メニュー「Tools/Team1/雑魚戦テストシーンを作成」から実行する。
/// 部屋はボス戦テストシーンと同じ形（床・左右の壁・高さ3の足場2枚）。
/// 雑魚：歩く×2（床と右の足場）、撃つ×2（左の足場と床の右端）。全員倒すと Chrome が出てくる。
/// 数値はすべて【仮】。
/// 【仮】マウスで動かせる盾ウィンドウを1枚置く（ShieldWindowSceneBuilder。レイヤー BulletBlocker が必要）。
/// </summary>
public static class StageTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/StageTest.unity";

    static readonly Color WalkerColor = new Color(0.45f, 0.85f, 0.35f);
    static readonly Color ShooterColor = new Color(0.75f, 0.40f, 0.95f);
    static readonly Color ZakoShotColor = new Color(0.85f, 0.55f, 1.00f);
    static readonly Color ChromeBodyColor = new Color(0.92f, 0.92f, 0.95f);
    static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);

    [MenuItem("Tools/Team1/雑魚戦テストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod StageTestSceneBuilder.Build）</summary>
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
        if (!ShieldWindowSceneBuilder.CheckLayer()) return;

        BossTestSceneBuilder.GetOrCreateAttacks(); // 無ければ作る
        GetOrCreateZakoShot();
        AbilitiesBuilder.GetOrCreate(); // カテゴリの能力（無ければ作る）
        ArtAssets.Apply(); // 絵の取り込み設定と、攻撃データ・能力・ボスへの割り当て
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（取り込みやシーンの切り替えで、前に読んだ物が使えなくなることがある）
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = GetOrCreateZakoShot();

        // --- 部屋（ボス戦テストシーンと同じ。床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);

        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // 【仮】盾ウィンドウ（マウスでドラッグ。敵の弾だけを止める）
        ShieldWindowSceneBuilder.Create(square, ShieldWindowSceneBuilder.DefaultPosition);

        // --- プレイヤー（左端から始める） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8.5f, 0.05f, 0f), attacks.PlayerMelee,
            AbilitiesBuilder.GetOrCreate().ForDebugKey);

        // --- 雑魚ウイルス ---
        var wave = new GameObject("EnemyWave").AddComponent<EnemyWave>();
        // テストシーンなので、HP の仮表示（頭の上の文字）を出す
        CreateWalker("Zako_Walker_Floor", new Vector2(2f, 0f), -1, wave.transform, square, debugLabel: true);
        CreateWalker("Zako_Walker_Folder", new Vector2(3.5f, 3f), 1, wave.transform, square, debugLabel: true);
        CreateShooter("Zako_Shooter_Folder", new Vector2(-3.5f, 3f), zakoShot, player.transform, wave.transform, square, debugLabel: true);
        CreateShooter("Zako_Shooter_Floor", new Vector2(8.5f, 0f), zakoShot, player.transform, wave.transform, square, debugLabel: true);
        // 【仮】部屋の中に雑魚を出し続ける。8体出し終えて、全員倒すと Chrome が出る（最初の4体と合わせて12体）
        CreateSpawner(player.transform, zakoShot, square, wave, 8, new Vector2(2.2f, 1f), new Vector2Int(3, 6), 45f, ArenaBounds, false);

        // --- ボス（雑魚を全員倒すと出てくる） ---
        var chrome = BossTestSceneBuilder.CreateBoss("Chrome", attacks.ChromeProfile, new Vector2(0f, 2.5f + 1.5f), new Vector2(4f, 3f), ChromeBodyColor, square, player.transform);
        chrome.gameObject.SetActive(false);
        var entrance = new GameObject("BossEntrance").AddComponent<BossEntrance>();
        PlayerTestSceneBuilder.SetProps(entrance, so =>
        {
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_boss").objectReferenceValue = chrome;
        });

        // 【仮】画面表示
        var director = new GameObject("StageTestDirector").AddComponent<StageTestDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_entrance").objectReferenceValue = entrance;
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
        Debug.Log($"雑魚戦テストシーンを作成しました: {ScenePath}");
    }

    /// <summary>撃つ雑魚の弾：1発、毎秒6マス、プレイヤーを狙う、予備動作0.6秒（ダメージ1・ノックバックはボスと同じ）</summary>
    internal static AttackData GetOrCreateZakoShot()
    {
        return BossTestSceneBuilder.GetOrCreateAttack("Zako/Attack_Zako_Shot", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Projectile, ZakoShotColor);
            so.FindProperty("_windup").floatValue = 0.6f;
            so.FindProperty("_projectileCount").intValue = 1;
            so.FindProperty("_spreadAngle").floatValue = 0f;
            so.FindProperty("_speed").floatValue = 6f;
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.4f, 0.4f);
            so.FindProperty("_aim").intValue = (int)AimMode.Target;
        });
    }

    /// <summary>
    /// 雑魚の共通部分：HP1、体の被弾範囲と同じ形の体当たり、点滅、倒れたら煙を出して消える。原点＝足元。
    /// HP1 は 2026-11-17 まさぴ〜決定（近接1回で倒れる。その代わり ZakoSpawner で数を増やす。前は HP5）
    /// 見た目はウイルスの煙の絵（コマ送り）。判定は体の大きさの四角（絵は外してある）。debugLabel なら HP の仮表示を出す
    /// </summary>
    static GameObject CreateZakoBase(string name, Vector2 feet, Vector2 size, Color color, Transform parent, Sprite square,
        Sprite[] frames, bool debugLabel, out Transform bodyTransform, out SpriteRenderer art, int hp = 1)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        var go = new GameObject(name);
        go.layer = enemyLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = feet;

        var health = go.AddComponent<Health>();
        PlayerTestSceneBuilder.SetProps(health, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)Team.Enemy;
            so.FindProperty("_max").intValue = Mathf.Max(1, hp);
            so.FindProperty("_oneDamagePerHit").boolValue = false;
            so.FindProperty("_invincibleTimeAfterHit").floatValue = 0f;
        });
        var blink = go.AddComponent<DamageBlink>();
        go.AddComponent<DisableOnDeath>();
        var death = go.AddComponent<DeathVfx>();
        PlayerTestSceneBuilder.SetProps(death, so => so.FindProperty("_offset").vector2Value = new Vector2(0f, size.y * 0.5f));
        if (debugLabel)
        {
            var label = go.AddComponent<HealthDebugLabel>();
            PlayerTestSceneBuilder.SetProps(label, so => so.FindProperty("_offset").vector3Value = new Vector3(0f, size.y + 0.4f, 0f));
        }

        var visual = new GameObject("Visual").transform;
        visual.SetParent(go.transform, false);
        var body = PlayerTestSceneBuilder.CreateSprite("Body", visual, new Vector2(0f, size.y * 0.5f), size, color, square, 0);
        body.gameObject.layer = enemyLayer;
        bodyTransform = body.transform;
        ArtLib.RemoveRenderer(bodyTransform);
        // 絵（右向き。Visual ごと左右反転する）
        art = ArtLib.AddArt(visual, "Art", null, Vector3.zero, 11, enemyLayer);
        ArtLib.Animate(art, frames, 7f);
        // 点滅させるのは絵だけ
        SpriteRenderer artRef = art;
        PlayerTestSceneBuilder.SetProps(blink, so =>
        {
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = artRef;
        });

        var hurtCol = PlayerTestSceneBuilder.AddHurtbox(bodyTransform.gameObject);
        PlayerTestSceneBuilder.AddHitbox(bodyTransform.gameObject, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: hurtCol);
        return go;
    }

    /// <summary>
    /// 雑魚を出し続ける部品を作る（2026-11-17）。歩く雑魚と撃つ雑魚のひな形を子に置く（非表示）。
    /// wave と quota を渡すと、ノルマを出し終えるまで wave をクリアにしない（1画面のステージ）。数値はすべて【仮】
    /// </summary>
    internal static ZakoSpawner CreateSpawner(Transform player, AttackData shot, Sprite square, EnemyWave wave, int quota,
        Vector2 interval, Vector2Int alive, float rampSeconds, Rect area, bool avoidBossRoom)
    {
        var spawner = new GameObject("ZakoSpawner").AddComponent<ZakoSpawner>();
        CreateWalker("Template_Walker", Vector2.zero, -1, spawner.transform, square);
        CreateShooter("Template_Shooter", Vector2.zero, shot, player, spawner.transform, square);
        var walker = spawner.transform.Find("Template_Walker").gameObject;
        var shooter = spawner.transform.Find("Template_Shooter").gameObject;
        walker.SetActive(false);
        shooter.SetActive(false);
        PlayerTestSceneBuilder.SetProps(spawner, so =>
        {
            var t = so.FindProperty("_templates");
            t.arraySize = 2;
            t.GetArrayElementAtIndex(0).objectReferenceValue = walker;
            t.GetArrayElementAtIndex(1).objectReferenceValue = shooter;
            var w = so.FindProperty("_weights");
            w.arraySize = 2;
            w.GetArrayElementAtIndex(0).floatValue = 0.7f; // 歩く雑魚を多めに
            w.GetArrayElementAtIndex(1).floatValue = 0.3f;
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_wave").objectReferenceValue = wave;
            so.FindProperty("_quota").intValue = quota;
            so.FindProperty("_interval").vector2Value = interval;
            so.FindProperty("_alive").vector2IntValue = alive;
            so.FindProperty("_rampSeconds").floatValue = rampSeconds;
            so.FindProperty("_area").rectValue = area;
            so.FindProperty("_avoidBossRoom").boolValue = avoidBossRoom;
            so.FindProperty("_groundMask").intValue = LayerMask.GetMask("Ground", "OneWayPlatform");
        });
        return spawner;
    }

    /// <summary>
    /// 歩く雑魚を作る。size・hp・frames を渡すと、大きさ・HP・絵を変えられる（2026-11-19：Photoshop のボスのインクの生命体。渡さなければ今までどおり）
    /// </summary>
    internal static void CreateWalker(string name, Vector2 feet, int direction, Transform parent, Sprite square, bool debugLabel = false,
        Vector2? size = null, int hp = 1, string frames = "Generated/Enemies/Zako_Walker.png")
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        Vector2 bodySize = size ?? new Vector2(0.8f, 0.8f);
        var go = CreateZakoBase(name, feet, bodySize, WalkerColor, parent, square,
            ArtAssets.LoadFrames(frames), debugLabel, out _, out _, hp);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = bodySize;
        box.offset = new Vector2(0f, bodySize.y * 0.5f);
        var body = go.AddComponent<KinematicBody2D>();
        PlayerTestSceneBuilder.SetProps(body, so =>
        {
            so.FindProperty("_solidMask").intValue = 1 << ground;
            so.FindProperty("_oneWayMask").intValue = 1 << oneWay;
        });
        var walker = go.AddComponent<ZakoWalker>();
        PlayerTestSceneBuilder.SetProps(walker, so =>
        {
            so.FindProperty("_startDirection").intValue = direction;
            so.FindProperty("_groundMask").intValue = (1 << ground) | (1 << oneWay);
            so.FindProperty("_visualRoot").objectReferenceValue = go.transform.Find("Visual");
        });
    }

    internal static void CreateShooter(string name, Vector2 feet, AttackData shot, Transform player, Transform parent, Sprite square, bool debugLabel = false)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        var size = new Vector2(0.8f, 1.0f);
        var go = CreateZakoBase(name, feet, size, ShooterColor, parent, square,
            ArtAssets.LoadFrames("Generated/Enemies/Zako_Shooter.png"), debugLabel, out Transform bodyTransform, out SpriteRenderer body);

        var runner = BossTestSceneBuilder.AddAttackRunner(go, Team.Enemy, 1 << playerLayer, player);
        // 弾は体の真ん中から出す（足元から出すと床に当たって消える）
        PlayerTestSceneBuilder.SetProps(runner, so => so.FindProperty("_origin").objectReferenceValue = bodyTransform);
        // 【仮】ため始めてから撃つまで頭の上に出す「!」。HP の仮表示より上に置く
        var mark = new GameObject("WindupMark").transform;
        mark.SetParent(go.transform, false);
        mark.localPosition = new Vector3(0f, size.y + 0.75f, 0f);
        ArtLib.AddArt(mark, "Alert", ArtAssets.LoadSprite("Generated/Attacks/Fx_Alert.png"), new Vector3(0f, 0.2f, 0f), 30, go.layer);
        mark.gameObject.SetActive(false);

        var shooter = go.AddComponent<ZakoShooter>();
        PlayerTestSceneBuilder.SetProps(shooter, so =>
        {
            so.FindProperty("_attack").objectReferenceValue = shot;
            so.FindProperty("_windupIndicator").objectReferenceValue = mark.gameObject;
            so.FindProperty("_visualRoot").objectReferenceValue = go.transform.Find("Visual");
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = body;
        });
    }
}
