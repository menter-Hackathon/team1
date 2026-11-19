using System.Collections;
using UnityEngine;

/// <summary>
/// ステージ1つ分の進行（仕様書 2.2）。ステージのシーンに1つ置く。
/// ・ラン状態の「今のステージ」を見て、そのアプリのボスをプレハブから作る（使用時間で強さを変えた設定を渡す）
/// ・ボスは雑魚を全員倒すと出てくる（BossEntrance）
/// ・ボスが正常化したら、少し待って次へ（GameSession.CompleteStage）
/// ・ライフが0になったら、少し待ってゲームオーバーの画面（GameSession.ShowGameOver。そこからリトライ）
/// ・プレイヤーの能力は、ラン状態の所持スキルにつなぐ（ステージをまたいで残る）
/// ・ステージの色を、そのアプリの色（AppTheme）に塗り替える（同じ GameObject の StageTheme）
/// ステージのシーンを直接再生したときは、サンプルデータでランを作って始める（開発用）。
/// </summary>
public class StageDirector : MonoBehaviour
{
    [Tooltip("直接再生したときに使う対応アプリ表")]
    [SerializeField] AppCatalog _catalog;
    [Tooltip("直接再生したときに使う、カテゴリごとの能力の表（上から3つを自動で持つ）")]
    [SerializeField] AbilityCatalog _abilities;
    [SerializeField] EnemyWave _wave;
    [SerializeField] BossEntrance _entrance;
    [SerializeField] PlayerSkills _player;
    [Tooltip("正常化してから次のステージへ進むまでの秒数")]
    [SerializeField, Min(0f)] float _clearDelay = 2.5f;
    [Tooltip("ライフが0になってから、やり直すまでの秒数")]
    [SerializeField, Min(0f)] float _retryDelay = 1.5f;
    [Tooltip("この距離以内でボス由来の感染ノイズが最大になる（ワールド単位）")]
    [SerializeField, Min(0f)] float _noiseNearDistance = 3f;
    [Tooltip("この距離からボス由来の感染ノイズが強まり始める（ワールド単位）")]
    [SerializeField, Min(0f)] float _noiseFarDistance = 24f;

    GameSession _session;
    Health _playerHealth;
    bool _ending;

    /// <summary>このステージの計画</summary>
    public StagePlan Plan { get; private set; }
    /// <summary>このステージのボス（雑魚を倒すまでは非表示）</summary>
    public BossController Boss { get; private set; }
    public EnemyWave Wave => _wave;
    public BossEntrance Entrance => _entrance;

    void Awake()
    {
        _session = GameSession.Instance;
        if (_session == null || _session.Run == null || _session.Run.Current == null)
        {
            // ステージのシーンを直接再生した（開発用）：サンプルデータでランを作る
            _session = GameSession.EnsureExists(_catalog, _abilities);
            if (!_session.PrepareRun(new SampleUsageProvider(), out string error))
            {
                Debug.LogError($"ステージを始められません：{error}", this);
                enabled = false;
                return;
            }
            _session.AutoPickLoadout(); // 能力えらびの画面は出さず、カードの上から順に持つ
        }

        Plan = _session.Run.Current;
        // ステージの色を、そのアプリのアイコンの色にする（壁紙・床・壁・足場）
        if (TryGetComponent(out StageTheme theme)) theme.Apply(Plan.Theme);
        // 出てくる位置は、ボス部屋（BattleArena）の床の真ん中からのずれ（ボス部屋がステージの端にあるステージもあるため）
        var arena = FindObjectOfType<BattleArena>();
        Vector2 roomFloor = arena != null ? new Vector2(arena.Bounds.center.x, arena.Bounds.yMin) : Vector2.zero;
        Boss = Instantiate(Plan.App.BossPrefab, roomFloor + Plan.App.SpawnPosition, Quaternion.identity);
        Boss.name = Plan.App.BossPrefab.name;
        Boss.gameObject.SetActive(false); // プレハブは非表示で保存してあるが、念のため
        Boss.SetProfile(Plan.Profile);
        // 代用ボス・ラスボス・アイコンから見た目を作る行（Office・Unity・Steam。2026-11-19）：着ているウィンドウを、そのアプリのアイコンから作ったものにする
        if (Plan.Visual != null)
        {
            if (Plan.IsStandIn) Boss.name = $"StandIn_{Plan.App.DisplayName}";
            AppBossLook.ApplyWindow(Boss, Plan.Visual, Plan.Theme, Plan.App.DisplayName);
        }
        if (_player != null && Boss.TryGetComponent(out AttackRunner runner)) runner.Target = _player.transform;
        if (_entrance != null) _entrance.SetBoss(Boss);
    }

    void Start()
    {
        if (Plan == null) return;
        foreach (var camera in FindObjectsOfType<Camera>())
        {
            Transform background = camera.transform.Find("Background");
            if (background == null || !background.TryGetComponent(out SpriteRenderer wallpaper)) continue;
            var decay = background.gameObject.AddComponent<DesktopDecay>();
            decay.Initialize(wallpaper, _session.Run.CurrentIndex, _session.Run.Stages.Count);
            decay.BindProximity(_player != null ? _player.transform : null, Boss,
                _noiseNearDistance, _noiseFarDistance);
            decay.BindBattle(_entrance, _wave);
        }
        if (_player != null)
        {
            _player.Bind(_session.Run.Loadout);
            _playerHealth = _player.GetComponent<Health>();
            if (_playerHealth != null) _playerHealth.Died += OnPlayerDied;
        }
        Boss.Normalized += OnBossNormalized;
        _session.NotifyStageStarted();
    }

    void OnDestroy()
    {
        if (_playerHealth != null) _playerHealth.Died -= OnPlayerDied;
        if (Boss != null) Boss.Normalized -= OnBossNormalized;
    }

    void OnBossNormalized(SkillDefinition reward)
    {
        if (_ending) return;
        _ending = true;
        StartCoroutine(After(_clearDelay, () => _session.CompleteStage(reward)));
    }

    void OnPlayerDied()
    {
        if (_ending) return;
        _ending = true;
        StartCoroutine(After(_retryDelay, () => _session.ShowGameOver()));
    }

    static IEnumerator After(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }
}
