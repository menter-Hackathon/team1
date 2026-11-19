using UnityEngine;

/// <summary>
/// 【仮】メモ（Apple の Notes）のキットのアセット（2026-11-19。docs/app-kits-1-design.md 14章。担当0）。数値はすべて【仮】。
/// ・ボス（ワープ）：P1 メモの紙（上の列 5枚）・クイックメモ／P2 メモの紙（7枚）・クイックメモ・鍵をかける
/// ・能力：ピン留め（クールタイム 4秒・ゲージ +8・ボスを 2秒留める）
/// 攻撃データの名前に、枠の語（tabmultiply・popupappear など）を入れない（Office のスキンの絵を付けず、メモの絵に差し替える）
/// </summary>
public class NotesKitAssets : KitAssetBuilder
{
    public override string Label => NotesKit.Label;
    public override string[] AppNames => NotesKit.AppNames;
    public override string[] BundleIds => NotesKit.BundleIds;
    public override bool HoverBoss => false;
    public override string SkillName => NotesKit.SkillName;
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => NotesKit.SkillDescription;
    public override string[] SkillTags => NotesKit.SkillTags;

    public override AttackData[] Phase1() => new[] { Papers("P1", 5, 3.5f), QuickMemo() };
    public override AttackData[] Phase2() => new[] { Papers("P2", 7, 2.9f), QuickMemo(), Lock() };
    public override AttackData PlayerSkill() => PinSkill();

    // ───────── ボスの技 ─────────

    /// <summary>メモの紙：黄色い紙（2×2）が上に count 枚並ぶ → 左から 0.3秒おきに、ゆっくり（毎秒 5マス）落ちる</summary>
    AttackData Papers(string phase, int count, float spacing)
    {
        AttackData inner = Data($"Attack_Notes_Paper_Fall_{phase}", AttackKind.Spawn, so =>
        {
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.TopRow;
            so.FindProperty("_spawnCount").intValue = count;
            so.FindProperty("_objectSize").vector2Value = new Vector2(2f, 2f);
            so.FindProperty("_fallSpeed").floatValue = 5f;
            so.FindProperty("_placeInterval").floatValue = 0.08f;
            so.FindProperty("_placeSpacing").floatValue = spacing;
            so.FindProperty("_dropHoldTime").floatValue = 0.8f; // 並んだ紙が予告（0.8秒）
            so.FindProperty("_dropInterval").floatValue = 0.3f;
            so.FindProperty("_landRemainTime").floatValue = 1.5f;
            so.FindProperty("_becomesPlatform").boolValue = false;
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFD60A");
        });
        return Dressed($"Attack_Notes_Paper_{phase}", inner, NotesKit.ArtPaper, false);
    }

    /// <summary>クイックメモ：近い方の下の隅から、メモの窓（5×4）がせり出す（予告 0.8秒・出る瞬間だけ当たる）→ 足場として 3秒</summary>
    AttackData QuickMemo()
    {
        AttackData window = Data("Attack_Notes_QuickMemo_Window", AttackKind.Spawn, so =>
        {
            so.FindProperty("_placement").intValue = (int)SpawnPlacement.AppearAroundTarget;
            so.FindProperty("_spawnCount").intValue = 1;
            so.FindProperty("_objectSize").vector2Value = new Vector2(5f, 4f);
            so.FindProperty("_telegraphTime").floatValue = 0.8f;
            so.FindProperty("_activeTime").floatValue = 0.2f;
            so.FindProperty("_landRemainTime").floatValue = 3f;
            so.FindProperty("_becomesPlatform").boolValue = true;
            so.FindProperty("_damage").intValue = 1;
            Color(so, "_color", "FFFDF0");
        });
        var custom = Custom<NotesQuickMemo>("NotesQuickMemo", so =>
        {
            so.FindProperty("_window").objectReferenceValue = window;
            so.FindProperty("_art").stringValue = NotesKit.ArtQuick;
        });
        return CustomData("Attack_Notes_QuickMemo", custom, so => so.FindProperty("_damage").intValue = 1);
    }

    /// <summary>鍵をかける（第2形態）：プレイヤーの位置に縦の帯（幅 4）→ 0.9秒後に鍵が閉まる。中にいたら選んでいる枠が 4秒使えない</summary>
    AttackData Lock()
    {
        var custom = Custom<NotesLockAttack>("NotesLockAttack", so =>
        {
            so.FindProperty("_width").floatValue = 4f;
            so.FindProperty("_telegraph").floatValue = 0.9f;
            so.FindProperty("_seal").floatValue = 4f;
            so.FindProperty("_selectedOnly").boolValue = true;
        });
        return CustomData("Attack_Notes_Lock_P2", custom, so =>
        {
            so.FindProperty("_damage").intValue = 0;
            so.FindProperty("_lingering").boolValue = true; // 封印の間は、もう一度選ばない
        });
    }

    // ───────── 能力 ─────────

    /// <summary>ピン留め：画びょうを正面に投げる（毎秒 14マス・9マス）。ゲージ +8、雑魚は倒れる。当たったボスは 2秒留まる</summary>
    AttackData PinSkill()
    {
        AttackData pin = Data("Attack_Notes_Pin_Shot", AttackKind.Projectile, so =>
        {
            so.FindProperty("_aim").intValue = (int)AimMode.Forward;
            so.FindProperty("_projectileCount").intValue = 1;
            so.FindProperty("_speed").floatValue = 14f;
            so.FindProperty("_lifetime").floatValue = 9f / 14f;
            so.FindProperty("_gravity").floatValue = 0f;
            so.FindProperty("_hitSize").vector2Value = new Vector2(0.8f, 0.5f);
            so.FindProperty("_spawnOffset").vector2Value = new Vector2(0.4f, 0.55f);
            so.FindProperty("_destroyOnTerrain").boolValue = true;
            so.FindProperty("_destroyOnHit").boolValue = true;
            so.FindProperty("_gaugeGain").intValue = 8;
            so.FindProperty("_damage").intValue = 3;
            so.FindProperty("_knockback").vector2Value = new Vector2(3f, 2f);
            Color(so, "_color", "FF453A");
        });
        var custom = Custom<NotesPinSkill>("NotesPinSkill", so =>
        {
            so.FindProperty("_pin").objectReferenceValue = pin;
            so.FindProperty("_stun").floatValue = 2f;
            so.FindProperty("_art").stringValue = NotesKit.ArtPin;
        });
        return CustomData("Attack_Notes_Skill_Pin", custom, so =>
        {
            so.FindProperty("_cooldown").floatValue = 4f;
            so.FindProperty("_gaugeGain").intValue = 8;
        });
    }
}
