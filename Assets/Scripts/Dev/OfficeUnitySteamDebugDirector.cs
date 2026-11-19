using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【確認用】Office・Unity・Steam のスキルとアプリボスを試すシーン（Scenes/Test/OfficeUnitySteamTest）の進行。
/// メニュー「Tools/Team1/Office・Unity・Steam テストシーンを作成」で作る。
/// ・プレイヤーは最初から 1：Office、2：Unity、3：Steam を持つ（プレイヤー版）
/// ・T／F1：Office のボス版（棒 → 円 → 折れ線の順）　Y／F2：Unity のボス版　U／F3：Steam のボス版（ボスの実績を1つ解除）
///   ボス版は、右側の「ボス版の発射台」から出す（ボスを出していなくても試せる）
/// ・I／F4：プレイヤーの Steam の実績を1つ解除（通知・バフを見る）
/// ・B：ボスを切り替える（なし → Office → Unity → Steam → なし）
/// ・O：Office のボスの見た目を Excel → Word → PowerPoint と切り替える
///   ボスの見た目は、ランと同じく Mac の本物のアプリのアイコンから作る（入っていないアプリは、名前から決まる色の仮のアイコン。2026-11-19）
/// ・N：ゲージ +10　P：ゲージを満タンにする（追い出し → ウイルス本体を倒すと正常化してスキルを獲得）
/// ・Z：雑魚を出す　R：やり直し
/// </summary>
public class OfficeUnitySteamDebugDirector : MonoBehaviour
{
    [SerializeField] Transform _player;
    [Tooltip("プレイヤーに最初から持たせるスキル（Office・Unity・Steam）")]
    [SerializeField] SkillDefinition[] _playerSkills;
    [Tooltip("ボス（Office・Unity・Steam の順）。最初は全部隠しておく")]
    [SerializeField] BossController[] _bosses;

    [Header("ボスの見た目（本物のアプリのアイコンから作る）")]
    [Tooltip("Office のボスの見た目にするアプリ（O キーで順に切り替える）")]
    [SerializeField] LookApp[] _officeApps =
    {
        new LookApp { Name = "Excel", BundleId = "com.microsoft.Excel" },
        new LookApp { Name = "Word", BundleId = "com.microsoft.Word" },
        new LookApp { Name = "PowerPoint", BundleId = "com.microsoft.Powerpoint" },
    };
    [SerializeField] LookApp _unityApp = new LookApp { Name = "Unity", BundleId = "com.unity3d.UnityEditor5.x" };
    [SerializeField] LookApp _steamApp = new LookApp { Name = "Steam", BundleId = "com.valvesoftware.steam" };

    [Header("ボス版の発射台")]
    [SerializeField] AttackRunner _bossCaster;
    [SerializeField] AttackData[] _officeBossAttacks;
    [SerializeField] AttackData _unityBossAttack;
    [SerializeField] AchievementPassive _steamBossPassive;

    [Header("雑魚")]
    [Tooltip("雑魚のひな形（非表示で置いておく）")]
    [SerializeField] GameObject _zakoTemplate;
    [SerializeField] Vector2 _zakoSpawn = new Vector2(2f, 0.05f);

    [System.Serializable]
    public struct LookApp
    {
        public string Name;
        [Tooltip("Mac のバンドルID（アイコンを探す。見つからなければ名前で探す）")]
        public string BundleId;
        public AppUsageRow Row => new AppUsageRow { Name = Name, BundleId = BundleId, Seconds = 1 };
    }

    readonly List<InputAction> _actions = new List<InputAction>();
    BossProfile[] _baseProfiles;
    AppVisual[] _looks;
    int _officeLook;
    readonly List<GameObject> _zakos = new List<GameObject>();
    Vector3 _playerStart;
    int _bossIndex = -1; // -1 = ボスなし
    int _officeTurn;
    int _steamBossNext;
    int _playerSteamNext;
    AchievementRunner _casterAchievements;
    string _log = "";
    float _logUntil;
    GUIStyle _style;
    GUIStyle _small;

    BossController CurrentBoss => _bossIndex >= 0 && _bosses != null && _bossIndex < _bosses.Length ? _bosses[_bossIndex] : null;

