using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 絵の取り込みと割り当て（メニュー：Tools/Team1/グラフィックを適用）。
/// ・Assets/Sprites の決まった画像に、取り込み設定を入れる（ドットをくっきり・圧縮なし・コマ分け・9スライスの縁・原点）
///   決まった画像だけに入れるので、デザイン担当が新しく置いた画像の設定は変えない
/// ・Resources/GameArt.asset（実行中に使う絵の置き場）を作って中身を入れる
/// ・攻撃データ・能力・ボスの設定に絵を入れる（すでにある物は、絵と色の欄だけを書き換える）
/// ・アプリの色（AppTheme：Assets/ScriptableObjects/Themes）を作り、対応アプリ表と能力につなぐ（色は作るときだけ入れる）
/// ・プレイヤーの Animator Controller を作る（無いときだけ）
/// シーンの作成ツールは、最初にこれを呼ぶ。
/// ドットの大きさ：ゲーム中の物は 1マス＝18ドット、背景は 15ドット（Tools/art/gen_sprites.py の説明）
/// </summary>
public static class ArtAssets
{
    public const string SpriteRoot = "Assets/Sprites";
    const string GameArtPath = "Assets/Resources/GameArt.asset";
    const string AnimFolder = "Assets/Animations/Player";
    public const string PlayerControllerPath = AnimFolder + "/Player.controller";

    /// <summary>取り込み設定（ppu：1マスのドット数、frame：1コマの幅（0なら1枚絵）、pivot：原点、border：縁（左, 下, 右, 上））</summary>
    struct Rule
    {
        public string Path;
        public float Ppu;
        public int Frame;
        public Vector2 Pivot;
        public Vector4 Border;
        public bool Readable; // 実行中に色を塗り替える絵（ThemeRecolor が読む）
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    static readonly Vector2 Feet = new Vector2(0.5f, 0f);

    static Rule R(string path, float ppu = 18f, int frame = 0, Vector2? pivot = null, Vector4? border = null, bool readable = false) =>
        new Rule { Path = path, Ppu = ppu, Frame = frame, Pivot = pivot ?? Center, Border = border ?? Vector4.zero, Readable = readable };

