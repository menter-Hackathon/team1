using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1回のプレイ（ラン）の進行。シーンをまたいで残る（DontDestroyOnLoad）。
/// ・タイトル：StartRun(読み込み方) → 使用データを読み、ステージの計画を作って、最初のステージへ
/// ・ステージ：StageDirector がボスを出し、正常化したら CompleteStage、ライフ0なら FailStage を呼ぶ
/// ・CompleteStage：次のステージへ。最後ならリザルトへ
/// ・FailStage：今のステージを最初からやり直す（仕様書 2.4）。ライフは全快、能力はステージを始めたときの状態、クールタイムはリセット
/// ・シーンの切り替えは画面を暗くしてから行う（文字は出さない）
/// UI 担当は、ここのイベントと Run（ラン状態）を見て画面を作る。
/// </summary>
public class GameSession : MonoBehaviour
{
    [SerializeField] AppCatalog _catalog;
    [SerializeField] string _titleScene = "RunTitle";
    [SerializeField] string _resultScene = "RunResult";
    [Tooltip("画面を暗くする・明るくする秒数")]
    [SerializeField, Min(0f)] float _fadeTime = 0.4f;

    float _fade; // 0＝見える、1＝真っ暗
    Texture2D _black;

    public static GameSession Instance { get; private set; }
    public AppCatalog Catalog => _catalog;
    public RunState Run { get; private set; }
    /// <summary>シーンを切り替えている途中か</summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>ランを始めたとき（ステージの計画が決まった）</summary>
    public event Action<RunState> RunStarted;
    /// <summary>ステージが始まったとき（リトライも含む）</summary>
    public event Action<StagePlan> StageStarted;
    /// <summary>ステージをクリアしたとき（正常化したアプリ, 手に入れた能力）</summary>
    public event Action<StagePlan, SkillDefinition> StageCleared;
    /// <summary>ライフが0になったとき（このあと同じステージをやり直す）</summary>
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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>無ければ作る（ステージのシーンを直接再生したときの開発用）</summary>
    public static GameSession EnsureExists(AppCatalog catalog)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GameSession");
        var session = go.AddComponent<GameSession>();
        session._catalog = catalog;
        return session;
    }

    /// <summary>使用データを読み込んでランを始め、最初のステージに進む。対応アプリが無ければ false</summary>
    public bool StartRun(IAppUsageProvider provider, out string error)
    {
        if (!PrepareRun(provider, out error)) return false;
        LoadCurrentStage();
        return true;
    }

    /// <summary>ランの計画だけを作る（シーンは切り替えない）。ステージを直接再生したときに使う</summary>
    public bool PrepareRun(IAppUsageProvider provider, out string error)
    {
        if (IsTransitioning) { error = "切り替え中です"; return false; }
        if (_catalog == null) { error = "対応アプリ表（AppCatalog）が設定されていません"; return false; }
        if (provider == null) { error = "読み込み方が指定されていません"; return false; }
        if (!provider.TryLoad(out List<AppUsage> usages, out error)) return false;

        List<StagePlan> plans = RunPlanner.Plan(_catalog, usages);
        if (plans.Count == 0)
        {
            error = "対応アプリが見つかりません";
            return false;
        }

        Run = new RunState(plans);
        Run.StageStartSlots = Run.Loadout.Snapshot();
        error = null;
        RunStarted?.Invoke(Run);
        return true;
    }

    /// <summary>StageDirector が呼ぶ：ステージが始まった</summary>
    public void NotifyStageStarted()
    {
        if (Run?.Current != null) StageStarted?.Invoke(Run.Current);
    }

    /// <summary>ステージをクリアした（ボスが正常化した）。次のステージかリザルトへ</summary>
    public void CompleteStage(SkillDefinition reward)
    {
        if (IsTransitioning || Run?.Current == null) return;
        StagePlan stage = Run.Current;
        Run.Cleared.Add(new ClearRecord { Stage = stage, Reward = reward });
        StageCleared?.Invoke(stage, reward);

        Run.CurrentIndex++;
        if (Run.IsFinished)
        {
            RunCompleted?.Invoke(Run);
            StartCoroutine(Transition(_resultScene));
        }
        else
        {
            LoadCurrentStage();
        }
    }

    /// <summary>ライフが0になった。今のステージを最初からやり直す</summary>
    public void FailStage()
    {
        if (IsTransitioning || Run?.Current == null) return;
        GameOver?.Invoke(Run.Current);
        Run.RetryCount++;
        Run.Loadout.Restore(Run.StageStartSlots);
        StartCoroutine(Transition(Run.Current.App.StageScene));
    }

    /// <summary>タイトルに戻る（ランは終わり）</summary>
    public void ReturnToTitle()
    {
        if (IsTransitioning) return;
        Run = null;
        StartCoroutine(Transition(_titleScene));
    }

    void LoadCurrentStage()
    {
        Run.StageStartSlots = Run.Loadout.Snapshot();
        StartCoroutine(Transition(Run.Current.App.StageScene));
    }

    IEnumerator Transition(string sceneName)
    {
        IsTransitioning = true;
        for (float t = 0f; t < _fadeTime; t += Time.unscaledDeltaTime)
        {
            _fade = t / _fadeTime;
            yield return null;
        }
        _fade = 1f;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"シーン「{sceneName}」を読み込めません。Build Settings に登録されているか確認してください", this);
            _fade = 0f;
            IsTransitioning = false;
            yield break;
        }
        while (!op.isDone) yield return null;

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
