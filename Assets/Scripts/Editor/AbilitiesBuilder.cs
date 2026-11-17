using UnityEditor;
using UnityEngine;

/// <summary>
/// カテゴリの能力と、能力えらびの表を作るエディタ拡張（2026-11-17：ゲームの流れ）。
/// ほかのシーンの作成ツールから呼ぶ（RunScenesBuilder など）。作るもの（すでにあれば作らない。数値を直すときはアセットを直接直す）：
/// ・攻撃データ（Assets/ScriptableObjects/Attacks/Player）：棒グラフ・バケツ塗り・ボム（＋爆発）
/// ・能力（Assets/ScriptableObjects/Skills）：Skill_BarGraph（Office）・Skill_PaintBucket（クリエイティブ）・Skill_Bomb（ゲーム）
/// ・能力えらびの表（Assets/ScriptableObjects/Run/AbilityCatalog）：カテゴリ → 能力・アプリの名前・バンドルID
/// 新しいタブ（ブラウザ）・ブレークポイント（開発）は BossTestSceneBuilder が作る。
/// SNS・チャット／AI／音楽・動画の能力はまだ無い（表に入れないので、カードにも出ない）。
/// 数値はすべて【仮】。絵と色は ArtAssets（Tools/Team1/グラフィックを適用）が入れる。
/// </summary>
public static class AbilitiesBuilder
{
    public const string CatalogPath = "Assets/ScriptableObjects/Run/AbilityCatalog.asset";

    /// <summary>カテゴリの能力5つ</summary>
    public struct Skills
    {
        public SkillDefinition NewTab, Breakpoint, BarGraph, PaintBucket, Bomb;
        public SkillDefinition[] All => new[] { NewTab, Breakpoint, BarGraph, PaintBucket, Bomb };
    }

