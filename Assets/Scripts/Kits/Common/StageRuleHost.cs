using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 【仮】ステージのギミックを動かす係（2026-11-19。docs/app-kits-1-design.md E3・app-kits-2-design.md 1-2 の片づけの表）。担当0 の物。
// ・StagePacer と同じやり方で、StageDirector があるシーンに実行時に自分で付く（シーン・ビルダーは変えない）。テスト用のシーンには付かない
// ・StagePaceTable.Enabled = false（StagePacer が無い）でも動く（場面は、ボスの登場までは Intro、そのあとは Done）
// ・片づけの表
//   | とき | 危ない物 | 置いた物 | 倍率 | 飾り |
//   | 最初の 3秒 | 出さない | 出さない | — | 出す |
//   | ③ 出来事・④ ひと休み | 新しく出さない | そのまま | そのまま | そのまま |
//   | ボスの登場（Done） | 全部消す | 全部消す | そのまま | そのまま |
//   | ボスが動き出して 4秒 | 出さない | 出さない | — | — |
//   | 50％（形態変化） | 全部消す。3秒は出さない | 全部消す | そのまま | そのまま |
//   | 100％・やり直し（End） | 全部消す | 全部消す | 全部外す | 消す |
// ・100％（VirusGauge.AnyFilled）では、ギミックが無いステージでも、倍率（StatMods）・メーター・印・能力の封印を全部外す
// ・やり直し（リトライ）はシーンを読み直すので、OnDestroy で End が呼ばれる

/// <summary>ステージのギミックを動かす係（StageDirector があるシーンに、実行時に自分で付く）</summary>
public class StageRuleHost : MonoBehaviour
{
    /// <summary>片づけの種類</summary>
    public enum Layer { Hazard, Placed, Decor }

    /// <summary>【仮】最初に何も出さない秒数</summary>
    public const float StartQuiet = 3f;
    /// <summary>【仮】ボスが動き出してから出さない秒数</summary>
    public const float BossQuiet = 4f;
    /// <summary>【仮】形態変化のあと、危ない物を出さない秒数</summary>
    public const float TransitionQuiet = 3f;
    /// <summary>【仮】置いた物の数の上限</summary>
    public const int MaxPlaced = 4;

    /// <summary>今のステージの係（無ければ null）</summary>
    public static StageRuleHost Current { get; private set; }

    /// <summary>キットのラベル</summary>
    public string Label { get; private set; }
    /// <summary>動いているギミック</summary>
    public StageRule Rule { get; private set; }
    public StageRuleContext Context { get; private set; }

    StageDirector _director;
    BossController _boss;
    readonly List<GameObject> _hazards = new List<GameObject>();
    readonly List<GameObject> _placed = new List<GameObject>();
    readonly List<GameObject> _decor = new List<GameObject>();
    AttackRunner _runner;
    bool _runnerTried;
    bool _ended;
    bool _begun;
    bool _doneSent;
    float _startAt;
    float _bossAt = -1f;
    float _quietUntil;

    // ───────── 実行時に自分で付く ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Current = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        VirusGauge.AnyFilled -= OnAnyFilled;
        VirusGauge.AnyFilled += OnAnyFilled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToFirstScene() => TryAttach(SceneManager.GetActiveScene());

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach(scene);

    static void TryAttach(Scene scene)
    {
        if (!AppKits.Enabled || !scene.IsValid()) return;
        if (Current != null && Current.gameObject.scene == scene) return;
        StageDirector director = FindObjectOfType<StageDirector>();
        if (director == null || director.gameObject.scene != scene || !director.isActiveAndEnabled || director.Plan == null) return;
        StagePlan plan = director.Plan;
        if (plan.IsFinal) return; // Terminal のステージには、ギミックは無い
        string appName = plan.App != null ? plan.App.DisplayName ?? "" : "";
        string bundle = plan.App != null && plan.App.BundleIdPrefixes != null && plan.App.BundleIdPrefixes.Length > 0 ? plan.App.BundleIdPrefixes[0] ?? "" : "";
        string label = AppKits.FindLabel(appName, bundle);
        if (label == null || !StageRules.Has(label) || !AppKits.IsOn(label, KitPart.Rule)) return;
        var go = new GameObject("StageRuleHost");
        SceneManager.MoveGameObjectToScene(go, scene);
        var host = go.AddComponent<StageRuleHost>();
        host.Setup(director, label, appName, bundle);
    }

    /// <summary>100％：倍率・メーター・印を全部外す（ギミックが無いステージでも）。ギミックがあれば End</summary>
    static void OnAnyFilled(VirusGauge gauge)
    {
        if (gauge == null || gauge.GetComponent<BossController>() == null) return;
        if (Current != null) Current.EndRule();
        StatMods.ClearAll();
        KitMeter.ClearAll();
        KitPips.ClearAll();
        KitPlayer.Unseal(); // 封印も 100％で戻す
        KitGuards.ClearAll(); // 被弾の見張りも外す
    }

