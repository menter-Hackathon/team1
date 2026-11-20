using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1回のプレイ（ラン）の進行。シーンをまたいで残る（DontDestroyOnLoad）。
/// ・タイトル：StartRun(読み込み方) → 使用データを読み、能力えらびのカードとステージの計画（上位10アプリからランダムに3つのボス＋最後にラスボスの Terminal）を作って、能力えらびの画面へ
/// ・能力えらび：ConfirmLoadout(選んだ能力) → 持っている能力にして、最初のステージへ（2026-11-17：ゲームの流れ。最初は1つ）
/// ・ステージ：StageDirector がボスを出し、正常化したら CompleteStage、ライフ0なら ShowGameOver を呼ぶ
/// ・CompleteStage：次のステージへ。最後ならリザルトへ
/// ・ステージに入る前（能力えらびの後・クリアの後）は、暗転の間に感染したデスクトップの画面（DesktopScreen）を出す（2026-11-18）。
///   クリアしたアプリが元に戻り、次のアプリのウィンドウが開いてステージへ。最後のクリアの後も出してからリザルトへ。リトライでは出さない
/// ・ShowGameOver：ゲームオーバーの画面を出してゲームを止める（2026-11-18）。画面の「リトライ」で FailStage、「タイトルへ」で ReturnToTitle
/// ・FailStage：今のステージを最初からやり直す（仕様書 2.4）。ライフは全快、能力はステージを始めたときの状態、クールタイムはリセット
/// ・ステージで遊んだ時間・倒した雑魚・受けたダメージを Run に数える（リザルトに出す。2026-11-18）
/// ・シーンの切り替えは画面を暗くしてから行う（文字は出さない）
/// UI 担当は、ここのイベントと Run（ラン状態）を見て画面を作る。
/// </summary>
public class GameSession : MonoBehaviour
{
    [SerializeField] AppCatalog _catalog;
    [SerializeField] string _titleScene = "RunTitle";
    [SerializeField] string _resultScene = "RunResult";
    [Tooltip("カテゴリごとの能力の表（能力えらびのカードを作る）。空なら能力えらびをしない")]
    [SerializeField] AbilityCatalog _abilities;
    [Tooltip("能力えらびのシーン。空なら能力えらびをせず、上から順に自動で選ぶ")]
    [SerializeField] string _loadoutScene = "RunLoadout";
    [Tooltip("ボスの候補にするアプリの数（使用時間の上位）")]
    [SerializeField, Min(1)] int _bossPool = 10;
    [Tooltip("ステージの数（候補からランダムに選ぶ。ラスボスのステージは別に足す）")]
    [SerializeField, Min(1)] int _stageCount = 3;
    [Tooltip("最後にラスボス（対応アプリ表のラスボス＝Terminal）のステージを足す（2026-11-18 まさぴ〜決定）")]
    [SerializeField] bool _finalBossStage = true;
    [Tooltip("ボスを選ぶ乱数の種（0＝毎回ちがう。確認用）")]
    [SerializeField] int _randomSeed;
    [Tooltip("画面を暗くする・明るくする秒数")]
    [SerializeField, Min(0f)] float _fadeTime = 0.4f;
    [Tooltip("ステージに入る前に、感染したデスクトップの画面を出す（2026-11-18）")]
    [SerializeField] bool _showDesktop = true;

    /// <summary>アイコンを読み込むアプリの数（使用時間の長い順）</summary>
    const int MaxIconApps = 12;
    /// <summary>感染したデスクトップの画面の Dock に並べるアプリの数</summary>
    const int MaxDockApps = 8;

    float _fade; // 0＝見える、1＝真っ暗
    Texture2D _black;
    IAppUsageProvider _lastProvider; // 「もう一度あそぶ」で同じ使用データを使う
    bool _inStage; // ステージで遊んでいる（時間を数える）
    DesktopScreen _desktop;
    BackupFx _backup; // バックアップ・復元・タイトルへ の演出（2026-11-20。docs/backup-restore-design.md）