    InputAction Key(string name, params string[] keys)
    {
        var a = new InputAction(name, InputActionType.Button);
        foreach (var k in keys) a.AddBinding($"<Keyboard>/{k}");
        _actions.Add(a);
        return a;
    }

    InputAction _office, _unity, _steamBoss, _steamPlayer, _switchBoss, _officeLookKey, _gauge, _purify, _zako, _reset;

    void Awake()
    {
        _office = Key("DebugOfficeBoss", "t", "f1");
        _unity = Key("DebugUnityBoss", "y", "f2");
        _steamBoss = Key("DebugSteamBoss", "u", "f3");
        _steamPlayer = Key("DebugSteamPlayer", "i", "f4");
        _switchBoss = Key("DebugSwitchBoss", "b");
        _officeLookKey = Key("DebugOfficeLook", "o");
        _gauge = Key("DebugGauge", "n");
        _purify = Key("DebugPurify", "p");
        _zako = Key("DebugZako", "z");
        _reset = Key("DebugReset", "r");
        if (_player != null) _playerStart = _player.position;
        if (_bosses != null) foreach (var b in _bosses) if (b != null) b.gameObject.SetActive(false);
        if (_zakoTemplate != null) _zakoTemplate.SetActive(false);
    }

    void Start()
    {
        if (_player != null && _player.TryGetComponent(out PlayerSkills skills) && _playerSkills != null)
        {
            foreach (var s in _playerSkills) if (s != null) skills.TryAcquire(s);
        }
        if (_steamBossPassive != null && _bossCaster != null)
        {
            _casterAchievements = (AchievementRunner)_steamBossPassive.Attach(_bossCaster.gameObject);
        }
        AchievementRunner.AnyUnlocked += OnUnlocked;
        foreach (var b in _bosses) if (b != null) b.Normalized += OnNormalized;
        PrepareLooks();
    }

    /// <summary>ボスの見た目を、アプリのアイコンから作る（アイコンはまとめて1回で読む）</summary>
    void PrepareLooks()
    {
        if (_bosses == null) return;
        _baseProfiles = new BossProfile[_bosses.Length];
        _looks = new AppVisual[_bosses.Length];
        for (int i = 0; i < _bosses.Length; i++) _baseProfiles[i] = _bosses[i] != null ? _bosses[i].Profile : null;
        var rows = new List<AppUsageRow>();
        if (_officeApps != null) foreach (var a in _officeApps) rows.Add(a.Row);
        rows.Add(_unityApp.Row);
        rows.Add(_steamApp.Row);
        AppVisuals.Prepare(rows);
        if (_officeApps != null && _officeApps.Length > 0) ApplyLook(0, _officeApps[0]);
        ApplyLook(1, _unityApp);
        ApplyLook(2, _steamApp);
    }

    void ApplyLook(int index, LookApp app)
    {
        if (_bosses == null || index >= _bosses.Length || _bosses[index] == null) return;
        AppVisual visual = AppVisuals.Get(app.Row);
        if (visual == null) return;
        _looks[index] = visual;
        BossController boss = _bosses[index];
        AppBossLook.ApplyWindow(boss, visual, null, app.Name);
        BossProfile baseProfile = _baseProfiles[index];
        if (baseProfile == null) return;
        // ゲージの横のアイコンと名前（設定の複製に入れる。アセットは変えない）
        BossProfile copy = baseProfile.CreateScaledCopy(baseProfile.GaugeMax, baseProfile.Phase2.WaitAfterAttack, baseProfile.Reward);
        copy.SetLook(app.Name, visual.Icon12);
        boss.SetProfile(copy);
        if (!visual.HasRealIcon) Log($"{app.Name} のアイコンが見つからないので、仮のアイコンにしました");
    }

    void SwitchOfficeLook()
    {
        if (_officeApps == null || _officeApps.Length == 0 || _looks == null) return;
        _officeLook = (_officeLook + 1) % _officeApps.Length;
        ApplyLook(0, _officeApps[_officeLook]);
        Log($"Office のボスの見た目：{_officeApps[_officeLook].Name}");
    }

