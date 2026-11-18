using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Office・Unity・Steam のスキルとアプリボスを用意するエディタ拡張。
/// ・「Tools/Team1/Office・Unity・Steam の素材を取り込む」：Assets/_Project/Art/VirusGameArt/VirusGameArt の画像に、
///   README のとおりの取り込み設定を入れる（PPU 54＝プレイヤーと同じ密度・Point・圧縮なし・ミップマップなし・セルでコマ分け・Pivot）
/// ・「Tools/Team1/Office・Unity・Steam テストシーンを作成」：素材を取り込み、アセット（攻撃データ・スキル・実績・ボスの設定）を作り、
///   ボスのプレハブを作って対応アプリ表に登録し、確認用のシーン（Scenes/Test/OfficeUnitySteamTest）を作る
/// アセットは「無ければ作る」。すでにあれば数値は上書きしない（調整した値を消さないため）。数値を初期値に戻すときはアセットを消してから実行する。
/// 数値は依頼書の初期値。ダメージ・ゲージの量は【仮】（ゲージは「クールタイム × 毎秒4」の目安に合わせた）。
/// </summary>
public static class OfficeUnitySteamBuilder
{
    public const string ArtRoot = "Assets/_Project/Art/VirusGameArt/VirusGameArt";
    const float Ppu = 54f; // プレイヤーの元素材（Player_Walk）と同じ。素材は 18 ドット/マスを3倍で書き出している
    const string ScenePath = "Assets/Scenes/Test/OfficeUnitySteamTest.unity";
    const string SkillFolder = "Assets/ScriptableObjects/Skills";
    const string AchievementFolder = "Assets/ScriptableObjects/Achievements";
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string PrefabFolder = "Assets/Prefabs/Bosses";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    static readonly Vector2 Left = new Vector2(0f, 0.5f);

    /// <summary>ボスの体の中心の、床からの高さ（素材 168 ドット＝3.1マス。オーラの下の余白ぶん少し下げる）</summary>
    const float BossCenterY = 1.45f;
    /// <summary>ボスの被弾範囲（本体 96 ドット＝1.8マス）</summary>
    static readonly Vector2 BossBodySize = new Vector2(1.9f, 1.9f);
    /// <summary>ボスの出る位置（部屋の床の真ん中が (0, 0)）</summary>
    static readonly Vector2 BossSpawn = new Vector2(6f, BossCenterY);

    // ───────── 素材の取り込み ─────────

    struct Cell
    {
        public string Path;
        public int W, H;
        public Vector2 Pivot;
        public Cell(string path, int w, int h, Vector2 pivot) { Path = path; W = w; H = h; Pivot = pivot; }
    }

    static IEnumerable<Cell> Cells()
    {
        foreach (string v in new[] { "", "_Virus" })
        {
            yield return new Cell($"SkillEffects/Office/Office_Bar{v}.png", 36, 126, Bottom);
            yield return new Cell($"SkillEffects/Office/Office_Pie{v}.png", 78, 78, Center);
            yield return new Cell($"SkillEffects/Office/Office_PieSlice{v}.png", 36, 36, Center);
            yield return new Cell($"SkillEffects/Office/Office_Line{v}.png", 228, 90, Left);
            foreach (string shape in new[] { "Cube", "Sphere", "Capsule" })
                yield return new Cell($"SkillEffects/Unity/Unity_{shape}{v}.png", 60, 60, Center);
            yield return new Cell($"SkillEffects/Unity/Unity_Spawn{v}.png", 90, 90, Center);
            yield return new Cell($"SkillEffects/Unity/Unity_Impact{v}.png", 108, 42, Bottom);
            yield return new Cell($"SkillEffects/Steam/Steam_Trophy{v}.png", 48, 48, Center);
            yield return new Cell($"SkillEffects/Steam/Steam_ToastBG{v}.png", 252, 66, Center);
            yield return new Cell($"SkillEffects/Steam/Steam_BuffBurst{v}.png", 132, 132, Center);
            yield return new Cell($"SkillEffects/Steam/Steam_BuffAura{v}.png", 78, 108, Bottom);
        }
        yield return new Cell("SkillEffects/Unity/Unity_Primitives.png", 60, 60, Center);
        yield return new Cell("SkillEffects/Unity/Unity_SpawnWarning_Virus.png", 90, 90, Center);
        foreach (string app in new[] { "Office", "Unity", "Steam" })
        {
            foreach (string anim in new[] { "Idle", "Attack", "Hit", "Purify" })
            {
                yield return new Cell($"AppBoss/{app}/AppBoss_{app}_{anim}.png", 168, 168, Center);
                yield return new Cell($"AppBoss/{app}/Body/AppBoss_{app}_{anim}_Body.png", 96, 96, Center);
            }
            yield return new Cell($"AppBoss/{app}/AppIcon_{app}_Normal.png", 96, 96, Center);
        }
    }