    void Setup(StageDirector director, string label, string appName, string bundle)
    {
        Current = this;
        _director = director;
        Label = label;
        StagePlan plan = director.Plan;
        RunState run = GameSession.Instance != null ? GameSession.Instance.Run : null;
        int stage = run != null ? run.CurrentIndex + 1 : 1;
        var pc = FindObjectOfType<PlayerController>();
        Context = new StageRuleContext
        {
            Host = this,
            Label = label,
            AppName = appName,
            BundleId = bundle,
            Director = director,
            Player = pc != null ? pc.transform : null,
            PlayerHealth = pc != null ? pc.GetComponent<Health>() : null,
            StageNumber = stage,
            Strength = Mathf.Clamp(StagePaceTable.For(stage, plan.IsFinal).Strength, 1, 3),
        };
        ReadLook(director, plan);
        Rule = StageRules.Create(label);
        if (Rule != null) Rule.Ctx = Context;
    }

    void ReadLook(StageDirector director, StagePlan plan)
    {
        AttackRunner boss = director.Boss != null ? director.Boss.GetComponent<AttackRunner>() : null;
        Context.Theme = plan.App != null ? plan.App.Theme : null;
        if (boss != null && boss.Skin != SkinId.None)
        {
            Context.Skin = boss.Skin;
            Context.Icon = boss.SkinIcon;
            if (boss.SkinTheme != null) Context.Theme = boss.SkinTheme;
        }
        else if (plan.App != null) Context.Skin = SkinTables.ForApp(Context.AppName, Context.BundleId);
        if (Context.Skin == SkinId.None) Context.Skin = SkinId.Generic;
        if (Context.Icon == null) Context.Icon = plan.Icon16 != null ? plan.Icon16 : plan.Profile != null ? plan.Profile.Icon : null;
    }

    void Start()
    {
        _startAt = Time.time;
        if (Rule == null) return;
        StagePacer pacer = StagePacer.Current;
        if (pacer != null && pacer.gameObject.scene == gameObject.scene)
        {
            pacer.PhaseChanged += OnPacerPhase;
            Context.Phase = pacer.Phase;
        }
        if (_director.Entrance != null) _director.Entrance.Appeared += OnBossAppeared;
        _begun = true;
        Safe(() => Rule.Begin(Context));
    }

    void OnDestroy()
    {
        EndRule();
        if (StagePacer.Current != null) StagePacer.Current.PhaseChanged -= OnPacerPhase;
        if (_director != null && _director.Entrance != null) _director.Entrance.Appeared -= OnBossAppeared;
        if (_boss != null)
        {
            _boss.TransitionStarted -= OnBossTransition;
            _boss.PhaseChanged -= OnBossPhase;
        }
        if (Current == this) Current = null;
    }

    // ───────── 毎フレーム ─────────

    void Update()
    {
        if (_ended || Rule == null || !_begun) return;
        Context.Elapsed = Time.time - _startAt;
        if (_bossAt >= 0f) Context.BossFightSeconds = Time.time - _bossAt;

        // StagePacer が無いとき：ボスの登場で Done
        if (!_doneSent && _director != null && _director.Entrance != null && _director.Entrance.IsTriggered) SendPhase(PacePhase.Done);
        // 登場の合図が無いシーン：ボスが動いていればボス戦
        if (_bossAt < 0f && _director != null && _director.Entrance == null && _director.Boss != null && _director.Boss.isActiveAndEnabled) OnBossAppeared(_director.Boss);

        if (Paused) return;
        Safe(() => Rule.Tick(Time.deltaTime));
    }

    /// <summary>Tick を呼ばない間（ボスの登場の演出・形態変化・ライフ0・正常化のあと）</summary>
    bool Paused
    {
        get
        {
            if (Context.PlayerHealth != null && Context.PlayerHealth.IsDead) return true;
            if (_doneSent && _bossAt < 0f) return true; // 登場の演出
            if (_boss != null && _boss.State != BossState.Fighting) return true;
            return false;
        }
    }

    internal bool CanSpawnHazard
    {
        get
        {
            if (_ended || Paused) return false;
            if (Time.time - _startAt < StartQuiet || Time.time < _quietUntil) return false;
            if (!Context.InBossFight)
                return !_doneSent && Context.Phase != PacePhase.Event && Context.Phase != PacePhase.Rest;
            if (Time.time - _bossAt < BossQuiet) return false;
            if (CustomEffect.AnyLingering) return false;
            return !Context.BossInMove;
        }
    }

