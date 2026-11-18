using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// チャットのボス（2026-11-19。Slack・Discord・LINE などのアプリ。数値・配置はすべて【仮】）を用意するエディタ拡張。
/// メニュー「Tools/Team1/チャットのボスを用意」か、「ランのシーンを作成」から呼ばれる。作るもの（すでにある物は作り直さない。プレハブは毎回作り直す）：
/// ・技（Assets/ScriptableObjects/Attacks/ChatBoss）：メッセージの3連投（第1・第2形態）、チャットbot（2体・3体）、通知の大技（第2形態）
/// ・雑魚のプレハブ Assets/Prefabs/Enemies/Zako_Chatbot（プレイヤーを追いかけて歩く。必要ならジャンプ。足場の端から落ちる）
/// ・ボスの設定 Boss_Chat（倒すと、そのアプリの能力。設定の能力は Slack）
/// ・プレハブ Assets/Prefabs/Bosses/Boss_Chat（非表示で保存）。どのシーンにも置ける（狙う相手が空なら、シーンのプレイヤーを狙う）
/// ・対応アプリ表の「チャット」の行（カテゴリ SNS・チャットの代用ボスのひな形。ステージは AI チャットのボスと同じ Stage_AIChat）
/// 見た目は AI チャットのボスと同じ作り（アプリのアイコンの体と、アプリの色の煙）。メッセージの絵は、Slack・Discord・LINE なら、それぞれのボス用の絵に差し替える。
/// </summary>
public static class ChatBossBuilder
{
    const string ProfilePath = "Assets/ScriptableObjects/Bosses/Boss_Chat.asset";
    const string PrefabPath = "Assets/Prefabs/Bosses/Boss_Chat.prefab";
    const string MinionPath = "Assets/Prefabs/Enemies/Zako_Chatbot.prefab";
    const string AppCatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string DisplayName = "チャット";
    const string AttackDir = "ChatBoss";

    [MenuItem("Tools/Team1/チャットのボスを用意")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildForRun();
        Debug.Log($"チャットのボスを用意しました（{ProfilePath}・{PrefabPath}・{MinionPath}・対応アプリ表の「{DisplayName}」）");
    }

