using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Photoshop のボス（2026-11-19。クリエイティブのアプリ。数値・配置はすべて【仮】）を用意するエディタ拡張。
/// メニュー「Tools/Team1/Photoshopのボスを用意」か、「ランのシーンを作成」から呼ばれる。作るもの（すでにある物は作り直さない。プレハブは毎回作り直す）：
/// ・技（Assets/ScriptableObjects/Attacks/PhotoshopBoss）：大きな筆（部屋の端へ寄って振り回し、インクをまき散らす。インクは点滅のあと攻撃）、
///   選択ツール（カーソルが伸びて四角く選び、インクの生命体を出す）、大技のフィルター（第2形態の最初に1回。画面の色が変わり、状態異常）
/// ・雑魚のプレハブ Assets/Prefabs/Enemies/Zako_InkBlob（大きめ・ゆっくり・HP3。プレイヤーを追いかけて歩く）
/// ・ボスの設定 Boss_Photoshop（倒すと、そのアプリの能力。設定の能力は Photoshop）
/// ・プレハブ Assets/Prefabs/Bosses/Boss_Photoshop（非表示で保存）。どのシーンにも置ける
/// ・対応アプリ表の「Photoshop」の行（カテゴリ クリエイティブの代用ボスのひな形。ステージは Stage_AIChat）
/// 見た目はほかのアプリのボスと同じ作り（アプリのアイコンの体と、アプリの色のオーラ）。技の絵もアプリの色に塗り替える。
/// </summary>
public static class PhotoshopBossBuilder
{
    const string ProfilePath = "Assets/ScriptableObjects/Bosses/Boss_Photoshop.asset";
    const string PrefabPath = "Assets/Prefabs/Bosses/Boss_Photoshop.prefab";
    const string MinionPath = "Assets/Prefabs/Enemies/Zako_InkBlob.prefab";
    const string AppCatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    const string DisplayName = "Photoshop";
    const string AttackDir = "PhotoshopBoss";

    [MenuItem("Tools/Team1/Photoshopのボスを用意")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildForRun();
        Debug.Log($"Photoshop のボスを用意しました（{ProfilePath}・{PrefabPath}・{MinionPath}・対応アプリ表の「{DisplayName}」）");
    }

