using UnityEditor;
using UnityEngine;

// Terminal（ラスボス）の専用の技と、再起動の演出のアセットを作る（2026-11-18。docs/app-boss-skills-design.md 4章）。
// 置き場所は BossSkillAssets.TerminalFolder。決まりは BossSkillAssets と同じ（無ければ作る。あれば数値は触らない）。
// ・第1形態：rm -rf（P1）、chmod（P1）、エラー弾（VS Code の P1 をそのまま）
// ・第2形態：文字列の雨、fork、rm -rf（P2）、chmod（P2）、エラー弾（VS Code の P2 をそのまま）
// ・ボスに足す仕組み：再起動の演出・カーネル・パニック（TerminalRebootExtension）
// ・rm -rf・chmod・fork は「場に残る技」、文字列の雨は「床が文字化けしている間は選ばない」（docs 1-3）
// 2026-11-19（docs/app-feedback-v2-design.md 6章）：rm -rf の消え方（第1形態は端から交互、第2形態はランダム）、
// 能力 rm -rf（TerminalSkill）。能力のアセットと、倒すと手に入るようにする所は、下の TerminalAppKit（アプリのキット）
public static partial class BossSkillAssets
{
    const string TerminalErrorBulletP1Path = "Assets/ScriptableObjects/Attacks/VSCode/Attack_VSCode_ErrorBullet_P1.asset";
    const string TerminalErrorBulletP2Path = "Assets/ScriptableObjects/Attacks/VSCode/Attack_VSCode_ErrorBullet_P2.asset";
    const string TerminalBreakpointP1Path = "Assets/ScriptableObjects/Attacks/VSCode/Attack_VSCode_Breakpoint_P1.asset";

    /// <summary>第1形態の技の並び（先頭が最初の技）</summary>
    public static AttackData[] TerminalPhase1() => new[]
    {
        TerminalRmRfData(1),
        TerminalChmodData(1),
        AssetDatabase.LoadAssetAtPath<AttackData>(TerminalErrorBulletP1Path),
    };

    /// <summary>第2形態（カーネル・パニック）の技の並び</summary>
    public static AttackData[] TerminalPhase2() => new[]
    {
        TerminalRainData(),
        TerminalForkData(),
        TerminalRmRfData(2),
        TerminalChmodData(2),
        AssetDatabase.LoadAssetAtPath<AttackData>(TerminalErrorBulletP2Path),
    };

    /// <summary>Terminal に足す仕組み（再起動の演出・ゲージの棒の見せ方・カーネル・パニックの背景）</summary>
    public static BossExtension[] TerminalExtensions() => new BossExtension[]
    {
        GetOrCreateExtension<TerminalRebootExtension>($"{TerminalFolder}/Terminal_Reboot.asset"),
    };

    // ───────── sudo rm -rf /floor（4-1） ─────────

    static AttackData TerminalRmRfData(int phase)
    {
        // 2026-11-19（docs/app-feedback-v2-design.md 6-1）：第1形態は端から順番に3つ（左からと右からを交互）。
        // 第2形態はランダムな3つ（3つ続けては選ばない）を同時に、点滅 1.5秒。数値は中身の初期値
        var custom = GetOrCreateCustom<TerminalRmRfAttack>($"{TerminalFolder}/Terminal_RmRf_P{phase}.asset", so =>
        {
            so.FindProperty("_randomSections").boolValue = phase >= 2;
        });
        // 前に作ったアセット（第2形態は「4つ・左右ランダム」）にも、形態の切り替えだけは毎回入れ直す（何度かけても同じ）。
        // 区画の数・秒数は新しい欄の初期値で動くので、手で直した値は触らない
        if (custom != null)
        {
            PlayerTestSceneBuilder.SetProps(custom, so =>
            {
                so.FindProperty("_randomSections").boolValue = phase >= 2;
                so.FindProperty("_alternateSides").boolValue = true;
            });
            EditorUtility.SetDirty(custom);
        }
        return GetOrCreateAttack($"{TerminalFolder}/Attack_Terminal_RmRf_P{phase}.asset", custom, so =>
        {
            so.FindProperty("_lingering").boolValue = true; // 床が戻るまで、ほかの場に残る技を選ばない
            Color(so, "_color", "FF453A");
        });
    }

