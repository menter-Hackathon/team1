using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro のステージのキット（2026-11-19。docs/app-feedback-v2-design.md 5-6・1-2）。担当2 の物。
/// ・StagePacer.Started で、Premiere Pro のステージ（スキンが Premiere）なら、タイムラインのステージ（PremiereTimelineStage）を付ける
///   （見た目・再生ヘッドとマーカー。ボスの登場で決まりを片づけ、見た目は残す）
/// ・場面③の出来事を、危ない物を出さない「書き出し」（PremiereRenderEvent）に入れ替える（今の Rendering... の帯は、再生ヘッドと重なるので出さない）
///   2026-11-19（docs/stage-gimmick-variety-a.md Premiere Pro）：書き出しと同時に「波形」（PremiereWaveformEvent。床を走る波形の山・流れる床）を進める（MakeEvent）
/// ・ほかのアプリのステージでは、何も付けない。StagePaceTable.Enabled = false（StagePacer が無い）のときも、何もしない
/// </summary>
public static class PremiereKit
{
    public static readonly string[] Names = { "Premiere Pro", "Adobe Premiere Pro", "Premiere" };
    public static readonly string[] Bundles = { "com.adobe.PremierePro" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
        AppStageEvents.Register("PremierePro", Names, Bundles, (stage, final) => MakeEvent());
    }

    static void OnStarted(StagePacer pacer)
    {
        if (pacer == null || !IsPremiere(pacer)) return;
        PremiereTimelineStage.Attach(pacer);
        // スキンで Premiere と分かったのに、出来事の表（名前・バンドルID）に当たらないときは、そのステージの名前を足して登録し直す
        // （今の Rendering... の帯が再生ヘッドと重ならないように）
        if (!AppStageEvents.Has(pacer.AppName, pacer.BundleId))
        {
            var names = new System.Collections.Generic.List<string>(Names);
            var bundles = new System.Collections.Generic.List<string>(Bundles);
            if (!string.IsNullOrEmpty(pacer.AppName)) names.Add(pacer.AppName);
            if (!string.IsNullOrEmpty(pacer.BundleId)) bundles.Add(pacer.BundleId);
            AppStageEvents.Register("PremierePro", names.ToArray(), bundles.ToArray(), (stage, final) => MakeEvent());
        }
    }

    /// <summary>【仮】場面③の出来事：書き出し＋波形（2026-11-19）。2つを同時に進める（始まりの通知 Rendering... と終わりの光は 1回）</summary>
    public static StageEvent MakeEvent() => new ComboEvent("Rendering...", new PremiereRenderEvent(), new PremiereWaveformEvent());

    /// <summary>Premiere Pro のステージか（スキンが Premiere。スキンの表の専用の行と同じ決め方）</summary>
    public static bool IsPremiere(StagePacer pacer)
    {
        if (pacer.Skin == SkinId.Premiere) return true;
        var row = new AppUsageRow { Name = pacer.AppName ?? "", BundleId = pacer.BundleId ?? "", Seconds = 1 };
        return AppMatch.Matches(row, Names, Bundles);
    }
}

/// <summary>
/// 【仮】Premiere Pro の場面③の出来事「書き出し」（2026-11-19。docs/app-feedback-v2-design.md 5-6）。危ない物は出さない。
/// 隅の通知 Rendering...（今のまま）。間は、タイムラインのステージが書き出しのウィンドウを出し、マーカーを通りごとに +1、再生ヘッドを2倍にする。
/// 雑魚は今までどおり StagePacer が出す
/// </summary>
public class PremiereRenderEvent : PaceEvent
{
    /// <summary>【仮】書き出しの長さ（秒。始まりの予告 0.8秒のあとから）</summary>
    const float Seconds = 12f;

    float _clock;

    public PremiereRenderEvent()
    {
        Toast = "Rendering...";
    }

    protected override bool NeedsRunner => false;

    protected override void OnBegin()
    {
        _clock = 0f;
    }

    protected override bool OnTick(float dt)
    {
        if (_clock <= 0f && PremiereTimelineStage.Current != null) PremiereTimelineStage.Current.BeginExport(Seconds);
        _clock += dt;
        return _clock < Seconds;
    }

    protected override void OnEnd(bool byBoss)
    {
        if (PremiereTimelineStage.Current != null) PremiereTimelineStage.Current.EndExport(!byBoss && _clock >= Seconds - 0.05f);
    }
}
