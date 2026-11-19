using UnityEngine;

/// <summary>blink：on 秒出ている（最後の warn 秒は点滅）→ off 秒消える（うすい形だけ。乗れない）。offset でずらす</summary>
public class AppPlatformBlink : AppPlatform
{
    bool _warning;
    public override bool IsSteady => false;

    float On => AppStageParams.Pos(P.on, 2.5f, 0.2f);
    float Off => AppStageParams.Pos(P.off, 1.5f, 0.2f);
    float Phase => Mathf.Repeat(Time.time - StartTime + AppStageParams.Num(P.offset), On + Off);

    public override bool IsSolidFor(float seconds)
    {
        float t = Phase;
        return t < On && On - t >= seconds;
    }

    // 最初のフレームまでは出ている（ほかの部品の Start で、足場として見つかるように）
    void Update()
    {
        float on = On;
        float warn = Mathf.Clamp(AppStageParams.Pos(P.warn, 0.6f, 0.1f), 0f, on);
        float t = Phase;
        bool open = t < on;
        bool warning = open && t >= on - warn;
        if (warning && !_warning) GameAudio.Event("ui_plat_warn");
        _warning = warning;
        SetSolid(open);
        SetAlpha(!open ? GhostAlpha : warning && Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0 ? 0.35f : 1f);
    }
}
