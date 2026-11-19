using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ブラウザのボス（2026-11-19。Safari・Arc・Firefox などのブラウザ。数値・配置はすべて【仮】）を用意するエディタ拡張。
/// メニュー「Tools/Team1/ブラウザのボスを用意」か、「ランのシーンを作成」から呼ばれる。作るもの（すでにある物は作り直さない。プレハブは毎回作り直す）：
/// ・技（Assets/ScriptableObjects/Attacks/BrowserBoss）：追尾するパケット（プレイヤーに IP アドレスを出してから。遅い。壊せる）、
///   爆弾のサイト（ウィンドウをいくつも開き、読み込みバーがたまったら爆発）、大技の雑魚を出し続けるウィンドウ（第2形態の最初に1回）
/// ・雑魚のプレハブ Assets/Prefabs/Enemies/Zako_Walker（いつもの歩く雑魚）と、雑魚を出し続けるウィンドウ Spawner_BrowserWindow（HP15）
/// ・ボスの設定 Boss_Browser（第2形態はゲージ65%から＝ボスの体力が少ないとき。倒すと、そのアプリの能力。設定の能力はパケット）
/// ・プレハブ Assets/Prefabs/Bosses/Boss_Browser（非表示で保存）。どのシーンにも置ける
/// ・対応アプリ表の「ブラウザ」の行（カテゴリ ブラウザの代用ボスのひな形。Chrome の行より優先される）
/// ・対応アプリ表の「Chrome」の行も、このボスにする（2026-11-19。ほかのボスと同じ、アプリのアイコン＋テーマ色のモヤの見た目。ステージは1画面）
/// </summary>
public static class BrowserBossBuilder
{
    /// <summary>ボスが最初にいる位置（部屋の床の中央から。2026-11-19：ほかのボスより少し低く、画面の真ん中寄り）【仮】</summary>
    public static readonly Vector2 SpawnPosition = new Vector2(0f, 6.6f);

    const string ProfilePath = "Assets/ScriptableObjects/Bosses/Boss_Browser.asset";
    const string PrefabPath = "Assets/Prefabs/Bosses/Boss_Browser.prefab";
    const string WalkerPath = "Assets/Prefabs/Enemies/Zako_Walker.prefab";
    const string SpawnerPath = "Assets/Prefabs/Enemies/Spawner_BrowserWindow.prefab";
    const string AppCatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string DisplayName = "ブラウザ";
    const string AttackDir = "BrowserBoss";

    [MenuItem("Tools/Team1/ブラウザのボスを用意")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildForRun();
        Debug.Log($"ブラウザのボスを用意しました（{ProfilePath}・{PrefabPath}・{SpawnerPath}・対応アプリ表の「{DisplayName}」）");
    }