    public static GameSession Instance { get; private set; }
    public AppCatalog Catalog => _catalog;
    public AbilityCatalog Abilities => _abilities;
    public RunState Run { get; private set; }
    /// <summary>直近に読み取った使用データ。ゲーム内の書類に表示する（起動中のみ保持）。</summary>
    public IReadOnlyList<AppUsage> LastUsages { get; private set; } = Array.Empty<AppUsage>();
    public bool HasUsageRecord { get; private set; }
    public bool LastUsageIsSample { get; private set; }

    public void RememberUsages(IEnumerable<AppUsage> usages,bool sample)
    {
        LastUsages=Array.AsReadOnly((usages??Enumerable.Empty<AppUsage>()).ToArray());
        HasUsageRecord=true;
        LastUsageIsSample=sample;
    }
    /// <summary>シーンを切り替えている途中か</summary>
    public bool IsTransitioning { get; private set; }
    /// <summary>ゲームオーバーの画面を出している</summary>
    public bool IsGameOver { get; private set; }
    /// <summary>感染したデスクトップの画面を出している（切り替えの途中。IsTransitioning も true）</summary>
    public bool IsOnDesktop { get; private set; }
    /// <summary>「もう一度あそぶ」ができる（前のランの使用データがある）</summary>
    public bool CanPlayAgain => _lastProvider != null;
    /// <summary>サンプルの使用データで遊んでいる（訓練モード）</summary>
    public bool IsSampleRun => _lastProvider is SampleUsageProvider;

    /// <summary>ランを始めたとき（ステージの計画が決まった）</summary>
    public event Action<RunState> RunStarted;
    /// <summary>ステージが始まったとき（リトライも含む）</summary>
    public event Action<StagePlan> StageStarted;
    /// <summary>ステージをクリアしたとき（正常化したアプリ, 手に入れた能力）</summary>
    public event Action<StagePlan, SkillDefinition> StageCleared;
    /// <summary>ライフが0になったとき（ゲームオーバーの画面を出す）</summary>
    public event Action<StagePlan> GameOver;
    /// <summary>最後のステージをクリアしたとき（このあとリザルトへ）</summary>
    public event Action<RunState> RunCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // タイトルに戻ってきたときなど、2つ目は消す
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (GetComponent<GameOverScreen>() == null) gameObject.AddComponent<GameOverScreen>();
        _desktop = GetComponent<DesktopScreen>();
        if (_desktop == null) _desktop = gameObject.AddComponent<DesktopScreen>();
        _backup = GetComponent<BackupFx>();
        if (_backup == null) _backup = gameObject.AddComponent<BackupFx>();
        Health.AnyDamaged += OnAnyDamaged;
        Health.AnyDied += OnAnyDied;
        // 図鑑：同じアプリの記録（「Excel」と「Microsoft Excel」など）を1つにまとめる（2026-11-20。まとめる物が無ければ何もしない）
        if (_catalog != null)
        {
            try { AppDex.MergeDuplicates(x => RunPlanner.DexIdentity(_catalog, x.name, x.bundleId)); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Health.AnyDamaged -= OnAnyDamaged;
        Health.AnyDied -= OnAnyDied;
    }

    void Update()
    {
        if (_inStage && Run != null && !IsTransitioning && !IsGameOver) Run.PlaySeconds += Time.deltaTime;
    }

    // ステージの中だけ数える：プレイヤーが受けたダメージと、倒れた雑魚（ボスは正常化なので数えない）
    void OnAnyDamaged(Health health, int amount)
    {
        if (_inStage && Run != null && health.Team == Team.Player) Run.DamageTaken += amount;
    }

    void OnAnyDied(Health health)
    {
        if (!_inStage || Run == null || health.Team != Team.Enemy) return;
        if (health.GetComponentInParent<BossController>() != null) return;
        Run.Kills++;
    }

    /// <summary>確認用：ボスを選ぶ乱数の種（0＝毎回ちがう）</summary>
    public int RandomSeed { get => _randomSeed; set => _randomSeed = value; }

    /// <summary>【確認用】ランを作るとき、ステージをしぼる（null なら何もしない）。遊ぶボスを選ぶ確認用の画面（BossSelectDebug）が使う</summary>
    public Func<List<StagePlan>, List<StagePlan>> StageFilter { get; set; }

    /// <summary>無ければ作る（ステージのシーンを直接再生したときの開発用）</summary>
    public static GameSession EnsureExists(AppCatalog catalog, AbilityCatalog abilities = null)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GameSession");
        var session = go.AddComponent<GameSession>();
        session._catalog = catalog;
        session._abilities = abilities;
        session._loadoutScene = null; // ステージを直接再生したとき（開発用）は、能力えらびの画面を出さない
        return session;
    }

