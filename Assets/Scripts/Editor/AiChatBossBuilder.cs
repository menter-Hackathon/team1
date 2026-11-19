using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// AI チャットのボス（2026-11-19。数値・配置はすべて【仮】）を用意するエディタ拡張。
/// メニュー「Tools/Team1/AIチャットのボスを用意」か、「ランのシーンを作成」から呼ばれる。作るもの（すでにある物は作り直さない。プレハブとステージは毎回作り直す）：
/// ・技（Assets/ScriptableObjects/Attacks/AIChat）：レーザー（1本・2連・扇3本・3連）、ミニロボット（2体・3体）、大技（文字列の巨大レーザー）
/// ・ボスの設定 Boss_AIChat：倒すと「AIアシスタント」。第2形態の最初に大技を1回。ダメージで 20% の確率でエラー（1.2秒止まる）
/// ・プレハブ Assets/Prefabs/Bosses/Boss_AIChat（非表示で保存）。どのシーンにも置ける（狙う相手が空なら、シーンのプレイヤーを狙う）
/// ・ステージ Assets/Scenes/Run/Stage_AIChat（すり抜け床の多い1画面。雑魚を倒すとボス）。Build Settings に足す
/// ・対応アプリ表の「AIチャット」の行（カテゴリ AI の代用ボスのひな形。ChatGPT・Claude などは、このボスとステージになる）
/// ほかのシーンに置くときは、CreateBoss（シーンの作成ツールから）か、プレハブをそのまま置く。
/// </summary>
public static class AiChatBossBuilder
{
    public const string StageScene = "Stage_AIChat";
    const string ProfilePath = "Assets/ScriptableObjects/Bosses/Boss_AIChat.asset";
    const string PrefabPath = "Assets/Prefabs/Bosses/Boss_AIChat.prefab";
    const string AppCatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string ThemePath = "Assets/ScriptableObjects/Themes/Theme_AI.asset";
    const string DisplayName = "AIチャット";

    /// <summary>部屋（横20×高さ11.5。床の上面が y=0）</summary>
    public static readonly Rect ArenaBounds = new Rect(-10f, 0f, 20f, 11.5f);
    /// <summary>ボスが浮かぶ位置（部屋の床の真ん中から）</summary>
    public static readonly Vector2 SpawnPosition = new Vector2(0f, 8.6f);
    /// <summary>体の大きさ（被弾範囲と体当たり）</summary>
    static readonly Vector2 BodySize = new Vector2(2.3f, 2.3f);

    [MenuItem("Tools/Team1/AIチャットのボスを用意")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildForRun();
        Debug.Log($"AIチャットのボスを用意しました（{ProfilePath}・{PrefabPath}・{StageScene}・対応アプリ表の「{DisplayName}」）");
    }