    static readonly Rule[] Rules =
    {
        // デザイン担当の絵（3倍に拡大して描かれているので 18×3＝54）
        R("Provided/Player_Walk.png", 54f, 39, Feet),
        R("Provided/Fx_VirusSmoke.png", 54f, 147),
        R("Provided/Bg_Wallpaper.png", 15f),
        R("Provided/Mockup_Desktop.png", 15f),
        // 背景（デザイン担当の壁紙から、下の道路を消したもの。画面いっぱい：320×180 ÷ 15 ＝ 21.33×12 マス）
        R("Generated/Background/Bg_Desktop.png", 15f, readable: true),
        // 地形
        R("Generated/Stage/Tile_Floor.png", pivot: new Vector2(0.5f, 1f), border: new Vector4(0, 0, 0, 3), readable: true),
        R("Generated/Stage/Tile_Wall.png", border: new Vector4(3, 0, 3, 0), readable: true),
        R("Generated/Stage/Platform_Folder.png", pivot: new Vector2(0.5f, 5f / 6f), border: new Vector4(9, 0, 3, 0), readable: true),
        R("Generated/Stage/Platform_Window.png", pivot: new Vector2(0.5f, 1f), border: new Vector4(8, 0, 3, 0), readable: true), // 2026-11-19：左の信号機の点（Mac）を伸ばさない
        R("Generated/Stage/Mac_Frame.png", border: new Vector4(3, 3, 3, 9)),   // 2026-11-19：Mac のウィンドウのふち（盾のウィンドウ）
        R("Generated/Stage/Mac_Lights.png"),                                   // 2026-11-19：信号機の3つの丸
        R("Generated/Stage/Room_Editor.png"),
        R("Generated/Stage/Room_TitleBar.png", border: new Vector4(1, 0, 1, 0), readable: true),
        R("Generated/Hazards/Hazard_Notification.png"),
        R("Generated/Hazards/Fx_Firewall.png", frame: 18, border: new Vector4(1, 0, 1, 0)),
        R("Generated/Hazards/Firewall_Emitter.png"),
        // ウィンドウ・ボタン
        R("Generated/UI/Win_Frame.png", border: new Vector4(6, 6, 6, 6)),
        R("Generated/UI/Btn_Close.png"),
        R("Generated/UI/Ui_Button.png", border: new Vector4(4, 4, 4, 4)),
        R("Generated/UI/Ui_ButtonHover.png", border: new Vector4(4, 4, 4, 4)),
        // 攻撃
        R("Generated/Attacks/Fx_TelegraphFrame.png", border: new Vector4(3, 3, 3, 3)),
        R("Generated/Attacks/Fx_Cursor.png", border: new Vector4(0, 1, 0, 1)),
        R("Generated/Attacks/Fx_Pillar.png", border: new Vector4(4, 0, 4, 0)),
        R("Generated/Attacks/Fx_PillarNeutral.png", border: new Vector4(4, 0, 4, 0), readable: true), // その場で作る能力で、アプリの色に塗り替える
        R("Generated/Attacks/Fx_RedWave.png", border: new Vector4(1, 0, 1, 0)),
        R("Generated/Attacks/Fx_BreakpointMark.png"),
        // カテゴリの能力（2026-11-17：ゲームの流れ）
        // その場で作る能力（2026-11-17）で、アプリの色に塗り替えるので、読み書きできる設定にする
        R("Generated/Attacks/Bullet_Bomb.png", readable: true),
        R("Generated/Attacks/Fx_Blast.png", readable: true),
        R("Generated/Attacks/Fx_Paint.png", border: new Vector4(2, 2, 2, 4), readable: true),
        R("Generated/Attacks/Fx_Bar.png", border: new Vector4(2, 0, 2, 0), readable: true),
        R("Generated/Attacks/Bullet_Orb.png", readable: true),
        R("Generated/Attacks/Fx_Shadow.png"),
        R("Generated/Attacks/Fx_Alert.png"),
        R("Generated/Attacks/Popup_Ad.png"),
        R("Generated/Attacks/Window_Error.png"),
        R("Generated/Attacks/Tab.png"),
        R("Generated/Attacks/Bullet_Tab.png"),
        R("Generated/Attacks/Bullet_Error.png"),
        R("Generated/Attacks/Bullet_Virus.png"),
        // ボス
        R("Generated/Bosses/Content_Browser.png"),
        R("Generated/Bosses/Content_Editor.png"),
        R("Generated/Bosses/Icon_Browser.png"),
        R("Generated/Bosses/Icon_Editor.png"),
        // ボス B（ウイルスがアプリを着ている）
        R("Generated/Bosses/BossB_Body_Float.png", frame: 92),
        R("Generated/Bosses/BossB_Body_Stand.png", frame: 92),
        R("Generated/Bosses/BossB_Hands.png"),
        R("Generated/Bosses/BossB_Face.png", frame: 30),
        R("Generated/Bosses/BossB_Window_Browser.png", frame: 48),
        R("Generated/Bosses/BossB_Window_Editor.png", frame: 48),
        R("Generated/Enemies/Virus_Fleeing.png", frame: 20, pivot: Feet),
        // 雑魚
        R("Generated/Enemies/Zako_Walker.png", frame: 16, pivot: Feet),
        R("Generated/Enemies/Zako_Shooter.png", frame: 16, pivot: Feet),
        // エフェクト
        R("Generated/Effects/Fx_VirusSmokeSmall.png", frame: 25),
        R("Generated/Effects/Fx_Shards.png", frame: 20),
        R("Generated/Effects/Fx_Sparkle.png", frame: 9),
        R("Generated/Player/Fx_Slash.png", frame: 16),
        R("Generated/Player/Fx_HitSpark.png", frame: 11),
        R("Generated/Player/Fx_TabOpen.png", frame: 18, readable: true),
        R("Generated/Player/Player_Idle.png", frame: 13, pivot: Feet),
        // 画面表示
        R("Generated/UI/Hud_Heart.png"),
        R("Generated/UI/Hud_HeartEmpty.png"),
        R("Generated/UI/Hud_Slot.png"),
        R("Generated/UI/Hud_SlotSelected.png"),
        R("Generated/UI/Hud_Digit1.png"),
        R("Generated/UI/Hud_Digit2.png"),
        R("Generated/UI/Hud_Digit3.png"),
        R("Generated/UI/Hud_Pixel.png"),
        R("Generated/UI/Hud_GaugeFrame.png", border: new Vector4(3, 3, 3, 3)),
        R("Generated/UI/Hud_GaugeFill.png"),
        R("Generated/UI/Icon_NewTab.png"),
        R("Generated/UI/Icon_Breakpoint.png"),
        R("Generated/UI/Icon_BarGraph.png"),
        R("Generated/UI/Icon_Paint.png"),
        R("Generated/UI/Icon_Bomb.png"),
        R("Generated/UI/Icon_GalleryView.png"),
    };

