using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ボス戦を試すための仮の部屋（1画面）を自動で作るエディタ拡張。
/// メニュー「Tools/Team1/ボス戦テストシーンを作成」から実行する。
/// 部屋の形は docs/app-boss-and-ability-design.md の2章（【要確認】ステージ担当と相談）：
///   1画面（約21×12マス）、床あり、高さ3マスにすり抜け床（フォルダ）2枚。カメラは固定。
/// 攻撃データのアセット（Assets/ScriptableObjects/Attacks）は、無ければ docs の数値で作る。あれば作り直さない。
/// 【仮】マウスで動かせる盾ウィンドウを1枚置く（ShieldWindowSceneBuilder。レイヤー BulletBlocker が必要）。
/// </summary>
public static class BossTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/BossTest.unity";
    const string AttackFolder = "Assets/ScriptableObjects/Attacks";

    static readonly Color GroundColor = new Color(0.36f, 0.30f, 0.62f);
    static readonly Color OneWayColor = new Color(0.30f, 0.80f, 0.90f);
    static readonly Color BackgroundColor = new Color(0.10f, 0.08f, 0.20f);

    // 部屋の内側：x は -10〜10、y は床の上面 0 から画面の上端 11.5 まで
    static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);

    [MenuItem("Tools/Team1/ボス戦テストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod BossTestSceneBuilder.Build）</summary>
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
        Attacks attacks = GetOrCreateAttacks();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 部屋（床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, GroundColor, square, terrain);
        // 【仮】フォルダの足場（上面が高さ3）。Chrome の3か所（x=-7・0・7、幅4）の間に、0.3マスずつ離して置く。
        // 足場の端に立っても Chrome の体には触れず、近接は届く
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, OneWayColor, square, terrain);

        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = ArenaBounds);

        // 【仮】盾ウィンドウ（マウスでドラッグ。敵の弾だけを止める）
        ShieldWindowSceneBuilder.Create(square, ShieldWindowSceneBuilder.DefaultPosition);

        // --- プレイヤー ---
        // 【仮】G キーで本物の能力2つを順番にもらえる（ボスを倒さずに能力を試せる）
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-6f, 0.05f, 0f), attacks.PlayerMelee,
            new[] { attacks.SkillNewTab, attacks.SkillBreakpoint });

        // --- ボス（B キーで切り替え。最初は Chrome） ---
        // Chrome：空中に浮く（下の端が床から2.5マス）。VS Code：床に立つ
        var chrome = CreateBoss("Chrome", attacks.ChromeProfile, new Vector2(0f, 2.5f + 1.5f), new Vector2(4f, 3f), ChromeBodyColor, square, player.transform);
        var vscode = CreateBoss("VSCode", attacks.VSCodeProfile, new Vector2(7f, 1.5f), new Vector2(4f, 3f), VSCodeBodyColor, square, player.transform);

        var director = new GameObject("BossTestDirector").AddComponent<BossTestDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            var arr = so.FindProperty("_bosses");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = chrome;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = vscode;
            so.FindProperty("_player").objectReferenceValue = player.transform;
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
        Debug.Log($"ボス戦テストシーンを作成しました: {ScenePath}");
    }

    static readonly Color VSCodeBodyColor = new Color(0.16f, 0.42f, 0.78f);
    static readonly Color ChromeBodyColor = new Color(0.92f, 0.92f, 0.95f);
    static readonly Color TitleBarColor = new Color(0.12f, 0.12f, 0.16f);

    /// <summary>
    /// ボスを作る（原点＝体の中心）。体の大きさの被弾範囲と、同じ形の体当たり判定を付ける。
    /// 見た目は仮：体の四角と、上の端のタイトルバー
    /// </summary>
    internal static BossController CreateBoss(string bossName, BossProfile profile, Vector2 position, Vector2 bodySize, Color bodyColor, Sprite square, Transform player)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        var boss = new GameObject($"Boss_{bossName}");
        boss.layer = enemyLayer;
        boss.transform.position = position;

        var body = PlayerTestSceneBuilder.CreateSprite("Body", boss.transform, Vector2.zero, bodySize, bodyColor, square, 0);
        body.gameObject.layer = enemyLayer;
        var titleBar = PlayerTestSceneBuilder.CreateSprite("TitleBar", boss.transform, new Vector2(0f, bodySize.y * 0.5f - 0.2f), new Vector2(bodySize.x, 0.4f), TitleBarColor, square, 1);
        titleBar.gameObject.layer = enemyLayer;

        var hurtCol = PlayerTestSceneBuilder.AddHurtbox(body.gameObject);
        var hurtbox = body.GetComponent<Hurtbox>();
        // 体当たり：被弾範囲と同じ形で、プレイヤーに当たり続ける（連続ヒットはプレイヤーの無敵時間で防ぐ）
        var contact = PlayerTestSceneBuilder.AddHitbox(body.gameObject, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: hurtCol);

        var gauge = boss.AddComponent<VirusGauge>();
        PlayerTestSceneBuilder.SetProps(gauge, so => so.FindProperty("_max").intValue = profile.GaugeMax);
        AddAttackRunner(boss, Team.Enemy, 1 << playerLayer, player);

        var controller = boss.AddComponent<BossController>();
        PlayerTestSceneBuilder.SetProps(controller, so =>
        {
            so.FindProperty("_profile").objectReferenceValue = profile;
            so.FindProperty("_contactHitbox").objectReferenceValue = contact;
            so.FindProperty("_hurtbox").objectReferenceValue = hurtbox;
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = body;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = titleBar;
        });
        return controller;
    }

    /// <summary>攻撃する人に AttackRunner を付ける。【仮】敵の弾は盾ウィンドウ（BulletBlocker）で止まるようにする</summary>
    internal static AttackRunner AddAttackRunner(GameObject go, Team team, int targetMask, Transform target)
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        var runner = go.AddComponent<AttackRunner>();
        PlayerTestSceneBuilder.SetProps(runner, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)team;
            so.FindProperty("_targetLayers").intValue = targetMask;
            so.FindProperty("_solidMask").intValue = 1 << ground;
            so.FindProperty("_groundMask").intValue = (1 << ground) | (1 << oneWay);
            // 弾の判定にだけ使う（_solidMask に入れると、瞬間移動も盾で止まってしまう）
            so.FindProperty("_projectileBlockerMask").intValue = team == Team.Enemy ? ShieldWindowSceneBuilder.BlockerMask : 0;
            so.FindProperty("_target").objectReferenceValue = target;
        });
        return runner;
    }

    // ───────── 攻撃データ（docs の数値） ─────────

    internal class Attacks
    {
        public AttackData PlayerMelee;
        public AttackData ChromeTabShurikenP1;
        public AttackData ChromeTabShurikenP2;

        public AttackData VSCodeErrorBulletP1;
        public AttackData VSCodeErrorBulletP2;
        public AttackData VSCodeRedWaveP1;
        public AttackData VSCodeRedWaveP2;
        public AttackData VSCodeBreakpointP1;
        public AttackData VSCodeBreakpointP2;
        public AttackData VSCodeErrorWindowFall;
        public AttackData VSCodeWarp;
        public BossProfile VSCodeProfile;

        public AttackData ChromePopupFallP1;
        public AttackData ChromePopupFallP2;
        public AttackData ChromeTabMultiply;
        public BossProfile ChromeProfile;

        public AttackData AbilityNewTab;
        public AttackData AbilityBreakpoint;
        public SkillDefinition SkillNewTab;
        public SkillDefinition SkillBreakpoint;
    }

    static readonly Color ChromeColor = new Color(0.35f, 0.65f, 1.00f);
    static readonly Color ErrorColor = new Color(1.00f, 0.30f, 0.30f);
    static readonly Color ErrorWindowColor = new Color(0.55f, 0.20f, 0.22f);
    static readonly Color PopupColor = new Color(0.95f, 0.85f, 0.35f);
    static readonly Color TabColor = new Color(0.55f, 0.75f, 1.00f);
    static readonly Color PlayerAbilityColor = new Color(1.00f, 0.55f, 0.80f);
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string SkillFolder = "Assets/ScriptableObjects/Skills";

    internal static Attacks GetOrCreateAttacks()
    {
        var a = new Attacks();

        // 【仮】近接：今の仮の攻撃の値。ゲージ+4 は docs 3章
        a.PlayerMelee = GetOrCreateAttack("Player/Attack_Player_Melee", so =>
        {
            Kind(so, AttackKind.Melee);
            F(so, "_activeTime", 0.12f);
            F(so, "_cooldown", 0.3f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 4);
            V(so, "_knockback", new Vector2(4f, 3f));
            F(so, "_hitStun", 0.2f);
        });

        // Chrome：タブ手裏剣（docs 4章）
        a.ChromeTabShurikenP1 = GetOrCreateAttack("Chrome/Attack_Chrome_TabShuriken_P1", so =>
        {
            BossCommon(so, AttackKind.Projectile, ChromeColor);
            F(so, "_windup", 0.8f);
            I(so, "_projectileCount", 3);
            F(so, "_spreadAngle", 30f);
            F(so, "_speed", 7f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.6f, 0.4f));
            E(so, "_aim", (int)AimMode.Target);
        });
        a.ChromeTabShurikenP2 = GetOrCreateAttack("Chrome/Attack_Chrome_TabShuriken_P2", so =>
        {
            BossCommon(so, AttackKind.Projectile, ChromeColor);
            F(so, "_windup", 0.8f);
            I(so, "_repeatCount", 2);
            F(so, "_repeatInterval", 0.4f);
            I(so, "_projectileCount", 5);
            F(so, "_spreadAngle", 60f);
            F(so, "_speed", 7f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.6f, 0.4f));
            E(so, "_aim", (int)AimMode.Target);
        });

        // ───── VS Code（docs 5章） ─────
        // 赤い波線：弾が当たった面に沿って、幅2マスの波線が残る。0.5秒ごとに判定。太さ0.4は【仮】
        a.VSCodeRedWaveP1 = GetOrCreateAttack("VSCode/Attack_VSCode_RedWave_P1", so =>
        {
            BossCommon(so, AttackKind.Area, ErrorColor);
            E(so, "_anchor", (int)AreaAnchor.HitPoint);
            B(so, "_snapToGround", false);
            B(so, "_alignToSurface", true);
            V(so, "_areaSize", new Vector2(2f, 0.4f));
            F(so, "_telegraphTime", 0f);
            F(so, "_persistDuration", 2f);
            F(so, "_tickInterval", 0.5f);
        });
        a.VSCodeRedWaveP2 = GetOrCreateAttack("VSCode/Attack_VSCode_RedWave_P2", so =>
        {
            BossCommon(so, AttackKind.Area, ErrorColor);
            E(so, "_anchor", (int)AreaAnchor.HitPoint);
            B(so, "_snapToGround", false);
            B(so, "_alignToSurface", true);
            V(so, "_areaSize", new Vector2(2f, 0.4f));
            F(so, "_telegraphTime", 0f);
            F(so, "_persistDuration", 3f);
            F(so, "_tickInterval", 0.5f);
        });
        // エラー弾：プレイヤーを狙って2連射（間隔0.3秒）、毎秒8マス、判定1.0×0.3。寿命3秒は【仮】
        a.VSCodeErrorBulletP1 = GetOrCreateAttack("VSCode/Attack_VSCode_ErrorBullet_P1", so =>
        {
            BossCommon(so, AttackKind.Projectile, ErrorColor);
            F(so, "_windup", 0.8f);
            I(so, "_repeatCount", 2);
            F(so, "_repeatInterval", 0.3f);
            I(so, "_projectileCount", 1);
            F(so, "_speed", 8f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(1.0f, 0.3f));
            E(so, "_aim", (int)AimMode.Target);
            B(so, "_destroyOnTerrain", true);
            R(so, "_onTerrainHit", a.VSCodeRedWaveP1);
        });
        a.VSCodeErrorBulletP2 = GetOrCreateAttack("VSCode/Attack_VSCode_ErrorBullet_P2", so =>
        {
            BossCommon(so, AttackKind.Projectile, ErrorColor);
            F(so, "_windup", 0.8f);
            I(so, "_repeatCount", 3);
            F(so, "_repeatInterval", 0.3f);
            I(so, "_projectileCount", 1);
            F(so, "_speed", 8f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(1.0f, 0.3f));
            E(so, "_aim", (int)AimMode.Target);
            B(so, "_destroyOnTerrain", true);
            R(so, "_onTerrainHit", a.VSCodeRedWaveP2);
        });
        // ブレークポイント：プレイヤーの位置の床に赤丸1.0秒 → 縦の柱（幅1.5×画面の高さ）を0.3秒。予告が予備動作の代わり
        a.VSCodeBreakpointP1 = GetOrCreateAttack("VSCode/Attack_VSCode_Breakpoint_P1", so =>
        {
            BossCommon(so, AttackKind.Area, ErrorColor);
            F(so, "_windup", 0f);
            E(so, "_anchor", (int)AreaAnchor.TargetPosition);
            B(so, "_snapToGround", true);
            V(so, "_areaSize", new Vector2(1.5f, ArenaBounds.height));
            F(so, "_telegraphTime", 1.0f);
            F(so, "_activeTime", 0.3f);
            I(so, "_areaCount", 1);
        });
        a.VSCodeBreakpointP2 = GetOrCreateAttack("VSCode/Attack_VSCode_Breakpoint_P2", so =>
        {
            BossCommon(so, AttackKind.Area, ErrorColor);
            F(so, "_windup", 0f);
            E(so, "_anchor", (int)AreaAnchor.TargetPosition);
            B(so, "_snapToGround", true);
            V(so, "_areaSize", new Vector2(1.5f, ArenaBounds.height));
            F(so, "_telegraphTime", 1.0f);
            F(so, "_activeTime", 0.3f);
            I(so, "_areaCount", 3);
            F(so, "_areaSpacing", 4f);
            F(so, "_areaStagger", 0.3f);
        });
        // エラーウィンドウ落下（第2形態）：3枚、影0.8秒、3×2、着地したら消える。落下速度はポップアップと同じ毎秒12マス【仮】
        a.VSCodeErrorWindowFall = GetOrCreateAttack("VSCode/Attack_VSCode_ErrorWindowFall", so =>
        {
            BossCommon(so, AttackKind.Spawn, ErrorWindowColor);
            F(so, "_windup", 0f);
            E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            I(so, "_spawnCount", 3);
            F(so, "_minDistanceFromTarget", 3f);
            V(so, "_objectSize", new Vector2(3f, 2f));
            F(so, "_fallSpeed", 12f);
            F(so, "_telegraphTime", 0.8f);
            F(so, "_landRemainTime", 0f);
            B(so, "_becomesPlatform", false);
        });
        // 反対側へ瞬間移動：0.5秒かけて消え、現れる場所に0.5秒前から予告。現れるのにかける0.25秒は【仮】
        a.VSCodeWarp = GetOrCreateAttack("VSCode/Attack_VSCode_Warp", so =>
        {
            Kind(so, AttackKind.Warp);
            F(so, "_windup", 0f);
            E(so, "_warpDestination", (int)WarpDestination.Opposite);
            F(so, "_vanishTime", 0.5f);
            F(so, "_appearDelay", 0f);
            F(so, "_appearTelegraph", 0.5f);
            F(so, "_appearTime", 0.25f);
            B(so, "_disableContactUntilAppeared", true);
        });

        // ───── Chrome（docs 4章） ─────
        // ポップアップ落下：影0.8秒 → 2枚（1枚はプレイヤーの真上、残りは3マス以上離れる）、3×2、毎秒12マス。着地後4秒すり抜け床
        a.ChromePopupFallP1 = GetOrCreateAttack("Chrome/Attack_Chrome_PopupFall_P1", so =>
        {
            BossCommon(so, AttackKind.Spawn, PopupColor);
            F(so, "_windup", 0f);
            E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            I(so, "_spawnCount", 2);
            F(so, "_minDistanceFromTarget", 3f);
            V(so, "_objectSize", new Vector2(3f, 2f));
            F(so, "_fallSpeed", 12f);
            F(so, "_telegraphTime", 0.8f);
            F(so, "_landRemainTime", 4f);
            B(so, "_becomesPlatform", true);
        });
        a.ChromePopupFallP2 = GetOrCreateAttack("Chrome/Attack_Chrome_PopupFall_P2", so =>
        {
            BossCommon(so, AttackKind.Spawn, PopupColor);
            F(so, "_windup", 0f);
            E(so, "_placement", (int)SpawnPlacement.AroundTarget);
            I(so, "_spawnCount", 4);
            F(so, "_minDistanceFromTarget", 3f);
            V(so, "_objectSize", new Vector2(3f, 2f));
            F(so, "_fallSpeed", 12f);
            F(so, "_telegraphTime", 0.8f);
            F(so, "_landRemainTime", 4f);
            B(so, "_becomesPlatform", true);
        });
        // タブ増殖（第2形態）：画面の上端に0.3秒ごとに1枚（最大6枚、3マス間隔）→ 0.5秒後、左から0.25秒おきに落とす（毎秒10マス）。床で消える
        // タブの大きさ 1.5×0.6 は 2026-11-15 にまさぴ〜が選んだ値（docs には未記載）
        a.ChromeTabMultiply = GetOrCreateAttack("Chrome/Attack_Chrome_TabMultiply", so =>
        {
            BossCommon(so, AttackKind.Spawn, TabColor);
            F(so, "_windup", 0f);
            E(so, "_placement", (int)SpawnPlacement.TopRow);
            I(so, "_spawnCount", 6);
            F(so, "_placeInterval", 0.3f);
            F(so, "_placeSpacing", 3f);
            F(so, "_dropHoldTime", 0.5f);
            F(so, "_dropInterval", 0.25f);
            F(so, "_fallSpeed", 10f);
            V(so, "_objectSize", new Vector2(1.5f, 0.6f));
            F(so, "_telegraphTime", 0f);
            F(so, "_landRemainTime", 0f);
            B(so, "_becomesPlatform", false);
        });

        // ───── 能力（docs 4・5章） ─────
        // 新しいタブ：前方へ6マス瞬間移動。壁や床にぶつかるならその手前。空中でも使える。無敵なし。クールタイム3秒
        a.AbilityNewTab = GetOrCreateAttack("Player/Attack_Ability_NewTab", so =>
        {
            Kind(so, AttackKind.Warp);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 3f);
            E(so, "_warpDestination", (int)WarpDestination.Forward);
            F(so, "_warpDistance", 6f);
            F(so, "_vanishTime", 0f);
            F(so, "_appearDelay", 0f);
            F(so, "_appearTelegraph", 0f);
            F(so, "_appearTime", 0f);
            B(so, "_disableContactUntilAppeared", false);
        });
        // ブレークポイント：前方4マス先の床に赤丸 → 0.3秒後に縦の柱（幅1.5×高さ6、判定0.2秒）。ゲージ+12、雑魚へのダメージは近接の3倍。クールタイム3秒
        a.AbilityBreakpoint = GetOrCreateAttack("Player/Attack_Ability_Breakpoint", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 3f);
            I(so, "_damage", 3);
            I(so, "_gaugeGain", 12);
            V(so, "_knockback", new Vector2(4f, 3f));
            F(so, "_hitStun", 0.2f);
            so.FindProperty("_color").colorValue = PlayerAbilityColor;
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 4f);
            B(so, "_snapToGround", true);
            V(so, "_areaSize", new Vector2(1.5f, 6f));
            F(so, "_telegraphTime", 0.3f);
            F(so, "_activeTime", 0.2f);
            I(so, "_areaCount", 1);
        });
        a.SkillNewTab = GetOrCreateSkill("Skill_NewTab", "新しいタブ", SkillCategory.Browser, a.AbilityNewTab);
        a.SkillBreakpoint = GetOrCreateSkill("Skill_Breakpoint", "ブレークポイント", SkillCategory.Dev, a.AbilityBreakpoint);

        a.ChromeProfile = GetOrCreateProfile("Boss_Chrome", so =>
        {
            so.FindProperty("_displayName").stringValue = "Chrome";
            Moves(so, "_phase1", 1.2f, 2, a.ChromeTabShurikenP1, a.ChromePopupFallP1);
            Moves(so, "_phase2", 0.8f, 2, a.ChromeTabShurikenP2, a.ChromePopupFallP2, a.ChromeTabMultiply);
            E(so, "_movement", (int)BossMovement.Hover);
            F(so, "_hoverSpeed", 3f);
            var pts = so.FindProperty("_hoverPointsX");
            pts.arraySize = 3;
            pts.GetArrayElementAtIndex(0).floatValue = -7f;
            pts.GetArrayElementAtIndex(1).floatValue = 0f;
            pts.GetArrayElementAtIndex(2).floatValue = 7f;
            R(so, "_reward", a.SkillNewTab);
        });

        a.VSCodeProfile = GetOrCreateProfile("Boss_VSCode", so =>
        {
            so.FindProperty("_displayName").stringValue = "VS Code";
            Moves(so, "_phase1", 1.2f, 2, a.VSCodeErrorBulletP1, a.VSCodeBreakpointP1);
            Moves(so, "_phase2", 0.8f, 2, a.VSCodeErrorBulletP2, a.VSCodeBreakpointP2, a.VSCodeErrorWindowFall);
            E(so, "_movement", (int)BossMovement.Warp);
            R(so, "_warpAttack", a.VSCodeWarp);
            R(so, "_reward", a.SkillBreakpoint);
        });

        AssetDatabase.SaveAssets();
        return a;
    }

    static void Moves(SerializedObject so, string phase, float wait, int attacksPerMove, params AttackData[] moves)
    {
        var arr = so.FindProperty($"{phase}.Moves");
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
        so.FindProperty($"{phase}.WaitAfterAttack").floatValue = wait;
        so.FindProperty($"{phase}.AttacksPerMove").intValue = attacksPerMove;
    }

    static BossProfile GetOrCreateProfile(string file, Action<SerializedObject> init)
    {
        string path = $"{BossFolder}/{file}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        if (profile != null) return profile;
        PlayerTestSceneBuilder.EnsureFolder(BossFolder);
        profile = ScriptableObject.CreateInstance<BossProfile>();
        AssetDatabase.CreateAsset(profile, path);
        PlayerTestSceneBuilder.SetProps(profile, init);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static SkillDefinition GetOrCreateSkill(string file, string displayName, SkillCategory category, AttackData attack)
    {
        string path = $"{SkillFolder}/{file}.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (skill != null) return skill;
        PlayerTestSceneBuilder.EnsureFolder(SkillFolder);
        skill = ScriptableObject.CreateInstance<SkillDefinition>();
        AssetDatabase.CreateAsset(skill, path);
        PlayerTestSceneBuilder.SetProps(skill, so =>
        {
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_icon").objectReferenceValue = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
            so.FindProperty("_category").intValue = (int)category; // 値を明示した enum なので、値で入れる
            so.FindProperty("_attack").objectReferenceValue = attack;
        });
        EditorUtility.SetDirty(skill);
        return skill;
    }

    /// <summary>ボスの攻撃の共通値（docs 3章：ダメージ1、ノックバック (6,5)、のけぞり0.25秒）</summary>
    internal static void BossCommon(SerializedObject so, AttackKind kind, Color color)
    {
        Kind(so, kind);
        I(so, "_damage", 1);
        I(so, "_gaugeGain", 0);
        V(so, "_knockback", new Vector2(6f, 5f));
        F(so, "_hitStun", 0.25f);
        so.FindProperty("_color").colorValue = color;
    }

    /// <summary>攻撃データを作る（すでにあれば、それを使う。値は上書きしない）</summary>
    internal static AttackData GetOrCreateAttack(string relativePath, Action<SerializedObject> init)
    {
        string path = $"{AttackFolder}/{relativePath}.asset";
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (data != null) return data;

        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        data = ScriptableObject.CreateInstance<AttackData>();
        AssetDatabase.CreateAsset(data, path);
        PlayerTestSceneBuilder.SetProps(data, init);
        EditorUtility.SetDirty(data);
        return data;
    }

    static void Kind(SerializedObject so, AttackKind kind) => so.FindProperty("_kind").intValue = (int)kind; // 値を明示した enum なので値で入れる
    static void E(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;
    static void I(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void V(SerializedObject so, string prop, Vector2 value) => so.FindProperty(prop).vector2Value = value;
    static void B(SerializedObject so, string prop, bool value) => so.FindProperty(prop).boolValue = value;
    static void R(SerializedObject so, string prop, UnityEngine.Object value) => so.FindProperty(prop).objectReferenceValue = value;
}