    [MenuItem("Tools/Team1/Office・Unity・Steam の素材を取り込む")]
    public static void ImportArt()
    {
        if (!AssetDatabase.IsValidFolder(ArtRoot))
        {
            Debug.LogError($"素材のフォルダが見つかりません: {ArtRoot}");
            return;
        }
        int changed = 0;
        foreach (Cell cell in Cells())
        {
            if (ApplyImport(cell)) changed++;
        }
        Debug.Log($"Office・Unity・Steam の素材の取り込み設定を確認しました（設定し直した画像：{changed}）");
    }

#pragma warning disable 618 // spritesheet は古い書き方だが、2D Sprite パッケージなしでコマ分けできる（ArtAssets と同じ）
    /// <summary>1枚分の取り込み設定を入れる。セルで左上から右へ、上の行から順に「名前_番号」で切る。設定を変えたら true</summary>
    static bool ApplyImport(Cell cell)
    {
        string path = $"{ArtRoot}/{cell.Path}";
        if (!File.Exists(path))
        {
            Debug.LogWarning($"素材が見つかりません: {path}");
            return false;
        }
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return false;
        ReadPngSize(path, out int width, out int height);
        int cols = Mathf.Max(1, width / cell.W), rows = Mathf.Max(1, height / cell.H);
        string baseName = Path.GetFileNameWithoutExtension(path);

        var metas = new SpriteMetaData[cols * rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                metas[r * cols + c] = new SpriteMetaData
                {
                    name = $"{baseName}_{r * cols + c}",
                    rect = new Rect(c * cell.W, height - (r + 1) * cell.H, cell.W, cell.H), // テクスチャの y は下から
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = cell.Pivot,
                };
            }
        }
        if (IsUpToDate(ti, metas)) return false;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 4096;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        ti.SetTextureSettings(settings);
        ti.spritesheet = metas;
        ti.SaveAndReimport();
        return true;
    }

    static bool IsUpToDate(TextureImporter ti, SpriteMetaData[] metas)
    {
        if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(ti.spritePixelsPerUnit, Ppu) || ti.filterMode != FilterMode.Point ||
            ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled) return false;
        var sheet = ti.spritesheet;
        if (sheet == null || sheet.Length != metas.Length) return false;
        for (int i = 0; i < sheet.Length; i++)
        {
            if (sheet[i].name != metas[i].name || sheet[i].rect != metas[i].rect || sheet[i].pivot != metas[i].pivot) return false;
        }
        return true;
    }
