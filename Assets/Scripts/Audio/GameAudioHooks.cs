using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲームの出来事と音をつなぐ所（2026-11-19）。GameAudio の続き。
/// ・どの Health・どの技・どのゲージでも出る知らせ（static のイベント）は、ここで聞いて鳴らす
/// ・1つの物の知らせ（ShieldWindow.Blocked など）は、その知らせを出す所に GameAudio.Event を1行足してある
/// ・画面・ステージ・ボスの曲の選び方もここ（Tools/audio/FORMAT.md の BGM の表）
/// </summary>
public partial class GameAudio
{
    /// <summary>ミュージック（Apple）のステージの曲（BPM 100 ちょうど。MusicRule のビートと合わせる）</summary>
    public const string MusicStageBgm = "bgm_stage_music";
    /// <summary>ボスが現れるジングルの、音楽としての終わり（165 BPM の2小節）。このあとすぐボスの曲（ジングルの余韻に重ねる）</summary>
    const float BossStingSeconds = 60f / 165f * 4f * 2f;

    static void HookEvents()
    {
        AttackRunner.AnyStarted -= OnMoveStarted;
        AttackRunner.AnyStarted += OnMoveStarted;
        AttackRunner.AnyReleased -= OnMoveReleased;
        AttackRunner.AnyReleased += OnMoveReleased;
        AttackRunner.AnyFollowUp -= OnMoveFollowUp;
        AttackRunner.AnyFollowUp += OnMoveFollowUp;
        Health.AnyDamaged -= OnAnyDamaged;
        Health.AnyDamaged += OnAnyDamaged;
        Health.AnyDied -= OnAnyDied;
        Health.AnyDied += OnAnyDied;
        VirusGauge.AnyFilled -= OnGaugeFilled;
        VirusGauge.AnyFilled += OnGaugeFilled;
        DismissableTab.Dismissed -= OnTabDismissed;
        DismissableTab.Dismissed += OnTabDismissed;
        StagePacer.Started -= OnPacerStarted;
        StagePacer.Started += OnPacerStarted;
    }

    static void OnMoveStarted(AttackRunner runner, AttackData data) => OnMove(runner, data, true);
    static void OnMoveReleased(AttackRunner runner, AttackData data) => OnMove(runner, data, false);
    // 追撃（ボムの爆発・赤い波線など）は、割り当てに行がある物だけ release の音を鳴らす（行が無ければ鳴らさない。たくさん出るので fallbacks は使わない）
    static void OnMoveFollowUp(AttackRunner runner, AttackData data) => OnMove(runner, data, false, true);

    // ダメージ：プレイヤー（被弾）と、それ以外（雑魚・追い出されたウイルス）。とどめの一撃は倒れた音だけにする
    // （AnyDamaged は IsDead になる前に来るので、残りの値で見分ける）
    static void OnAnyDamaged(Health h, int amount)
    {
        try
        {
            if (h == null || h.Current <= 0) return;
            Event(h.Team == Team.Player ? "player_hurt" : "enemy_hit");
        }
        catch (Exception e) { LogOnce("OnAnyDamaged", e); }
    }

    static void OnAnyDied(Health h)
    {
        try
        {
            if (h == null) return;
            if (h.Team == Team.Player)
            {
                Event("player_die");
                StopBgm(1.2f); // このあとゲームオーバーのジングル（テストシーンでは、やり直しで曲が戻る）
            }
            else
            {
                KillChain chain = KillChain.Current;
                int count = chain != null && KillChain.RewardsOpen && RewardKit.IsZako(h) && chain.TimeLeft01 > 0f
                    ? chain.Count : 0;
                Event("enemy_die", Mathf.Pow(2f, Mathf.Min(count, 4) * 2f / 12f));
            }
        }
        catch (Exception e) { LogOnce("OnAnyDied", e); }
    }

    static void OnGaugeFilled(VirusGauge gauge)
    {
        if (gauge != null && gauge.GetComponent<BossController>() != null) Event("boss_purged");
    }

    static void OnTabDismissed(DismissableTab tab) => Event("tab_close");

    static void OnPacerStarted(StagePacer pacer)
    {
        if (pacer == null) return;
        pacer.PhaseChanged -= OnPacePhase;
        pacer.PhaseChanged += OnPacePhase;
    }

    // ステージの出来事が始まった（山場も）。静かな場面・ボスの登場（Done）では鳴らさない
    static void OnPacePhase(PacePhase phase)
    {
        if (phase == PacePhase.Event || phase == PacePhase.Climax) Event("pace_phase");
    }

    // ───────── ステージ・ボスの曲 ─────────

