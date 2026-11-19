using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ヒットストップの入口（2026-11-18。docs/app-skill-visuals-design.md 1-3。まさぴ〜：入れる）。
/// ・ゲームの時間の速さ（Time.timeScale）を一瞬だけ小さくして、実時間で戻す
/// ・時間の速さが 1 でないとき（暗転・デスクトップの画面・ゲームオーバー・再起動の演出の途中）は、何もしない
/// ・戻すのは、時間の速さが「自分が入れた値のまま」のときだけ（止めている間にゲームオーバーなどが変えたら、さわらない）
/// ・止まっている間に次の頼みが来たら、長い方に合わせる（足さない）
/// ・1秒の間に止める合計は 0.2秒まで。多い分は捨てる（連続で当たる能力で、ゲームが重く見えないように）
/// 当たり判定・ダメージは変えない。技の秒数は、止まった分だけ実時間で伸びる
/// </summary>
public static class HitStop
{
    /// <summary>止めている間の時間の速さ（0 にはしない：0 で割る所があっても困らないように）</summary>
    public const float StopScale = 0.02f;
    /// <summary>1秒の間に止めてよい合計（実時間の秒）</summary>
    public const float BudgetPerSecond = 0.2f;

    static bool _active;
    static float _endReal;
    static readonly List<Vector2> _spent = new List<Vector2>(); // (いつ, 何秒)
    static HitStopRunner _runner;

    /// <summary>今、止めているか</summary>
    public static bool IsStopping => _active && Mathf.Approximately(Time.timeScale, StopScale);

    /// <summary>
    /// 実時間で seconds 秒だけ止める。止めたら true。
    /// ignoreBudget：1秒に 0.2秒までの決まりを使わない（形態変化の演出など、1回きりの物）
    /// </summary>
    public static bool Request(float seconds, bool ignoreBudget = false)
    {
        if (seconds <= 0.005f) return false;
        GameSession s = GameSession.Instance;
        if (s != null && (s.IsTransitioning || s.IsGameOver || s.IsOnDesktop)) return false;
        float now = Time.unscaledTime;
        EnsureRunner();

        if (IsStopping)
        {
            // 止めている途中：長い方に合わせる（伸ばした分だけ数える）
            float extend = now + seconds - _endReal;
            if (extend <= 0f) return true;
            float granted = ignoreBudget ? extend : Mathf.Min(extend, Remaining(now));
            if (granted <= 0.005f) return true;
            _endReal += granted;
            if (!ignoreBudget) _spent.Add(new Vector2(now, granted));
            return true;
        }
        _active = false;
        if (!Mathf.Approximately(Time.timeScale, 1f)) return false; // ほかの所が時間を変えている

        float g = ignoreBudget ? seconds : Mathf.Min(seconds, Remaining(now));
        if (g <= 0.01f) return false;
        Time.timeScale = StopScale;
        _active = true;
        _endReal = now + g;
        if (!ignoreBudget) _spent.Add(new Vector2(now, g));
        return true;
    }

    /// <summary>止めているなら、すぐ戻す（自分の値のままのときだけ）</summary>
    public static void Release()
    {
        if (!_active) return;
        _active = false;
        if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
    }

    static float Remaining(float now)
    {
        float sum = 0f;
        for (int i = _spent.Count - 1; i >= 0; i--)
        {
            if (now - _spent[i].x > 1f) _spent.RemoveAt(i);
            else sum += _spent[i].y;
        }
        return Mathf.Max(0f, BudgetPerSecond - sum);
    }

    /// <summary>毎フレーム（実時間）呼ばれる：時間が来たら戻す。ほかの所が時間を変えていたら、手を離す</summary>
    internal static void Tick()
    {
        if (!_active) return;
        if (!Mathf.Approximately(Time.timeScale, StopScale))
        {
            _active = false; // ゲームオーバー・暗転などが変えた。さわらない
            return;
        }
        if (Time.unscaledTime >= _endReal) Release();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _active = false;
        _endReal = 0f;
        _spent.Clear();
        _runner = null;
    }

    static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("HitStop");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<HitStopRunner>();
    }
}

/// <summary>【仮】HitStop を実時間で進める（シーンをまたいで残る）</summary>
public class HitStopRunner : MonoBehaviour
{
    void Update() => HitStop.Tick();
    void OnDisable() => HitStop.Release();
}
