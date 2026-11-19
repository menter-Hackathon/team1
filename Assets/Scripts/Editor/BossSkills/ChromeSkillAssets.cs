using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 本物の Chrome の専用の技と、Chrome 専用の能力「恐竜ラン」のアセットを作る（2026-11-19。docs/app-feedback-v2-design.md 2章）。
// ・能力：ChromeAppKit（IAppKit）を AppKitsBuilder（メニュー「Tools/Team1/アプリのキットを用意」）が集めて、Skill_App_Chrome を作り、能力の表に入れる
// ・ボス：ChromeAppFixup（IAppKitFixup）が、Boss_Chrome を複製して Boss_Chrome_App を作り、技を入れ替えて、対応アプリの表の Chrome の行の OwnProfile に入れる。
//   代用ボス（Safari・Excel など）は今の Boss_Chrome を借りるので、Boss_Chrome と Attack_Chrome_* は変えない
// ・決まり（BossSkillAssets）：無ければ作る。作ったときだけ数値を入れる（手で直した値は残す）。数値を入れ直すときは、アセットを消してから
// ・partial の BossSkillAssets には足さない（ChromePhase1 などの名前は担当0 の仮の口と重なるため）。自分のクラスから BossSkillAssets の道具を呼ぶ
public static class ChromeSkillAssets
{
    public const string Folder = "Assets/ScriptableObjects/Attacks/Bosses/Chrome";
    const string ChromeAttacks = "Assets/ScriptableObjects/Attacks/Chrome";
    const string BossFolder = "Assets/ScriptableObjects/Bosses";
    const string CatalogPath = "Assets/ScriptableObjects/Run/AppCatalog.asset";
    public const string SkillPath = "Assets/ScriptableObjects/Skills/Skill_App_Chrome.asset";
    public const string BossPath = BossFolder + "/Boss_Chrome_App.asset";

    /// <summary>
    /// 広告の形4種類（優先度 B。ChromeAdPopups）を使うか。false にすると、今のポップアップ出現（Attack_Chrome_PopupAppear_P1/P2）に戻る
    /// （遊んでみて具合が悪いときの逃げ道）
    /// </summary>
    static readonly bool UseAdShapes = true;

    /// <summary>第1形態：タブ飛ばし（今の P1）⇄ ポップアップ広告（2枚）。交互</summary>
    public static AttackData[] Phase1() => Compact(
        Load($"{ChromeAttacks}/Attack_Chrome_TabShuriken_P1.asset"),
        UseAdShapes ? AdPopups(1) ?? Load($"{ChromeAttacks}/Attack_Chrome_PopupAppear_P1.asset") : Load($"{ChromeAttacks}/Attack_Chrome_PopupAppear_P1.asset"));

    /// <summary>第2形態：タブ飛ばし（5発×2回。今の P2）、ポップアップ広告（4枚）、タブ増殖、オフライン</summary>
    public static AttackData[] Phase2() => Compact(
        Load($"{ChromeAttacks}/Attack_Chrome_TabShuriken_P2.asset"),
        UseAdShapes ? AdPopups(2) ?? Load($"{ChromeAttacks}/Attack_Chrome_PopupAppear_P2.asset") : Load($"{ChromeAttacks}/Attack_Chrome_PopupAppear_P2.asset"),
        Load($"{ChromeAttacks}/Attack_Chrome_TabMultiply.asset"),
        Offline());

    /// <summary>能力「恐竜ラン」の攻撃データ。種類 Custom、ダメージ 3・ゲージ +12・クールタイム 3.5（docs 2-4）</summary>
    public static AttackData DinoRun()
    {
        var custom = BossSkillAssets.GetOrCreateCustom<ChromeDinoRun>($"{BossSkillAssets.PlayerAppFolder}/Custom_App_Chrome_DinoRun.asset");
        return BossSkillAssets.GetOrCreateAttack($"{BossSkillAssets.PlayerAppFolder}/Attack_App_Chrome_DinoRun.asset", custom, so =>
        {
            Float(so, "_cooldown", 3.5f);                     // 押した瞬間から
            Int(so, "_damage", 3);                            // 雑魚：1体に1回、ダメージ 3
            Int(so, "_gaugeGain", 12);                        // ボス：1回だけ +12
            Vec2(so, "_knockback", new Vector2(7f, 5f));      // はね飛ばす
            Float(so, "_hitStun", 0.2f);
            Vec2(so, "_hitSize", new Vector2(1.2f, 1.3f));    // 恐竜の大きさ（技の中身の欄が本物。ここは見る用）
        });
    }

    /// <summary>オフライン（第2形態の技）。場に残る技。ライフ -1</summary>
    public static AttackData Offline()
    {
        var custom = BossSkillAssets.GetOrCreateCustom<ChromeOffline>($"{Folder}/Chrome_Offline.asset");
        return BossSkillAssets.GetOrCreateAttack($"{Folder}/Attack_Chrome_Offline.asset", custom, so =>
        {
            Int(so, "_damage", 1);
            Vec2(so, "_knockback", new Vector2(6f, 5f));
            Float(so, "_hitStun", 0.25f);
            Bool(so, "_lingering", true);                     // 場に残る技（ほかの場に残る技と重ねない）
            BossSkillAssets.Color(so, "_color", "535353");
        });
    }

