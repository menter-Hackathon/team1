using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】Terminal のステージのギミック「コマンドで進むステージ」（2026-11-19。docs/app-feedback-v2-design.md 6-4。担当3）。
/// ラスボス Terminal のステージの雑魚の時間だけ。ボス戦には何も足さない。StagePacer.Started を購読して、自分で付く。
/// ・画面の上のまん中にコマンドの行（ボスの技と同じ TerminalCommandLine）。場面が変わるたびに1行打たれる（飾り。場面の切り替えは StagePacer のまま）
///   ① 入り ls /virus：置いてある雑魚の頭の上に、番号の札 [1] [2]… が 1秒出る
///   ② 盛り上がり ./virus.sh &amp;：雑魚が出るたびに、行の下に [n] 4021 のような1行が流れる
///   ③ 出来事 sudo reboot：今の出来事（床の全部。予告が長い）。2026-11-19（docs/stage-gimmick-variety-a.md Terminal）：前半に kill -9 を 2回足し、
///      床の全部は後半に 1回にする（TerminalKillEvent.Make を AppStageEvents に登録。行は kill -9 * → sudo reboot と打ち直す）
///   ⑤ 最高潮 yes virus：行の下に virus の字が流れ続ける
///   ボスの登場 clear：行と札が全部消える
/// ・雑魚を倒したとき：倒れた所に、緑の小さい字 killed（0.4秒）
/// ・床が1区画ずつ消える（② と ⑤ の間。ボスの技 rm -rf の予習）：7秒ごとに、行が rm floor/3 に変わり、ランダムな1区画が
///   赤い点滅 1.2秒 → くずれる 0.3秒 → 文字化けの床 2.5秒 → 戻る。今いる区画は2回に1回だけ選ぶ。③ の間と、そのあと 2秒は出さない
///   （③ が始まったら、出ている区画はすぐ戻す）。絵と判定はボスの技の部品（TerminalRmRfEffect）を、区画1つで使い回す
/// ・B：右上（ゲージの下）に top の表。暗い小さいウィンドウに、残りの雑魚の数だけ行が並び、倒すと1行ずつ消える（飾り。右上のゲージと同じ意味）。
///   右上の通知（CornerToast）と同じ場所なので、通知が出ている間はかくす
/// ボスの登場（PacePhase.Done）とシーンが変わるときに、出した物を全部消す。
/// 能力 rm -rf のカード：AbilityCatalog が決める（図鑑で倒していれば1枚だけ。倒す前は出さない。6-3。2026-11-19 担当0 が移した）
/// </summary>
public class TerminalKit : MonoBehaviour
{
    // ───────── キットの名前（Editor の TerminalAppKit も使う） ─────────

    public const string Label = "Terminal";
    public static string[] AppNames => new[] { "Terminal", "ターミナル" };
    public static string[] BundleIds => new[] { "com.apple.Terminal" };

    // ───────── 数値【仮】 ─────────

    const float RmInterval = 7f;          // 床を消す間隔
    const float RmFirstInBuild = 4f;      // ② に入ってから1回目まで
    const float RmFirstInClimax = 2.5f;   // ⑤ に入ってから1回目まで（③ のあと 2秒は出さない）
    const float RmBlink = 1.2f;
    const float RmCrumble = 0.3f;
    const float RmLinger = 2.5f;
    const int Sections = 5;
    const float TagLife = 1f;             // 番号の札
    const float KilledLife = 0.4f;
    const float YesInterval = 0.12f;      // yes virus の行が流れる間隔
    const int OutputRows = 2;
    const int TextOrder = 55;
    const int TopRows = 6;                // top の表に並べる行の数（多い分は +n）
    const float TopW = 112f;              // top の表の幅（PixelGui のドット）
    const float TopY = 62f;               // top の表の上の端（右上の STAGE とゲージの下）
    const float TopRowH = 9f;

