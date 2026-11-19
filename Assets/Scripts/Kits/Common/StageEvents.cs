using System;

// 【仮】アプリごとの出来事（場面③）の、キットの登録所（2026-11-19。docs/app-kits-1-design.md E4）。担当0 の物。
// ・今ある AppStageEvents（StagePacer が場面③でまず見る）に、キットのスイッチ付きで入れるだけ。
// ・専用の出来事を持つのは Teams だけの予定。ほかのキットのギミックは StageRule で足す。
// ・ラスボスのステージ（Terminal）では呼ばない。AppKits.Enabled = false・Rule のスイッチが切れていれば、スキンの出来事に戻る。

/// <summary>キットの出来事（場面③）の登録所</summary>
public static class StageEvents
{
    /// <summary>
    /// キットの出来事を登録する（アプリの名前は AppKits.Register で登録した物を使う）。
    /// factory はステージの番号（1〜）を受け取り、新しい出来事を返す（null ならスキンの出来事）。
    /// extraZako：出来事が自分で雑魚を出す型（群れ）のときの、雑魚の上乗せ（出さないなら 0）
    /// </summary>
    public static void Register(string label, Func<int, StageEvent> factory, int extraZako = 0)
        => Register(label, null, null, factory, extraZako);

    /// <summary>キットの出来事を登録する（アプリの名前も一緒に。AppKits.Register もする）</summary>
    public static void Register(string label, string[] appNames, string[] bundleIds, Func<int, StageEvent> factory, int extraZako = 0)
    {
        if (string.IsNullOrEmpty(label) || factory == null) return;
        if (appNames != null || bundleIds != null) AppKits.Register(label, appNames, bundleIds);
        AppStageEvents.Register("kit:" + label, null, null,
            (stage, isFinal) => isFinal ? null : factory(stage),
            extraZako, () => AppKits.IsOn(label, KitPart.Rule), label);
    }
}