    /// <summary>ランで遊べるようにする：技・雑魚・設定・プレハブ・対応アプリ表の行（-executeMethod ChatBossBuilder.BuildForRun）</summary>
    public static void BuildForRun()
    {
        AbilitiesBuilder.EnsureChatSkills();
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
        return AiChatBossBuilder.CreateIconBoss("Boss_Chat", position, player, profile, so =>
        {
            so.FindProperty("_previewAppName").stringValue = "Slack";
            so.FindProperty("_previewBundleId").stringValue = "com.tinyspeck.slackmacgap";
            // メッセージの絵：Slack・Discord・LINE は、それぞれのボス用の絵に差し替える（ほかのアプリは、灰色の絵をアプリの色に塗り替える）
            Sprite head = ArtAssets.LoadSprite("Generated/Attacks/ChatBoss_Generic_Head.png");
            Sprite body = ArtAssets.LoadSprite("Generated/Attacks/ChatBoss_Generic_Body.png");
            var skins = so.FindProperty("_attackSkins");
            var list = new (string name, string file, float gap)[]
            {
                ("Slack", "Slack", -1f / GameArt.PixelsPerUnit),
                ("Discord", "Discord", -1f / GameArt.PixelsPerUnit),
                ("LINE", "Line", 1f / GameArt.PixelsPerUnit),
            };
            skins.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                var e = skins.GetArrayElementAtIndex(i);
                var names = e.FindPropertyRelative("AppNames");
                names.arraySize = 1;
                names.GetArrayElementAtIndex(0).stringValue = list[i].name;
                AiChatBossBuilder.Set(e.FindPropertyRelative("From"), new UnityEngine.Object[] { head, body });
                AiChatBossBuilder.Set(e.FindPropertyRelative("To"), new UnityEngine.Object[]
                {
                    ArtAssets.LoadSprite($"Generated/Attacks/ChatBoss_{list[i].file}_Head.png"),
                    ArtAssets.LoadSprite($"Generated/Attacks/ChatBoss_{list[i].file}_Body.png"),
                });
                e.FindPropertyRelative("OverrideChatGap").boolValue = true;
                e.FindPropertyRelative("ChatGap").floatValue = list[i].gap;
            }
        });
    }

    static void BuildPrefab(BossProfile profile)
    {
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Bosses");
        BossController boss = CreateBoss(AiChatBossBuilder.SpawnPosition, null, profile);
        boss.gameObject.SetActive(false); // 雑魚を倒すまで出さないので、非表示で保存する
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(boss.gameObject);
    }

    /// <summary>
    /// チャットbot の雑魚のプレハブ（歩く雑魚と同じ作り。HP1）。プレイヤーのいる方へ歩き、必要ならジャンプする。足場の端では折り返さず、落ちる。
    /// 絵は灰色のチャットbot（ボスが出すときに、アプリの色に塗り替えた絵に差し替える）
    /// </summary>
    static GameObject GetOrCreateMinionPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MinionPath);
        if (existing != null) return existing;
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Enemies");
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var holder = new GameObject("MinionHolder");
        StageTestSceneBuilder.CreateWalker("Zako_Chatbot", Vector2.zero, -1, holder.transform, square);
        GameObject go = holder.transform.Find("Zako_Chatbot").gameObject;
        go.transform.SetParent(null, true);
        PlayerTestSceneBuilder.SetProps(go.GetComponent<ZakoWalker>(), so =>
        {
            so.FindProperty("_speed").floatValue = 2.2f;
            so.FindProperty("_turnAtLedges").boolValue = false; // 端から落ちる
            so.FindProperty("_chasePlayer").boolValue = true;
            so.FindProperty("_jumpWhenNeeded").boolValue = true; // プレイヤーが上の段にいるとき・壁のときは跳ぶ
        });
        Sprite[] frames = ArtAssets.LoadFrames("Generated/Enemies/Zako_Chatbot.png");
        var anim = go.GetComponentInChildren<SpriteFrameAnimation>(true);
        if (anim != null && frames.Length > 0)
        {
            PlayerTestSceneBuilder.SetProps(anim, so => AiChatBossBuilder.Set(so.FindProperty("_frames"), frames));
            anim.GetComponent<SpriteRenderer>().sprite = frames[0];
        }
        PrefabUtility.SaveAsPrefabAsset(go, MinionPath);
        UnityEngine.Object.DestroyImmediate(go);
        UnityEngine.Object.DestroyImmediate(holder);
        return AssetDatabase.LoadAssetAtPath<GameObject>(MinionPath);
    }

    // ───────── 技・設定 ─────────

    /// <summary>技とボスの設定を用意する（すでにあれば、それを使う。数値は上書きしない）</summary>
    public static BossProfile GetOrCreateProfile()
    {
        GameObject minion = GetOrCreateMinionPrefab();
        // メッセージ：自分の前に3つ連投（0.35秒おき）→ 0.7秒後に、まとまったままプレイヤーの方へ飛ぶ。プレイヤーの物より大きく、色が濁っている
        AttackData msg1 = Attack("Attack_ChatBoss_Messages_P1", so => Messages(so, 0.35f, 0.7f, 5f));
        AttackData msg2 = Attack("Attack_ChatBoss_Messages_P2", so => Messages(so, 0.28f, 0.55f, 6f)); // 2026-11-19：ゆっくりに（8.5・10 から）
        // チャットbot：ボスの下に出て落ち、プレイヤーを追いかけて歩く（必要ならジャンプ）。同時に3体まで（2026-11-19：数を減らした）
        AttackData bots1 = Attack("Attack_ChatBoss_Chatbots_P1", so => Chatbots(so, minion, 1));
        AttackData bots2 = Attack("Attack_ChatBoss_Chatbots_P2", so => Chatbots(so, minion, 2));
        // 通知の大技（第2形態）：通知のベル3つ（どれにも残りの数）・3回鳴る（1.6秒後から2.2秒おき）。回収しきれないと部屋全体に攻撃。その間ボスは動かない
        AttackData alarm = Attack("Attack_ChatBoss_Alarm", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Alarm, Color.white);
            so.FindProperty("_fixedColors").boolValue = true; // 通知の赤は、アプリの色に塗り替えない
            F(so, "_windup", 0.5f);
            I(so, "_spawnCount", 3);
            I(so, "_alarmRings", 3);
            F(so, "_alarmFirstRing", 1.6f);
            F(so, "_alarmRingInterval", 2.2f);
            AiChatBossBuilder.Set(so.FindProperty("_alarmBellFrames"), ArtAssets.LoadFrames("Generated/Attacks/Fx_Bell.png"));
            AiChatBossBuilder.Set(so.FindProperty("_alarmBadgeFrames"), ArtAssets.LoadFrames("Generated/Attacks/Fx_NotifBadge.png"));
        });

        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath);
        if (profile != null) return profile;
        PlayerTestSceneBuilder.EnsureFolder("Assets/ScriptableObjects/Bosses");
        profile = ScriptableObject.CreateInstance<BossProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var reward = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Slack.asset");
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            so.FindProperty("_displayName").stringValue = DisplayName;
            so.FindProperty("_gaugeMax").intValue = 100;
            so.FindProperty("_startDelay").floatValue = 1.2f;
            Moves(so, "_phase1", 1.2f, 2, msg1, bots1);
            Moves(so, "_phase2", 0.9f, 2, msg2, bots2, alarm); // 後半は通知の大技が加わる
            E(so, "_movement", (int)BossMovement.Hover);
            F(so, "_hoverSpeed", 4.5f);
            // 画面全体を動く（上の段・中段・床の近く）
            var pts = new (float x, float y)[] { (-6f, 8.6f), (0f, 8.6f), (6f, 8.6f), (-3.3f, 4.2f), (3.3f, 4.2f), (0f, 1.3f) };
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

    static void Messages(SerializedObject so, float interval, float fireDelay, float speed)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Chat, Color.white);
        F(so, "_windup", 0.4f);
        I(so, "_repeatCount", 3);
        F(so, "_repeatInterval", interval);
        I(so, "_chatMaxMessages", 3);
        F(so, "_chatFireDelay", fireDelay);
        so.FindProperty("_chatAnchor").vector2Value = new Vector2(3.0f, -0.6f); // ボスの体の前（原点は体の真ん中）
        F(so, "_chatGap", 1f / GameArt.PixelsPerUnit);
        F(so, "_speed", speed);
        F(so, "_lifetime", 5f); // ゆっくりでも部屋の端まで届く
        E(so, "_aim", (int)AimMode.Target);
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/ChatBoss_Generic_Head.png");
        so.FindProperty("_chatBodySprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/ChatBoss_Generic_Body.png");
    }

    static void Chatbots(SerializedObject so, GameObject minion, int count)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Summon, Color.white);
        F(so, "_windup", 0.5f);
        E(so, "_summonType", (int)SummonType.Minion);
        I(so, "_summonCount", count);
        I(so, "_summonMaxAlive", 3);
        so.FindProperty("_summonPrefab").objectReferenceValue = minion;
        AiChatBossBuilder.Set(so.FindProperty("_summonFrames"), ArtAssets.LoadFrames("Generated/Enemies/Zako_Chatbot.png"));
    }

    static void Moves(SerializedObject so, string phase, float wait, int attacksPerMove, params AttackData[] moves)
    {
        var arr = so.FindProperty($"{phase}.Moves");
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
        so.FindProperty($"{phase}.WaitAfterAttack").floatValue = wait;
        so.FindProperty($"{phase}.AttacksPerMove").intValue = attacksPerMove;
    }

    static void E(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;
    static void I(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;

    // ───────── 対応アプリ表 ─────────

    /// <summary>
    /// 対応アプリ表に「チャット」の行を足す（無いときだけ。あれば、ボスのプレハブ・設定が空の欄だけ入れる）。
    /// バンドルIDは入れない。カテゴリ SNS・チャットのアプリ（Slack・Discord・LINE・Zoom など）が代用ボスになるときのひな形
    /// </summary>
    static void EnsureCatalogEntry(BossController prefab, BossProfile profile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(AppCatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("対応アプリ表がまだ無いので、チャットの行は足していません（先に「Tools/Team1/ランのシーンを作成」を実行してください）");
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
            n.FindPropertyRelative("Category").intValue = (int)SkillCategory.SocialChat;
            n.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
            n.FindPropertyRelative("Profile").objectReferenceValue = profile;
            n.FindPropertyRelative("SpawnPosition").vector2Value = AiChatBossBuilder.SpawnPosition;
            n.FindPropertyRelative("StageScene").stringValue = AiChatBossBuilder.StageScene;
            n.FindPropertyRelative("Theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>("Assets/ScriptableObjects/Themes/Theme_Slack.asset");
            n.FindPropertyRelative("StandInSameCategoryOnly").boolValue = true;
            n.FindPropertyRelative("StandInStageScene").stringValue = AiChatBossBuilder.StageScene;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Min").intValue = 80;
            n.FindPropertyRelative("GaugeMax").FindPropertyRelative("Max").intValue = 130;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Min").floatValue = 0.9f;
            n.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Max").floatValue = 0.7f;
            n.FindPropertyRelative("AbilityStat").intValue = (int)AttackStat.None;
        });
        EditorUtility.SetDirty(catalog);
    }
}