    /// <summary>ランで遊べるようにする：技・設定・プレハブ・ステージ・対応アプリ表の行（-executeMethod AiChatBossBuilder.BuildForRun）</summary>
    public static void BuildForRun()
    {
        BossProfile profile = GetOrCreateProfile();
        ArtAssets.Apply(); // 絵の取り込み設定と、技への絵の割り当て
        profile = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath);
        BossController prefab = BuildPrefab(profile);
        BuildStage();
        EnsureCatalogEntry(AssetDatabase.LoadAssetAtPath<BossController>(PrefabPath), AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath));
        AssetDatabase.SaveAssets();
    }

    /// <summary>ステージ Stage_AIChat を作り直して、Build Settings に足す</summary>
    public static string BuildStage()
    {
        string stagePath = RunScenesBuilder.BuildOneScreenStage(StageScene, BuildPlatforms, new Vector2(6.5f, 2.8f), new Vector2(-6.5f, 2.8f));
        RunScenesBuilder.AddBuildScene(stagePath);
        return stagePath;
    }

    /// <summary>ステージ Stage_AIChat が無ければ作る（チャットのボスも、このステージを使う。2026-11-19）</summary>
    public static void EnsureStage()
    {
        string path = $"{RunScenesBuilder.RunSceneFolder}/{StageScene}.unity";
        if (System.IO.File.Exists(path)) RunScenesBuilder.AddBuildScene(path);
        else BuildStage();
    }

    // ───────── ステージの形 ─────────

    /// <summary>
    /// すり抜け床（フォルダ）の多い部屋：上下に逃げやすい（大技のレーザーは部屋の高さの半分くらいなので、1〜2段登るか降りれば避けられる）。
    /// 段の高さは 2.8 ずつ（ジャンプは 3.2）。1段目 2.8（3枚）、2段目 5.6（4枚）、3段目 8.4（2枚）
    /// </summary>
    public static void BuildPlatforms(Transform terrain)
    {
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        void Folder(float x, float top, float width) =>
            PlayerTestSceneBuilder.CreateBlock($"Folder_{x:0.#}_{top:0.#}", new Vector2(x, top - 0.15f), new Vector2(width, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        Folder(-6.5f, 2.8f, 3f);
        Folder(0f, 2.8f, 3f);
        Folder(6.5f, 2.8f, 3f);
        Folder(-9f, 5.6f, 1.6f);
        Folder(-3.3f, 5.6f, 3f);
        Folder(3.3f, 5.6f, 3f);
        Folder(9f, 5.6f, 1.6f);
        Folder(-6f, 8.4f, 3f);
        Folder(6f, 8.4f, 3f);
    }

    // ───────── ボス ─────────

    /// <summary>
    /// ボスを作る（原点＝体の中心）。シーンの作成ツールから、ほかのボスと同じように置ける（BossTestSceneBuilder.CreateBoss と同じ作り）。
    /// player が空なら、出てきたときにシーンのプレイヤーを狙う
    /// </summary>
    public static BossController CreateBoss(Vector2 position, Transform player, BossProfile profile = null)
    {
        if (profile == null) profile = GetOrCreateProfile();
        return CreateIconBoss("Boss_AIChat", position, player, profile, null);
    }

    /// <summary>
    /// アプリのアイコンのボスを作る（AI チャットのボス・チャットのボスで共通。2026-11-19）。
    /// 体の判定・体当たり・ゲージ・攻撃・追い出されるウイルス・見た目（AiChatBossVisual）。visualSetup で見た目の設定を足せる（試すアプリ・技の絵の差し替えなど）
    /// </summary>
    internal static BossController CreateIconBoss(string objectName, Vector2 position, Transform player, BossProfile profile, Action<SerializedObject> visualSetup)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var boss = new GameObject(objectName);
        boss.layer = enemyLayer;
        boss.transform.position = position;

        // 判定の物（体の大きさの四角）。絵は外して、判定だけにする
        var body = PlayerTestSceneBuilder.CreateSprite("Body", boss.transform, Vector2.zero, BodySize, Color.white, square, 0);
        body.gameObject.layer = enemyLayer;
        GameObject bodyGo = body.gameObject;
        ArtLib.RemoveRenderer(bodyGo.transform);
        var hurtCol = PlayerTestSceneBuilder.AddHurtbox(bodyGo);
        var hurtbox = bodyGo.GetComponent<Hurtbox>();
        var contact = PlayerTestSceneBuilder.AddHitbox(bodyGo, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: hurtCol);

        var gauge = boss.AddComponent<VirusGauge>();
        PlayerTestSceneBuilder.SetProps(gauge, so => so.FindProperty("_max").intValue = profile.GaugeMax);
        BossTestSceneBuilder.AddAttackRunner(boss, Team.Enemy, 1 << playerLayer, player);
        var controller = boss.AddComponent<BossController>();
        AiChatBossVisual visual = Decorate(controller, visualSetup);
        Health ejected = BossTestSceneBuilder.CreateEjectedVirus(boss.transform, square);
        PlayerTestSceneBuilder.SetProps(controller, so =>
        {
            so.FindProperty("_profile").objectReferenceValue = profile;
            so.FindProperty("_contactHitbox").objectReferenceValue = contact;
            so.FindProperty("_hurtbox").objectReferenceValue = hurtbox;
            so.FindProperty("_externalVisuals").boolValue = true; // 色は AiChatBossVisual が受け持つ
            so.FindProperty("_renderers").arraySize = 0;
            so.FindProperty("_normalizedColor").colorValue = Color.white;
            so.FindProperty("_ejectedVirus").objectReferenceValue = ejected;
        });
        return controller;
    }

    /// <summary>見た目：後ろに煙3つ（ゆっくり漂う）、体（アプリのアイコン）、頭の上にエラーのウィンドウ</summary>
    static AiChatBossVisual Decorate(BossController boss, Action<SerializedObject> visualSetup)
    {
        int layer = boss.gameObject.layer;
        var art = new GameObject("Art");
        art.layer = layer;
        art.transform.SetParent(boss.transform, false);

        Sprite[] smokeFrames = ArtAssets.LoadFrames("Provided/Fx_VirusSmoke.png");
        var smoke = new SpriteRenderer[3];
        Vector3[] offsets = { new Vector3(-0.8f, -0.25f, 0f), new Vector3(0.85f, 0.05f, 0f), new Vector3(0f, 0.55f, 0f) };
        for (int i = 0; i < smoke.Length; i++)
        {
            smoke[i] = ArtLib.AddArt(art.transform, $"Smoke_{i}", smokeFrames.Length > 0 ? smokeFrames[0] : null, offsets[i], 8, layer);
            smoke[i].transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        }
        Sprite defaultIcon = ArtAssets.LoadSprite("Generated/Bosses/Boss_AiDefaultIcon.png");
        var icon = ArtLib.AddArt(art.transform, "Icon", defaultIcon, Vector3.zero, 11, layer);
        var error = ArtLib.AddArt(art.transform, "Error", ArtAssets.LoadSprite("Generated/Attacks/Fx_AiError.png"), new Vector3(0f, 2.0f, 0f), 30, layer);
        error.enabled = false;

        var visual = art.AddComponent<AiChatBossVisual>();
        PlayerTestSceneBuilder.SetProps(visual, so =>
        {
            so.FindProperty("_boss").objectReferenceValue = boss;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_error").objectReferenceValue = error;
            so.FindProperty("_defaultIcon").objectReferenceValue = defaultIcon;
            so.FindProperty("_defaultTheme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>(ThemePath);
            Set(so.FindProperty("_smoke"), smoke);
            Set(so.FindProperty("_smokeFrames"), smokeFrames);
            visualSetup?.Invoke(so);
        });
        return visual;
    }

    internal static void Set(SerializedProperty arr, UnityEngine.Object[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static BossController BuildPrefab(BossProfile profile)
    {
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Bosses");
        // 狙う相手（プレイヤー）は、ステージで作ったときに StageDirector が入れる（空のままでも、シーンのプレイヤーを狙う）
        BossController boss = CreateBoss(SpawnPosition, null, profile);
        boss.gameObject.SetActive(false); // 雑魚を倒すまで出さないので、非表示で保存する
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(boss.gameObject);
        return AssetDatabase.LoadAssetAtPath<BossController>(PrefabPath);
    }

    // ───────── 技・設定 ─────────

    /// <summary>技とボスの設定を用意する（すでにあれば、それを使う。数値は上書きしない）</summary>
    public static BossProfile GetOrCreateProfile()
    {
        Color white = Color.white; // 絵はゲームの中でアプリの色に塗り替えるので、掛ける色は白
        // ふつうのレーザー：ボスからプレイヤーの位置へ。予告（電流）1.0秒 → 太くなって細くなる 0.8秒。太さ 0.9
        AttackData laser = Attack("Attack_AIChat_Laser_P1", so => Laser(so, white, 1, 0f, 1, 0f, 1.0f));
        // 2連：2本目は、そのときのプレイヤーの位置へ撃ち直す
        AttackData laserDouble = Attack("Attack_AIChat_LaserDouble_P1", so => Laser(so, white, 1, 0f, 2, 0.9f, 1.0f));
        // 第2形態：扇に3本（50°）／3連
        AttackData laserFan = Attack("Attack_AIChat_LaserFan_P2", so => Laser(so, white, 3, 50f, 1, 0f, 1.0f));
        AttackData laserBurst = Attack("Attack_AIChat_LaserBurst_P2", so => Laser(so, white, 1, 0f, 3, 0.7f, 0.9f));
        // ミニロボット：たくさん出て、プレイヤーへ速く飛んでくる。追いかけ方はかなりゆるい（触れるとダメージ。1回当てると壊れる）
        AttackData mini1 = Attack("Attack_AIChat_MiniBots_P1", so => MiniBots(so, white, MiniCountP1, MiniSpeedP1));
        AttackData mini2 = Attack("Attack_AIChat_MiniBots_P2", so => MiniBots(so, white, MiniCountP2, MiniSpeedP2));
        // 大技：LOADING と考え中（1.8秒）→ 部屋の左端へ（0.6秒）→ プレイヤーの高さに赤い危険エリア（1.6秒）→ 文字列の巨大レーザー（2.8秒）。
        // 太さは部屋の高さの半分くらい（5.2）。上下に1〜2段動けば避けられる
        AttackData codeStream = Attack("Attack_AIChat_CodeStream", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Beam, new Color(1f, 1f, 1f, 0.85f));
            F(so, "_windup", 1.8f);
            E(so, "_beamStyle", (int)BeamStyle.CodeStream);
            E(so, "_beamOrigin", (int)BeamOrigin.ArenaLeft);
            F(so, "_beamMoveTime", 0.6f);
            F(so, "_telegraphTime", 1.6f);
            F(so, "_activeTime", 2.8f);
            F(so, "_beamWidth", 5.2f);
            F(so, "_beamHitThreshold", 0.5f);
            so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Fx_AiLaser.png");
        });

        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath);
        if (profile != null) return profile;
        PlayerTestSceneBuilder.EnsureFolder("Assets/ScriptableObjects/Bosses");
        profile = ScriptableObject.CreateInstance<BossProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        SkillDefinition reward = AbilitiesBuilder.GetOrCreateAiAssistant();
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            so.FindProperty("_displayName").stringValue = DisplayName;
            so.FindProperty("_gaugeMax").intValue = 100;
            so.FindProperty("_startDelay").floatValue = 1.2f;
            // レーザーが中心。ときどきミニロボット（同じ技は2回続けない）
            Moves(so, "_phase1", 1.3f, 2, laser, laserDouble, mini1);
            Moves(so, "_phase2", 0.9f, 2, laserFan, laserBurst, mini2);
            so.FindProperty("_phase2OpeningMove").objectReferenceValue = codeStream;
            so.FindProperty("_errorChance").floatValue = 0.2f;
            so.FindProperty("_errorStunTime").floatValue = 1.2f;
            so.FindProperty("_errorCooldown").floatValue = 4f;
            E(so, "_movement", (int)BossMovement.Hover);
            HoverPoints(so);
            so.FindProperty("_reward").objectReferenceValue = reward;
        });
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ミニロボットの数・速さ・曲がりやすさ・いられる秒数（2026-11-19：もっとたくさん、速く、追いかけ方はかなりゆるく）
    const int MiniCountP1 = 5, MiniCountP2 = 8;
    const float MiniSpeedP1 = 5f, MiniSpeedP2 = 5.5f;
    const float MiniTurnRate = 25f, MiniLifetime = 5f;

    /// <summary>
    /// 飛んで移動する先（2026-11-19：上だけでなく下にも動く）。上の段 3か所（高さ 8.6）、中段 2か所（4.2。1段目と2段目の足場の間）、床の近く1か所（1.3）。
    /// 今いる場所以外から、ランダムに選ぶ
    /// </summary>
    static readonly (float x, float y)[] HoverPointsXY = { (-3f, 8.6f), (0f, 8.6f), (3f, 8.6f), (-3.3f, 4.2f), (3.3f, 4.2f), (0f, 1.3f) };

    static void HoverPoints(SerializedObject so)
    {
        F(so, "_hoverSpeed", 4.5f);
        var xs = so.FindProperty("_hoverPointsX");
        var ys = so.FindProperty("_hoverPointsY");
        xs.arraySize = ys.arraySize = HoverPointsXY.Length;
        for (int i = 0; i < HoverPointsXY.Length; i++)
        {
            xs.GetArrayElementAtIndex(i).floatValue = HoverPointsXY[i].x;
            ys.GetArrayElementAtIndex(i).floatValue = HoverPointsXY[i].y;
        }
    }

    static AttackData Attack(string file, Action<SerializedObject> init) => BossTestSceneBuilder.GetOrCreateAttack($"AIChat/{file}", init);

    static void Laser(SerializedObject so, Color color, int count, float spread, int repeat, float interval, float telegraph)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Beam, color);
        F(so, "_windup", 0.35f);
        E(so, "_beamStyle", (int)BeamStyle.Laser);
        E(so, "_beamOrigin", (int)BeamOrigin.Owner);
        E(so, "_aim", (int)AimMode.Target);
        I(so, "_projectileCount", count);
        F(so, "_spreadAngle", spread);
        I(so, "_repeatCount", repeat);
        F(so, "_repeatInterval", interval);
        F(so, "_telegraphTime", telegraph);
        F(so, "_activeTime", 0.8f);
        F(so, "_beamWidth", 0.9f);
        F(so, "_beamHitThreshold", 0.45f);
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Fx_AiLaser.png");
        Frames(so, "_telegraphFrames", ArtAssets.LoadFrames("Generated/Attacks/Fx_AiSpark.png"));
    }

    static void MiniBots(SerializedObject so, Color color, int count, float speed)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Summon, color);
        F(so, "_windup", 0.5f);
        E(so, "_summonType", (int)SummonType.Chaser);
        I(so, "_summonCount", count);
        F(so, "_summonSpeed", speed);
        F(so, "_summonTurnRate", MiniTurnRate);
        F(so, "_summonLifetime", MiniLifetime);
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Summon_AiMini.png");
        Frames(so, "_summonFrames", ArtAssets.LoadFrames("Generated/Attacks/Summon_AiMini.png"));
    }

    static void Moves(SerializedObject so, string phase, float wait, int attacksPerMove, params AttackData[] moves)
    {
        var arr = so.FindProperty($"{phase}.Moves");
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
        so.FindProperty($"{phase}.WaitAfterAttack").floatValue = wait;
        so.FindProperty($"{phase}.AttacksPerMove").intValue = attacksPerMove;
    }

    static void Frames(SerializedObject so, string prop, Sprite[] frames)
    {
        var arr = so.FindProperty(prop);
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    static void E(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;
    static void I(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;

    // ───────── 対応アプリ表 ─────────

    /// <summary>
    /// 対応アプリ表に「AIチャット」の行を足す（無いときだけ。あれば、ボスのプレハブ・設定が空の欄だけ入れる）。
    /// バンドルIDは入れない（この行そのものには当たらない）。カテゴリ AI のアプリ（ChatGPT・Claude など）が代用ボスになるときのひな形で、
    /// 見た目（アイコン・色）はそのアプリのもの、ステージは Stage_AIChat、倒すと「AIアシスタント」
    /// </summary>
    static void EnsureCatalogEntry(BossController prefab, BossProfile profile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(AppCatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("対応アプリ表がまだ無いので、AIチャットの行は足していません（先に「Tools/Team1/ランのシーンを作成」を実行してください）");
            return;
        }
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var apps = so.FindProperty("_apps");
            for (int i = 0; i < apps.arraySize; i++)
            {
                var e = apps.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("DisplayName").stringValue != DisplayName) continue;
                if (e.FindPropertyRelative("BossPrefab").objectReferenceValue == null) e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
                if (e.FindPropertyRelative("Profile").objectReferenceValue == null) e.FindPropertyRelative("Profile").objectReferenceValue = profile;
                return;
            }
            apps.arraySize++;
            var n = apps.GetArrayElementAtIndex(apps.arraySize - 1);
            n.FindPropertyRelative("DisplayName").stringValue = DisplayName;
            n.FindPropertyRelative("BundleIdPrefixes").arraySize = 0;
            n.FindPropertyRelative("ScreenTimeNames").arraySize = 0;
            n.FindPropertyRelative("Category").intValue = (int)SkillCategory.AI;
            n.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
            n.FindPropertyRelative("Profile").objectReferenceValue = profile;
            n.FindPropertyRelative("SpawnPosition").vector2Value = SpawnPosition;
            n.FindPropertyRelative("StageScene").stringValue = StageScene;
            n.FindPropertyRelative("Theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>(ThemePath);
            n.FindPropertyRelative("StandInSameCategoryOnly").boolValue = true;
            n.FindPropertyRelative("StandInStageScene").stringValue = StageScene;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Min").intValue = 80;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Max").intValue = 130;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Min").floatValue = 0.9f;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Max").floatValue = 0.7f;
            n.FindPropertyRelative("AbilityStat").intValue = (int)AttackStat.None;
        });
        EditorUtility.SetDirty(catalog);
    }
}
