using UnityEditor;
using UnityEngine;

/// <summary>
/// カテゴリの能力と、能力えらびの表を作るエディタ拡張（2026-11-17：ゲームの流れ）。
/// ほかのシーンの作成ツールから呼ぶ（RunScenesBuilder など）。作るもの（すでにあれば作らない。数値を直すときはアセットを直接直す）：
/// ・攻撃データ（Assets/ScriptableObjects/Attacks/Player）：棒グラフ・バケツ塗り・ボム（＋爆発）
/// ・能力（Assets/ScriptableObjects/Skills）：Skill_BarGraph（Office）・Skill_PaintBucket（クリエイティブ）・Skill_Bomb（ゲーム）・
///   Skill_AiAssistant（AI。2026-11-18。メニュー「Tools/Team1/AIアシスタントの能力を用意」でも作れる）
/// ・能力えらびの表（Assets/ScriptableObjects/Run/AbilityCatalog）：カテゴリ → 能力・アプリの名前・バンドルID
/// ・その場で作る能力のひな形（Assets/ScriptableObjects/Attacks/Player/Gen）：拡散ショット・トラップ弾（＋床）・ノヴァ。
///   表のひな形の欄が空なら、ブレークポイント・棒グラフ・バケツ塗り・ボムの攻撃と合わせて7つ入れる（2026-11-17）
/// 新しいタブ（ブラウザ）・ブレークポイント（開発）は BossTestSceneBuilder が作る。
/// SNS・チャット／音楽・動画の能力はまだ無い（表に入れないので、カードにも出ない）。
/// 数値はすべて【仮】。絵と色は ArtAssets（Tools/Team1/グラフィックを適用）が入れる。
/// </summary>
public static class AbilitiesBuilder
{
    public const string CatalogPath = "Assets/ScriptableObjects/Run/AbilityCatalog.asset";

    /// <summary>カテゴリの能力。All（テストシーンの G キーで渡す5つ）には AIアシスタントを入れない（専用のテストシーン AiAssistantTest で試す）</summary>
    public struct Skills
    {
        public SkillDefinition NewTab, Breakpoint, BarGraph, PaintBucket, Bomb, AiAssistant;
        public SkillDefinition[] All => new[] { NewTab, Breakpoint, BarGraph, PaintBucket, Bomb };
    }

    [MenuItem("Tools/Team1/AIアシスタントの能力を用意")]
    public static void EnsureAiAssistantFromMenu()
    {
        SkillDefinition skill = GetOrCreateAiAssistant();
        EnsureAiCategory(skill);
        ArtAssets.Apply(); // ロボット・レーザーの絵、アイコン、色を入れる
        AssetDatabase.SaveAssets();
        Debug.Log("AIアシスタントの能力を用意しました（Skill_AiAssistant。能力えらびの表の AI カテゴリ）");
    }