    // ───────── chmod 000 player（4-2） ─────────

    static AttackData TerminalChmodData(int phase)
    {
        // アクセス拒否の柱：VS Code のブレークポイント（指定位置の柱）の複製を赤に。幅1.5×高さ6、予告0.8秒、判定0.2秒
        AttackData pillar = CopyAttack(TerminalBreakpointP1Path, $"{TerminalFolder}/Attack_Terminal_DenyPillar_P{phase}.asset", so =>
        {
            so.FindProperty("_anchor").intValue = (int)AreaAnchor.TargetPosition;
            so.FindProperty("_snapToGround").boolValue = true;
            so.FindProperty("_areaSize").vector2Value = new Vector2(1.5f, 6f);
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_activeTime").floatValue = 0.2f;
            so.FindProperty("_persistDuration").floatValue = 0f;
            so.FindProperty("_areaCount").intValue = phase >= 2 ? 3 : 1; // 第2形態はプレイヤーの位置と ±4マス
            so.FindProperty("_areaSpacing").floatValue = 4f;
            so.FindProperty("_areaStagger").floatValue = 0f;
            Color(so, "_color", "FF453A");
        });

        // アクセス禁止エリア（2026-11-18 まさぴ〜の指示で変更）：幅5マス。第1形態は1つ、第2形態は3つ（間 7.5マス）
        var custom = GetOrCreateCustom<TerminalChmodAttack>($"{TerminalFolder}/Terminal_Chmod_P{phase}.asset", so =>
        {
            so.FindProperty("_pillar").objectReferenceValue = pillar;
            so.FindProperty("_zoneWidth").floatValue = 5f;
            so.FindProperty("_zoneCount").intValue = phase >= 2 ? 3 : 1;
            so.FindProperty("_zoneSpacing").floatValue = 7.5f;
        });
        // 中身が先にできていて、柱が空のままなら入れる
        if (custom != null && pillar != null)
        {
            PlayerTestSceneBuilder.SetProps(custom, so =>
            {
                SerializedProperty p = so.FindProperty("_pillar");
                if (p.objectReferenceValue == null) p.objectReferenceValue = pillar;
            });
            EditorUtility.SetDirty(custom);
        }

        // 封印はダメージなし（判定は技の中で、エリアの中にいるかで見る）
        return GetOrCreateAttack($"{TerminalFolder}/Attack_Terminal_Chmod_P{phase}.asset", custom, so =>
        {
            so.FindProperty("_lingering").boolValue = true; // 封印の間、ほかの場に残る技を選ばない
            so.FindProperty("_damage").intValue = 0;
            Color(so, "_color", "FF453A");
        });
    }

    // ───────── 文字列の雨（4-4） ─────────

    static AttackData TerminalRainData()
    {
        var custom = GetOrCreateCustom<TerminalStringRainAttack>($"{TerminalFolder}/Terminal_StringRain.asset");
        // 文字列は弾として出す：毎秒9マス、判定は長さ3（進む向き）×幅1.4、足場はすり抜け、床で消える
        return GetOrCreateAttack($"{TerminalFolder}/Attack_Terminal_StringRain.asset", custom, so =>
        {
            so.FindProperty("_avoidWhileFloorCorrupted").boolValue = true; // 床が文字化けの間は、逃げ場がないので出さない
            so.FindProperty("_speed").floatValue = 9f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(3f, 1.4f);
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            Color(so, "_color", "FF453A");
        });
    }

    // ───────── fork() &（4-3） ─────────