    [MenuItem("Tools/Team1/グラフィックを適用")]
    public static void ApplyFromMenu()
    {
        Apply();
        Debug.Log("グラフィックを適用しました（取り込み設定・GameArt・攻撃データ・能力・ボス・プレイヤーのアニメーション）");
    }

    /// <summary>コマンドラインから実行する用（-executeMethod ArtAssets.Apply）。シーンの作成ツールからも呼ぶ</summary>
    public static void Apply()
    {
        ApplyImportSettings();
        BuildGameArt();
        ApplyToData();
        GetOrCreatePlayerController();
        AssetDatabase.SaveAssets();
    }

    // ───────── 取り込み設定 ─────────

#pragma warning disable 618 // spritesheet は古い書き方だが、2D Sprite パッケージなしでコマ分けできる
    const string FontJaPath = "Assets/Fonts/DotGothic16-Regular.ttf";
    const string FontEnPath = "Assets/Fonts/PressStart2P-Regular.ttf";

    /// <summary>ドットのフォントは、ぼかさずに描く（アンチエイリアスなし）。拡大はゲームの中で点のまま行う</summary>
    static void ApplyFontSettings()
    {
        foreach (string path in new[] { FontJaPath, FontEnPath })
        {
            if (!File.Exists(path)) continue;
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TrueTypeFontImporter fi)) continue;
            if (fi.fontRenderingMode == FontRenderingMode.HintedRaster && fi.fontTextureCase == FontTextureCase.Dynamic) continue;
            fi.fontRenderingMode = FontRenderingMode.HintedRaster;
            fi.fontTextureCase = FontTextureCase.Dynamic;
            fi.SaveAndReimport();
        }
    }

    static void ApplyImportSettings()
    {
        ApplyFontSettings();
        foreach (Rule rule in Rules)
        {
            string path = $"{SpriteRoot}/{rule.Path}";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"絵が見つかりません: {path}（Tools/art/gen_sprites.py で作れます）");
                continue;
            }
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ReadPngSize(path, out int width, out int height);
            if (IsUpToDate(ti, rule, width)) continue; // 設定が同じなら取り込み直さない（ほかのアセットの読み込み直しを起こさない）

            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = rule.Ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.isReadable = rule.Readable;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 9スライス・並べ表示に必要
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = rule.Pivot;
            settings.spriteBorder = rule.Border;
            settings.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(settings);

            string baseName = Path.GetFileNameWithoutExtension(path);
            if (rule.Frame > 0 && width > rule.Frame)
            {
                ti.spriteImportMode = SpriteImportMode.Multiple;
                int count = width / rule.Frame;
                var metas = new SpriteMetaData[count];
                for (int i = 0; i < count; i++)
                {
                    metas[i] = new SpriteMetaData
                    {
                        name = $"{baseName}_{i}",
                        rect = new Rect(i * rule.Frame, 0, rule.Frame, height),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = rule.Pivot,
                        border = rule.Border,
                    };
                }
                ti.spritesheet = metas;
            }
            else
            {
                ti.spriteImportMode = SpriteImportMode.Single;
            }
            ti.SaveAndReimport();
        }
    }
