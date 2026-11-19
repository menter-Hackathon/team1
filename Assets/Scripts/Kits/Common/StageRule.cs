using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】ステージのギミック（2026-11-19。docs/app-kits-1-design.md E3・app-kits-2-design.md 1-2）。担当0 の物。
// ・ステージの最初から 100％まで生きる決まり。StageRuleHost が、StageDirector のあるシーンに実行時に付いて呼ぶ。
// ・登録：キットのファイルの中で StageRules.Register("Slack", () => new SlackRule())（[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]）。
//   ラベルは AppKits.Register と同じ。名前を AppKits に登録していなければ、Register の名前つきの形を使う。
// ・片づけ：出した物は ctx.TrackHazard／TrackPlaced／TrackDecor に渡す（危ない物は ctx.Hazard で出してもよい）。
//   StageRuleHost が片づけの表（app-kits-2 1-2）のとおりに消す。倍率（StatMods）は 100％で全部外れる。
// ・Terminal のステージ（ラスボス）には付かない。AppKits.Enabled = false・キットの Rule のスイッチが切れていても付かない。

/// <summary>ステージのギミック1つ（キットが継ぐ）</summary>
public abstract class StageRule
{
    /// <summary>このギミックの状態と道具（Begin の前に入る）</summary>
    public StageRuleContext Ctx { get; internal set; }

    /// <summary>ステージの最初（StageRuleHost の Start）</summary>
    public virtual void Begin(StageRuleContext ctx) { }
    /// <summary>毎フレーム（ボスの登場の演出・形態変化の間・ライフ0 の間は呼ばれない）</summary>
    public virtual void Tick(float dt) { }
    /// <summary>場面が変わった（Done ＝ ボスの登場。危ない物・置いた物は StageRuleHost が消したあと）</summary>
    public virtual void OnPhase(PacePhase phase) { }
    /// <summary>ボス戦が始まった（ボスが姿を見せた）</summary>
    public virtual void OnBossAppeared(BossController boss) { }
    /// <summary>ボスの形態が変わった（2 ＝ 第2形態。形態変化の演出が終わったとき）</summary>
    public virtual void OnBossPhase(int phase) { }
    /// <summary>100％・ステージの終わり・やり直し。出した物と倍率を全部片づける（何回呼ばれてもよいように作る）</summary>
    public virtual void End() { }
}

/// <summary>ギミックが見る状態と、使う道具（StageRuleHost が作る）</summary>
public class StageRuleContext
{
    internal StageRuleHost Host;

    /// <summary>キットのラベル（"Slack" など）</summary>
    public string Label { get; internal set; } = "";
    /// <summary>ステージのアプリの名前・バンドルID</summary>
    public string AppName { get; internal set; } = "";
    public string BundleId { get; internal set; } = "";
    /// <summary>場面を進める係（StagePaceTable.Enabled = false のときは null）</summary>
    public StagePacer Pacer => StagePacer.Current;
    public StageDirector Director { get; internal set; }
    public Transform Player { get; internal set; }
    public Health PlayerHealth { get; internal set; }
    /// <summary>1〜3（StagePaceTable の強さ。1＝弱い）</summary>
    public int Strength { get; internal set; } = 1;
    public int StageNumber { get; internal set; } = 1;
    public SkinId Skin { get; internal set; } = SkinId.Generic;
    public AppTheme Theme { get; internal set; }
    public Sprite Icon { get; internal set; }

    /// <summary>今の画面（1画面は部屋、ほかはカメラ）</summary>
    public Rect PlayArea => Host != null ? Host.PlayArea : BattleArena.GetBounds();
    /// <summary>戦う部屋（ボス部屋）の範囲</summary>
    public Rect Arena => BattleArena.GetBounds();
    /// <summary>今の場面（StagePacer が無ければ、ボスの登場までは Intro、そのあとは Done）</summary>
    public PacePhase Phase { get; internal set; } = PacePhase.Intro;
    /// <summary>ステージが始まってからの秒数</summary>
    public float Elapsed { get; internal set; }

    /// <summary>ボス（ボス戦の前は null）</summary>
    public BossController Boss { get; internal set; }
    /// <summary>ボス戦の間か（ボスが姿を見せてから、100％まで）</summary>
    public bool InBossFight { get; internal set; }
    /// <summary>ボスが姿を見せてからの秒数（ボス戦の前は 0）</summary>
    public float BossFightSeconds { get; internal set; }
    /// <summary>ボスの形態（ボス戦の前は 0）</summary>
    public int BossPhase => Boss != null ? Boss.Phase : 0;
    /// <summary>ボスが形態変化の演出中か</summary>
    public bool BossTransitioning => Boss != null && Boss.State == BossState.Transition;
    /// <summary>ボスが技の途中か（予告も含む）</summary>
    public bool BossInMove => Boss != null && Boss.Runner != null && Boss.Runner.IsRunning;

