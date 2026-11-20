using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// 実績の窓口（シーンをまたいで残る。ゲームの起動時に自動で作る）。
    /// ・起動時：スクリーンタイムを読み、「PCの使い方」を判定する。解除があれば起動時スキャン画面を出す
    /// ・プレイ中：GameSession のイベント（ステージ開始・クリア・ランのクリア）と Health.AnyDamaged を購読して、その場で判定する
    /// ・正常化したとき：アプリ図鑑の記録で「アプリ図鑑」を判定する
    /// ・解除は Unlocked（UniRx の Observable）で流す。画面（吹き出し・リザルト）はそれを見る
    /// このプロジェクトには VContainer が無いので、既存（GameSession・AppDex）と同じく、シングルトン＋static で持つ。
    /// 既存のクラスは変えず、イベントを購読するだけ。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class AchievementService : MonoBehaviour
    {
        /// <summary>確認用：起動時スキャンの取得元を差し替える（null なら本番の取得元）</summary>
        public static IScreenTimeSource SourceOverride { get; set; }

        public static AchievementService Instance { get; private set; }

        /// <summary>エディタの「実績デバッグ」で、再生したときの起動時スキャンに偽データを使うか（EditorPrefs）</summary>
        public const string EditorUseFakeKey = "Team1.Achievements.UseFakeOnLaunch";
        public const string EditorPresetKey = "Team1.Achievements.FakePreset";

        static IScreenTimeSource DefaultSource()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EditorUseFakeKey, false))
                return new PresetScreenTimeSource(UnityEditor.EditorPrefs.GetInt(EditorPresetKey, 0));