    // ───────── 実行時に付く ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnPacerStarted;
        StagePacer.Started += OnPacerStarted;
        // 場面③：kill -9 ＋ sudo reboot（ラスボスのステージなので StageEvents ではなく、直接登録。作れなければ表の sudo reboot のまま）
        AppStageEvents.Register(Label, AppNames, BundleIds, (stage, isFinal) => TerminalKillEvent.Make());
        // 能力のカード（倒す前は出さない・倒したら1枚だけ）は AbilityCatalog が決める（2026-11-19 担当0。ここでスイッチは切らない）
    }

    static void OnPacerStarted(StagePacer pacer)
    {
        if (pacer == null || !IsTerminal(pacer) || pacer.GetComponent<TerminalKit>() != null) return;
        pacer.gameObject.AddComponent<TerminalKit>().Setup(pacer);
        // スキンで Terminal と分かったのに、出来事の表（名前・バンドルID）に当たらないときは、そのステージの名前を足して登録し直す（PremiereKit と同じ）
        if (!AppStageEvents.Has(pacer.AppName, pacer.BundleId))
        {
            var names = new List<string>(AppNames);
            var bundles = new List<string>(BundleIds);
            if (!string.IsNullOrEmpty(pacer.AppName)) names.Add(pacer.AppName);
            if (!string.IsNullOrEmpty(pacer.BundleId)) bundles.Add(pacer.BundleId);
            AppStageEvents.Register(Label, names.ToArray(), bundles.ToArray(), (stage, isFinal) => TerminalKillEvent.Make());
        }
    }

    /// <summary>【仮】2026-11-19：コマンドの行を打ち直す（出来事の kill -9 * など）。床を消している間・Terminal のステージでなければ何もしない</summary>
    public static void Command(string command)
    {
        StagePacer p = StagePacer.Current;
        TerminalKit kit = p != null ? p.GetComponent<TerminalKit>() : null;
        if (kit == null || kit._done || kit._line == null || kit._rmEndAt >= 0f) return;
        GameAudio.Event("gm_terminal_cmd");
        kit._line.Retype(command);
    }

    static bool IsTerminal(StagePacer p)
    {
        if (p.Skin == SkinId.Terminal) return true;
        var row = new AppUsageRow { Name = p.AppName ?? "", BundleId = p.BundleId ?? "", Seconds = 1 };
        return AppMatch.Matches(row, AppNames, BundleIds);
    }

    // ───────── 中身 ─────────

    StagePacer _pacer;
    AttackRunner _runner;          // コマンドの行・1区画の床の持ち主（出来事用の AttackRunner のプレハブ）
    bool _canHurt;                 // プレハブがあった（当てる相手が入っている）。無ければ床は出さない
    TerminalCommandLine _line;
    TerminalRmRfEffect _floor;
    readonly List<GameObject> _tags = new List<GameObject>();
    PacePhase _phase;
    string _phaseCommand = "";
    bool _tagsShown;
    int _lastSpawned;
    int _job;
    float _nextYes;
    float _nextRmAt = -1f;
    float _rmEndAt = -1f;
    int _rmCount;
    bool _done;
    float _clearAt = -1f;

    readonly List<int> _pids = new List<int>();   // top の表の行（残りの雑魚の数だけ）
    bool _toastShowing;                            // 右上の通知（CornerToast）が出ている間は、top の表を出さない（重なるので）
    float _nextToastCheck;

    static AttackData _floorData;
    static TerminalRmRfAttack _floorCfg;

    void Setup(StagePacer pacer)
    {
        _pacer = pacer;
        AttackRunner prefab = Resources.Load<AttackRunner>(StagePaceTable.HazardRunnerPath);
        if (prefab != null)
        {
            _runner = Instantiate(prefab, pacer.PlayArea.center, Quaternion.identity, transform);
            _canHurt = true;
        }
        else
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            _runner = go.AddComponent<AttackRunner>(); // 飾りだけ（当てる相手が無い）
        }
        _runner.name = "TerminalKitRunner";
        _runner.Target = pacer.Player;
        _runner.Skin = pacer.Skin;
        _runner.SkinTheme = pacer.Theme;
        _runner.SkinIcon = pacer.Icon;

        _line = TerminalCommandLine.Spawn(_runner, "", 100000f);
        if (_line != null) _line.SetOutputRows(OutputRows);
        pacer.PhaseChanged += OnPhase;
        Health.AnyDied += OnAnyDied;
        _lastSpawned = pacer.Spawner != null ? pacer.Spawner.Spawned : 0;
        Enter(pacer.Phase);
    }

    void OnDestroy()
    {
        Unhook();
        if (_runner != null)
        {
            _runner.Cancel();
            _runner.ClearSpawned();
        }
        ClearTags();
    }

    void Unhook()
    {
        if (_pacer != null) _pacer.PhaseChanged -= OnPhase;
        Health.AnyDied -= OnAnyDied;
    }

    void OnPhase(PacePhase phase) => Enter(phase);

    void Enter(PacePhase phase)
    {
        if (_done) return;
        _phase = phase;
        if (_line != null) _line.ClearOutput();
        switch (phase)
        {
            case PacePhase.Intro:
                Type("ls /virus");
                _tagsShown = false;
                _nextRmAt = -1f;
                break;
            case PacePhase.Build:
                Type("./virus.sh &");
                if (_pacer.Spawner != null) _lastSpawned = _pacer.Spawner.Spawned;
                _nextRmAt = Time.time + RmFirstInBuild;
                break;
            case PacePhase.Event:
                Type("sudo reboot");
                StopFloor(); // ③ の間は出さない（出ていたら、すぐ戻す）
                _nextRmAt = -1f;
                break;
            case PacePhase.Rest:
                _nextRmAt = -1f;
                break;
            case PacePhase.Climax:
                Type("yes virus");
                _nextYes = Time.time + 0.6f; // 打ち終わってから流れ始める
                _nextRmAt = Time.time + RmFirstInClimax;
                break;
            case PacePhase.Done:
                Finish();
                break;
        }
    }

    /// <summary>場面のコマンドを打つ（床を消している間は、戻ってから打ち直す）</summary>
    void Type(string command)
    {
        _phaseCommand = command;
        GameAudio.Event("gm_terminal_cmd");
        if (_line != null && _rmEndAt < 0f) _line.Retype(command);
    }

    void Update()
    {
        if (_done)
        {
            if (_clearAt >= 0f && Time.time >= _clearAt) Cleanup();
            return;
        }
        if (_pacer == null) return;
        if (Time.time >= _nextToastCheck)
        {
            _nextToastCheck = Time.time + 0.2f;
            _toastShowing = FindObjectOfType<CornerToast>() != null;
        }

        switch (_phase)
        {
            case PacePhase.Intro:
                if (!_tagsShown && _line != null && _line.Typed)
                {
                    _tagsShown = true;
                    GameAudio.Event("gm_terminal_ls");
                    ShowTags();
                }
                break;
            case PacePhase.Build:
                JobLines();
                break;
            case PacePhase.Climax:
                if (Time.time >= _nextYes && _line != null && _rmEndAt < 0f)
                {
                    _line.AddOutput("virus");
                    _nextYes = Time.time + YesInterval;
                }
                break;
        }

        if ((_phase == PacePhase.Build || _phase == PacePhase.Climax) && _nextRmAt >= 0f && Time.time >= _nextRmAt)
        {
            _nextRmAt = Time.time + RmInterval;
            StartRm();
        }
        if (_rmEndAt >= 0f && Time.time >= _rmEndAt)
        {
            _rmEndAt = -1f;
            GameAudio.Event("gm_terminal_rm_restore");
            if (_line != null) _line.Retype(_phaseCommand); // 床が戻ったら、場面のコマンドに戻る
        }
    }

    // ───────── ① 番号の札 ─────────

    void ShowTags()
    {
        var list = new List<Health>();
        foreach (Health h in FindObjectsOfType<Health>())
        {
            if (h == null || h.IsDead || !h.isActiveAndEnabled || h.Team != Team.Enemy) continue;
            if (h.GetComponentInParent<BossController>() != null) continue;
            list.Add(h);
        }
        list.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < list.Count; i++)
        {
            Vector2 head = FxKit.HeadOf(list[i].gameObject, (Vector2)list[i].transform.position + Vector2.up);
            FxAnim a = FxKit.Text($"[{i + 1}]", head + new Vector2(0f, 0.45f), 0.4f, TerminalArt.Green, TextOrder, TagLife, TerminalArt.Black).Pop().FadeFrom(0.75f);
            _tags.Add(a.gameObject);
        }
    }

    void ClearTags()
    {
        foreach (var t in _tags) if (t != null) Destroy(t);
        _tags.Clear();
    }

    // ───────── ② ジョブの行 ─────────

    void JobLines()
    {
        ZakoSpawner spawner = _pacer.Spawner;
        if (spawner == null || _line == null) return;
        while (_lastSpawned < spawner.Spawned)
        {
            _lastSpawned++;
            _job++;
            _line.AddOutput($"[{_job}] {UnityEngine.Random.Range(1000, 9999)}");
        }
    }

    // ───────── 倒したとき killed ─────────

    void OnAnyDied(Health h)
    {
        if (_done || h == null || h.Team != Team.Enemy || h.GetComponentInParent<BossController>() != null) return;
        Vector2 p = FxKit.HeadOf(h.gameObject, (Vector2)h.transform.position + Vector2.up * 0.5f);
        FxKit.Text("killed", p + new Vector2(0f, 0.2f), 0.4f, TerminalArt.Green, TextOrder, KilledLife, TerminalArt.Black).Rise(0.3f).FadeFrom(0.5f);
    }

    // ───────── 床が1区画ずつ消える ─────────

    void StartRm()
    {
        if (!_canHurt || _runner == null || _floor != null) return;
        Rect arena = BattleArena.GetBounds();
        float w = arena.width / Sections;
        float px = _pacer.Player != null ? _pacer.Player.position.x : arena.center.x;
        int playerSection = Mathf.Clamp(Mathf.FloorToInt((px - arena.xMin) / w), 0, Sections - 1);
        _rmCount++;
        bool allowPlayer = _rmCount % 2 == 0; // 今いる区画は2回に1回だけ
        var choices = new List<int>();
        for (int i = 0; i < Sections; i++) if (allowPlayer || i != playerSection) choices.Add(i);
        int index = choices[UnityEngine.Random.Range(0, choices.Count)];

        EnsureFloorAssets();
        _floor = TerminalRmRfEffect.Spawn(_runner, _floorData, _floorCfg, new[] { index });
        if (_floor == null) return;
        GameAudio.Event("gm_terminal_rm_floor");
        if (_line != null) _line.Retype($"rm floor/{index + 1}");
        _rmEndAt = Time.time + _floorCfg.RestoreAtFor(1) + 0.1f;
    }

    void StopFloor()
    {
        if (_floor != null) Destroy(_floor.gameObject);
        _floor = null;
        if (_rmEndAt >= 0f)
        {
            _rmEndAt = -1f;
            if (_line != null) _line.Retype(_phaseCommand);
        }
    }

    /// <summary>1区画の床の数値と判定のデータ（その場で作る。アセットにしない）。名前でエラーの字が No such file になる</summary>
    static void EnsureFloorAssets()
    {
        if (_floorCfg == null) _floorCfg = TerminalRmRfAttack.CreateRuntime(RmBlink, RmCrumble, RmLinger, Sections);
        if (_floorData == null)
        {
            _floorData = ScriptableObject.CreateInstance<AttackData>(); // ダメージ 1・ノックバックは初期値
            _floorData.name = "Attack_Terminal_RmRf";
            _floorData.hideFlags = HideFlags.DontSave;
        }
    }

    // ───────── B：top の表 ─────────

    void OnGUI()
    {
        if (_done || _pacer == null || _pacer.Remaining < 0 || _toastShowing || TerminalArt.GuiHidden()) return;
        SyncPids(_pacer.Remaining);
        GUI.depth = 6; // 画面表示（PixelHud）より手前、コマンドの行より奥
        PixelGui.Begin(false);
        int shown = Mathf.Min(_pids.Count, TopRows);
        bool more = _pids.Count > TopRows;
        float h = (shown + 1 + (more ? 1 : 0)) * TopRowH + 4f + 3f + MacUi.TitleH;
        var win = new Rect(Mathf.Round(PixelGui.Width - 6f - TopW), TopY, TopW, Mathf.Round(h));
        Rect r = MacUi.Gui.Window(win, "top", MacLook.Dark);
        PixelGui.Fill(r, TerminalArt.LineBg);
        GUIStyle style = PixelGui.Style(true, 8);
        float y = r.y + 2f;
        PixelGui.Draw(new Rect(r.x + 4f, y, r.width - 8f, 8f), "PID  COMMAND", style, TerminalArt.Gray);
        y += TopRowH;
        for (int i = 0; i < shown; i++, y += TopRowH)
            PixelGui.Draw(new Rect(r.x + 4f, y, r.width - 8f, 8f), $"{_pids[i]} virus", style, TerminalArt.Green);
        if (more) PixelGui.Draw(new Rect(r.x + 4f, y, r.width - 8f, 8f), $"+{_pids.Count - TopRows}", style, TerminalArt.Gray);
        PixelGui.End();
    }

    /// <summary>行の数を、残りの雑魚の数に合わせる（減るときは、どこかの行が1行消える）</summary>
    void SyncPids(int remaining)
    {
        remaining = Mathf.Max(0, remaining);
        while (_pids.Count > remaining) _pids.RemoveAt(UnityEngine.Random.Range(0, _pids.Count));
        while (_pids.Count < remaining) _pids.Add(UnityEngine.Random.Range(1000, 9999));
    }

    // ───────── ボスの登場：clear ─────────

    void Finish()
    {
        _done = true;
        Unhook();
        StopFloor();
        ClearTags();
        _nextRmAt = -1f;
        if (_line != null)
        {
            _line.ClearOutput();
            _line.Retype("clear");
            _clearAt = Time.time + TerminalCommandLine.TypingTime("clear") + 0.05f;
        }
        else Cleanup();
    }

    /// <summary>出した物を全部消す（コマンドの行・床・くずれる字）</summary>
    void Cleanup()
    {
        _clearAt = -1f;
        if (_runner != null)
        {
            _runner.Cancel();
            _runner.ClearSpawned();
            Destroy(_runner.gameObject);
        }
        _runner = null;
        _line = null;
        Destroy(this);
    }
}