    /// <summary>ランで遊べるようにする（-executeMethod BrowserBossBuilder.BuildForRun）</summary>
    public static void BuildForRun()
    {
        AbilitiesBuilder.EnsurePacketSkill();
        ArtAssets.Apply(); // 絵の取り込み設定（先に入れておくと、技に絵が入る）
        GetOrCreateProfile();
        ArtAssets.Apply();
        BuildPrefab(AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath));
        AiChatBossBuilder.EnsureStage();
        EnsureCatalogEntry(AssetDatabase.LoadAssetAtPath<BossController>(PrefabPath), AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath));
        AssetDatabase.SaveAssets();
    }

    // ───────── ボス ─────────

    /// <summary>ボスを作る（原点＝体の中心）。シーンの作成ツールから、ほかのボスと同じように置ける。player が空なら、シーンのプレイヤーを狙う</summary>
    public static BossController CreateBoss(Vector2 position, Transform player, BossProfile profile = null)
    {
        if (profile == null) profile = GetOrCreateProfile();
        return AiChatBossBuilder.CreateIconBoss("Boss_Browser", position, player, profile, so =>
        {
            so.FindProperty("_previewAppName").stringValue = "Safari";
            so.FindProperty("_previewBundleId").stringValue = "com.apple.Safari";
        });
    }

    static void BuildPrefab(BossProfile profile)
    {
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Bosses");
        BossController boss = CreateBoss(SpawnPosition, null, profile);
        boss.gameObject.SetActive(false); // 雑魚を倒すまで出さないので、非表示で保存する
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(boss.gameObject);
    }

    /// <summary>いつもの歩く雑魚のプレハブ（雑魚戦と同じ作り。HP1）</summary>
    static GameObject GetOrCreateWalkerPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WalkerPath);
        if (existing != null) return existing;
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Enemies");
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var holder = new GameObject("MinionHolder");
        StageTestSceneBuilder.CreateWalker("Zako_Walker", Vector2.zero, -1, holder.transform, square);
        GameObject go = holder.transform.Find("Zako_Walker").gameObject;
        go.transform.SetParent(null, true);
        PrefabUtility.SaveAsPrefabAsset(go, WalkerPath);
        UnityEngine.Object.DestroyImmediate(go);
        UnityEngine.Object.DestroyImmediate(holder);
        return AssetDatabase.LoadAssetAtPath<GameObject>(WalkerPath);
    }

    /// <summary>
    /// 雑魚を出し続けるウィンドウのプレハブ：HP15（そこそこ固い）。壊すまで3秒ごとに、いつもの歩く雑魚を出す（同時に4体まで）。
    /// 被弾範囲はウィンドウの大きさ。触れてもダメージはない
    /// </summary>
    /// <summary>ウィンドウの絵の拡大率（2026-11-19：攻撃を当てやすいように大きく。2倍は大きすぎたので1.5倍）【仮】</summary>
    const float SpawnerScale = 1.5f;
    /// <summary>ウィンドウの表示の順番：ボス（モヤ 8・アイコン 11）・雑魚（11）・プレイヤー（15）より後ろ、地形（0）より前</summary>
    const int SpawnerSortingOrder = 4;

    /// <summary>毎回作り直す（同じ場所に上書きするので、技からの参照はそのまま）</summary>
    static GameObject GetOrCreateSpawnerPrefab(GameObject walker)
    {
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Enemies");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var go = new GameObject("Spawner_BrowserWindow");
        try
        {
            BuildSpawner(go, walker, enemyLayer, square);
            PrefabUtility.SaveAsPrefabAsset(go, SpawnerPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go); // 途中で失敗しても、作りかけをシーンに残さない
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPath);
    }

    static void BuildSpawner(GameObject go, GameObject walker, int enemyLayer, Sprite square)
    {
        go.layer = enemyLayer;
        var health = go.AddComponent<Health>();
        PlayerTestSceneBuilder.SetProps(health, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)Team.Enemy;
            so.FindProperty("_max").intValue = 15;
            so.FindProperty("_oneDamagePerHit").boolValue = false;
            so.FindProperty("_invincibleTimeAfterHit").floatValue = 0f;
        });
        Sprite window = ArtAssets.LoadSprite("Generated/Enemies/Window_Spawner.png");
        var art = ArtLib.AddArt(go.transform, "Art", window, Vector3.zero, SpawnerSortingOrder, enemyLayer);
        art.transform.localScale = new Vector3(SpawnerScale, SpawnerScale, 1f);
        Vector2 size = (window != null ? (Vector2)window.bounds.size : new Vector2(2.6f, 1.9f)) * SpawnerScale;
        var blink = go.AddComponent<DamageBlink>();
        PlayerTestSceneBuilder.SetProps(blink, so =>
        {
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = art;
        });
        go.AddComponent<DisableOnDeath>();
        var death = go.AddComponent<DeathVfx>();
        PlayerTestSceneBuilder.SetProps(death, so =>
        {
            so.FindProperty("_big").boolValue = true;
            so.FindProperty("_offset").vector2Value = Vector2.zero;
        });
        var label = go.AddComponent<HealthDebugLabel>(); // 残りの固さが分かるように（仮の表示）
        PlayerTestSceneBuilder.SetProps(label, so => so.FindProperty("_offset").vector3Value = new Vector3(0f, size.y * 0.5f + 0.4f, 0f));
        // 被弾範囲（絵は消す。SpriteRenderer を消したあとに触らないよう、先に GameObject を持っておく）
        GameObject bodyGo = PlayerTestSceneBuilder.CreateSprite("Body", go.transform, Vector2.zero, size, Color.white, square, 0).gameObject;
        bodyGo.layer = enemyLayer;
        ArtLib.RemoveRenderer(bodyGo.transform);
        PlayerTestSceneBuilder.AddHurtbox(bodyGo);
        var spawner = go.AddComponent<SpawnerWindow>();
        PlayerTestSceneBuilder.SetProps(spawner, so =>
        {
            so.FindProperty("_minion").objectReferenceValue = walker;
            so.FindProperty("_interval").floatValue = 3f;
            so.FindProperty("_firstDelay").floatValue = 1.2f;
            so.FindProperty("_maxAlive").intValue = 4;
            so.FindProperty("_art").objectReferenceValue = art;
        });
    }

    // ───────── 技・設定 ─────────

    /// <summary>技とボスの設定を用意する（すでにあれば、それを使う。数値は上書きしない）</summary>
    public static BossProfile GetOrCreateProfile()
    {
        GameObject walker = GetOrCreateWalkerPrefab();
        GameObject spawner = GetOrCreateSpawnerPrefab(walker);
        // 追尾するパケット：プレイヤーの頭の上に IP アドレスを出して1秒 → かなり遅いパケットを撃つ。攻撃で壊せる
        AttackData packets1 = Attack("Attack_BrowserBoss_Packets_P1", so => Packets(so, 3, 3f));
        AttackData packets2 = Attack("Attack_BrowserBoss_Packets_P2", so => Packets(so, 5, 3.5f));
        // 爆弾のサイト：ウィンドウを開き、読み込みバーが2割ほどゆっくり（1.6秒）→ 一気に（0.25秒）たまったら爆発（直径3.4）
        AttackData bombs1 = Attack("Attack_BrowserBoss_BombSites_P1", so => BombSites(so, 3));
        AttackData bombs2 = Attack("Attack_BrowserBoss_BombSites_P2", so => BombSites(so, 5));
        // 大技：雑魚を出し続けるウィンドウを置く（壊すまで出し続ける）
        AttackData spawn = Attack("Attack_BrowserBoss_Spawner", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Summon, Color.white);
            so.FindProperty("_fixedColors").boolValue = true;
            F(so, "_windup", 0.6f);
            so.FindProperty("_summonType").intValue = (int)SummonType.Minion;
            so.FindProperty("_summonCount").intValue = 1;
            so.FindProperty("_summonMaxAlive").intValue = 1;
            so.FindProperty("_summonPrefab").objectReferenceValue = spawner;
        });

        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath);
        if (profile != null) return profile;
        PlayerTestSceneBuilder.EnsureFolder("Assets/ScriptableObjects/Bosses");
        profile = ScriptableObject.CreateInstance<BossProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var reward = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Packet.asset");
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            so.FindProperty("_displayName").stringValue = DisplayName;
            so.FindProperty("_gaugeMax").intValue = 100;
            so.FindProperty("_phase2Threshold").floatValue = 0.65f; // ボスの体力が少なくなってから、大技（雑魚を出し続けるウィンドウ）
            so.FindProperty("_startDelay").floatValue = 1.2f;
            Moves(so, "_phase1", 1.3f, 2, packets1, bombs1);
            Moves(so, "_phase2", 1.0f, 2, packets2, bombs2);
            so.FindProperty("_phase2OpeningMove").objectReferenceValue = spawn;
            so.FindProperty("_movement").intValue = (int)BossMovement.Hover;
            F(so, "_hoverSpeed", 4.5f);
            var pts = new (float x, float y)[] { (-6f, 7.8f), (0f, 6.6f), (6f, 7.8f), (-3.3f, 4.2f), (3.3f, 4.2f), (0f, 1.3f) };
            var xs = so.FindProperty("_hoverPointsX");
            var ys = so.FindProperty("_hoverPointsY");
            xs.arraySize = ys.arraySize = pts.Length;
            for (int i = 0; i < pts.Length; i++)
            {
                xs.GetArrayElementAtIndex(i).floatValue = pts[i].x;
                ys.GetArrayElementAtIndex(i).floatValue = pts[i].y;
            }
            so.FindProperty("_reward").objectReferenceValue = reward;
        });
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static AttackData Attack(string file, Action<SerializedObject> init) => BossTestSceneBuilder.GetOrCreateAttack($"{AttackDir}/{file}", init);

    static void Packets(SerializedObject so, int count, float speed)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Packet, Color.white);
        F(so, "_windup", 0.2f);
        so.FindProperty("_projectileCount").intValue = count;
        F(so, "_spreadAngle", 90f);
        F(so, "_speed", speed);
        F(so, "_lifetime", 7f);
        so.FindProperty("_hitSize").vector2Value = new Vector2(16f / GameArt.PixelsPerUnit, 4f / GameArt.PixelsPerUnit);
        so.FindProperty("_destroyOnHit").boolValue = true;
        F(so, "_packetRange", 30f);         // 部屋のどこにいてもロックオンする
        F(so, "_packetInterval", 0.2f);
        F(so, "_packetTurnRate", 70f);      // ゆるく曲がる
        so.FindProperty("_packetHeal").intValue = 0;
        F(so, "_packetLockTime", 1.0f);     // IP アドレスを見せてから撃つ
        so.FindProperty("_packetDestructible").boolValue = true;
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Fx_Packet.png");
    }

    static void BombSites(SerializedObject so, int count)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.BombSite, Color.white);
        so.FindProperty("_fixedColors").boolValue = true; // サイトの絵と爆発の色は、アプリの色に塗り替えない
        F(so, "_windup", 0.3f);
        so.FindProperty("_spawnCount").intValue = count;
        so.FindProperty("_objectSize").vector2Value = new Vector2(48f / GameArt.PixelsPerUnit, 34f / GameArt.PixelsPerUnit); // 絵と同じ
        so.FindProperty("_areaSize").vector2Value = new Vector2(3.4f, 3.4f);
        F(so, "_telegraphTime", 1.6f);
        F(so, "_bombSlowPortion", 0.2f);
        F(so, "_bombFillTime", 0.25f);
        F(so, "_activeTime", 0.3f);
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Window_BombSite.png");
        so.FindProperty("_bombBlastSprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Fx_Blast.png");
    }

    static void Moves(SerializedObject so, string phase, float wait, int attacksPerMove, params AttackData[] moves)
    {
        var arr = so.FindProperty($"{phase}.Moves");
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
        so.FindProperty($"{phase}.WaitAfterAttack").floatValue = wait;
        so.FindProperty($"{phase}.AttacksPerMove").intValue = attacksPerMove;
    }

    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;

    // ───────── 対応アプリ表 ─────────

    /// <summary>
    /// 対応アプリ表に「ブラウザ」の行を足す（無いときだけ）。バンドルIDは入れない。
    /// カテゴリ ブラウザのアプリ（Safari・Arc・Firefox など）が代用ボスになるときのひな形（同じカテゴリ専用なので、Chrome の行より優先される）
    /// </summary>
    static void EnsureCatalogEntry(BossController prefab, BossProfile profile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(AppCatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("対応アプリ表がまだ無いので、ブラウザの行は足していません（先に「Tools/Team1/ランのシーンを作成」を実行してください）");
            return;
        }
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var apps = so.FindProperty("_apps");
            // Chrome の行も、このボス（アイコン＋テーマ色のモヤ）にする。テーマ・使用時間の設定はそのまま
            for (int i = 0; i < apps.arraySize; i++)
            {
                var e = apps.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("DisplayName").stringValue != "Chrome") continue;
                e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("Profile").objectReferenceValue = profile;
                e.FindPropertyRelative("SpawnPosition").vector2Value = SpawnPosition;
                e.FindPropertyRelative("StageScene").stringValue = AiChatBossBuilder.StageScene;
                e.FindPropertyRelative("AbilityStat").intValue = (int)AttackStat.None; // 能力はパケット（ボスの設定のごほうび）
            }
            for (int i = 0; i < apps.arraySize; i++)
            {
                var e = apps.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("DisplayName").stringValue != DisplayName) continue;
                if (e.FindPropertyRelative("BossPrefab").objectReferenceValue == null) e.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
                if (e.FindPropertyRelative("Profile").objectReferenceValue == null) e.FindPropertyRelative("Profile").objectReferenceValue = profile;
                e.FindPropertyRelative("SpawnPosition").vector2Value = SpawnPosition;
                return;
            }
            apps.arraySize++;
            var n = apps.GetArrayElementAtIndex(apps.arraySize - 1);
            n.FindPropertyRelative("DisplayName").stringValue = DisplayName;
            n.FindPropertyRelative("BundleIdPrefixes").arraySize = 0;
            n.FindPropertyRelative("ScreenTimeNames").arraySize = 0;
            n.FindPropertyRelative("Category").intValue = (int)SkillCategory.Browser;
            n.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
            n.FindPropertyRelative("Profile").objectReferenceValue = profile;
            n.FindPropertyRelative("SpawnPosition").vector2Value = SpawnPosition;
            n.FindPropertyRelative("StageScene").stringValue = AiChatBossBuilder.StageScene;
            n.FindPropertyRelative("Theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>("Assets/ScriptableObjects/Themes/Theme_Chrome.asset");
            n.FindPropertyRelative("StandInSameCategoryOnly").boolValue = true;
            n.FindPropertyRelative("StandInStageScene").stringValue = AiChatBossBuilder.StageScene;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Min").intValue = 80;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Max").intValue = 130;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Min").floatValue = 1.0f;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Max").floatValue = 0.8f;
            n.FindPropertyRelative("AbilityStat").intValue = (int)AttackStat.None;
        });
        EditorUtility.SetDirty(catalog);
    }
}

