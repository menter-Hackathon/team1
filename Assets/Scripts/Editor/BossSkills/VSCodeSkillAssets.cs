using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 本物の VS Code の専用の技（行のブレークポイント）のアセットを作る（2026-11-19。docs/app-feedback-v2-design.md 3章）。
// ・VSCodeAppFixup（IAppKitFixup）が、Boss_VSCode を複製して Boss_VSCode_App を作り、ブレークポイントを「行」に入れ替えて、
//   対応アプリの表の VS Code の行の OwnProfile に入れる。代用ボス（Xcode・Slack など）は今の Boss_VSCode（柱）のまま
// ・能力はカテゴリの「ブレークポイント」のまま（見せ方は DevSkin で直した）。キット（IAppKit）は作らない
// ・攻撃データの名前に Breakpoint を入れる（DevSkin.Error が、当たったプレイヤーの頭の上に ⏸ を出す）
public static class VSCodeSkillAssets
{
    public const string Folder = "Assets/ScriptableObjects/Attacks/Bosses/VSCode";
    const string VSCodeAttacks = "Assets/ScriptableObjects/Attacks/VSCode";
    public const string BossPath = "Assets/ScriptableObjects/Bosses/Boss_VSCode_App.asset";

    /// <summary>第1形態：エラー弾（今の P1）⇄ ブレークポイント（行。1行）</summary>
    public static AttackData[] Phase1() => Compact(
        Load($"{VSCodeAttacks}/Attack_VSCode_ErrorBullet_P1.asset"),
        BreakpointLine(1));

    /// <summary>第2形態：エラー弾（今の P2）、ブレークポイント（行。3行）、エラーログ（今のエラーウィンドウ落下。絵はログの束）</summary>
    public static AttackData[] Phase2() => Compact(
        Load($"{VSCodeAttacks}/Attack_VSCode_ErrorBullet_P2.asset"),
        BreakpointLine(2),
        Load($"{VSCodeAttacks}/Attack_VSCode_ErrorWindowFall.asset"));

    /// <summary>行のブレークポイント。phase：1＝1行、2＝3行（0.7秒ずつずらす）</summary>
    public static AttackData BreakpointLine(int phase)
    {
        var custom = BossSkillAssets.GetOrCreateCustom<VSCodeBreakpointLine>($"{Folder}/VSCode_BreakpointLine_P{phase}.asset", so =>
        {
            ChromeSkillAssets.Int(so, "_lines", phase >= 2 ? 3 : 1);
        });
        return BossSkillAssets.GetOrCreateAttack($"{Folder}/Attack_VSCode_BreakpointLine_P{phase}.asset", custom, so =>
        {
            ChromeSkillAssets.Int(so, "_damage", 1);
            ChromeSkillAssets.Vec2(so, "_knockback", new Vector2(6f, 5f));
            ChromeSkillAssets.Float(so, "_hitStun", 0.25f);   // ⏸ を出す長さにも使う
            BossSkillAssets.Color(so, "_color", "FFCC00");
        });
    }

    static AttackData Load(string path)
    {
        var d = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (d == null) Debug.LogWarning($"[VS Code] 攻撃データが見つかりません：{path}");
        return d;
    }

    static AttackData[] Compact(params AttackData[] list)
    {
        var l = new List<AttackData>();
        foreach (var d in list) if (d != null) l.Add(d);
        return l.ToArray();
    }
}

/// <summary>
/// 本物の VS Code のボス（Boss_VSCode_App）を用意する（2026-11-19。docs 1-1・3-0）。AppKitsBuilder が最後に呼ぶ。何度呼んでも同じ結果。
/// Boss_VSCode を複製（無ければ）して、技を入れ替え、対応アプリの表の VS Code の行の OwnProfile に入れる。手に入る能力・瞬間移動は複製のまま
/// </summary>
public class VSCodeAppFixup : IAppKitFixup
{
    public void Apply()
    {
        BossProfile profile = ChromeSkillAssets.GetOrCopyProfile("Assets/ScriptableObjects/Bosses/Boss_VSCode.asset", VSCodeSkillAssets.BossPath);
        if (profile == null) return;
        AttackData[] p1 = VSCodeSkillAssets.Phase1();
        AttackData[] p2 = VSCodeSkillAssets.Phase2();
        PlayerTestSceneBuilder.SetProps(profile, so =>
        {
            ChromeSkillAssets.Moves(so, "_phase1", p1);
            ChromeSkillAssets.Moves(so, "_phase2", p2);
        });
        EditorUtility.SetDirty(profile);
        ChromeSkillAssets.SetOwnProfile("VS Code", profile);
        AssetDatabase.SaveAssets();
    }
}