    /// <summary>使用データを読み込んでランを始め、最初のステージに進む。対応アプリが無ければ false</summary>
    public bool StartRun(IAppUsageProvider provider, out string error)
    {
        if (!PrepareRun(provider, out error)) return false;
        _lastProvider = provider;
        int pick = _abilities != null ? _abilities.PickCount : 0;
        if (!string.IsNullOrEmpty(_loadoutScene) && pick > 0 && Run.Candidates.Count >= pick)
        {
            StartCoroutine(Transition(_loadoutScene)); // 能力えらびの画面へ
        }
        else
        {
            AutoPickLoadout();
            LoadCurrentStage();
        }
        return true;
    }

    /// <summary>能力えらびの画面から：選んだ能力を持って、最初のステージへ（選んだ順に 1・2・3 の枠）</summary>
    public void ConfirmLoadout(IList<SkillDefinition> picks)
    {
        if (IsTransitioning || Run == null) return;
        Run.Loadout.Clear();
        if (picks != null)
        {
            foreach (var s in picks)
            {
                if (s != null) Run.Loadout.TryAcquire(s);
            }
            SkillDefinition first = picks.FirstOrDefault(p => p != null);
            Run.Partner = Run.Candidates.FirstOrDefault(c => c.Skill == first);
        }
        LoadCurrentStage();
    }

    /// <summary>能力えらびをしないとき（ステージを直接再生したときなど）：カードの上から順に選ぶ</summary>
    public void AutoPickLoadout()
    {
        if (Run == null || Run.Loadout.Count > 0) return;
        int pick = _abilities != null ? _abilities.PickCount : 0;
        foreach (var c in Run.Candidates.Take(pick)) Run.Loadout.TryAcquire(c.Skill);
        Run.Partner = Run.Candidates.FirstOrDefault();
        Run.StageStartSlots = Run.Loadout.Snapshot();
    }