    void OnEnable() { foreach (var a in _actions) a.Enable(); }
    void OnDisable() { foreach (var a in _actions) a.Disable(); }

    void OnDestroy()
    {
        foreach (var a in _actions) a.Dispose();
        AchievementRunner.AnyUnlocked -= OnUnlocked;
        if (_bosses != null) foreach (var b in _bosses) if (b != null) b.Normalized -= OnNormalized;
    }

    void Update()
    {
        // 発射台はプレイヤーの方を向く（ボス版は「前方」がプレイヤー側）
        if (_bossCaster != null && _player != null) _bossCaster.Facing = _player.position.x >= _bossCaster.transform.position.x ? 1 : -1;
        if (_office.WasPressedThisFrame() && _officeBossAttacks != null && _officeBossAttacks.Length > 0)
        {
            AttackData a = _officeBossAttacks[_officeTurn % _officeBossAttacks.Length];
            if (_bossCaster.Run(a))
            {
                _officeTurn++;
                Log($"Office ボス版：{a.name}");
            }
        }
        if (_unity.WasPressedThisFrame() && _unityBossAttack != null && _bossCaster.Run(_unityBossAttack)) Log("Unity ボス版：生成予告 → 生成");
        if (_steamBoss.WasPressedThisFrame() && _casterAchievements != null)
        {
            int n = _casterAchievements.Tracker.Count;
            if (n > 0)
            {
                if (_steamBossNext >= n) { _casterAchievements.Restart(); _steamBossNext = 0; }
                _casterAchievements.ForceUnlock(_steamBossNext++);
            }
        }
        if (_steamPlayer.WasPressedThisFrame()) UnlockPlayerSteam();
        if (_switchBoss.WasPressedThisFrame()) SwitchBoss();
        if (_officeLookKey.WasPressedThisFrame()) SwitchOfficeLook();
        if (_gauge.WasPressedThisFrame() && CurrentBoss != null) CurrentBoss.Gauge.Add(10);
        if (_purify.WasPressedThisFrame() && CurrentBoss != null) CurrentBoss.Gauge.Add(CurrentBoss.Gauge.Max);
        if (_zako.WasPressedThisFrame()) SpawnZako();
        if (_reset.WasPressedThisFrame()) ResetAll();
    }

    void UnlockPlayerSteam()
    {
        AchievementRunner runner = _player != null ? _player.GetComponent<AchievementRunner>() : null;
        if (runner == null)
        {
            Log("Steam を装備していません（3の枠に入れてください）");
            return;
        }
        int n = runner.Tracker.Count;
        if (n == 0) return;
        if (_playerSteamNext >= n) { runner.Restart(); _playerSteamNext = 0; }
        runner.ForceUnlock(_playerSteamNext++);
    }

    void SwitchBoss()
    {
        if (_bosses == null || _bosses.Length == 0) return;
        if (CurrentBoss != null) CurrentBoss.gameObject.SetActive(false);
        _bossIndex++;
        if (_bossIndex >= _bosses.Length) _bossIndex = -1;
        if (CurrentBoss != null)
        {
            CurrentBoss.gameObject.SetActive(true); // OnEnable で最初から
            if (_player != null && CurrentBoss.TryGetComponent(out AttackRunner r)) r.Target = _player;
            Log($"ボス：{CurrentBoss.Profile.DisplayName}");
        }
        else Log("ボスなし");
        if (_bossCaster != null) _bossCaster.gameObject.SetActive(CurrentBoss == null); // ボスがいる間は発射台を隠す
    }

    void SpawnZako()
    {
        if (_zakoTemplate == null) return;
        _zakos.RemoveAll(z => z == null);
        var z = Instantiate(_zakoTemplate, _zakoSpawn, Quaternion.identity);
        z.SetActive(true);
        _zakos.Add(z);
    }