    /// <summary>
    /// ポップアップ広告（形4種類。優先度 B。docs 2-2）。形ごとの攻撃データ（Attack_Chrome_PopupAppear_P1 の複製。名前に PopupAppear が入るので窓の絵が付く）を4つ作り、
    /// 専用の技 ChromeAdPopups にまとめる。phase：1＝2枚、2＝4枚。元のアセットが無ければ null（今の技のまま）
    /// </summary>
    public static AttackData AdPopups(int phase)
    {
        string src = $"{ChromeAttacks}/Attack_Chrome_PopupAppear_P{phase}.asset";
        if (AssetDatabase.LoadAssetAtPath<AttackData>(src) == null) return null;
        // 形：四角 3×2（今の形）、横長のバナー 4×1.2、たて長 1.6×3、小さい四角 2×1.6
        var shapes = new (string name, Vector2 size)[]
        {
            ("Square", new Vector2(3f, 2f)),
            ("Banner", new Vector2(4f, 1.2f)),
            ("Tall", new Vector2(1.6f, 3f)),
            ("Small", new Vector2(2f, 1.6f)),
        };
        var datas = new List<AttackData>();
        foreach (var s in shapes)
        {
            Vector2 size = s.size;
            AttackData d = BossSkillAssets.CopyAttack(src, $"{Folder}/Attack_Chrome_PopupAppear_{s.name}_P{phase}.asset", so =>
            {
                Vec2(so, "_objectSize", size);
                Int(so, "_spawnCount", phase >= 2 ? 4 : 2);
            });
            if (d == null) return null;
            datas.Add(d);
        }
        var custom = BossSkillAssets.GetOrCreateCustom<ChromeAdPopups>($"{Folder}/Chrome_AdPopups_P{phase}.asset", so =>
        {
            Int(so, "_count", phase >= 2 ? 4 : 2);
        });
        // 形の攻撃データは、いつも入れ直す（並びは 四角・バナー・たて長・小さい四角）
        PlayerTestSceneBuilder.SetProps(custom, so =>
        {
            SerializedProperty arr = so.FindProperty("_shapes");
            if (arr == null)
            {
                Debug.LogWarning("ChromeAdPopups の欄 _shapes が見つかりません");
                return;
            }
            arr.arraySize = datas.Count;
            for (int i = 0; i < datas.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = datas[i];
        });
        EditorUtility.SetDirty(custom);
        return BossSkillAssets.GetOrCreateAttack($"{Folder}/Attack_Chrome_AdPopups_P{phase}.asset", custom, so =>
        {
            Int(so, "_damage", 1);
            Vec2(so, "_knockback", new Vector2(6f, 5f));
            Float(so, "_hitStun", 0.25f);
            BossSkillAssets.Color(so, "_color", "FF453A");
        });
    }

    // ───────── 道具（欄が見つからなくても止めない。警告だけ） ─────────

    static AttackData Load(string path)
    {
        var d = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (d == null) Debug.LogWarning($"[Chrome] 攻撃データが見つかりません：{path}");
        return d;
    }

    static AttackData[] Compact(params AttackData[] list)
    {
        var l = new List<AttackData>();
        foreach (var d in list) if (d != null) l.Add(d);
        return l.ToArray();
    }

    internal static void Float(SerializedObject so, string name, float v)
    {
        var p = so.FindProperty(name);
        if (p != null) p.floatValue = v;
        else Debug.LogWarning($"[Chrome/VSCode] 欄 {name} が見つかりません（{so.targetObject.name}）");
    }

    internal static void Int(SerializedObject so, string name, int v)
    {
        var p = so.FindProperty(name);
        if (p != null) p.intValue = v;
        else Debug.LogWarning($"[Chrome/VSCode] 欄 {name} が見つかりません（{so.targetObject.name}）");
    }

    internal static void Bool(SerializedObject so, string name, bool v)
    {
        var p = so.FindProperty(name);
        if (p != null) p.boolValue = v;
        else Debug.LogWarning($"[Chrome/VSCode] 欄 {name} が見つかりません（{so.targetObject.name}）");
    }

    internal static void Vec2(SerializedObject so, string name, Vector2 v)
    {
        var p = so.FindProperty(name);
        if (p != null) p.vector2Value = v;
        else Debug.LogWarning($"[Chrome/VSCode] 欄 {name} が見つかりません（{so.targetObject.name}）");
    }

    internal static void Ref(SerializedObject so, string name, Object v)
    {
        var p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = v;
        else Debug.LogWarning($"[Chrome/VSCode] 欄 {name} が見つかりません（{so.targetObject.name}）");
    }

    /// <summary>ボスの設定の技の並びを入れる（空・null の形態は今のまま）</summary>
    internal static void Moves(SerializedObject so, string phase, AttackData[] moves)
    {
        if (moves == null || moves.Length == 0) return;
        SerializedProperty ph = so.FindProperty(phase);
        SerializedProperty arr = ph != null ? ph.FindPropertyRelative("Moves") : null;
        if (arr == null)
        {
            Debug.LogWarning($"[Chrome/VSCode] 欄 {phase}.Moves が見つかりません（{so.targetObject.name}）");
            return;
        }
        arr.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
    }

    /// <summary>ボスの設定を複製して作る（無ければ）。あれば今の物</summary>
    internal static BossProfile GetOrCopyProfile(string sourcePath, string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        if (profile != null) return profile;
        if (!AssetDatabase.CopyAsset(sourcePath, path))
        {
            Debug.LogWarning($"ボスの設定を複製できません：{sourcePath} → {path}");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<BossProfile>(path);
    }

    /// <summary>対応アプリの表の、表示名が displayName の行の OwnProfile に入れる</summary>
    internal static void SetOwnProfile(string displayName, BossProfile profile)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AppCatalog>(CatalogPath);
        if (catalog == null || profile == null) return;
        PlayerTestSceneBuilder.SetProps(catalog, so =>
        {
            SerializedProperty apps = so.FindProperty("_apps");
            if (apps == null) return;
            for (int i = 0; i < apps.arraySize; i++)
            {
                SerializedProperty e = apps.GetArrayElementAtIndex(i);
                SerializedProperty n = e.FindPropertyRelative("DisplayName");
                if (n == null || n.stringValue != displayName) continue;
                SerializedProperty own = e.FindPropertyRelative("OwnProfile");
                if (own == null)
                {
                    Debug.LogWarning("対応アプリの表に OwnProfile の欄がありません（AppCatalog.cs を確かめる）");
                    return;
                }
                own.objectReferenceValue = profile;
                return;
            }
            Debug.LogWarning($"対応アプリの表に {displayName} の行がありません");
        });
        EditorUtility.SetDirty(catalog);
    }
}

/// <summary>
/// Chrome のキット（能力だけ。2026-11-19）。ボスは作らない（Phase1 が null。本物の Chrome のボスは ChromeAppFixup が作る Boss_Chrome_App）。
/// 能力「恐竜ラン」は Skill_App_Chrome になり、本物の Chrome を倒したとき・能力えらびの Chrome のカードがこれになる（【要相談⑤】よいものとして作る）。
/// 「新しいタブ」は、ほかのブラウザ（Safari・Arc など）の能力として残る
/// </summary>
public class ChromeAppKit : IAppKit
{
    public string Label => "Chrome";
    public string[] AppNames => new[] { "Google Chrome", "Chrome" };
    public string[] BundleIds => new[] { "com.google.Chrome" };
    public bool HoverBoss => true;
    public AttackData[] Phase1() => null;
    public AttackData[] Phase2() => null;
    public AttackData PlayerSkill() => ChromeSkillAssets.DinoRun();
    public string SkillName => "恐竜ラン";
    public SkillCategory Category => SkillCategory.Browser;
}

/// <summary>
/// 本物の Chrome のボス（Boss_Chrome_App）を用意する（2026-11-19。docs 1-1・2-0）。AppKitsBuilder が最後に呼ぶ。何度呼んでも同じ結果。
/// ・Boss_Chrome を複製（無ければ）して、技を入れ替える（第1形態：タブ飛ばし・広告。第2形態：タブ飛ばし・広告・タブ増殖・オフライン）
/// ・手に入る能力：Skill_App_Chrome（恐竜ラン）。能力の見た目のスキンは Browser
/// ・対応アプリの表の Chrome の行の OwnProfile に入れる（代用ボスは今の Boss_Chrome のまま）
/// </summary>
public class ChromeAppFixup : IAppKitFixup
{
    public void Apply()
    {
        BossProfile profile = ChromeSkillAssets.GetOrCopyProfile("Assets/ScriptableObjects/Bosses/Boss_Chrome.asset", ChromeSkillAssets.BossPath);
        if (profile == null) return;
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(ChromeSkillAssets.SkillPath);
        AttackData[] p1 = ChromeSkillAssets.Phase1();
        AttackData[] p2 = ChromeSkillAssets.Phase2();
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            ChromeSkillAssets.Moves(so, "_phase1", p1);
            ChromeSkillAssets.Moves(so, "_phase2", p2);
            if (skill != null) ChromeSkillAssets.Ref(so, "_reward", skill);
        });
        EditorUtility.SetDirty(profile);

        if (skill != null)
        {
            PlayerTestSceneBuilder.SetProps(skill, so => ChromeSkillAssets.Int(so, "_skin", (int)SkinId.Browser));
            EditorUtility.SetDirty(skill);
        }
        else Debug.LogWarning("[Chrome] Skill_App_Chrome がありません（ChromeAppKit の能力が作られていない）。Boss_Chrome_App の手に入る能力は今のまま");

        ChromeSkillAssets.SetOwnProfile("Chrome", profile);
        AssetDatabase.SaveAssets();
    }
}