    /// <summary>ランの計画だけを作る（シーンは切り替えない）。ステージを直接再生したときに使う</summary>
    public bool PrepareRun(IAppUsageProvider provider, out string error)
    {
        if (IsTransitioning) { error = "切り替え中です"; return false; }
        if (_catalog == null) { error = "対応アプリ表（AppCatalog）が設定されていません"; return false; }
        if (provider == null) { error = "読み込み方が指定されていません"; return false; }
        if (!provider.TryLoad(out List<AppUsage> usages, out error)) return false;
        RememberUsages(usages,provider is SampleUsageProvider);

        // 能力えらびのカード：使用時間の長いアプリから、カテゴリの能力を出す（足りなければ、まだ出ていないカテゴリで埋める）。
        // カテゴリの分からないアプリは、ひな形から能力をその場で作り、アイコンの色に塗る（2026-11-17）。
        // 使用データのアプリはすべてここに来る（対応表に無いアプリも）。スキャンで全部のアプリを渡せば、そのままカードになる。
        // カードを先に作る（ボスの報酬と、同じ能力を使うため）
        var candidates = new List<AbilityCandidate>();
        if (_abilities != null)
        {
            var rows = usages.Select(u => new AppUsageRow { Name = u.DisplayName, BundleId = u.Id, Seconds = u.UsageSeconds }).ToList();
            AppVisuals.Prepare(rows.Where(r => r.Seconds > 0 && !_abilities.IsExcluded(r)).OrderByDescending(r => r.Seconds).Take(MaxIconApps));
            candidates = _abilities.BuildCandidates(rows,
                r => AppVisuals.Get(r)?.Icon16,
                (r, usedTemplates) => AppVisuals.Generate(r, _abilities, usedTemplates));
        }

        // ステージ：上位10アプリから、ランダムに3つをボスにする（2026-11-17 まさぴ〜決定）。能力えらびの表が無いときは、今までどおり対応アプリだけ
        var rng = new System.Random(_randomSeed != 0 ? _randomSeed : Environment.TickCount);
        List<StagePlan> plans = _abilities != null
            ? RunPlanner.PlanRandom(_catalog, _abilities, usages, rng, _bossPool, _stageCount)
            : RunPlanner.Plan(_catalog, usages);
        if (plans.Count == 0)
        {
            error = "駆除対象のアプリが見つかりません";
            return false;
        }
        // ラスボス：毎回、最後のステージに Terminal（2026-11-18）
        if (_finalBossStage && _abilities != null)
        {
            StagePlan final = RunPlanner.CreateFinalPlan(_catalog);
            if (final != null) plans.Add(final);
        }

        // 【確認用】遊ぶボスを選ぶとき：ステージをしぼる（2026-11-18。BossSelectDebug）
        if (StageFilter != null)
        {
            plans = StageFilter(plans) ?? plans;
            if (plans.Count == 0)
            {
                error = "選んだボスのステージが作れません";
                return false;
            }
        }

        Run = new RunState(plans);
        Run.Candidates = candidates;
        // アプリ図鑑：ボスの候補（上位10アプリ）を載せる（最初はグレー。2026-11-17）
        var pool = _abilities != null ? RunPlanner.BossPool(_catalog, _abilities, usages, _bossPool) : null;
        if (pool != null)
        {
            AppDex.RecordSeen(pool.Select(p => (p.entry != null ? p.entry.DisplayName : p.row.Name, p.entry != null ? FirstBundle(p.entry) : p.row.BundleId)));
        }
        PrepareIconsAndDock(Run, pool);
        Run.StageStartSlots = Run.Loadout.Snapshot();
        error = null;
        RunStarted?.Invoke(Run);
        return true;
    }

    static string FirstBundle(AppEntry app) => app.BundleIdPrefixes?.FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "";

    /// <summary>
    /// ステージのアイコンと、感染したデスクトップの画面の Dock を用意する（アイコンはここで1回だけ読む。画面の描画中には読まない）。
    /// Dock：ボスの候補（使用時間の長い順）を8個まで。このランのボスのアプリは必ず入れる
    /// </summary>
    static void PrepareIconsAndDock(RunState run, List<(AppEntry entry, AppUsageRow row, double seconds)> pool)
    {
        foreach (StagePlan s in run.Stages)
        {
            int k = pool != null ? pool.FindIndex(p => IsStageOf(p.entry, p.row, s)) : -1;
            AppUsageRow row = k >= 0 ? pool[k].row : new AppUsageRow { Name = s.App.DisplayName, BundleId = FirstBundle(s.App), Seconds = 1 };
            s.Icon16 = s.Visual != null ? s.Visual.Icon16 : AppVisuals.Get(row)?.Icon16;
        }

        var dock = new List<DockApp>();
        if (pool != null)
        {
            foreach (var p in pool)
            {
                int stage = run.Stages.FindIndex(s => IsStageOf(p.entry, p.row, s));
                dock.Add(new DockApp
                {
                    Name = p.entry != null ? p.entry.DisplayName : p.row.Name,
                    Icon = stage >= 0 ? run.Stages[stage].Icon16 : AppVisuals.Get(p.row)?.Icon16,
                    Stage = stage,
                });
            }
        }
        for (int i = 0; i < run.Stages.Count; i++)
        {
            if (!dock.Any(d => d.Stage == i)) dock.Add(new DockApp { Name = run.Stages[i].DisplayName, Icon = run.Stages[i].Icon16, Stage = i });
        }
        while (dock.Count > MaxDockApps)
        {
            int drop = dock.FindLastIndex(d => d.Stage < 0); // 使用時間の短い方から、ボスでないアプリを外す
            if (drop < 0) break;
            dock.RemoveAt(drop);
        }
        run.Dock = dock;
    }

