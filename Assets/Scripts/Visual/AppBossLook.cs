using UnityEngine;

/// <summary>
/// ボス B（ウイルスがアプリを着ている）に、アプリのアイコンから作った見た目を着せる（2026-11-19：代用ボス・ラスボス・Office・Unity・Steam で同じ作り方）。
/// ・着ているウィンドウ：BossWindowArt で、アイコン（24 ドット）と色から作る（ひび3段階）
/// ・ゲージの横のアイコン（12 ドット）と名前：ボスの設定の複製に入れる（アセットそのものは書き換えない）
/// </summary>
public static class AppBossLook
{
    /// <summary>着ているウィンドウを差し替える。windowTheme：ウィンドウの色（空ならアイコンの色）</summary>
    public static bool ApplyWindow(BossController boss, AppVisual visual, AppTheme windowTheme, string appName)
    {
        if (boss == null || visual == null || visual.Icon24 == null) return false;
        var armor = boss.GetComponentInChildren<BossArmorVisual>(true);
        if (armor == null) return false;
        armor.SetWindowStages(BossWindowArt.Create(visual.Icon24, windowTheme != null ? windowTheme : visual.Theme, $"BossWindow_{appName}"));
        return true;
    }

    /// <summary>
    /// ウィンドウと、ゲージの横のアイコン・名前をまとめて差し替える（確認用のシーン用。ランでは RunPlanner と StageDirector が分けて行う）。
    /// ボスの設定は複製して渡すので、アセットは変わらない
    /// </summary>
    public static void Apply(BossController boss, AppVisual visual, string appName)
    {
        if (boss == null || visual == null) return;
        ApplyWindow(boss, visual, null, appName);
        BossProfile profile = boss.Profile;
        if (profile == null) return;
        BossProfile copy = profile.CreateScaledCopy(profile.GaugeMax, profile.Phase2.WaitAfterAttack, profile.Reward);
        copy.SetLook(appName, visual.Icon12);
        boss.SetProfile(copy);
    }
}