    /// <summary>
    /// ステージが始まった（StageDirector・StageTestDirector の Start。リトライも）。シーンの種類で曲を選ぶ。
    /// ミュージックのステージ（MusicRule が動く）は、ビートの決まりが始まるときに、曲の頭をビートに合わせて始める（MusicRule.Begin）
    /// </summary>
    public static void StageStarted()
    {
        try
        {
            if (StageRuleHost.Current != null && StageRuleHost.Current.Rule is MusicRule) return;
            PlayBgm(StageBgmId(SceneManager.GetActiveScene().name));
            SetBgmPitch(1f, 0f);
        }
        catch (Exception e) { LogOnce("StageStarted", e); }
    }

    /// <summary>ステージのシーンの名前から曲：Stage_Scroll（Chrome）・Stage_Windows（VS Code）・ほか（1画面）</summary>
    public static string StageBgmId(string sceneName)
    {
        string n = sceneName ?? "";
        if (n.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0) return "bgm_stage_scroll";
        if (n.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0) return "bgm_stage_windows";
        return "bgm_stage_desktop";
    }

    /// <summary>ボスが現れる（BossEntrance）：ジングルのあとにボスの曲。ミュージックのステージはビートの曲のまま</summary>
    public static void BossAppearing(BossController boss)
    {
        try
        {
            Event("boss_appear");
            if (CurrentBgmIs(MusicStageBgm)) return;
            PlayJingle("jingle_boss_appear", BossBgmId(boss), BossStingSeconds);
        }
        catch (Exception e) { LogOnce("BossAppearing", e); }
    }

    /// <summary>ボス戦をすぐ始める（ボスのテストシーン。ジングルなし）。ピッチも戻す</summary>
    public static void BossFightStarted(BossController boss)
    {
        try
        {
            PlayBgm(BossBgmId(boss), 0.5f);
            SetBgmPitch(1f, 0.2f);
        }
        catch (Exception e) { LogOnce("BossFightStarted", e); }
    }

    /// <summary>ボスの形態が変わった：第2形態は曲を少し速く・高く（ミュージックのステージはビートを保つので変えない）</summary>
    public static void BossPhase(int phase)
    {
        if (CurrentBgmIs(MusicStageBgm)) return;
        SetBgmPitch(phase >= 2 ? 1.06f : 1f, 0.8f);
    }

    /// <summary>
    /// ボスが正常化した：ボスの曲を消していく。クリアのジングルは、ステージの流れ（GameSession.CompleteStage → StageCleared）で鳴らす。
    /// ステージの流れが無いテストシーン（BossTest・StageTest）は、ここですぐ鳴らす
    /// </summary>
    public static void BossNormalized()
    {
        try
        {
            GameSession s = GameSession.Instance;
            bool inRun = s != null && s.Run != null && UnityEngine.Object.FindObjectOfType<StageDirector>() != null;
            if (inRun) StopBgm(1.5f);
            else StageCleared();
        }
        catch (Exception e) { LogOnce("BossNormalized", e); }
    }

    /// <summary>ステージクリア（GameSession.CompleteStage）：クリアのジングル。そのあとは静か（デスクトップ・リザルトの曲はジングルのあと）</summary>
    public static void StageCleared()
    {
        Event("stage_clear");
        PlayJingle("jingle_stage_clear", "");
    }

    /// <summary>ゲームオーバー：ジングルのあとは静か。リトライ・タイトルの曲が来たら、ジングルを止めてすぐ流す</summary>
    public static void GameOver() => PlayJingle("jingle_gameover", "", -1f, false);

    static string BossBgmId(BossController boss) => IsFinalBoss(boss) ? "bgm_boss_final" : "bgm_boss";

    /// <summary>ラスボス（Terminal）か：ステージの計画（IsFinal）で見る。計画が無いテストシーンでは名前で見る</summary>
    static bool IsFinalBoss(BossController boss)
    {
        if (boss == null) return false;
        StageDirector dir = UnityEngine.Object.FindObjectOfType<StageDirector>();
        if (dir != null && dir.Boss == boss && dir.Plan != null) return dir.Plan.IsFinal;
        string n = boss.Profile != null ? boss.Profile.DisplayName : "";
        return (n ?? "").IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0
               || boss.name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ───────── 画面 ─────────

    /// <summary>リザルトが出た：曲、少しあとにランク、図鑑に色が付いたアプリがあれば、そのあとに</summary>
    public static void ResultShown(RunState run)
    {
        try
        {
            PlayBgm("bgm_result");
            GameAudio a = Ensure();
            if (a != null) a.StartCoroutine(a.ResultRoutine(run != null && run.NewDex.Count > 0, SceneManager.GetActiveScene().name));
        }
        catch (Exception e) { LogOnce("ResultShown", e); }
    }

    IEnumerator ResultRoutine(bool newDex, string scene)
    {
        yield return new WaitForSecondsRealtime(0.7f); // 暗転が明けてから
        if (SceneManager.GetActiveScene().name != scene) yield break;
        Event("result_rank");
        if (!newDex) yield break;
        yield return new WaitForSecondsRealtime(0.5f);
        if (SceneManager.GetActiveScene().name == scene) Event("dex_unlock");
    }
}
