using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】ミュージック（Apple Music）のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 4-1。担当C）。
/// ラベルは Editor/Kits/MusicKitAssets.cs の Label と同じ "Music"。ステージの決まりは「ビート」（MusicRule）。
/// 能力「リピート」のために、最後に使った能力を覚える（シーンごとに PlayerSkillExecutor.SkillUsed を見る）
/// </summary>
public static class MusicKit
{
    public const string Label = "Music";
    public static readonly string[] Names = { "Music", "ミュージック", "Apple Music" };
    public static readonly string[] Bundles = { "com.apple.Music" };

    /// <summary>最後に使った能力（リピート自身は入れない。シーンが変わると消える）</summary>
    public static SkillDefinition LastSkill { get; private set; }

    static PlayerSkillExecutor _watched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new MusicRule());
        LastSkill = null;
        _watched = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LastSkill = null;
        Watch();
    }

    /// <summary>プレイヤーの能力を見張る（まだなら）</summary>
    public static void Watch()
    {
        PlayerSkillExecutor ex = Object.FindObjectOfType<PlayerSkillExecutor>();
        if (ex == null || ex == _watched) return;
        if (_watched != null) _watched.SkillUsed -= OnSkillUsed;
        _watched = ex;
        ex.SkillUsed += OnSkillUsed;
    }

    static void OnSkillUsed(int slot, SkillDefinition skill)
    {
        if (skill == null || skill.Attack == null) return;
        if (skill.Attack.Kind == AttackKind.Custom && skill.Attack.Custom is MusicRepeat) return;
        LastSkill = skill;
    }
}
