using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】メール（Apple の Mail）のキットのアセット（2026-11-19。docs/app-kits-1-design.md 15章）。担当2。
/// ボス：浮く（Chrome の体）。第1形態＝送信・迷惑メール、第2形態＝送信・迷惑メール（4通）・全員に返信。
/// 能力：送信（行って戻る紙飛行機）。実行時の登録は Kits/Mail/MailKit.cs（ラベル "Mail"）。
/// 数値はすべて【仮】（作るたびに入れ直す。コードの数値が正）
/// </summary>
public class MailKitAssets : KitAssetBuilder
{
    public override string Label => "Mail";
    public override string[] AppNames => new[] { "Mail", "メール" };
    public override string[] BundleIds => new[] { "com.apple.mail" };
    public override bool HoverBoss => true;

    public override AttackData[] Phase1() => new[] { Send(1), Spam(1) };
    public override AttackData[] Phase2() => new[] { Send(2), Spam(2), ReplyAll() };
    public override AttackData PlayerSkill() => SendSkill();

    public override string SkillName => "送信";
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => "紙飛行機を投げる。敵を貫いて飛び、戻ってきてもう一度当たる";
    public override string[] SkillTags => new[] { "貫通", "2回当たる" };

    // ───────── ボスの技 ─────────

    /// <summary>送信：ボスの横に紙飛行機 3つ（線の予告 0.8秒）→ 0.35秒おきに 3発（毎秒 10マス）</summary>
    AttackData Send(int phase)
    {
        var custom = Custom<MailSendAttack>($"Mail_Send_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = 3;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_interval").floatValue = phase >= 2 ? 0.3f : 0.35f;
            so.FindProperty("_lockBefore").floatValue = 0.3f;
        });
        return CustomData($"Attack_Mail_Send_P{phase}", custom, so => Shot(so, 10f, new Vector2(0.8f, 0.5f)));
    }

    /// <summary>迷惑メール：封筒 3通（第2形態 4通）。床に 6秒。近づくと 0.6秒で直径 3 の爆発。叩けば消える</summary>
    AttackData Spam(int phase)
    {
        var custom = Custom<MailSpamAttack>($"Mail_Spam_P{phase}", so =>
        {
            so.FindProperty("_count").intValue = phase >= 2 ? 4 : 3;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_life").floatValue = 6f;
            so.FindProperty("_minDistance").floatValue = 2f;
            so.FindProperty("_spacing").floatValue = 2.5f;
        });
        return CustomData($"Attack_Mail_Spam_P{phase}", custom, so =>
        {
            so.FindProperty("_damage").intValue = 1;
        });
    }

    /// <summary>全員に返信（第2形態）：封筒の全部とボスから線（0.8秒）→ 1発ずつ（毎秒 10マス）。封筒が無ければ先に迷惑メール</summary>
    AttackData ReplyAll()
    {
        AttackData spam = Spam(2);
        var custom = Custom<MailReplyAllAttack>("Mail_ReplyAll_P2", so =>
        {
            so.FindProperty("_spam").objectReferenceValue = spam;
            so.FindProperty("_telegraph").floatValue = 0.8f;
            so.FindProperty("_afterSpam").floatValue = 0.6f;
        });
        return CustomData("Attack_Mail_ReplyAll_P2", custom, so => Shot(so, 10f, new Vector2(0.8f, 0.5f)));
    }

    // ───────── 能力 ─────────

    /// <summary>送信（能力）：毎秒 14マス・8マス先で戻る。行き +5・帰り +5、雑魚 3。クールタイム 2.5秒</summary>
    AttackData SendSkill()
    {
        var custom = Custom<MailSendSkill>("Mail_SendSkill", so =>
        {
            so.FindProperty("_distance").floatValue = 8f;
            so.FindProperty("_speed").floatValue = 14f;
            so.FindProperty("_catchBonus").floatValue = 0.5f;
        });
        return CustomData("Attack_App_Mail_Send", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 2.5f;
            so.FindProperty("_damage").intValue = 3;
            so.FindProperty("_gaugeGain").intValue = 5;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            so.FindProperty("_hitStun").floatValue = 0.15f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(1.0f, 0.6f);
            so.FindProperty("_sprite").objectReferenceValue = null;
            Color(so, "_color", "FFFFFF");
        });
    }

    // ───────── 道具 ─────────

    static void Shot(SerializedObject so, float speed, Vector2 size)
    {
        so.FindProperty("_speed").floatValue = speed;
        so.FindProperty("_hitSize").vector2Value = size;
        so.FindProperty("_lifetime").floatValue = 3f;
        so.FindProperty("_gravity").floatValue = 0f;
        so.FindProperty("_destroyOnTerrain").boolValue = true;
        so.FindProperty("_destroyOnHit").boolValue = true;
        so.FindProperty("_damage").intValue = 1;
        so.FindProperty("_sprite").objectReferenceValue = null;
    }
}