    // ボスの候補の1つが、そのステージのアプリか（対応表のアプリは表の行で、代用ボスは名前で比べる）
    static bool IsStageOf(AppEntry entry, AppUsageRow row, StagePlan stage) =>
        entry != null ? stage.App == entry : string.Equals(stage.App.DisplayName, row.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>StageDirector が呼ぶ：ステージが始まった</summary>
    public void NotifyStageStarted()
    {
        if (Run?.Current == null) return;
        _inStage = true;
        StageStarted?.Invoke(Run.Current);
    }

    /// <summary>リザルトから：同じ使用データで、新しいランを始める（ボスは選び直す）</summary>
    public bool PlayAgain(out string error)
    {
        error = null;
        if (_lastProvider == null) { error = "前のランの使用データがありません"; return false; }
        return StartRun(_lastProvider, out error);
    }

    /// <summary>ステージをクリアした（ボスが正常化した）。次のステージかリザルトへ</summary>
    public void CompleteStage(SkillDefinition reward)
    {
        if (IsTransitioning || Run?.Current == null) return;
        _inStage = false;
        StagePlan stage = Run.Current;
        int cleared = Run.CurrentIndex;
        Run.Cleared.Add(new ClearRecord { Stage = stage, Reward = reward });
        // アプリ図鑑：正常化したアプリに色を付ける。初めてなら、リザルトで光らせる
        if (AppDex.RecordDefeat(stage.App.DisplayName, FirstBundle(stage.App), reward)) Run.NewDex.Add(AppDex.KeyOf(stage.App.DisplayName));
        GameAudio.StageCleared();
        StageCleared?.Invoke(stage, reward);

        Run.CurrentIndex++;
        if (Run.IsFinished)
        {
            RunCompleted?.Invoke(Run);
            StartCoroutine(Transition(_resultScene, true, cleared)); // 全部元に戻ったデスクトップを見せてからリザルトへ
        }
        else
        {
            LoadCurrentStage(cleared);
        }
    }

    /// <summary>ライフが0になった：ゲームオーバーの画面を出して、ゲームを止める（2026-11-18）</summary>
    public void ShowGameOver()
    {
        if (IsTransitioning || IsGameOver || Run?.Current == null) return;
        _inStage = false;
        IsGameOver = true;
        Run.LastFailed = Run.Current;
        Time.timeScale = 0f;
        GameAudio.GameOver();
        GameOver?.Invoke(Run.Current);
    }

    /// <summary>今のステージを最初からやり直す（ゲームオーバーの画面の「リトライ」）</summary>
    public void FailStage()
    {
        if (IsTransitioning || Run?.Current == null) return;
        IsGameOver = false;
        _inStage = false;
        Run.RetryCount++;
        Run.Loadout.Restore(Run.StageStartSlots);
        StartCoroutine(Transition(Run.Current.App.StageScene, fx: 1)); // 復元の演出（BackupFx。2026-11-20）
    }

    /// <summary>タイトルに戻る（ランは終わり）</summary>
    public void ReturnToTitle()
    {
        if (IsTransitioning) return;
        bool fromGameOver = IsGameOver; // ゲームオーバーから戻るときだけ演出（一時停止・リザルト・能力えらびからは今までどおり。2026-11-20）
        IsGameOver = false;
        _inStage = false;
        Run = null;
        StartCoroutine(Transition(_titleScene, fx: fromGameOver ? 2 : 0));
    }

    /// <summary>今のステージへ（感染したデスクトップの画面を通る）。restored：いまクリアしたステージ（-1＝なし）</summary>
    void LoadCurrentStage(int restored = -1)
    {
        Run.StageStartSlots = Run.Loadout.Snapshot();
        if (BackupFx.Enabled && _backup != null) _backup.Arm(Run.Current); // ステージに入ったら右下にバックアップの札（2026-11-20）
        StartCoroutine(Transition(Run.Current.App.StageScene, true, restored));
    }

    /// <summary>シーンを切り替える。desktop なら、暗転の間に感染したデスクトップの画面を出し、終わるのを待ってから読み込む。
    /// fx：1＝復元・2＝タイトルへ の演出（BackupFx。2026-11-20）。呼んだフレームから真っ黒の上に見せ、最初の暗くする 0.4 秒は飛ばす</summary>
    IEnumerator Transition(string sceneName, bool desktop = false, int restored = -1, int fx = 0)
    {
        IsTransitioning = true;
        if (_backup != null) _backup.StopPill();
        if (fx != 0 && BackupFx.Enabled && _backup != null)
        {
            Time.timeScale = 0f; // 後ろのステージは止めたまま
            if (fx == 1) _backup.BeginRestore(Run?.Current);
            else _backup.BeginQuit();
            while (!_backup.IsDone) yield return null;
            for (float t = 0f; t < _fadeTime; t += Time.unscaledDeltaTime)
            {
                _fade = t / _fadeTime;
                yield return null;
            }
            _fade = 1f;
            _backup.Hide();
        }
        Time.timeScale = 1f; // レベルアップの選択などで止めていても、戻してから切り替える
        for (float t = _fade * _fadeTime; t < _fadeTime; t += Time.unscaledDeltaTime)
        {
            _fade = t / _fadeTime;
            yield return null;
        }
        _fade = 1f;

        if (desktop && _showDesktop && _desktop != null && Run != null && Run.Dock.Count > 0)
        {
            IsOnDesktop = true;
            Time.timeScale = 0f; // 前のシーンは止めておく（画面は上から塗りつぶす）
            _desktop.Show(Run, restored);
            for (float t = 0f; t < _fadeTime; t += Time.unscaledDeltaTime)
            {
                _fade = 1f - t / _fadeTime;
                yield return null;
            }
            _fade = 0f;
            while (!_desktop.IsDone) yield return null;
            for (float t = 0f; t < _fadeTime; t += Time.unscaledDeltaTime)
            {
                _fade = t / _fadeTime;
                yield return null;
            }
            _fade = 1f;
            _desktop.Hide();
            IsOnDesktop = false;
            Time.timeScale = 1f;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"シーン「{sceneName}」を読み込めません。Build Settings に登録されているか確認してください", this);
            _fade = 0f;
            IsTransitioning = false;
            yield break;
        }
        while (!op.isDone) yield return null;
        if (BackupFx.Enabled && _backup != null) _backup.ShowPillIfArmed(); // 控えを取ったばかりなら、明るくなり始めた所から札を出す

        for (float t = 0f; t < _fadeTime; t += Time.unscaledDeltaTime)
        {
            _fade = 1f - t / _fadeTime;
            yield return null;
        }
        _fade = 0f;
        IsTransitioning = false;
    }

    void OnGUI()
    {
        if (_fade <= 0f) return;
        if (_black == null)
        {
            _black = new Texture2D(1, 1);
            _black.SetPixel(0, 0, Color.black);
            _black.Apply();
        }
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, _fade);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
        GUI.color = Color.white;
    }
}