#pragma warning restore 618

    static bool IsUpToDate(TextureImporter ti, Rule rule, int width)
    {
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        bool multiple = rule.Frame > 0 && width > rule.Frame;
        if (ti.textureType != TextureImporterType.Sprite || !Mathf.Approximately(ti.spritePixelsPerUnit, rule.Ppu) ||
            ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled ||
            ti.isReadable != rule.Readable ||
            settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteAlignment != (int)SpriteAlignment.Custom ||
            settings.spritePivot != rule.Pivot || settings.spriteBorder != rule.Border ||
            ti.spriteImportMode != (multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single))
        {
            return false;
        }
        if (!multiple) return true;
        var sheet = ti.spritesheet;
        if (sheet == null || sheet.Length != width / rule.Frame) return false;
        for (int i = 0; i < sheet.Length; i++)
        {
            if ((int)sheet[i].rect.x != i * rule.Frame || (int)sheet[i].rect.width != rule.Frame || sheet[i].pivot != rule.Pivot || sheet[i].border != rule.Border) return false;
        }
        return true;
    }

    static void ReadPngSize(string path, out int width, out int height)
    {
        byte[] b = new byte[24];
        using (var fs = File.OpenRead(path)) fs.Read(b, 0, 24);
        width = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        height = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
    }

    // ───────── 読み込み ─────────

    /// <summary>1枚絵を読む（Assets/Sprites からの相対パス）</summary>
    public static Sprite LoadSprite(string relPath)
    {
        string path = $"{SpriteRoot}/{relPath}";
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (s == null) Debug.LogWarning($"スプライトが見つかりません: {path}");
        return s;
    }

    /// <summary>コマ分けした絵を、コマの順に読む</summary>
    public static Sprite[] LoadFrames(string relPath)
    {
        string path = $"{SpriteRoot}/{relPath}";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        sprites.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
        if (sprites.Count == 0) Debug.LogWarning($"コマが見つかりません: {path}");
        return sprites.ToArray();
    }

    static int FrameIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    // ───────── GameArt ─────────

    static void BuildGameArt()
    {
        PlayerTestSceneBuilder.EnsureFolder("Assets/Resources");
        var art = AssetDatabase.LoadAssetAtPath<GameArt>(GameArtPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<GameArt>();
            AssetDatabase.CreateAsset(art, GameArtPath);
        }
        PlayerTestSceneBuilder.SetProps(art, so =>
        {
            Set(so, "_telegraphFrame", LoadSprite("Generated/Attacks/Fx_TelegraphFrame.png"));
            Set(so, "_shadow", LoadSprite("Generated/Attacks/Fx_Shadow.png"));
            Set(so, "_breakpointMark", LoadSprite("Generated/Attacks/Fx_BreakpointMark.png"));
            Set(so, "_warpCursor", LoadSprite("Generated/Attacks/Fx_Cursor.png"));
            SetArray(so, "_smoke", LoadFrames("Provided/Fx_VirusSmoke.png"));
            SetArray(so, "_smokeSmall", LoadFrames("Generated/Effects/Fx_VirusSmokeSmall.png"));
            SetArray(so, "_hitSpark", LoadFrames("Generated/Player/Fx_HitSpark.png"));
            SetArray(so, "_tabOpen", LoadFrames("Generated/Player/Fx_TabOpen.png"));
            SetArray(so, "_shards", LoadFrames("Generated/Effects/Fx_Shards.png"));
            SetArray(so, "_sparkle", LoadFrames("Generated/Effects/Fx_Sparkle.png"));
            Set(so, "_heart", LoadSprite("Generated/UI/Hud_Heart.png"));
            Set(so, "_heartEmpty", LoadSprite("Generated/UI/Hud_HeartEmpty.png"));
            Set(so, "_slot", LoadSprite("Generated/UI/Hud_Slot.png"));
            Set(so, "_slotSelected", LoadSprite("Generated/UI/Hud_SlotSelected.png"));
            SetArray(so, "_digits", new[]
            {
                LoadSprite("Generated/UI/Hud_Digit1.png"), LoadSprite("Generated/UI/Hud_Digit2.png"), LoadSprite("Generated/UI/Hud_Digit3.png"),
            });
            Set(so, "_pixel", LoadSprite("Generated/UI/Hud_Pixel.png"));
            Set(so, "_gaugeFrame", LoadSprite("Generated/UI/Hud_GaugeFrame.png"));
            Set(so, "_gaugeFill", LoadSprite("Generated/UI/Hud_GaugeFill.png"));
            Set(so, "_background", LoadSprite("Generated/Background/Bg_Desktop.png"));
            Set(so, "_windowFrame", LoadSprite("Generated/UI/Win_Frame.png"));
            Set(so, "_button", LoadSprite("Generated/UI/Ui_Button.png"));
            Set(so, "_buttonHover", LoadSprite("Generated/UI/Ui_ButtonHover.png"));
            SetArray(so, "_playerWalk", LoadFrames("Provided/Player_Walk.png"));
            SetArray(so, "_zakoWalk", LoadFrames("Generated/Enemies/Zako_Walker.png"));
            Set(so, "_fontJa", AssetDatabase.LoadAssetAtPath<Font>(FontJaPath));
            Set(so, "_fontEn", AssetDatabase.LoadAssetAtPath<Font>(FontEnPath));
        });
        EditorUtility.SetDirty(art);
    }

    static void Set(SerializedObject so, string prop, UnityEngine.Object value) => so.FindProperty(prop).objectReferenceValue = value;

    static void SetArray(SerializedObject so, string prop, IReadOnlyList<UnityEngine.Object> values)
    {
        var arr = so.FindProperty(prop);
        arr.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ───────── 攻撃データ・能力・ボス ─────────

    const string AttackFolder = "Assets/ScriptableObjects/Attacks";

    /// <summary>攻撃データの絵（攻撃データのパス → 絵）。色は白にして、絵の色をそのまま出す</summary>
    static readonly (string attack, string sprite)[] AttackSprites =
    {
        ("Chrome/Attack_Chrome_TabShuriken_P1", "Generated/Attacks/Bullet_Tab.png"),
        ("Chrome/Attack_Chrome_TabShuriken_P2", "Generated/Attacks/Bullet_Tab.png"),
        ("Chrome/Attack_Chrome_PopupAppear_P1", "Generated/Attacks/Popup_Ad.png"),
        ("Chrome/Attack_Chrome_PopupAppear_P2", "Generated/Attacks/Popup_Ad.png"),
        ("Chrome/Attack_Chrome_TabMultiply", "Generated/Attacks/Tab.png"),
        ("VSCode/Attack_VSCode_ErrorBullet_P1", "Generated/Attacks/Bullet_Error.png"),
        ("VSCode/Attack_VSCode_ErrorBullet_P2", "Generated/Attacks/Bullet_Error.png"),
        ("VSCode/Attack_VSCode_RedWave_P1", "Generated/Attacks/Fx_RedWave.png"),
        ("VSCode/Attack_VSCode_RedWave_P2", "Generated/Attacks/Fx_RedWave.png"),
        ("VSCode/Attack_VSCode_Breakpoint_P1", "Generated/Attacks/Fx_Pillar.png"),
        ("VSCode/Attack_VSCode_Breakpoint_P2", "Generated/Attacks/Fx_Pillar.png"),
        ("VSCode/Attack_VSCode_ErrorWindowFall", "Generated/Attacks/Window_Error.png"),
        ("Zako/Attack_Zako_Shot", "Generated/Attacks/Bullet_Virus.png"),
        ("Stage/Attack_Stage_Notification", "Generated/Hazards/Hazard_Notification.png"),
        // 能力のブレークポイントは白い柱に、VS Code の色を掛ける（ApplyToData の中で入れる。敵の柱は赤のまま）
        ("Player/Attack_Ability_Breakpoint", "Generated/Attacks/Fx_PillarNeutral.png"),
        // カテゴリの能力：絵にアプリの色が入っているので、白（絵の色のまま）
        ("Player/Attack_Ability_BarGraph", "Generated/Attacks/Fx_Bar.png"),
        ("Player/Attack_Ability_PaintBucket", "Generated/Attacks/Fx_Paint.png"),
        ("Player/Attack_Ability_Bomb", "Generated/Attacks/Bullet_Bomb.png"),
        ("Player/Attack_Ability_Bomb_Blast", "Generated/Attacks/Fx_Blast.png"),
        // その場で作る能力のひな形（ゲームの中で、アプリの色に塗り替える）
        ("Player/Gen/Attack_Gen_Spread", "Generated/Attacks/Bullet_Orb.png"),
        ("Player/Gen/Attack_Gen_Trap", "Generated/Attacks/Bullet_Orb.png"),
        ("Player/Gen/Attack_Gen_TrapFloor", "Generated/Attacks/Fx_Paint.png"),
        ("Player/Gen/Attack_Gen_Nova", "Generated/Attacks/Fx_Blast.png"),
    };

    static void ApplyToData()
    {
        AppTheme chrome = GetOrCreateTheme("Theme_Chrome", ChromeTheme);
        AppTheme vscode = GetOrCreateTheme("Theme_VSCode", VSCodeTheme);
        foreach (var (attack, sprite) in AttackSprites)
        {
            var data = AssetDatabase.LoadAssetAtPath<AttackData>($"{AttackFolder}/{attack}.asset");
            if (data == null) continue; // まだ作られていない（シーンの作成ツールが作ったあとに、もう一度呼ばれる）
            Sprite s = LoadSprite(sprite);
            // 能力の色＝手に入れたアプリの色（敵の攻撃は白＝絵の色のまま）
            Color color = attack == "Player/Attack_Ability_Breakpoint" && vscode != null ? vscode.Main : Color.white;
            if (data.Sprite == s && data.Color == color) continue; // 同じなら書き換えない
            PlayerTestSceneBuilder.SetProps(data, so =>
            {
                so.FindProperty("_sprite").objectReferenceValue = s;
                so.FindProperty("_color").colorValue = color;
            });
            EditorUtility.SetDirty(data);
        }
        SetRef<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_NewTab.asset", "_theme", chrome);
        SetRef<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Breakpoint.asset", "_theme", vscode);
        ApplyThemesToCatalog(chrome, vscode);
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_NewTab.asset", "Generated/UI/Icon_NewTab.png");
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Breakpoint.asset", "Generated/UI/Icon_Breakpoint.png");
        // カテゴリの能力の色とアイコン（Office＝緑、クリエイティブ＝紫、ゲーム＝橙）
        SetRef<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_BarGraph.asset", "_theme", GetOrCreateTheme("Theme_Office", OfficeTheme));
        SetRef<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_PaintBucket.asset", "_theme", GetOrCreateTheme("Theme_Creative", CreativeTheme));
        SetRef<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Bomb.asset", "_theme", GetOrCreateTheme("Theme_Game", GameTheme));
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_BarGraph.asset", "Generated/UI/Icon_BarGraph.png");
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_PaintBucket.asset", "Generated/UI/Icon_Paint.png");
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_Bomb.asset", "Generated/UI/Icon_Bomb.png");
        // アプリ専用の能力（2026-11-18）：Zoom のギャラリービュー
        SetIcon<SkillDefinition>("Assets/ScriptableObjects/Skills/Skill_App_Zoom.asset", "Generated/UI/Icon_GalleryView.png");
        SetIcon<BossProfile>("Assets/ScriptableObjects/Bosses/Boss_Chrome.asset", "Generated/Bosses/Icon_Browser.png");
        SetIcon<BossProfile>("Assets/ScriptableObjects/Bosses/Boss_VSCode.asset", "Generated/Bosses/Icon_Editor.png");
    }

    static void SetRef<T>(string assetPath, string prop, UnityEngine.Object value) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null) return;
        var so0 = new SerializedObject(asset);
        if (so0.FindProperty(prop).objectReferenceValue == value) return;
        PlayerTestSceneBuilder.SetProps(asset, so => so.FindProperty(prop).objectReferenceValue = value);
        EditorUtility.SetDirty(asset);
    }

    // ───────── アプリの色（トンマナ） ─────────

    const string ThemeFolder = "Assets/ScriptableObjects/Themes";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";

    /// <summary>対応アプリ表の各アプリに、色を入れる（名前で探す。ランのシーンの作成ツールが表を作ったあとにも呼ぶ）</summary>
    public static void ApplyThemesToCatalog() => ApplyThemesToCatalog(
        AssetDatabase.LoadAssetAtPath<AppTheme>($"{ThemeFolder}/Theme_Chrome.asset"),
        AssetDatabase.LoadAssetAtPath<AppTheme>($"{ThemeFolder}/Theme_VSCode.asset"));

    static void ApplyThemesToCatalog(AppTheme chrome, AppTheme vscode)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        if (catalog == null) return;
        var so = new SerializedObject(catalog);
        var apps = so.FindProperty("_apps");
        bool changed = false;
        for (int i = 0; i < apps.arraySize; i++)
        {
            var e = apps.GetArrayElementAtIndex(i);
            string name = e.FindPropertyRelative("DisplayName").stringValue;
            AppTheme t = name == "Chrome" ? chrome : name == "VS Code" ? vscode : null;
            var prop = e.FindPropertyRelative("Theme");
            if (t == null || prop.objectReferenceValue == t) continue;
            prop.objectReferenceValue = t;
            changed = true;
        }
        if (!changed) return;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    /// <summary>アプリの色のアセットを作る。すでにあれば、それを使う（色は上書きしない。直すときはアセットを直接直す）</summary>
    /// <summary>ラスボス（Terminal）の色：黒い画面と緑の文字（2026-11-18。【仮】）</summary>
    public static AppTheme GetOrCreateTerminalTheme() => GetOrCreateTheme("Theme_Terminal", TerminalTheme);

    static void TerminalTheme(SerializedObject so)
    {
        Col(so, "_sky", "0b0f0c"); Col(so, "_cloud", "16261a");
        Colors(so, "_hills", "020d05", "04200c", "0a4a1c", "128a34", "33ff66");
        Col(so, "_floor", "262626"); Col(so, "_floorTop", "33ff66");
        Col(so, "_wall", "121212"); Col(so, "_wallHighlight", "262626");
        Col(so, "_folder", "128a34"); Col(so, "_folderShade", "0a4a1c"); Col(so, "_folderTab", "33ff66");
        Col(so, "_main", "33ff66");
        Colors(so, "_accents", "33ff66", "128a34");
    }

    static AppTheme GetOrCreateTheme(string file, Action<SerializedObject> init)
    {
        string path = $"{ThemeFolder}/{file}.asset";
        var theme = AssetDatabase.LoadAssetAtPath<AppTheme>(path);
        if (theme != null) return theme;
        PlayerTestSceneBuilder.EnsureFolder(ThemeFolder);
        theme = ScriptableObject.CreateInstance<AppTheme>();
        AssetDatabase.CreateAsset(theme, path);
        PlayerTestSceneBuilder.SetProps(theme, init);
        EditorUtility.SetDirty(theme);
        return theme;
    }

    static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out Color c) ? c : Color.magenta;

    static void Colors(SerializedObject so, string prop, params string[] hex)
    {
        var arr = so.FindProperty(prop);
        arr.arraySize = hex.Length;
        for (int i = 0; i < hex.Length; i++) arr.GetArrayElementAtIndex(i).colorValue = Hex(hex[i]);
    }

    static void Col(SerializedObject so, string prop, string hex) => so.FindProperty(prop).colorValue = Hex(hex);

    // Chrome：アイコンの4色（赤・黄・緑・青）。昼の空と緑の丘に、赤と黄の差し色
    static void ChromeTheme(SerializedObject so)
    {
        Col(so, "_sky", "4f93f0"); Col(so, "_cloud", "ffffff");
        Colors(so, "_hills", "0f4a22", "1a7334", "2e9e4a", "3fbf55", "6edc5e");
        Col(so, "_floor", "8a8f99"); Col(so, "_floorTop", "ea4335");
        Col(so, "_wall", "5f6368"); Col(so, "_wallHighlight", "80868b");
        Col(so, "_folder", "fbbc05"); Col(so, "_folderShade", "c99400"); Col(so, "_folderTab", "ea4335");
        Col(so, "_main", "4285f4");
        Colors(so, "_accents", "ea4335", "fbbc05", "34a853", "4285f4");
    }

    // VS Code：アイコンの青と、エディタの暗い色。夜の空に青の丘
    static void VSCodeTheme(SerializedObject so)
    {
        Col(so, "_sky", "161b26"); Col(so, "_cloud", "2b3a52");
        Colors(so, "_hills", "06223f", "0b3d6e", "0065a9", "007acc", "3ea6ff");
        Col(so, "_floor", "3c3c3c"); Col(so, "_floorTop", "007acc");
        Col(so, "_wall", "252526"); Col(so, "_wallHighlight", "3c3c3c");
        Col(so, "_folder", "007acc"); Col(so, "_folderShade", "0065a9"); Col(so, "_folderTab", "3ea6ff");
        Col(so, "_main", "007acc");
        Colors(so, "_accents", "007acc");
    }

    // Office・仕事：Excel の緑。差し色は Word の青・PowerPoint の橙
    static void OfficeTheme(SerializedObject so)
    {
        Col(so, "_sky", "cfe8d6"); Col(so, "_cloud", "ffffff");
        Colors(so, "_hills", "0e3b22", "15502e", "217346", "33a565", "7fd19b");
        Col(so, "_floor", "8a8f99"); Col(so, "_floorTop", "217346");
        Col(so, "_wall", "4a5a50"); Col(so, "_wallHighlight", "6b7d72");
        Col(so, "_folder", "33a565"); Col(so, "_folderShade", "217346"); Col(so, "_folderTab", "185c37");
        Col(so, "_main", "217346");
        Colors(so, "_accents", "217346", "2b579a", "d24726", "33a565");
    }

    // クリエイティブ：Figma の5色。夕方の紫の空
    static void CreativeTheme(SerializedObject so)
    {
        Col(so, "_sky", "2c1a4d"); Col(so, "_cloud", "4b2f7a");
        Colors(so, "_hills", "1a0f33", "32206b", "5b3bb0", "a259ff", "c99bff");
        Col(so, "_floor", "3a3a44"); Col(so, "_floorTop", "a259ff");
        Col(so, "_wall", "2a2333"); Col(so, "_wallHighlight", "463a55");
        Col(so, "_folder", "f24e1e"); Col(so, "_folderShade", "b8360f"); Col(so, "_folderTab", "ff7262");
        Col(so, "_main", "a259ff");
        Colors(so, "_accents", "f24e1e", "ff7262", "a259ff", "1abcfe", "0acf83");
    }

    // ゲーム：ゲームランチャーの暗い青に、橙の差し色
    static void GameTheme(SerializedObject so)
    {
        Col(so, "_sky", "1b2838"); Col(so, "_cloud", "2a475e");
        Colors(so, "_hills", "0f1a24", "1b2838", "2a475e", "66c0f4", "c7d5e0");
        Col(so, "_floor", "3d4450"); Col(so, "_floorTop", "e4572e");
        Col(so, "_wall", "171a21"); Col(so, "_wallHighlight", "2a3140");
        Col(so, "_folder", "e4572e"); Col(so, "_folderShade", "a93a1b"); Col(so, "_folderTab", "ffb000");
        Col(so, "_main", "e4572e");
        Colors(so, "_accents", "e4572e", "ffb000", "66c0f4");
    }

    static void SetIcon<T>(string assetPath, string sprite) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null) return;
        Sprite s = LoadSprite(sprite);
        var so0 = new SerializedObject(asset);
        if (so0.FindProperty("_icon").objectReferenceValue == s) return; // 同じなら書き換えない
        PlayerTestSceneBuilder.SetProps(asset, so => so.FindProperty("_icon").objectReferenceValue = s);
        EditorUtility.SetDirty(asset);
    }

    // ───────── プレイヤーのアニメーション ─────────

    /// <summary>
    /// プレイヤーの Animator Controller（待機・歩き・上昇・落下）。パラメータは PlayerAnimationBridge が入れる。
    /// すでにあれば、それを使う（デザイン担当が編集した物を上書きしない）
    /// </summary>
    public static RuntimeAnimatorController GetOrCreatePlayerController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (existing != null) return existing;

        PlayerTestSceneBuilder.EnsureFolder(AnimFolder);
        Sprite[] walk = LoadFrames("Provided/Player_Walk.png");
        Sprite[] idle = LoadFrames("Generated/Player/Player_Idle.png");
        if (walk.Length < 5 || idle.Length < 2) return null;

        AnimationClip idleClip = Clip("Player_Idle", idle, 3f, true);
        AnimationClip walkClip = Clip("Player_Walk", walk, 11f, true);
        AnimationClip jumpClip = Clip("Player_Jump", new[] { walk[4] }, 1f, false);
        AnimationClip fallClip = Clip("Player_Fall", new[] { walk[1] }, 1f, false);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
        ctrl.AddParameter("SpeedX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300, 0));
        AnimatorState sWalk = sm.AddState("Walk", new Vector3(300, 100));
        AnimatorState sJump = sm.AddState("Jump", new Vector3(560, 0));
        AnimatorState sFall = sm.AddState("Fall", new Vector3(560, 100));
        sIdle.motion = idleClip;
        sWalk.motion = walkClip;
        sJump.motion = jumpClip;
        sFall.motion = fallClip;
        sm.defaultState = sIdle;

        Link(sIdle, sWalk, ("SpeedX", AnimatorConditionMode.Greater, 0.2f), ("IsGrounded", AnimatorConditionMode.If, 0f));
        Link(sWalk, sIdle, ("SpeedX", AnimatorConditionMode.Less, 0.2f));
        foreach (AnimatorState ground in new[] { sIdle, sWalk })
        {
            Link(ground, sJump, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VelocityY", AnimatorConditionMode.Greater, 0.1f));
            Link(ground, sFall, ("IsGrounded", AnimatorConditionMode.IfNot, 0f), ("VelocityY", AnimatorConditionMode.Less, 0.1f));
        }
        Link(sJump, sFall, ("VelocityY", AnimatorConditionMode.Less, 0.1f));
        Link(sJump, sIdle, ("IsGrounded", AnimatorConditionMode.If, 0f));
        Link(sFall, sIdle, ("IsGrounded", AnimatorConditionMode.If, 0f));
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void Link(AnimatorState from, AnimatorState to, params (string param, AnimatorConditionMode mode, float value)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        foreach (var c in conditions) t.AddCondition(c.mode, c.value, c.param);
    }

    static AnimationClip Clip(string name, Sprite[] frames, float fps, bool loop)
    {
        string path = $"{AnimFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + (loop ? 1 : 0)];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        if (loop) keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
