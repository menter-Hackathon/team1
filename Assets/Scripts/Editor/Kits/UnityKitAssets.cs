using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】Unity のキットのアセット（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// ・ボス：ワープ型（VS Code の体）。第1形態「3D Object」「Rigidbody の球」、第2形態はその強い版＋「Missing Shader」
/// ・能力：キューブ投げ（クールタイム 4秒・ゲージ 8×最大2回）
/// ・ステージの決まりは実行時のコード（Kits/Unity/UnityKit.cs・UnityRule.cs）
/// ・攻撃データの名前に、スキンの枠の語を入れない（3D の物は 3D のまま）
/// </summary>
public class UnityKitAssets : KitAssetBuilder
{
    public override string Label => "Unity";
    public override string[] AppNames => new[] { "Unity", "Unity Hub" };
    public override string[] BundleIds => new[] { "com.unity3d." };
    public override bool HoverBoss => false;
    public override string SkillName => "キューブ投げ";
    public override SkillCategory Category => SkillCategory.Dev;
    public override string SkillDescription => "3Dの立方体を山なりに投げる。はずんでもう1回当たり、止まると足場と弾よけになる。";
    public override string[] SkillTags => new[] { "投げる", "足場" };

    public override AttackData[] Phase1() => new[] { Spawn(false), Ball(false) };
    public override AttackData[] Phase2() => new[] { Spawn(true), Ball(true), MissingShader() };
    public override AttackData PlayerSkill() => CubeThrow();

    // 3D の物のマテリアル（Standard）を Resources に用意する（ビルドに Standard を入れるため）
    public override void AfterBuild(BossProfile boss, SkillDefinition skill) => Kit3DAssets.EnsureStandardMaterial();

    // 技1：GameObject > 3D Object
    AttackData Spawn(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<UnitySpawnAttack>($"Unity_Object3D_{tag}", so =>
        {
            so.FindProperty("_count").intValue = p2 ? 5 : 3;
            so.FindProperty("_stagger").floatValue = p2 ? 0.3f : 0f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_height").vector2Value = new Vector2(5f, 9f);
            so.FindProperty("_remain").floatValue = 4f;
            so.FindProperty("_roll").floatValue = 2f;
            so.FindProperty("_size").floatValue = 1.6f;
        });
        return CustomData($"Attack_Unity_Object3D_{tag}", custom, BossHit);
    }

    // 技2：Rigidbody の球
    AttackData Ball(bool p2)
    {
        string tag = p2 ? "P2" : "P1";
        var custom = Custom<UnityBallAttack>($"Unity_Rigidbody_{tag}", so =>
        {
            so.FindProperty("_diameter").floatValue = 2f;
            so.FindProperty("_appear").floatValue = 0.3f;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_speed").floatValue = 9f;
            so.FindProperty("_secondDelay").floatValue = p2 ? 1.2f : 0f;
        });
        return CustomData($"Attack_Unity_Rigidbody_{tag}", custom, BossHit);
    }

    // 技3（第2形態）：Missing Shader（場に残る技）
    AttackData MissingShader()
    {
        var custom = Custom<UnityMissingShaderAttack>("Unity_MissingShader_P2", so =>
        {
            so.FindProperty("_zones").intValue = 5;
            so.FindProperty("_pink").intValue = 3;
            so.FindProperty("_telegraph").floatValue = 1f;
            so.FindProperty("_duration").floatValue = 2.5f;
        });
        return CustomData("Attack_Unity_MissingShader_P2", custom, so =>
        {
            BossHit(so);
            so.FindProperty("_lingering").boolValue = true;
        });
    }

    // 能力：キューブ投げ
    AttackData CubeThrow()
    {
        var custom = Custom<UnityCubeThrow>("Unity_CubeThrow", so =>
        {
            so.FindProperty("_velocity").vector2Value = new Vector2(7.5f, 8.5f);
            so.FindProperty("_gravity").floatValue = 26f;
            so.FindProperty("_size").floatValue = 1f;
            so.FindProperty("_remain").floatValue = 3f;
            so.FindProperty("_max").intValue = 2;
        });
        return CustomData("Attack_App_Unity_CubeThrow", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_gaugeGain").intValue = 8;  // 1回の当たり。はずんでもう1回（最大 16）
            so.FindProperty("_damage").intValue = 2;     // 雑魚へのダメージ
            so.FindProperty("_knockback").vector2Value = new Vector2(4f, 4f);
            so.FindProperty("_hitStun").floatValue = 0.2f;
        });
    }

    // ボスの技の基本（ライフ1・ノックバック (6,5)・のけぞり 0.25秒）
    static void BossHit(SerializedObject so)
    {
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_knockback").vector2Value = new Vector2(6f, 5f);
        so.FindProperty("_hitStun").floatValue = 0.25f;
    }
}

/// <summary>
/// 【仮】Kit3D のマテリアルのアセット（2026-11-19。担当A）。
/// Standard は Always Included に無いので、実行時の Shader.Find("Standard") はビルドで null になる。
/// Resources に Standard を指すマテリアルを置くと、Standard がビルドに入る（Kit3D は Resources.Load で読み、色は MaterialPropertyBlock で変える）。
/// 無ければ作る。プロジェクトの設定は変えない
/// </summary>
public static class Kit3DAssets
{
    const string Path = "Assets/Resources/" + Kit3D.StandardMaterialResource + ".mat";

    public static void EnsureStandardMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Path);
        if (mat != null) return;
        Shader s = Shader.Find("Standard");
        if (s == null) { Debug.LogWarning("[Kit3D] Standard のシェーダーが見つかりません。マテリアルは作りません（Legacy Shaders/Diffuse に落ちる）"); return; }
        PlayerTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(Path).Replace('\\', '/'));
        mat = new Material(s) { name = "Kit3D_Standard" };
        mat.color = Color.white;
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = false;
        AssetDatabase.CreateAsset(mat, Path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Kit3D] マテリアルを作りました：{Path}");
    }
}