    static AttackData TerminalForkData()
    {
        var custom = GetOrCreateCustom<TerminalForkAttack>($"{TerminalFolder}/Terminal_Fork.asset");
        // 分身の弾：毎秒6マス、まっすぐ、直径0.4、3秒で消える
        return GetOrCreateAttack($"{TerminalFolder}/Attack_Terminal_Fork.asset", custom, so =>
        {
            so.FindProperty("_lingering").boolValue = true; // 分身がいる間、rm -rf と chmod を選ばない
            so.FindProperty("_speed").floatValue = 6f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.4f, 0.4f);
            so.FindProperty("_lifetime").floatValue = 3f;
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            Color(so, "_color", "33FF66");
        });
    }

    // ───────── 能力 rm -rf（2026-11-19。docs/app-feedback-v2-design.md 6-3） ─────────

    /// <summary>
    /// Terminal の能力（床をはう文字化けの波）の攻撃データ。無ければ作る。
    /// 雑魚 3・ボスのゲージ +14（1回だけ）・クールタイム 5秒。波の大きさ・速さ・距離は中身（TerminalPlayerRm）の初期値
    /// </summary>
    public static AttackData TerminalSkill()
    {
        var custom = GetOrCreateCustom<TerminalPlayerRm>($"{TerminalFolder}/Terminal_PlayerRm.asset");
        return GetOrCreateAttack($"{PlayerAppFolder}/Attack_App_Terminal.asset", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 5f;
            so.FindProperty("_damage").intValue = 3;
            so.FindProperty("_gaugeGain").intValue = 14;
            so.FindProperty("_knockback").vector2Value = new Vector2(4f, 4f);
            so.FindProperty("_hitStun").floatValue = 0.2f;
            Color(so, "_color", "33FF66");
        });
    }
}

/// <summary>
/// 【仮】Terminal のアプリのキット（2026-11-19。docs/app-feedback-v2-design.md 6-3。AppKitsBuilder が集める）。
/// ・ボス：作らない（Phase1 は null。Boss_Terminal はラスボスの設定のまま。技は AppBossSkillsBuilder が入れる）
/// ・能力：Skill_App_Terminal（rm -rf）を作り、能力の表の「アプリ専用の能力」に入れる（AppKitsBuilder）。見た目のスキンは Terminal
/// ・直し（Apply）：Boss_Terminal の「手に入る能力」に Skill_App_Terminal を入れる
///   （倒した瞬間に手に入る演出・リザルト・図鑑に出る。最後のステージなので、そのランでは使わない）
/// ・まだ倒していないときは、能力えらびのカードに出さない（実行時に TerminalKit がキットの能力のスイッチを切る）
/// 何度かけても同じ結果になる
/// </summary>
public class TerminalAppKit : KitAssetBuilder, IAppKitFixup
{
    const string SkillPath = "Assets/ScriptableObjects/Skills/Skill_App_Terminal.asset";
    const string BossPath = "Assets/ScriptableObjects/Bosses/Boss_Terminal.asset";

    public override string Label => TerminalKit.Label;
    public override string[] AppNames => TerminalKit.AppNames;
    public override string[] BundleIds => TerminalKit.BundleIds;
    public override bool HoverBoss => false;
    public override AttackData[] Phase1() => null;
    public override AttackData[] Phase2() => null;
    public override AttackData PlayerSkill() => BossSkillAssets.TerminalSkill();
    public override string SkillName => "rm -rf";
    public override SkillCategory Category => SkillCategory.Dev;
    public override string SkillDescription => "床をはう文字化けの波で、敵と弾を消す。";
    public override string[] SkillTags => new[] { "貫通", "弾を消す" };
    public override SkinId SkillSkin => SkinId.Terminal;

    public void Apply()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillPath);
        if (skill == null)
        {
            Debug.LogWarning("[TerminalAppKit] Skill_App_Terminal がありません（能力の表が無いと作られません）");
            return;
        }
        AppTheme theme = ArtAssets.GetOrCreateTerminalTheme();
        PlayerTestSceneBuilder.SetProps(skill, so =>
        {
            so.FindProperty("_skin").intValue = (int)SkinId.Terminal; // SkinTables は直さず、能力のアセットの欄で決める
            SerializedProperty t = so.FindProperty("_theme");
            if (t.objectReferenceValue == null && theme != null) t.objectReferenceValue = theme;
        });
        EditorUtility.SetDirty(skill);

        var boss = AssetDatabase.LoadAssetAtPath<BossProfile>(BossPath);
        if (boss == null)
        {
            Debug.LogWarning("[TerminalAppKit] Boss_Terminal がありません");
            return;
        }
        PlayerTestSceneBuilder.SetProps(boss, so => so.FindProperty("_reward").objectReferenceValue = skill);
        EditorUtility.SetDirty(boss);
    }
}