    /// <summary>
    /// AI の能力「AIアシスタント」（2026-11-18。数値は【仮】）。
    /// 使うと、プレイヤーの後ろ上に AI ロボットが出て、プレイヤーの体の中心から6マス以内の敵へレーザーを撃つ（0.25秒ごとに当たる）。
    /// レーザーを合計7秒撃つか、30秒たつと消える。クールタイム12秒は、ロボットが消えてから数える（PlayerSkillExecutor）。
    /// 絵（ロボット・レーザー・アイコン）と色は ArtAssets が入れる。
    /// </summary>
    public static SkillDefinition GetOrCreateAiAssistant()
    {
        AttackData attack = BossTestSceneBuilder.GetOrCreateAttack("Player/Attack_Ability_AiAssistant", so =>
        {
            Kind(so, AttackKind.Summon);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 12f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 4);              // 1秒当て続けるごとに +4（近接1回分）。撃ち切ると +28
            V(so, "_knockback", new Vector2(0.5f, 0.5f));
            F(so, "_hitStun", 0.05f);
            F(so, "_tickInterval", 1f);          // 1秒当て続けると1回当たる（雑魚は HP1 なので倒れる）
            F(so, "_summonFps", 10f);
            F(so, "_summonLifetime", 30f);
            F(so, "_summonFireTime", 7f);
            F(so, "_summonRange", 6f);
            V(so, "_summonHoverOffset", new Vector2(0.9f, 1.9f));
            F(so, "_summonFollowSharpness", 6f);
            F(so, "_beamWidth", 6f / GameArt.PixelsPerUnit); // レーザーの絵の高さ（6ドット）と同じ
        });
        return BossTestSceneBuilder.GetOrCreateSkill("Skill_AiAssistant", "AIアシスタント", SkillCategory.AI, attack);
    }

    /// <summary>
    /// 能力えらびの表に AI カテゴリ（ChatGPT・Codex・Claude・Perplexity）が無ければ足す。あれば、能力が空のときだけ入れる（手で直した値は残す）。
    /// アプリの名前・バンドルIDは docs 9章の表から
    /// </summary>
    public static void EnsureAiCategory(SkillDefinition skill)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(CatalogPath);
        if (catalog == null || skill == null) return;
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var arr = so.FindProperty("_categories");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("Category").intValue != (int)SkillCategory.AI) continue;
                var s = e.FindPropertyRelative("Skill");
                if (s.objectReferenceValue == null) s.objectReferenceValue = skill;
                return;
            }
            arr.arraySize++;
            Entry(arr.GetArrayElementAtIndex(arr.arraySize - 1), SkillCategory.AI, skill,
                new[] { "ChatGPT", "Codex", "Claude", "Perplexity" },
                new[] { "com.openai.", "com.anthropic.claudefordesktop", "ai.perplexity." });
        });
        EditorUtility.SetDirty(catalog);
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

        // ───── その場で作る能力のひな形（対応表に無いアプリ。効果の絵はゲームの中でアプリの色に塗り替える） ─────
        // 拡散ショット：正面の少し上（22°）へ、扇状（24°）に弾を撃つ。浮いているボスにも、床のボスにも届く
        AttackData spread = BossTestSceneBuilder.GetOrCreateAttack("Player/Gen/Attack_Gen_Spread", so =>
        {
            Kind(so, AttackKind.Projectile);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 2.5f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 3);
            V(so, "_knockback", new Vector2(2f, 1.5f));
            F(so, "_hitStun", 0.1f);
            I(so, "_projectileCount", 3);
            F(so, "_spreadAngle", 24f);
            E(so, "_aim", (int)AimMode.Forward);
            F(so, "_launchAngle", 22f);
            V(so, "_spawnOffset", new Vector2(0.3f, 0.8f));
            F(so, "_speed", 13f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 1f);
            V(so, "_hitSize", new Vector2(0.45f, 0.45f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
        });
        // トラップ弾：ボムと同じ放物線で投げ、当たった所に2.6×1の床が1.6秒残る（0.4秒ごとに当たり直す）
        AttackData trapFloor = BossTestSceneBuilder.GetOrCreateAttack("Player/Gen/Attack_Gen_TrapFloor", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 3);
            V(so, "_knockback", new Vector2(1f, 1f));
            F(so, "_hitStun", 0.1f);
            E(so, "_anchor", (int)AreaAnchor.HitPoint);
            B(so, "_alignToSurface", true);
            E(so, "_shape", (int)AreaShape.Box);
            V(so, "_areaSize", new Vector2(2.6f, 1f));
            I(so, "_areaCount", 1);
            F(so, "_telegraphTime", 0f);
            F(so, "_activeTime", 0.1f);
            F(so, "_persistDuration", 1.6f);
            F(so, "_tickInterval", 0.4f);
        });
        AttackData trap = BossTestSceneBuilder.GetOrCreateAttack("Player/Gen/Attack_Gen_Trap", so =>
        {
            Kind(so, AttackKind.Projectile);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 3.5f);
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 2);
            V(so, "_knockback", new Vector2(2f, 2f));
            F(so, "_hitStun", 0.1f);
            I(so, "_projectileCount", 1);
            E(so, "_aim", (int)AimMode.Forward);
            F(so, "_launchAngle", 60f);
            V(so, "_spawnOffset", new Vector2(0.3f, 0.8f));
            F(so, "_speed", 12f);
            F(so, "_gravity", 18f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.45f, 0.45f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
            R(so, "_onTerrainHit", trapFloor);
            B(so, "_followUpOnHit", true);
        });
        // ノヴァ：前方1.5マスの床の上に、直径4.5の円（浮いているボスにも届く高さ）
        AttackData nova = BossTestSceneBuilder.GetOrCreateAttack("Player/Gen/Attack_Gen_Nova", so =>
        {
            Kind(so, AttackKind.Area);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 2.5f);
            I(so, "_damage", 2);
            I(so, "_gaugeGain", 9);
            V(so, "_knockback", new Vector2(5f, 4f));
            F(so, "_hitStun", 0.2f);
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 1.5f);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Circle);
            V(so, "_areaSize", new Vector2(4.5f, 4.5f));
            I(so, "_areaCount", 1);
            F(so, "_telegraphTime", 0f);
            F(so, "_activeTime", 0.25f);
        });

        var skills = new Skills
        {
            NewTab = a.SkillNewTab,
            Breakpoint = a.SkillBreakpoint,
            BarGraph = BossTestSceneBuilder.GetOrCreateSkill("Skill_BarGraph", "棒グラフ", SkillCategory.Office, bar),
            PaintBucket = BossTestSceneBuilder.GetOrCreateSkill("Skill_PaintBucket", "バケツ塗り", SkillCategory.Creative, paint),
            Bomb = BossTestSceneBuilder.GetOrCreateSkill("Skill_Bomb", "ボム", SkillCategory.Game, bomb),
            AiAssistant = GetOrCreateAiAssistant(),
        };
        var catalog = GetOrCreateCatalog(skills);
        EnsureAiCategory(skills.AiAssistant);
        FillTemplates(catalog, new[]
        {
            // 名前・攻撃・アプリごとに変える数値と範囲（すべて【仮】）
            ("ピラー", a.AbilityBreakpoint, AttackStat.AreaScale, 0.8f, 1.25f),
            ("連続ピラー", bar, AttackStat.AreaCount, 2f, 4f),
            ("ペイント", paint, AttackStat.PersistDuration, 1.5f, 2.5f),
            ("ロブ弾", bomb, AttackStat.None, 1f, 1f),
            ("拡散ショット", spread, AttackStat.ProjectileCount, 3f, 5f),
            ("トラップ弾", trap, AttackStat.None, 1f, 1f),
            ("ノヴァ", nova, AttackStat.AreaScale, 0.9f, 1.2f),
        });
        EnsureAppSkills();
        EnsureChatSkills();
        EnsurePhotoshopSkill();
        AssetDatabase.SaveAssets();
        return skills;
    }

    // ───────── Photoshop の能力（2026-11-19：筆とインク。数値は【仮】） ─────────

    [MenuItem("Tools/Team1/Photoshopの能力を用意")]
    public static void EnsurePhotoshopSkillFromMenu()
    {
        EnsurePhotoshopSkill();
        ArtAssets.Apply(); // 筆とインクの絵・アプリの色・アイコンを入れる
        AssetDatabase.SaveAssets();
        Debug.Log("Photoshop の能力を用意しました（能力えらびの表の「アプリ専用の能力」に足しました）");
    }

    /// <summary>
    /// Photoshop の能力（取り回しのよい近距離の武器）：
    /// ・筆 Attack_App_Photoshop（種類 Brush）：長さ3.2マス（近接の約3倍のリーチ）を、0.2秒で下（-75°）から上（80°）へ振る。
    ///   筆にもダメージ2・ゲージ +6。振る途中で、インクが3つ飛び出す。クールタイム2.5秒
    /// ・インク Attack_App_Photoshop_Ink：直径1.4マス。1.8秒その場に残り、敵に当たるとダメージ1・ゲージ +2 で消える
    /// ・能力 Skill_App_Photoshop と、能力えらびの表の「アプリ専用の能力」の行（無いときだけ足す）。絵と色は ArtAssets が入れる
    /// </summary>
    public static SkillDefinition EnsurePhotoshopSkill()
    {
        AttackData ink = BossTestSceneBuilder.GetOrCreateAttack("Player/App/Attack_App_Photoshop_Ink", so =>
        {
            Kind(so, AttackKind.Area); // 中身は「残り続ける判定」と同じ項目を使う（大きさ・持続時間・ヒット間隔）
            I(so, "_damage", 1);
            I(so, "_gaugeGain", 2);
            V(so, "_knockback", new Vector2(1f, 1f));
            F(so, "_hitStun", 0.1f);
            V(so, "_areaSize", new Vector2(1.4f, 1.4f)); // 2026-11-19：1 から大きくした
            B(so, "_destroyOnHit", true);                // 敵に当たったら消える
            F(so, "_telegraphTime", 0f);                 // 予告なし（出た瞬間から当たる。予告があるとボスのインクのように点滅してから攻撃する）
            F(so, "_persistDuration", 1.8f);
            F(so, "_tickInterval", 0.5f);
        });
        AttackData brush = BossTestSceneBuilder.GetOrCreateAttack("Player/App/Attack_App_Photoshop", so =>
        {
            Kind(so, AttackKind.Brush);
            F(so, "_windup", 0f);
            F(so, "_cooldown", 2.5f);
            I(so, "_damage", 2);
            I(so, "_gaugeGain", 6);
            V(so, "_knockback", new Vector2(4f, 4f));
            F(so, "_hitStun", 0.2f);
            F(so, "_brushLength", 58f / GameArt.PixelsPerUnit); // 絵の長さ（58ドット）と同じ
            F(so, "_brushWidth", 0.5f);
            F(so, "_brushSwingTime", 0.2f);
            V(so, "_brushAngles", new Vector2(-75f, 80f));
            V(so, "_brushPivot", new Vector2(0.25f, 0.55f));
            R(so, "_brushInk", ink);
            I(so, "_brushInkCount", 3);
            F(so, "_brushInkFling", 0.8f);
        });
        SkillDefinition skill = BossTestSceneBuilder.GetOrCreateSkill("Skill_App_Photoshop", "Photoshop", SkillCategory.Creative, brush);

        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(CatalogPath);
        if (catalog == null) return skill;
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var arr = so.FindProperty("_appSkills");
            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("Label").stringValue == "Photoshop") return;
            }
            arr.arraySize++;
            AppSkill(arr.GetArrayElementAtIndex(arr.arraySize - 1), "Photoshop", skill, new[] { "Photoshop", "Adobe Photoshop" }, new[] { "com.adobe.Photoshop" });
        });
        EditorUtility.SetDirty(catalog);
        return skill;
    }

    // ───────── チャットアプリの能力（2026-11-19：Slack・Discord・LINE。数値は【仮】） ─────────

    [MenuItem("Tools/Team1/チャットアプリの能力を用意")]
    public static void EnsureChatSkillsFromMenu()
    {
        EnsureChatSkills();
        ArtAssets.Apply(); // メッセージの絵・エフェクト・アプリの色・アイコンを入れる
        AssetDatabase.SaveAssets();
        Debug.Log("チャットアプリの能力（Slack・Discord・LINE）を用意しました（能力えらびの表の「アプリ専用の能力」に足しました）");
    }

    /// <summary>
    /// アプリ1つ分：キー（アセット名）・能力の名前・表の確認用の名前・スクリーンタイムでの名前・バンドルID・
    /// メッセージの間のすき間（マス。マイナスなら重ねてつなげる）・飛ぶ速さ・いちばん新しいメッセージを出す高さ（足元から）
    /// </summary>
    static readonly (string key, string name, string label, string[] names, string[] bundles, float gap, float speed, float anchorY)[] ChatApps =
    {
        // LINE：吹き出しが1つずつ分かれる（すき間1ドット）。3つ積むと 26 ドットで、Slack・Discord と同じ大きさ（2026-11-19）
        ("Line", "LINE", "LINE", new[] { "LINE" }, new[] { "jp.naver.line.mac" }, 1f / GameArt.PixelsPerUnit, 16f, 0.42f),
        // Slack・Discord：続けて送ると1つの枠につながる（1ドット重ねて、上のメッセージの下の線を隠す）。
        // 出す高さは、いちばん下の端が LINE と同じ（足元から 0.2）になるように。小さな雑魚にも当たる（2026-11-19）
        ("Slack", "Slack", "Slack", new[] { "Slack" }, new[] { "com.tinyspeck.slackmacgap" }, -1f / GameArt.PixelsPerUnit, 15f, 0.45f),
        ("Discord", "Discord", "Discord", new[] { "Discord" }, new[] { "com.hnc.Discord" }, -1f / GameArt.PixelsPerUnit, 15f, 0.45f),
    };

    /// <summary>
    /// チャットアプリの能力（Slack・Discord・LINE）を用意する：攻撃データ Attack_App_〇〇（種類 Chat）・能力 Skill_App_〇〇・
    /// 能力えらびの表の「アプリ専用の能力」の行（無いときだけ足す。手で直した行は残す）。絵と色は ArtAssets が入れる。
    /// 止まっている間はクールタイムなしで3つまでメッセージを出せ、最後に足してから1秒で一斉に飛ぶ。動くか3つ出したらクールタイム2.5秒
    /// </summary>
    public static void EnsureChatSkills()
    {
        var created = new System.Collections.Generic.List<(string label, SkillDefinition skill, string[] names, string[] bundles)>();
        foreach (var app in ChatApps)
        {
            AttackData attack = BossTestSceneBuilder.GetOrCreateAttack($"Player/App/Attack_App_{app.key}", so =>
            {
                Kind(so, AttackKind.Chat);
                F(so, "_windup", 0f);
                F(so, "_cooldown", 2.5f);
                I(so, "_damage", 1);
                I(so, "_gaugeGain", 4);              // 1つにつき近接1回分。3つ全部当たると +12
                V(so, "_knockback", new Vector2(2f, 1.5f));
                F(so, "_hitStun", 0.15f);
                F(so, "_speed", app.speed);
                F(so, "_lifetime", 1.2f);
                B(so, "_destroyOnTerrain", true);
                B(so, "_destroyOnHit", true);
                F(so, "_chatGap", app.gap);
                I(so, "_chatMaxMessages", 3);
                F(so, "_chatFireDelay", 1f);
                V(so, "_chatAnchor", new Vector2(1.85f, app.anchorY)); // メッセージ（幅38ドット＝2.1マス）の左の端が、プレイヤーの体より前に来る（2026-11-19）
            });
            SkillDefinition skill = BossTestSceneBuilder.GetOrCreateSkill($"Skill_App_{app.key}", app.name, SkillCategory.SocialChat, attack);
            created.Add((app.label, skill, app.names, app.bundles));
        }

        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(CatalogPath);
        if (catalog == null) return;
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var arr = so.FindProperty("_appSkills");
            foreach (var c in created)
            {
                bool found = false;
                for (int i = 0; i < arr.arraySize && !found; i++) found = arr.GetArrayElementAtIndex(i).FindPropertyRelative("Label").stringValue == c.label;
                if (found) continue;
                arr.arraySize++;
                AppSkill(arr.GetArrayElementAtIndex(arr.arraySize - 1), c.label, c.skill, c.names, c.bundles);
            }
        });
        EditorUtility.SetDirty(catalog);
    }

    const string AppSkillAttackFolder = "Assets/ScriptableObjects/Attacks/Player/App";
    const string SkillFolder = "Assets/ScriptableObjects/Skills";

    /// <summary>
    /// アプリ専用の能力（2026-11-18 まさぴ〜決定：Zoom・Finder・Premiere Pro）を用意する。中身はチームで決めるので、今は【仮】：
    /// ・能力のアセット Skill_App_〇〇（名前は「〇〇（仮）」、アイコンは空＝アプリのアイコンで代わりに出す）
    /// ・攻撃データ Attack_App_〇〇（今ある能力の攻撃の複製。このアプリだけの物なので、書き換えてもほかの能力は変わらない）
    /// ・能力えらびの表の「アプリ専用の能力」の欄（空のときだけ入れる。手で直した値は残す）と、除外の表から Finder を外す
    /// すでにあるアセットは作り直さない。
    /// </summary>
    public static void EnsureAppSkills()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(CatalogPath);
        if (catalog == null) return;
        SkillDefinition zoom = GetOrCreateAppSkill("Zoom", "Zoom（仮）", SkillCategory.SocialChat, "Gen/Attack_Gen_Spread");
        SkillDefinition finder = GetOrCreateAppSkill("Finder", "Finder（仮）", SkillCategory.Unknown, "Gen/Attack_Gen_Nova");
        SkillDefinition premiere = GetOrCreateAppSkill("PremierePro", "Premiere Pro（仮）", SkillCategory.Creative, "Attack_Ability_BarGraph");
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var arr = so.FindProperty("_appSkills");
            if (arr.arraySize == 0)
            {
                arr.arraySize = 3;
                AppSkill(arr.GetArrayElementAtIndex(0), "Zoom", zoom, new[] { "Zoom", "zoom.us", "Zoom Workplace" }, new[] { "us.zoom.xos" });
                AppSkill(arr.GetArrayElementAtIndex(1), "Finder", finder, new[] { "Finder" }, new[] { "com.apple.finder" });
                AppSkill(arr.GetArrayElementAtIndex(2), "Premiere Pro", premiere, new[] { "Premiere Pro", "Adobe Premiere Pro" }, new[] { "com.adobe.PremierePro" });
            }
            RemoveString(so.FindProperty("_excludedNames"), "Finder");
            RemoveString(so.FindProperty("_excludedBundleIds"), "com.apple.finder");
        });
        EditorUtility.SetDirty(catalog);
    }

    static SkillDefinition GetOrCreateAppSkill(string key, string displayName, SkillCategory category, string sourceAttack)
    {
        string attackPath = $"{AppSkillAttackFolder}/Attack_App_{key}.asset";
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(attackPath);
        if (attack == null)
        {
            PlayerTestSceneBuilder.EnsureFolder(AppSkillAttackFolder);
            AssetDatabase.CopyAsset($"Assets/ScriptableObjects/Attacks/Player/{sourceAttack}.asset", attackPath);
            attack = AssetDatabase.LoadAssetAtPath<AttackData>(attackPath);
        }
        string skillPath = $"{SkillFolder}/Skill_App_{key}.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPath);
        if (skill != null) return skill;
        PlayerTestSceneBuilder.EnsureFolder(SkillFolder);
        skill = ScriptableObject.CreateInstance<SkillDefinition>();
        AssetDatabase.CreateAsset(skill, skillPath);
        PlayerTestSceneBuilder.SetProps(skill, so =>
        {
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_icon").objectReferenceValue = null; // 空＝アプリのアイコンで代わりに出す（絵ができたら入れる）
            so.FindProperty("_category").intValue = (int)category;
            so.FindProperty("_attack").objectReferenceValue = attack;
        });
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static void AppSkill(SerializedProperty e, string label, SkillDefinition skill, string[] names, string[] bundles)
    {
        e.FindPropertyRelative("Label").stringValue = label;
        e.FindPropertyRelative("Skill").objectReferenceValue = skill;
        Strings(e.FindPropertyRelative("AppNames"), names);
        Strings(e.FindPropertyRelative("BundleIdPrefixes"), bundles);
    }

    static void RemoveString(SerializedProperty arr, string value)
    {
        for (int i = arr.arraySize - 1; i >= 0; i--)
        {
            if (string.Equals(arr.GetArrayElementAtIndex(i).stringValue, value, System.StringComparison.OrdinalIgnoreCase)) arr.DeleteArrayElementAtIndex(i);
        }
    }

    /// <summary>表のひな形の欄が空なら入れる（すでに入っていれば、手で直した値を残す）</summary>
    static void FillTemplates(AbilityCatalog catalog, (string name, AttackData attack, AttackStat stat, float min, float max)[] templates)
    {
        if (catalog == null || catalog.Templates.Length > 0) return;
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            var arr = so.FindProperty("_templates");
            arr.arraySize = templates.Length;
            for (int i = 0; i < templates.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Name").stringValue = templates[i].name;
                e.FindPropertyRelative("Attack").objectReferenceValue = templates[i].attack;
                e.FindPropertyRelative("VaryStat").intValue = (int)templates[i].stat;
                e.FindPropertyRelative("VaryMin").floatValue = templates[i].min;
                e.FindPropertyRelative("VaryMax").floatValue = templates[i].max;
            }
        });
        EditorUtility.SetDirty(catalog);
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
            I(so, "_cardCount", 3); // 使用時間の上位3アプリ（2026-11-18：画面のイメージ）
            I(so, "_pickCount", 1); // 最初に持てる能力は1つ（2026-11-17 まさぴ〜決定）
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