/// <summary>
/// ブラウザのボスを試すテストシーン（2026-11-19。ボス戦）。メニュー「Tools/Team1/ブラウザのボスのテストシーンを作成」から実行する。
/// プレイヤーは最初から 1：パケット を持っている。C キーで見た目を Safari → Chrome → Arc → Firefox と切り替える。R：やり直し、N：ゲージ +10
/// </summary>
public static class BrowserBossTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/BrowserBossTest.unity";

    [MenuItem("Tools/Team1/ブラウザのボスのテストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod BrowserBossTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        AbilitiesBuilder.EnsurePacketSkill();
        ArtAssets.Apply(); // 絵の取り込み設定（先に入れておくと、技に絵が入る）
        BrowserBossBuilder.GetOrCreateProfile();
        ArtAssets.Apply();
        BossBattleTestSceneBuilder.Build(ScenePath, new[] { "Assets/ScriptableObjects/Skills/Skill_Packet.asset" },
            (pos, player) => BrowserBossBuilder.CreateBoss(pos, player),
            new[] { "Safari", "Google Chrome", "Arc", "Firefox" },
            new[] { "com.apple.Safari", "com.google.Chrome", "company.thebrowser.Browser", "org.mozilla.firefox" },
            false, "ブラウザのボス（1：パケット　C：ボスのアプリを切り替え　R：やり直し　N：ゲージ+10）", BrowserBossBuilder.SpawnPosition);
    }
}