    /// <summary>能力5つと、能力えらびの表を用意する（無ければ作る）</summary>
    public static Skills GetOrCreate()
    {
        var a = BossTestSceneBuilder.GetOrCreateAttacks();

        // 棒グラフ：前方に、背の高い柱を3本、手前から順に立てる（0.15秒ずつずらす）。浮いているボスにも届く高さ
        AttackData bar = BossTestSceneBuilder.GetOrCreateAttack("Player/Attack_Ability_BarGraph", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 3.5f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 5);
            V(so, "_knockback", new Vector2(3f, 2f));
            F(so, "_hitStun", 0.15f);
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 2f);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Box);
            V(so, "_areaSize", new Vector2(1.2f, 4f));
            I(so, "_areaCount", 3);
            F(so, "_areaSpacing", 1.8f);
            F(so, "_areaStagger", 0.15f);
            B(so, "_areaForwardOrder", true);
            F(so, "_telegraphTime", 0f);
            F(so, "_activeTime", 0.25f);
        });

        // バケツ塗り：前方の範囲を絵の具で塗りつぶす。2秒残り、0.5秒ごとに当たり直す
        AttackData paint = BossTestSceneBuilder.GetOrCreateAttack("Player/Attack_Ability_PaintBucket", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 5f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 3);
            V(so, "_knockback", new Vector2(1f, 1f));
            F(so, "_hitStun", 0.1f);
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 2.5f);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Box);
            V(so, "_areaSize", new Vector2(3.5f, 3.2f));
            I(so, "_areaCount", 1);
            F(so, "_telegraphTime", 0f);
            F(so, "_activeTime", 0.1f);
            F(so, "_persistDuration", 2f);
            F(so, "_tickInterval", 0.5f);
        });

        // ボムの爆発：当たった位置に円の判定
        AttackData blast = BossTestSceneBuilder.GetOrCreateAttack("Player/Attack_Ability_Bomb_Blast", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            I(so, "_damage", 3);
            I(so, "_gaugeGain", 8);
            V(so, "_knockback", new Vector2(6f, 6f));
            F(so, "_hitStun", 0.3f);
            E(so, "_anchor", (int)AreaAnchor.HitPoint);
            B(so, "_alignToSurface", false);
            E(so, "_shape", (int)AreaShape.Circle);
            V(so, "_areaSize", new Vector2(4f, 1f)); // 円の直径4。中心は当たった位置の0.5上
            I(so, "_areaCount", 1);
            F(so, "_telegraphTime", 0f);
            F(so, "_activeTime", 0.2f);
        });

        // ボム：正面の斜め上へ投げる（放物線）。床・壁・相手に当たると爆発する
        AttackData bomb = BossTestSceneBuilder.GetOrCreateAttack("Player/Attack_Ability_Bomb", so =>
        {
            Kind(so, AttackKind.Projectile);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 3f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 4);
            V(so, "_knockback", new Vector2(3f, 3f));
            F(so, "_hitStun", 0.15f);
            I(so, "_projectileCount", 1);
            F(so, "_spreadAngle", 0f);
            E(so, "_aim", (int)AimMode.Forward);
            F(so, "_launchAngle", 60f);
            V(so, "_spawnOffset", new Vector2(0.3f, 0.8f)); // 足元ではなく、体の中ほどから投げる
            F(so, "_speed", 12f);
            F(so, "_gravity", 18f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.5f, 0.5f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
            R(so, "_onTerrainHit", blast);
            B(so, "_followUpOnHit", true);
        });

        var skills = new Skills
        {
            NewTab = a.SkillNewTab,
            Breakpoint = a.SkillBreakpoint,
            BarGraph = BossTestSceneBuilder.GetOrCreateSkill("Skill_BarGraph", "棒グラフ", SkillCategory.Office, bar),
            PaintBucket = BossTestSceneBuilder.GetOrCreateSkill("Skill_PaintBucket", "バケツ塗り", SkillCategory.Creative, paint),
            Bomb = BossTestSceneBuilder.GetOrCreateSkill("Skill_Bomb", "ボム", SkillCategory.Game, bomb),
        };
        GetOrCreateCatalog(skills);
        AssetDatabase.SaveAssets();
        return skills;
    }

    /// <summary>能力えらびの表を作る（すでにあれば作らない）。アプリの名前・バンドルIDは docs 9章の表から</summary>
    static AbilityCatalog GetOrCreateCatalog(Skills s)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        PlayerTestSceneBuilder.EnsureFolder("Assets/ScriptableObjects/Run");
        catalog = ScriptableObject.CreateInstance<AbilityCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            I(so, "_cardCount", 5);
            I(so, "_pickCount", 3);
            var arr = so.FindProperty("_categories");
            arr.arraySize = 5;
            Entry(arr.GetArrayElementAtIndex(0), SkillCategory.Browser, s.NewTab,
                new[] { "Google Chrome", "Chrome", "Safari", "Arc", "Firefox", "Microsoft Edge", "Brave Browser", "Brave" },
                new[] { "com.google.Chrome", "com.apple.Safari", "company.thebrowser.", "org.mozilla.firefox", "com.microsoft.edgemac", "com.brave.Browser" });
            Entry(arr.GetArrayElementAtIndex(1), SkillCategory.Dev, s.Breakpoint,
                new[] { "Visual Studio Code", "Code", "Cursor", "Xcode", "Simulator", "シミュレーター", "Unity", "Unity Hub", "Terminal", "ターミナル",
                        "IntelliJ IDEA", "IntelliJ IDEA CE", "GitHub Desktop", "iTerm", "iTerm2", "Warp", "Rider", "Docker Desktop", "Postman" },
                new[] { "com.microsoft.VSCode", "com.todesktop.230313mzl4w4u92", "com.apple.dt.Xcode", "com.apple.iphonesimulator", "com.unity3d.",
                        "com.apple.Terminal", "com.jetbrains.", "com.github.GitHubClient", "com.googlecode.iterm2", "dev.warp.", "com.docker.", "com.postmanlabs." });
            Entry(arr.GetArrayElementAtIndex(2), SkillCategory.Office, s.BarGraph,
                new[] { "Notion", "Microsoft Word", "Word", "Microsoft Excel", "Excel", "Microsoft PowerPoint", "PowerPoint", "Keynote", "Pages", "Numbers",
                        "Mail", "メール", "Calendar", "カレンダー", "Notes", "メモ", "Microsoft Outlook", "Outlook", "Obsidian", "Linear" },
                new[] { "notion.id", "com.microsoft.Word", "com.microsoft.Excel", "com.microsoft.Powerpoint", "com.microsoft.Outlook", "com.apple.iWork.",
                        "com.apple.mail", "com.apple.iCal", "com.apple.Notes", "md.obsidian", "com.linear" });
            Entry(arr.GetArrayElementAtIndex(3), SkillCategory.Creative, s.PaintBucket,
                new[] { "Figma", "Photoshop", "Illustrator", "Premiere Pro", "After Effects", "Blender", "OBS", "OBS Studio", "GarageBand",
                        "Aseprite", "CLIP STUDIO PAINT", "Final Cut Pro" },
                new[] { "com.figma.", "com.adobe.", "org.blenderfoundation.", "com.obsproject.", "com.apple.garageband", "com.aseprite", "jp.co.celsys.", "com.apple.FinalCut" });
            Entry(arr.GetArrayElementAtIndex(4), SkillCategory.Game, s.Bomb,
                new[] { "Steam", "Minecraft Launcher", "Minecraft", "Epic Games Launcher" },
                new[] { "com.valvesoftware.steam", "com.mojang.", "com.epicgames." });
        });
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    static void Entry(SerializedProperty e, SkillCategory category, SkillDefinition skill, string[] names, string[] bundles)
    {
        e.FindPropertyRelative("Category").intValue = (int)category; // 値を明示した enum なので、値で入れる
        e.FindPropertyRelative("Skill").objectReferenceValue = skill;
        Strings(e.FindPropertyRelative("AppNames"), names);
        Strings(e.FindPropertyRelative("BundleIdPrefixes"), bundles);
    }

    static void Strings(SerializedProperty arr, string[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    static void Kind(SerializedObject so, AttackKind kind) => so.FindProperty("_kind").intValue = (int)kind;
    static void E(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void F(SerializedObject so, string prop, float value) => so.FindProperty(prop).floatValue = value;
    static void I(SerializedObject so, string prop, int value) => so.FindProperty(prop).intValue = value;
    static void V(SerializedObject so, string prop, Vector2 value) => so.FindProperty(prop).vector2Value = value;
    static void B(SerializedObject so, string prop, bool value) => so.FindProperty(prop).boolValue = value;
    static void R(SerializedObject so, string prop, Object value) => so.FindProperty(prop).objectReferenceValue = value;
}
