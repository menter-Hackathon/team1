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

    static readonly Color GroundColor = new Color(0.36f, 0.30f, 0.62f);
    static readonly Color OneWayColor = new Color(0.30f, 0.80f, 0.90f);
    static readonly Color BackgroundColor = new Color(0.10f, 0.08f, 0.20f);
    static readonly Color WalkerColor = new Color(0.45f, 0.85f, 0.35f);
    static readonly Color ShooterColor = new Color(0.75f, 0.40f, 0.95f);
    static readonly Color ZakoShotColor = new Color(0.85f, 0.55f, 1.00f);
    static readonly Color WindupMarkColor = new Color(1.00f, 0.85f, 0.20f);
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

        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        AttackData zakoShot = GetOrCreateZakoShot();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 部屋（ボス戦テストシーンと同じ。床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);

        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // 【仮】盾ウィンドウ（マウスでドラッグ。敵の弾だけを止める）
        ShieldWindowSceneBuilder.Create(square, ShieldWindowSceneBuilder.DefaultPosition);

        // --- プレイヤー（左端から始める） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-8.5f, 0.05f, 0f), attacks.PlayerMelee,
            new[] { attacks.SkillNewTab, attacks.SkillBreakpoint });

        // --- 雑魚ウイルス ---
        var wave = new GameObject("EnemyWave").AddComponent<EnemyWave>();
        CreateWalker("Zako_Walker_Floor", new Vector2(2f, 0f), -1, wave.transform, square);
        CreateWalker("Zako_Walker_Folder", new Vector2(3.5f, 3f), 1, wave.transform, square);
        CreateShooter("Zako_Shooter_Folder", new Vector2(-3.5f, 3f), zakoShot, player.transform, wave.transform, square);
        CreateShooter("Zako_Shooter_Floor", new Vector2(8.5f, 0f), zakoShot, player.transform, wave.transform, square);

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
        cam.backgroundColor = BackgroundColor;
        camGo.AddComponent<AudioListener>();

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
    /// 雑魚の共通部分：HP5、体の被弾範囲と同じ形の体当たり、点滅、倒れたら消える、HP の仮表示。原点＝足元。
    /// HP5 は 2026-11-16 まさぴ〜決定（近接なら5回、ブレークポイントなら2回で倒れる）
    /// </summary>
    static GameObject CreateZakoBase(string name, Vector2 feet, Vector2 size, Color color, Transform parent, Sprite square, out SpriteRenderer body)
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
            so.FindProperty("_max").intValue = 5;
            so.FindProperty("_oneDamagePerHit").boolValue = false;
            so.FindProperty("_invincibleTimeAfterHit").floatValue = 0f;
        });
        go.AddComponent<DamageBlink>();
        go.AddComponent<DisableOnDeath>();
        var label = go.AddComponent<HealthDebugLabel>();
        PlayerTestSceneBuilder.SetProps(label, so => so.FindProperty("_offset").vector3Value = new Vector3(0f, size.y + 0.4f, 0f));

        var visual = new GameObject("Visual").transform;
        visual.SetParent(go.transform, false);
        body = PlayerTestSceneBuilder.CreateSprite("Body", visual, new Vector2(0f, size.y * 0.5f), size, color, square, 0);
        body.gameObject.layer = enemyLayer;
        var eye = PlayerTestSceneBuilder.CreateSprite("Eye", visual, new Vector2(size.x * 0.22f, size.y * 0.7f), new Vector2(0.14f, 0.18f), Color.white, square, 1);
        eye.gameObject.layer = enemyLayer;

        var hurtCol = PlayerTestSceneBuilder.AddHurtbox(body.gameObject);
        PlayerTestSceneBuilder.AddHitbox(body.gameObject, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: hurtCol);
        return go;
    }

    internal static void CreateWalker(string name, Vector2 feet, int direction, Transform parent, Sprite square)
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        var size = new Vector2(0.8f, 0.8f);
        var go = CreateZakoBase(name, feet, size, WalkerColor, parent, square, out _);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f);
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

    internal static void CreateShooter(string name, Vector2 feet, AttackData shot, Transform player, Transform parent, Sprite square)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        var size = new Vector2(0.8f, 1.0f);
        var go = CreateZakoBase(name, feet, size, ShooterColor, parent, square, out SpriteRenderer body);

        var runner = BossTestSceneBuilder.AddAttackRunner(go, Team.Enemy, 1 << playerLayer, player);
        // 弾は体の真ん中から出す（足元から出すと床に当たって消える）
        PlayerTestSceneBuilder.SetProps(runner, so => so.FindProperty("_origin").objectReferenceValue = body.transform);
        // 【仮】ため始めてから撃つまで頭の上に出す「!」（黄色い縦棒と点）。HP の仮表示より上に置く
        var mark = new GameObject("WindupMark").transform;
        mark.SetParent(go.transform, false);
        mark.localPosition = new Vector3(0f, size.y + 0.75f, 0f);
        PlayerTestSceneBuilder.CreateSprite("Bar", mark, new Vector2(0f, 0.4f), new Vector2(0.16f, 0.45f), WindupMarkColor, square, 20);
        PlayerTestSceneBuilder.CreateSprite("Dot", mark, Vector2.zero, new Vector2(0.16f, 0.16f), WindupMarkColor, square, 20);
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