#endif
            return new PendingScreenTimeSource();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("AchievementService").AddComponent<AchievementService>();
        }

        readonly Subject<AchievementUnlock> _unlocked = new Subject<AchievementUnlock>();
        readonly List<AchievementUnlock> _runUnlocks = new List<AchievementUnlock>();
        List<AchievementUnlock> _launchUnlocks = new List<AchievementUnlock>();
        AchievementDatabase _db;
        AchievementEngine _engine;
        GameSession _bound;
        bool _inStage;
        bool _tookDamage;

        /// <summary>解除の知らせ（起動時・プレイ中・正常化したとき、すべて）</summary>
        public IObservable<AchievementUnlock> Unlocked => _unlocked;
        public AchievementDatabase Database => _db;
        public AchievementEngine Engine => _engine;
        /// <summary>このランで解除した実績（リザルトに出す）</summary>
        public IReadOnlyList<AchievementUnlock> RunUnlocks => _runUnlocks;
        /// <summary>今回の起動時スキャンで解除した実績</summary>
        public IReadOnlyList<AchievementUnlock> LaunchUnlocks => _launchUnlocks;
        /// <summary>最後の起動時スキャンを飛ばした理由（飛ばさなかったら null）</summary>
        public string LastScanError { get; private set; }
        public IAppCategoryResolver Categories => _db != null && _db.Categories != null ? _db.Categories : (IAppCategoryResolver)AppCategoryMap.Empty;

        public AchievementDefinition Definition(string id) => _db != null ? _db.Find(id) : null;
        public Sprite Frame(AchievementRank rank) => _db != null ? _db.Frame(rank) : null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _db = AchievementDatabase.Load();
            if (_db == null)
                Debug.LogWarning("[実績] 定義（Resources/AchievementDatabase）がありません。メニューの Tools ＞ Achievements ＞ CSV から定義を作る を実行してください");
            var specs = _db != null ? _db.Definitions.Where(d => d != null).Select(d => d.ToSpec()) : Enumerable.Empty<AchievementSpec>();
            _engine = new AchievementEngine(specs, AchievementSaveStore.Load());
            Health.AnyDamaged += OnAnyDamaged;
            if (GetComponent<AchievementPopup>() == null) gameObject.AddComponent<AchievementPopup>();
            if (GetComponent<AchievementScanScreen>() == null) gameObject.AddComponent<AchievementScanScreen>();
        }

        void Start() => RunLaunchScan();

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Health.AnyDamaged -= OnAnyDamaged;
            Bind(null);
            _unlocked.OnCompleted();
        }

        void Update()
        {
            GameSession session = GameSession.Instance;
            if (!ReferenceEquals(session, _bound)) Bind(session);
        }

        // ───────── 起動時 ─────────

        /// <summary>
        /// 起動時スキャン：スクリーンタイムを読み、前回の判定以降のデータで「PCの使い方」を判定する。
        /// 取れなかったら飛ばす（前回の判定日時は変えない＝次の起動時にまとめて判定）
        /// </summary>
        public List<AchievementUnlock> RunLaunchScan(IScreenTimeSource source = null, bool showScreen = true)
        {
            source ??= SourceOverride ?? DefaultSource();
            DateTime now = DateTime.Now;
            if (!source.TryRead(now, out ScreenTimeSnapshot snapshot, out string error))
            {
                LastScanError = error;
                Debug.Log($"[実績] 起動時の判定を飛ばしました（{source.Label}）：{error}");
                return new List<AchievementUnlock>();
            }
            LastScanError = null;
            var ctx = new AchievementContext { Now = now, ScreenTime = snapshot, Categories = Categories };
            _launchUnlocks = _engine.Evaluate(AchievementTrigger.LaunchScan, ctx);
            Persist();
            foreach (var u in _launchUnlocks) _unlocked.OnNext(u);
            if (showScreen && _launchUnlocks.Count > 0)
                GetComponent<AchievementScanScreen>()?.Show(_launchUnlocks, _engine.AlmostThere());
            return _launchUnlocks;
        }

        /// <summary>
        /// スクリーンタイムのスキャン結果（タイトルの「スキャン」）を受け取る。
        /// 「コンプリート」に使う使用時間TOP10（ボスの候補と同じ選び方）のアプリ名だけを残す。使用時間は残さない
        /// </summary>
        public void ReportScreenTimeScan(IEnumerable<AppUsage> usages)
        {
            GameSession session = GameSession.Instance;
            if (usages == null || session == null || session.Catalog == null) return;
            int count = _db != null ? _db.TopAppCount : 10;
            var names = RunPlanner.BossPool(session.Catalog, session.Abilities, usages, count)
                .Select(p => p.entry != null ? p.entry.DisplayName : p.row.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            if (names.Count == 0) return;
            _engine.Save.topApps = names;
            Persist();
        }

        // ───────── プレイ中 ─────────

        void Bind(GameSession session)
        {
            if (!ReferenceEquals(_bound, null))
            {
                _bound.RunStarted -= OnRunStarted;
                _bound.StageStarted -= OnStageStarted;
                _bound.StageCleared -= OnStageCleared;
                _bound.RunCompleted -= OnRunCompleted;
                _bound.GameOver -= OnGameOver;
            }
            _bound = session;
            _inStage = false;
            if (ReferenceEquals(session, null)) return;
            session.RunStarted += OnRunStarted;
            session.StageStarted += OnStageStarted;
            session.StageCleared += OnStageCleared;
            session.RunCompleted += OnRunCompleted;
            session.GameOver += OnGameOver;
        }

        void OnRunStarted(RunState run) => _runUnlocks.Clear();

        // リトライでも呼ばれる：やり直したところから被弾を数える
        void OnStageStarted(StagePlan stage)
        {
            _inStage = true;
            _tookDamage = false;
        }

        void OnGameOver(StagePlan stage) => _inStage = false;

        void OnAnyDamaged(Health health, int amount)
        {
            if (_inStage && health != null && health.Team == Team.Player && amount > 0) _tookDamage = true;
        }

        void OnStageCleared(StagePlan stage, SkillDefinition reward)
        {
            _inStage = false;
            if (stage == null || AppDex.Suspended) return; // 確認用のラン（図鑑に書かないとき）は数えない
            RunState run = _bound != null ? _bound.Run : null;
            var play = new PlayEvent
            {
                Kind = PlayEventKind.StageCleared,
                StageIndex = run != null ? run.CurrentIndex : 0, // クリアの知らせは、次のステージに進む前に来る
                IsFinalBoss = stage.IsFinal,
                TookDamage = _tookDamage,
                EquippedSkills = run != null ? run.Loadout.Count : 0,
                AppName = stage.DisplayName,
            };
            Publish(_engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Now = DateTime.Now, Play = play, Categories = Categories }));
            Publish(_engine.Evaluate(AchievementTrigger.OnPurify, new AchievementContext { Now = DateTime.Now, Purify = BuildPurifyLog(run, stage), Categories = Categories }));
            Persist();
        }

        void OnRunCompleted(RunState run)
        {
            if (run == null || AppDex.Suspended) return;
            string partner = PartnerName(run);
            if (string.IsNullOrEmpty(partner)) return;
            _engine.Save.AddPartnerClear(partner);
            var play = new PlayEvent { Kind = PlayEventKind.RunCompleted, Partner = partner };
            Publish(_engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Now = DateTime.Now, Play = play, Categories = Categories }));
            Persist();
        }

        PurifyLog BuildPurifyLog(RunState run, StagePlan stage)
        {
            AppCatalog catalog = _bound != null ? _bound.Catalog : null;
            var log = new PurifyLog
            {
                TopApps = new List<string>(_engine.Save.topApps ?? new List<string>()),
                Partner = run != null ? DexName(catalog, PartnerName(run)) : null,
                LastPurified = stage != null && !stage.IsFinal ? stage.DisplayName : null,
            };
            foreach (AppDexEntry e in AppDex.Entries)
            {
                if (e == null || !e.IsDefeated) continue;
                if (catalog != null && catalog.IsFinalBossApp(e.bundleId, e.name)) continue; // 最終ウイルスボスは数えない
                log.PurifiedApps.Add(e.name);
            }
            return log;
        }

        /// <summary>
        /// 図鑑・TOP10 と同じ名前にそろえる（相棒の名前はスクリーンタイムの名前「Google Chrome」、図鑑は対応表の名前「Chrome」のため）
        /// </summary>
        static string DexName(AppCatalog catalog, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || catalog == null) return name;
            AppEntry entry = catalog.FindByName(name);
            return entry != null && entry.BossPrefab != null && entry.Profile != null ? entry.DisplayName : name;
        }

        /// <summary>相棒の名前（リザルトと同じ：カテゴリで埋めたカードは能力の名前、ほかはアプリ名）</summary>
        public static string PartnerName(RunState run)
        {
            if (run == null || run.Partner.Skill == null) return null;
            return run.Partner.IsFiller ? run.Partner.Skill.DisplayName : run.Partner.AppName;
        }

        void Publish(List<AchievementUnlock> unlocks)
        {
            if (unlocks == null) return;
            foreach (var u in unlocks)
            {
                if (_bound != null && _bound.Run != null) _runUnlocks.Add(u);
                _unlocked.OnNext(u);
            }
        }

        void Persist() => AchievementSaveStore.Save(_engine.Save);

        // ───────── 確認用 ─────────

        /// <summary>確認用：強制的に解除する（吹き出しも出る）</summary>
        public bool ForceUnlock(string id)
        {
            var u = _engine.ForceUnlock(id, DateTime.Now);
            if (u == null) return false;
            Publish(new List<AchievementUnlock> { u });
            Persist();
            return true;
        }

        /// <summary>確認用：偽のスクリーンタイムで起動時スキャンを再生する</summary>
        public List<AchievementUnlock> ReplayLaunchScan(ScreenTimeSnapshot snapshot) =>
            RunLaunchScan(new FixedScreenTimeSource(snapshot), true);

        /// <summary>確認用：セーブを消す（実績の記録だけ。アプリ図鑑は消さない）</summary>
        public void ResetSave()
        {
            AchievementSaveStore.Delete();
            _engine.Reset();
            _launchUnlocks = new List<AchievementUnlock>();
            _runUnlocks.Clear();
        }
    }
}
