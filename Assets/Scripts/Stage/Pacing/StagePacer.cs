using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 【仮】ステージの山と谷（2026-11-19。docs/stage-pacing-design.md 2章・7章・10章）。担当1（流れ）の物。
// ・StageDirector があるシーンに、実行時に自分で付く（シーン・ビルダー・StageDirector は変えない）
// ・雑魚の時間を5つの場面（入り → 盛り上がり → 出来事 → ひと休み → 最高潮）に分ける。場面は時間ではなく、進み具合で切り替える
//   1画面：場面ごとの予算を出し終えて、全員倒したら次へ。横スクロール：一番右まで進んだ x。ウィンドウ移動：部屋
// ・雑魚は ZakoSpawner を止めて（Paused）、この係が SpawnNow で組・群れ・はさみうちに出す。ノルマ（右上のゲージ）は最初に決めて変えない
// ・ボスの登場（Done）で、出した物（出来事・印）を全部片づける
// ・StagePaceTable.Enabled = false なら付かない（今までの出し方）
// 数・秒数はすべて【仮】（StagePaceTable）。

/// <summary>1ステージの雑魚の時間の場面（2-1）。Done＝ボスの登場が始まった（全部片づける）</summary>
public enum PacePhase { Intro, Build, Event, Rest, Climax, Done }

/// <summary>場面を進める係。StageDirector があるシーンに、実行時に自分で付く</summary>
public class StagePacer : MonoBehaviour
{
    // ───────── つなぎ目（10-2。名前と形は変えない） ─────────

    /// <summary>今のステージの係（無ければ null）</summary>
    public static StagePacer Current { get; protected set; }
    /// <summary>ステージごとに1回（StagePacer の Start の中で出す）。ここで購読する</summary>
    public static event Action<StagePacer> Started;
    protected static void RaiseStarted(StagePacer pacer)
    {
        if (Started == null) return;
        foreach (Action<StagePacer> h in Started.GetInvocationList())
        {
            try { h(pacer); }
            catch (Exception e) { Debug.LogException(e); } // ほかの担当の部品の例外で、流れを止めない
        }
    }

