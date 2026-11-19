using UnityEngine;

// 【仮】出来事（アプリごとの事件）で使う攻撃データの組（2026-11-19。docs/stage-pacing-design.md 3-1・10-2）。担当2 の物。
// エディタのメニュー「Tools/Team1/ステージの山と谷を用意」（StagePacingBuilder）が作る：
//   Assets/Resources/StagePacing/PaceAssets.asset（この組）と Assets/Resources/StagePacing/HazardRunner.prefab（出来事用の AttackRunner）。
// 実行時は Resources.Load で読む。無ければ、危ない物を出す出来事（雨・柱床）は出さない（エラーにしない）。
// 攻撃データの名前で、スキンの枠が決まる（SkinTables.FrameOf）。ErrorWindowFall＝落下物、Breakpoint＝柱、RedWave＝床。

/// <summary>出来事の攻撃データと、出来事用の AttackRunner のプレハブ</summary>
public class PaceAssets : ScriptableObject
{
    /// <summary>Resources.Load の名前</summary>
    public const string ResourcePath = "StagePacing/PaceAssets";
    /// <summary>出来事用の AttackRunner のプレハブ（StagePaceTable.HazardRunnerPath と同じ）</summary>
    public const string RunnerPath = "StagePacing/HazardRunner";

    [Header("雨（落下物。名前に ErrorWindowFall）")]
    [Tooltip("ふつう（Dev・Finder・Generic）")]
    public AttackData Rain;
    [Tooltip("小さい・速い（Chat の通知の嵐）")]
    public AttackData RainSmall;
    [Tooltip("大きい・ゆっくり（AI の生成が止まらない）")]
    public AttackData RainBig;

    [Header("柱・床（名前に Breakpoint・RedWave）")]
    [Tooltip("柱（Office の棒グラフ）。低い柱を並べて床の半分を覆う")]
    public AttackData Pillar;
    [Tooltip("柱・リズム（Media。予告が短く、判定も短い）")]
    public AttackData PillarRhythm;
    [Tooltip("床（Creative の塗りつぶし・Premiere の再生ヘッド）")]
    public AttackData Floor;
    [Tooltip("床・再起動の予告（Terminal。予告が長い）")]
    public AttackData FloorReboot;

    [Header("出来事用の AttackRunner（陣営＝敵・当てる相手＝プレイヤー）")]
    public AttackRunner HazardRunner;

    static PaceAssets _cached;

    /// <summary>組を読む（無ければ null）</summary>
    public static PaceAssets Load()
    {
        if (_cached == null) _cached = Resources.Load<PaceAssets>(ResourcePath);
        return _cached;
    }

    /// <summary>出来事用の AttackRunner のプレハブ（無ければ null）</summary>
    public static AttackRunner RunnerPrefab()
    {
        PaceAssets a = Load();
        if (a != null && a.HazardRunner != null) return a.HazardRunner;
        return Resources.Load<AttackRunner>(RunnerPath);
    }

    /// <summary>出来事用の AttackRunner を、今のシーンに1つ作る（プレハブが無ければ null）</summary>
    public static AttackRunner CreateRunner()
    {
        AttackRunner prefab = RunnerPrefab();
        if (prefab == null) return null;
        AttackRunner runner = Instantiate(prefab);
        runner.name = "PaceHazardRunner";
        runner.gameObject.SetActive(true);
        return runner;
    }
}