    /// <summary>
    /// 今、新しい危ない物を出してよいか（片づけの表と公平さの決まりをまとめた物）：
    /// 最初の 3秒・③ 出来事・④ ひと休み・ボスの登場の演出・ボスが動き出して 4秒・形態変化のあと 3秒・
    /// ボスの場に残る技の間・ボスの技の途中・ライフ0 の間は false
    /// </summary>
    public bool CanSpawnHazard => Host != null && Host.CanSpawnHazard;
    /// <summary>今、新しく物を置いてよいか（最初の 3秒・ボスの登場の演出・ボスが動き出して 4秒・形態変化のあと 3秒は false。同時に 4つまで）</summary>
    public bool CanPlace => Host != null && Host.CanPlace;

    /// <summary>危ない物を出す AttackRunner（出来事用と同じ物。そのステージのスキン・色・アイコン・狙う相手が入っている）。無ければ null</summary>
    public AttackRunner Hazard => Host != null ? Host.HazardRunner : null;

    /// <summary>危ない物として覚える（ボスの登場・50％・100％で消す）。渡した物を返す</summary>
    public GameObject TrackHazard(GameObject go) { Host?.Track(go, StageRuleHost.Layer.Hazard); return go; }
    /// <summary>置いた物（箱・四角・拾える物）として覚える（ボスの登場・50％・100％で消す）</summary>
    public GameObject TrackPlaced(GameObject go) { Host?.Track(go, StageRuleHost.Layer.Placed); return go; }
    /// <summary>飾り（HUD・背景の物）として覚える（100％・ステージの終わりで消す）</summary>
    public GameObject TrackDecor(GameObject go) { Host?.Track(go, StageRuleHost.Layer.Decor); return go; }

    /// <summary>今ある危ない物・置いた物の数（ctx.Hazard で出した物も数える）</summary>
    public int HazardCount => Host != null ? Host.Count(StageRuleHost.Layer.Hazard) : 0;
    public int PlacedCount => Host != null ? Host.Count(StageRuleHost.Layer.Placed) : 0;

    /// <summary>危ない物を全部消す（ctx.Hazard で出した物も）</summary>
    public void ClearHazards() => Host?.Clear(StageRuleHost.Layer.Hazard);
    public void ClearPlaced() => Host?.Clear(StageRuleHost.Layer.Placed);
    public void ClearDecor() => Host?.Clear(StageRuleHost.Layer.Decor);

    /// <summary>倍率・メーターの鍵（"slack.unread" など）。キットの名前で始まる</summary>
    public string Key(string name) => (Label ?? "kit").ToLowerInvariant() + "." + name;
}

/// <summary>ステージのギミックの登録所</summary>
public static class StageRules
{
    static readonly Dictionary<string, Func<StageRule>> Factories = new Dictionary<string, Func<StageRule>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>ギミックを登録する（アプリの名前は AppKits.Register で登録した物を使う）</summary>
    public static void Register(string label, Func<StageRule> factory)
    {
        if (string.IsNullOrEmpty(label) || factory == null) return;
        Factories[label] = factory;
    }

    /// <summary>ギミックを登録する（アプリの名前も一緒に。AppKits.Register もする）</summary>
    public static void Register(string label, string[] appNames, string[] bundleIds, Func<StageRule> factory)
    {
        if (string.IsNullOrEmpty(label)) return;
        AppKits.Register(label, appNames, bundleIds);
        Register(label, factory);
    }

    /// <summary>そのキットのギミックが登録されているか</summary>
    public static bool Has(string label) => !string.IsNullOrEmpty(label) && Factories.ContainsKey(label);

    /// <summary>今のステージのギミック（無ければ null）</summary>
    public static StageRule Current => StageRuleHost.Current != null ? StageRuleHost.Current.Rule : null;

    /// <summary>ギミックを作る（無い・スイッチが切れている・作れないときは null）</summary>
    internal static StageRule Create(string label)
    {
        if (!Has(label) || !AppKits.IsOn(label, KitPart.Rule)) return null;
        try { return Factories[label](); }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }
}