    internal bool CanPlace
    {
        get
        {
            if (_ended || Paused) return false;
            if (Time.time - _startAt < StartQuiet || Time.time < _quietUntil) return false;
            if (Context.InBossFight && Time.time - _bossAt < BossQuiet) return false;
            return Count(Layer.Placed) < MaxPlaced;
        }
    }

    /// <summary>今の画面</summary>
    internal Rect PlayArea
    {
        get
        {
            StagePacer p = StagePacer.Current;
            if (Context.InBossFight) return BattleArena.GetBounds();
            if (p != null) return p.PlayArea;
            return ScreenUi.ViewRect();
        }
    }

    // ───────── 出来事 ─────────

    void OnPacerPhase(PacePhase phase) => SendPhase(phase);

    void SendPhase(PacePhase phase)
    {
        if (_ended) return;
        if (phase == PacePhase.Done)
        {
            if (_doneSent) return;
            _doneSent = true;
            Clear(Layer.Hazard);
            Clear(Layer.Placed);
        }
        Context.Phase = phase;
        Safe(() => Rule.OnPhase(phase));
    }

    void OnBossAppeared(BossController boss)
    {
        if (_ended || _bossAt >= 0f) return;
        if (!_doneSent) SendPhase(PacePhase.Done);
        _bossAt = Time.time;
        _boss = boss;
        Context.Boss = boss;
        Context.InBossFight = true;
        Context.BossFightSeconds = 0f;
        if (boss != null)
        {
            boss.TransitionStarted += OnBossTransition;
            boss.PhaseChanged += OnBossPhase;
        }
        Safe(() => Rule.OnBossAppeared(boss));
    }

    void OnBossTransition()
    {
        Clear(Layer.Hazard);
        Clear(Layer.Placed);
        float t = _boss != null && _boss.Profile != null ? _boss.Profile.TransitionTime : 1.5f;
        _quietUntil = Time.time + t + TransitionQuiet;
    }

    void OnBossPhase(int phase)
    {
        _quietUntil = Mathf.Max(_quietUntil, Time.time + TransitionQuiet);
        Safe(() => Rule.OnBossPhase(phase));
    }

    /// <summary>ギミックを終わらせて、出した物を全部片づける（何回呼んでもよい）</summary>
    internal void EndRule()
    {
        if (_ended) return;
        _ended = true;
        if (Rule != null && _begun) Safe(() => Rule.End());
        Clear(Layer.Hazard);
        Clear(Layer.Placed);
        Clear(Layer.Decor);
        if (_runner != null) Destroy(_runner.gameObject);
        _runner = null;
        StatMods.ClearAll();
        KitMeter.ClearAll();
        KitPips.ClearAll();
        KitPlayer.Unseal();
    }

    // ───────── 片づけ ─────────

    /// <summary>危ない物を出す AttackRunner（1回だけ作る。プレハブが無ければ null）</summary>
    internal AttackRunner HazardRunner
    {
        get
        {
            if (_ended) return null;
            if (_runner != null || _runnerTried) return _runner;
            _runnerTried = true;
            AttackRunner prefab = PaceAssets.RunnerPrefab();
            if (prefab == null) return null;
            _runner = Instantiate(prefab, PlayArea.center, Quaternion.identity);
            _runner.name = "KitRuleHazardRunner";
            _runner.gameObject.SetActive(true);
            SceneManager.MoveGameObjectToScene(_runner.gameObject, gameObject.scene);
            _runner.Target = Context.Player;
            _runner.Skin = Context.Skin;
            _runner.SkinTheme = Context.Theme;
            _runner.SkinIcon = Context.Icon;
            return _runner;
        }
    }

    List<GameObject> ListOf(Layer layer) => layer == Layer.Hazard ? _hazards : layer == Layer.Placed ? _placed : _decor;

    internal void Track(GameObject go, Layer layer)
    {
        if (go == null) return;
        if (_ended) { Destroy(go); return; }
        List<GameObject> list = ListOf(layer);
        list.RemoveAll(g => g == null);
        if (!list.Contains(go)) list.Add(go);
    }

    internal int Count(Layer layer)
    {
        List<GameObject> list = ListOf(layer);
        list.RemoveAll(g => g == null);
        int n = list.Count;
        if (layer == Layer.Hazard && _runner != null) n += _runner.SpawnedCount;
        return n;
    }

    internal void Clear(Layer layer)
    {
        List<GameObject> list = ListOf(layer);
        foreach (var go in list) if (go != null) Destroy(go);
        list.Clear();
        if (layer == Layer.Hazard && _runner != null)
        {
            _runner.Cancel();
            _runner.ClearSpawned();
        }
    }

    void Safe(Action a)
    {
        try { a(); }
        catch (Exception e) { Debug.LogException(e); } // キットの例外で、ステージを止めない
    }
}