    void ResetAll()
    {
        if (CurrentBoss != null) CurrentBoss.ResetBoss();
        foreach (var z in _zakos) if (z != null) Destroy(z);
        _zakos.Clear();
        if (_bossCaster != null) _bossCaster.ClearSpawned();
        _casterAchievements?.Restart();
        _steamBossNext = 0;
        if (_player != null)
        {
            if (_player.TryGetComponent(out Health h)) h.ResetHealth();
            if (_player.TryGetComponent(out PlayerController c))
            {
                c.CanControl = true;
                c.SetVelocity(Vector2.zero);
            }
            if (_player.TryGetComponent(out KinematicBody2D body)) body.Warp(_playerStart);
            if (_player.TryGetComponent(out AchievementRunner runner)) runner.Restart();
            _playerSteamNext = 0;
        }
        Log("やり直し");
    }

    void OnUnlocked(AchievementRunner runner, AchievementDefinition def)
    {
        string who = runner.Team == Team.Player ? "プレイヤー" : "ボス";
        Log($"{who}の実績解除：{(def != null ? def.DisplayName : "?")}（バフ {runner.Passive.Buff.Duration} 秒）");
    }

    void OnNormalized(SkillDefinition reward) => Log(reward != null ? $"正常化！ スキル「{reward.DisplayName}」を獲得" : "正常化！");

    void Log(string message)
    {
        _log = message;
        _logUntil = Time.time + 3f;
        Debug.Log($"[OfficeUnitySteamTest] {message}");
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        float y = 8f;
        BossController boss = CurrentBoss;
        if (boss != null)
        {
            string state = boss.State == BossState.Normalized ? "正常化" : boss.State == BossState.Ejected ? "とどめ待ち（ウイルス本体を倒す）" : $"第{boss.Phase}形態";
            GUI.Label(new Rect(Screen.width - 460f, y, 450f, 22f), $"{boss.Profile.DisplayName}　{boss.Gauge.Current}/{boss.Gauge.Max}　{state}", _style);
            AppVisual look = _looks != null && _bossIndex >= 0 && _bossIndex < _looks.Length ? _looks[_bossIndex] : null;
            Sprite icon = boss.State == BossState.Normalized ? (look != null ? look.Icon24 : boss.NormalizedIcon) : null;
            if (icon != null) DrawSprite(new Rect(Screen.width - 72f, y + 26f, 64f, 64f), icon);
        }

        if (_player != null)
        {
            var stats = _player.GetComponent<StatModifiers>();
            if (stats != null && (stats.AttackMultiplier > 1f || stats.SpeedMultiplier > 1f))
            {
                GUI.Label(new Rect(10f, Screen.height - 72f, 600f, 20f), $"プレイヤーのバフ：攻撃 ×{stats.AttackMultiplier:0.00}　移動 ×{stats.SpeedMultiplier:0.00}", _small);
            }
            var exec = _player.GetComponent<PlayerSkillExecutor>();
            var skills = _player.GetComponent<PlayerSkills>();
            if (exec != null && skills != null)
            {
                for (int i = 0; i < skills.Capacity; i++)
                {
                    SkillDefinition s = skills.GetSkill(i);
                    if (s == null || !s.HasAttackCycle) continue;
                    AttackData next = exec.GetNextAttack(s);
                    GUI.Label(new Rect(10f, Screen.height - 54f, 600f, 20f), $"{i + 1}：{s.DisplayName} の次 → {(next != null ? next.name : "-")}", _small);
                }
            }
        }

        if (Time.time < _logUntil) GUI.Label(new Rect(10f, Screen.height * 0.25f, Screen.width - 20f, 24f), _log, _style);

        GUI.Label(new Rect(10f, Screen.height - 36f, Screen.width - 20f, 34f),
            "【確認用】1/2/3：Office・Unity・Steam（プレイヤー版）　T/F1：Office ボス版　Y/F2：Unity ボス版　U/F3：Steam ボス版の実績　I/F4：プレイヤーの実績\n" +
            "B：ボス切替　O：Office の見た目　N：ゲージ+10　P：ゲージ満タン　Z：雑魚　R：やり直し　J/F：近接　K：ダッシュ", _small);
    }

    static void DrawSprite(Rect r, Sprite s)
    {
        Texture2D tex = s.texture;
        Rect uv = new Rect(s.textureRect.x / tex.width, s.textureRect.y / tex.height, s.textureRect.width / tex.width, s.textureRect.height / tex.height);
        GUI.DrawTextureWithTexCoords(r, tex, uv);
    }
}