#pragma warning restore 618

    static void ReadPngSize(string path, out int width, out int height)
    {
        byte[] b = new byte[24];
        using (var fs = File.OpenRead(path)) fs.Read(b, 0, 24);
        width = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        height = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
    }

    /// <summary>コマ分けした素材を、番号の順に読む（ArtRoot からの相対パス）</summary>
    public static Sprite[] Frames(string rel)
    {
        string path = $"{ArtRoot}/{rel}";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        sprites.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
        if (sprites.Count == 0) Debug.LogWarning($"コマが見つかりません: {path}（先に「素材を取り込む」を実行してください）");
        return sprites.ToArray();
    }

    public static Sprite First(string rel)
    {
        Sprite[] f = Frames(rel);
        return f.Length > 0 ? f[0] : null;
    }

    static int FrameIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    // ───────── アセット ─────────

    public class Assets
    {
        public AttackData OfficeBar, OfficePie, OfficeLine;
        public AttackData OfficeBossBar, OfficeBossPie, OfficeBossLine;
        public AttackData UnitySpawn, UnityBossSpawnP1, UnityBossSpawnP2;
        public AttackData SteamBossTrophy, SteamBossBurst;
        public AchievementPassive SteamPassive, SteamBossPassive;
        public SkillDefinition SkillOffice, SkillUnity, SkillSteam;
        public BossProfile OfficeProfile, UnityProfile, SteamProfile;
        public SkillDefinition[] Skills => new[] { SkillOffice, SkillUnity, SkillSteam };
    }

    /// <summary>折れ線の点（README：3倍後の座標、セル 228×90 の左上が原点）→ 判定の中心からのマス</summary>
    static Vector2[] LinePoints()
    {
        var px = new[] { new Vector2(12, 72), new Vector2(51, 39), new Vector2(90, 54), new Vector2(129, 18), new Vector2(168, 42), new Vector2(210, 9) };
        return px.Select(p => new Vector2((p.x - 228f * 0.5f) / Ppu, (90f * 0.5f - p.y) / Ppu)).ToArray();
    }

    const float OfficeFps = 12f;

    public static Assets GetOrCreateAssets()
    {
        var a = new Assets();
        string O = "SkillEffects/Office/", U = "SkillEffects/Unity/", S = "SkillEffects/Steam/";

        // ───── Office（プレイヤー版） ─────
        // 棒グラフ：前方に4本を 0.1 秒ずつ（青→橙→灰→黄）。F2〜F5 で当たり、打ち上げる
        a.OfficeBar = Attack("Office/Attack_Office_Bar", so =>
        {
            Common(so, AttackKind.Area, cooldown: 3f, damage: 1, gauge: 3, knockback: new Vector2(1.5f, 9f), hitStun: 0.3f);
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 1.1f);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Box);
            V(so, "_areaSize", new Vector2(36f / Ppu, 126f / Ppu));
            I(so, "_areaCount", 4);
            F(so, "_areaSpacing", 0.85f);
            F(so, "_areaStagger", 0.1f);
            B(so, "_areaForwardOrder", true);
            F(so, "_telegraphTime", 0f);
            F(so, "_hitDelay", 1f / OfficeFps);    // F2 から
            F(so, "_activeTime", 4f / OfficeFps);  // F2〜F5
            Anim(so, Frames(O + "Office_Bar.png"), 10, OfficeFps, false);
        });
        // 円グラフ：前方やや上に出て、F6 で PieSlice を4つ前方へ扇状に撃つ
        a.OfficePie = Attack("Office/Attack_Office_Pie", so =>
        {
            Common(so, AttackKind.Projectile, cooldown: 3f, damage: 1, gauge: 3, knockback: new Vector2(3f, 3f), hitStun: 0.15f);
            F(so, "_windup", 5f / OfficeFps); // F6 で発射
            SetSprites(so, "_windupFrames", Frames(O + "Office_Pie.png"));
            F(so, "_windupFrameRate", OfficeFps);
            I(so, "_projectileCount", 4);
            F(so, "_spreadAngle", 50f);
            E(so, "_aim", (int)AimMode.Forward);
            V(so, "_spawnOffset", new Vector2(1.0f, 1.4f));
            F(so, "_speed", 9f);
            F(so, "_gravity", 0f);
            F(so, "_lifetime", 1.4f);
            V(so, "_hitSize", new Vector2(0.55f, 0.55f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
            Anim(so, Frames(O + "Office_PieSlice.png"), 4, 16f, true);
        });
        // 折れ線グラフ：前方に出す。線に沿って貫通し、1回の発動で同じ敵には1回だけ（Hitbox の「1回だけ当たる」）
        a.OfficeLine = Attack("Office/Attack_Office_Line", so =>
        {
            Common(so, AttackKind.Area, cooldown: 3f, damage: 1, gauge: 12, knockback: new Vector2(3f, 2f), hitStun: 0.2f);
            Line(so, Frames(O + "Office_Line.png"));
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 228f / Ppu * 0.5f + 0.4f);
            V(so, "_spawnOffset", new Vector2(0f, 0.7f));
            F(so, "_activeTime", 7f / 14f); // F1〜F7
        });

        // ───── Office（ボス版） ─────
        a.OfficeBossBar = Attack("Office/Attack_OfficeBoss_Bar", so =>
        {
            BossCommon(so, AttackKind.Area);
            E(so, "_anchor", (int)AreaAnchor.Forward);
            F(so, "_forwardDistance", 1.8f);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Box);
            V(so, "_areaSize", new Vector2(36f / Ppu, 126f / Ppu));
            I(so, "_areaCount", 7); // 6本以上。プレイヤーの方（前方）へ順に伸びてくる
            F(so, "_areaSpacing", 1.4f);
            F(so, "_areaStagger", 0.12f);
            B(so, "_areaForwardOrder", true);
            F(so, "_telegraphTime", 0f);
            F(so, "_hitDelay", 1f / OfficeFps);
            F(so, "_activeTime", 4f / OfficeFps);
            Anim(so, Frames(O + "Office_Bar_Virus.png"), 10, OfficeFps, false);
        });
        a.OfficeBossPie = Attack("Office/Attack_OfficeBoss_Pie", so =>
        {
            BossCommon(so, AttackKind.Projectile);
            F(so, "_windup", 5f / OfficeFps);
            SetSprites(so, "_windupFrames", Frames(O + "Office_Pie_Virus.png"));
            F(so, "_windupFrameRate", OfficeFps);
            I(so, "_projectileCount", 8);
            F(so, "_spreadAngle", 360f); // 全方位
            E(so, "_aim", (int)AimMode.Forward);
            V(so, "_spawnOffset", new Vector2(1.2f, 1.0f));
            F(so, "_speed", 6f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.55f, 0.55f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
            Anim(so, Frames(O + "Office_PieSlice_Virus.png"), 4, 16f, true);
        });
        a.OfficeBossLine = Attack("Office/Attack_OfficeBoss_Line", so =>
        {
            BossCommon(so, AttackKind.Area);
            Line(so, Frames(O + "Office_Line_Virus.png"));
            E(so, "_anchor", (int)AreaAnchor.ForwardAtTargetHeight); // プレイヤーのいる高さに出す
            F(so, "_forwardDistance", 228f / Ppu * 0.5f + 1.0f);
            F(so, "_hitDelay", 0.15f);
            F(so, "_activeTime", 1.2f);
            I(so, "_holdFrame", 6); // F7（閃光）で止めて、光る時間を長くする
        });

        // ───── Unity ─────
        PrimitiveEntry[] Prims(string v) => new[]
        {
            new PrimitiveEntry { Sprite = First(U + $"Unity_Cube{v}.png"), Shape = PrimitiveShape.Box, Size = new Vector2(0.95f, 0.95f) },
            new PrimitiveEntry { Sprite = First(U + $"Unity_Sphere{v}.png"), Shape = PrimitiveShape.Circle, Size = new Vector2(0.95f, 0.95f) },
            new PrimitiveEntry { Sprite = First(U + $"Unity_Capsule{v}.png"), Shape = PrimitiveShape.Capsule, Size = new Vector2(0.55f, 1.0f) },
        };
        a.UnitySpawn = Attack("Unity/Attack_Unity_Spawn", so =>
        {
            Common(so, AttackKind.PhysicsObject, cooldown: 2.5f, damage: 1, gauge: 10, knockback: new Vector2(3f, 4f), hitStun: 0.2f);
            E(so, "_placement", (int)SpawnPlacement.ForwardAbove);
            V(so, "_spawnOffset", new Vector2(1.3f, 1.8f)); // 前方やや上
            I(so, "_spawnCount", 1);
            F(so, "_telegraphTime", 0f);
            Physics(so, Prims(""), maxAlive: 3, lifetime: 8f,
                Frames(U + "Unity_Spawn.png"), Frames(U + "Unity_Impact.png"), null);
        });
        void UnityBoss(SerializedObject so, int count)
        {
            BossCommon(so, AttackKind.PhysicsObject);
            E(so, "_placement", (int)SpawnPlacement.AroundTarget); // 1つ目はプレイヤーの頭上、残りはランダム
            I(so, "_spawnCount", count);
            F(so, "_dropHeight", 3.5f);
            F(so, "_minDistanceFromTarget", 2.5f);
            F(so, "_telegraphTime", 0.7f); // 予告を 0.7 秒点滅
            Physics(so, Prims("_Virus"), maxAlive: 6, lifetime: 6f,
                Frames(U + "Unity_Spawn_Virus.png"), Frames(U + "Unity_Impact_Virus.png"), Frames(U + "Unity_SpawnWarning_Virus.png"));
        }
        a.UnityBossSpawnP1 = Attack("Unity/Attack_UnityBoss_Spawn_P1", so => UnityBoss(so, 2));
        a.UnityBossSpawnP2 = Attack("Unity/Attack_UnityBoss_Spawn_P2", so => UnityBoss(so, 3));

        // ───── Steam（ボスの攻撃【仮】。依頼書に攻撃の指定が無いので、素材で作った） ─────
        a.SteamBossTrophy = Attack("Steam/Attack_SteamBoss_Trophy", so =>
        {
            BossCommon(so, AttackKind.Projectile);
            I(so, "_projectileCount", 3);
            F(so, "_spreadAngle", 30f);
            E(so, "_aim", (int)AimMode.Target);
            V(so, "_spawnOffset", new Vector2(0.8f, 0.3f));
            F(so, "_speed", 7f);
            F(so, "_lifetime", 3f);
            V(so, "_hitSize", new Vector2(0.6f, 0.6f));
            B(so, "_destroyOnTerrain", true);
            B(so, "_destroyOnHit", true);
            Anim(so, Frames(S + "Steam_Trophy_Virus.png"), 0, 10f, true);
        });
        a.SteamBossBurst = Attack("Steam/Attack_SteamBoss_Burst", so =>
        {
            BossCommon(so, AttackKind.Area);
            E(so, "_anchor", (int)AreaAnchor.TargetPosition);
            B(so, "_snapToGround", true);
            E(so, "_shape", (int)AreaShape.Circle);
            V(so, "_areaSize", new Vector2(2.2f, 2.2f));
            F(so, "_telegraphTime", 0.8f);
            F(so, "_activeTime", 0.35f);
            Anim(so, Frames(S + "Steam_BuffBurst_Virus.png"), 0, 14f, false);
        });

        // ───── Steam：実績・常に効く効果 ─────
        var kill5 = Achievement("Achievement_Steam_Kill5", new AchievementSpec("連続駆除", AchievementCondition.KillsWithin, 5, 10f));
        var noDamage = Achievement("Achievement_Steam_NoDamage15", new AchievementSpec("無傷の15秒", AchievementCondition.NoDamageFor, 1, 15f));
        var skill3 = Achievement("Achievement_Steam_Skill3", new AchievementSpec("スキルを3回使う", AchievementCondition.SkillUses, 3));
        var firstBoss = Achievement("Achievement_Steam_FirstBossHit", new AchievementSpec("はじめての一撃", AchievementCondition.FirstBossDamage));
        var bossHit3 = Achievement("Achievement_SteamBoss_Hit3", new AchievementSpec("3回命中", AchievementCondition.HitsLanded, 3));
        var bossTime20 = Achievement("Achievement_SteamBoss_Time20", new AchievementSpec("20秒経過", AchievementCondition.TimeElapsed, 1, 20f));

        a.SteamPassive = Passive("Passive_Steam", so =>
        {
            SetObjects(so, "_achievements", new UnityEngine.Object[] { kill5, noDamage, skill3, firstBoss });
            E(so, "_targets", (int)BuffTargets.Self);
            PassiveLook(so, "", new Color(0.75f, 0.85f, 1f), true);
        });
        a.SteamBossPassive = Passive("Passive_SteamBoss", so =>
        {
            SetObjects(so, "_achievements", new UnityEngine.Object[] { bossHit3, bossTime20 });
            E(so, "_targets", (int)BuffTargets.SelfAndAllies); // ボス自身と雑魚ウイルス
            PassiveLook(so, "_Virus", new Color(0.9f, 0.7f, 1f), false); // 紫の通知（左下）
        });

        // ───── スキル ─────
        a.SkillOffice = Skill("Skill_Office", "グラフ", SkillCategory.Office, a.OfficeBar, First("AppBoss/Office/AppIcon_Office_Normal.png"), so =>
            SetObjects(so, "_attackCycle", new UnityEngine.Object[] { a.OfficeBar, a.OfficePie, a.OfficeLine }));
        a.SkillUnity = Skill("Skill_Unity", "オブジェクト生成", SkillCategory.Dev, a.UnitySpawn, First("AppBoss/Unity/AppIcon_Unity_Normal.png"), null);
        a.SkillSteam = Skill("Skill_Steam", "実績解除", SkillCategory.Game, null, First("AppBoss/Steam/AppIcon_Steam_Normal.png"), so =>
            R(so, "_passive", a.SteamPassive));

        // ───── ボスの設定 ─────
        a.OfficeProfile = Profile("Boss_Office", "Office", "Office", a.SkillOffice, null, so =>
        {
            Phase(so, "_phase1", 1.2f, true, a.OfficeBossBar, a.OfficeBossPie, a.OfficeBossLine); // 3種類のグラフを順番に
            Phase(so, "_phase2", 0.8f, true, a.OfficeBossBar, a.OfficeBossPie, a.OfficeBossLine);
        });
        a.UnityProfile = Profile("Boss_Unity", "Unity", "Unity", a.SkillUnity, null, so =>
        {
            Phase(so, "_phase1", 1.4f, false, a.UnityBossSpawnP1);
            Phase(so, "_phase2", 1.0f, false, a.UnityBossSpawnP2);
        });
        a.SteamProfile = Profile("Boss_Steam", "Steam", "Steam", a.SkillSteam, a.SteamBossPassive, so =>
        {
            Phase(so, "_phase1", 1.2f, false, a.SteamBossTrophy, a.SteamBossBurst);
            Phase(so, "_phase2", 0.8f, false, a.SteamBossTrophy, a.SteamBossBurst);
        });

        AssetDatabase.SaveAssets();
        return a;
    }

    // ───── 攻撃データの部品 ─────

    static void Common(SerializedObject so, AttackKind kind, float cooldown, int damage, int gauge, Vector2 knockback, float hitStun)
    {
        so.FindProperty("_kind").intValue = (int)kind;
        F(so, "_windup", 0f);
        F(so, "_cooldown", cooldown);
        I(so, "_damage", damage);
        I(so, "_gaugeGain", gauge);
        V(so, "_knockback", knockback);
        F(so, "_hitStun", hitStun);
        so.FindProperty("_color").colorValue = Color.white;
    }

    /// <summary>ボスの技の共通値（docs 3章：ダメージ1、ノックバック (6,5)、のけぞり0.25秒）。Attack の F3 に合わせて予備動作 0.2 秒</summary>
    static void BossCommon(SerializedObject so, AttackKind kind)
    {
        Common(so, kind, cooldown: 0f, damage: 1, gauge: 0, knockback: new Vector2(6f, 5f), hitStun: 0.25f);
        F(so, "_windup", 0.2f);
    }

    static void Anim(SerializedObject so, Sprite[] frames, int perRow, float fps, bool loop)
    {
        SetSprites(so, "_frames", frames);
        I(so, "_framesPerRow", perRow);
        F(so, "_frameRate", fps);
        B(so, "_loopFrames", loop);
        if (frames.Length > 0) R(so, "_sprite", frames[0]);
    }

    static void Line(SerializedObject so, Sprite[] frames)
    {
        B(so, "_snapToGround", false);
        E(so, "_shape", (int)AreaShape.Polyline);
        V(so, "_areaSize", new Vector2(228f / Ppu, 90f / Ppu));
        var pts = so.FindProperty("_polylinePoints");
        Vector2[] p = LinePoints();
        pts.arraySize = p.Length;
        for (int i = 0; i < p.Length; i++) pts.GetArrayElementAtIndex(i).vector2Value = p[i];
        F(so, "_polylineRadius", 0.12f);
        I(so, "_areaCount", 1);
        F(so, "_telegraphTime", 0f);
        Anim(so, frames, 0, 14f, false);
    }

    static void Physics(SerializedObject so, PrimitiveEntry[] prims, int maxAlive, float lifetime, Sprite[] spawn, Sprite[] impact, Sprite[] warning)
    {
        var arr = so.FindProperty("_primitives");
        arr.arraySize = prims.Length;
        for (int i = 0; i < prims.Length; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Sprite").objectReferenceValue = prims[i].Sprite;
            e.FindPropertyRelative("Shape").intValue = (int)prims[i].Shape;
            e.FindPropertyRelative("Size").vector2Value = prims[i].Size;
        }
        I(so, "_maxAlive", maxAlive);
        F(so, "_objectLifetime", lifetime);
        F(so, "_damageFallSpeed", 4f);
        F(so, "_impactSpeed", 5f);
        SetSprites(so, "_spawnFrames", spawn);
        F(so, "_spawnFrameRate", 14f);
        I(so, "_spawnReleaseFrame", 1); // F2 で本体
        SetSprites(so, "_impactFrames", impact);
        F(so, "_impactFrameRate", 14f);
        SetSprites(so, "_warningFrames", warning);
        F(so, "_warningFrameRate", 8f);
        if (prims.Length > 0) R(so, "_sprite", prims[0].Sprite);
    }

    static void PassiveLook(SerializedObject so, string v, Color title, bool right)
    {
        string S = "SkillEffects/Steam/";
        var buff = so.FindProperty("_buff");
        buff.FindPropertyRelative("AttackBonus").floatValue = 0.3f;
        buff.FindPropertyRelative("SpeedBonus").floatValue = 0.15f;
        buff.FindPropertyRelative("Duration").floatValue = 8f;
        buff.FindPropertyRelative("StackMode").intValue = (int)BuffStackMode.Extend;
        R(so, "_toastBackground", First(S + $"Steam_ToastBG{v}.png"));
        SetSprites(so, "_trophyFrames", Frames(S + $"Steam_Trophy{v}.png"));
        F(so, "_trophyFrameRate", 10f);
        SetSprites(so, "_burstFrames", Frames(S + $"Steam_BuffBurst{v}.png"));
        F(so, "_burstFrameRate", 14f);
        SetSprites(so, "_auraFrames", Frames(S + $"Steam_BuffAura{v}.png"));
        F(so, "_auraFrameRate", 10f);
        so.FindProperty("_titleColor").colorValue = title;
        F(so, "_toastDuration", 3f);
        B(so, "_toastOnRight", right);
    }

    static void Phase(SerializedObject so, string phase, float wait, bool sequential, params AttackData[] moves)
    {
        SetObjects(so, $"{phase}.Moves", moves);
        so.FindProperty($"{phase}.WaitAfterAttack").floatValue = wait;
        so.FindProperty($"{phase}.AttacksPerMove").intValue = 0; // 動かない
        so.FindProperty($"{phase}.Sequential").boolValue = sequential;
    }

    // ───── 作る（無ければ） ─────

    static AttackData Attack(string rel, Action<SerializedObject> init) => BossTestSceneBuilder.GetOrCreateAttack(rel, init);

    static T GetOrCreate<T>(string path, Action<SerializedObject> init) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        PlayerTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        if (init != null) PlayerTestSceneBuilder.SetProps(asset, init);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static AchievementDefinition Achievement(string file, AchievementSpec spec) =>
        GetOrCreate<AchievementDefinition>($"{AchievementFolder}/{file}.asset", so =>
        {
            var p = so.FindProperty("_spec");
            p.FindPropertyRelative("Name").stringValue = spec.Name;
            p.FindPropertyRelative("Condition").intValue = (int)spec.Condition;
            p.FindPropertyRelative("Count").intValue = spec.Count;
            p.FindPropertyRelative("Window").floatValue = spec.Window;
        });

    static AchievementPassive Passive(string file, Action<SerializedObject> init) =>
        GetOrCreate<AchievementPassive>($"{AchievementFolder}/{file}.asset", init);

    static SkillDefinition Skill(string file, string displayName, SkillCategory category, AttackData attack, Sprite icon, Action<SerializedObject> extra) =>
        GetOrCreate<SkillDefinition>($"{SkillFolder}/{file}.asset", so =>
        {
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_category").intValue = (int)category;
            so.FindProperty("_attack").objectReferenceValue = attack;
            extra?.Invoke(so);
        });

    static BossProfile Profile(string file, string displayName, string app, SkillDefinition reward, SkillPassive passive, Action<SerializedObject> moves) =>
        GetOrCreate<BossProfile>($"{BossFolder}/{file}.asset", so =>
        {
            so.FindProperty("_displayName").stringValue = displayName;
            Sprite icon = First($"AppBoss/{app}/AppIcon_{app}_Normal.png");
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_normalIcon").objectReferenceValue = icon;
            so.FindProperty("_gaugeMax").intValue = 100;
            so.FindProperty("_phase2Threshold").floatValue = 0.5f;
            so.FindProperty("_transitionTime").floatValue = 1.5f;
            so.FindProperty("_startDelay").floatValue = 1f;
            so.FindProperty("_movement").intValue = (int)BossMovement.None;
            so.FindProperty("_reward").objectReferenceValue = reward;
            so.FindProperty("_passive").objectReferenceValue = passive;
            moves(so);
        });

    // ───────── ボス ─────────

    /// <summary>
    /// アプリボスを作る（原点＝体の中心）。見た目は素材の合成版（オーラ込み）1枚を AppBossAnimator でコマ送りする。
    /// 被弾範囲・体当たり・ゲージ・攻撃・ゲージ100%で追い出されるウイルス本体は、ほかのボスと同じ
    /// </summary>
    public static BossController CreateAppBoss(string app, BossProfile profile, Vector2 position, Sprite square, Transform player)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        var boss = new GameObject($"Boss_{app}");
        boss.layer = enemyLayer;
        boss.transform.position = position;

        var bodyGo = new GameObject("Body");
        bodyGo.layer = enemyLayer;
        bodyGo.transform.SetParent(boss.transform, false);
        bodyGo.transform.localScale = new Vector3(BossBodySize.x, BossBodySize.y, 1f); // AddHurtbox は 1×1 の四角
        var hurtCol = PlayerTestSceneBuilder.AddHurtbox(bodyGo);
        var hurtbox = bodyGo.GetComponent<Hurtbox>();
        var contact = PlayerTestSceneBuilder.AddHitbox(bodyGo, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: hurtCol);

        var gauge = boss.AddComponent<VirusGauge>();
        PlayerTestSceneBuilder.SetProps(gauge, so => so.FindProperty("_max").intValue = profile.GaugeMax);
        BossTestSceneBuilder.AddAttackRunner(boss, Team.Enemy, 1 << playerLayer, player);

        var artGo = new GameObject("Art");
        artGo.layer = enemyLayer;
        artGo.transform.SetParent(boss.transform, false);
        var sr = artGo.AddComponent<SpriteRenderer>();
        Sprite[] idle = Frames($"AppBoss/{app}/AppBoss_{app}_Idle.png");
        sr.sprite = idle.Length > 0 ? idle[0] : null;
        sr.sortingOrder = 12;

        var controller = boss.AddComponent<BossController>();
        Health ejected = BossTestSceneBuilder.CreateEjectedVirus(boss.transform, square);
        PlayerTestSceneBuilder.SetProps(controller, so =>
        {
            so.FindProperty("_profile").objectReferenceValue = profile;
            so.FindProperty("_contactHitbox").objectReferenceValue = contact;
            so.FindProperty("_hurtbox").objectReferenceValue = hurtbox;
            so.FindProperty("_externalVisuals").boolValue = false; // 点滅・形態変化の色は BossController が付ける
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = sr;
            so.FindProperty("_normalizedColor").colorValue = Color.white;
            so.FindProperty("_ejectedVirus").objectReferenceValue = ejected;
        });

        var anim = artGo.AddComponent<AppBossAnimator>();
        PlayerTestSceneBuilder.SetProps(anim, so =>
        {
            so.FindProperty("_boss").objectReferenceValue = controller;
            so.FindProperty("_renderer").objectReferenceValue = sr;
            SetSprites(so, "_idle", idle);
            F(so, "_idleFps", 8f);
            SetSprites(so, "_attack", Frames($"AppBoss/{app}/AppBoss_{app}_Attack.png"));
            F(so, "_attackFps", 10f);
            SetSprites(so, "_hit", Frames($"AppBoss/{app}/AppBoss_{app}_Hit.png"));
            F(so, "_hitFps", 12f);
            SetSprites(so, "_purify", Frames($"AppBoss/{app}/AppBoss_{app}_Purify.png"));
            F(so, "_purifyFps", 10f);
            R(so, "_normalIcon", First($"AppBoss/{app}/AppIcon_{app}_Normal.png"));
        });
        return controller;
    }

    /// <summary>ボスのプレハブを作る（あれば同じパスに上書き。GUID は変わらない）</summary>
    static BossController BuildBossPrefab(string app, BossProfile profile, Sprite square)
    {
        string path = $"{PrefabFolder}/Boss_{app}.prefab";
        PlayerTestSceneBuilder.EnsureFolder(PrefabFolder);
        BossController boss = CreateAppBoss(app, profile, BossSpawn, square, null); // 狙う相手は StageDirector が入れる
        boss.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, path);
        UnityEngine.Object.DestroyImmediate(boss.gameObject);
        return AssetDatabase.LoadAssetAtPath<BossController>(path);
    }

    struct AppRow
    {
        public string Name, ScreenTimeName, BundleId, Boss, Theme;
        public SkillCategory Category;
    }

    /// <summary>
    /// 対応アプリ表に登録する（2026-11-18 決定：アプリ単位で登録）。同じ表示名の行があれば、ボスと設定だけ入れ直す。
    /// Office＝Excel・Word・PowerPoint、Unity＝Unity・Unity Hub、Steam＝Steam。ステージは1画面
    /// </summary>
    static void RegisterToCatalog(Dictionary<string, (BossController prefab, BossProfile profile)> bosses)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning($"対応アプリ表が無いので登録しません（先に「ランのシーンを作成」を実行してください）: {CatalogPath}");
            return;
        }
        var rows = new[]
        {
            new AppRow { Name = "Excel", ScreenTimeName = "Microsoft Excel", BundleId = "com.microsoft.Excel", Boss = "Office", Category = SkillCategory.Office, Theme = "Theme_Office" },
            new AppRow { Name = "Word", ScreenTimeName = "Microsoft Word", BundleId = "com.microsoft.Word", Boss = "Office", Category = SkillCategory.Office, Theme = "Theme_Office" },
            new AppRow { Name = "PowerPoint", ScreenTimeName = "Microsoft PowerPoint", BundleId = "com.microsoft.Powerpoint", Boss = "Office", Category = SkillCategory.Office, Theme = "Theme_Office" },
            new AppRow { Name = "Unity", ScreenTimeName = "Unity Editor", BundleId = "com.unity3d.UnityEditor", Boss = "Unity", Category = SkillCategory.Dev, Theme = null },
            new AppRow { Name = "Unity Hub", ScreenTimeName = "Unity Hub", BundleId = "com.unity3d.unityhub", Boss = "Unity", Category = SkillCategory.Dev, Theme = null },
            new AppRow { Name = "Steam", ScreenTimeName = "Steam", BundleId = "com.valvesoftware.steam", Boss = "Steam", Category = SkillCategory.Game, Theme = "Theme_Game" },
        };

        var so = new SerializedObject(catalog);
        var apps = so.FindProperty("_apps");
        foreach (AppRow row in rows)
        {
            SerializedProperty e = null;
            for (int i = 0; i < apps.arraySize; i++)
            {
                if (apps.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName").stringValue == row.Name) e = apps.GetArrayElementAtIndex(i);
            }
            bool isNew = e == null;
            if (isNew)
            {
                apps.arraySize++;
                e = apps.GetArrayElementAtIndex(apps.arraySize - 1);
                e.FindPropertyRelative("DisplayName").stringValue = row.Name;
                SetStrings(e.FindPropertyRelative("BundleIdPrefixes"), row.BundleId);
                SetStrings(e.FindPropertyRelative("ScreenTimeNames"), row.ScreenTimeName);
                e.FindPropertyRelative("Category").intValue = (int)row.Category;
                e.FindPropertyRelative("SpawnPosition").vector2Value = BossSpawn;
                e.FindPropertyRelative("StageScene").stringValue = "Stage_Desktop";
                e.FindPropertyRelative("GaugeMax").FindPropertyRelative("Min").intValue = 80;
                e.FindPropertyRelative("GaugeMax").FindPropertyRelative("Max").intValue = 140;
                e.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Min").floatValue = 0.8f;
                e.FindPropertyRelative("Phase2Wait").FindPropertyRelative("Max").floatValue = 0.6f;
                e.FindPropertyRelative("AbilityStat").intValue = (int)AttackStat.None;
                if (row.Theme != null)
                {
                    e.FindPropertyRelative("Theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AppTheme>($"Assets/ScriptableObjects/Themes/{row.Theme}.asset");
                }
            }
            e.FindPropertyRelative("ExcludeFromStandIn").boolValue = true; // 代用ボスのひな形にしない（ボス B の部品が無い）
            e.FindPropertyRelative("BossPrefab").objectReferenceValue = bosses[row.Boss].prefab;
            e.FindPropertyRelative("Profile").objectReferenceValue = bosses[row.Boss].profile;
        }
        if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(catalog);
    }

    // ───────── 確認用のシーン ─────────

    [MenuItem("Tools/Team1/Office・Unity・Steam テストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod OfficeUnitySteamBuilder.Build）</summary>
    public static void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (ground < 0 || oneWay < 0 || playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません");
            return;
        }

        ImportArt();
        BossTestSceneBuilder.GetOrCreateAttacks();
        ArtAssets.Apply();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（BossTestSceneBuilder と同じ理由）
        Sprite square = PlayerTestSceneBuilder.GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();
        Assets a = GetOrCreateAssets();

        // プレハブと対応アプリ表（ランで出るように）
        var prefabs = new Dictionary<string, (BossController, BossProfile)>
        {
            ["Office"] = (BuildBossPrefab("Office", a.OfficeProfile, square), a.OfficeProfile),
            ["Unity"] = (BuildBossPrefab("Unity", a.UnityProfile, square), a.UnityProfile),
            ["Steam"] = (BuildBossPrefab("Steam", a.SteamProfile, square), a.SteamProfile),
        };
        RegisterToCatalog(prefabs);

        // --- 部屋（BossTest と同じ形） ---
        var terrain = new GameObject("Terrain").transform;
        PlayerTestSceneBuilder.CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(22f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Left", new Vector2(-10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Wall_Right", new Vector2(10.5f, 6f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Left", new Vector2(-3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        PlayerTestSceneBuilder.CreateBlock("Folder_Right", new Vector2(3.5f, 2.85f), new Vector2(2.4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        var arena = new GameObject("BattleArena").AddComponent<BattleArena>();
        PlayerTestSceneBuilder.SetProps(arena, so => so.FindProperty("_bounds").rectValue = new Rect(-10f, 0f, 20f, 11.5f));

        // --- プレイヤー（G キーで Office・Unity・Steam も順にもらえる） ---
        var player = PlayerTestSceneBuilder.CreatePlayer(square, new Vector3(-6f, 0.05f, 0f), attacks.PlayerMelee, a.Skills);
        player.AddComponent<StatModifiers>();

        // --- ボス（最初は隠す。B で出す） ---
        var bosses = new[]
        {
            CreateAppBoss("Office", a.OfficeProfile, BossSpawn, square, player.transform),
            CreateAppBoss("Unity", a.UnityProfile, BossSpawn, square, player.transform),
            CreateAppBoss("Steam", a.SteamProfile, BossSpawn, square, player.transform),
        };

        // --- ボス版の発射台（ボスを出さずにボス版のスキルを試す） ---
        var caster = new GameObject("BossSkillCaster");
        caster.layer = enemyLayer;
        caster.transform.position = new Vector3(7f, 1.2f, 0f);
        var marker = PlayerTestSceneBuilder.CreateSprite("Marker", caster.transform, Vector2.zero, new Vector2(0.6f, 0.6f), new Color(0.7f, 0.3f, 1f, 0.6f), square, 5);
        marker.gameObject.layer = enemyLayer;
        var casterRunner = BossTestSceneBuilder.AddAttackRunner(caster, Team.Enemy, 1 << playerLayer, player.transform);
        caster.AddComponent<VirusGauge>(); // Steam ボス版の実績の「陣営」を決めるため（攻撃は当たらない：被弾範囲なし）

        // --- 雑魚のひな形（Z で出す） ---
        var zakoRoot = new GameObject("Zako").transform;
        StageTestSceneBuilder.CreateWalker("ZakoTemplate", new Vector2(2f, 0.05f), -1, zakoRoot, square);
        GameObject zako = zakoRoot.Find("ZakoTemplate")?.gameObject;

        var director = new GameObject("OfficeUnitySteamDebugDirector").AddComponent<OfficeUnitySteamDebugDirector>();
        PlayerTestSceneBuilder.SetProps(director, so =>
        {
            so.FindProperty("_player").objectReferenceValue = player.transform;
            SetObjects(so, "_playerSkills", a.Skills);
            SetObjects(so, "_bosses", bosses);
            so.FindProperty("_bossCaster").objectReferenceValue = casterRunner;
            SetObjects(so, "_officeBossAttacks", new UnityEngine.Object[] { a.OfficeBossBar, a.OfficeBossPie, a.OfficeBossLine });
            so.FindProperty("_unityBossAttack").objectReferenceValue = a.UnityBossSpawnP1;
            so.FindProperty("_steamBossPassive").objectReferenceValue = a.SteamBossPassive;
            so.FindProperty("_zakoTemplate").objectReferenceValue = zako;
            so.FindProperty("_zakoSpawn").vector2Value = new Vector2(2f, 0.05f);
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
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log($"Office・Unity・Steam のテストシーンを作成しました: {ScenePath}");
    }

    // ───── 小さな道具 ─────

    static void E(SerializedObject so, string p, int v) => so.FindProperty(p).intValue = v;
    static void F(SerializedObject so, string p, float v) => so.FindProperty(p).floatValue = v;
    static void I(SerializedObject so, string p, int v) => so.FindProperty(p).intValue = v;
    static void V(SerializedObject so, string p, Vector2 v) => so.FindProperty(p).vector2Value = v;
    static void B(SerializedObject so, string p, bool v) => so.FindProperty(p).boolValue = v;
    static void R(SerializedObject so, string p, UnityEngine.Object v) => so.FindProperty(p).objectReferenceValue = v;

    static void SetSprites(SerializedObject so, string p, Sprite[] sprites) => SetObjects(so, p, sprites ?? Array.Empty<Sprite>());

    static void SetObjects(SerializedObject so, string p, IReadOnlyList<UnityEngine.Object> values)
    {
        var arr = so.FindProperty(p);
        arr.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void SetStrings(SerializedProperty arr, params string[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