    /// <summary>ランで遊べるようにする（-executeMethod PhotoshopBossBuilder.BuildForRun）</summary>
    public static void BuildForRun()
    {
        AbilitiesBuilder.EnsurePhotoshopSkill();
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
        return AiChatBossBuilder.CreateIconBoss("Boss_Photoshop", position, player, profile, so =>
        {
            so.FindProperty("_previewAppName").stringValue = "Photoshop";
            so.FindProperty("_previewBundleId").stringValue = "com.adobe.Photoshop";
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

    /// <summary>インクの生命体のプレハブ（歩く雑魚と同じ作り）：大きめ（1.2×1.6）・ゆっくり（毎秒1.1マス）・HP3。プレイヤーを追いかけ、足場の端から落ちる</summary>
    static GameObject GetOrCreateMinionPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MinionPath);
        if (existing != null) return existing;
        PlayerTestSceneBuilder.EnsureFolder("Assets/Prefabs/Enemies");
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var holder = new GameObject("MinionHolder");
        StageTestSceneBuilder.CreateWalker("Zako_InkBlob", Vector2.zero, -1, holder.transform, square,
            size: new Vector2(1.2f, 1.6f), hp: 3, frames: "Generated/Enemies/Zako_InkBlob.png");
        GameObject go = holder.transform.Find("Zako_InkBlob").gameObject;
        go.transform.SetParent(null, true);
        PlayerTestSceneBuilder.SetProps(go.GetComponent<ZakoWalker>(), so =>
        {
            so.FindProperty("_speed").floatValue = 1.1f;
            so.FindProperty("_turnAtLedges").boolValue = false;
            so.FindProperty("_chasePlayer").boolValue = true;
            so.FindProperty("_knockbackScale").floatValue = 0.25f; // 重いので、あまり押し戻されない
        });
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
        // インク：空中に漂い、攻撃の範囲が薄い赤で数回点滅（1.6秒）してから攻撃（0.3秒）。直径1.6
        AttackData ink = Attack("Attack_PsBoss_Ink", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Area, Color.white);
            so.FindProperty("_areaSize").vector2Value = new Vector2(1.6f, 1.6f);
            F(so, "_telegraphTime", 1.6f);
            F(so, "_activeTime", 0.3f);
            so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Fx_BossInk.png");
        });
        // 大きな筆：部屋の端（プレイヤーと反対側）へ寄ってから、反対側へ向けて上から下へ振り回し、インクをまき散らす（筆そのものは当たらない）
        AttackData sweep1 = Attack("Attack_PsBoss_BrushSweep_P1", so => Sweep(so, ink, 8));
        AttackData sweep2 = Attack("Attack_PsBoss_BrushSweep_P2", so => Sweep(so, ink, 12));
        // 選択ツール：カーソルが伸びてきて四角く選び、その中にインクの生命体を出す
        AttackData marquee1 = Attack("Attack_PsBoss_Marquee_P1", so => Marquee(so, minion, 1, 2));
        AttackData marquee2 = Attack("Attack_PsBoss_Marquee_P2", so => Marquee(so, minion, 2, 3));
        // 大技：画面にフィルター。画面の色が変わり、状態異常が2つ（30秒）
        AttackData filter = Attack("Attack_PsBoss_Filter", so =>
        {
            BossTestSceneBuilder.BossCommon(so, AttackKind.Filter, Color.white);
            so.FindProperty("_fixedColors").boolValue = true;
            F(so, "_windup", 1.0f);
            F(so, "_activeTime", 1.2f);
            F(so, "_filterDuration", 30f);
            so.FindProperty("_filterEffectCount").intValue = 2;
            var tints = so.FindProperty("_filterTints");
            // 名前の順（色相・彩度／セピア／ポスタリゼーション／ネオン光彩）
            var colors = new[] { new Color(0.65f, 0.25f, 1f, 0.22f), new Color(0.95f, 0.65f, 0.25f, 0.2f), new Color(0.1f, 0.85f, 1f, 0.18f), new Color(1f, 0.25f, 0.65f, 0.2f) };
            tints.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++) tints.GetArrayElementAtIndex(i).colorValue = colors[i];
        });

        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfilePath);
        if (profile != null) return profile;
        PlayerTestSceneBuilder.EnsureFolder("Assets/ScriptableObjects/Bosses");
        profile = ScriptableObject.CreateInstance<BossProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var reward = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Photoshop.asset");
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            so.FindProperty("_displayName").stringValue = DisplayName;
            so.FindProperty("_gaugeMax").intValue = 100;
            so.FindProperty("_startDelay").floatValue = 1.2f;
            Moves(so, "_phase1", 1.3f, 2, sweep1, marquee1);
            Moves(so, "_phase2", 1.0f, 2, sweep2, marquee2);
            so.FindProperty("_phase2OpeningMove").objectReferenceValue = filter;
            so.FindProperty("_movement").intValue = (int)BossMovement.Hover;
            F(so, "_hoverSpeed", 4.5f);
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

    static void Sweep(SerializedObject so, AttackData ink, int inkCount)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Brush, Color.white);
        F(so, "_windup", 0.35f);
        F(so, "_brushLength", 9f);
        F(so, "_brushWidth", 0.9f);
        F(so, "_brushSwingTime", 0.6f);
        so.FindProperty("_brushAngles").vector2Value = new Vector2(110f, -55f); // 上から下へ
        so.FindProperty("_brushPivot").vector2Value = Vector2.zero;            // 体の真ん中で持つ
        so.FindProperty("_brushHits").boolValue = false;                       // 筆そのものは当たらない（インクで攻撃）
        so.FindProperty("_brushInk").objectReferenceValue = ink;
        so.FindProperty("_brushInkCount").intValue = inkCount;
        F(so, "_brushInkFling", 13f);
        F(so, "_brushInkFlingMin", 3f);
        so.FindProperty("_brushInkScatter").boolValue = true;
        so.FindProperty("_brushMoveToEdge").boolValue = true;
        F(so, "_brushMoveTime", 0.8f);
        so.FindProperty("_sprite").objectReferenceValue = ArtAssets.LoadSprite("Generated/Attacks/Brush_Photoshop.png");
    }

    static void Marquee(SerializedObject so, GameObject minion, int count, int maxAlive)
    {
        BossTestSceneBuilder.BossCommon(so, AttackKind.Summon, Color.white);
        F(so, "_windup", 0.3f);
        so.FindProperty("_summonType").intValue = (int)SummonType.Minion;
        so.FindProperty("_summonCount").intValue = count;
        so.FindProperty("_summonMaxAlive").intValue = maxAlive;
        so.FindProperty("_summonMarquee").boolValue = true;
        so.FindProperty("_summonPrefab").objectReferenceValue = minion;
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
    /// 対応アプリ表に「Photoshop」の行を足す（無いときだけ。あれば、ボスのプレハブ・設定が空の欄だけ入れる）。
    /// バンドルIDは入れない。カテゴリ クリエイティブのアプリ（Photoshop・Figma・Illustrator・Premiere Pro など）が代用ボスになるときのひな形
    /// </summary>
    static void EnsureCatalogEntry(BossController prefab, BossProfile profile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(AppCatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("対応アプリ表がまだ無いので、Photoshop の行は足していません（先に「Tools/Team1/ランのシーンを作成」を実行してください）");
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
            n.FindPropertyRelative("Category").intValue = (int)SkillCategory.Creative;
            n.FindPropertyRelative("BossPrefab").objectReferenceValue = prefab;
            n.FindPropertyRelative("Profile").objectReferenceValue = profile;
            n.FindPropertyRelative("SpawnPosition").vector2Value = AiChatBossBuilder.SpawnPosition;
            n.FindPropertyRelative("StageScene").stringValue = AiChatBossBuilder.StageScene;
            n.FindPropertyRelative("Theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>("Assets/ScriptableObjects/Themes/Theme_Photoshop.asset");
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
