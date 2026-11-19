using System;
using UnityEngine;

/// <summary>scroll：range だけ上（下）へ speed で流れる → フェードして消える → 元の場所にフェードして出る（offset でずらす）</summary>
[DefaultExecutionOrder(-200)]
public class AppPlatformScroll : AppPlatform
{
    const float FadeOut = 0.4f, FadeIn = 0.5f;
    public override bool IsSteady => false;
    public override bool IsSolidFor(float seconds) => false;

    void FixedUpdate()
    {
        float speed = AppStageParams.Pos(P.speed, 0.6f);
        float range = AppStageParams.Pos(P.range, 2f, 0.1f);
        float dir = string.Equals(P.dir, "down", StringComparison.OrdinalIgnoreCase) ? -1f : 1f;
        float travel = range / speed;
        float cycle = travel + FadeOut + FadeIn;
        float t = Mathf.Repeat(Time.fixedTime - StartFixedTime + AppStageParams.Num(P.offset), cycle);
        if (t < travel)
        {
            SetSolid(true);
            SetAlpha(1f);
            MoveBody(Home + Vector2.up * (dir * speed * t), true);
        }
        else if (t < travel + FadeOut)
        {
            SetSolid(false);
            SetAlpha(1f - (t - travel) / FadeOut);
            MoveBody(Home + Vector2.up * (dir * range), false);
        }
        else
        {
            SetSolid(false);
            MoveBody(Home, false);
            SetAlpha((t - travel - FadeOut) / FadeIn);
        }
    }
}