    public PacePhase Phase { get; protected set; } = PacePhase.Intro;
    /// <summary>場面が変わったとき（Done ＝ ボスの登場が始まった）</summary>
    public event Action<PacePhase> PhaseChanged;
    protected void RaisePhaseChanged(PacePhase phase)
    {
        if (PhaseChanged == null) return;
        foreach (Action<PacePhase> h in PhaseChanged.GetInvocationList())
        {
            try { h(phase); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>0〜1（1画面＝倒した数、ほか＝進んだ x）</summary>
    public float Progress { get; protected set; }
    /// <summary>1〜（RunState.CurrentIndex + 1）</summary>
    public int StageNumber { get; protected set; } = 1;
    /// <summary>ラスボス（Terminal）のステージか</summary>
    public bool IsFinal { get; protected set; }
    /// <summary>そのステージのアプリの名前（スクリーンタイムの名前。2026-11-19：アプリのキットが見る）</summary>
    public string AppName { get; protected set; } = "";
    /// <summary>そのステージのアプリのバンドルID（無ければ空）</summary>
    public string BundleId { get; protected set; } = "";
    /// <summary>そのステージのスキン（SkinTables.ForApp。StageDirector がボスに入れるのと同じ決め方）</summary>
    public SkinId Skin { get; protected set; } = SkinId.Generic;
    public AppTheme Theme { get; protected set; }
    public Sprite Icon { get; protected set; }
    /// <summary>今の画面（1画面は部屋、ほかはカメラ）</summary>
    public Rect PlayArea { get; protected set; }
    public Transform Player { get; protected set; }
    public ZakoSpawner Spawner { get; protected set; }
    /// <summary>ボスまでの残りの数（ノルマの無いステージは -1）</summary>
    public int Remaining { get; protected set; } = -1;

    // ───────── 中身 ─────────

    /// <summary>ステージの形。None：雑魚の出し方は変えない（ノルマも入口も無いシーン）</summary>
    enum Layout { None, OneScreen, Scroll, Rooms }
    /// <summary>雑魚の種類（出す直前にひな形の番号へ。無ければ歩く雑魚）</summary>
    enum Kind { Walker, Shooter, Small, Elite, Flyer, Charger }
    /// <summary>どの端から出すか</summary>
    enum SideRule { Far, Opposite, Ahead, Behind, Scatter }
    enum JobResult { Spawned, Dropped, Retry }

    /// <summary>出し方の1段（組・群れ・1体）</summary>
    class Step
    {
        public Kind[] Kinds;
        public SideRule Side;
        public float Spacing; // 1体ずつの間（秒）
        public float Gap;     // 出し終えてから、次の段までの秒数
        public int Cap;       // 同時の数（これを越えるなら待つ）
    }

    /// <summary>これから出す1体</summary>
    class Job
    {
        public Kind Kind;
        public int Side;
        public int Slot;
        public bool Scatter;
        public float At;
        public int Tries;
    }

    [Serializable] class CameraDump { public float[] _roomCentersX; }
    [Serializable] class GateDump { public float _triggerX; }

    StageDirector _director;
    Health _playerHealth;
    EnemyWave _wave;
    BossEntrance _entrance;
    StagePaceTable.Row _row;
    Layout _layout;
    int[] _buildGroups;
    int[] _climaxSwarms;
    int _eventBudget;
    float[] _rooms;
    float _startX, _maxX, _gateX;
    float _phaseAt;
    int _spawnedAtPhase;

    readonly List<Step> _plan = new List<Step>();
    readonly List<Job> _queue = new List<Job>();
    int _planIndex;
    float _planReadyAt;
    float _noGapFloor;
    float _pendingGap;
    int _lastSide = 1;
    int _gateGroup;
    float _zeroSince = float.PositiveInfinity;
    float _lastActivity;
    bool _fallback;

    StageEvent _event;
    bool _eventRunning;
    bool _eventSpawnsZako;
    bool _eventTried;
    float _eventStartAt;
    float _eventAt;
    AttackRunner _runner;

    float _room3At = -1f;
    bool _lastKillShown;
    PaceMarks _marks;

    int PhaseSpawned => Spawner != null ? Spawner.Spawned - _spawnedAtPhase : 0;
    bool PlanDone => _queue.Count == 0 && _planIndex >= _plan.Count;
    float PlayerX => Player != null ? Player.position.x : PlayArea.center.x;

    // ───────── 実行時に自分で付く（10-1） ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Current = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 最初のシーン（ステージのシーンを直接再生したとき）。sceneLoaded で付いていれば何もしない
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToFirstScene() => TryAttach(SceneManager.GetActiveScene());

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach(scene);

    static void TryAttach(Scene scene)
    {
        if (!StagePaceTable.Enabled || !scene.IsValid()) return;
        if (Current != null && Current.gameObject.scene == scene) return;
        StageDirector director = FindObjectOfType<StageDirector>();
        // テスト用のシーン（StageDirector が無い）・ランを始められなかったシーンには付かない
        if (director == null || director.gameObject.scene != scene || !director.isActiveAndEnabled || director.Plan == null) return;
        var go = new GameObject("StagePacer");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<StagePacer>().Setup(director);
    }

    /// <summary>付いた直後（シーンの Awake のあと、Start の前）。ノルマを決めて、雑魚を出すのを止める</summary>
    void Setup(StageDirector director)
    {
        Current = this;
        _director = director;
        _wave = director.Wave;
        _entrance = director.Entrance;
        _marks = new PaceMarks(transform);
        Spawner = FindObjectOfType<ZakoSpawner>();
        var pc = FindObjectOfType<PlayerController>();
        Player = pc != null ? pc.transform : null;
        _playerHealth = Player != null ? Player.GetComponent<Health>() : null;

        StagePlan plan = director.Plan;
        RunState run = GameSession.Instance != null ? GameSession.Instance.Run : null;
        StageNumber = run != null ? run.CurrentIndex + 1 : 1;
        IsFinal = plan.IsFinal;
        ReadLook(director, plan);
        _row = StagePaceTable.For(StageNumber, IsFinal);
        _buildGroups = _row.Build;
        _climaxSwarms = _row.Climax;
        _eventBudget = _row.Event + Mathf.Max(0, SafeExtraZako());

        bool gate = _entrance != null && _entrance.WaitsForGate;
        if (Spawner == null) _layout = Layout.None;
        else if (gate) _layout = ReadRooms() ? Layout.Rooms : Layout.Scroll;
        else if (_wave != null && Spawner.Quota > 0 && Spawner.Wave == _wave) _layout = Layout.OneScreen;
        else _layout = Layout.None;

        if (_layout == Layout.None) return;
        Spawner.Paused = true; // 自分からは出させない（出すのはこの係）
        if (_layout == Layout.OneScreen) PrepareQuota();
        UpdateArea();
    }

    /// <summary>全部の数を最初に決める（2-2：ゲージは戻らないし、途中で伸びない）</summary>
    void PrepareQuota()
    {
        int planned = Sum(_buildGroups) + _eventBudget + Sum(_climaxSwarms) + (_row.Elite ? 1 : 0);
        if (StagePaceTable.MoreZako)
        {
            Spawner.AddQuota(planned - Spawner.Quota); // 少ないときは何もしない（多い分は最高潮で出し切る）
            return;
        }
        // 【要相談①】増やさない：今の数のまま、表の割合で場面だけ分ける
        if (planned <= Spawner.Quota) return;
        float k = Spawner.Quota / (float)planned;
        _buildGroups = Scale(_buildGroups, k);
        _climaxSwarms = Scale(_climaxSwarms, k);
        _eventBudget = Mathf.Max(1, Mathf.RoundToInt(_eventBudget * k));
    }

    /// <summary>スキン・色・アイコン：ボスの AttackRunner に StageDirector が入れた値を使う（同じになるように）</summary>
    void ReadLook(StageDirector director, StagePlan plan)
    {
        Theme = plan.App != null ? plan.App.Theme : null;
        AppName = plan.App != null ? plan.App.DisplayName ?? "" : "";
        BundleId = plan.App != null && plan.App.BundleIdPrefixes != null && plan.App.BundleIdPrefixes.Length > 0 ? plan.App.BundleIdPrefixes[0] ?? "" : "";
        AttackRunner boss = director.Boss != null ? director.Boss.GetComponent<AttackRunner>() : null;
        if (boss != null && boss.Skin != SkinId.None)
        {
            Skin = boss.Skin;
            Icon = boss.SkinIcon;
            if (boss.SkinTheme != null) Theme = boss.SkinTheme;
        }
        else if (plan.App != null)
        {
            string bundle = plan.App.BundleIdPrefixes != null && plan.App.BundleIdPrefixes.Length > 0 ? plan.App.BundleIdPrefixes[0] : null;
            Skin = SkinTables.ForApp(plan.App.DisplayName, bundle);
        }
        if (Skin == SkinId.None) Skin = SkinId.Generic;
        if (Icon == null)
            Icon = plan.Visual != null ? (plan.Visual.Icon16 != null ? plan.Visual.Icon16 : plan.Visual.Icon12)
                : plan.Icon16 != null ? plan.Icon16 : plan.Profile != null ? plan.Profile.Icon : null;
    }

    void Start()
    {
        _lastActivity = Time.time;
        _startX = Player != null ? Player.position.x : 0f;
        _maxX = _startX;
        if (_layout == Layout.Scroll || _layout == Layout.Rooms) _gateX = ReadGateX();
        if (_layout == Layout.OneScreen) Remaining = Mathf.Max(0, _wave.Expected - _wave.Killed); // Started の中で読めるように
        UpdateArea();
        UpdateProgress();
        RaiseStarted(this);
    }

    void OnDestroy()
    {
        if (Phase != PacePhase.Done) StopEvent();
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (Phase == PacePhase.Done) return;
        UpdateArea();
        UpdateProgress();
        if (_layout == Layout.OneScreen) LastKillCheck();
        if (BossStarted()) { Finish(); return; }
        if (_eventRunning) TickEvent();
        if (_playerHealth != null && _playerHealth.IsDead) return; // ライフ0（ゲームオーバーの画面まで）は出さない（今までと同じ）
        switch (_layout)
        {
            case Layout.OneScreen: TickOneScreen(); break;
            case Layout.Scroll:
            case Layout.Rooms: TickGate(); break;
        }
        if (_layout != Layout.None && !_fallback) RunQueue();
        if (_layout == Layout.OneScreen)
            _marks.Tick(Spawner.AliveList, Remaining > 0 && Remaining <= StagePaceTable.MarkFrom);
    }

    // ───────── 1画面（2-2） ─────────

    void TickOneScreen()
    {
        int alive = _wave.Remaining;
        Remaining = Mathf.Max(0, _wave.Expected - _wave.Killed);
        TrackZero(alive);
        Watchdog(alive);
        if (_fallback) return;
        switch (Phase)
        {
            case PacePhase.Intro:
                if (alive == 0) EnterBuild(); // 最初から置いてある雑魚を倒し切った
                break;
            case PacePhase.Build:
                RunPlan(alive);
                if (PlanDone && alive == 0) EnterEvent();
                break;
            case PacePhase.Event:
                RunPlan(alive);
                // 「予算を倒し切った」と「出来事が終わった」の両方で終わる
                if (!_eventRunning && PlanDone && alive == 0 && (PhaseSpawned >= _eventBudget || Spawner.QuotaUsedUp))
                {
                    if (_row.Rest) SetPhase(PacePhase.Rest);
                    else EnterClimax(); // 【要相談⑧】Terminal はひと休みなし
                }
                break;
            case PacePhase.Rest:
                if (Time.time - _phaseAt >= StagePaceTable.RestSeconds) EnterClimax();
                break;
            case PacePhase.Climax:
                RunPlan(alive);
                // 表より多く残っていたら（数を増やさない設定など）、組で出し切る
                if (PlanDone && !Spawner.QuotaUsedUp)
                    AddGroups(Mathf.Min(3, Spawner.Quota - Spawner.Spawned), StagePaceTable.BuildInterval, StagePaceTable.ClimaxCap, false);
                break;
        }
    }

    void EnterBuild()
    {
        SetPhase(PacePhase.Build);
        ClearPlan();
        for (int i = 0; i < _buildGroups.Length; i++)
            _plan.Add(new Step
            {
                Kinds = GroupKinds(_buildGroups[i], i, _buildGroups.Length, true),
                Side = SideRule.Far,
                Spacing = StagePaceTable.GroupStep,
                Gap = StagePaceTable.BuildInterval,
                Cap = StagePaceTable.BuildCap,
            });
        ArmPlan(StagePaceTable.NoGapDelay);
    }

    void EnterEvent()
    {
        SetPhase(PacePhase.Event);
        ClearPlan();
        StartEvent();
        if (_event == null) AddGroups(_eventBudget, StagePaceTable.BuildInterval, StagePaceTable.BuildCap, true); // 出来事が無い間は ② の出し方
        else if (!_eventSpawnsZako) AddSingles(_eventBudget); // 雨・柱床：1体ずつ・撃つ雑魚なし
        ArmPlan(StagePaceTable.NoGapDelay);
    }

    void EnterClimax()
    {
        StopEvent();
        SetPhase(PacePhase.Climax);
        ClearPlan();
        bool gate = _layout != Layout.OneScreen;
        for (int i = 0; i < _climaxSwarms.Length; i++)
        {
            Kind[] kinds = Fill(Kind.Small, _climaxSwarms[i]);
            if (i > 0 && i == _climaxSwarms.Length - 1 && kinds.Length >= 2 && StageNumber >= StagePaceTable.FlyerFromStage) kinds[kinds.Length - 1] = Kind.Flyer;
            _plan.Add(new Step
            {
                Kinds = kinds,
                Side = gate ? (i == 0 ? SideRule.Ahead : SideRule.Behind) : (i == 0 ? SideRule.Far : SideRule.Opposite), // 2つ目ははさみうち
                Spacing = StagePaceTable.SwarmStep,
                Gap = gate ? StagePaceTable.GateSwarmGap : StagePaceTable.ClimaxGap,
                Cap = StagePaceTable.ClimaxCap,
            });
        }
        if (_row.Elite)
            _plan.Add(new Step { Kinds = new[] { Kind.Elite }, Side = gate ? SideRule.Ahead : SideRule.Far, Gap = StagePaceTable.ClimaxGap, Cap = StagePaceTable.ClimaxCap });
        ArmPlan(StagePaceTable.ClimaxLeadIn);
    }

    /// <summary>最後の1体：0.25秒のスローと白い光（7章）。紫が吸いこまれるのは担当2（Done を見る）</summary>
    void LastKillCheck()
    {
        if (_lastKillShown || _wave == null || !_wave.IsCleared) return;
        _lastKillShown = true;
        HitStop.Request(StagePaceTable.LastSlowSeconds, true);
        ScreenFx.WhiteFlash();
    }

    /// <summary>雑魚が1体もいないまま出せない時間が続いたら、今までの出し方に戻す（ボスが出なくなるのを防ぐ）</summary>
    void Watchdog(int alive)
    {
        if (_fallback) return;
        bool fine = Phase == PacePhase.Intro || Phase == PacePhase.Rest || _eventRunning || alive > 0 || Spawner.QuotaUsedUp || Spawner.IsStopped;
        if (fine) { _lastActivity = Time.time; return; }
        if (Time.time - _lastActivity < StagePaceTable.WatchdogSeconds) return;
        _fallback = true;
        ClearPlan();
        _queue.Clear();
        Spawner.SetWeights(0.7f, 0.3f);
        Spawner.SetPace(StagePaceTable.BuildInterval, StagePaceTable.BuildCap);
        Spawner.Paused = false;
        Debug.LogWarning("[StagePacer] 雑魚を出せないので、今までの出し方に戻した（ノルマは同じ）");
    }

    // ───────── 横スクロール・ウィンドウ移動（2-4） ─────────

    void TickGate()
    {
        int alive = Spawner.Alive;
        TrackZero(alive);
        PacePhase target = GateTarget();
        while (Phase < target) EnterGate(NextGatePhase(Phase));
        switch (Phase)
        {
            case PacePhase.Build:
                RunPlan(alive);
                if (PlanDone) AddGateGroup();
                break;
            case PacePhase.Event:
                if (!_eventTried && Time.time >= _eventStartAt)
                {
                    ClearPlan();
                    StartEvent();
                }
                if (_eventRunning && _eventSpawnsZako) break; // 群れ：雑魚は出来事が出す
                RunPlan(alive);
                if (PlanDone)
                {
                    if (_eventRunning) AddSingles(1); // 雨・柱床の間：1体ずつ・撃つ雑魚なし
                    else AddGateGroup();               // 出来事の前・あと（出来事が無い間も）
                }
                break;
            case PacePhase.Climax:
                RunPlan(alive);
                if (PlanDone) AddGateGroup(); // 群れのあとは、ボス部屋まで組を前から
                break;
        }
    }

    PacePhase NextGatePhase(PacePhase p)
    {
        var next = (PacePhase)((int)p + 1);
        if (next == PacePhase.Rest && !_row.Rest) next = PacePhase.Climax;
        return next;
    }

    void EnterGate(PacePhase p)
    {
        switch (p)
        {
            case PacePhase.Build:
                SetPhase(PacePhase.Build);
                ClearPlan();
                ArmPlan(StagePaceTable.NoGapDelay);
                break;
            case PacePhase.Event:
                SetPhase(PacePhase.Event);
                ClearPlan();
                _eventTried = false;
                _eventStartAt = Time.time + (_layout == Layout.Rooms ? StagePaceTable.RoomsEventDelay : 0f);
                break;
            case PacePhase.Rest:
                StopEvent(); // 区間を出たら終わり
                SetPhase(PacePhase.Rest);
                ClearPlan();
                _queue.Clear();
                break;
            default:
                EnterClimax();
                break;
        }
    }

    /// <summary>今いる場所の場面（戻らない）</summary>
    PacePhase GateTarget()
    {
        PacePhase p;
        if (_layout == Layout.Rooms)
        {
            // 部屋1＝①②、部屋2＝③、部屋3 の最初の 3秒＝④、残り＝⑤
            int room = RoomIndex(PlayerX);
            if (room <= 0) p = PlayerX < _rooms[0] ? PacePhase.Intro : PacePhase.Build;
            else if (room == 1) p = PacePhase.Event;
            else
            {
                if (_room3At < 0f) _room3At = Time.time;
                p = Time.time - _room3At < StagePaceTable.RestSeconds ? PacePhase.Rest : PacePhase.Climax;
            }
        }
        else
        {
            float pr = Progress;
            p = pr < StagePaceTable.ScrollBuildFrom ? PacePhase.Intro
                : pr < StagePaceTable.ScrollEventFrom ? PacePhase.Build
                : pr < StagePaceTable.ScrollRestFrom ? PacePhase.Event
                : pr < StagePaceTable.ScrollClimaxFrom ? PacePhase.Rest
                : PacePhase.Climax;
        }
        if (p == PacePhase.Rest && !_row.Rest) p = PacePhase.Climax;
        return p > Phase ? p : Phase;
    }

    int RoomIndex(float x)
    {
        int best = 0;
        for (int i = 1; i < _rooms.Length; i++)
            if (Mathf.Abs(x - _rooms[i]) < Mathf.Abs(x - _rooms[best])) best = i;
        return best;
    }

    void AddGateGroup()
    {
        int n = _buildGroups[_gateGroup % _buildGroups.Length];
        _plan.Add(new Step
        {
            Kinds = GroupKinds(n, _gateGroup % _buildGroups.Length, _buildGroups.Length, true),
            Side = SideRule.Ahead, // 前から
            Spacing = StagePaceTable.GroupStep,
            Gap = StagePaceTable.GateGroupInterval,
            Cap = StagePaceTable.GateCap,
        });
        _gateGroup++;
    }

    bool ReadRooms()
    {
        var cam = FindObjectOfType<StageCamera>();
        if (cam != null)
        {
            try
            {
                // 部屋の真ん中の x はカメラの非公開の設定なので、保存の形（JSON）で読む
                // 横スクロールのカメラは部屋の一覧が空。読めなかったときも空になるので、下のシーンの名前で決める
                var d = JsonUtility.FromJson<CameraDump>(JsonUtility.ToJson(cam));
                if (d != null && d._roomCentersX != null && d._roomCentersX.Length >= 3)
                {
                    _rooms = d._roomCentersX;
                    return true;
                }
            }
            catch (Exception) { /* 読めなければ、シーンの名前で決める */ }
        }
        if (!gameObject.scene.name.Contains("Windows")) return false;
        _rooms = new[] { 0f, 21f, 42f, 63f }; // StageLayoutsBuilder.BuildWindows と同じ【仮】
        return true;
    }

    float ReadGateX()
    {
        var gate = FindObjectOfType<BossRoomGate>();
        if (gate != null)
        {
            try
            {
                var d = JsonUtility.FromJson<GateDump>(JsonUtility.ToJson(gate));
                if (d != null && d._triggerX > _startX + 1f) return d._triggerX;
            }
            catch (Exception) { /* 読めなければ、ボス部屋の左の端 */ }
        }
        return BattleArena.Current != null ? BattleArena.Current.Bounds.xMin : _startX + 60f;
    }

    // ───────── 出来事（場面③。中身は担当2） ─────────

    void StartEvent()
    {
        _eventTried = true;
        StageEvent ev = null;
        try { ev = AppStageEvents.For(AppName, BundleId, StageNumber, IsFinal) ?? StageEventTable.For(Skin, StageNumber, IsFinal); } // アプリの出来事が先（2026-11-19）
        catch (Exception e) { Debug.LogException(e); }
        if (ev == null) return;
        _runner = CreateRunner();
        _event = ev;
        _eventRunning = true;
        _eventAt = Time.time;
        try
        {
            _eventSpawnsZako = ev.SpawnsOwnZako;
            ev.Begin(this, _runner, _row.Strength, _eventSpawnsZako ? _eventBudget : 0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            StopEvent();
        }
    }

    void TickEvent()
    {
        bool going;
        try { going = _event != null && _event.Tick(Time.deltaTime); }
        catch (Exception e) { Debug.LogException(e); going = false; }
        float max = _layout == Layout.OneScreen ? StagePaceTable.EventMaxSeconds : StagePaceTable.GateEventSeconds;
        if (Time.time - _eventAt > max) going = false;
        if (going) return;
        StopEvent();
        // 出来事が先に終わったら、残りを ② の出し方で出す（並んでいる分はそのまま）
        ClearPlan();
        if (_layout == Layout.OneScreen && Phase == PacePhase.Event)
        {
            int left = _eventBudget - PhaseSpawned - _queue.Count;
            if (left > 0) AddGroups(left, StagePaceTable.BuildInterval, StagePaceTable.BuildCap, true);
            ArmPlan(StagePaceTable.NoGapDelay);
        }
    }

    /// <summary>出来事を終わらせて、危ない物を全部消す（途中でもよい）</summary>
    void StopEvent()
    {
        StageEvent ev = _event;
        _event = null;
        _eventRunning = false;
        _eventSpawnsZako = false;
        if (ev != null)
        {
            try { ev.End(); }
            catch (Exception e) { Debug.LogException(e); }
        }
        if (_runner != null)
        {
            _runner.Cancel();
            _runner.ClearSpawned();
            Destroy(_runner.gameObject);
        }
        _runner = null;
    }

    /// <summary>出来事用の AttackRunner（担当2 のプレハブ）。そのステージのスキン・色・アイコンと、狙う相手を入れる。無ければ null</summary>
    AttackRunner CreateRunner()
    {
        AttackRunner prefab = Resources.Load<AttackRunner>(StagePaceTable.HazardRunnerPath);
        if (prefab == null) return null;
        AttackRunner r = Instantiate(prefab, PlayArea.center, Quaternion.identity);
        r.name = "PaceHazardRunner";
        r.Target = Player;
        r.Skin = Skin;
        r.SkinTheme = Theme;
        r.SkinIcon = Icon;
        return r;
    }

    /// <summary>出来事が雑魚を出す型（群れ）の上乗せ（2-3。担当2 の表）</summary>
    int SafeExtraZako()
    {
        try { int app = AppStageEvents.ExtraZako(AppName, BundleId); return app >= 0 ? app : StageEventTable.ExtraZako(Skin, StageNumber, IsFinal); }
        catch (Exception e) { Debug.LogException(e); return 0; }
    }

    // ───────── 出し方（組・群れ・はさみうち・1体ずつ） ─────────

    /// <summary>段を出してよいか見て、よければ並べる（空白をなくす：雑魚が0体なら 0.3秒で次）</summary>
    void RunPlan(int alive)
    {
        if (_queue.Count > 0 || _planIndex >= _plan.Count) return;
        Step s = _plan[_planIndex];
        bool timeUp = Time.time >= _planReadyAt;
        bool empty = alive == 0 && Time.time >= Mathf.Max(_zeroSince, _noGapFloor) + StagePaceTable.NoGapDelay;
        if (!timeUp && !empty) return;
        if (alive > 0 && alive + s.Kinds.Length > s.Cap) return;
        int side = ResolveSide(s.Side);
        for (int i = 0; i < s.Kinds.Length; i++)
            _queue.Add(new Job { Kind = s.Kinds[i], Side = side, Slot = i, Scatter = s.Side == SideRule.Scatter, At = Time.time + i * s.Spacing });
        if (s.Side != SideRule.Scatter) _lastSide = side;
        _pendingGap = s.Gap;
        _planIndex++;
    }

    void RunQueue()
    {
        for (int i = 0; i < _queue.Count;)
        {
            Job j = _queue[i];
            if (Time.time < j.At) { i++; continue; }
            JobResult r = TrySpawn(j);
            if (r == JobResult.Retry)
            {
                j.Tries++;
                j.At = Time.time + 0.15f;
                if (j.Tries >= 4) j.Scatter = true; // 列の場所に出せなければ、どこかに出す
                // 道中は数えないので、出せなければあきらめる（1画面はノルマなので、出せるまで続ける）
                if (_layout == Layout.OneScreen || j.Tries < 12) { i++; continue; }
            }
            _queue.RemoveAt(i);
            if (_queue.Count == 0)
            {
                _planReadyAt = Time.time + _pendingGap;
                _noGapFloor = Time.time;
            }
        }
    }

    JobResult TrySpawn(Job j)
    {
        if (Spawner == null || Spawner.IsStopped || Spawner.QuotaUsedUp) return JobResult.Dropped;
        if (Player == null) return JobResult.Retry;
        int template = TemplateOf(j.Kind, out bool flying);
        Rect view = PlayArea;
        Vector2 player = Player.position;
        Vector2 feet;
        bool ok = j.Scatter
            ? SpawnPatterns.TryRandom(view, Spawner, player, out feet)
            : SpawnPatterns.TryLineSlot(view, j.Side, j.Slot, Spawner, player, out feet);
        if (!ok) return JobResult.Retry;
        int before = Spawner.Spawned;
        Health h = Spawner.SpawnNow(template, flying ? feet + Vector2.up * StagePaceTable.FlyerHeight : feet);
        if (h == null && template != 0 && Spawner.Spawned == before) h = Spawner.SpawnNow(0, feet); // 新しいひな形が出せなければ、歩く雑魚で
        if (h == null && Spawner.Spawned == before) return JobResult.Dropped;
        _lastActivity = Time.time;
        return JobResult.Spawned;
    }

    /// <summary>雑魚の種類 → ひな形の番号（担当3 の物が無ければ 0＝歩く雑魚）</summary>
    int TemplateOf(Kind kind, out bool flying)
    {
        flying = false;
        switch (kind)
        {
            case Kind.Shooter: return ShootersAlive() < StagePaceTable.MaxShooters && Spawner.Template(1) != null ? 1 : 0; // 撃つ雑魚は同時に2体まで
            case Kind.Small: return Variant("Small");
            case Kind.Elite: return Variant("Elite");
            case Kind.Charger: return Variant("Charger");
            case Kind.Flyer:
                int f = Variant("Flyer");
                flying = f != 0;
                return f;
        }
        return 0;
    }

    int Variant(string name)
    {
        int i = PaceVariants.Get(name);
        return i >= 0 && Spawner.Template(i) != null ? i : 0;
    }

    int ShootersAlive()
    {
        int n = 0;
        IReadOnlyList<Health> list = Spawner.AliveList;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].GetComponent<ZakoShooter>() != null) n++;
        return n;
    }

    int ResolveSide(SideRule rule)
    {
        Rect view = PlayArea;
        float px = PlayerX;
        float minD = Spawner != null ? Spawner.MinDistance : 4f;
        switch (rule)
        {
            case SideRule.Far: return SpawnPatterns.FarSide(view, px);
            case SideRule.Opposite:
                int s = -_lastSide; // はさみうち。プレイヤーがその端にいたら、遠い方から
                return SpawnPatterns.SideBlocked(view, s, px, minD) ? -s : s;
            case SideRule.Behind: return SpawnPatterns.SideBlocked(view, -1, px, minD) ? 1 : -1;
            default: return 1;
        }
    }

    /// <summary>2〜3体の組に分けて足す（5＝3＋2、4＝2＋2）</summary>
    void AddGroups(int count, float gap, int cap, bool shooters)
    {
        while (count > 0)
        {
            int n = count >= 5 ? 3 : count == 4 ? 2 : count;
            Kind[] kinds = shooters ? GroupKinds(n, -1, 0, false) : Fill(Kind.Walker, n);
            _plan.Add(new Step { Kinds = kinds, Side = SideRule.Far, Spacing = StagePaceTable.GroupStep, Gap = gap, Cap = cap });
            count -= n;
        }
    }

    /// <summary>1体ずつ足す（どこかに。歩く雑魚だけ。③の雨・柱床の間）</summary>
    void AddSingles(int count)
    {
        for (int i = 0; i < count; i++)
            _plan.Add(new Step { Kinds = new[] { Kind.Walker }, Side = SideRule.Scatter, Gap = StagePaceTable.EventInterval, Cap = StagePaceTable.EventCap });
    }

    /// <summary>組の中身（先頭は歩く雑魚。撃つ雑魚が混ざる。最初の組に突っ込む雑魚、最後の組にとぶ雑魚）</summary>
    Kind[] GroupKinds(int n, int groupIndex, int groupCount, bool specials)
    {
        var k = new Kind[Mathf.Max(1, n)];
        for (int i = 0; i < k.Length; i++)
            k[i] = i > 0 && UnityEngine.Random.value < StagePaceTable.ShooterChance ? Kind.Shooter : Kind.Walker;
        if (!specials) return k;
        if (k.Length >= 2 && groupIndex == 0 && (IsFinal || StageNumber >= StagePaceTable.ChargerFromStage)) k[1] = Kind.Charger;
        if (groupIndex == groupCount - 1 && StageNumber >= StagePaceTable.FlyerFromStage) k[k.Length - 1] = Kind.Flyer;
        return k;
    }

    static Kind[] Fill(Kind kind, int n)
    {
        var k = new Kind[Mathf.Max(1, n)];
        for (int i = 0; i < k.Length; i++) k[i] = kind;
        return k;
    }

    void ClearPlan()
    {
        _plan.Clear();
        _planIndex = 0;
    }

    /// <summary>次の段を出せるようにする（delay 秒後。雑魚が0体のときも、この時刻より前には出さない）</summary>
    void ArmPlan(float delay)
    {
        _planReadyAt = Time.time + delay;
        _noGapFloor = Time.time + delay - StagePaceTable.NoGapDelay;
    }

    // ───────── 共通 ─────────

    bool BossStarted()
    {
        if (_entrance != null) return _entrance.IsTriggered; // 1画面は雑魚を倒し切ったとき、道中はボス部屋に入ったとき
        return _layout == Layout.OneScreen && _wave != null && _wave.IsCleared;
    }

    /// <summary>ボスの登場：Done を出してから（ほかの部品が片づける）、出来事・並んでいる雑魚・印を消す</summary>
    void Finish()
    {
        SetPhase(PacePhase.Done);
        StopEvent();
        ClearPlan();
        _queue.Clear();
        _marks.Clear();
        if (_layout == Layout.OneScreen) Remaining = 0;
    }

    void SetPhase(PacePhase p)
    {
        if (Phase == p) return;
        Phase = p;
        _phaseAt = Time.time;
        _spawnedAtPhase = Spawner != null ? Spawner.Spawned : 0;
        RaisePhaseChanged(p);
    }

    void TrackZero(int alive)
    {
        if (alive > 0) _zeroSince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(_zeroSince)) _zeroSince = Time.time;
    }

    void UpdateArea()
    {
        if (_layout == Layout.OneScreen && Spawner != null && Spawner.Area.width > 0f) PlayArea = Spawner.Area;
        else if (_layout == Layout.Scroll || _layout == Layout.Rooms) PlayArea = ScreenUi.ViewRect();
        else PlayArea = BattleArena.GetBounds();
    }

    void UpdateProgress()
    {
        if (Player != null) _maxX = Mathf.Max(_maxX, Player.position.x);
        if (_layout == Layout.Scroll || _layout == Layout.Rooms)
            Progress = Mathf.Clamp01((_maxX - _startX) / Mathf.Max(1f, _gateX - _startX));
        else
            Progress = _wave != null ? _wave.Progress : 0f;
    }

    static int Sum(int[] a)
    {
        int s = 0;
        if (a != null) foreach (int v in a) s += v;
        return s;
    }

    static int[] Scale(int[] a, float k)
    {
        var r = new int[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = Mathf.Max(1, Mathf.RoundToInt(a[i] * k));
        return r;
    }
}
